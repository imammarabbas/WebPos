# Local setup: one host, one database

Windows Terminal has **no database**. Master and the REST API share one Postgres through one WebPos host. Use the same host URL for both.

## Recommended (Docker)

```powershell
# From repo root
docker compose up -d --build api
```

| App | URL |
|-----|-----|
| API health | http://localhost:8080/health |
| **Master admin** | http://localhost:8080/master |
| Login | http://localhost:8080/login |
| Terminal SDK | `WebPosSdk:BaseAddress` = `http://localhost:8080/` |

**Do not** open Master on port 6168 while the Terminal talks to Docker on 8080 — that was the usual “two databases” mistake.

### Admin login (pilot)

| User | Password |
|------|----------|
| ammar | ammar123 |
| admin | admin123 |

Terminal uses PINs (not web passwords).

## Alternative: `dotnet run` on the same port

```powershell
dotnet run --project WebPos\WebPos.csproj --urls http://localhost:8080
```

Launch profile now defaults to **http://localhost:8080** and opens `/master`.

Postgres must be reachable at `localhost:5432` with database **`WebPos`**.

Connection password: copy from `.env` `POSTGRES_PASSWORD` into gitignored  
[`WebPos/appsettings.Development.Local.json`](../WebPos/appsettings.Development.Local.json)  
(created by `scripts/Generate-PilotEnv.ps1`, or write manually).

`Start-PilotApi.ps1` also uses `Database=WebPos` + `.env` `POSTGRES_PASSWORD` (same as Docker).

## If Master and Terminal show different products/sales

1. Master URL is not 8080 → stop the 6168 process; use http://localhost:8080/master  
2. `GET http://localhost:8080/health` fails → start Docker API or local host on 8080  
3. Password mismatch → regenerate with `pwsh -File scripts/Generate-PilotEnv.ps1` or sync `Development.Local.json` to `.env`  
4. After Core changes: `docker compose up -d --build api`

## Blank Master page after login

If login succeeds but `/master` stays blank, the browser likely failed to load `_framework/blazor.web.js` (check DevTools Network). The Docker image must be rebuilt after `RequiresAspNetWebAssets` is set on the host project. Clear site cookies for `localhost:8080` once after rebuild if antiforgery errors remain.
