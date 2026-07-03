# Public vs Internal Endpoints

This document classifies public, protected, internal, debug-only, and infrastructure endpoints for the Order System Messaging project with API Gateway and Keycloak.

## Goal

The API Gateway is the main public HTTP boundary of the system.

Client applications should call the system through the API Gateway.

Downstream services should not be treated as public APIs, even if their host ports are exposed during local development for debugging.

## Public entry point

The public local development entry point is:

```text
http://localhost:5080
```

This is the API Gateway.

The gateway is responsible for:

* reverse proxy routing
* JWT bearer authentication
* route-level authorization
* role-based access control
* future rate limiting
* keeping downstream services behind a clear HTTP boundary

## Public health endpoint

The gateway exposes a public health endpoint:

```http
GET /health
```

Full local URL:

```text
http://localhost:5080/health
```

Expected behavior:

```text
200 OK
```

This endpoint does not require authentication.

## Public protected API routes through API Gateway

The following routes are exposed through the API Gateway.

All API routes require a valid JWT access token from the local Keycloak realm `order-system`.

### Orders

| Method | Path                  | Gateway policy      | Intended access    |
| ------ | --------------------- | ------------------- | ------------------ |
| `POST` | `/api/v1/orders`      | `CanCreateOrder`    | authenticated user |
| `GET`  | `/api/v1/orders/{id}` | `AuthenticatedUser` | authenticated user |
| `GET`  | `/api/v1/orders`      | `SupportOrAdmin`    | support or admin   |

### Inventory

| Method | Path                                  | Gateway policy       | Intended access  |
| ------ | ------------------------------------- | -------------------- | ---------------- |
| `GET`  | `/api/v1/inventory-items`             | `SupportOrAdmin`     | support or admin |
| `GET`  | `/api/v1/inventory-items/{productId}` | `SupportOrAdmin`     | support or admin |
| `POST` | `/api/v1/inventory-items`             | `CanManageInventory` | admin            |
| `PUT`  | `/api/v1/inventory-items/{productId}` | `CanManageInventory` | admin            |

### Notifications

| Method | Path                         | Gateway policy         | Intended access  |
| ------ | ---------------------------- | ---------------------- | ---------------- |
| `GET`  | `/api/v1/notifications`      | `CanReadNotifications` | support or admin |
| `GET`  | `/api/v1/notifications/{id}` | `CanReadNotifications` | support or admin |

## Gateway behavior

Expected gateway responses:

| Situation                         | Expected response           |
| --------------------------------- | --------------------------- |
| Missing token                     | `401 Unauthorized`          |
| Invalid token                     | `401 Unauthorized`          |
| Valid token but insufficient role | `403 Forbidden`             |
| Valid token and sufficient role   | downstream service response |
| Unknown gateway route             | `404 Not Found`             |
| Method not explicitly routed      | `404 Not Found`             |

## Local debug-only downstream URLs

The following URLs may be exposed during local Docker Compose development.

They are for local debugging only.

They should not be treated as public client entry points.

### Orders Service

```text
http://localhost:5081
```

Examples:

```http
GET /api/v1/orders
GET /api/v1/orders/{id}
POST /api/v1/orders
```

### Inventory Service

```text
http://localhost:5082
```

Examples:

```http
GET /api/v1/inventory-items
GET /api/v1/inventory-items/{productId}
POST /api/v1/inventory-items
PUT /api/v1/inventory-items/{productId}
```

### Notifications Service

```text
http://localhost:5083
```

Examples:

```http
GET /api/v1/notifications
GET /api/v1/notifications/{id}
```

## Downstream service protection

Downstream services must validate JWT tokens for protected HTTP endpoints.

This provides defense in depth.

The gateway is the public boundary, but downstream services still protect sensitive endpoints.

Expected direct downstream behavior:

| Request type                                             | Expected response    |
| -------------------------------------------------------- | -------------------- |
| Direct downstream call without token                     | `401 Unauthorized`   |
| Direct downstream call with invalid token                | `401 Unauthorized`   |
| Direct downstream call with valid token but wrong role   | `403 Forbidden`      |
| Direct downstream call with valid token and correct role | application response |

## Internal and diagnostic endpoints

Downstream service health endpoints are internal or local diagnostic endpoints.

Examples:

```text
http://localhost:5081/health/live
http://localhost:5081/health/ready

http://localhost:5082/health/live
http://localhost:5082/health/ready

http://localhost:5083/health/live
http://localhost:5083/health/ready
```

These endpoints are used for local diagnostics and Docker health checks.

They are not routed through the API Gateway.

## Infrastructure endpoints

The following endpoints are infrastructure or admin endpoints.

They are local-development only.

### Keycloak

```text
http://localhost:18080
http://localhost:18080/admin
```

Keycloak is the local Identity Provider.

The admin console is not a public application endpoint.

### RabbitMQ Management UI

```text
http://localhost:15672
```

RabbitMQ Management UI is a local broker administration tool.

It must not be exposed as a public application endpoint.

### PostgreSQL databases

The PostgreSQL containers are infrastructure dependencies.

They are not HTTP APIs.

They should not be treated as public endpoints.

## Not publicly routed through the API Gateway

The following must not be exposed through the API Gateway:

* downstream service health endpoints
* downstream Swagger endpoints
* RabbitMQ Management UI
* Keycloak Admin Console
* PostgreSQL ports
* message consumer internals
* database administration endpoints
* broad catch-all routes to downstream services
* service-specific diagnostic endpoints

## Route exposure rule

The API Gateway should expose only explicit route and method combinations.

Avoid broad catch-all proxy rules such as:

```text
/api/v1/{**catch-all}
```

Avoid exposing all downstream service paths automatically.

Each public route should have:

* explicit path
* explicit HTTP method
* explicit authorization policy
* explicit downstream cluster

## Current public boundary

The intended public boundary is:

```text
Client
  -> ApiGateway
  -> Downstream services
```

The downstream services are still separately reachable in local development only for debugging and verification.

Production-like access should treat the API Gateway as the only public HTTP entry point.

## Security notes

Authentication proves who the caller is.

Authorization decides what the caller can do.

The system must not trust spoofable client-provided identity headers, for example:

```text
X-User-Id
X-Role
X-Email
```

JWT access tokens remain the source of truth for user identity and roles.

## Local development limitation

Local Docker Compose security is not production security.

Production would require, at minimum:

* HTTPS
* real secrets
* hardened Keycloak hostname configuration
* no public database ports
* no public RabbitMQ Management UI
* restricted network access to downstream services
* centralized secret management
* production-grade observability and audit logging
