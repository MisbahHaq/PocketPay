# Inventory Management System

Warehouse inventory management: users and roles, product catalog, stock levels and
movements, purchasing, and sales.

Built in phases. This is **Phase 1**: solution structure, PostgreSQL schema, and
authentication. Later phases add catalog, stock, purchasing, and sales.

## Status

| Area | State |
| --- | --- |
| Solution structure and layer boundaries | done |
| PostgreSQL schema and EF Core migrations | done |
| Authentication and authorisation | done |
| Catalog | phase 2 |
| Stock | phase 3 |
| Purchasing | phase 4 |
| Sales | phase 5 |
| Containers, CI, observability | phase 6 |

## Requirements

- .NET SDK 8.0 (pinned in `backend/global.json`)
- PostgreSQL 14 or newer. Developed against 18.
- Node.js 20 or newer, for the frontend

## Layout

```
backend/
  src/Inventory.Domain          entities, value objects, domain events, exceptions
  src/Inventory.Application     use cases, validators, abstractions
  src/Inventory.Infrastructure  EF Core, Npgsql, Identity, JWT, seeding
  src/Inventory.Api             controllers, middleware, configuration
  tests/Inventory.UnitTests         no database needed
  tests/Inventory.IntegrationTests  boots the API against a real PostgreSQL
frontend/                       React + TypeScript + Vite + Tailwind + shadcn/ui
docs/
  architecture.md  layer boundaries, tokens, concurrency, transactions
  erd.md           entity relationship diagram
  api.md           endpoint reference
  testing.md       how to run the suites
scripts/
  create-test-role.sql   one-time test database setup
```

## Getting started

### 1. Database

Create the role and database used by the integration tests. Run once as the PostgreSQL
superuser:

```powershell
psql -U postgres -h localhost -d postgres -v ON_ERROR_STOP=1 -f scripts/create-test-role.sql
```

This creates a least-privilege role, deliberately without `SUPERUSER` or `CREATEDB`, so
the suite never needs privileges a CI runner would lack. Details in
[docs/testing.md](docs/testing.md).

### 2. Backend configuration

Secrets are not committed. Store them as user secrets:

```powershell
cd backend/src/Inventory.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=inventory_test;Username=inventory_test;Password=inventory_test_local"
dotnet user-secrets set "Jwt:SigningKey" "replace-this-with-at-least-32-random-characters"
```

Generate a signing key with:

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
```

`Jwt:SigningKey` is validated at startup. A short or missing key fails immediately rather
than at the first login attempt, where the failure would look like an authentication bug.

### 3. Apply migrations

```powershell
cd backend
dotnet tool restore
dotnet ef database update --project src/Inventory.Infrastructure --startup-project src/Inventory.Api
```

Migrations also apply automatically on startup in the Development environment. The
production default is off, so several instances starting together cannot race each other
into a schema change.

### 4. Run the API

```powershell
cd backend/src/Inventory.Api
dotnet run
```

Swagger UI is at `/swagger`.

### 5. Create the first admin

Seeding creates the roles and, in Development only, an administrator when
`Database:Admin:Email` and `Database:Admin:Password` are configured. Otherwise promote an
account by hand:

```sql
UPDATE users SET is_active = true WHERE email = 'you@example.com';
INSERT INTO user_roles (user_id, role_id)
SELECT u.id, r.id FROM users u, roles r
WHERE u.email = 'you@example.com' AND r.name = 'Admin';
```

### 6. Frontend

```powershell
cd frontend
npm install
npm run dev
```

## Tests

```powershell
dotnet test backend/Inventory.sln
```

Unit tests need nothing. Integration tests need the test database from step 1. See
[docs/testing.md](docs/testing.md).

## Roles

| Role | May do |
| --- | --- |
| `Admin` | manage users, configure the system, hard-delete catalog rows, approve stock |
| `Manager` | manage catalog and pricing, approve purchase orders, confirm sales orders, book adjustments |
| `Staff` | read the catalog, receive goods, issue stock, record transfers |

Recording a movement and approving it are separate permissions on purpose: no single
account can both cause a stock change and sign it off, which is what makes the audit trail
worth keeping.

## Conventions

- `snake_case` for all database identifiers.
- UUIDv7 for primary keys, sequential for index locality.
- RFC 7807 `ProblemDetails` for every error.
- Deny by default: an endpoint without an authorisation attribute is unreachable.
- Authorization is defined as named policies, never as inline role checks, so a
  requirement can be reviewed in one place.

## Licence

Not yet decided.