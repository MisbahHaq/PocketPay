# Testing

Two suites, with different requirements:

| Suite | Needs a database | Command |
| --- | --- | --- |
| `Inventory.UnitTests` | no | `dotnet test backend/tests/Inventory.UnitTests` |
| `Inventory.IntegrationTests` | yes, PostgreSQL | `dotnet test backend/tests/Inventory.IntegrationTests` |

Run everything:

```powershell
dotnet test backend/Inventory.sln
```

## Unit tests

Pure logic, no I/O: UUID generation, token creation, refresh-token lifecycle, pagination,
and request validation. Runs in about a second and is the suite to run on every save.

```powershell
dotnet test backend/tests/Inventory.UnitTests --nologo
```

## Integration tests

Boots the real API in-process against a real PostgreSQL database. This is deliberate:
the things most likely to be wrong in an auth system - the Identity schema, the `xmin`
concurrency mapping, transaction boundaries, ProblemDetails status codes, role checks -
cannot be verified against an in-memory stand-in, because the stand-in behaves
differently in exactly the ways that matter.

### One-time setup: create the test role

The tests connect as a dedicated least-privilege role. It is deliberately created with
`NOCREATEDB` and `NOSUPERUSER`, so a fixture that quietly needed superuser rights would
fail here rather than pass locally and fail in CI, where those rights do not exist.

Run this once, as the PostgreSQL superuser, on the machine running the tests:

```powershell
psql -U postgres -h localhost -d postgres -v ON_ERROR_STOP=1 -f scripts/create-test-role.sql
```

The script is idempotent: it reuses an existing role and database and only resets the
password. It creates the role **without** `SUPERUSER`, `CREATEDB`, `CREATEROLE` or
`BYPASSRLS`, because integration tests must never need privileges a CI runner would not
have.

Verify it, and confirm the privileges really are limited:

```sql
SELECT rolname, rolsuper, rolcreatedb, rolcreaterole, rolbypassrls
FROM pg_roles WHERE rolname = 'inventory_test';
```

All four boolean columns must read `f`.

### Pointing the tests at your server

`scripts/create-test-role.sql` creates the role locally, so the tests need no configuration
at all: the fixture falls back to those same defaults.

To target a different server or database, set `INVENTORY_TEST_CONNECTION_STRING`:

```powershell
$env:INVENTORY_TEST_CONNECTION_STRING = "Host=localhost;Port=5432;Database=inventory_test;Username=inventory_test;Password=inventory_test_local"
```

Use an environment variable rather than committing a password. CI sets the same variable
from its own secret store, which is why no real credential appears in this repository.

### What the fixture does

- Applies every pending EF migration **once** for the whole assembly.
- Truncates all application tables between tests (`RESTART IDENTITY CASCADE`).
- Disables test parallelisation.

Parallel execution is off because every test arrives at the same database and truncation
is destructive: running in parallel would let one test delete another's rows mid-assertion
and produce failures that depend on scheduling. Correctness is worth more here than a
faster wall clock on a suite this small.

There is no per-test database. Creating one would need `CREATEDB`, which is exactly the
privilege the test role is denied.

## What is asserted, and why

A test earns its place by failing for a reason someone would care about. The
integration suite concentrates on:

- **Authentication** - login returns a token pair; a wrong password and an unknown
  account produce byte-identical `401` responses, so the endpoint cannot be used to
  enumerate accounts; forged, garbage, and wrong-key tokens are all rejected; five failed
  attempts lock the account.
- **Authorization** - Staff cannot create users, Manager cannot create users, only Admin
  can. This is the assertion that stops privilege escalation.
- **Token rotation** - each refresh issues a new token, revokes the old one, and keeps
  the family id; replaying a rotated token revokes the whole family.
- **Concurrency** - two contexts loading the same row produce a
  `DbUpdateConcurrencyException` on the second save, proving `xmin` is actually mapped.
- **Schema** - tables are snake_case; user keys are `uuid`; refresh tokens are stored
  hashed and never in the clear.

## Regression guards

These tests exist because the corresponding defect was present at some point, and a
naive refactor would silently reintroduce it:

| Test | Guards against |
| --- | --- |
| `SequentialGuidTests.Uses_big_endian_byte_order...` | Writing the timestamp in little-endian, which still sorts correctly in memory but scrambles index locality in PostgreSQL |
| `RegisterUserRequestValidatorTests.Accepts_every_system_role` | Using `IsInEnum()` on a `string`, which silently rejected all three real roles |
| `RegisterUserRequestValidatorTests.An_empty_password_is_rejected_with_one_clear_message` | Placing `NotEmpty` inside a `.When(...)`-guarded chain, which silenced it so a blank password produced no error at all |
| `RefreshTokenFlowTests.Replaying_a_rotated_token_revokes_the_entire_family` | Revoking only the replayed token, which would hand an attacker a fresh token in exchange for being detected |
| `PersistenceTests.Created_at_is_not_overwritten_by_a_later_update` | An audit interceptor stamping `CreatedAt` on every save |

## Writing new tests

Prefer asserting observable behaviour through the HTTP surface. When a test needs
arrange-heavy state, add a helper to `InventoryApiFactory` rather than repeating setup,
so the setup change is made once.

Never assert on a log line, on an exception's exact message, or on the number of database
queries. Those are implementation details that should be free to change without a test
failing.