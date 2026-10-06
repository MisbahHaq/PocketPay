# API reference

Base URL in development: `http://localhost:5126` (or `https://localhost:7266` on the `https` launch profile). The port comes from `backend/src/Inventory.Api/Properties/launchSettings.json`.

Swagger UI is served at `/swagger` in the Development environment, with the OpenAPI
document at `/swagger/v1/swagger.json`.

All endpoints are versioned in the path: `/api/v1/...`.

## Endpoints

| Method | Path | Auth | Rate limit | Purpose |
| --- | --- | --- | --- | --- |
| POST | `/api/v1/auth/login` | anonymous | 10/min | Exchange credentials for tokens |
| POST | `/api/v1/auth/refresh` | anonymous | 60/min | Rotate the refresh token |
| POST | `/api/v1/auth/logout` | anonymous | 60/min | Revoke a refresh token family |
| GET | `/api/v1/auth/me` | bearer | global | Current user profile and roles |
| POST | `/api/v1/auth/users` | Admin | global | Create a user account |
| GET | `/health/live` | anonymous | global | Process is up |
| GET | `/health/ready` | anonymous | global | Process is up and the database responds |

Global rate limit is 120/minute per client.

## `POST /api/v1/auth/login`

```json
{ "email": "alex@example.com", "password": "Str0ng-Passw0rd!" }
```

`200 OK`

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "tokenType": "Bearer",
  "expiresInSeconds": 900,
  "expiresAt": "2026-03-01T12:15:00+00:00",
  "refreshToken": "kZ3vN8xQ...",
  "refreshTokenExpiresAt": "2026-03-08T12:00:00+00:00",
  "user": {
    "id": "6f1c...",
    "email": "alex@example.com",
    "fullName": "Alex Okonkwo",
    "roles": ["Manager"]
  }
}
```

`422 Unprocessable Entity` when the body fails validation, for example a malformed email.

`401 Unauthorized` for a wrong password, an unknown account, a locked account, or a
deactivated account. **These responses are byte-identical**, deliberately: a client that
could tell "no such user" from "wrong password" could enumerate accounts.

`429 Too Many Requests` after 10 attempts in a minute from one client key.

## `POST /api/v1/auth/refresh`

```json
{ "refreshToken": "kZ3vN8xQ..." }
```

`200 OK` with the same body shape as login, carrying a **new** refresh token. The
presented token is revoked and cannot be used again.

`401 Unauthorized` when the token is unknown, expired, or revoked. Presenting a token that
was already rotated is treated as a replay: the entire token family is revoked, so the
`200` response to the original rotation and any replacement tokens stop working.

## `POST /api/v1/auth/logout`

```json
{ "refreshToken": "kZ3vN8xQ..." }
```

`204 No Content`, always, whether or not the token existed. An endpoint that reveals
whether a token was valid is an enumeration surface, and a logout that can fail is an
annoyance to the user.

## `GET /api/v1/auth/me`

Requires `Authorization: Bearer <accessToken>`.

`200 OK`

```json
{
  "id": "6f1c...",
  "email": "alex@example.com",
  "fullName": "Alex Okonkwo",
  "roles": ["Manager"]
}
```

Roles are read from the database, not from the token, so a role change takes effect
immediately rather than when the access token expires.

## `POST /api/v1/auth/users`

Admin only. There is no self-registration: an open sign-up would let anyone who finds the
URL become Staff, and Staff can move real stock.

```json
{
  "email": "sam@example.com",
  "password": "Str0ng-Passw0rd!",
  "confirmPassword": "Str0ng-Passw0rd!",
  "fullName": "Sam Rivera",
  "role": "Staff"
}
```

`201 Created` with the new user's profile and role.

`role` is optional and defaults to `Staff`, the lowest privilege, so a request that
forgets it cannot accidentally create an administrator. Accepted values are `Admin`,
`Manager` and `Staff`, matched case-insensitively.

Password rules: at least 12 characters, with a digit, a lowercase letter, an uppercase
letter, and a symbol.

`409 Conflict` when the email is already registered.
`422 Unprocessable Entity` on validation failure.
`403 Forbidden` for any authenticated non-Admin.

## Errors

Every error is RFC 7807 `ProblemDetails`:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 422,
  "detail": "The request body failed validation.",
  "instance": "/api/v1/auth/users",
  "traceId": "0HN7...",
  "errors": {
    "email": ["Enter a valid email address."],
    "password": ["Password must be at least 12 characters."]
  }
}
```

`errors` is keyed by field name so a form can highlight the right input. `traceId` matches
the `traceId` response header and the log entry, so a user-reported failure can be found
without guessing.

| Status | When |
| --- | --- |
| 400 | malformed request |
| 401 | missing, invalid or expired token; bad credentials |
| 403 | authenticated but lacking the required role |
| 404 | resource not found |
| 409 | duplicate account, or a failed concurrency check |
| 422 | validation failed |
| 429 | rate limited; `Retry-After` is set |

## Authentication for subsequent requests

```
Authorization: Bearer <accessToken>
```

Access tokens last 15 minutes. Refresh proactively when `expiresAt` is near rather than
waiting for a `401`, and treat any `401` as "clear local state and send the user to the
login page" rather than retrying.

## CORS

The `spa` policy allows the origins in `Cors:AllowedOrigins`, with credentials, so the
frontend can send cookies or authorization headers from a different origin during
development. The wildcard `*` is rejected at startup: combined with credentials it is
rejected by browsers, and without credentials it is a needless hole.