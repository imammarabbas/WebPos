# WebPos

Point-of-sale and master admin for a single shop. Deploy with **Docker Compose** (API + Postgres). EF migrations run automatically when the API starts. Shop data lives in Docker volume `webpos_pgdata` and survives updates.

More detail: [docs/LOCAL_SETUP.md](docs/LOCAL_SETUP.md)

---

## Requirements

- Windows 10/11 with [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running
- Git (for clone / update)

---

## SHOP INSTALLATION

### Option A — double-click (recommended)

1. Copy the project folder onto the shop PC (or `git clone`).
2. Double-click **`INSTALL-POS.bat`**.
3. When Notepad opens `.env`, replace every `CHANGE_ME` value and save:
   - `POSTGRES_PASSWORD`
   - `SECURITY_JWT_KEY` (at least 32 characters)
   - `PILOT_OWNER_PASSWORD`, `PILOT_ADMIN_PASSWORD`, and all PIN fields
4. Wait until the script reports the API is healthy.

### Option B — PowerShell

```powershell
git clone https://github.com/imammarabbas/WebPos.git
cd WebPos
copy .env.example .env
notepad .env
docker compose up -d --build
```

### After install — open these URLs

| App | URL |
|-----|-----|
| API health | http://localhost:8080/health |
| Master admin | http://localhost:8080/master |
| Login | http://localhost:8080/login |

**Never commit `.env`.** Only `.env.example` belongs in Git.

---

## SHOP UPDATE

Keeps the existing shop database (volume `webpos_pgdata`).

### Option A — double-click

1. Double-click **`UPDATE-POS.bat`**.
2. Wait until health check passes.

### Option B — PowerShell

```powershell
cd WebPos
git pull
docker compose up -d --build
```

- Rebuild applies new code.
- Postgres data stays in `webpos_pgdata`.
- Schema updates apply automatically on API start (EF migrations).

---

## Stop / start (without wiping data)

```powershell
cd WebPos
docker compose stop
docker compose start
```

Or:

```powershell
docker compose down
docker compose up -d --build
```

(`down` without `-v` keeps the database volume.)

---

## Danger: wipe shop database

```powershell
docker compose down -v
```

The `-v` flag deletes `webpos_pgdata` and **destroys all sales, stock, and cash history**. Never use this on a live shop unless you have a verified backup and intend to wipe.

---

## What not to put in Git

Never commit or push:

- `.env`
- `WebPos/appsettings.Pilot.json`
- `WebPos/appsettings.Development.Local.json`
- `WebPos/dev-secrets/` (enrollment keys)
- Database dumps (`backup.sql`, `*.sql`, backups)
- Certificates / private keys (`*.pem`, `*.pfx`)
- IDE folders (`.vs/`)
