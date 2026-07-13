# ERP Business Requirements & Strategic Plan

## 1. Technical Standards
- **Architecture:** Clean Architecture (Core/Application/Infrastructure).
- **Core Engine:** All financial movements (Sales, Purchases, Payments, Expenses) must use `ITransactionService`.
- **Integrity:** - Use `long` for money (Paisa). Never use float/double.
  - Every multi-table operation must use `IDbContextTransaction` for atomicity.
  - Data is immutable. Use "Correction Entries" for errors, never delete.
- **Service Pattern:** Use Interfaces (e.g., ISalesService) for all business logic.

## 2. Service Definitions
- **TransactionService:** The central engine. Records all double-entry ledger entries.
- **SalesService:** - Handle Invoices & Discounts. 
  - Discount must be recorded as an "Expense" category.
  - Update Inventory & Post to Ledger in one transaction.
- **ProcurementService (Milk Collection):** - Track collection by Supplier. 
  - Handle Supplier Credit and Payments.
- **FinanceService:** - Unified Party Ledger for Customers and Suppliers. 
  - Use "Role" to separate logic (Customer vs Supplier).
- **ExpenseService:** Categorized operational costs (Rent, Wages, Electricity).
- **ReportingService:** Provide Net Profit, Cash Flow, and Stock Valuation.

## 3. Financial Logic
- **Party Roles:** Customers and Suppliers share the `Party` table but are filtered by `Role`.
- **Accounting:** - Sale = Revenue (Credit) + Account Receivable (Debit).
  - Discount = Expense (Debit) + Revenue (Credit/Reduction).
  - Purchase = Inventory (Debit) + Account Payable (Credit).## 5. Specific Business Context (Milk Shop)
- **Units of Measure:** System must handle multiple units (e.g., kg for bulk purchase, gram for retail sale). Conversion logic is required.
- **Product Variants:** Milk must be tracked by type (Buffalo vs. Cow).
- **Payment Modes:** Support for multiple channels: Cash, EasyPaisa, Bank Transfer, Mobile Wallet.
- **Transaction Types:** 
  - Sales (including partial payments).
  - Purchases (bulk collection from farmers/suppliers).
  - Sales Returns & Purchase Returns (must trigger inventory and ledger reversals).
  - Balance Adjustments (for manual corrections).
- **Dairy Dynamics:** Procurement must track fat/SNF or quality metrics if applicable, but fundamentally track weight (kg/gram).
# ERP Retail & Dairy System - Requirements Specification

1.  **Core Financial Engine:** Double-entry, atomic transactions, amount stored in `long` (Paisa), correction-only ledger.
2.  **Retail Logic:** Multi-mode lookup (Barcode/SKU/PLU), category-tap, automatic "Initial Purchase" for new stock.
3.  **Unit Conversion:** Flexible units (KG/G/Tray/Dozen) with strict multiplier logic.
4.  **Discount Engine:** Separate recording of `DiscountAmount` and `DiscountReason` as a business expense.
5.  **Customer & Supplier CRM:** Unified party table, partial payment tracking, and ledger balance updates.
6.  **Digital Communication:** WhatsApp-integrated receipt link with phone number override for walk-ins.
7.  **Procurement:** Scan-purchase and bulk invoice-batch processing.
8.  **Purchase Returns:** Full/partial returns with automatic ledger credit notes and inventory updates.
9.  **BI & Analytics:** Granular Profitability (Gross Profit per Product/Category), exportable to CSV/JSON.
10. **Multi-Account Engine:** Distinct tracking for 'Cash', 'Bank', and 'Digital Wallets' (EasyPaisa/JazzCash).
11. **Split Payments:** Support for multiple accounts in a single invoice.
12. **Receipt Traceability:** All returns/adjustments linked to `ReceiptNumber` for audit-perfect reconciliation.
13. **Expenses & Shrinkage:** Tracking for Salaries, Rent, Bills, Damages, and Stolen goods (Inventory Loss).
14. **Management Cash:** Dedicated "Owner Draw" feature to segregate business/personal funds.
15. **Shift Management:** Start/End shift logic with opening float and mandatory closing counts.
16. **Cash Reconciliation:** Automatic variance report (Expected vs. Actual) for all payment accounts per shift.