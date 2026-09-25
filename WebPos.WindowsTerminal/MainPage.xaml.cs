using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WebPos.WindowsTerminal;

public partial class MainPage : ContentPage
{
#if WINDOWS
    private const int VkLButton = 0x01;

    private Microsoft.UI.Xaml.Controls.WebView2? _webView;
    private Microsoft.UI.Windowing.AppWindow? _appWindow;
    private Microsoft.UI.Xaml.Window? _nativeWindow;
    private nint _mainHwnd;
    private CancellationTokenSource? _focusRestoreCts;
    private bool _handlersAttached;
    /// <summary>Suppress GotFocus/LostFocus while we programmatically Focus the WebView.</summary>
    private int _suppressFocusEvents;
    private volatile bool _isWindowActive = true;
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

        if (_handlersAttached)
        {
            return;
        }

        _handlersAttached = true;

        if (Window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
        {
            _nativeWindow = nativeWindow;
            nativeWindow.Activated += OnNativeWindowActivated;

            try
            {
                _mainHwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_mainHwnd);
                _appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                if (_appWindow is not null)
                {
                    _appWindow.Changed += OnAppWindowChanged;
                }
            }
            catch
            {
                /* ignore until WinUI window is ready */
            }
        }
        else if (Window is not null)
        {
            Window.Activated += OnMauiWindowActivated;
        }

        // Title-bar click: WebView loses focus while the window stays activated/foreground.
        webView.LostFocus += OnWebViewLostFocus;
        // Clicking back into the WebView after title-bar/frame interaction.
        webView.GotFocus += OnWebViewGotFocus;
#endif
        await Task.CompletedTask;
    }

#if WINDOWS
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private void OnNativeWindowActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.Deactivated)
        {
            _isWindowActive = false;
            return;
        }

        _isWindowActive = true;
        SchedulePosFocusRestore("Activated");
    }

    private void OnMauiWindowActivated(object? sender, EventArgs e)
    {
        _isWindowActive = true;
        SchedulePosFocusRestore("MauiActivated");
    }

    private void OnAppWindowChanged(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowChangedEventArgs args)
    {
        // Size/position alone must never restore if another app is foreground.
        // Schedule only; RestorePosFocusAfterSettleAsync re-checks after debounce.
        if (args.DidSizeChange || args.DidPresenterChange || args.DidPositionChange)
        {
            SchedulePosFocusRestore("AppWindowChanged");
        }
    }

    private void OnWebViewLostFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (Volatile.Read(ref _suppressFocusEvents) > 0)
        {
            TraceFocus("LostFocus suppressed");
            return;
        }

        TraceFocus("LostFocus");
        // Title-bar click: window stays foreground; restore after settle.
        SchedulePosFocusRestore("LostFocus");
    }

    private void OnWebViewGotFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (Volatile.Read(ref _suppressFocusEvents) > 0)
        {
            TraceFocus("GotFocus suppressed");
            return;
        }

        // WebView already has native focus; only repair DOM target (scan vs modal).
        ScheduleDomFocusOnly();
    }

    /// <summary>
    /// True only when this POS window still owns input (WinUI active + OS foreground).
    /// </summary>
    private bool IsPosForegroundActive()
    {
        if (!_isWindowActive)
        {
            return false;
        }

        nint hwnd = _mainHwnd;
        if (hwnd == 0 && _nativeWindow is not null)
        {
            try
            {
                hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_nativeWindow);
                _mainHwnd = hwnd;
            }
            catch
            {
                return false;
            }
        }

        if (hwnd == 0)
        {
            return false;
        }

        try
        {
            return GetForegroundWindow() == hwnd;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsPrimaryMouseButtonDown()
    {
        try
        {
            return (GetAsyncKeyState(VkLButton) & 0x8000) != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Debounce coalesces rapid resize/drag/LostFocus events; not a permanent poll.
    /// </summary>
    private void SchedulePosFocusRestore(string reason)
    {
        CancellationTokenSource cts = new();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _focusRestoreCts, cts);
        if (previous is not null)
        {
            try { previous.Cancel(); } catch { /* ignore */ }
            previous.Dispose();
        }

        TraceFocus($"Schedule restore ({reason})");
        _ = RestorePosFocusAfterSettleAsync(cts, restoreNativeWebView: true);
    }

    private void ScheduleDomFocusOnly()
    {
        CancellationTokenSource cts = new();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _focusRestoreCts, cts);
        if (previous is not null)
        {
            try { previous.Cancel(); } catch { /* ignore */ }
            previous.Dispose();
        }

        _ = RestorePosFocusAfterSettleAsync(cts, restoreNativeWebView: false);
    }

    private async Task RestorePosFocusAfterSettleAsync(CancellationTokenSource cts, bool restoreNativeWebView)
    {
        try
        {
            await Task.Delay(120, cts.Token);
            if (!ReferenceEquals(_focusRestoreCts, cts))
            {
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                TraceFocus("debounce settle");

                // Re-check after debounce: size/position/LostFocus must never steal focus from another app.
                if (!IsPosForegroundActive())
                {
                    TraceFocus("foreground gate: skip");
                    return;
                }

                TraceFocus("foreground gate: pass");

                // Mid title-bar drag: do not yank focus; AppWindow.Changed covers release.
                if (restoreNativeWebView && IsPrimaryMouseButtonDown())
                {
                    TraceFocus("mouse down: skip mid-drag");
                    return;
                }

                if (restoreNativeWebView)
                {
                    FocusWebView();
                }

                await InvokeWebPosFocusPrimaryAsync();
            });
        }
        catch (OperationCanceledException)
        {
            /* superseded by a newer restore request */
        }
        finally
        {
            if (ReferenceEquals(_focusRestoreCts, cts))
            {
                Interlocked.CompareExchange(ref _focusRestoreCts, null, cts);
            }

            cts.Dispose();
        }
    }

    private void FocusWebView()
    {
        try
        {
            Interlocked.Increment(ref _suppressFocusEvents);
            TraceFocus("FocusWebView");
            _webView?.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        }
        catch
        {
            /* ignore until WebView2 is ready */
        }
        finally
        {
            // Allow synchronous GotFocus/LostFocus from Focus() to observe the suppress flag,
            // then clear so genuine user focus still restores DOM.
            _ = ClearFocusEventSuppressAsync();
        }
    }

    private async Task ClearFocusEventSuppressAsync()
    {
        try
        {
            await Task.Delay(200);
        }
        finally
        {
            Interlocked.Decrement(ref _suppressFocusEvents);
        }
    }

    private async Task InvokeWebPosFocusPrimaryAsync()
    {
        Microsoft.UI.Xaml.Controls.WebView2? webView = _webView;
        if (webView?.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            TraceFocus("webPosFocusPrimary");
            // Existing intelligent helper: skips when a modal is open or another input is focused.
            await webView.CoreWebView2.ExecuteScriptAsync(
                "typeof webPosFocusPrimary==='function'&&webPosFocusPrimary()");
        }
        catch
        {
            /* ignore transient WebView script failures during resize */
        }
    }

    [Conditional("DEBUG")]
    private static void TraceFocus(string message)
    {
        Debug.WriteLine($"[PosFocus] {message}");
    }
#endif
}
