# PayPocket
A fintech-style digital wallet and bill-payment backend built with ASP.NET Core and PostgreSQL.

**Current status: Weeks 1-8 in progress / scaffolding.** Core ledger, transfers, auth, and scaffolding for idempotency, audit, rate limiting, reconciliation, and admin dashboard are present.

## Architecture

`mermaid
graph TD
    Client[API clients] --> Api[WalletPlatform.Api]
    Api --> Application[WalletPlatform.Application]
    Api --> Infrastructure[WalletPlatform.Infrastructure]
    Infrastructure --> Application
    Application --> Domain[WalletPlatform.Domain]
    Infrastructure --> Postgres[(PostgreSQL)]
    Api -. dev only .-> Scalar[Scalar / OpenAPI UI]
`

Dependencies point inward only. Domain has no dependencies, Application knows only Domain, Infrastructure implements Application ports, and Api is the composition root.

| Project | Responsibility |
| --- | --- |
| src/WalletPlatform.Api | Controllers, middleware, DI composition, Program.cs |
| src/WalletPlatform.Application | Use cases, interfaces, DTOs, validators |
| src/WalletPlatform.Domain | Entities, value objects, business rules |
| src/WalletPlatform.Infrastructure | EF Core WalletDbContext, migrations, external adapters |
| src/WalletPlatform.MockBiller | Mock biller service for bill payments/reconciliation |
| tests/WalletPlatform.UnitTests | Fast, dependency-free tests |
| tests/WalletPlatform.IntegrationTests | API tests against a real Postgres in Docker |
| web/admin-dashboard | Vite + React admin dashboard (scaffolding) |

## Tech stack

- .NET 10 (LTS) / ASP.NET Core controllers
- EF Core 10 + Npgsql (PostgreSQL 17)
- Serilog
- xUnit, FluentAssertions 6.x, FsCheck, Testcontainers
- Docker Compose, GitHub Actions
- React + Vite

## Run locally

`ash
cp .env.example .env
docker compose up -d
dotnet tool restore
dotnet restore
dotnet ef database update --project src/WalletPlatform.Infrastructure --startup-project src/WalletPlatform.Api
dotnet run --project src/WalletPlatform.Api
`

API on http://localhost:5178, Scalar on http://localhost:5178/scalar/v1 (dev). Admin dashboard on http://localhost:5173.

## Admin dashboard

`ash
cd web/admin-dashboard
npm install
npm run dev
`

## Tests

`ash
dotnet test
`

## Progress summary

### Done
- Clean architecture, DI, health checks
- Auth (register/login/refresh), JWT, roles, password hashing, lockout
- Double-entry ledger, derived balances, immutability, reversals, trial balance
- Wallet transfers with state machine, fees, validation
- Idempotency/concurrency scaffolding, account locker
- Bill payments/outbox entities and mock biller
- Audit with hash chaining wired
- Rate limiting options/policies/extensions wired
- Security headers, correlation ID, forwarded headers
- Reconciliation scaffolding (matching SQL, policies, service, controller, scheduler)
- Functional admin dashboard UI (Vite + React + TypeScript) with Dashboard, Users, Transfers, Reconciliation, Audit, in-memory tokens, single-flight refresh, and full test suite

### Remaining
- Enforce rate limiting per policy
- Apply audit across critical operations
- Fix transfer concurrency (row locking/serializable)
- Complete reconciliation matching/import/processing
- Complete bill payments flows
- Stabilize integration tests
