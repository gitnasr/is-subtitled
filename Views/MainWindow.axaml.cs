using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using IsSubtitled.ViewModels;

namespace IsSubtitled.Views;

public partial class MainWindow : Window, IUiServices
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
                vm.AttachUi(this);
        };
    }

    public async Task<string?> PickFolderAsync(string? startIn)
    {
        IStorageFolder? start = null;
        if (!string.IsNullOrEmpty(startIn))
            start = await StorageProvider.TryGetFolderFromPathAsync(startIn);

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select folder to scan",
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickSaveFileAsync(string suggestedName)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save results",
            SuggestedFileName = suggestedName,
            DefaultExtension = "txt",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Text file") { Patterns = new[] { "*.txt" } },
            },
        });

        return file?.TryGetLocalPath();
    }

    public async Task SetClipboardAsync(string text)
    {
        IClipboard? clipboard = Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(text);
    }
}
