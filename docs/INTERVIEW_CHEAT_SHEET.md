# 7-Question Interview Cheat Sheet

> Quick reference for explaining the PayPocket codebase in an interview. Each answer is 3–5 sentences; expand if asked.

---

### 1. Architecture & Layering
**Q: How is the solution structured and why?**

**A:** Four projects with strict inward dependencies: `Api → Application, Infrastructure`; `Infrastructure → Application`; `Application → Domain`; `Domain → nothing`. The compiler enforces this via project references. `Api` is the only composition root. `Domain` has zero external dependencies — pure business logic. `Application` defines use cases and interfaces (ports). `Infrastructure` implements those interfaces (adapters: EF Core, JWT, etc.). This makes `Domain`/`Application` unit-testable in milliseconds without a database or EF Core runtime.

---

### 2. Money Representation
**Q: How is money represented and why not `decimal`/`double`?**

**A:** `Money` is a `record(long MinorUnits, Currency Currency)` — an integer of the smallest unit (e.g. paisa for PKR). All arithmetic uses `checked` operations and throws `InvalidOperationException` if currencies differ. This eliminates floating-point rounding errors (0.1 + 0.2 ≠ 0.3 in binary), makes cross-currency bugs impossible at runtime, and keeps a practical upper bound (1e15 minor units ≈ 10 trillion major units).

---

### 3. The Double-Entry Ledger
**Q: How does the ledger work? Where is the balance stored?**

**A:** No balance column exists anywhere. Every money movement is a `JournalEntry` (aggregate root) with ≥2 `LedgerEntry` postings. The entry must balance per currency (debits == credits). Posting amounts are always positive; the `Direction` (Debit/Credit) carries the sign. Accounts have a type (`Asset` = debit-normal, `Liability`/`Revenue` = credit-normal) that determines whether a credit increases or decreases the derived balance. Balances are computed on demand by summing postings (SQL window function for running balance). Immutability is enforced by a PostgreSQL `BEFORE UPDATE OR DELETE` trigger; corrections are made by posting a reversal entry.

---

### 4. Transfer State Machine
**Q: Walk through the transfer lifecycle and how invalid transitions are prevented.**

**A:** `Transfer` is an aggregate with private constructor and three transition methods: `Complete(journalEntryId)`, `Fail(reason)`, `MarkReversed()`. Legal transitions: `Pending → Completed` (journal entry posted), `Pending → Failed` (business rule rejected), `Completed → Reversed` (admin reversal). All other moves (e.g. `Failed → anything`, `Reversed → anything`) throw `TransferException`. No public setters; the state machine is entirely encapsulated. Unit tests cover all 10 legal/illegal transitions. A `Failed` transfer never posts a journal entry but is still persisted for audit.

---

### 5. Transfer Posting & Fees
**Q: What does the journal entry for a 1,000 transfer with 25 fee look like?**

**A:** Single journal entry (type `Transfer`):
- Debit source wallet (liability) 1,025 (amount + fee)
- Credit destination wallet (liability) 1,000
- Credit `SYSTEM_FEES` (revenue) 25
Debits (1,025) = Credits (1,025). If fee = 0, only two postings. The fee goes to a `Revenue` account so it appears in the trial balance as platform income.

---

### 6. Concurrency & Locking
**Q: Is the transfer implementation safe under concurrent load? How?**

**A:** **Yes, for the same wallet.** All mutable state is checked inside one serializable transaction while holding ordered `FOR UPDATE` row locks on both source and destination ledger accounts. Ordered lock acquisition (by account ID) prevents deadlocks between opposing transfers. If Postgres aborts a transaction with a serialization failure (`40001`) or deadlock (`40P01`), the whole unit of work is rolled back and the transfer is retried, so no partial posting can survive.

---

### 7. Authentication & Authorization
**Q: How does auth work and why 404 instead of 403 for other users' resources?**

**A:** Short-lived (15 min) JWT access tokens with `sub`, `email`, `role`, `jti` claims. Opaque refresh tokens (256-bit random); only SHA-256 hash stored. Rotation on every use; family tracking detects reuse and revokes the whole family. Passwords hashed via ASP.NET Core Identity's `PasswordHasher` standalone. All customer endpoints return 404 (not 403) when the requesting user doesn't own the resource — this prevents account enumeration attacks. Admin endpoints use `AdminOnly` policy; customer endpoints use `CustomerOnly` policy.

---

## Quick File Map (for "Where is X implemented?")

| Concern | Key Files |
| --- | --- |
| Domain entities (Transfer, JournalEntry, LedgerAccount, Money, User, Wallet) | `src/WalletPlatform.Domain/Entities/*.cs` |
| Domain enums (TransferStatus, Currency, Direction, AccountType) | `src/WalletPlatform.Domain/Enums/*.cs` |
| Domain exceptions (LedgerException, TransferException) | `src/WalletPlatform.Domain/Exceptions/*.cs` |
| Application interfaces (ITransferService, ILedgerService, IUnitOfWork, repositories) | `src/WalletPlatform.Application/Abstractions/` |
| Use cases / handlers (CreateTransferHandler, GetTransfersHandler, ReverseTransferHandler, etc.) | `src/WalletPlatform.Application/Transfers/`, `Admin/` |
| DTOs / Requests / Responses | `src/WalletPlatform.Application/Transfers/TransferRequests.cs`, `TransferResponses.cs` |
| Policy classes (DestinationWalletPolicy, AmountLimitsPolicy, etc.) | `src/WalletPlatform.Application/Transfers/Policies/` |
| FluentValidation validators | `src/WalletPlatform.Application/Transfers/TransferRequests.cs` |
| EF Core configurations (JournalEntry, LedgerAccount, LedgerEntry, Transfer) | `src/WalletPlatform.Infrastructure/Data/Configurations/*.cs` |
| Migrations (InitialCreate, AddAccounts, AddLedger, BackfillWalletLedgerAccounts, AddTransfers) | `src/WalletPlatform.Infrastructure/Migrations/*.cs` |
| Infrastructure implementations (TransferService, LedgerService, repositories, UnitOfWork) | `src/WalletPlatform.Infrastructure/Persistence/*.cs`, `Services/TransferService.cs` |
| Controllers (WalletsController, AdminController) | `src/WalletPlatform.Api/Controllers/*.cs` |
| Auth middleware, policies, ProblemDetails mapping | `src/WalletPlatform.Api/Authorization/`, `Middleware/`, `Validation/` |
| Unit tests (domain + policy + property-based) | `tests/WalletPlatform.UnitTests/` |

---

## Key Commands

```bash
# Build & test (CI-equivalent)
dotnet build paypocket.slnx --configuration Release --nologo
dotnet test paypocket.slnx --configuration Release --no-build --nologo

# Migrations
dotnet ef migrations add <Name> --project src/WalletPlatform.Infrastructure --startup-project src/WalletPlatform.Api
dotnet ef database update --project src/WalletPlatform.Infrastructure --startup-project src/WalletPlatform.Api

# Run API
dotnet run --project src/WalletPlatform.Api
```

---

## Known Gaps (explicitly documented)

| Gap | Where | Notes |
| --- | --- | --- |
| Balance snapshots | Sum-on-read; no materialized views | Acceptable at current scale |
| Database-backed integration tests | No integration-test project in this repo | Verification is unit/property only |

---

## ADR Index (docs/DECISIONS.md)

| ADR | Topic |
| --- | --- |
| 001 | Modular monolith |
| 002 | PostgreSQL |
| 003 | Clean architecture layering |
| 004 | JWT + rotating refresh tokens |
| 005 | Standalone PasswordHasher |
| 006 | Money as integer minor units |
| 007 | Wallet + ledger account in registration txn |
| 008 | Double-entry ledger, derived balances |
| 009 | Append-only ledger, reversed by reversals |
| 010 | Posting amounts always positive; direction carries sign |
| 011 | Funding = debit SYSTEM_FUNDING |
| 012 | Transfer state machine |
| 013 | Fee → SYSTEM_FEES revenue |
| 014 | Failed transfers persisted for audit |
| 015 | Wallet-number addressing + masked lookup |
| 016 | 404 over 403 for other users' resources |
| 017 | Resolved concurrency gap (TOCTOU) |