namespace WebPos.WindowsTerminal.Services;

public interface IBarcodeScannerService
{
    event Func<string, Task>? OnBarcodeScanned;

    Task InitializeAsync();
}
