# Architecture Decision Records

Format: **Context / Decision / Consequences**. Entries are append-only; supersede rather than rewrite.

---

## ADR-001: Modular monolith over microservices

**Context**
PayPocket needs wallets, a double-entry ledger, transfers, bill payments, idempotency, an outbox, and a reconciliation job. These parts share one transactional consistency boundary: a transfer must debit and credit atomically. Distributed transactions across services add significant operational and correctness cost, and the team is a single developer.

**Decision**
Build a single deployable ASP.NET Core process organized into strongly separated layers/projects. Module boundaries are enforced by project references and namespaces rather than network calls.

**Consequences**
- Money movements can use one ACID transaction; no sagas or distributed locks for the core flow.
- One build, one deploy, one database — far cheaper to operate and debug.
- Scaling is vertical/horizontal replicas of the whole app, not per-module scaling.
- If a module later needs independent scaling (e.g. the reconciliation job), it can be extracted along an existing seam because boundaries are explicit. Until then, microservices would be premature.

---

## ADR-002: PostgreSQL as the database

**Context**
The ledger demands strict ACID guarantees, correct concurrency under contention (row locks / serializable isolation), and reliable constraints (unique idempotency keys, non-negative balances). The workload is transactional, not analytical.

**Decision**
Use PostgreSQL 17 via EF Core 10 with the Npgsql provider, running locally in Docker Compose.

**Consequences**
- Strong transactional guarantees and mature locking primitives for wallet transfers.
- Native `numeric`/`decimal` support avoids floating-point money errors.
- Rich indexing (partial, covering, unique) supports idempotency and outbox patterns.
- Operationally heavier than an embedded database, which is acceptable because Docker makes local setup one command.
- EF Core abstraction keeps most code provider-agnostic, but migrations and some SQL will be Postgres-specific.

---

## ADR-003: Clean-architecture layering

**Context**
Business rules (double-entry invariants, balance constraints) must be testable without a database, and infrastructure choices (EF Core, HTTP adapters) should be replaceable without rewriting use cases. The codebase will grow for ten weeks.

**Decision**
Four projects with dependencies pointing inward:
`Api -> Application, Infrastructure`; `Infrastructure -> Application`; `Application -> Domain`; `Domain -> nothing`.

**Consequences**
- Domain and Application can be unit-tested in milliseconds without Docker or EF Core.
- The compiler prevents accidental coupling (e.g. EF entities leaking into domain logic); a unit test also asserts the dependency direction.
- Infrastructure implements interfaces defined in Application (Dependency Inversion), so adapters are swappable.
- `Api` is the only composition root, keeping DI registration in one visible place.
- Cost: more projects and mapping code than a single-file API. Accepted because the enforced boundaries pay off as features accumulate.

---

## ADR-004: JWT access tokens with rotating refresh tokens

**Context**
The API is consumed by first-party clients that need stateless request authentication but must also support long-lived sessions and the ability to revoke a compromised session.

**Decision**
Issue short-lived (15 min) JWT access tokens and long-lived (7-day) opaque refresh tokens. Refresh tokens are 256-bit random values; only their SHA-256 hash is persisted. Every refresh rotates the token, and a refresh token belongs to a family shared by one login session.

**Consequences**
- Request authentication stays stateless and cheap (no DB hit for the access token).
- Only hashes are stored, so a database leak does not expose usable refresh tokens.
- Rotation plus family tracking gives reuse detection: presenting an already-rotated token revokes the whole family, containing token theft.
- Cost: clients must handle rotation, and access tokens cannot be revoked before expiry. The short lifetime bounds that window.

---

## ADR-005: Standalone PasswordHasher instead of full Identity

**Context**
Registration and login need correct password hashing and lockout, but not Identity's user/role stores, its EF schema, or its cookie flows — the API is token-based and the schema is owned by our own entities.

**Decision**
Use ASP.NET Core Identity's `PasswordHasher<T>` on its own, behind an `IPasswordHasher` port in the Application layer. Implement lockout (5 attempts / 15 minutes) directly on the `User` entity.

**Consequences**
- Battle-tested PBKDF2 hashing without adopting Identity's schema or its opinionated lifetime.
- The Application layer stays free of Identity types; the adapter lives in Infrastructure and can be swapped.
- Cost: lockout and user management are our responsibility rather than Identity's built-in behavior.

---

## ADR-006: Money as integer minor units

**Context**
Financial arithmetic must be exact; binary floating point cannot represent decimal amounts such as 0.10 and borrowing across currencies must be impossible.

**Decision**
Represent money as a `Money(long MinorUnits, Currency Currency)` value object. All arithmetic uses `checked` operations and rejects mixing currencies at runtime with `InvalidOperationException`.

**Consequences**
- No floating-point rounding drift; amounts are exact integers of the smallest unit (e.g. paisa).
- Currency is carried with the amount, so cross-currency arithmetic fails loudly instead of silently.
- Cost: presentation layers convert to and from major units, and `long` imposes a practical (but ample) upper bound.

## ADR-007: Every wallet gets a ledger account in the registration transaction

**Context**
Customer wallets are liability accounts in the double-entry ledger (ADR-008). If a wallet could exist without its ledger account, the ledger and the wallet table could disagree and money could be posted nowhere. Wallets created before the ledger existed also need a backfill.

**Decision**
`RegisterUserHandler` creates the user, wallet and the wallet's `LedgerAccount` inside one `IUnitOfWork.ExecuteInTransactionAsync` block; any failure rolls all three back. A data migration (`BackfillWalletLedgerAccounts`) gives every pre-existing wallet a liability account, guarded by `NOT EXISTS` so it is safe to re-run. System accounts are seeded idempotently at startup by `SystemAccountSeeder` (find-or-create per code and currency), backed by the unique filtered index on `(SystemCode, Currency)`.

**Consequences**
- No wallet can be persisted without a matching liability ledger account.
- Backfill and seeding are idempotent, so they are safe against re-deploys and multiple instances.
- Startup seeding is gated by `Seed:RunOnStartup` so tests that manage migrations themselves stay in control.
- Cost: registration now performs three saves in one transaction instead of two; the extra round trip buys an invariant.

## ADR-008: Double-entry ledger with derived, never-stored balances

**Context**
A wallet needs a balance, but a stored balance is a second source of truth. If a bug ever writes a posting without updating the balance (or vice versa), the two disagree permanently and there is no way to tell which is right. Auditing also requires reconstructing the *history* of movements, not just a final number.

**Decision**
Model all money movement as double-entry journal entries. A `JournalEntry` is an aggregate root composed of `LedgerEntry` postings; it can only exist through `Debit`/`Credit` and must `Build()` (≥2 postings, per-currency debit total == credit total). Accounts carry a type (`Asset`, `Liability`, `Revenue`) with a credit-normal or debit-normal convention (`AccountTypeExtensions.IsCreditNormal`). Balances are **derived** by summing postings, conventionally signed via `LedgerEntry.SignedAmountForCreditNormal/…DebitNormal`.

**Consequences**
- Postings are the single source of truth; a balance is always recomputable and can never drift.
- The statement's running balance is a single SQL window function over the indexed `(LedgerAccountId, CreatedAtUtc)` path.
- New use cases (transfers, bill payments) plug in as new journal-entry types without changing balance logic.
- Cost: reads sum postings; snapshots will be added later as a pure cache that can always be discarded.

## ADR-009: An append-only ledger corrected by reversals

**Context**
Financial history must be tamper-evident. If a ledger row could be edited or deleted, a bug — or a malicious actor — could rewrite history and no audit could detect it. Yet mistakes and fraud happen and must be corrected.

**Decision**
Ledger rows are immutable. The domain exposes no mutators, and a PostgreSQL `BEFORE UPDATE OR DELETE` trigger (`prevent_ledger_mutation`) rejects any direct modification of `JournalEntries` or `LedgerEntries`. Corrections are made by posting a **reversal**: a new journal entry that mirrors the original (every posting keeps its amount, swaps direction) and links back via `ReversesJournalEntryId`. A unique filtered index makes an entry reversible at most once.

**Consequences**
- History can never be rewritten; both the original and its correction remain visible for audit.
- Immutability is enforced at the database, so a stray script or a future service cannot bypass it.
- The equal-and-opposite reversal is guaranteed to balance because the original did.
- Cost: the ledger grows with corrections rather than shrinking; queries filter rather than delete.

## ADR-010: Posting amounts are always positive; direction carries the sign

**Context**
If both positive and negative amounts were allowed, "do debits equal credits?" would depend on sign conventions applied inconsistently across code. A posting like "debit −100" is ambiguous and invites subtle bugs.

**Decision**
A `Money` amount on a posting is a strictly positive magnitude (`LedgerEntry.Create` rejects `≤ 0`); the `Direction` enum (`Debit`/`Credit`) carries the sign, and the signed effect on a balance is computed from the account's normal side. This is enforced in the domain and by the database CHECK `AmountMinorUnits > 0`.

**Consequences**
- "Debits == credits" is a plain, unambiguous comparison of positive sums.
- The sign of a balance is derived from the account type, not baked into stored amounts.
- Property-based tests (FsCheck) assert these rules across hundreds of generated inputs.
- Cost: every read that needs a signed value must know the account type (`AccountTypeExtensions`).

## ADR-011: Funding is modelled as a debit to the SYSTEM_FUNDING asset

**Context**
This project simulates deposits. In double entry, money cannot appear from nothing — every deposit needs a counterparty account to debit. We also need a clear, seeded source account so funding flows are auditable end to end.

**Decision**
Simulated deposits debit `SYSTEM_FUNDING` (an `Asset`, debit-normal) and credit the customer wallet (a `Liability`, credit-normal). System accounts (`Funding`, `Fees`, `BillerSettlement`) are seeded once per currency, each with a type fixed by its code. `POST /api/admin/wallets/{id}/fund` performs this posting; `GET /api/admin/ledger/trial-balance` proves debits still equal credits per currency.

**Consequences**
- Every rupee in a wallet has a visible, balanced counterpart in the platform's own accounts.
- The trial balance is a one-call global integrity check.
- `SYSTEM_FEES` and `SYSTEM_BILLER_SETTLEMENT` are seeded but intentionally unused until biller payments (Weeks 4–6).
- Cost: the funding asset grows with simulated deposits; it is not backed by real money and is clearly a test/demo construct.
