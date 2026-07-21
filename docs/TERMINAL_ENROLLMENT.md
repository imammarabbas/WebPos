# Terminal Enrollment Procedure

One-time setup per POS terminal before daily use.

## Prerequisites

- WebPos API running and reachable (HTTPS).
- PostgreSQL migrated (`dotnet ef database update --project WebPos`).
- Production secrets configured (see `.env.example` / `appsettings.Production.json`).
- A **Terminal** row exists in the database (pilot seed creates id `00000000-0000-0000-0000-000000000010`).
- Admin credentials (default seed: `admin` / `admin123` — change in production).

## 1. Generate enrollment keys (server)

Generate a 3072-bit RSA key pair. Store **private** key on the API server only:

```bash
openssl genrsa -out enrollment-private.pem 3072
openssl rsa -in enrollment-private.pem -pubout -out enrollment-public.pem
```

Set environment variables (or Key Vault):

- `Security__Enrollment__PrivateKeyPem` — full PEM private key
- `Security__Enrollment__PublicKeyPem` — full PEM public key (also on terminal for validation)

## 2. Enroll the terminal

**POST** `/api/terminal-enrollment`  
Header: `X-Api-Version: 1.0.0`

```json
{
  "terminalId": "00000000-0000-0000-0000-000000000010",
  "adminUsername": "admin",
  "adminPassword": "admin123"
}
```

Response includes `token` (enrollment JWT), `tenantId`, `terminalId`, `expiresAtUtc`.

Save the token securely on the terminal via `SecureEnrollmentCertificateStore` (Windows Terminal does this automatically when enrollment UI is used, or inject via secure provisioning).

## 3. Configure Windows Terminal

Edit `WebPos.WindowsTerminal/appsettings.json` or set environment variable:

```json
{
  "WebPosSdk": {
    "BaseAddress": "https://your-api-host:8080/",
    "ApiVersion": "1.0.0"
  }
}
```

Or: `WebPosSdk__BaseAddress=https://your-api-host:8080/`

## 4. Daily shift flow (pilot)

1. Launch **WebPos.WindowsTerminal** (valid enrollment cert required).
2. **PIN login** — `POST /api/auth/login` (SDK sends enrollment Bearer automatically).
3. **Start shift** — `POST /api/shift/start`.
4. **Load products** — `GET /api/products/for-sale`.
5. **Complete sale** — `POST /api/sales/complete`.
6. **Close shift** — `POST /api/shift/close` → review cash variance report.

Optional: `GET /api/sync/bootstrap` on shift start for offline catalogue cache.

## 5. Verify health

```bash
curl https://your-api-host:8080/health
```

Should return `Healthy` when database is reachable.

## Troubleshooting

| Symptom | Cause |
|---------|--------|
| 401 on API calls | Missing/expired enrollment Bearer or invalid JWT |
| 403 on sales/shift | Tenant claim does not match resolved tenant |
| 426 | Missing or wrong `X-Api-Version` header |
| Terminal won't start | No enrollment certificate in secure store |
