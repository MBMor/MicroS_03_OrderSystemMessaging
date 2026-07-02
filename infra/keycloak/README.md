# Keycloak Local Development Configuration

This directory contains local-only Keycloak configuration for the API Gateway + Identity project.

## Purpose

Keycloak is used as the local Identity Provider for development and learning purposes.

It provides:

- realm: `order-system`
- client: `order-system-api`
- realm roles:
  - `customer`
  - `support`
  - `admin`
- development users:
  - `alice.customer`
  - `sam.support`
  - `anna.admin`

## Files

```text
infra/keycloak/
  README.md
  import/
    .gitkeep
    order-system-realm.json
```

## Local-only credentials

The Docker Compose setup uses development-only credentials.

### Keycloak admin

```text
Username: admin
Password: admin
```

### Keycloak database

```text
Username: keycloak
Password: keycloak
Database: keycloak
```

### Development users

```text
Username: alice.customer
Password: Alice123!
Role: customer
```

```text
Username: sam.support
Password: Sam123!
Role: support
```

```text
Username: anna.admin
Password: Anna123!
Role: admin
```

These credentials are local-development only.

Do not use them outside local development.

## Local URLs

From the host machine:

```text
Keycloak base URL:
http://localhost:18080

Keycloak Admin Console:
http://localhost:18080/admin

Realm URL:
http://localhost:18080/realms/order-system
```

From containers in Docker Compose:

```text
http://keycloak:8080
```

## Realm import

Realm import files are placed here:

```text
infra/keycloak/import
```

Inside the Keycloak container this directory is mounted as:

```text
/opt/keycloak/data/import
```

The imported realm file is:

```text
infra/keycloak/import/order-system-realm.json
```

The Keycloak container is configured with:

```text
start-dev --import-realm
```

## Client

The local API client is:

```text
order-system-api
```

This client is configured as a public OpenID Connect client for local development.

It allows Direct Access Grants so that local testing can retrieve tokens using simple curl commands.

This is for learning and local testing only.

## Token endpoint

From the host machine, the token endpoint is:

```text
http://localhost:18080/realms/order-system/protocol/openid-connect/token
```

From containers in Docker Compose, the equivalent internal URL is:

```text
http://keycloak:8080/realms/order-system/protocol/openid-connect/token
```

## Role mapping

The realm import configures a protocol mapper that emits realm roles into a flat access token claim:

```json
"roles": ["customer"]
```

or:

```json
"roles": ["support"]
```

or:

```json
"roles": ["admin"]
```

The ASP.NET Core API Gateway will later configure role mapping to use this claim as the role claim type.

## Audience

The realm import configures an audience mapper so access tokens include:

```json
"aud": "order-system-api"
```

This will allow the API Gateway and downstream services to validate that the token was issued for this API.

## Important issuer URL note

JWT issuer URLs are sensitive.

For local Docker Compose, there are two relevant URLs:

```text
Host URL:
http://localhost:18080/realms/order-system

Docker network URL:
http://keycloak:8080/realms/order-system
```

The API Gateway and downstream services must validate the issuer that appears in the JWT access token.

This project will use explicit configuration for JWT authority and issuer validation instead of hard-coding URLs in application code.

## Production note

This Keycloak setup is local-development only.

Production would require, at minimum:

- real secrets
- HTTPS
- hardened hostname settings
- proper backup strategy
- production database configuration
- admin credentials stored outside source control
- no password grant flow for normal client applications