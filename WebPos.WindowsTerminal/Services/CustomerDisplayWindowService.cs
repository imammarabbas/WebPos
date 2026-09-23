namespace WebPos.WindowsTerminal.Services;

/// <summary>
/// Opens/focuses a real MAUI secondary window for the customer-facing display.
/// </summary>
public sealed class CustomerDisplayWindowService
{
    private readonly object _gate = new();
    private Window? _window;

    public void OpenOrFocus()
    {
        MainThread.BeginInvokeOnMainThread(OpenOrFocusCore);
    }

    private void OpenOrFocusCore()
    {
        Application? app = Application.Current;
        if (app is null)
        {
            return;
        }

        Window? existing;
        lock (_gate)
        {
            existing = _window;
        }

        if (existing is not null)
        {
            ActivateWindow(existing);
            return;
        }

        var page = new CustomerDisplayPage();
        var window = new Window(page)
        {
            Title = "Customer Display — Smart POS",
            Width = 960,
            Height = 720
        };

        window.Destroying += OnWindowDestroying;

        lock (_gate)
        {
            if (_window is not null)
            {
                // Another open raced in — focus that one and discard this.
                ActivateWindow(_window);
                return;
            }

            _window = window;
        }

        app.OpenWindow(window);
        ActivateWindow(window);
    }

    private void OnWindowDestroying(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            if (sender is Window window)
            {
                window.Destroying -= OnWindowDestroying;
                if (ReferenceEquals(_window, window))
                {
                    _window = null;
                }
            }
        }
    }

    private static void ActivateWindow(Window window)
    {
        try
        {
#if WINDOWS
            if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native)
            {
                native.Activate();
            }
#endif
        }
        catch
        {
            /* ignore until platform handler is ready */
        }
    }
}
