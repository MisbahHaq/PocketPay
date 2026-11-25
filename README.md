# PayPocket
A fintech-style digital wallet and bill-payment backend built with ASP.NET Core and PostgreSQL.
<img width="1903" height="1079" alt="Screenshot 2026-11-24 123249" src="https://github.com/user-attachments/assets/f2208342-f325-4dc5-b8da-d1b60d176725" />
<img width="1919" height="1079" alt="Screenshot 2026-11-24 123230" src="https://github.com/user-attachments/assets/4bb1fbf0-3acd-4118-8e8c-6d9838c70794" />
<img width="1919" height="1079" alt="Screenshot 2026-11-24 123303" src="https://github.com/user-attachments/assets/8cb863c6-26c7-4160-839e-566b370dca92" />
<img width="1906" height="1079" alt="Screenshot 2026-11-24 123318" src="https://github.com/user-attachments/assets/76a79a84-ba05-4c4f-b9cd-dd8fe66b61b4" />


## Why this project exists
PayPocket exists as a learning/portfolio project to practice building a production-grade fintech backend: double-entry accounting, wallet transfers, idempotency, audit trails, and reconciliation. It demonstrates clean architecture, domain-driven design, and the operational concerns (rate limiting, audit, reconciliation) required of real payment systems.

## What I'm using and why
- **.NET 10 / ASP.NET Core** — LTS runtime with mature ecosystem for APIs; controllers + middleware pipeline fit the layered design.
- **EF Core 10 + Npgsql (PostgreSQL 17)** — strongly-typed ORM with migrations; Postgres gives reliable ACID transactions and JSONB support for audit/outbox payloads.
- **Serilog** — structured logging with sinks/correlation for debugging payment flows.
- **xUnit + FluentAssertions + FsCheck + Testcontainers** — unit/property tests plus real Postgres integration tests.
- **Docker Compose + GitHub Actions** — reproducible local dev and CI.
- **React + Vite (admin dashboard)** — fast dev server and component model for the ops/admin UI.
- **Scalar / OpenAPI** — interactive API docs in dev only.


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
