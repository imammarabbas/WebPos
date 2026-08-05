# Terminal Enrollment Procedure

One-time setup per POS terminal before daily use.

## Prerequisites

- WebPos API running (`docker compose up --build` or local Kestrel).
- Migrations applied automatically on API startup (non-Testing).
- Secrets generated: `pwsh -File scripts/Generate-PilotEnv.ps1` (writes `.env` + terminal public key).
- Pilot terminal id: `00000000-0000-0000-0000-000000000010` (override with `PILOT_TERMINAL_ID`).
- Default pilot credentials (change for real shops):
  - Admin: `admin` / `admin123` (`PILOT_ADMIN_PASSWORD`)
  - Cashier PIN: `2468` (`PILOT_CASHIER_PIN`)

## 1. Generate enrollment keys

```powershell
powershell -File scripts\Generate-PilotEnv.ps1
```

### API host options

**A) Docker** (needs Docker Hub reachability):

```powershell
docker compose up --build -d
```

**B) Local host** (recommended when Docker Hub DNS fails):

```powershell
dotnet run --project WebPos\WebPos.csproj -c Debug --no-launch-profile --environment Pilot --urls http://localhost:8080
```

Wait until `GET http://localhost:8080/health` returns Healthy.

## 2. Enroll from Windows Terminal (recommended)

1. Launch **WebPos.WindowsTerminal**.
2. Open **Enroll Terminal** (`/enroll`) if not already enrolled.
3. Confirm Terminal ID, enter admin username/password, tap **Enroll**.
4. Certificate is stored in Windows Secure Storage; you are redirected to PIN login.

## 3. Enroll via API (optional / smoke)

**POST** `/api/terminal-enrollment`  
Header: `X-Api-Version: 1.0.0`

```json
{
  "terminalId": "00000000-0000-0000-0000-000000000010",
  "adminUsername": "admin",
  "adminPassword": "admin123"
}
```

Automated smoke (API only):

```powershell
pwsh -File scripts/Day1-PilotSmoke.ps1
```

## 4. Configure API base URL

`WebPos.WindowsTerminal/appsettings.json` (updated by Generate-PilotEnv):

```json
{
  "WebPosSdk": {
    "BaseAddress": "http://localhost:8080/",
    "ApiVersion": "1.0.0"
  }
}
```

Or: `WebPosSdk__BaseAddress=http://your-api-host:8080/`

## 5. Daily shift flow (pilot)

1. PIN login (`2468` for seeded cashier).
2. **Start shift** — opening cash.
3. **Sales** — Buffalo/Cow milk catalog is seeded at API startup.
4. **Close shift** — blind cash count → variance report.

## 6. Verify health

```bash
curl http://localhost:8080/health
```

## Troubleshooting

| Symptom | Cause |
|---------|--------|
| 401 on API calls | Missing/expired enrollment Bearer or invalid JWT |
| 403 on sales/shift | Tenant claim does not match resolved tenant |
| 426 | Missing or wrong `X-Api-Version` header |
| Enroll fails validating token | Terminal missing `Security:Enrollment:PublicKeyPem` (re-run Generate-PilotEnv) |
| No products on sales screen | Catalog seed failed — check API logs for StartupSeeding |
