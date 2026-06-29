# MicroS 03 - Order System Messaging

## Local end-to-end verification

This section describes how to verify the full asynchronous order flow locally.

The expected flow is:

```text
POST /orders
  -> Orders DB: Order + OrderCreated outbox message
  -> RabbitMQ: order.created
  -> Inventory Service: stock reservation
  -> Inventory DB: StockReservation + StockReserved / StockReservationFailed outbox message
  -> RabbitMQ: stock.reserved / stock.reservation.failed
  -> Orders Service: update order status
  -> Notifications Service: create notifications
```

---

### Prerequisites

Required tools:

```text
.NET SDK
Docker Desktop
EF Core CLI tools
```

Check EF Core tools:

```bash
dotnet ef --version
```

If missing:

```bash
dotnet tool install --global dotnet-ef
```

---

### 1. Start infrastructure only

For a clean local verification, you can remove existing containers and volumes:

```bash
docker compose down -v
```

Then start only PostgreSQL databases and RabbitMQ:

```bash
docker compose up -d orders-db inventory-db notifications-db rabbitmq
```

Check that infrastructure is healthy:

```bash
docker compose ps
```

Expected:

```text
orders-db          healthy
inventory-db       healthy
notifications-db   healthy
rabbitmq           healthy
```

---

### 2. Apply EF Core migrations

The project does not run database migrations automatically at application startup.

Set development environment first.

#### PowerShell

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
```

#### Bash

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

Check container status:

```bash
docker compose ps
```

Expected:

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

### 4. Verify health endpoints

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

or a JSON response with:

```json
{
  "status": "Healthy"
}
```

---

## Successful stock reservation scenario

Use this product ID for the verification:

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

If the item already exists, either continue with the existing item or update it:

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

### 2. Create order

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

---

### 3. Verify order status

Replace `{orderId}` with the returned order ID:

```bash
curl "http://localhost:5081/api/v1/orders/{orderId}"
```

Expected final status:

```json
{
  "status": "StockReserved"
}
```

---

### 4. Verify inventory quantity

```bash
curl "http://localhost:5082/api/v1/inventory-items/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
```

Expected values:

```text
availableQuantity = 48
reservedQuantity = 2
```

---

### 5. Verify notifications

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

## Failed stock reservation scenario

This scenario verifies the insufficient stock path.

---

### 1. Create order with unavailable quantity

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

---

### 2. Verify order status

```bash
curl "http://localhost:5081/api/v1/orders/{orderId}"
```

Expected final status:

```json
{
  "status": "StockReservationFailed"
}
```

---

### 3. Verify notifications

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

## Database verification

You can also verify the flow directly in PostgreSQL.

---

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

Expected statuses:

```text
StockReserved
StockReservationFailed
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

Expected:

```text
OrderCreated -> Published
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

Expected consumers:

```text
orders.stock-reserved-consumer
orders.stock-reservation-failed-consumer
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

Expected statuses:

```text
Reserved
Failed
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

Expected:

```text
StockReserved -> Published
StockReservationFailed -> Published
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

Expected consumer:

```text
inventory.order-created-consumer
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

Expected event types:

```text
OrderCreated
StockReserved
StockReservationFailed
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

Expected consumers:

```text
notifications.order-created-consumer
notifications.stock-reserved-consumer
notifications.stock-reservation-failed-consumer
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

If a DLQ contains messages, inspect the service logs.

---

## Useful logs

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

## Common troubleshooting

### API container is unhealthy

Check readiness endpoint from the host:

```bash
curl http://localhost:5081/health/ready
curl http://localhost:5082/health/ready
curl http://localhost:5083/health/ready
```

Then check the same endpoint from inside the container:

```bash
docker exec orders-api curl --fail http://localhost:8080/health/ready
docker exec inventory-api curl --fail http://localhost:8080/health/ready
docker exec notifications-api curl --fail http://localhost:8080/health/ready
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

The sample uses a fixed product ID:

```text
aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee
```

If you run the test repeatedly, creating the same inventory item can return conflict.

Either update the item with `PUT`, or use a different `productId`.

---

### Outbox messages stay Pending

Check whether RabbitMQ queues and bindings exist.

In RabbitMQ UI, verify:

```text
ordersystem.events exchange
inventory.order-created queue
orders.stock-reserved queue
orders.stock-reservation-failed queue
notifications.* queues
```

Also inspect service logs:

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

## Local smoke test script

A simple PowerShell smoke test is available in:

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

The smoke test checks both scenarios:

```text
1. Successful stock reservation
2. Failed stock reservation because of insufficient stock
```

---

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

Expected result:

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

### Apply EF Core migrations

The project does not run database migrations automatically at application startup.

Apply migrations manually:

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

### Run the smoke test

#### PowerShell 7+

```powershell
pwsh ./scripts/smoke-test.ps1
```

#### Windows PowerShell

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-test.ps1
```

---

### Optional parameters

You can override service URLs:

```powershell
pwsh ./scripts/smoke-test.ps1 `
  -OrdersBaseUrl "http://localhost:5081" `
  -InventoryBaseUrl "http://localhost:5082" `
  -NotificationsBaseUrl "http://localhost:5083"
```

You can also increase the timeout for slower machines:

```powershell
pwsh ./scripts/smoke-test.ps1 -TimeoutSeconds 180
```

---

### Expected successful output

```text
Starting local smoke test.
Orders API:        http://localhost:5081
Inventory API:     http://localhost:5082
Notifications API: http://localhost:5083

==> Checking service readiness
[OK] Orders Service readiness health check is healthy.
[OK] Inventory Service readiness health check is healthy.
[OK] Notifications Service readiness health check is healthy.

==> Creating inventory item
[OK] Inventory item ... was created.

==> Creating order for successful stock reservation
[OK] Successful scenario order ... was created.

==> Waiting for successful order to become StockReserved
[OK] Order ... reached status StockReserved.

==> Checking inventory quantity after successful reservation
[OK] Inventory quantities are correct after successful reservation.

==> Checking notifications for successful scenario
[OK] Notification 'was created' for order ... exists.
[OK] Notification 'Stock reserved' for order ... exists.

==> Creating order for failed stock reservation
[OK] Failed scenario order ... was created.

==> Waiting for failed order to become StockReservationFailed
[OK] Order ... reached status StockReservationFailed.

==> Checking notifications for failed scenario
[OK] Notification 'was created' for order ... exists.
[OK] Notification 'Stock reservation failed' for order ... exists.

==> Checking inventory quantity after failed reservation
[OK] Inventory quantities are unchanged after failed reservation.

Smoke test completed successfully.
```

---

### What the script verifies

The script verifies the system as a black-box through HTTP APIs.

It checks:

```text
- all readiness health endpoints,
- creation of an inventory item,
- successful order flow,
- order status transition to StockReserved,
- inventory quantity update,
- notification creation,
- failed order flow,
- order status transition to StockReservationFailed,
- failed notification creation,
- unchanged inventory quantity after failed reservation.
```

The script does not connect directly to PostgreSQL or RabbitMQ.

---

### Troubleshooting

#### Health check fails

Check readiness endpoints manually:

```bash
curl http://localhost:5081/health/ready
curl http://localhost:5082/health/ready
curl http://localhost:5083/health/ready
```

Then inspect logs:

```bash
docker compose logs orders-api --tail=200
docker compose logs inventory-api --tail=200
docker compose logs notifications-api --tail=200
```

---

#### Timeout while waiting for order status

Check RabbitMQ Management UI:

```text
http://localhost:15672
```

Credentials:

```text
guest / guest
```

Inspect these queues:

```text
inventory.order-created
orders.stock-reserved
orders.stock-reservation-failed
notifications.order-created
notifications.stock-reserved
notifications.stock-reservation-failed
```

Also inspect DLQ queues:

```text
inventory.order-created.dlq
orders.stock-reserved.dlq
orders.stock-reservation-failed.dlq
notifications.order-created.dlq
notifications.stock-reserved.dlq
notifications.stock-reservation-failed.dlq
```

If a DLQ contains messages, inspect the corresponding service logs.

---

#### Error: relation does not exist

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

#### API container is unhealthy

Check the readiness endpoint from inside the container.

Orders:

```bash
docker exec orders-api curl --fail http://localhost:8080/health/ready
```

Inventory:

```bash
docker exec inventory-api curl --fail http://localhost:8080/health/ready
```

Notifications:

```bash
docker exec notifications-api curl --fail http://localhost:8080/health/ready
```

---

### Notes

This smoke test is not a replacement for integration tests.

It is intended as a fast local verification that the main asynchronous flow works end-to-end:

```text
HTTP request
  -> outbox
  -> RabbitMQ
  -> consumer
  -> database update
  -> notification
```

Integration tests with Testcontainers should be added separately.
