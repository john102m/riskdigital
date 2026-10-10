# Proposal: Password-Protect the `/admin` Endpoints

## Problem

Every endpoint under `/admin` (defined in `EndPointConfig/ManagementEndpoints.cs`) is served on the public internet with **no authentication**. Anyone who knows — or guesses — the URL can hit them. The app log at `https://risk.spooch.co.uk/admin/app-log` is the obvious one, but the group is more dangerous than just log-reading:

| Endpoint | Risk if anonymous |
|----------|-------------------|
| `GET /admin/reset/{code}` | Wipes a specific live game |
| `GET /admin/reset` | Wipes **all** games |
| `GET /admin/gameover` | Force-ends a game |
| `GET /admin/train` | Kicks off CPU-heavy ML retraining on demand |
| `GET /admin/logs-download` | Downloads all gameplay CSV logs |
| `POST /admin/logs-upload` | **Appends arbitrary rows into the training CSVs** that feed the ML models — model poisoning |
| `GET /admin/app-log`, `/missions`, `/ml-status`, `/logs-status`, `/testcombat`, `/testblitz`, `/testdice` | Information disclosure / state disruption |

Observed in the live log: a plain `GET /admin/reset/5486` returned `200` and reset a game. No credential was required.

This is a personal, single-admin game, so full user accounts / ASP.NET Identity would be overkill. A single shared secret is proportionate.

---

## Proposed Solution

A single shared-secret token, checked by one endpoint filter applied to the whole `/admin` group. No user accounts, no login page, no new NuGet dependencies.

### Why a group filter

All admin routes already live under one `app.MapGroup("/admin")`. Applying auth to the group means **one** attachment point rather than editing ~16 endpoints individually, and any admin endpoint added later is protected automatically.

### Token source

Read from configuration, **never hardcoded**:
- `appsettings.json` ships with an empty `AdminToken` (committed, blank).
- The real value is supplied in production via a non-committed `appsettings.Production.json` or a Plesk environment variable, so the secret never lands in git.

### Fail closed

If `AdminToken` is not configured, the filter denies all `/admin` requests (returns `503`) rather than silently allowing them. Missing config must never mean "wide open".

---

## 1. New file: `EndPointConfig/AdminAuthFilter.cs`

One class per file, per project convention.

```csharp
using System.Security.Cryptography;
using System.Text;

namespace Risk.Server.EndPointConfig;

public sealed class AdminAuthFilter : IEndpointFilter
{
    private readonly string? _token;

    public AdminAuthFilter(IConfiguration config) => _token = config["AdminToken"];

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        // Not configured → fail closed
        if (string.IsNullOrEmpty(_token))
            return Results.StatusCode(503);

        var req = ctx.HttpContext.Request;
        var provided = req.Headers["X-Admin-Token"].FirstOrDefault()
                       ?? req.Query["key"].FirstOrDefault();

        // Constant-time compare to avoid timing side-channel
        var ok = provided is not null && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(_token));

        return ok ? await next(ctx) : Results.Unauthorized();
    }
}
```

The token is accepted **either**:
- as an `X-Admin-Token` request header (for tooling / curl), or
- as a `?key=...` query parameter (so an admin URL can still be pasted straight into a browser, which is how `/admin/app-log` is used today).

## 2. One-line change in `ManagementEndpoints.cs`

```csharp
// before
var admin = app.MapGroup("/admin");

// after
var admin = app.MapGroup("/admin").AddEndpointFilter<AdminAuthFilter>();
```

Nothing else in that file changes.

## 3. Config key in `appsettings.json`

```json
"AdminToken": ""
```

Left blank in the repo. Real value set out-of-band in production (Plesk env var or `appsettings.Production.json`, which stays untracked).

---

## Usage After the Change

Browser (unchanged workflow, one extra param):
```
https://risk.spooch.co.uk/admin/app-log?key=YOUR_TOKEN
```

Tooling (preferred — keeps the secret out of URLs/history):
```
curl -H "X-Admin-Token: YOUR_TOKEN" https://risk.spooch.co.uk/admin/app-log
```

---

## Scope / What This Does NOT Touch

- `/gamehub` (SignalR) stays open — players need it.
- Public pages `/board` and `/guide` stay open.
- No change to game logic, ML, or client code.

---

## Honest Limitations

- A `?key=` in the URL appears in browser history and the web server's access logs. The header route avoids this; the query route is a deliberate convenience trade-off. Over HTTPS the token is not exposed on the wire either way.
- This is a shared secret, not real authentication/authorisation — appropriate for a personal single-admin surface, **not** a pattern to reuse at work.
- Protects access, not rate-limiting; a holder of the token can still hammer `/admin/train`.

---

## Change Summary

| File | Change |
|------|--------|
| `EndPointConfig/AdminAuthFilter.cs` | **New** — the filter |
| `EndPointConfig/ManagementEndpoints.cs` | One line — `.AddEndpointFilter<AdminAuthFilter>()` |
| `appsettings.json` | Add blank `AdminToken` key |

No new dependencies. No client changes.

---

## Open Decision

Keep the `?key=` query-param option for browser convenience, or **header-only** (more secure, but admin URLs can no longer just be clicked)?
