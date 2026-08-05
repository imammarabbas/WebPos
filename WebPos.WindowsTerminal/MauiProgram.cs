using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebPos.Client.Sdk;
using WebPos.Client.Sdk.Security;
using WebPos.WindowsTerminal.Services;

namespace WebPos.WindowsTerminal;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        builder.Configuration
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        // Override via env: WebPosSdk__BaseAddress=http://localhost:8080/
        builder.Services.AddWebPosSdk(builder.Configuration);

        IKeyProvider keyProvider =
            new ConfigurationKeyProvider(builder.Configuration);
        IEnrollmentCertificateValidator certificateValidator =
            new EnrollmentCertificateValidator(keyProvider);
        var certificateStore =
            new SecureEnrollmentCertificateStore(certificateValidator);
        certificateStore.InitializeAsync().GetAwaiter().GetResult();

        var terminalOptions = new TerminalOptions();
        if (certificateStore.Identity is { } identity)
        {
            terminalOptions.ApplyEnrollment(identity.TenantId, identity.TerminalId);
        }

        builder.Services.AddSingleton(keyProvider);
        builder.Services.AddSingleton(certificateValidator);
        builder.Services.AddSingleton(certificateStore);
        builder.Services.AddSingleton<IEnrollmentCertificateAccessor>(
            certificateStore);

        builder.Services.AddSingleton(terminalOptions);
        builder.Services.AddSingleton<ISessionService, SessionService>();
        builder.Services.AddSingleton<LoginApiClient>();
        builder.Services.AddSingleton<ShiftApiClient>();
        builder.Services.AddSingleton<SalesApiClient>();
        builder.Services.AddSingleton<CartService>();
        builder.Services.AddSingleton<ReceiveDraftService>();
        builder.Services.AddSingleton<CustomerApiClient>();
        builder.Services.AddSingleton<WhatsAppReceiptService>();
        builder.Services.AddSingleton<LastSaleReceiptStore>();
        builder.Services.AddSingleton<CartHoldService>();
        builder.Services.AddSingleton<ShiftStatusApiClient>();
        builder.Services.AddSingleton<InvoiceApiClient>();
        builder.Services.AddSingleton<PurchaseIntakeApiClient>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
