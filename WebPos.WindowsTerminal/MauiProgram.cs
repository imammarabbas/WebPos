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

        // Override via env: WebPosSdk__BaseAddress=https://your-server:8080/
        builder.Services.AddWebPosSdk(builder.Configuration);

        IKeyProvider keyProvider =
            new ConfigurationKeyProvider(builder.Configuration);
        IEnrollmentCertificateValidator certificateValidator =
            new EnrollmentCertificateValidator(keyProvider);
        var certificateStore =
            new SecureEnrollmentCertificateStore(certificateValidator);
        certificateStore.InitializeAsync().GetAwaiter().GetResult();
        Common.Models.EnrollmentIdentity enrollmentIdentity =
            certificateStore.Identity
            ?? throw new InvalidOperationException(
                "Validated terminal enrollment identity is unavailable.");

        builder.Services.AddSingleton(keyProvider);
        builder.Services.AddSingleton(certificateValidator);
        builder.Services.AddSingleton(certificateStore);
        builder.Services.AddSingleton<IEnrollmentCertificateAccessor>(
            certificateStore);

        builder.Services.AddSingleton(
            new TerminalOptions(
                enrollmentIdentity.TenantId,
                enrollmentIdentity.TerminalId));
        builder.Services.AddSingleton<ISessionService, SessionService>();
        builder.Services.AddSingleton<LoginService>();
        builder.Services.AddSingleton<ShiftService>();
        builder.Services.AddSingleton<SalesService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
