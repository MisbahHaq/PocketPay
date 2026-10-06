# Troubleshooting

Ordered by how often you are likely to meet them.

## `Could not prepare the database... See README.md step 1`

The `inventory_test` role and database do not exist yet, or the password does not match
what you stored.

```powershell
psql -U postgres -h localhost -d postgres -v ON_ERROR_STOP=1 -f scripts/create-test-role.sql
```

Then confirm the role actually matches your connection string:

```powershell
dotnet user-secrets list --project backend/src/Inventory.Api
```

`ConnectionStrings:DefaultConnection` must contain the same password the script set.
Delete `ConnectionStrings:DefaultConnection` from user secrets to fall back to the script's
default, which is what the fixture and the README both assume.

If PostgreSQL is not running:

```powershell
Get-Service postgresql*
Start-Service postgresql-x64-18
```

## `Password authentication failed for user "inventory_test"`

Same root cause as above. Note that PostgreSQL reports this for *any* missing role as well
as a wrong password, so it does not distinguish "no such user" from "wrong password".

## `Cannot reach the server` shown in the browser

The API is not running, or it is on a different port than the proxy expects.

The API listens on **http://localhost:5126**. The Vite proxy in `frontend/vite.config.ts`
must point at the same port. If you changed `launchSettings.json`, change the proxy too.

Check both processes are up:

```powershell
Test-NetConnection -Port 5126   # API
Test-NetConnection -Port 5173   # Vite
```

## Login returns 401 for credentials you know are right

- The account may not exist yet. No seed admin is created unless **both** `Seed:AdminEmail`
  and `Seed:AdminPassword` are set - and the keys are `Seed:...`, not
  `Database:Admin:...`.
- Five failed attempts in a row lock the account for 15 minutes. Check
  `users.access_failed_count` and `users.lockout_end`.
- The signing key changed after the token was issued. Restart the frontend so the stale
  token is discarded.

## `Cors:AllowedOrigins is empty` at startup

The API refuses to start rather than fail as an obscure browser error later. The default
`appsettings.json` already lists `http://localhost:5173`. If you overrode it, set it back:

```powershell
dotnet user-secrets set "Cors:AllowedOrigins:0" "http://localhost:5173" --project backend/src/Inventory.Api
```

Behind the Vite proxy this is not strictly needed, since the browser only ever talks to
localhost:5173 - but the API validates the setting regardless, so it must be present.

## `The signing key must be at least 32 characters`

`Jwt:SigningKey` is validated on start, deliberately: a bad key should fail here, not as an
unexplained auth failure at the first login attempt.

```powershell
$r = [Security.Cryptography.RandomNumberGenerator]::Create(); $b = New-Object byte[] 48; $r.GetBytes($b); [Convert]::ToBase64String($b)
```

## Integration tests: `Could not connect using INVENTORY_TEST_CONNECTION_STRING`

Run the role script (above). The connection string the tests use defaults to the script's
values, so usually this just means the script has not been run.

```powershell
$env:INVENTORY_TEST_CONNECTION_STRING = "Host=localhost;Port=5432;Database=inventory_test;Username=inventory_test;Password=inventory_test_local"
dotnet test backend/tests/Inventory.IntegrationTests
```

## Integration tests fail with `429` rate-limit responses

They should not: the test factory replaces the limiter with an unlimited one. If you see
429s, `PostConfigure<RateLimiterOptions>` in `tests/Inventory.IntegrationTests/InventoryApiFactory.cs`
is no longer running, or a test is creating its own client outside the factory.

## `npm run dev` fails to resolve `@/...`

The path alias is declared in two places that must agree:
`frontend/vite.config.ts` and `frontend/tsconfig.app.json`. Both use `@/* -> ./src/*`.
Changing one without the other produces build errors only on some imports.

## Page loads but the sidebar is missing

Check the user object from `/api/v1/auth/me`. Sidebar entries declare roles, so a user
with no roles sees only the items marked unrestricted. The server still enforces access
regardless of what is shown.
