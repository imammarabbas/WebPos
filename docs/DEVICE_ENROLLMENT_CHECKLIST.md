# Device enrollment checklist (Windows Terminal)

Complete once per machine after the API is healthy on `http://localhost:8080`.

## Prerequisites

- [ ] `powershell -File scripts\Generate-PilotEnv.ps1` (writes terminal public key + BaseAddress)
- [ ] API running: `dotnet run --project WebPos --no-launch-profile --environment Pilot --urls http://localhost:8080`
- [ ] `GET http://localhost:8080/health` → Healthy
- [ ] Day-0 API smoke already passed (see [`PILOT_WEEK_LOG.md`](PILOT_WEEK_LOG.md))

## On the Windows Terminal app

1. Launch `WebPos.WindowsTerminal` (Debug).
2. If not enrolled, app opens **`/enroll`**.
3. Terminal ID: `00000000-0000-0000-0000-000000000010`
4. Admin: `admin` / `admin123` → **Enroll**.
5. PIN login: `2468`.
6. Start shift (opening cash `0` or float).
7. Sell Buffalo or Cow milk.
8. **Close shift** — enter blind cash count matching sales; confirm variance 0.
9. Sign off.

## API-only enrollment (installer fallback)

```http
POST /api/terminal-enrollment
X-Api-Version: 1.0.0
{ "terminalId":"00000000-0000-0000-0000-000000000010", "adminUsername":"admin", "adminPassword":"admin123" }
```

Store returned `token` via terminal Enroll UI (preferred) or `SecureEnrollmentCertificateStore.StoreAsync`.

## Sign-off

| Step | Done |
|------|------|
| Enrolled via UI (no SecureStorage hack) | |
| Sale completed on device | |
| Close shift variance shown | |

Operator: ____________  Date: ____________
