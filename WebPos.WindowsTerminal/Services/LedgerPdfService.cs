using Common.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QColors = QuestPDF.Helpers.Colors;

namespace WebPos.WindowsTerminal.Services;

public interface ILedgerPdfService
{
    byte[] GenerateStatement(
        PartyDto party,
        IReadOnlyList<PartyLedgerEntryDto> entries,
        DateOnly from,
        DateOnly to,
        long openingBalancePaisa,
        long closingBalancePaisa);
}

public sealed class LedgerPdfService : ILedgerPdfService
{
    static LedgerPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateStatement(
        PartyDto party,
        IReadOnlyList<PartyLedgerEntryDto> entries,
        DateOnly from,
        DateOnly to,
        long openingBalancePaisa,
        long closingBalancePaisa)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(entries);

        long totalDebit = entries.Sum(e => e.DebitPaisa);
        long totalCredit = entries.Sum(e => e.CreditPaisa);
        string roleLabel = string.Equals(party.Role, "SUPPLIER", StringComparison.OrdinalIgnoreCase)
            ? "Supplier"
            : "Customer";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text(TerminalBranding.PosDisplayName).Bold().FontSize(18);
                    col.Item().Text("Statement of Account").FontSize(14).FontColor(QColors.Grey.Darken2);
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(QColors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Item().Text($"{roleLabel}: {party.Name}").Bold();
                    col.Item().Text($"Phone: {party.PhoneNumber}");
                    if (!string.IsNullOrWhiteSpace(party.Email))
                    {
                        col.Item().Text($"Email: {party.Email}");
                    }

                    col.Item().Text($"Current Balance: Rs {FormatRs(party.CurrentBalancePaisa)}");
                    col.Item().PaddingTop(4).Text($"Period: {from:dd MMM yyyy} – {to:dd MMM yyyy}");
                    col.Item().Text($"Opening Balance: Rs {FormatRs(openingBalancePaisa)}");

                    col.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.2f);
                            c.RelativeColumn(1.4f);
                            c.RelativeColumn(2.2f);
                            c.RelativeColumn(1.1f);
                            c.RelativeColumn(1.1f);
                            c.RelativeColumn(1.2f);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Background(QColors.Grey.Lighten3).Padding(4).Text("Date").Bold();
                            h.Cell().Background(QColors.Grey.Lighten3).Padding(4).Text("Ref #").Bold();
                            h.Cell().Background(QColors.Grey.Lighten3).Padding(4).Text("Description").Bold();
                            h.Cell().Background(QColors.Grey.Lighten3).Padding(4).AlignRight().Text("Debit").Bold();
                            h.Cell().Background(QColors.Grey.Lighten3).Padding(4).AlignRight().Text("Credit").Bold();
                            h.Cell().Background(QColors.Grey.Lighten3).Padding(4).AlignRight().Text("Balance").Bold();
                        });

                        foreach (PartyLedgerEntryDto entry in entries)
                        {
                            string date = entry.CreatedAt.ToLocalTime().ToString("dd/MM/yy");
                            string reference = !string.IsNullOrWhiteSpace(entry.InvoiceNo)
                                ? entry.InvoiceNo!
                                : entry.PurchaseOrderId?.ToString("N")[..8] ?? entry.ReferenceDetails;
                            if (reference.Length > 18)
                            {
                                reference = reference[..18];
                            }

                            string desc = string.IsNullOrWhiteSpace(entry.ReferenceType)
                                ? entry.Type
                                : entry.ReferenceType;

                            table.Cell().BorderBottom(0.5f).BorderColor(QColors.Grey.Lighten2).Padding(3).Text(date);
                            table.Cell().BorderBottom(0.5f).BorderColor(QColors.Grey.Lighten2).Padding(3).Text(reference);
                            table.Cell().BorderBottom(0.5f).BorderColor(QColors.Grey.Lighten2).Padding(3).Text(desc);
                            table.Cell().BorderBottom(0.5f).BorderColor(QColors.Grey.Lighten2).Padding(3).AlignRight()
                                .Text(entry.DebitPaisa > 0 ? FormatRs(entry.DebitPaisa) : string.Empty);
                            table.Cell().BorderBottom(0.5f).BorderColor(QColors.Grey.Lighten2).Padding(3).AlignRight()
                                .Text(entry.CreditPaisa > 0 ? FormatRs(entry.CreditPaisa) : string.Empty);
                            table.Cell().BorderBottom(0.5f).BorderColor(QColors.Grey.Lighten2).Padding(3).AlignRight()
                                .Text(FormatRs(entry.NewBalancePaisa));
                        }
                    });

                    col.Item().PaddingTop(16).Background(QColors.Blue.Lighten5).Padding(10).Column(box =>
                    {
                        box.Item().Text("Summary").Bold();
                        box.Item().Text($"Total Debit: Rs {FormatRs(totalDebit)}");
                        box.Item().Text($"Total Credit: Rs {FormatRs(totalCredit)}");
                        box.Item().Text($"Closing / Net Balance: Rs {FormatRs(closingBalancePaisa)}").Bold();
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Generated by Smart POS · ");
                    x.Span(DateTime.Now.ToString("dd MMM yyyy HH:mm"));
                });
            });
        }).GeneratePdf();
    }

    private static string FormatRs(long paisa) => (paisa / 100m).ToString("N2");
}
