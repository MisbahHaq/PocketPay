# PayPocket

A fintech-style digital wallet and bill-payment backend built with ASP.NET Core and PostgreSQL.

**Current status: Week 3 — the double-entry ledger.** Building on the Week 2 accounts and authentication, wallet funding now flows through an append-only, double-entry ledger: every movement is a balanced journal entry, balances are derived (never stored), corrections are made with reversals, and an admin endpoint reports a trial balance. Weeks 4–6 will add customer transfers, idempotency and biller payments (see [Known gaps](#known-gaps-weeks-4-6)).

## Architecture

```mermaid
graph TD
    Client[API clients] --> Api[WalletPlatform.Api]
    Api --> Application[WalletPlatform.Application]
    Api --> Infrastructure[WalletPlatform.Infrastructure]
    Infrastructure --> Application
    Application --> Domain[WalletPlatform.Domain]

    Infrastructure --> Postgres[(PostgreSQL)]
    Api -. dev only .-> Scalar[Scalar / OpenAPI UI]
```

Dependencies point inward only. `Domain` has no dependencies at all, `Application` knows only `Domain`, `Infrastructure` implements `Application` ports, and `Api` is the composition root. The compiler enforces this through project references.

| Project | Responsibility |
| --- | --- |
| `src/WalletPlatform.Api` | Controllers, middleware, DI composition, `Program.cs` |
| `src/WalletPlatform.Application` | Use cases, interfaces, DTOs, validators |
| `src/WalletPlatform.Domain` | Entities, value objects, business rules |
| `src/WalletPlatform.Infrastructure` | EF Core `WalletDbContext`, migrations, external adapters |
| `tests/WalletPlatform.UnitTests` | Fast, dependency-free tests |
| `tests/WalletPlatform.IntegrationTests` | API tests against a real Postgres in Docker |

## Tech stack

- .NET 10 (LTS) / ASP.NET Core controllers
- EF Core 10 + Npgsql (PostgreSQL 17)
- Serilog (console sink, request logging)
- xUnit, FluentAssertions 6.x, FsCheck (property-based tests), Testcontainers
- Docker Compose, GitHub Actions

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (running)
- Optional: `psql` or any Postgres client to inspect the database

## Run locally

```bash
# 1. Create your environment file (never commit it)
cp .env.example .env          # Windows: Copy-Item .env.example .env

# 2. Start PostgreSQL (host port 5434 -> container 5432, see .env)
docker compose up -d
docker compose ps            # wait for STATUS = healthy

# 3. Restore local tools and packages
dotnet tool restore
dotnet restore

# 4. Apply database migrations
dotnet ef database update \
  --project src/WalletPlatform.Infrastructure \
  --startup-project src/WalletPlatform.Api

# 5. Run the API
dotnet run --project src/WalletPlatform.Api
```

The API listens on `http://localhost:5178` (from `Properties/launchSettings.json`).

| Endpoint | Auth | Purpose |
| --- | --- | --- |
| `GET /api/ping` | anonymous | App version and server time (UTC) |
| `POST /api/auth/register` | anonymous | Create a customer account and its wallet |
| `POST /api/auth/login` | anonymous | Exchange credentials for an access + refresh token |
| `POST /api/auth/refresh` | anonymous | Rotate a refresh token for a new token pair |
| `GET /api/users/me` | Bearer | Current user profile and wallet summary |
| `GET /api/wallets/{walletId}/balance` | Bearer (Customer) | Derived balance of one of your wallets |
| `GET /api/wallets/{walletId}/statement` | Bearer (Customer) | Paged statement with a running balance |
| `GET /api/admin/users` | Bearer (Admin) | Paged list of users |
| `POST /api/admin/wallets/{walletId}/fund` | Bearer (Admin) | Simulate a deposit into a wallet |
| `POST /api/admin/journal-entries/{id}/reverse` | Bearer (Admin) | Reverse a journal entry (once) |
| `GET /api/admin/ledger/trial-balance` | Bearer (Admin) | Per-currency debit/credit totals |
| `GET /health/live` | anonymous | Liveness — process is up, no dependencies checked |
| `GET /health/ready` | anonymous | Readiness — verifies PostgreSQL connectivity |
| `GET /openapi/v1.json` | anonymous | OpenAPI document (Development only) |
| `GET /scalar/v1` | anonymous | Scalar API UI (Development only) |

> Port 5434 is the default host port because locally installed PostgreSQL services commonly occupy 5432/5433. Change `POSTGRES_PORT` in `.env` (and the connection string) if you prefer another port.

## Run tests

```bash
dotnet test
```

- Unit tests need nothing beyond the SDK.
- Integration tests use Testcontainers, which starts a disposable `postgres:17-alpine` container — Docker must be running. They do not use the compose database.

## Database migrations

```bash
# Add a migration
dotnet ef migrations add <Name> \
  --project src/WalletPlatform.Infrastructure \
  --startup-project src/WalletPlatform.Api

# Apply pending migrations
dotnet ef database update \
  --project src/WalletPlatform.Infrastructure \
  --startup-project src/WalletPlatform.Api
```

## Configuration

The development connection string lives in `src/WalletPlatform.Api/appsettings.Development.json`. Override it anywhere else via the standard ASP.NET Core environment variable:

```bash
ConnectionStrings__PayPocketDb="Host=...;Port=...;Database=...;Username=...;Password=..."
```

`.env` only feeds Docker Compose; the application never reads it directly.

### Authentication configuration

JWT settings live under the `Jwt` section. The signing key is **never** committed — it is supplied out of band via user-secrets (development) or environment variables (deployment):

```bash
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -hex 32)" --project src/WalletPlatform.Api
```

| Key | Default | Notes |
| --- | --- | --- |
| `Jwt:Issuer` | `paypocket-dev` | Validated on token creation and validation |
| `Jwt:Audience` | `paypocket-dev` | Validated on token creation and validation |
| `Jwt:SigningKey` | *(required)* | HMAC-SHA256 key, at least 32 characters |
| `Jwt:AccessTokenMinutes` | `15` | Access token lifetime |
| `Jwt:RefreshTokenDays` | `7` | Refresh token lifetime |

Optionally bootstrap an administrator by setting `SeedAdmin:Email` and `SeedAdmin:Password` (environment variables `SeedAdmin__Email` / `SeedAdmin__Password`). Seeding runs at startup only when both are present, and is skipped if an account with that email already exists.

### Authentication model

- Access tokens are short-lived (15 min) JWTs with `sub`, `email`, `role` and `jti` claims.
- Refresh tokens are 256-bit random values; only their SHA-256 hash is stored. Each use rotates the token, and presenting a revoked token revokes the entire token family.
- Passwords are stored as PBKDF2 hashes via ASP.NET Core Identity's `PasswordHasher<T>` (used standalone, without the rest of Identity). Login enforces a lockout of 5 failed attempts for 15 minutes.
- Login failures return one generic message so the endpoint cannot be used to enumerate accounts.

## The ledger

Money moves only through a **double-entry ledger**. Every movement is a *journal entry*: a small set of *postings* that must satisfy four rules, enforced in the domain and backed by database constraints:

1. At least two postings.
2. Per currency, total debits equal total credits.
3. Every posting amount is strictly positive; the *direction* (debit/credit) carries the sign.
4. A posting's currency matches its account's currency.

A fifth rule — **immutability** — is enforced by a database trigger: ledger rows are append-only, never updated or deleted. A mistake is corrected by posting a **reversal** (the mirror image of the original), not by editing history.

### Accounts and normal balances

| Account | Type | Normal balance | Meaning |
| --- | --- | --- | --- |
| Customer wallet | Liability (credit-normal) | Credit | Money the platform owes the customer |
| `SYSTEM_FUNDING` | Asset (debit-normal) | Debit | Cash the platform holds |
| `SYSTEM_FEES` | Revenue (credit-normal) | Credit | Fees collected |
| `SYSTEM_BILLER_SETTLEMENT` | Liability (credit-normal) | Credit | Money owed to billers pending settlement |

Every customer wallet has exactly one ledger account, created in the same transaction as the wallet. Balances are **derived by summing postings** — there is no balance column, so a cached balance can never drift from the truth.

### Worked example: funding a wallet with PKR 1,000.00

PKR 1,000.00 is stored as **100,000 minor units** (paisa). An admin calls:

```
POST /api/admin/wallets/{walletId}/fund
Authorization: Bearer <admin token>
{ "amountMinorUnits": 100000 }
```

This posts one balanced journal entry (type `Funding`):

| Account | Direction | Amount (minor units) |
| --- | --- | --- |
| `SYSTEM_FUNDING` (asset) | **Debit** | 100,000 |
| Customer wallet (liability) | **Credit** | 100,000 |

Debits (100,000) equal credits (100,000), so the entry balances. The two effects:

- Debiting the funding asset **increases** it by 100,000 — the platform now holds that cash.
- Crediting the customer's liability **increases** it by 100,000 — the platform now owes the customer that amount.

`GET /api/wallets/{walletId}/balance` returns the derived balance:

```json
{ "ledgerAccountId": "…", "walletId": "…", "currency": "PKR",
  "accountType": "Liability", "signedMinorUnits": 100000 }
```

`signedMinorUnits` is positive because the account holds its *normal* (credit) balance.

If the funding was a mistake, an admin reverses it:

```
POST /api/admin/journal-entries/{journalEntryId}/reverse
```

This posts a second entry that swaps direction — debit the wallet, credit funding — so the wallet's derived balance returns to 0. The original entry is untouched; both entries remain visible forever.

`GET /api/admin/ledger/trial-balance` proves the whole ledger is consistent:

```json
{ "isBalanced": true,
  "currencies": [ { "currency": "PKR", "totalDebits": 100000,
                    "totalCredits": 100000, "difference": 0 } ] }
```

### Known gaps (Weeks 4–6)

The ledger deliberately leaves these for later weeks and they are **not** production-safe yet:

- **Customer-initiated transfers** between wallets (only admin funding exists).
- **Idempotency keys** — a retried request can currently post a second entry; the HTTP layer has no request de-duplication.
- **Concurrency control** — no row locking or optimistic concurrency on accounts, so simultaneous operations rely solely on database constraints and the trial balance staying zero-sum.
- **Biller payments and settlement** (`SYSTEM_BILLER_SETTLEMENT` is seeded but unused).
- **Performance** — balances are sum-on-read; statement snapshots are deferred until the ledger is large enough to need them.

## CI

`.github/workflows/ci.yml` runs on pushes and pull requests to `main`: restore, build (Release), test, then uploads `.trx` results as an artifact. Testcontainers works out of the box on the Ubuntu runner because Docker is preinstalled.

## Design decisions

See [docs/DECISIONS.md](docs/DECISIONS.md).
