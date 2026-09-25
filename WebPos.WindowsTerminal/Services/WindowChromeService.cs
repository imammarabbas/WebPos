#if WINDOWS
using Microsoft.UI.Windowing;
#endif

namespace WebPos.WindowsTerminal.Services;

/// <summary>
/// Toggles native MAUI/WinUI windows into OS fullscreen (F11).
/// Main POS and customer display are tracked separately so one
/// window going fullscreen never rebinds or restyles the other.
/// </summary>
public sealed class WindowChromeService
{
#if WINDOWS
    private readonly object _gate = new();
    private AppWindow? _mainWindow;
    private AppWindow? _customerWindow;
#endif

    public bool HasMain
    {
        get
        {
#if WINDOWS
            lock (_gate)
            {
                return _mainWindow is not null;
            }
#else
            return false;
#endif
        }
    }

    public bool IsMainFullscreen
    {
        get
        {
#if WINDOWS
            lock (_gate)
            {
                return IsFullscreenPresenter(_mainWindow);
            }
#else
            return false;
#endif
        }
    }

    public bool IsCustomerFullscreen
    {
        get
        {
#if WINDOWS
            lock (_gate)
            {
                return IsFullscreenPresenter(_customerWindow);
            }
#else
            return false;
#endif
        }
    }

#if WINDOWS
    /// <summary>Bind the first POS shell window only; never overwrite with a later window.</summary>
    public bool TryAttachMain(AppWindow appWindow)
    {
        ArgumentNullException.ThrowIfNull(appWindow);

        lock (_gate)
        {
            if (_mainWindow is not null)
            {
                return false;
            }

            _mainWindow = appWindow;
            return true;
        }
    }

    public void AttachCustomer(AppWindow appWindow)
    {
        ArgumentNullException.ThrowIfNull(appWindow);

        lock (_gate)
        {
            // Never let the customer display steal the main window slot.
            if (ReferenceEquals(appWindow, _mainWindow))
            {
                return;
            }

            _customerWindow = appWindow;
        }
    }

    public void DetachCustomer()
    {
        lock (_gate)
        {
            _customerWindow = null;
        }
    }

    private static bool IsFullscreenPresenter(AppWindow? appWindow) =>
        appWindow?.Presenter?.Kind == AppWindowPresenterKind.FullScreen;

    private static bool Toggle(AppWindow? appWindow)
    {
        if (appWindow is null)
        {
            return false;
        }

        if (appWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen)
        {
            // Restore the window's own overlapped state (do not touch the sibling window).
            appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
        }
        else
        {
            appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }

        return IsFullscreenPresenter(appWindow);
    }
#endif

    /// <summary>Toggle the main POS window. Kept for existing F11 bridge.</summary>
    public bool ToggleFullscreen() => ToggleMainFullscreen();

    public bool IsFullscreen => IsMainFullscreen;

    public bool ToggleMainFullscreen()
    {
#if WINDOWS
        AppWindow? target;
        lock (_gate)
        {
            target = _mainWindow;
        }

        return Toggle(target);
#else
        return false;
#endif
    }

    public bool ToggleCustomerFullscreen()
    {
#if WINDOWS
        AppWindow? target;
        lock (_gate)
        {
            target = _customerWindow;
        }

        return Toggle(target);
#else
        return false;
#endif
    }
}
