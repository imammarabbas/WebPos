# Pilot week daily log

Use this during the one-store pilot. Keep entries short — pain points only.

## Credentials (change before shop network)

| Role | Value |
|------|--------|
| Admin | `admin` / `admin123` |
| Cashier PIN | `2468` |
| Terminal ID | `00000000-0000-0000-0000-000000000010` |
| API | `http://localhost:8080` |

## How to start the API (local, when Docker Hub is down)

```powershell
powershell -File scripts\Generate-PilotEnv.ps1
dotnet run --project WebPos\WebPos.csproj -c Debug --no-launch-profile --environment Pilot --urls http://localhost:8080
```

Then smoke: enroll → login → shift → sale → close (see `scripts/Day1-PilotSmoke.ps1` or manual API calls).

## Day 0 — dry run (automated)

| Check | Result |
|-------|--------|
| `/health` | Healthy |
| Enroll terminal | OK (API; UI checklist in DEVICE_ENROLLMENT_CHECKLIST.md) |
| PIN login | OK |
| Start shift | OK |
| Cash sale (Buffalo Milk x2) | OK — 44000 paisa |
| Close shift variance | 0 (balanced) |
| Date | 2026-07-21 |

## Day 1 — dry run note

Second automated pass hit enrollment rate limit (5 / 5 min) after Day 0 — expected. Re-run after cooldown or use an already-issued enrollment token. Day 0 reconcile remains the go-live proof.

## Daily template (copy for Days 1–7)

### Day __ — ____-__-__

- [ ] API up / health green
- [ ] Cashier PIN login OK
- [ ] Shift opened
- [ ] Sales count: ____
- [ ] Shift closed; expected cash: ____; counted: ____; variance: ____
- [ ] Gaps / friction (cashier notes):

```
-


```

## End-of-week summary

- Days completed: ____ / 7
- Blocking issues:
- Nice-to-haves:
- Ready for deferred ERP triage? Y/N
