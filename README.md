# MicroS 03 - Order System Messaging

Training microservices project focused on asynchronous communication, RabbitMQ messaging, the outbox pattern, idempotent consumers, API Gateway, Keycloak identity provider integration, JWT authentication, role-based authorization, rate limiting, health checks, PostgreSQL, Docker Compose, integration tests, and CI.

The project demonstrates a simple order-processing flow:

```
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
```

## Goals

This project is designed to demonstrate:

```
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
```

## Technology stack

```
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
xUnit
Testcontainers
GitHub Actions
```

## Services

### API Gateway

Responsible for:

```
- public HTTP entry point
- reverse proxy routing with YARP
- JWT bearer authentication
- route-level authorization
- role-based access control
- gateway-level rate limiting
- gateway health and readiness checks
```

Local URL:

```
http://localhost:5080
```

Health:

```
http://localhost:5080/health
http://localhost:5080/health/live
http://localhost:5080/health/ready
```

### Keycloak

Responsible for:

```
- local identity provider
- order-system realm
- local users and roles
- JWT access token issuing
```

Local URL:

```
http://localhost:18080
```

Admin console:

```
http://localhost:18080/admin
```

Local admin credentials:

```
admin / admin
```

Realm:

```
order-system
```

Client:

```
order-system-api
```

Users:

```
alice.customer / Alice123! / customer
sam.support    / Sam123!   / support
anna.admin     / Anna123!  / admin
```

### Orders Service

Responsible for:

```
- creating orders
- storing order items
- writing OrderCreated events to the outbox
- publishing OrderCreated to RabbitMQ
- consuming StockReserved
- consuming StockReservationFailed
- updating order status
```

Local debug-only URL:

```
http://localhost:5081
```

Swagger:

```
http://localhost:5081/swagger
```

Health:

```
http://localhost:5081/health/live
http://localhost:5081/health/ready
```

### Inventory Service

Responsible for:

```
- managing inventory items
- consuming OrderCreated
- reserving stock
- storing stock reservations
- writing StockReserved or StockReservationFailed events to the outbox
- publishing inventory result events to RabbitMQ
```

Local debug-only URL:

```
http://localhost:5082
```

Swagger:

```
http://localhost:5082/swagger
```

Health:

```
http://localhost:5082/health/live
http://localhost:5082/health/ready
```

### Notifications Service

Responsible for:

```
- consuming OrderCreated
- consuming StockReserved
- consuming StockReservationFailed
- storing notifications
- exposing notifications through read API endpoints
```

Local debug-only URL:

```
http://localhost:5083
```

Swagger:

```
http://localhost:5083/swagger
```

Health:

```
http://localhost:5083/health/live
http://localhost:5083/health/ready
```

## High-level architecture

```
src/
  ApiGateway/
    Public HTTP boundary
    YARP reverse proxy
    JWT auth
    authorization policies
    rate limiting
    readiness checks

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
```

## Public HTTP boundary

The intended public local entry point is the API Gateway:

```
http://localhost:5080
```

Client applications should call the system through the API Gateway.

Downstream services are exposed on host ports only for local debugging and verification. They should not be treated as public client entry points.

## Public gateway routes

### Orders

```
POST /api/v1/orders
GET  /api/v1/orders
GET  /api/v1/orders/{id}
```

### Inventory

```
GET  /api/v1/inventory-items
GET  /api/v1/inventory-items/{productId}
POST /api/v1/inventory-items
PUT  /api/v1/inventory-items/{productId}
```

### Notifications

```
GET /api/v1/notifications
GET /api/v1/notifications/{id}
```

## Authorization model

Roles:

```
customer
support
admin
```

Gateway policies:

```
AuthenticatedUser
CustomerOnly
SupportOrAdmin
AdminOnly
CanCreateOrder
CanManageInventory
CanReadNotifications
```

### Route access

Orders:

```
POST /api/v1/orders      -> CanCreateOrder
GET  /api/v1/orders/{id} -> AuthenticatedUser
GET  /api/v1/orders      -> SupportOrAdmin
```

Inventory:

```
GET  /api/v1/inventory-items             -> SupportOrAdmin
GET  /api/v1/inventory-items/{productId} -> SupportOrAdmin
POST /api/v1/inventory-items             -> CanManageInventory
PUT  /api/v1/inventory-items/{productId} -> CanManageInventory
```

Notifications:

```
GET /api/v1/notifications      -> CanReadNotifications
GET /api/v1/notifications/{id} -> CanReadNotifications
```

## Expected security responses

Missing token:

```
401 Unauthorized
```

Invalid or expired token:

```
401 Unauthorized
```

Valid token with insufficient role:

```
403 Forbidden
```

Rate limit exceeded:

```
429 Too Many Requests
```

Unknown gateway route:

```
404 Not Found
```

Wrong method on known resource path:

```
405 Method Not Allowed
```

## Rate limiting

The API Gateway uses in-memory rate limiting.

Policies:

```
OrderCreationLimit      -> 5 requests / minute
AuthenticatedUserLimit  -> 60 requests / minute
AdminEndpointLimit      -> 30 requests / minute
```

Applied routes:

```
POST /api/v1/orders -> OrderCreationLimit
```

General protected read routes use:

```
AuthenticatedUserLimit
```

Admin/write inventory routes use:

```
AdminEndpointLimit
```

This is suitable for local single-instance learning. A real multi-instance gateway would require a distributed rate limiting strategy.

## Message broker

RabbitMQ is used with a topic exchange.

Exchange:

```
ordersystem.events
```

Routing keys:

```
order.created
stock.reserved
stock.reservation.failed
```

Main queues:

```
inventory.order-created

orders.stock-reserved
orders.stock-reservation-failed

notifications.order-created
notifications.stock-reserved
notifications.stock-reservation-failed
```

Dead-letter queues:

```
inventory.order-created.dlq

orders.stock-reserved.dlq
orders.stock-reservation-failed.dlq

notifications.order-created.dlq
notifications.stock-reserved.dlq
notifications.stock.reservation.failed.dlq
```

RabbitMQ Management UI:

```
http://localhost:15672
```

Credentials:

```
guest / guest
```

## Databases

Each service owns its own PostgreSQL database.

Orders DB:

```
ordersdb
localhost:5433
```

Inventory DB:

```
inventorydb
localhost:5434
```

Notifications DB:

```
notificationsdb
localhost:5435
```

Keycloak DB:

```
keycloak
```

The project does not run EF Core migrations automatically at application startup.

## Prerequisites

Required tools:

```
.NET SDK 10
Docker Desktop
EF Core CLI tools
curl
jq
```

Check EF Core tools:

```
dotnet ef --version
```

Install EF Core tools if missing:

```
dotnet tool install --global dotnet-ef
```

## Local setup

### 1. Start infrastructure

For a clean verification, remove existing containers and volumes:

```
docker compose down -v
```

Start the full stack:

```
docker compose up -d --build
```

Check containers:

```
docker compose ps
```

Expected important containers:

```
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
```

### 2. Apply EF Core migrations

Set development environment.

PowerShell:

```
$env:ASPNETCORE_ENVIRONMENT = "Development"
```

Bash:

```
export ASPNETCORE_ENVIRONMENT=Development
```

Apply migrations:

```
dotnet ef database update \
  --project src/OrdersService/OrdersService.Infrastructure \
  --startup-project src/OrdersService/OrdersService.Api

dotnet ef database update \
  --project src/InventoryService/InventoryService.Infrastructure \
  --startup-project src/InventoryService/InventoryService.Api

dotnet ef database update \
  --project src/NotificationsService/NotificationsService.Infrastructure \
  --startup-project src/NotificationsService/NotificationsService.Api
```

### 3. Verify health endpoints

Gateway:

```
curl -i http://localhost:5080/health
curl -i http://localhost:5080/health/live
curl -i http://localhost:5080/health/ready
```

Downstream debug-only health endpoints:

```
curl -i http://localhost:5081/health/ready
curl -i http://localhost:5082/health/ready
curl -i http://localhost:5083/health/ready
```

Expected:

```
Healthy
```

or JSON response with:

```
"status": "Healthy"
```

## Local token retrieval

Customer token:

```
CUSTOMER_TOKEN=$(curl -s -X POST "http://localhost:18080/realms/order-system/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=order-system-api" \
  -d "grant_type=password" \
  -d "username=alice.customer" \
  -d "password=Alice123!" | jq -r ".access_token")
```

Support token:

```
SUPPORT_TOKEN=$(curl -s -X POST "http://localhost:18080/realms/order-system/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=order-system-api" \
  -d "grant_type=password" \
  -d "username=sam.support" \
  -d "password=Sam123!" | jq -r ".access_token")
```

Admin token:

```
ADMIN_TOKEN=$(curl -s -X POST "http://localhost:18080/realms/order-system/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=order-system-api" \
  -d "grant_type=password" \
  -d "username=anna.admin" \
  -d "password=Anna123!" | jq -r ".access_token")
```

More examples:

```
docs/security/local-curl-examples.md
```

## Manual verification through API Gateway

Use this product ID for manual examples:

```
aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee
```

### 1. Create inventory item as admin

```
curl -i -X POST "http://localhost:5080/api/v1/inventory-items" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "productId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
    "productName": "Keyboard",
    "availableQuantity": 50
  }'
```

If the item already exists, update it:

```
curl -i -X PUT "http://localhost:5080/api/v1/inventory-items/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "productName": "Keyboard",
    "availableQuantity": 50
  }'
```

Verify inventory item:

```
curl -i "http://localhost:5080/api/v1/inventory-items/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" \
  -H "Authorization: Bearer $SUPPORT_TOKEN"
```

Expected values:

```
availableQuantity = 50
reservedQuantity = 0
```

### 2. Create order as customer

```
curl -i -X POST "http://localhost:5080/api/v1/orders" \
  -H "Authorization: Bearer $CUSTOMER_TOKEN" \
  -H "Content-Type: application/json" \
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
```

Copy the returned order ID.

Immediately after creation, the order may have this status:

```
PendingStockReservation
```

After asynchronous processing completes, it should become:

```
StockReserved
```

### 3. Verify order status

Replace `{orderId}` with the returned order ID:

```
curl -i "http://localhost:5080/api/v1/orders/{orderId}" \
  -H "Authorization: Bearer $CUSTOMER_TOKEN"
```

Expected final status:

```
StockReserved
```

### 4. Verify inventory quantity

```
curl -i "http://localhost:5080/api/v1/inventory-items/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" \
  -H "Authorization: Bearer $SUPPORT_TOKEN"
```

Expected values:

```
availableQuantity = 48
reservedQuantity = 2
```

### 5. Verify notifications

```
curl -i "http://localhost:5080/api/v1/notifications?page=1&pageSize=20" \
  -H "Authorization: Bearer $SUPPORT_TOKEN"
```

Expected notifications include:

```
OrderCreated
StockReserved
```

## Failed stock reservation scenario

Create an order with unavailable quantity:

```
curl -i -X POST "http://localhost:5080/api/v1/orders" \
  -H "Authorization: Bearer $CUSTOMER_TOKEN" \
  -H "Content-Type: application/json" \
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
```

Copy the returned order ID.

Verify order status:

```
curl -i "http://localhost:5080/api/v1/orders/{orderId}" \
  -H "Authorization: Bearer $CUSTOMER_TOKEN"
```

Expected final status:

```
StockReservationFailed
```

Verify notifications:

```
curl -i "http://localhost:5080/api/v1/notifications?page=1&pageSize=20" \
  -H "Authorization: Bearer $SUPPORT_TOKEN"
```

Expected notifications include:

```
OrderCreated
StockReservationFailed
```

## Database verification

### Orders DB

List orders:

```
docker exec orders-db psql -U postgres -d ordersdb -c '
select
  "Id",
  "Status",
  "CreatedAtUtc",
  "UpdatedAtUtc"
from "Orders"
order by "CreatedAtUtc" desc;
'
```

Check Orders outbox:

```
docker exec orders-db psql -U postgres -d ordersdb -c '
select
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
```

Check processed messages:

```
docker exec orders-db psql -U postgres -d ordersdb -c '
select
  "MessageId",
  "EventType",
  "ConsumerName",
  "ProcessedAtUtc"
from "ProcessedMessages"
order by "ProcessedAtUtc" desc;
'
```

### Inventory DB

List inventory:

```
docker exec inventory-db psql -U postgres -d inventorydb -c '
select
  "ProductId",
  "ProductName",
  "AvailableQuantity",
  "ReservedQuantity"
from "InventoryItems";
'
```

Check stock reservations:

```
docker exec inventory-db psql -U postgres -d inventorydb -c '
select
  "OrderId",
  "Status",
  "FailureReason",
  "CreatedAtUtc"
from "StockReservations"
order by "CreatedAtUtc" desc;
'
```

Check Inventory outbox:

```
docker exec inventory-db psql -U postgres -d inventorydb -c '
select
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
```

Check processed messages:

```
docker exec inventory-db psql -U postgres -d inventorydb -c '
select
  "MessageId",
  "EventType",
  "ConsumerName",
  "ProcessedAtUtc"
from "ProcessedMessages"
order by "ProcessedAtUtc" desc;
'
```

### Notifications DB

List notifications:

```
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
```

Check processed messages:

```
docker exec notifications-db psql -U postgres -d notificationsdb -c '
select
  "MessageId",
  "EventType",
  "ConsumerName",
  "ProcessedAtUtc"
from "ProcessedMessages"
order by "ProcessedAtUtc" desc;
'
```

## Tests

The repository contains these test groups:

```
Domain unit tests
Application unit tests
PostgreSQL API integration tests
API Gateway integration tests
```

### Domain unit tests

Projects:

```
tests/OrdersService.Domain.UnitTests
tests/InventoryService.Domain.UnitTests
tests/NotificationsService.Domain.UnitTests
```

Run:

```
dotnet test tests/OrdersService.Domain.UnitTests/OrdersService.Domain.UnitTests.csproj
dotnet test tests/InventoryService.Domain.UnitTests/InventoryService.Domain.UnitTests.csproj
dotnet test tests/NotificationsService.Domain.UnitTests/NotificationsService.Domain.UnitTests.csproj
```

### Application unit tests

Projects:

```
tests/OrdersService.Application.UnitTests
tests/InventoryService.Application.UnitTests
tests/NotificationsService.Application.UnitTests
```

Run:

```
dotnet test tests/OrdersService.Application.UnitTests/OrdersService.Application.UnitTests.csproj
dotnet test tests/InventoryService.Application.UnitTests/InventoryService.Application.UnitTests.csproj
dotnet test tests/NotificationsService.Application.UnitTests/NotificationsService.Application.UnitTests.csproj
```

EF Core InMemory is used only for fast application service tests. It is not a replacement for PostgreSQL integration tests.

### PostgreSQL API integration tests

Projects:

```
tests/OrdersService.Api.PostgresIntegrationTests
tests/InventoryService.Api.PostgresIntegrationTests
tests/NotificationsService.Api.PostgresIntegrationTests
```

They verify:

```
- controller routing
- API versioning
- HTTP status codes
- request validation behavior
- authorization behavior
- EF Core mapping
- PostgreSQL schema and migrations
- persistence through real PostgreSQL
```

Run:

```
dotnet test tests/OrdersService.Api.PostgresIntegrationTests/OrdersService.Api.PostgresIntegrationTests.csproj
dotnet test tests/InventoryService.Api.PostgresIntegrationTests/InventoryService.Api.PostgresIntegrationTests.csproj
dotnet test tests/NotificationsService.Api.PostgresIntegrationTests/NotificationsService.Api.PostgresIntegrationTests.csproj
```

Docker Desktop must be running before executing PostgreSQL integration tests.

### API Gateway integration tests

Project:

```
tests/ApiGateway.IntegrationTests
```

They verify:

```
- authorized gateway routing
- internal endpoint protection
- gateway rate limiting
- gateway routing without real downstream services
- gateway behavior without real Keycloak
```

Run:

```
dotnet test tests/ApiGateway.IntegrationTests/ApiGateway.IntegrationTests.csproj --filter Category=Integration
```

### Run all tests

```
dotnet test OrderSystemMessaging.slnx
```

## Code coverage

The CI workflow collects code coverage using:

```
coverlet.collector
XPlat Code Coverage
Cobertura XML
```

Run tests with coverage locally:

```
dotnet test OrderSystemMessaging.slnx \
  --configuration Release \
  --collect:"XPlat Code Coverage" \
  --logger "trx" \
  --results-directory TestResults
```

Coverage files are generated under:

```
TestResults/
```

The generated coverage files are usually named:

```
coverage.cobertura.xml
```

The CI workflow uploads coverage XML files as an artifact named:

```
coverage-reports
```

It also uploads test result files as an artifact named:

```
test-results
```

The CI workflow collects and publishes coverage artifacts but does not enforce a minimum coverage threshold.

## GitHub Actions

The repository contains a CI workflow:

```
.github/workflows/ci.yml
```

The workflow runs on:

```
push
pull_request
workflow_dispatch
```

It performs:

```
dotnet restore
dotnet build
dotnet test with coverage
upload test results
upload coverage reports
```

Artifacts:

```
test-results
coverage-reports
```

## Documentation

Security and gateway documentation:

```
docs/security/keycloak-role-mapping.md
docs/security/public-internal-endpoints.md
docs/security/local-curl-examples.md
```

Keycloak local setup:

```
infra/keycloak/README.md
```

## Troubleshooting

### API Gateway is unhealthy

Check gateway readiness:

```
curl -i http://localhost:5080/health/ready
```

Check gateway logs:

```
docker compose logs api-gateway --tail=200
```

Check downstream readiness:

```
curl -i http://localhost:5081/health/ready
curl -i http://localhost:5082/health/ready
curl -i http://localhost:5083/health/ready
```

Check Keycloak:

```
curl -i http://localhost:18080/realms/order-system
```

### Downstream API container is unhealthy

Check readiness endpoints from the host:

```
curl -i http://localhost:5081/health/ready
curl -i http://localhost:5082/health/ready
curl -i http://localhost:5083/health/ready
```

Then check the same endpoints from inside containers:

```
docker exec orders-api curl --fail http://localhost:8080/health/ready
docker exec inventory-api curl --fail http://localhost:8080/health/ready
docker exec notifications-api curl --fail http://localhost:8080/health/ready
```

Inspect logs:

```
docker compose logs orders-api --tail=200
docker compose logs inventory-api --tail=200
docker compose logs notifications-api --tail=200
```

### Error: relation does not exist

The database exists, but EF Core migrations were not applied.

Run:

```
dotnet ef database update \
  --project src/OrdersService/OrdersService.Infrastructure \
  --startup-project src/OrdersService/OrdersService.Api

dotnet ef database update \
  --project src/InventoryService/InventoryService.Infrastructure \
  --startup-project src/InventoryService/InventoryService.Api

dotnet ef database update \
  --project src/NotificationsService/NotificationsService.Infrastructure \
  --startup-project src/NotificationsService/NotificationsService.Api
```

### Token request fails

Check Keycloak container:

```
docker compose logs keycloak --tail=200
```

Check realm endpoint:

```
curl -i http://localhost:18080/realms/order-system
```

If the realm does not exist, restart Keycloak with import enabled:

```
docker compose down
docker compose up -d keycloak-db keycloak
```

### Gateway returns 401

Common causes:

```
- missing Authorization header
- expired token
- wrong token issuer
- wrong audience
- token was not obtained from the local order-system realm
```

Get a fresh token and retry.

### Gateway returns 403

The token is valid, but the user does not have the required role.

Examples:

```
customer cannot list all orders
customer cannot read inventory
customer cannot read notifications
support cannot create or update inventory items
```

### Gateway returns 429

The request was rejected by rate limiting.

Wait until the rate limit window resets and retry.

### Gateway returns 404 for internal paths

This is expected.

Examples that should not be exposed through the gateway:

```
/api/v1/orders/swagger
/api/v1/inventory-items/swagger
/api/v1/notifications/swagger
/api/v1/orders/internal/status
/api/v1/inventory-items/internal/status
/api/v1/notifications/internal/status
```

### Gateway internal endpoint protection tests fail with 200 OK

Check API Gateway route constraints.

ID routes should not match arbitrary text such as `swagger`.

Use GUID constraints for ID-like gateway routes:

```
/api/v1/orders/{id:guid}
/api/v1/inventory-items/{productId:guid}
/api/v1/notifications/{id:guid}
```

### Testcontainers tests fail locally

Check that Docker Desktop is running.

Run one integration test project directly:

```
dotnet test tests/OrdersService.Api.PostgresIntegrationTests/OrdersService.Api.PostgresIntegrationTests.csproj
```

If Docker is not available, PostgreSQL integration tests cannot run.

## Useful commands

Build solution:

```
dotnet build OrderSystemMessaging.slnx
```

Run all tests:

```
dotnet test OrderSystemMessaging.slnx
```

Run API Gateway integration tests:

```
dotnet test tests/ApiGateway.IntegrationTests/ApiGateway.IntegrationTests.csproj --filter Category=Integration
```

Start local stack:

```
docker compose up -d --build
```

Stop local stack:

```
docker compose down
```

Stop local stack and remove volumes:

```
docker compose down -v
```

Run smoke test:

```
pwsh ./scripts/smoke-test.ps1
```

Show gateway logs:

```
docker compose logs api-gateway --tail=200
```

Show Keycloak logs:

```
docker compose logs keycloak --tail=200
```

Show RabbitMQ logs:

```
docker compose logs rabbitmq --tail=200
```

## Current limitations

This is a local learning project.

The project does not currently include:

```
- production HTTPS setup
- production-grade secrets management
- distributed rate limiting
- distributed tracing
- metrics dashboard
- centralized log aggregation
- Kubernetes deployment
- automated full RabbitMQ E2E test suite
```

These topics are intended for later microservices training projects.
