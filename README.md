# MicroS 03 - Order System Messaging

Training microservices project focused on asynchronous communication between services using RabbitMQ, the outbox pattern, idempotent consumers, PostgreSQL, Docker Compose, health checks, API versioning, unit tests, integration tests, and CI.

The project demonstrates a simple order-processing flow:

```text
Orders Service
  -> publishes OrderCreated
  -> Inventory Service reserves stock
  -> publishes StockReserved or StockReservationFailed
  -> Orders Service updates order status
  -> Notifications Service stores notifications
```

---

## Goals

This project is designed to demonstrate:

```text
- microservice boundaries
- database per service
- asynchronous messaging with RabbitMQ
- topic exchange routing
- outbox pattern
- idempotent consumers
- dead-letter queues
- API versioning
- validation
- ProblemDetails error responses
- health checks
- Docker Compose orchestration
- PostgreSQL integration tests with Testcontainers
- unit and application service tests
- GitHub Actions CI with test results and coverage artifacts
```

---

## Technology stack

```text
.NET 10
ASP.NET Core Web API
Controllers
Entity Framework Core
PostgreSQL
RabbitMQ
Docker Compose
FluentValidation
xUnit
Testcontainers
GitHub Actions
```

---

## Services

### Orders Service

Responsible for:

```text
- creating orders
- storing order items
- writing OrderCreated events to the outbox
- publishing OrderCreated to RabbitMQ
- consuming StockReserved
- consuming StockReservationFailed
- updating order status
```

Local URL:

```text
http://localhost:5081
```

Swagger:

```text
http://localhost:5081/swagger
```

Health:

```text
http://localhost:5081/health/live
http://localhost:5081/health/ready
```

---

### Inventory Service

Responsible for:

```text
- managing inventory items
- consuming OrderCreated
- reserving stock
- storing stock reservations
- writing StockReserved or StockReservationFailed events to the outbox
- publishing inventory result events to RabbitMQ
```

Local URL:

```text
http://localhost:5082
```

Swagger:

```text
http://localhost:5082/swagger
```

Health:

```text
http://localhost:5082/health/live
http://localhost:5082/health/ready
```

---

### Notifications Service

Responsible for:

```text
- consuming OrderCreated
- consuming StockReserved
- consuming StockReservationFailed
- storing notifications
- exposing notifications through read API endpoints
```

Local URL:

```text
http://localhost:5083
```

Swagger:

```text
http://localhost:5083/swagger
```

Health:

```text
http://localhost:5083/health/live
http://localhost:5083/health/ready
```

---

## High-level architecture

```text
src/
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
  OrdersService.Domain.UnitTests/
  OrdersService.Application.UnitTests/
  OrdersService.Api.PostgresIntegrationTests/

  InventoryService.Domain.UnitTests/
  InventoryService.Application.UnitTests/
  InventoryService.Api.PostgresIntegrationTests/

  NotificationsService.Domain.UnitTests/
  NotificationsService.Application.UnitTests/
  NotificationsService.Api.PostgresIntegrationTests/

scripts/
  smoke-test.ps1
```

---

## Message broker

RabbitMQ is used with a topic exchange.

Exchange:

```text
ordersystem.events
```

Routing keys:

```text
order.created
stock.reserved
stock.reservation.failed
```

Main queues:

```text
inventory.order-created

orders.stock-reserved
orders.stock-reservation-failed

notifications.order-created
notifications.stock-reserved
notifications.stock-reservation-failed
```

Dead-letter queues:

```text
inventory.order-created.dlq

orders.stock-reserved.dlq
orders.stock-reservation-failed.dlq

notifications.order-created.dlq
notifications.stock-reserved.dlq
notifications.stock-reservation-failed.dlq
```

RabbitMQ Management UI:

```text
http://localhost:15672
```

Credentials:

```text
guest / guest
```

---

## Main business flow

### Successful stock reservation

```text
1. Client creates inventory item.
2. Client creates order.
3. Orders Service stores order with status PendingStockReservation.
4. Orders Service writes OrderCreated to OutboxMessages.
5. Orders outbox publisher publishes order.created to RabbitMQ.
6. Inventory Service consumes order.created.
7. Inventory Service reserves stock.
8. Inventory Service stores StockReservation with status Reserved.
9. Inventory Service writes StockReserved to OutboxMessages.
10. Inventory outbox publisher publishes stock.reserved to RabbitMQ.
11. Orders Service consumes stock.reserved.
12. Orders Service updates order status to StockReserved.
13. Notifications Service stores OrderCreated and StockReserved notifications.
```

Expected final state:

```text
Order.Status = StockReserved
Inventory.AvailableQuantity decreases
Inventory.ReservedQuantity increases
Notifications contain OrderCreated and StockReserved notifications
```

---

### Failed stock reservation

```text
1. Client creates order with unavailable quantity.
2. Orders Service stores order with status PendingStockReservation.
3. Orders Service publishes OrderCreated through the outbox.
4. Inventory Service consumes OrderCreated.
5. Inventory Service cannot reserve stock.
6. Inventory Service stores StockReservation with status Failed.
7. Inventory Service writes StockReservationFailed to the outbox.
8. Inventory outbox publisher publishes stock.reservation.failed.
9. Orders Service consumes stock.reservation.failed.
10. Orders Service updates order status to StockReservationFailed.
11. Notifications Service stores OrderCreated and StockReservationFailed notifications.
```

Expected final state:

```text
Order.Status = StockReservationFailed
Inventory quantities remain unchanged
Notifications contain OrderCreated and StockReservationFailed notifications
```

---

## Local prerequisites

Required tools:

```text
.NET 10 SDK
Docker Desktop
EF Core CLI tools
PowerShell 7+ recommended for smoke-test.ps1
```

Check .NET:

```bash
dotnet --info
```

Check EF Core CLI tools:

```bash
dotnet ef --version
```

Install EF Core CLI tools if missing:

```bash
dotnet tool install --global dotnet-ef
```

---

## Local setup

### 1. Start infrastructure only

For a clean local verification, remove existing containers and volumes:

```bash
docker compose down -v
```

Start PostgreSQL databases and RabbitMQ:

```bash
docker compose up -d orders-db inventory-db notifications-db rabbitmq
```

Check status:

```bash
docker compose ps
```

Expected infrastructure containers:

```text
orders-db          healthy
inventory-db       healthy
notifications-db   healthy
rabbitmq           healthy
```

---

### 2. Apply EF Core migrations

The project does not run database migrations automatically at application startup.

Set environment first.

PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
```

Bash:

```bash
export ASPNETCORE_ENVIRONMENT=Development
```

Apply migrations:

```bash
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

---

### 3. Start all services

```bash
docker compose up -d --build
```

Check status:

```bash
docker compose ps
```

Expected containers:

```text
orders-api          healthy
inventory-api       healthy
notifications-api   healthy
orders-db           healthy
inventory-db        healthy
notifications-db    healthy
rabbitmq            healthy
```

---

## Health checks

Orders:

```bash
curl http://localhost:5081/health/ready
```

Inventory:

```bash
curl http://localhost:5082/health/ready
```

Notifications:

```bash
curl http://localhost:5083/health/ready
```

Expected result:

```text
Healthy
```

or JSON with:

```json
{
  "status": "Healthy"
}
```

---

## API endpoints

### Orders API

Create order:

```http
POST /api/v1/orders
```

Get order by ID:

```http
GET /api/v1/orders/{id}
```

List orders:

```http
GET /api/v1/orders?page=1&pageSize=20
```

---

### Inventory API

Create inventory item:

```http
POST /api/v1/inventory-items
```

Get inventory item by product ID:

```http
GET /api/v1/inventory-items/{productId}
```

Update inventory item:

```http
PUT /api/v1/inventory-items/{productId}
```

List inventory items:

```http
GET /api/v1/inventory-items?page=1&pageSize=20
```

---

### Notifications API

Get notification by ID:

```http
GET /api/v1/notifications/{id}
```

List notifications:

```http
GET /api/v1/notifications?page=1&pageSize=20
```

Filter notifications:

```http
GET /api/v1/notifications?sourceEventType=OrderCreated
GET /api/v1/notifications?status=Created
```

---

## Manual verification

Use this product ID for manual examples:

```text
aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee
```

---

### 1. Create inventory item

```bash
curl -X POST "http://localhost:5082/api/v1/inventory-items" \
  -H "Content-Type: application/json" \
  -d '{
    "productId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
    "productName": "Keyboard",
    "availableQuantity": 50
  }'
```

If the item already exists, update it:

```bash
curl -X PUT "http://localhost:5082/api/v1/inventory-items/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" \
  -H "Content-Type: application/json" \
  -d '{
    "productName": "Keyboard",
    "availableQuantity": 50
  }'
```

Verify inventory item:

```bash
curl "http://localhost:5082/api/v1/inventory-items/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
```

Expected values:

```text
availableQuantity = 50
reservedQuantity = 0
```

---

### 2. Create order with available stock

```bash
curl -X POST "http://localhost:5081/api/v1/orders" \
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

Copy the returned order `id`.

Immediately after creation, the order may have this status:

```text
PendingStockReservation
```

After asynchronous processing completes, it should become:

```text
StockReserved
```

Verify order:

```bash
curl "http://localhost:5081/api/v1/orders/{orderId}"
```

Expected final status:

```json
{
  "status": "StockReserved"
}
```

Verify inventory item:

```bash
curl "http://localhost:5082/api/v1/inventory-items/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
```

Expected values:

```text
availableQuantity = 48
reservedQuantity = 2
```

Verify notifications:

```bash
curl "http://localhost:5083/api/v1/notifications?page=1&pageSize=20"
```

Expected notifications include:

```text
OrderCreated
StockReserved
```

Example subjects:

```text
Order {orderId} was created
Stock reserved for order {orderId}
```

---

### 3. Create order with insufficient stock

```bash
curl -X POST "http://localhost:5081/api/v1/orders" \
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

Copy the returned order `id`.

Verify order:

```bash
curl "http://localhost:5081/api/v1/orders/{orderId}"
```

Expected final status:

```json
{
  "status": "StockReservationFailed"
}
```

Verify notifications:

```bash
curl "http://localhost:5083/api/v1/notifications?page=1&pageSize=20"
```

Expected notifications include:

```text
OrderCreated
StockReservationFailed
```

Example subjects:

```text
Order {orderId} was created
Stock reservation failed for order {orderId}
```

---

## Local smoke test script

A PowerShell smoke test is available in:

```text
scripts/smoke-test.ps1
```

The script verifies the main asynchronous flow through public HTTP APIs:

```text
Orders API
  -> RabbitMQ
  -> Inventory API
  -> RabbitMQ
  -> Orders API
  -> Notifications API
```

It checks both scenarios:

```text
1. Successful stock reservation
2. Failed stock reservation because of insufficient stock
```

### Prerequisites

Make sure the full Docker Compose stack is running and database migrations are already applied.

Start the stack:

```bash
docker compose up -d --build
```

Check container status:

```bash
docker compose ps
```

Run the smoke test with PowerShell 7+:

```powershell
pwsh ./scripts/smoke-test.ps1
```

Run the smoke test with Windows PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-test.ps1
```

Override service URLs:

```powershell
pwsh ./scripts/smoke-test.ps1 `
  -OrdersBaseUrl "http://localhost:5081" `
  -InventoryBaseUrl "http://localhost:5082" `
  -NotificationsBaseUrl "http://localhost:5083"
```

Increase timeout for slower machines:

```powershell
pwsh ./scripts/smoke-test.ps1 -TimeoutSeconds 180
```

Expected final output:

```text
Smoke test completed successfully.
```

The smoke test is a black-box HTTP verification. It does not connect directly to PostgreSQL or RabbitMQ.

---

## Database verification

You can inspect the data directly in PostgreSQL.

### Orders DB

```bash
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

```bash
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

```bash
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

---

### Inventory DB

```bash
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

```bash
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

```bash
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

```bash
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

---

### Notifications DB

```bash
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

```bash
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

---

## RabbitMQ verification

Open RabbitMQ Management UI:

```text
http://localhost:15672
```

Credentials:

```text
guest / guest
```

Expected main queues after successful processing:

```text
inventory.order-created                       0 messages
orders.stock-reserved                         0 messages
orders.stock-reservation-failed               0 messages
notifications.order-created                   0 messages
notifications.stock-reserved                  0 messages
notifications.stock-reservation-failed        0 messages
```

Expected DLQ queues:

```text
inventory.order-created.dlq                   0 messages
orders.stock-reserved.dlq                     0 messages
orders.stock-reservation-failed.dlq           0 messages
notifications.order-created.dlq               0 messages
notifications.stock-reserved.dlq              0 messages
notifications.stock-reservation-failed.dlq    0 messages
```

If a DLQ contains messages, inspect the corresponding service logs.

---

## Logs

Orders:

```bash
docker compose logs orders-api --tail=200
```

Inventory:

```bash
docker compose logs inventory-api --tail=200
```

Notifications:

```bash
docker compose logs notifications-api --tail=200
```

RabbitMQ:

```bash
docker compose logs rabbitmq --tail=200
```

---

## Tests

The repository contains three main test groups:

```text
Domain unit tests
Application unit tests
PostgreSQL API integration tests
```

There are no automated E2E tests in the repository at this stage. End-to-end behavior is verified locally through Docker Compose and `scripts/smoke-test.ps1`.

---

### Domain unit tests

Domain unit tests validate business rules without database, API host, or RabbitMQ.

Projects:

```text
tests/OrdersService.Domain.UnitTests
tests/InventoryService.Domain.UnitTests
tests/NotificationsService.Domain.UnitTests
```

Run all domain unit tests:

```bash
dotnet test tests/OrdersService.Domain.UnitTests/OrdersService.Domain.UnitTests.csproj
dotnet test tests/InventoryService.Domain.UnitTests/InventoryService.Domain.UnitTests.csproj
dotnet test tests/NotificationsService.Domain.UnitTests/NotificationsService.Domain.UnitTests.csproj
```

---

### Application unit tests

Application unit tests verify application services using EF Core InMemory.

Projects:

```text
tests/OrdersService.Application.UnitTests
tests/InventoryService.Application.UnitTests
tests/NotificationsService.Application.UnitTests
```

Run all application unit tests:

```bash
dotnet test tests/OrdersService.Application.UnitTests/OrdersService.Application.UnitTests.csproj
dotnet test tests/InventoryService.Application.UnitTests/InventoryService.Application.UnitTests.csproj
dotnet test tests/NotificationsService.Application.UnitTests/NotificationsService.Application.UnitTests.csproj
```

Note:

```text
EF Core InMemory is used only for fast application service tests.
It is not a replacement for PostgreSQL integration tests.
```

---

### PostgreSQL API integration tests

PostgreSQL API integration tests use Testcontainers and require Docker.

They verify:

```text
- controller routing
- API versioning
- HTTP status codes
- request validation behavior
- EF Core mapping
- PostgreSQL schema and migrations
- persistence through real PostgreSQL
```

Projects:

```text
tests/OrdersService.Api.PostgresIntegrationTests
tests/InventoryService.Api.PostgresIntegrationTests
tests/NotificationsService.Api.PostgresIntegrationTests
```

Run Orders API PostgreSQL integration tests:

```bash
dotnet test tests/OrdersService.Api.PostgresIntegrationTests/OrdersService.Api.PostgresIntegrationTests.csproj
```

Run Inventory API PostgreSQL integration tests:

```bash
dotnet test tests/InventoryService.Api.PostgresIntegrationTests/InventoryService.Api.PostgresIntegrationTests.csproj
```

Run Notifications API PostgreSQL integration tests:

```bash
dotnet test tests/NotificationsService.Api.PostgresIntegrationTests/NotificationsService.Api.PostgresIntegrationTests.csproj
```

Run all PostgreSQL API integration tests:

```bash
dotnet test tests/OrdersService.Api.PostgresIntegrationTests/OrdersService.Api.PostgresIntegrationTests.csproj
dotnet test tests/InventoryService.Api.PostgresIntegrationTests/InventoryService.Api.PostgresIntegrationTests.csproj
dotnet test tests/NotificationsService.Api.PostgresIntegrationTests/NotificationsService.Api.PostgresIntegrationTests.csproj
```

Docker Desktop must be running before executing integration tests.

---

### Run all tests

```bash
dotnet test OrderSystemMessaging.slnx
```

This runs all test projects included in the solution.

---

## Code coverage

The CI workflow collects code coverage using:

```text
coverlet.collector
XPlat Code Coverage
Cobertura XML
```

Run tests with coverage locally:

```bash
dotnet test OrderSystemMessaging.slnx \
  --configuration Release \
  --collect:"XPlat Code Coverage" \
  --logger "trx" \
  --results-directory TestResults
```

Coverage files are generated under:

```text
TestResults/
```

The generated coverage files are usually named:

```text
coverage.cobertura.xml
```

The current CI workflow uploads coverage XML files as an artifact named:

```text
coverage-reports
```

It also uploads test result files as an artifact named:

```text
test-results
```

The current CI workflow collects and publishes coverage artifacts but does not enforce a minimum coverage threshold.

---

## GitHub Actions

The repository contains a CI workflow:

```text
.github/workflows/ci.yml
```

The workflow runs on:

```text
push
pull_request
workflow_dispatch
```

It performs:

```text
dotnet restore
dotnet build
dotnet test with coverage
upload test results
upload coverage reports
```

Artifacts:

```text
test-results
coverage-reports
```

---

## Troubleshooting

### API container is unhealthy

Check readiness endpoints from the host:

```bash
curl http://localhost:5081/health/ready
curl http://localhost:5082/health/ready
curl http://localhost:5083/health/ready
```

Then check the same endpoints from inside containers:

```bash
docker exec orders-api curl --fail http://localhost:8080/health/ready
docker exec inventory-api curl --fail http://localhost:8080/health/ready
docker exec notifications-api curl --fail http://localhost:8080/health/ready
```

Inspect logs:

```bash
docker compose logs orders-api --tail=200
docker compose logs inventory-api --tail=200
docker compose logs notifications-api --tail=200
```

---

### Error: relation does not exist

The database exists, but EF Core migrations were not applied.

Run:

```bash
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

---

### Inventory item already exists

The manual examples use a fixed product ID:

```text
aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee
```

If you run the examples repeatedly, creating the same inventory item can return conflict.

Use one of these options:

```text
- update the existing item with PUT
- use a different productId
- reset the local environment with docker compose down -v
```

---

### Outbox messages stay Pending

Check whether RabbitMQ is running:

```bash
docker compose ps rabbitmq
```

Open RabbitMQ Management UI:

```text
http://localhost:15672
```

Verify that these exist:

```text
ordersystem.events exchange
inventory.order-created queue
orders.stock-reserved queue
orders.stock-reservation-failed queue
notifications.order-created queue
notifications.stock-reserved queue
notifications.stock-reservation-failed queue
```

Inspect service logs:

```bash
docker compose logs orders-api --tail=200
docker compose logs inventory-api --tail=200
docker compose logs notifications-api --tail=200
```

---

### Messages appear in DLQ

A DLQ message usually means that a consumer received a message but failed while processing it.

Typical causes:

```text
invalid JSON payload
validation failure
missing database record
invalid order state transition
database exception
```

Inspect the corresponding service logs and the DLQ message payload in RabbitMQ Management UI.

---

### Testcontainers tests fail locally

Check that Docker Desktop is running.

Then run one integration test project directly:

```bash
dotnet test tests/OrdersService.Api.PostgresIntegrationTests/OrdersService.Api.PostgresIntegrationTests.csproj
```

If Docker is not available, PostgreSQL integration tests cannot run.

---

## Current limitations

This project intentionally does not include:

```text
- authentication and authorization
- API Gateway
- distributed tracing
- metrics dashboard
- production secrets management
- Kubernetes deployment
- automated RabbitMQ E2E tests
```

Those are intended for later microservices training projects.

---

## Useful commands

Build solution:

```bash
dotnet build OrderSystemMessaging.slnx
```

Run all tests:

```bash
dotnet test OrderSystemMessaging.slnx
```

Start local stack:

```bash
docker compose up -d --build
```

Stop local stack:

```bash
docker compose down
```

Stop local stack and remove volumes:

```bash
docker compose down -v
```

Run smoke test:

```powershell
pwsh ./scripts/smoke-test.ps1
```