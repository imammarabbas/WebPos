# Pilot feedback triage (post week)

Fill this **after** one real store week using [`PILOT_WEEK_LOG.md`](PILOT_WEEK_LOG.md). Do not start ERP work until the week log has at least several day entries.

## Decision rule

Only schedule an item if cashiers or the owner **blocked** on it during the week (lost sale, could not close day, compliance). Ignore speculative wants.

## Deferred backlog (from ERP_REQUIREMENTS)

| Item | Status | Shop blocked? (Y/N) | Notes / schedule |
|------|--------|---------------------|------------------|
| Split payments / partial cash+credit | Deferred | | |
| WhatsApp receipts | Deferred | | |
| Damaged stock / shrinkage service | Deferred | | |
| Stock valuation / net-profit BI UI | Deferred | | |
| Multi-wallet (EasyPaisa/JazzCash) shift reconcile | Deferred | | |
| Owner draw / expense HTTP controllers | Deferred | | |
| Sale-time inventory/COGS ledger polish | Deferred | | |
| Full offline client sync store | Deferred | | |

## Gaps observed in week (copy from daily log)

1.
2.
3.

## Next build slice (max 1–2 items)

1.
2.

## Explicit non-goals until next triage

- Clean Architecture full split
- Multi-tenant admin portal polish
- Template Blazor pages cleanup (Counter/Weather) unless it confuses cashiers
