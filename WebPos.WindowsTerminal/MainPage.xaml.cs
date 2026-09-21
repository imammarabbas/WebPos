namespace WebPos.WindowsTerminal;

public partial class MainPage : ContentPage
{
#if WINDOWS
    private Microsoft.UI.Xaml.Controls.WebView2? _webView;
#endif

    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
#if WINDOWS
        if (blazorWebView.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.WebView2 webView)
        {
            return;
        }

        await webView.EnsureCoreWebView2Async();
        webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
        webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
        _webView = webView;
        webView.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);

        if (Window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
        {
            nativeWindow.Activated += OnNativeWindowActivated;
        }
        else if (Window is not null)
        {
            Window.Activated += OnMauiWindowActivated;
        }
#endif
        await Task.CompletedTask;
    }

#if WINDOWS
    private void OnNativeWindowActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.Deactivated)
        {
            return;
        }

        FocusWebView();
    }

    private void OnMauiWindowActivated(object? sender, EventArgs e) => FocusWebView();

    private void FocusWebView()
    {
        try
        {
            _webView?.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        }
        catch
        {
            /* ignore until WebView2 is ready */
        }
    }
#endif
}
