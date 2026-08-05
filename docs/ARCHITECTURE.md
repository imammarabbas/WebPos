# WebPos architecture

## One core, many thin terminals

WebPos is sold as a **multi-tenant cloud POS**. Domain rules live in **one place**; each shop runs a thin enrolled terminal that talks to that core over HTTP.

| Layer | Responsibility | Location |
|-------|----------------|----------|
| **Core** | Sales, stock, purchases, ledger, shifts, tenancy, enrollment — source of truth | [`WebPos.Core`](../WebPos.Core/) (class library; host must not duplicate domain rules) |
| **API + Master host** | REST controllers and Master Blazor UI over Core | [`WebPos`](../WebPos/) |
| **Client SDK** | Shared DTOs and typed HTTP client | [`WebPos.Client.Sdk`](../WebPos.Client.Sdk/) |
| **Windows Terminal** | Cart UI, PIN/session UX, receipts — **no domain/DB** | [`WebPos.WindowsTerminal`](../WebPos.WindowsTerminal/) |

```
Shop terminals  --HTTPS + enrollment cert-->  WebPos host
                                               ├─ Controllers → Core
                                               ├─ Master Blazor → Core (DI)
                                               └─ Postgres (tenant-filtered)
```

## Rules of the road

1. **Core is authoritative.** Stock, price, discount, credit, and ledger validation happen on the server.
2. **Terminals are clients.** They call `IApiClient` via thin `*ApiClient` wrappers. They must **not** reference `WebPos.Core` or EF.
3. **Master shares Core** with the API in the same host process — not a second business stack.
4. **Multi-shop packaging:** one hosted API/DB; one tenant per shop; enroll each terminal; same terminal binary for all shops.
5. **Offline (future):** local queue that replays to the same API/Core — do not ship a second rule engine.

## Naming

Terminal wrappers use the `*ApiClient` suffix so they are not confused with Core services such as `WebPos.Core.Services.SalesService`.

## Local run (same data for Master + Terminal)

See [`LOCAL_SETUP.md`](LOCAL_SETUP.md). Short version: one host on **http://localhost:8080** for API + Master; Terminal points at that URL; one Postgres database named `WebPos`.
