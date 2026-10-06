# Frontend

React 18, TypeScript, Vite, Tailwind CSS 4, shadcn/ui, React Router 7, TanStack Query,
Axios, React Hook Form, Zod.

## Commands

```powershell
npm install
npm run dev        # http://localhost:5173, proxies /api to localhost:5126
npm run build      # type-check then production build
npm run preview    # serve dist/
npm run typecheck
npm run lint
```

## How a request flows

```
component
  -> TanStack Query (caching, retry, dedupe)
    -> axios instance (src/lib/api.ts)
      -> request interceptor: attach access token, refreshing first if expiring
        -> Vite proxy (development) -> .NET API
      <- 401? response interceptor: refresh once, replay the original request
    <- otherwise: hand back data, or a typed error
```

Two decisions in there are load-bearing.

**Refreshes are deduplicated.** The API rotates refresh tokens, so four simultaneous
401s firing four refresh calls would make three of them replays. The server treats a replay
as a stolen token and revokes the whole family, signing the user out. `refreshInFlight`
collapses concurrent attempts into one.

**A request is replayed at most once.** Without the `_retried` flag, a permanently invalid
token would loop: 401, refresh, 401, refresh, until the tab hung or the rate limiter
intervened.

## Session storage

Tokens live in `localStorage` under `inventory.*` keys. Only the tokens are persisted;
the user object is refetched from `/auth/me` on load, so role changes take effect on
reload rather than from a stale cached copy.

`localStorage` is readable by any script on the origin, so it is only acceptable because
the refresh token is single-use, rotation-protected, and family-revocable. Moving to
httpOnly cookies would be better; that is a deliberate later change, since it requires
cookie-based CORS and CSRF handling on the API.

## Auth flow

- `AuthProvider` (`src/context/AuthContext.tsx`) owns `user`, `signIn`, `signOut`.
- `RequireAuth` waits for the session check before redirecting, so a page reload does not
  bounce a signed-in user to the login screen.
- `RedirectIfAuthenticated` keeps a signed-in user off `/login`.
- `localStorage.from` remembers the intended route so sign-in returns there.

## Errors

`src/lib/api.ts` converts everything into either:

- `ValidationError` — carries `errors` keyed by field name, so forms show the message next
  to the offending input.
- `ApiError` — a message and status, including the `traceId` for matching a log entry.
- `SessionExpiredError` — raised when the refresh could not be recovered.

## Nav gating

Sidebar entries declare the roles allowed to see them, but that is presentation only. Each
route is protected server-side by an authorization policy, so hiding a link is a
convenience, never a security control.

## Adding a page

1. Create `src/pages/YourPage.tsx`.
2. Add the route inside the guarded layout in `src/routes/AppRoutes.tsx`.
3. Add a sidebar entry in `src/components/AppLayout.tsx` with its roles.

## Adding a shadcn/ui component

The primitives in `src/components/ui/` were written by hand rather than pulled by the CLI,
so there is no dependency on a generator at build time. To add one, copy the upstream
component into that folder and keep the `cn()` merge helper, so class overrides keep
working.