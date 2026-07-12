# Observability Troubleshooting Guide

This document describes common troubleshooting scenarios for local observability in the Order System Messaging project.

It covers:

    - Aspire Dashboard
    - OpenTelemetry traces
    - custom metrics
    - correlation IDs
    - RabbitMQ trace context
    - outbox publishing
    - smoke test failures
    - Keycloak token issues
    - Docker Compose diagnostics

## Quick verification checklist

Run these commands first:

    dotnet build OrderSystemMessaging.slnx
    dotnet test OrderSystemMessaging.slnx
    docker compose ps
    curl -i http://localhost:5080/health/ready

Expected result:

    - solution builds
    - tests pass
    - all important containers are running
    - gateway readiness is Healthy

Then run the observability smoke test:

    pwsh ./scripts/observability-smoke-test.ps1 \
      -AccessToken "$CUSTOMER_TOKEN" \
      -SetupAccessToken "$ADMIN_TOKEN" \
      -VerifyDockerLogs

Expected final output:

    Observability smoke test completed successfully.

## Required local containers

Expected containers:

    api-gateway
    keycloak
    keycloak-db
    orders-api
    inventory-api
    notifications-api
    orders-db
    inventory-db
    notifications-db
    rabbitmq
    aspire-dashboard

Check:

    docker compose ps

If a container is missing or unhealthy, inspect logs:

    docker compose logs api-gateway --tail=200
    docker compose logs orders-api --tail=200
    docker compose logs inventory-api --tail=200
    docker compose logs notifications-api --tail=200
    docker compose logs keycloak --tail=200
    docker compose logs rabbitmq --tail=200
    docker compose logs aspire-dashboard --tail=200

## Aspire Dashboard is empty

Aspire Dashboard URL:

    http://localhost:18888

Check the container:

    docker compose ps aspire-dashboard

Check logs:

    docker compose logs aspire-dashboard --tail=200

Check OpenTelemetry environment variables in services:

    docker compose exec orders-api printenv | grep OTEL
    docker compose exec inventory-api printenv | grep OTEL
    docker compose exec notifications-api printenv | grep OTEL

Expected values:

    OTEL_EXPORTER_OTLP_ENDPOINT=http://aspire-dashboard:18889
    OTEL_EXPORTER_OTLP_PROTOCOL=grpc

If Aspire is still empty, generate traffic:

    pwsh ./scripts/observability-smoke-test.ps1 \
      -AccessToken "$CUSTOMER_TOKEN" \
      -SetupAccessToken "$ADMIN_TOKEN" \
      -VerifyDockerLogs

Then refresh:

    http://localhost:18888

## Aspire shows traces but no custom metrics

First generate traffic. Custom metrics are emitted only when the related code path runs.

Run:

    pwsh ./scripts/observability-smoke-test.ps1 \
      -AccessToken "$CUSTOMER_TOKEN" \
      -SetupAccessToken "$ADMIN_TOKEN" \
      -VerifyDockerLogs

Expected business metrics:

    orders.created.total
    orders.stock_reserved.total
    orders.stock_reservation_failed.total

    inventory.stock_reservations.total
    inventory.stock_reservation_failures.total

    notifications.created.total

Expected outbox metrics:

    outbox.messages.published.total
    outbox.publish.duration.ms

Expected RabbitMQ metrics:

    rabbitmq.messages.published.total
    rabbitmq.messages.consumed.total
    rabbitmq.consume.duration.ms

Technical failure metrics appear only after technical failures:

    outbox.messages.failed.total
    outbox.messages.retried.total
    rabbitmq.messages.failed.total
    rabbitmq.messages.dead_lettered.total

A business stock reservation failure is not a technical failure.

Therefore this business result:

    StockReservationFailed

should increase business failure metrics, but it should not increase:

    rabbitmq.messages.failed.total
    rabbitmq.messages.dead_lettered.total

## Aspire shows outbox.publish_batch as separate traces

This is expected.

The batch polling loop runs in a background service:

    orders-service: outbox.publish_batch
    inventory-service: outbox.publish_batch

It can appear as a separate technical trace.

The important part is that the individual message publish and consume spans are connected to the original business trace through W3C trace context:

    traceparent
    tracestate

Expected main business trace:

    api-gateway: POST /api/v1/orders
      -> orders-service
      -> outbox.publish_message
      -> rabbitmq.publish
      -> inventory-service rabbitmq.consume
      -> inventory.reserve_stock
      -> inventory outbox.publish_message
      -> rabbitmq.publish
      -> orders-service rabbitmq.consume
      -> notifications-service rabbitmq.consume

## Health check traces are missing

This is expected.

Health check paths are intentionally filtered from tracing:

    /health
    /health/live
    /health/ready

Outgoing health check HTTP calls are also filtered from HttpClient tracing.

This prevents readiness probes from polluting Aspire traces.

## API Gateway logs do not contain every correlation ID

This is expected.

The API Gateway does not log every successful proxied request to Docker logs.

Use Aspire traces to verify gateway participation.

Expected gateway spans:

    api-gateway: POST /api/v1/orders
    api-gateway: GET /api/v1/orders/{id:guid}

The observability smoke test can still pass even if Docker logs do not contain the gateway correlation ID.

## Correlation ID is missing from Orders / Inventory / Notifications logs

Check that the request sends:

    X-Correlation-Id

Example:

    curl -i -X POST "http://localhost:5080/api/v1/orders" \
      -H "Authorization: Bearer $CUSTOMER_TOKEN" \
      -H "Content-Type: application/json" \
      -H "X-Correlation-Id: manual-debug-flow" \
      -d '{
        "customerName": "Debug User",
        "customerEmail": "debug@example.com",
        "items": [
          {
            "productId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            "productName": "Keyboard",
            "quantity": 1
          }
        ]
      }'

Then search logs:

    docker compose logs orders-api --tail=2000 | grep manual-debug-flow
    docker compose logs inventory-api --tail=2000 | grep manual-debug-flow
    docker compose logs notifications-api --tail=2000 | grep manual-debug-flow

If the ID is present in Orders but not in Inventory or Notifications, check:

    - OrderCreated event payload contains correlationId
    - RabbitMQ headers contain X-Correlation-Id
    - consumers call RabbitMqMessageHeaders.GetCorrelationIdOrCreate(...)
    - consumers set CorrelationIdAccessor
    - consumers create CorrelationIdLogScope

## Trace context is not connected across RabbitMQ

Check that outbox tables contain trace context columns:

Orders DB:

    docker exec orders-db psql -U postgres -d ordersdb -c '
    select
      "EventType",
      "RoutingKey",
      "TraceParent",
      "TraceState",
      "OccurredAtUtc"
    from "OutboxMessages"
    order by "OccurredAtUtc" desc
    limit 10;
    '

Inventory DB:

    docker exec inventory-db psql -U postgres -d inventorydb -c '
    select
      "EventType",
      "RoutingKey",
      "TraceParent",
      "TraceState",
      "OccurredAtUtc"
    from "OutboxMessages"
    order by "OccurredAtUtc" desc
    limit 10;
    '

Expected:

    TraceParent is not null for messages created inside traced request/message flow.

If TraceParent is null:

    - check RabbitMqTraceContextHeaders.CaptureCurrent()
    - check Activity.Current exists when creating outbox message
    - check custom ActivitySource is registered in OpenTelemetry setup
    - check AddSource(...) includes OrderSystem.Orders, OrderSystem.Inventory, OrderSystem.Outbox and OrderSystem.Messaging

If traceparent is present in the database but consumers still create root traces:

    - check RabbitMqTraceContextHeaders.SetTraceContext(...)
    - check RabbitMqTraceContextHeaders.InjectCurrent(...)
    - check RabbitMqTraceContextHeaders.Extract(...)
    - check consumers start Activity with extracted ActivityContext

## RabbitMQ messages are not consumed

Check RabbitMQ container:

    docker compose ps rabbitmq
    docker compose logs rabbitmq --tail=200

Open RabbitMQ Management UI:

    http://localhost:15672

Credentials:

    guest / guest

Check exchange:

    ordersystem.events

Check queues:

    inventory.order-created
    orders.stock-reserved
    orders.stock-reservation-failed
    notifications.order-created
    notifications.stock-reserved
    notifications.stock-reservation-failed

Check dead-letter queues:

    inventory.order-created.dlq
    orders.stock-reserved.dlq
    orders.stock-reservation-failed.dlq
    notifications.order-created.dlq
    notifications.stock-reserved.dlq
    notifications.stock.reservation.failed.dlq

If messages stay in the main queue:

    - check consumer service is running
    - check consumer logs
    - check RabbitMQ connection settings
    - check queue names in appsettings
    - check topology initializer logs

If messages go to DLQ:

    - inspect consumer error logs
    - inspect rabbitmq.messages.dead_lettered.total
    - inspect dead-letter queue in RabbitMQ UI

## Outbox messages stay Pending

Orders DB:

    docker exec orders-db psql -U postgres -d ordersdb -c '
    select
      "Id",
      "EventType",
      "RoutingKey",
      "Status",
      "RetryCount",
      "LastError",
      "OccurredAtUtc",
      "ProcessedAtUtc"
    from "OutboxMessages"
    order by "OccurredAtUtc" desc;
    '

Inventory DB:

    docker exec inventory-db psql -U postgres -d inventorydb -c '
    select
      "Id",
      "EventType",
      "RoutingKey",
      "Status",
      "RetryCount",
      "LastError",
      "OccurredAtUtc",
      "ProcessedAtUtc"
    from "OutboxMessages"
    order by "OccurredAtUtc" desc;
    '

If messages stay Pending:

    - check outbox publisher background service logs
    - check RabbitMQ is healthy
    - check topology initializer
    - check exchange name
    - check routing key
    - check OTLP/exporter errors are not crashing startup

Relevant logs:

    docker compose logs orders-api --tail=500 | grep -i outbox
    docker compose logs inventory-api --tail=500 | grep -i outbox

## Outbox messages are retried or failed

Check LastError:

    docker exec orders-db psql -U postgres -d ordersdb -c '
    select
      "EventType",
      "RoutingKey",
      "Status",
      "RetryCount",
      "LastError"
    from "OutboxMessages"
    where "Status" <> 1
    order by "OccurredAtUtc" desc;
    '

    docker exec inventory-db psql -U postgres -d inventorydb -c '
    select
      "EventType",
      "RoutingKey",
      "Status",
      "RetryCount",
      "LastError"
    from "OutboxMessages"
    where "Status" <> 1
    order by "OccurredAtUtc" desc;
    '

Check logs:

    docker compose logs orders-api --tail=500 | grep -i "Publishing Orders outbox message failed"
    docker compose logs inventory-api --tail=500 | grep -i "Publishing Inventory outbox message failed"

Expected structured properties:

    OutboxMessageId
    EventId
    EventType
    RoutingKey
    RetryCount
    MaxRetryCount
    OutboxStatus
    CorrelationId
    ErrorType

## Dead-letter metrics are missing

This is usually expected.

The observability smoke test covers business success and business failure.

It does not intentionally create technical consumer failures.

The metric:

    rabbitmq.messages.dead_lettered.total

appears only when a consumer fails technically and sends a message to a DLQ with:

    BasicNackAsync(..., requeue: false)

To test DLQ manually, publish invalid JSON into a queue using RabbitMQ Management UI.

Example target queue:

    inventory.order-created

After consumer failure, inspect:

    docker compose logs inventory-api --tail=500 | grep -i dead-lettered

And check Aspire metric:

    rabbitmq.messages.dead_lettered.total

## Token request returns null or empty token

Check Keycloak is running:

    docker compose ps keycloak
    docker compose logs keycloak --tail=200

Check realm endpoint:

    curl -i http://localhost:18080/realms/order-system

Get customer token:

    export CUSTOMER_TOKEN=$(curl -s -X POST "http://localhost:18080/realms/order-system/protocol/openid-connect/token" \
      -H "Content-Type: application/x-www-form-urlencoded" \
      -d "client_id=order-system-api" \
      -d "grant_type=password" \
      -d "username=alice.customer" \
      -d 'password=Alice123!' | jq -r ".access_token // empty")

Check token:

    echo "$CUSTOMER_TOKEN" | cut -c1-30

Expected prefix:

    eyJhbGciOi

If token is empty:

    - check username
    - check password
    - check realm name
    - check client_id
    - check Keycloak import

## 401 Unauthorized

401 means the token is missing, invalid, expired, or not accepted by the API.

Common causes:

    - missing Authorization header
    - token was overwritten with literal "<token>"
    - expired token
    - wrong issuer
    - wrong audience
    - Keycloak realm not loaded
    - API container has stale configuration

Check token is real:

    echo "$CUSTOMER_TOKEN" | cut -c1-30

Bad value:

    <token>

Good value:

    eyJhbGciOi...

Retry with a fresh token.

## 403 Forbidden

403 means the token is valid, but the role is insufficient.

Expected examples:

    customer cannot list all orders
    customer cannot read inventory
    customer cannot read notifications
    support cannot create inventory items
    support cannot update inventory items

Use admin token for inventory setup:

    export ADMIN_TOKEN=$(curl -s -X POST "http://localhost:18080/realms/order-system/protocol/openid-connect/token" \
      -H "Content-Type: application/x-www-form-urlencoded" \
      -d "client_id=order-system-api" \
      -d "grant_type=password" \
      -d "username=anna.admin" \
      -d 'password=Anna123!' | jq -r ".access_token // empty")

Then run:

    pwsh ./scripts/observability-smoke-test.ps1 \
      -AccessToken "$CUSTOMER_TOKEN" \
      -SetupAccessToken "$ADMIN_TOKEN" \
      -VerifyDockerLogs

## 429 Too Many Requests

The API Gateway applies rate limiting.

If you run smoke tests repeatedly, wait until the rate limit window resets.

Order creation is limited by the order creation policy.

Retry after a short pause.

## relation does not exist

The database exists, but migrations were not applied.

Apply migrations:

    dotnet ef database update \
      --project src/OrdersService/OrdersService.Infrastructure \
      --startup-project src/OrdersService/OrdersService.Api

    dotnet ef database update \
      --project src/InventoryService/InventoryService.Infrastructure \
      --startup-project src/InventoryService/InventoryService.Api

    dotnet ef database update \
      --project src/NotificationsService/NotificationsService.Infrastructure \
      --startup-project src/NotificationsService/NotificationsService.Api

If running inside Docker with old volumes, reset:

    docker compose down -v
    docker compose up -d --build

Then apply migrations again.

## column TraceParent does not exist

This means the database schema is older than the current code.

Apply the latest migrations.

Orders:

    dotnet ef database update \
      --project src/OrdersService/OrdersService.Infrastructure \
      --startup-project src/OrdersService/OrdersService.Api

Inventory:

    dotnet ef database update \
      --project src/InventoryService/InventoryService.Infrastructure \
      --startup-project src/InventoryService/InventoryService.Api

If local volumes are disposable, reset:

    docker compose down -v
    docker compose up -d --build

Then apply migrations.

## Observability smoke test fails on inventory setup with 403

The setup token does not have admin role.

The script creates an inventory item before creating orders.

Inventory create requires admin privileges.

Use:

    anna.admin / Anna123!

Example:

    export ADMIN_TOKEN=$(curl -s -X POST "http://localhost:18080/realms/order-system/protocol/openid-connect/token" \
      -H "Content-Type: application/x-www-form-urlencoded" \
      -d "client_id=order-system-api" \
      -d "grant_type=password" \
      -d "username=anna.admin" \
      -d 'password=Anna123!' | jq -r ".access_token // empty")

Run:

    pwsh ./scripts/observability-smoke-test.ps1 \
      -AccessToken "$CUSTOMER_TOKEN" \
      -SetupAccessToken "$ADMIN_TOKEN" \
      -VerifyDockerLogs

## Observability smoke test succeeds but gateway log check warns

This is acceptable.

Example warning:

    Docker logs for 'api-gateway' do not contain correlation id ...

The gateway does not log every successful proxied request.

Verify gateway participation in Aspire traces instead.

## Observability smoke test times out waiting for StockReserved

Check order status manually:

    curl -i "http://localhost:5080/api/v1/orders/{orderId}" \
      -H "Authorization: Bearer $CUSTOMER_TOKEN"

Check Orders logs:

    docker compose logs orders-api --tail=500

Check Inventory logs:

    docker compose logs inventory-api --tail=500

Check RabbitMQ queues:

    http://localhost:15672

Typical causes:

    - inventory item does not exist
    - insufficient stock
    - RabbitMQ consumer is not running
    - outbox publisher is not running
    - RabbitMQ topology is not initialized
    - migrations are missing
    - message is stuck in outbox
    - message is in DLQ

## Observability smoke test times out waiting for notification

Check Notifications service logs:

    docker compose logs notifications-api --tail=500

Check Notifications DB:

    docker exec notifications-db psql -U postgres -d notificationsdb -c '
    select
      "SourceEventType",
      "Recipient",
      "Subject",
      "Status",
      "CreatedAtUtc"
    from "Notifications"
    order by "CreatedAtUtc" desc;
    '

Check RabbitMQ queues:

    notifications.order-created
    notifications.stock-reserved
    notifications.stock-reservation-failed

Check DLQs:

    notifications.order-created.dlq
    notifications.stock-reserved.dlq
    notifications.stock.reservation.failed.dlq

## API route shows without leading slash in Aspire

This is cosmetic.

Example:

    inventory-service: POST api/v1/inventory-items

versus:

    api-gateway: POST /api/v1/orders

It does not indicate a tracing problem.

It comes from route normalization and display naming.

## Clean reset

Use this when local containers or volumes are inconsistent.

Warning: this removes local PostgreSQL data.

    docker compose down -v
    docker compose up -d --build

Apply migrations again:

    dotnet ef database update \
      --project src/OrdersService/OrdersService.Infrastructure \
      --startup-project src/OrdersService/OrdersService.Api

    dotnet ef database update \
      --project src/InventoryService/InventoryService.Infrastructure \
      --startup-project src/InventoryService/InventoryService.Api

    dotnet ef database update \
      --project src/NotificationsService/NotificationsService.Infrastructure \
      --startup-project src/NotificationsService/NotificationsService.Api

Get fresh tokens and rerun smoke tests.

## Useful log commands

Gateway:

    docker compose logs api-gateway --tail=300

Orders:

    docker compose logs orders-api --tail=300

Inventory:

    docker compose logs inventory-api --tail=300

Notifications:

    docker compose logs notifications-api --tail=300

RabbitMQ:

    docker compose logs rabbitmq --tail=300

Aspire Dashboard:

    docker compose logs aspire-dashboard --tail=300

Search by correlation ID:

    docker compose logs orders-api --tail=2000 | grep "your-correlation-id"
    docker compose logs inventory-api --tail=2000 | grep "your-correlation-id"
    docker compose logs notifications-api --tail=2000 | grep "your-correlation-id"

Search failures:

    docker compose logs orders-api --tail=2000 | grep -i "error\|failed\|dead-letter"
    docker compose logs inventory-api --tail=2000 | grep -i "error\|failed\|dead-letter"
    docker compose logs notifications-api --tail=2000 | grep -i "error\|failed\|dead-letter"