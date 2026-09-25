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

        var windowChrome = new WindowChromeService();
        builder.Services.AddSingleton(windowChrome);

#if WINDOWS
        builder.ConfigureLifecycleEvents(events =>
        {
            events.AddWindows(windows => windows.OnWindowCreated(window =>
            {
                nint handle = WindowNative.GetWindowHandle(window);
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(handle);
                AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
                if (appWindow is null)
                {
                    return;
                }

                // Only the first window is the POS shell. Customer display is attached
                // separately so fullscreen / title-bar chrome never cross-wire.
                if (!windowChrome.TryAttachMain(appWindow))
                {
                    windowChrome.AttachCustomer(appWindow);

                    // Customer-only F11 — never ToggleMainFullscreen.
                    void AttachCustomerNativeF11()
                    {
                        if (window.Content is not Microsoft.UI.Xaml.UIElement root)
                        {
                            return;
                        }

                        root.AddHandler(
                            Microsoft.UI.Xaml.UIElement.KeyDownEvent,
                            new Microsoft.UI.Xaml.Input.KeyEventHandler((_, args) =>
                            {
                                if (args.Key != Windows.System.VirtualKey.F11)
                                {
                                    return;
                                }

                                windowChrome.ToggleCustomerFullscreen();
                                args.Handled = true;
                            }),
                            handledEventsToo: true);
                    }

                    if (window.Content is not null)
                    {
                        AttachCustomerNativeF11();
                    }
                    else
                    {
                        void OnCustomerActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
                        {
                            window.Activated -= OnCustomerActivated;
                            AttachCustomerNativeF11();
                        }

                        window.Activated += OnCustomerActivated;
                    }

                    return;
                }

                window.ExtendsContentIntoTitleBar = true;

                // Native F11 when WinUI content has focus (WebView2 still uses the JS bridge).
                void AttachNativeF11()
                {
                    if (window.Content is not Microsoft.UI.Xaml.UIElement root)
                    {
                        return;
                    }

                    root.AddHandler(
                        Microsoft.UI.Xaml.UIElement.KeyDownEvent,
                        new Microsoft.UI.Xaml.Input.KeyEventHandler((_, args) =>
                        {
                            if (args.Key != Windows.System.VirtualKey.F11)
                            {
                                return;
                            }

                            windowChrome.ToggleMainFullscreen();
                            args.Handled = true;
                        }),
                        handledEventsToo: true);
                }

                if (window.Content is not null)
                {
                    AttachNativeF11();
                }
                else
                {
                    void OnActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
                    {
                        window.Activated -= OnActivated;
                        AttachNativeF11();
                    }

                    window.Activated += OnActivated;
                }

                if (AppWindowTitleBar.IsCustomizationSupported())
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
                        // Skip while fullscreen — title bar insets are unstable then.
                        if (appWindow.Presenter?.Kind == AppWindowPresenterKind.FullScreen)
                        {
                            return;
                        }

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
                        if (args.DidSizeChange || args.DidPresenterChange)
                        {
                            UpdateDragRegions();
                        }
                    };
                }
            }));
        });
#endif

        builder.Services.AddMauiBlazorWebView();

        string contentRoot = AppContext.BaseDirectory;
        builder.Configuration
            .SetBasePath(contentRoot)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables();

        // Override via env: WebPosSdk__BaseAddress=http://localhost:8080/
        builder.Services.AddWebPosSdk(builder.Configuration);

        string? configuredBase = builder.Configuration["WebPosSdk:BaseAddress"];
#if DEBUG
        System.Diagnostics.Debug.WriteLine(
            $"[WebPos.WindowsTerminal] ContentRoot={contentRoot}; WebPosSdk:BaseAddress={configuredBase ?? "(unset → SDK default)"}");
#endif

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
        builder.Services.AddSingleton<CashAccountApiClient>();
        builder.Services.AddSingleton<CartService>();
        builder.Services.AddSingleton<CustomerDisplayState>();
        builder.Services.AddSingleton<CustomerDisplayWindowService>();
        builder.Services.AddSingleton<CustomerApiClient>();
        builder.Services.AddSingleton<ProductAdminApiClient>();
        builder.Services.AddSingleton<WhatsAppReceiptService>();
        builder.Services.AddSingleton<ILedgerPdfService, LedgerPdfService>();
        builder.Services.AddSingleton<IEmailService, SmtpEmailService>();
        builder.Services.AddSingleton<StatementFileService>();
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
