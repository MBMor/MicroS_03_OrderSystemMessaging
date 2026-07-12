# Order System Messaging

Training microservices project focused on asynchronous communication, RabbitMQ messaging, the outbox pattern, idempotent consumers, API Gateway, Keycloak identity provider integration, JWT authentication, role-based authorization, rate limiting, health checks, PostgreSQL, Docker Compose, integration tests, CI, structured logging, correlation ID propagation, OpenTelemetry tracing, custom metrics, and Aspire Dashboard.

The project demonstrates a simple order-processing flow:

    Client
      -> API Gateway
      -> Orders Service
      -> Orders DB: Order + OrderCreated outbox message
      -> RabbitMQ: order.created
      -> Inventory Service: stock reservation
      -> Inventory DB: StockReservation + StockReserved / StockReservationFailed outbox message
      -> RabbitMQ: stock.reserved / stock.reservation.failed
      -> Orders Service: order status update
      -> Notifications Service: notification storage

## Goals

This project is designed to demonstrate:

    - microservice boundaries
    - database per service
    - asynchronous messaging with RabbitMQ
    - topic exchange routing
    - outbox pattern
    - idempotent consumers
    - dead-letter queues
    - API Gateway as the public HTTP boundary
    - Keycloak as local Identity Provider
    - JWT bearer authentication
    - role-based authorization
    - public vs internal endpoint separation
    - gateway-level rate limiting
    - API versioning
    - validation
    - ProblemDetails error responses
    - health checks and readiness checks
    - Docker Compose orchestration
    - PostgreSQL integration tests with Testcontainers
    - API Gateway integration tests
    - unit and application service tests
    - GitHub Actions CI with test results and coverage artifacts
    - structured JSON logging
    - correlation ID propagation
    - OpenTelemetry distributed tracing
    - OpenTelemetry custom metrics
    - RabbitMQ trace context propagation
    - Aspire Dashboard for local observability

## Technology stack

    .NET 10
    ASP.NET Core Web API
    Controllers
    YARP Reverse Proxy
    Keycloak
    JWT Bearer Authentication
    Entity Framework Core
    PostgreSQL
    RabbitMQ
    Docker Compose
    FluentValidation
    OpenTelemetry
    Aspire Dashboard
    xUnit
    Testcontainers
    GitHub Actions

## Services

### API Gateway

Responsible for:

    - public HTTP entry point
    - reverse proxy routing with YARP
    - JWT bearer authentication
    - route-level authorization
    - role-based access control
    - gateway-level rate limiting
    - gateway health and readiness checks
    - forwarding correlation IDs
    - exporting OpenTelemetry traces and metrics

Local URL:

    http://localhost:5080

Health:

    http://localhost:5080/health
    http://localhost:5080/health/live
    http://localhost:5080/health/ready

### Keycloak

Responsible for:

    - local identity provider
    - order-system realm
    - local users and roles
    - JWT access token issuing

Local URL:

    http://localhost:18080

Admin console:

    http://localhost:18080/admin

Local admin credentials:

    admin / admin

Realm:

    order-system

Client:

    order-system-api

Users:

    alice.customer / Alice123! / customer
    sam.support    / Sam123!   / support
    anna.admin     / Anna123!  / admin

### Orders Service

Responsible for:

    - creating orders
    - storing order items
    - writing OrderCreated events to the outbox
    - publishing OrderCreated to RabbitMQ
    - consuming StockReserved
    - consuming StockReservationFailed
    - updating order status
    - structured logging with correlation ID
    - custom business, outbox and messaging traces
    - custom business, outbox and messaging metrics

Local debug-only URL:

    http://localhost:5081

Swagger:

    http://localhost:5081/swagger

Health:

    http://localhost:5081/health/live
    http://localhost:5081/health/ready

### Inventory Service

Responsible for:

    - managing inventory items
    - consuming OrderCreated
    - reserving stock
    - storing stock reservations
    - writing StockReserved or StockReservationFailed events to the outbox
    - publishing inventory result events to RabbitMQ
    - structured logging with correlation ID
    - custom business, outbox and messaging traces
    - custom business, outbox and messaging metrics

Local debug-only URL:

    http://localhost:5082

Swagger:

    http://localhost:5082/swagger

Health:

    http://localhost:5082/health/live
    http://localhost:5082/health/ready

### Notifications Service

Responsible for:

    - consuming OrderCreated
    - consuming StockReserved
    - consuming StockReservationFailed
    - storing notifications
    - exposing notifications through read API endpoints
    - structured logging with correlation ID
    - custom notification and messaging traces
    - custom notification and messaging metrics

Local debug-only URL:

    http://localhost:5083

Swagger:

    http://localhost:5083/swagger

Health:

    http://localhost:5083/health/live
    http://localhost:5083/health/ready

### Aspire Dashboard

Responsible for local observability:

    - distributed traces
    - custom application traces
    - runtime metrics
    - custom business metrics
    - custom outbox metrics
    - custom RabbitMQ messaging metrics
    - structured log inspection

Local URL:

    http://localhost:18888

OTLP endpoints inside Docker network:

    http://aspire-dashboard:18889    gRPC
    http://aspire-dashboard:18890    HTTP/protobuf

## High-level architecture

    src/
      ApiGateway/
        Public HTTP boundary
        YARP reverse proxy
        JWT auth
        authorization policies
        rate limiting
        readiness checks
        OpenTelemetry setup

      Observability.Shared/
        Correlation/
        Logging/
        Messaging/
        Metrics/
        OpenTelemetry/
        Tracing/

      OrderSystem.Contracts/
        Shared integration event contracts

      OrdersService/
        OrdersService.Api/
        OrdersService.Application/
        OrdersService.Domain/
        OrdersService.Infrastructure/

      InventoryService/
        InventoryService.Api/
        InventoryService.Application/
        InventoryService.Domain/
        InventoryService.Infrastructure/

      NotificationsService/
        NotificationsService.Api/
        NotificationsService.Application/
        NotificationsService.Domain/
        NotificationsService.Infrastructure/

    tests/
      ApiGateway.IntegrationTests/

      OrdersService.Domain.UnitTests/
      OrdersService.Application.UnitTests/
      OrdersService.Api.PostgresIntegrationTests/

      InventoryService.Domain.UnitTests/
      InventoryService.Application.UnitTests/
      InventoryService.Api.PostgresIntegrationTests/

      NotificationsService.Domain.UnitTests/
      NotificationsService.Application.UnitTests/
      NotificationsService.Api.PostgresIntegrationTests/

    docs/
      security/
        keycloak-role-mapping.md
        public-internal-endpoints.md
        local-curl-examples.md

    infra/
      keycloak/
        import/
          order-system-realm.json

    scripts/
      smoke-test.ps1
      observability-smoke-test.ps1

## Public HTTP boundary

The intended public local entry point is the API Gateway:

    http://localhost:5080

Client applications should call the system through the API Gateway.

Downstream services are exposed on host ports only for local debugging and verification. They should not be treated as public client entry points.

## Public gateway routes

### Orders

    POST /api/v1/orders
    GET  /api/v1/orders
    GET  /api/v1/orders/{id}

### Inventory

    GET  /api/v1/inventory-items
    GET  /api/v1/inventory-items/{productId}
    POST /api/v1/inventory-items
    PUT  /api/v1/inventory-items/{productId}

### Notifications

    GET /api/v1/notifications
    GET /api/v1/notifications/{id}

## Authorization model

Roles:

    customer
    support
    admin

Gateway policies:

    AuthenticatedUser
    CustomerOnly
    SupportOrAdmin
    AdminOnly
    CanCreateOrder
    CanManageInventory
    CanReadNotifications

### Route access

Orders:

    POST /api/v1/orders      -> CanCreateOrder
    GET  /api/v1/orders/{id} -> AuthenticatedUser
    GET  /api/v1/orders      -> SupportOrAdmin

Inventory:

    GET  /api/v1/inventory-items             -> SupportOrAdmin
    GET  /api/v1/inventory-items/{productId} -> SupportOrAdmin
    POST /api/v1/inventory-items             -> CanManageInventory
    PUT  /api/v1/inventory-items/{productId} -> CanManageInventory

Notifications:

    GET /api/v1/notifications      -> CanReadNotifications
    GET /api/v1/notifications/{id} -> CanReadNotifications

### Important role notes

Creating or updating inventory items requires admin privileges.

The support role can read inventory and notifications, but it cannot create or update inventory items.

Expected examples:

    alice.customer can create orders
    sam.support can list orders, read inventory and read notifications
    anna.admin can create or update inventory items

## Expected security responses

Missing token:

    401 Unauthorized

Invalid or expired token:

    401 Unauthorized

Valid token with insufficient role:

    403 Forbidden

Rate limit exceeded:

    429 Too Many Requests

Unknown gateway route:

    404 Not Found

Wrong method on known resource path:

    405 Method Not Allowed

## Rate limiting

The API Gateway uses in-memory rate limiting.

Policies:

    OrderCreationLimit      -> 5 requests / minute
    AuthenticatedUserLimit  -> 60 requests / minute
    AdminEndpointLimit      -> 30 requests / minute

Applied routes:

    POST /api/v1/orders -> OrderCreationLimit

General protected read routes use:

    AuthenticatedUserLimit

Admin/write inventory routes use:

    AdminEndpointLimit

This is suitable for local single-instance learning. A real multi-instance gateway would require a distributed rate limiting strategy.

## Message broker

RabbitMQ is used with a topic exchange.

Exchange:

    ordersystem.events

Routing keys:

    order.created
    stock.reserved
    stock.reservation.failed

Main queues:

    inventory.order-created

    orders.stock-reserved
    orders.stock-reservation-failed

    notifications.order-created
    notifications.stock-reserved
    notifications.stock-reservation-failed

Dead-letter queues:

    inventory.order-created.dlq

    orders.stock-reserved.dlq
    orders.stock-reservation-failed.dlq

    notifications.order-created.dlq
    notifications.stock-reserved.dlq
    notifications.stock.reservation.failed.dlq

RabbitMQ Management UI:

    http://localhost:15672

Credentials:

    guest / guest

## Databases

Each service owns its own PostgreSQL database.

Orders DB:

    ordersdb
    localhost:5433

Inventory DB:

    inventorydb
    localhost:5434

Notifications DB:

    notificationsdb
    localhost:5435

Keycloak DB:

    keycloak

The project does not run EF Core migrations automatically at application startup.

## Observability

The project includes local observability through:

    - structured JSON console logs
    - correlation ID propagation
    - W3C trace context propagation
    - OpenTelemetry tracing
    - OpenTelemetry metrics
    - custom ActivitySource spans
    - custom Meter metrics
    - Aspire Dashboard

### Correlation ID

The system uses:

    X-Correlation-Id

Correlation ID is propagated through:

    - incoming HTTP requests
    - API Gateway
    - downstream services
    - structured logs
    - outbox event payloads
    - RabbitMQ headers
    - RabbitMQ consumers

If a request does not contain a correlation ID, the application creates one.

Correlation ID is intended for log search and troubleshooting.

### Trace context

The system propagates W3C trace context through RabbitMQ by using these headers:

    traceparent
    tracestate

For outbox publishing, trace context is captured when the outbox message is created and stored with the outbox message.

This allows a later background publisher to attach RabbitMQ publish spans back to the original business trace.

### OpenTelemetry traces

The system emits traces for:

    - HTTP requests
    - outgoing HTTP calls
    - business operations
    - outbox publishing
    - RabbitMQ publishing
    - RabbitMQ consuming
    - notification creation

Main custom ActivitySource names:

    OrderSystem.Orders
    OrderSystem.Inventory
    OrderSystem.Notifications
    OrderSystem.Outbox
    OrderSystem.Messaging

Expected main trace for successful order flow:

    api-gateway: POST /api/v1/orders
      -> orders-service
      -> orders.create
      -> outbox.publish_message
      -> rabbitmq.publish
      -> inventory-service rabbitmq.consume
      -> inventory.reserve_stock
      -> inventory outbox.publish_message
      -> rabbitmq.publish
      -> orders-service rabbitmq.consume
      -> notifications-service rabbitmq.consume

The outbox batch polling span can appear as a separate trace:

    orders-service: outbox.publish_batch
    inventory-service: outbox.publish_batch

This is expected because the batch loop is a background service. The individual outbox message publish spans are connected to the original business trace through stored trace context.

### Health check telemetry filtering

Incoming health check requests are excluded from HTTP tracing.

Filtered paths include:

    /health
    /health/live
    /health/ready

Outgoing health check HTTP calls are also filtered from HttpClient tracing.

This prevents readiness probes from creating excessive trace noise.

### Structured logs

Logs are structured JSON console logs.

Important log fields include:

    CorrelationId
    TraceId
    SpanId
    EventId
    EventType
    OutboxMessageId
    OutboxStatus
    RetryCount
    MaxRetryCount
    QueueName
    DeadLetterQueueName
    RoutingKey
    DeliveryTag
    Redelivered
    ErrorType

Normal success flow is primarily observable through traces and metrics.

Logs are focused mainly on:

    - startup
    - readiness problems
    - outbox publish failures
    - RabbitMQ publish failures
    - RabbitMQ consume failures
    - dead-letter scenarios
    - unexpected exceptions

### Custom metrics

The system emits custom business, outbox and RabbitMQ metrics.

Business metrics:

    orders.created.total
    orders.stock_reserved.total
    orders.stock_reservation_failed.total

    inventory.stock_reservations.total
    inventory.stock_reservation_failures.total

    notifications.created.total

Outbox metrics:

    outbox.messages.published.total
    outbox.messages.failed.total
    outbox.messages.retried.total
    outbox.publish.duration.ms

RabbitMQ metrics:

    rabbitmq.messages.published.total
    rabbitmq.messages.consumed.total
    rabbitmq.messages.failed.total
    rabbitmq.messages.dead_lettered.total
    rabbitmq.consume.duration.ms

Runtime metrics are also exported through OpenTelemetry.

### Metric tags

Common metric tags:

    order.status
    stock.reservation.status
    stock.reservation.failure_reason
    notification.type
    event.type
    outbox.status
    outcome
    messaging.system
    messaging.operation.name
    messaging.destination.name
    messaging.rabbitmq.routing_key
    messaging.rabbitmq.queue.name
    messaging.rabbitmq.dead_letter_queue.name
    error.type

The following values are intentionally not used as metric tags because they are high-cardinality:

    order.id
    event.id
    correlation.id
    trace.id
    message.id
    delivery.tag
    customer.email
    customer.name

Those values belong to logs and traces, not metric dimensions.

### Aspire Dashboard

Aspire Dashboard is available at:

    http://localhost:18888

Use it to inspect:

    - traces
    - spans
    - logs
    - metrics
    - service resources

After a successful observability smoke test, look for traces such as:

    api-gateway: POST /api/v1/orders
    inventory-service: POST api/v1/inventory-items
    orders-service: outbox.publish_batch
    inventory-service: outbox.publish_batch
    api-gateway: GET /api/v1/orders/{id:guid}
    notifications-service: GET api/v1/notifications

Short polling traces from the smoke test are expected because the script repeatedly checks order status and notifications.

## Prerequisites

Required tools:

    .NET SDK 10
    Docker Desktop
    EF Core CLI tools
    PowerShell 7
    curl
    jq

Check EF Core tools:

    dotnet ef --version

Install EF Core tools if missing:

    dotnet tool install --global dotnet-ef

## Local setup

### 1. Start infrastructure

For a clean verification, remove existing containers and volumes:

    docker compose down -v

Start the full stack:

    docker compose up -d --build

Check containers:

    docker compose ps

Expected important containers:

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

### 2. Apply EF Core migrations

Set development environment.

PowerShell:

    $env:ASPNETCORE_ENVIRONMENT = "Development"

Bash:

    export ASPNETCORE_ENVIRONMENT=Development

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

### 3. Verify health endpoints

Gateway:

    curl -i http://localhost:5080/health
    curl -i http://localhost:5080/health/live
    curl -i http://localhost:5080/health/ready

Downstream debug-only health endpoints:

    curl -i http://localhost:5081/health/ready
    curl -i http://localhost:5082/health/ready
    curl -i http://localhost:5083/health/ready

Expected:

    Healthy

or JSON response with:

    "status": "Healthy"

### 4. Open local dashboards

Aspire Dashboard:

    http://localhost:18888

RabbitMQ Management UI:

    http://localhost:15672

Keycloak:

    http://localhost:18080

## Local token retrieval

### Customer token

Git Bash:

    export CUSTOMER_TOKEN=$(curl -s -X POST "http://localhost:18080/realms/order-system/protocol/openid-connect/token" \
      -H "Content-Type: application/x-www-form-urlencoded" \
      -d "client_id=order-system-api" \
      -d "grant_type=password" \
      -d "username=alice.customer" \
      -d 'password=Alice123!' | jq -r ".access_token // empty")

PowerShell:

    $env:CUSTOMER_TOKEN = (curl.exe -s -X POST "http://localhost:18080/realms/order-system/protocol/openid-connect/token" `
      -H "Content-Type: application/x-www-form-urlencoded" `
      -d "client_id=order-system-api" `
      -d "grant_type=password" `
      -d "username=alice.customer" `
      -d "password=Alice123!" | jq -r ".access_token // empty")

### Support token

Git Bash:

    export SUPPORT_TOKEN=$(curl -s -X POST "http://localhost:18080/realms/order-system/protocol/openid-connect/token" \
      -H "Content-Type: application/x-www-form-urlencoded" \
      -d "client_id=order-system-api" \
      -d "grant_type=password" \
      -d "username=sam.support" \
      -d 'password=Sam123!' | jq -r ".access_token // empty")

PowerShell:

    $env:SUPPORT_TOKEN = (curl.exe -s -X POST "http://localhost:18080/realms/order-system/protocol/openid-connect/token" `
      -H "Content-Type: application/x-www-form-urlencoded" `
      -d "client_id=order-system-api" `
      -d "grant_type=password" `
      -d "username=sam.support" `
      -d "password=Sam123!" | jq -r ".access_token // empty")

### Admin token

Git Bash:

    export ADMIN_TOKEN=$(curl -s -X POST "http://localhost:18080/realms/order-system/protocol/openid-connect/token" \
      -H "Content-Type: application/x-www-form-urlencoded" \
      -d "client_id=order-system-api" \
      -d "grant_type=password" \
      -d "username=anna.admin" \
      -d 'password=Anna123!' | jq -r ".access_token // empty")

PowerShell:

    $env:ADMIN_TOKEN = (curl.exe -s -X POST "http://localhost:18080/realms/order-system/protocol/openid-connect/token" `
      -H "Content-Type: application/x-www-form-urlencoded" `
      -d "client_id=order-system-api" `
      -d "grant_type=password" `
      -d "username=anna.admin" `
      -d "password=Anna123!" | jq -r ".access_token // empty")

Verify token is not empty:

    echo "$CUSTOMER_TOKEN" | cut -c1-30
    echo "$SUPPORT_TOKEN" | cut -c1-30
    echo "$ADMIN_TOKEN" | cut -c1-30

Expected prefix:

    eyJhbGciOi

More examples:

    docs/security/local-curl-examples.md

## Manual verification through API Gateway

Use this product ID for manual examples:

    aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee

### 1. Create inventory item as admin

    curl -i -X POST "http://localhost:5080/api/v1/inventory-items" \
      -H "Authorization: Bearer $ADMIN_TOKEN" \
      -H "Content-Type: application/json" \
      -d '{
        "productId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
        "productName": "Keyboard",
        "availableQuantity": 50
      }'

If the item already exists, update it:

    curl -i -X PUT "http://localhost:5080/api/v1/inventory-items/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" \
      -H "Authorization: Bearer $ADMIN_TOKEN" \
      -H "Content-Type: application/json" \
      -d '{
        "productName": "Keyboard",
        "availableQuantity": 50
      }'

Verify inventory item:

    curl -i "http://localhost:5080/api/v1/inventory-items/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" \
      -H "Authorization: Bearer $SUPPORT_TOKEN"

Expected values:

    availableQuantity = 50
    reservedQuantity = 0

### 2. Create order as customer

    curl -i -X POST "http://localhost:5080/api/v1/orders" \
      -H "Authorization: Bearer $CUSTOMER_TOKEN" \
      -H "Content-Type: application/json" \
      -H "X-Correlation-Id: manual-success-flow" \
      -d '{
        "customerName": "John Doe",
        "customerEmail": "john.doe@example.com",
        "items": [
          {
            "productId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            "productName": "Keyboard",
            "quantity": 2
          }
        ]
      }'

Copy the returned order ID.

Immediately after creation, the order may have this status:

    PendingStockReservation

After asynchronous processing completes, it should become:

    StockReserved

### 3. Verify order status

Replace `{orderId}` with the returned order ID:

    curl -i "http://localhost:5080/api/v1/orders/{orderId}" \
      -H "Authorization: Bearer $CUSTOMER_TOKEN" \
      -H "X-Correlation-Id: manual-success-flow"

Expected final status:

    StockReserved

### 4. Verify inventory quantity

    curl -i "http://localhost:5080/api/v1/inventory-items/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" \
      -H "Authorization: Bearer $SUPPORT_TOKEN" \
      -H "X-Correlation-Id: manual-success-flow"

Expected values:

    availableQuantity = 48
    reservedQuantity = 2

### 5. Verify notifications

    curl -i "http://localhost:5080/api/v1/notifications?page=1&pageSize=20" \
      -H "Authorization: Bearer $SUPPORT_TOKEN" \
      -H "X-Correlation-Id: manual-success-flow"

Expected notifications include:

    OrderCreated
    StockReserved

## Failed stock reservation scenario

Create an order with unavailable quantity:

    curl -i -X POST "http://localhost:5080/api/v1/orders" \
      -H "Authorization: Bearer $CUSTOMER_TOKEN" \
      -H "Content-Type: application/json" \
      -H "X-Correlation-Id: manual-failure-flow" \
      -d '{
        "customerName": "Jane Doe",
        "customerEmail": "jane.doe@example.com",
        "items": [
          {
            "productId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            "productName": "Keyboard",
            "quantity": 999
          }
        ]
      }'

Copy the returned order ID.

Verify order status:

    curl -i "http://localhost:5080/api/v1/orders/{orderId}" \
      -H "Authorization: Bearer $CUSTOMER_TOKEN" \
      -H "X-Correlation-Id: manual-failure-flow"

Expected final status:

    StockReservationFailed

Verify notifications:

    curl -i "http://localhost:5080/api/v1/notifications?page=1&pageSize=20" \
      -H "Authorization: Bearer $SUPPORT_TOKEN" \
      -H "X-Correlation-Id: manual-failure-flow"

Expected notifications include:

    OrderCreated
    StockReservationFailed

## Smoke tests

### Standard smoke test

Run:

    pwsh ./scripts/smoke-test.ps1

The standard smoke test verifies the basic order-processing flow.

### Observability smoke test

The observability smoke test verifies:

    - readiness
    - inventory setup
    - successful order flow
    - failed business reservation flow
    - notifications
    - inventory quantities
    - correlation ID visibility in service logs
    - traces and metrics available for manual Aspire inspection

First create tokens:

    export CUSTOMER_TOKEN=$(curl -s -X POST "http://localhost:18080/realms/order-system/protocol/openid-connect/token" \
      -H "Content-Type: application/x-www-form-urlencoded" \
      -d "client_id=order-system-api" \
      -d "grant_type=password" \
      -d "username=alice.customer" \
      -d 'password=Alice123!' | jq -r ".access_token // empty")

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

Expected final output:

    Observability smoke test completed successfully.

The script prints correlation IDs for manual Aspire inspection:

    setup
    success
    failure

The API Gateway may not contain every correlation ID in Docker logs because it does not log every successful proxied request. Verify gateway spans in Aspire Dashboard.

## Database verification

### Orders DB

List orders:

    docker exec orders-db psql -U postgres -d ordersdb -c '
    select
      "Id",
      "Status",
      "CreatedAtUtc",
      "UpdatedAtUtc"
    from "Orders"
    order by "CreatedAtUtc" desc;
    '

Check Orders outbox:

    docker exec orders-db psql -U postgres -d ordersdb -c '
    select
      "EventType",
      "RoutingKey",
      "Status",
      "RetryCount",
      "LastError",
      "TraceParent",
      "TraceState",
      "OccurredAtUtc",
      "ProcessedAtUtc"
    from "OutboxMessages"
    order by "OccurredAtUtc" desc;
    '

Check processed messages:

    docker exec orders-db psql -U postgres -d ordersdb -c '
    select
      "MessageId",
      "EventType",
      "ConsumerName",
      "ProcessedAtUtc"
    from "ProcessedMessages"
    order by "ProcessedAtUtc" desc;
    '

### Inventory DB

List inventory:

    docker exec inventory-db psql -U postgres -d inventorydb -c '
    select
      "ProductId",
      "ProductName",
      "AvailableQuantity",
      "ReservedQuantity"
    from "InventoryItems";
    '

Check stock reservations:

    docker exec inventory-db psql -U postgres -d inventorydb -c '
    select
      "OrderId",
      "Status",
      "FailureReason",
      "CreatedAtUtc"
    from "StockReservations"
    order by "CreatedAtUtc" desc;
    '

Check Inventory outbox:

    docker exec inventory-db psql -U postgres -d inventorydb -c '
    select
      "EventType",
      "RoutingKey",
      "Status",
      "RetryCount",
      "LastError",
      "TraceParent",
      "TraceState",
      "OccurredAtUtc",
      "ProcessedAtUtc"
    from "OutboxMessages"
    order by "OccurredAtUtc" desc;
    '

Check processed messages:

    docker exec inventory-db psql -U postgres -d inventorydb -c '
    select
      "MessageId",
      "EventType",
      "ConsumerName",
      "ProcessedAtUtc"
    from "ProcessedMessages"
    order by "ProcessedAtUtc" desc;
    '

### Notifications DB

List notifications:

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

Check processed messages:

    docker exec notifications-db psql -U postgres -d notificationsdb -c '
    select
      "MessageId",
      "EventType",
      "ConsumerName",
      "ProcessedAtUtc"
    from "ProcessedMessages"
    order by "ProcessedAtUtc" desc;
    '

## Tests

The repository contains these test groups:

    Domain unit tests
    Application unit tests
    PostgreSQL API integration tests
    API Gateway integration tests

### Domain unit tests

Projects:

    tests/OrdersService.Domain.UnitTests
    tests/InventoryService.Domain.UnitTests
    tests/NotificationsService.Domain.UnitTests

Run:

    dotnet test tests/OrdersService.Domain.UnitTests/OrdersService.Domain.UnitTests.csproj
    dotnet test tests/InventoryService.Domain.UnitTests/InventoryService.Domain.UnitTests.csproj
    dotnet test tests/NotificationsService.Domain.UnitTests/NotificationsService.Domain.UnitTests.csproj

### Application unit tests

Projects:

    tests/OrdersService.Application.UnitTests
    tests/InventoryService.Application.UnitTests
    tests/NotificationsService.Application.UnitTests

Run:

    dotnet test tests/OrdersService.Application.UnitTests/OrdersService.Application.UnitTests.csproj
    dotnet test tests/InventoryService.Application.UnitTests/InventoryService.Application.UnitTests.csproj
    dotnet test tests/NotificationsService.Application.UnitTests/NotificationsService.Application.UnitTests.csproj

Application tests include helper coverage for:

    - RabbitMQ correlation ID headers
    - RabbitMQ W3C trace context headers
    - outbox-related application behavior
    - idempotent processing behavior

EF Core InMemory is used only for fast application service tests. It is not a replacement for PostgreSQL integration tests.

### PostgreSQL API integration tests

Projects:

    tests/OrdersService.Api.PostgresIntegrationTests
    tests/InventoryService.Api.PostgresIntegrationTests
    tests/NotificationsService.Api.PostgresIntegrationTests

They verify:

    - controller routing
    - API versioning
    - HTTP status codes
    - request validation behavior
    - authorization behavior
    - EF Core mapping
    - PostgreSQL schema and migrations
    - persistence through real PostgreSQL

Run:

    dotnet test tests/OrdersService.Api.PostgresIntegrationTests/OrdersService.Api.PostgresIntegrationTests.csproj
    dotnet test tests/InventoryService.Api.PostgresIntegrationTests/InventoryService.Api.PostgresIntegrationTests.csproj
    dotnet test tests/NotificationsService.Api.PostgresIntegrationTests/NotificationsService.Api.PostgresIntegrationTests.csproj

Docker Desktop must be running before executing PostgreSQL integration tests.

### API Gateway integration tests

Project:

    tests/ApiGateway.IntegrationTests

They verify:

    - authorized gateway routing
    - internal endpoint protection
    - gateway rate limiting
    - gateway routing without real downstream services
    - gateway behavior without real Keycloak

Run:

    dotnet test tests/ApiGateway.IntegrationTests/ApiGateway.IntegrationTests.csproj --filter Category=Integration

### Run all tests

    dotnet test OrderSystemMessaging.slnx

## Code coverage

The CI workflow collects code coverage using:

    coverlet.collector
    XPlat Code Coverage
    Cobertura XML

Run tests with coverage locally:

    dotnet test OrderSystemMessaging.slnx \
      --configuration Release \
      --collect:"XPlat Code Coverage" \
      --logger "trx" \
      --results-directory TestResults

Coverage files are generated under:

    TestResults/

The generated coverage files are usually named:

    coverage.cobertura.xml

The CI workflow uploads coverage XML files as an artifact named:

    coverage-reports

It also uploads test result files as an artifact named:

    test-results

The CI workflow collects and publishes coverage artifacts but does not enforce a minimum coverage threshold.

## GitHub Actions

The repository contains a CI workflow:

    .github/workflows/ci.yml

The workflow runs on:

    push
    pull_request
    workflow_dispatch

It performs:

    dotnet restore
    dotnet build
    dotnet test with coverage
    upload test results
    upload coverage reports

Artifacts:

    test-results
    coverage-reports

## Documentation

Security and gateway documentation:

    docs/security/keycloak-role-mapping.md
    docs/security/public-internal-endpoints.md
    docs/security/local-curl-examples.md

Keycloak local setup:

    infra/keycloak/README.md

Observability troubleshooting:

    docs/observability/troubleshooting.md

## Troubleshooting

### API Gateway is unhealthy

Check gateway readiness:

    curl -i http://localhost:5080/health/ready

Check gateway logs:

    docker compose logs api-gateway --tail=200

Check downstream readiness:

    curl -i http://localhost:5081/health/ready
    curl -i http://localhost:5082/health/ready
    curl -i http://localhost:5083/health/ready

Check Keycloak:

    curl -i http://localhost:18080/realms/order-system

### Downstream API container is unhealthy

Check readiness endpoints from the host:

    curl -i http://localhost:5081/health/ready
    curl -i http://localhost:5082/health/ready
    curl -i http://localhost:5083/health/ready

Then check the same endpoints from inside containers:

    docker exec orders-api curl --fail http://localhost:8080/health/ready
    docker exec inventory-api curl --fail http://localhost:8080/health/ready
    docker exec notifications-api curl --fail http://localhost:8080/health/ready

Inspect logs:

    docker compose logs orders-api --tail=200
    docker compose logs inventory-api --tail=200
    docker compose logs notifications-api --tail=200

### Aspire Dashboard is empty

Check that the Aspire Dashboard container is running:

    docker compose ps aspire-dashboard

Check OTLP environment variables in services:

    docker compose exec orders-api printenv | grep OTEL
    docker compose exec inventory-api printenv | grep OTEL
    docker compose exec notifications-api printenv | grep OTEL

Expected values:

    OTEL_EXPORTER_OTLP_ENDPOINT=http://aspire-dashboard:18889
    OTEL_EXPORTER_OTLP_PROTOCOL=grpc

Generate traffic:

    pwsh ./scripts/observability-smoke-test.ps1 \
      -AccessToken "$CUSTOMER_TOKEN" \
      -SetupAccessToken "$ADMIN_TOKEN" \
      -VerifyDockerLogs

Then refresh:

    http://localhost:18888

### Custom metrics are missing

First generate business traffic.

Expected metrics after observability smoke test:

    orders.created.total
    orders.stock_reserved.total
    orders.stock_reservation_failed.total
    inventory.stock_reservations.total
    inventory.stock_reservation_failures.total
    notifications.created.total
    outbox.messages.published.total
    outbox.publish.duration.ms
    rabbitmq.messages.published.total
    rabbitmq.messages.consumed.total
    rabbitmq.consume.duration.ms

Failure metrics are visible only after technical failures:

    outbox.messages.failed.total
    outbox.messages.retried.total
    rabbitmq.messages.failed.total
    rabbitmq.messages.dead_lettered.total

A business stock reservation failure is not a technical failure. It should produce business failure metrics, but it should not produce RabbitMQ technical failure metrics.

### Health check traces are missing

This is expected.

Health check paths are intentionally filtered from tracing:

    /health
    /health/live
    /health/ready

### API Gateway logs do not contain every correlation ID

This is expected.

The gateway does not log every successful proxied request to Docker logs.

Use Aspire traces to verify gateway participation in the distributed trace.

### Error: relation does not exist

The database exists, but EF Core migrations were not applied.

Run:

    dotnet ef database update \
      --project src/OrdersService/OrdersService.Infrastructure \
      --startup-project src/OrdersService/OrdersService.Api

    dotnet ef database update \
      --project src/InventoryService/InventoryService.Infrastructure \
      --startup-project src/InventoryService/InventoryService.Api

    dotnet ef database update \
      --project src/NotificationsService/NotificationsService.Infrastructure \
      --startup-project src/NotificationsService/NotificationsService.Api

### Token request fails

Check Keycloak container:

    docker compose logs keycloak --tail=200

Check realm endpoint:

    curl -i http://localhost:18080/realms/order-system

If the realm does not exist, restart Keycloak with import enabled:

    docker compose down
    docker compose up -d keycloak-db keycloak

### Gateway returns 401

Common causes:

    - missing Authorization header
    - expired token
    - wrong token issuer
    - wrong audience
    - token was not obtained from the local order-system realm

Get a fresh token and retry.

### Gateway returns 403

The token is valid, but the user does not have the required role.

Examples:

    customer cannot list all orders
    customer cannot read inventory
    customer cannot read notifications
    support cannot create or update inventory items

Use admin token for inventory create/update requests.

### Gateway returns 429

The request was rejected by rate limiting.

Wait until the rate limit window resets and retry.

### Gateway returns 404 for internal paths

This is expected.

Examples that should not be exposed through the gateway:

    /api/v1/orders/swagger
    /api/v1/inventory-items/swagger
    /api/v1/notifications/swagger
    /api/v1/orders/internal/status
    /api/v1/inventory-items/internal/status
    /api/v1/notifications/internal/status

### Gateway internal endpoint protection tests fail with 200 OK

Check API Gateway route constraints.

ID routes should not match arbitrary text such as swagger.

Use GUID constraints for ID-like gateway routes:

    /api/v1/orders/{id:guid}
    /api/v1/inventory-items/{productId:guid}
    /api/v1/notifications/{id:guid}

### Testcontainers tests fail locally

Check that Docker Desktop is running.

Run one integration test project directly:

    dotnet test tests/OrdersService.Api.PostgresIntegrationTests/OrdersService.Api.PostgresIntegrationTests.csproj

If Docker is not available, PostgreSQL integration tests cannot run.

## Useful commands

Build solution:

    dotnet build OrderSystemMessaging.slnx

Run all tests:

    dotnet test OrderSystemMessaging.slnx

Run API Gateway integration tests:

    dotnet test tests/ApiGateway.IntegrationTests/ApiGateway.IntegrationTests.csproj --filter Category=Integration

Run standard smoke test:

    pwsh ./scripts/smoke-test.ps1

Run observability smoke test:

    pwsh ./scripts/observability-smoke-test.ps1 \
      -AccessToken "$CUSTOMER_TOKEN" \
      -SetupAccessToken "$ADMIN_TOKEN" \
      -VerifyDockerLogs

Start local stack:

    docker compose up -d --build

Stop local stack:

    docker compose down

Stop local stack and remove volumes:

    docker compose down -v

Show gateway logs:

    docker compose logs api-gateway --tail=200

Show Orders logs:

    docker compose logs orders-api --tail=200

Show Inventory logs:

    docker compose logs inventory-api --tail=200

Show Notifications logs:

    docker compose logs notifications-api --tail=200

Show RabbitMQ logs:

    docker compose logs rabbitmq --tail=200

Show Aspire Dashboard logs:

    docker compose logs aspire-dashboard --tail=200

## Current limitations

This is a local learning project.

The project does not currently include:

    - production HTTPS setup
    - production-grade secrets management
    - distributed rate limiting
    - centralized production log aggregation
    - alerting rules
    - Kubernetes deployment
    - automated full RabbitMQ E2E test suite
    - production dashboard provisioning
    - production-ready OpenTelemetry collector setup

The included Aspire Dashboard setup is intended for local development and learning.

## Suggested learning focus

This project is useful for practicing:

    - asynchronous microservices communication
    - transactional outbox
    - message idempotency
    - local identity provider integration
    - API Gateway authorization boundary
    - operational troubleshooting
    - distributed tracing
    - correlation ID propagation
    - custom metrics design
    - avoiding high-cardinality metric tags

## Verification checklist

After local setup, verify:

    dotnet build OrderSystemMessaging.slnx
    dotnet test OrderSystemMessaging.slnx
    docker compose up -d --build
    curl -i http://localhost:5080/health/ready
    pwsh ./scripts/smoke-test.ps1
    pwsh ./scripts/observability-smoke-test.ps1 -AccessToken "$CUSTOMER_TOKEN" -SetupAccessToken "$ADMIN_TOKEN" -VerifyDockerLogs

Expected result:

    - health checks are healthy
    - standard smoke test passes
    - observability smoke test passes
    - Aspire Dashboard shows traces
    - Aspire Dashboard shows custom metrics
    - Orders, Inventory and Notifications logs contain correlation IDs