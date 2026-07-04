# Local Auth and Gateway Curl Examples

This document contains local curl examples for the API Gateway, Keycloak token retrieval, role-based authorization, downstream direct-call verification, and rate limiting.

## Local URLs

API Gateway:

```
http://localhost:5080
```

Keycloak:

```
http://localhost:18080
```

Keycloak Admin Console:

```
http://localhost:18080/admin
```

RabbitMQ Management UI:

```
http://localhost:15672
```

Downstream debug-only URLs:

```
http://localhost:5081
http://localhost:5082
http://localhost:5083
```

## Test users

Local Keycloak realm:

```
order-system
```

Client:

```
order-system-api
```

Users:

```
alice.customer / Alice123! / role: customer
sam.support    / Sam123!   / role: support
anna.admin     / Anna123!  / role: admin
```

## Start the local stack

```
docker compose up -d --build
```

Check containers:

```
docker compose ps
```

## Gateway health checks

Public gateway liveness:

```
curl -i http://localhost:5080/health
```

Gateway liveness with JSON response:

```
curl -i http://localhost:5080/health/live
```

Gateway readiness with downstream and Keycloak checks:

```
curl -i http://localhost:5080/health/ready
```

Expected readiness result:

```
HTTP/1.1 200 OK
```

The response should include:

```
orders-api          Healthy
inventory-api       Healthy
notifications-api   Healthy
keycloak            Healthy
```

## Get customer token

Bash:

```
CUSTOMER_TOKEN=$(curl -s -X POST "http://localhost:18080/realms/order-system/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=order-system-api" \
  -d "grant_type=password" \
  -d "username=alice.customer" \
  -d "password=Alice123!" | jq -r ".access_token")
```

PowerShell:

```
$customerTokenResponse = Invoke-RestMethod `
  -Method Post `
  -Uri "http://localhost:18080/realms/order-system/protocol/openid-connect/token" `
  -ContentType "application/x-www-form-urlencoded" `
  -Body @{
    client_id = "order-system-api"
    grant_type = "password"
    username = "alice.customer"
    password = "Alice123!"
  }

$CUSTOMER_TOKEN = $customerTokenResponse.access_token
```

## Get support token

Bash:

```
SUPPORT_TOKEN=$(curl -s -X POST "http://localhost:18080/realms/order-system/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=order-system-api" \
  -d "grant_type=password" \
  -d "username=sam.support" \
  -d "password=Sam123!" | jq -r ".access_token")
```

PowerShell:

```
$supportTokenResponse = Invoke-RestMethod `
  -Method Post `
  -Uri "http://localhost:18080/realms/order-system/protocol/openid-connect/token" `
  -ContentType "application/x-www-form-urlencoded" `
  -Body @{
    client_id = "order-system-api"
    grant_type = "password"
    username = "sam.support"
    password = "Sam123!"
  }

$SUPPORT_TOKEN = $supportTokenResponse.access_token
```

## Get admin token

Bash:

```
ADMIN_TOKEN=$(curl -s -X POST "http://localhost:18080/realms/order-system/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=order-system-api" \
  -d "grant_type=password" \
  -d "username=anna.admin" \
  -d "password=Anna123!" | jq -r ".access_token")
```

PowerShell:

```
$adminTokenResponse = Invoke-RestMethod `
  -Method Post `
  -Uri "http://localhost:18080/realms/order-system/protocol/openid-connect/token" `
  -ContentType "application/x-www-form-urlencoded" `
  -Body @{
    client_id = "order-system-api"
    grant_type = "password"
    username = "anna.admin"
    password = "Anna123!"
  }

$ADMIN_TOKEN = $adminTokenResponse.access_token
```

## Verify 401 Unauthorized

Missing token:

```
curl -i http://localhost:5080/api/v1/orders
```

Expected:

```
HTTP/1.1 401 Unauthorized
```

## Verify customer access

Customer can create an order through the gateway:

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
        "quantity": 1
      }
    ]
  }'
```

Expected authorization result:

```
Not 401
Not 403
```

The final status code may still depend on application data and validation.

Customer can get a specific order by ID:

```
curl -i "http://localhost:5080/api/v1/orders/{orderId}" \
  -H "Authorization: Bearer $CUSTOMER_TOKEN"
```

Customer cannot list all orders:

```
curl -i "http://localhost:5080/api/v1/orders" \
  -H "Authorization: Bearer $CUSTOMER_TOKEN"
```

Expected:

```
HTTP/1.1 403 Forbidden
```

## Verify support access

Support can list orders:

```
curl -i "http://localhost:5080/api/v1/orders" \
  -H "Authorization: Bearer $SUPPORT_TOKEN"
```

Support can read inventory:

```
curl -i "http://localhost:5080/api/v1/inventory-items" \
  -H "Authorization: Bearer $SUPPORT_TOKEN"
```

Support can read notifications:

```
curl -i "http://localhost:5080/api/v1/notifications" \
  -H "Authorization: Bearer $SUPPORT_TOKEN"
```

Support cannot create inventory items:

```
curl -i -X POST "http://localhost:5080/api/v1/inventory-items" \
  -H "Authorization: Bearer $SUPPORT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "productId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
    "productName": "Keyboard",
    "availableQuantity": 50
  }'
```

Expected:

```
HTTP/1.1 403 Forbidden
```

## Verify admin access

Admin can create inventory items:

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

Admin can update inventory items:

```
curl -i -X PUT "http://localhost:5080/api/v1/inventory-items/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "productName": "Keyboard",
    "availableQuantity": 50
  }'
```

Expected authorization result:

```
Not 401
Not 403
```

The final status code may still depend on current data.

## Verify internal endpoint protection through gateway

Downstream Swagger must not be exposed through the gateway:

```
curl -i http://localhost:5080/api/v1/orders/swagger \
  -H "Authorization: Bearer $ADMIN_TOKEN"

curl -i http://localhost:5080/api/v1/inventory-items/swagger \
  -H "Authorization: Bearer $ADMIN_TOKEN"

curl -i http://localhost:5080/api/v1/notifications/swagger \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

Expected:

```
HTTP/1.1 404 Not Found
```

Gateway does not expose downstream health through service paths:

```
curl -i http://localhost:5080/api/v1/orders/health/ready \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

Expected:

```
HTTP/1.1 404 Not Found
```

## Verify direct downstream protection

Direct downstream calls are exposed only for local debugging.

Without token:

```
curl -i http://localhost:5081/api/v1/orders
curl -i http://localhost:5082/api/v1/inventory-items
curl -i http://localhost:5083/api/v1/notifications
```

Expected:

```
HTTP/1.1 401 Unauthorized
```

With support token:

```
curl -i http://localhost:5081/api/v1/orders \
  -H "Authorization: Bearer $SUPPORT_TOKEN"

curl -i http://localhost:5082/api/v1/inventory-items \
  -H "Authorization: Bearer $SUPPORT_TOKEN"

curl -i http://localhost:5083/api/v1/notifications \
  -H "Authorization: Bearer $SUPPORT_TOKEN"
```

Expected authorization result:

```
Not 401
Not 403
```

## Verify rate limiting

Order creation route has a stricter rate limit.

Send 6 requests quickly:

```
for i in {1..6}; do
  echo "Request $i"

  curl -s -o /dev/null -w "%{http_code}\n" \
    -X POST "http://localhost:5080/api/v1/orders" \
    -H "Authorization: Bearer $CUSTOMER_TOKEN" \
    -H "Content-Type: application/json" \
    -d '{
      "customerName": "John Doe",
      "customerEmail": "john.doe@example.com",
      "items": [
        {
          "productId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
          "productName": "Keyboard",
          "quantity": 1
        }
      ]
    }'
done
```

Expected:

```
The sixth request should return 429.
```

Example:

```
201
201
201
201
201
429
```

The first five responses can also be 400 or 409 depending on validation and current data. The important part is that the sixth request returns:

```
429 Too Many Requests
```

## Decode token locally

Bash:

```
echo "$SUPPORT_TOKEN" | cut -d "." -f2 | base64 -d | jq
```

PowerShell:

```
$payload = $SUPPORT_TOKEN.Split('.')[1]
$payload = $payload.Replace('-', '+').Replace('_', '/')
switch ($payload.Length % 4) {
  2 { $payload += "==" }
  3 { $payload += "=" }
}
[System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload)) | ConvertFrom-Json
```

Expected useful claims:

```
preferred_username
roles
aud
iss
exp
```

## Common expected responses

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
