using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace WebPos.WindowsTerminal.Services;

/// <summary>Saves PDF bytes via the Windows file-save picker (Documents fallback).</summary>
public sealed class StatementFileService
{
    public async Task<string?> SavePdfAsync(byte[] pdfBytes, string suggestedFileName)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (string.IsNullOrWhiteSpace(suggestedFileName))
        {
            suggestedFileName = "statement.pdf";
        }

        if (!suggestedFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            suggestedFileName += ".pdf";
        }

#if WINDOWS
        try
        {
            FileSavePicker picker = new()
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedFileName)
            };
            picker.FileTypeChoices.Add("PDF Document", [".pdf"]);

            nint hwnd = GetMainWindowHandle();
            if (hwnd != 0)
            {
                InitializeWithWindow.Initialize(picker, hwnd);
            }

            StorageFile? file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return null;
            }

            await FileIO.WriteBytesAsync(file, pdfBytes);
            return file.Path;
        }
        catch
        {
            // Fall through to Documents folder.
        }
#endif

        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "WebPosStatements");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, suggestedFileName);
        await File.WriteAllBytesAsync(path, pdfBytes).ConfigureAwait(false);
        return path;
    }

#if WINDOWS
    private static nint GetMainWindowHandle()
    {
        try
        {
            if (Microsoft.Maui.Controls.Application.Current?.Windows.Count > 0
                && Microsoft.Maui.Controls.Application.Current.Windows[0].Handler?.PlatformView
                    is Microsoft.UI.Xaml.Window native)
            {
                return WindowNative.GetWindowHandle(native);
            }
        }
        catch
        {
            // ignore
        }

        return 0;
    }
#endif
}
