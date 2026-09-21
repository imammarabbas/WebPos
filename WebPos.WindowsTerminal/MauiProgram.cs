#if WINDOWS
using Microsoft.Maui.LifecycleEvents;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT.Interop;
#endif
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

#if WINDOWS
        builder.ConfigureLifecycleEvents(events =>
        {
            events.AddWindows(windows => windows.OnWindowCreated(window =>
            {
                window.ExtendsContentIntoTitleBar = true;

                nint handle = WindowNative.GetWindowHandle(window);
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(handle);
                AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

                if (appWindow is not null && AppWindowTitleBar.IsCustomizationSupported())
                {
                    AppWindowTitleBar titleBar = appWindow.TitleBar;
                    titleBar.ExtendsContentIntoTitleBar = true;

                    // Dark navy matching the #10182c header background
                    Windows.UI.Color darkHeaderColor = Windows.UI.Color.FromArgb(255, 16, 24, 44);

                    titleBar.BackgroundColor = darkHeaderColor;
                    titleBar.InactiveBackgroundColor = darkHeaderColor;
                    titleBar.ForegroundColor = Microsoft.UI.Colors.White;
                    titleBar.InactiveForegroundColor = Microsoft.UI.Colors.Gray;

                    titleBar.ButtonBackgroundColor = darkHeaderColor;
                    titleBar.ButtonInactiveBackgroundColor = darkHeaderColor;
                    titleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
                    titleBar.ButtonInactiveForegroundColor = Microsoft.UI.Colors.Gray;
                    titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(255, 30, 41, 59);
                    titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.White;
                    titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(255, 15, 23, 42);
                    titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.White;

                    void UpdateDragRegions()
                    {
                        int captionH = titleBar.Height;
                        int dragHeight = captionH > 32 ? captionH : 48;
                        int dragWidth = Math.Max(0, appWindow.Size.Width - titleBar.RightInset);
                        titleBar.SetDragRectangles(
                        [
                            new Windows.Graphics.RectInt32(0, 0, dragWidth, dragHeight)
                        ]);
                    }

                    UpdateDragRegions();
                    appWindow.Changed += (_, args) =>
                    {
                        if (args.DidSizeChange)
                        {
                            UpdateDragRegions();
                        }
                    };
                }
            }));
        });
#endif

        builder.Services.AddMauiBlazorWebView();

        builder.Configuration
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        // Override via env: WebPosSdk__BaseAddress=http://localhost:8080/
        builder.Services.AddWebPosSdk(builder.Configuration);

        IKeyProvider keyProvider =
            new MutableKeyProvider(new ConfigurationKeyProvider(builder.Configuration));
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
        builder.Services.AddSingleton<CustomerApiClient>();
        builder.Services.AddSingleton<ProductAdminApiClient>();
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
