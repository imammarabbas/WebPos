using System.Threading.Channels;
using Microsoft.JSInterop;

namespace WebPos.WindowsTerminal.Services;

public sealed class BarcodeScannerService(IJSRuntime jsRuntime) : IBarcodeScannerService, IAsyncDisposable
{
    private readonly IJSRuntime _js = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private readonly object _gate = new();
    private readonly Channel<string> _queue = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    private DotNetObjectReference<BarcodeScannerService>? _selfRef;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private bool _initialized;
    private bool _disposed;

    public event Func<string, Task>? OnBarcodeScanned;

    public async Task InitializeAsync()
    {
        if (_disposed)
        {
            return;
        }

        bool startLoop = false;
        lock (_gate)
        {
            if (_initialized)
            {
                return;
            }

            _selfRef = DotNetObjectReference.Create(this);
            _loopCts = new CancellationTokenSource();
            _initialized = true;
            startLoop = true;
        }

        try
        {
            await _js.InvokeVoidAsync("webPosBarcodeScanner.start", _selfRef);
            if (startLoop)
            {
                CancellationToken ct = _loopCts!.Token;
                _loopTask = Task.Run(() => ProcessScanQueueAsync(ct), ct);
            }
        }
        catch
        {
            lock (_gate)
            {
                _initialized = false;
                _loopCts?.Cancel();
                _loopCts?.Dispose();
                _loopCts = null;
                _loopTask = null;
                _selfRef?.Dispose();
                _selfRef = null;
            }

            throw;
        }
    }

    /// <summary>
    /// Fast path from JS: enqueue only. Processing is sequential in <see cref="ProcessScanQueueAsync"/>.
    /// </summary>
    [JSInvokable]
    public Task OnGlobalBarcodeScanned(string code)
    {
        if (_disposed || string.IsNullOrWhiteSpace(code))
        {
            return Task.CompletedTask;
        }

        _queue.Writer.TryWrite(code.Trim());
        return Task.CompletedTask;
    }

    private async Task ProcessScanQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (string code in _queue.Reader.ReadAllAsync(cancellationToken))
            {
                Func<string, Task>? handlers = OnBarcodeScanned;
                if (handlers is not null)
                {
                    foreach (Delegate d in handlers.GetInvocationList())
                    {
                        if (d is not Func<string, Task> handler)
                        {
                            continue;
                        }

                        try
                        {
                            await handler(code).ConfigureAwait(false);
                        }
                        catch
                        {
                            // Swallow per-handler failures so one subscriber cannot break the circuit.
                        }
                    }
                }

                try
                {
                    await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _queue.Writer.TryComplete();

        CancellationTokenSource? cts;
        Task? loop;
        lock (_gate)
        {
            cts = _loopCts;
            loop = _loopTask;
            _loopCts = null;
            _loopTask = null;
        }

        try
        {
            cts?.Cancel();
        }
        catch
        {
            /* ignore */
        }

        if (loop is not null)
        {
            try
            {
                await loop.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch
            {
                /* ignore teardown timeouts */
            }
        }

        cts?.Dispose();

        try
        {
            await _js.InvokeVoidAsync("webPosBarcodeScanner.stop");
        }
        catch
        {
            /* ignore during teardown */
        }

        lock (_gate)
        {
            _selfRef?.Dispose();
            _selfRef = null;
            _initialized = false;
        }
    }
}
