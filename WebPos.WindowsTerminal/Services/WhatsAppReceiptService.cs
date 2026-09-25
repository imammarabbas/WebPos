using System.Text;
using Common.Models;
using WebPos.WindowsTerminal;

namespace WebPos.WindowsTerminal.Services;

public sealed class WhatsAppReceiptService
{
    public string BuildReceiptMessage(
        CompleteSaleResult sale,
        IReadOnlyList<CartLine> lines,
        string paymentLabel,
        string? onlineTxnRef = null,
        long? changePaisa = null)
    {
        ArgumentNullException.ThrowIfNull(sale);
        ArgumentNullException.ThrowIfNull(lines);

        StringBuilder body = new();
        body.AppendLine($"{TerminalBranding.PosDisplayName} — Receipt");
        body.AppendLine($"Invoice: {sale.InvoiceNo}");
        body.AppendLine($"Payment: {paymentLabel}");
        if (!string.IsNullOrWhiteSpace(onlineTxnRef))
        {
            body.AppendLine($"Txn ref: {onlineTxnRef.Trim()}");
        }

        body.AppendLine();
        foreach (CartLine line in lines)
        {
            string qty = line.Product.IsLoose
                ? line.Quantity.ToString("0.##")
                : line.Quantity.ToString("0");
            body.AppendLine(
                $"{line.Product.Name} × {qty} = Rs {(line.LineTotalPaisa / 100m):N2}");
        }

        body.AppendLine();
        body.AppendLine($"Total: Rs {(sale.TotalAmountPaisa / 100m):N2}");
        if (changePaisa is > 0)
        {
            body.AppendLine($"Change: Rs {(changePaisa.Value / 100m):N2}");
        }

        body.AppendLine();
        body.AppendLine("Thank you for shopping with us.");
        return body.ToString();
    }

    public string BuildLedgerMessage(
        string partyName,
        DateOnly from,
        DateOnly to,
        long openingBalancePaisa,
        long closingBalancePaisa,
        IReadOnlyList<PartyLedgerEntryDto> entries,
        string partyRole = "CUSTOMER")
    {
        ArgumentNullException.ThrowIfNull(entries);

        bool isSupplier = string.Equals(partyRole, "SUPPLIER", StringComparison.OrdinalIgnoreCase);
        long totalDebit = entries.Sum(e => e.DebitPaisa);
        long totalCredit = entries.Sum(e => e.CreditPaisa);

        StringBuilder body = new();
        body.AppendLine($"{TerminalBranding.PosDisplayName} — {(isSupplier ? "Supplier" : "Customer")} Statement");
        body.AppendLine();
        body.AppendLine($"{(isSupplier ? "Supplier" : "Customer")}: {partyName}");
        body.AppendLine($"Period: {from:dd/MM/yyyy} – {to:dd/MM/yyyy}");
        body.AppendLine();
        body.AppendLine($"Opening Balance: Rs {(openingBalancePaisa / 100m):N2}");
        body.AppendLine($"Total Debit: Rs {(totalDebit / 100m):N2}");
        body.AppendLine($"Total Credit: Rs {(totalCredit / 100m):N2}");
        body.AppendLine($"Closing Balance: Rs {(closingBalancePaisa / 100m):N2}");
        body.AppendLine();
        body.AppendLine("Date       Ref           Debit       Credit      Balance");

        const int maxLines = 40;
        int shown = 0;
        foreach (PartyLedgerEntryDto entry in entries)
        {
            if (shown >= maxLines)
            {
                body.AppendLine($"… and {entries.Count - maxLines} more entries (see PDF).");
                break;
            }

            string date = entry.CreatedAt.ToLocalTime().ToString("dd/MM/yy");
            string invoice = string.IsNullOrWhiteSpace(entry.InvoiceNo)
                ? (string.IsNullOrWhiteSpace(entry.ReferenceDetails) ? entry.Type : entry.ReferenceDetails)
                : entry.InvoiceNo;
            if (invoice.Length > 12)
            {
                invoice = invoice[^12..];
            }

            string debit = entry.DebitPaisa > 0 ? $"{entry.DebitPaisa / 100m:N2}" : string.Empty;
            string credit = entry.CreditPaisa > 0 ? $"{entry.CreditPaisa / 100m:N2}" : string.Empty;
            body.AppendLine(
                $"{date,-10} {invoice,-13} {debit,-11} {credit,-11} {(entry.NewBalancePaisa / 100m):N2}");
            shown++;
        }

        body.AppendLine();
        body.AppendLine($"Net Balance: Rs {(closingBalancePaisa / 100m):N2}");
        return body.ToString();
    }

    public string? BuildWhatsAppUrl(string? phoneRaw, string message)
    {
        string? digits = NormalizePhoneDigits(phoneRaw);
        if (string.IsNullOrEmpty(digits))
        {
            return null;
        }

        string encoded = Uri.EscapeDataString(message);
        return $"https://wa.me/{digits}?text={encoded}";
    }

    /// <summary>
    /// Normalizes PK mobile numbers to WhatsApp international form (92xxxxxxxxxx).
    /// </summary>
    public static string? NormalizePhoneDigits(string? phoneRaw)
    {
        if (string.IsNullOrWhiteSpace(phoneRaw))
        {
            return null;
        }

        StringBuilder digits = new();
        foreach (char c in phoneRaw)
        {
            if (char.IsDigit(c))
            {
                digits.Append(c);
            }
        }

        string value = digits.ToString();
        if (value.Length == 0)
        {
            return null;
        }

        if (value.StartsWith('0') && value.Length == 11)
        {
            return "92" + value[1..];
        }

        if (value.StartsWith("92", StringComparison.Ordinal) && value.Length >= 12)
        {
            return value;
        }

        if (value.Length >= 10)
        {
            return value;
        }

        return null;
    }
}
