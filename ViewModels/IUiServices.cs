using System.Threading.Tasks;

namespace IsSubtitled.ViewModels;

/// <summary>
/// View-level operations the ViewModel needs but that require a TopLevel
/// (folder/file dialogs, clipboard). Implemented by MainWindow.
/// </summary>
public interface IUiServices
{
    Task<string?> PickFolderAsync(string? startIn);
    Task<string?> PickSaveFileAsync(string suggestedName);
    Task SetClipboardAsync(string text);
}
