using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace WebPos.WindowsTerminal.Services;

public interface IEmailService
{
    bool IsConfigured { get; }

    Task SendAsync(
        string toEmail,
        string subject,
        string bodyText,
        byte[]? attachmentBytes = null,
        string? attachmentFileName = null,
        CancellationToken cancellationToken = default);
}

public sealed class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsConfigured =>
        _configuration.GetValue("Smtp:Enabled", false)
        && !string.IsNullOrWhiteSpace(_configuration["Smtp:Host"])
        && !string.IsNullOrWhiteSpace(_configuration["Smtp:From"]);

    public async Task SendAsync(
        string toEmail,
        string subject,
        string bodyText,
        byte[]? attachmentBytes = null,
        string? attachmentFileName = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Email is not configured. Set Smtp:Enabled and Host/From in appsettings.json.");
        }

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            throw new ArgumentException("Recipient email is required.", nameof(toEmail));
        }

        string host = _configuration["Smtp:Host"]!;
        int port = _configuration.GetValue("Smtp:Port", 587);
        string from = _configuration["Smtp:From"]!;
        string? user = _configuration["Smtp:User"];
        string? password = _configuration["Smtp:Password"];
        bool useSsl = _configuration.GetValue("Smtp:UseSsl", true);

        MimeMessage message = new();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(toEmail.Trim()));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = bodyText ?? string.Empty };

        if (attachmentBytes is { Length: > 0 })
        {
            Multipart multipart = new("mixed")
            {
                message.Body,
                new MimePart("application", "pdf")
                {
                    Content = new MimeContent(new MemoryStream(attachmentBytes)),
                    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                    ContentTransferEncoding = ContentEncoding.Base64,
                    FileName = string.IsNullOrWhiteSpace(attachmentFileName)
                        ? "statement.pdf"
                        : attachmentFileName
                }
            };
            message.Body = multipart;
        }

        using SmtpClient client = new();
        try
        {
            await client.ConnectAsync(
                host,
                port,
                useSsl ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None,
                cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(user))
            {
                await client.AuthenticateAsync(user, password ?? string.Empty, cancellationToken)
                    .ConfigureAwait(false);
            }

            await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
            await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP send to {To} failed.", toEmail);
            throw;
        }
    }
}
