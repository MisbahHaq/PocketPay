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
