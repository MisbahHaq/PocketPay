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
| Backend authentication and authorisation | done |
| Frontend shell, login flow, session handling | done |
| Catalog | phase 2 |
| Stock | phase 3 |
| Purchasing | phase 4 |
| Sales | phase 5 |
| Containers, CI, observability | phase 6 |

## Stack

**Backend** .NET 8, EF Core 8, Npgsql, ASP.NET Core Identity, MediatR, FluentValidation,
Serilog, Swashbuckle

**Frontend** React 18, TypeScript, Vite 6, Tailwind CSS 4, shadcn/ui (Radix),
React Router 7, TanStack Query, Axios, React Hook Form, Zod

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
frontend/
  src/components/ui/       shadcn/ui primitives
  src/context/             AuthContext: session state
  src/lib/api.ts           axios instance, token refresh, error shaping
  src/lib/auth/            token storage and auth calls
  src/pages/               route components
  src/routes/              route table and auth guards
  src/types/api.ts         API response types
docs/
  architecture.md  layer boundaries, tokens, concurrency, transactions
  erd.md           entity relationship diagram
  api.md           endpoint reference
  testing.md       how to run the test suites
  frontend.md      frontend architecture and request flow
  troubleshooting.md  first thing to read when a step fails
scripts/
  create-test-role.sql   one-time test database setup
```

## Getting started

If a step below fails, check [docs/troubleshooting.md](docs/troubleshooting.md) first - it covers every error message the commands above can produce.

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

Secrets are not committed. `Inventory.Api` already carries a `UserSecretsId`, so there is
no `init` step: running `dotnet user-secrets init` would generate a *new* id and orphan
anything already stored.

```powershell
cd backend/src/Inventory.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=inventory_test;Username=inventory_test;Password=inventory_test_local"
dotnet user-secrets set "Jwt:SigningKey" "<generated value, see below>"
```

Generate a signing key with:

```powershell
$r = [Security.Cryptography.RandomNumberGenerator]::Create(); $b = New-Object byte[] 48; $r.GetBytes($b); [Convert]::ToBase64String($b)
```

(The static `RandomNumberGenerator::GetBytes` overload does not exist in Windows
PowerShell 5.1, hence the explicit `Create()`.)

To seed the first administrator in Development, set these as well. The keys are
`Seed:...`, not `Database:Admin:...`, and both are required:

```powershell
dotnet user-secrets set "Seed:AdminEmail" "you@example.com"
dotnet user-secrets set "Seed:AdminPassword" "a-strong-password-here"
```

If either seed key is missing, seeding logs a line saying so and skips the administrator,
which is deliberate: this codebase will not invent a default credential.

`Jwt:SigningKey` is validated at startup. A short or missing key fails immediately rather
than at the first login attempt, where the failure would look like an authentication bug.

### 3. Apply migrations

```powershell
cd backend
dotnet tool restore
dotnet ef database update --project src/Inventory.Infrastructure --startup-project src/Inventory.Api
```

Migrations also apply automatically on startup in the Development environment, so this
step is optional on a first run. The production default is off, so several instances
starting together cannot race each other into a schema change.

### 4. Run the API

```powershell
cd backend/src/Inventory.Api
dotnet run --launch-profile http
```

Listens on **http://localhost:5126**. Swagger UI is at <http://localhost:5126/swagger>.

`--launch-profile http` matters: without it `dotnet run` uses the first profile in
`launchSettings.json`, which is `http` anyway, but naming it makes the port explicit.
That port is also what the frontend proxies to.

### 5. Run the frontend

```powershell
cd frontend
npm install
npm run dev
```

Open <http://localhost:5173>. Vite proxies `/api` and `/health` to
`http://localhost:5126`, so the browser stays on one origin and CORS never enters the
picture while developing.

Both terminals must be running: the API and the dev server. If the login page reports
"Cannot reach the server", the API is not running or is on a different port than
`vite.config.ts` expects.

Other frontend commands:

```powershell
npm run build       # type-check, then production build into dist/
npm run preview     # serve the production build locally
npm run typecheck   # types only
npm run lint        # eslint
```

To point the client at a different API origin, set `VITE_API_BASE_URL` in
`frontend/.env.local`. Leave it unset in development so the proxy applies; set an absolute
URL only when the API is on another host.

### 6. Sign in

<http://localhost:5173/login>

If you skipped the `Seed:AdminEmail` step and have no account, promote one by hand once
the API has started and created the roles:

```sql
UPDATE users SET is_active = true WHERE email = 'you@example.com';
INSERT INTO user_roles (user_id, role_id)
SELECT u.id, r.id FROM users u, roles r
WHERE u.email = 'you@example.com' AND r.name = 'Admin';
```

The password must still have been set when the account was created, since Identity stores
only a hash of it.

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