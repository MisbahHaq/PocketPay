# Architecture

## Shape

Clean Architecture, four projects, dependencies pointing inwards only:

```
Inventory.Domain          no dependencies
        ^
Inventory.Application    -> Domain
        ^
Inventory.Infrastructure -> Application, Domain
        ^
Inventory.Api            -> Infrastructure, Application, Domain
```

`Domain` has no project references at all. If a future change needs one, the layer
boundary is wrong and should be fixed rather than relaxed.

## Layers

**Domain** - entities, value objects, domain events, and the exceptions the business rules
raise. Knows nothing about HTTP, EF Core, or PostgreSQL.

**Application** - use cases as MediatR handlers, their requests and responses, validators,
and the abstractions the outer layers implement. Owns transaction boundaries.

The one deliberate exception: `Application` references EF Core's `IQueryable` and
expression types, so use cases can compose filtering, sorting and pagination in the
database. This is a real trade. The alternative, a specification pattern with a repository
per query, produces far more code for composite filters and gives up SQL composition that
list endpoints genuinely need. The constraint that keeps this honest is that `Application`
never references `DbContext`, `SaveChanges`, or any Npgsql type - only `IQueryable`.

**Infrastructure** - EF Core, Npgsql, ASP.NET Identity, JWT issuance, the audit
interceptor, and the seeder. Everything that knows the vendor.

**Api** - controllers, middleware, configuration, and the HTTP pipeline. Controllers are
thin: parse, delegate, return.

## Request pipeline

Every request passes through, in order:

```
Serilog request logging
  -> CORS
    -> Security headers
      -> Rate limiting
        -> Authentication (JWT)
          -> Authorization (deny by default)
            -> Endpoint
              -> MediatR
                -> Validation behaviour      (422 on failure)
                  -> Logging behaviour
                    -> Handler
                      -> UnitOfWork: SaveChanges + dispatch domain events
        <- Exception middleware             (RFC 7807 ProblemDetails)
```

Authorization defaults to deny. A controller with no `[Authorize]` attribute is
unreachable, so forgetting the attribute fails closed.

## Identity

- `ApplicationUser : IdentityUser<Guid>` and `IdentityRole<Guid>`. Identity fully supports
  a custom key type, and a Guid user key means audit columns like `CreatedBy`,
  `ApprovedBy` and `PerformedBy` are the same type as every other foreign key.
- Identity tables are renamed to snake_case (`users`, `roles`, `user_roles`) so they match
  the convention used by the application tables.
- Roles: `Admin`, `Manager`, `Staff`. The split follows the physical trust boundary in a
  warehouse. `Staff` records movement; `Manager` approves it; `Admin` owns configuration
  and user accounts. No account can both record and approve, which is what makes the audit
  trail meaningful.
- Accounts are never deleted. Deleting a user would orphan every stock movement they ever
  touched, which is exactly the history the audit trail exists to preserve. Deactivate
  instead.

## Tokens

- **Access token**: JWT, HS256, short-lived (15 minutes default). Carries `sub`, `jti`,
  name, email, and one claim per role.
- **Refresh token**: opaque 256-bit random value, base64url, 7 days.

Only the SHA-256 hash of a refresh token is stored, so a database disclosure does not hand
over usable credentials. Every refresh rotates the token and revokes its predecessor.

Each refresh token belongs to a **family**: the chain of rotations descending from one
login. Presenting an already-rotated token means either a replay or a stolen token, and
both are treated as compromise - the entire family is revoked. Revoking only the replayed
token would mean an attacker who rotates once is rewarded with a fresh, valid token at the
moment of detection.

## Concurrency

`IHasRowVersion.RowVersion` maps to PostgreSQL's `xmin`, the transaction id of the row's
last update. Assigning it requires no trigger, no extra column, and no write-side code.

Two operators acting on the same stock row both load the same `xmin`; the second save
raises `DbUpdateConcurrencyException` rather than silently overwriting the first. This
matters more for inventory than for most domains: a lost update means a stock figure is
wrong, and nobody notices until a physical count.

Caveat: `xmin` wraps at 2^32 and its values can be frozen by aggressive `autovacuum
FREEZE`. Both are documented PostgreSQL behaviours, not defects. Code must treat the token
as opaque and never compare token values for ordering.

## Identifiers

Ids are UUIDv7 (`SequentialGuid`), not v4. Random v4 keys scatter inserts across the index
and make the primary key a write hotspot on the high-churn tables. v7 embeds a timestamp in
the high bits, so keys are both globally unique and monotonically increasing, and index
inserts stay roughly sequential.

The generator is thread-safe, advances its logical clock when more than 4096 ids are
requested within one millisecond, and writes the timestamp big-endian so the value stays
sorted in PostgreSQL as well as in memory.

## Errors

RFC 7807 `ProblemDetails` from a single middleware, so every failure looks the same to a
client:

| Status | Meaning |
| --- | --- |
| 400 | malformed request |
| 401 | unauthenticated, or bad credentials |
| 403 | authenticated but not permitted |
| 404 | not found |
| 409 | conflict, e.g. duplicate account or a failed concurrency check |
| 422 | validation failed, with per-field details |
| 429 | rate limited |

Validation errors use `ValidationProblemDetails`, with `errors` keyed by field name so a
form can highlight the right input.

Login failures are deliberately identical for "wrong password" and "no such account".
Differentiating them turns the endpoint into a user enumeration oracle.

## Transactions

`IUnitOfWork.ExecuteInTransactionAsync` wraps a use case in a database transaction with
bounded retry. Retry re-evaluates the business rules on each attempt rather than blindly
replaying, because a failed attempt may have partially applied a rule.

Npgsql's `EnableRetryOnFailure` is deliberately **not** enabled on the DbContext. Its
execution strategy rejects user-initiated transactions unless every one is wrapped in
`strategy.ExecuteAsync()`, which forbids the manual transactions that stock operations
need. Retry lives in `UnitOfWork` instead, where it belongs.

## Rate limiting

Three limiters, each covering a gap the others leave:

- **Global**, 120/minute per client, token bucket, to absorb a flood of any kind.
- **Login**, 10/minute, a fixed window tighter than the global one, to slow credential
  stuffing.
- **Refresh**, 60/minute, because token refresh is cheap for a legitimate client but
  useful to an attacker replaying stolen tokens.

Partition keys prefer the authenticated user id and fall back to the client address, so
the limit applies per account rather than per office.

## Observability

Serilog with request logging, correlation ids, and environment-appropriate sinks. The log
records who did what and to which row; business information belongs in the database audit
tables, not in log files, because logs expire and audits must not.

## Testing

See [testing.md](testing.md). Unit tests cover pure logic; integration tests boot the real
API against a real PostgreSQL database, because the Identity schema, the `xmin` mapping,
and transaction behaviour cannot be verified against an in-memory substitute.