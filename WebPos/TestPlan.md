# POS System - Testing Traceability Matrix

| ID | Feature | Priority | Service / Logic | Test Focus (ACID/Business) |
|:---|:---|:---:|:---|:---|
| 01 | **Double-Entry Engine** | Critical | `ITransactionService` | Ensure debit=credit in `GeneralLedger`. |
| 02 | **Atomic Sales Flow** | Critical | `SalesService` | Sale -> Inv -> Ledger. Rollback if any fails. |
| 03 | **Unit Conversion** | High | `InventoryService` | KG/G conversion logic accuracy. |
| 04 | **Discount Handling** | High | `SalesService` | Ensure discount is recorded as an Expense. |
| 05 | **Procurement** | High | `ProcurementService` | Bulk collection -> Ledger -> Stock update. |
| 06 | **Party Ledger** | High | `FinanceService` | Unified Party lookup for Customer/Supplier. |
| 07 | **Split Payments** | Medium | `SalesService` | Cash + EasyPaisa + Bank in one invoice. |
| 08 | **Sales Returns** | Critical | `SalesService` | Inventory restoration + Ledger reversal. |
| 09 | **Purchase Returns** | Critical | `ProcurementService`| Credit note generation + Inventory reduction. |
| 10 | **Shift Management** | High | `ShiftService` | Opening float -> Sales/Exp -> Closing count. |
| 11 | **Cash Reconciliation** | High | `ShiftService` | Expected vs Actual variance report. |
| 12 | **Owner Draw** | Medium | `FinanceService` | Segregate personal/business funds. |
| 13 | **Damaged Stock** | Medium | `InventoryService` | Shrinkage logs -> Inventory loss ledger. |
| 14 | **Multi-Account** | High | `FinanceService` | Ensure distinct tracking for Bank/Wallet/Cash. |
| 15 | **Receipt Tracking** | Low | `ReportingService` | Verify `ReceiptNumber` linkage for audits. |
| 16 | **Profitability BI** | Low | `ReportingService` | Gross Profit per Category/Product. |

## Strategy
- **Infrastructure:** `Testcontainers` (PostgreSQL) for all integration tests.
- **Rules:** - No mocks for `DbContext` or `ITransactionService`.
    - Every test must perform a `Database.BeginTransaction()` to simulate production.
    - All monetary values must be validated as `long` (Paisa).