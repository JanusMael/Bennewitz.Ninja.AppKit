using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Bennewitz.Ninja.AppServices.Abstractions.Dialogs;
using Bennewitz.Ninja.AppServices.Abstractions;
using Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;

namespace Bennewitz.Ninja.AppServices.AvaloniaUI;

/// <summary>
/// Avalonia implementation of <see cref="IDialogService"/>.
/// </summary>
/// <remarks>
/// Call <see cref="RegisterSaveChangesDialog"/> once at application startup to wire up the
/// host-specific Save Changes dialog.  All other dialog methods are self-contained.
/// </remarks>
public sealed class AvaloniaDialogService : IDialogService
{
    // Factory registered by the host application so this service never depends on
    // application-specific ViewModels or Views.  The factory receives the prompt and the
    // current main window (may be null when no window is open) and is responsible for
    // creating and showing the concrete dialog.
    private Func<ISaveChangesPrompt, Window?, Task<bool>>? _saveChangesFactory;

    /// <summary>
    /// Optional application icon applied to all programmatic dialogs (alert, input,
    /// confirm).  Set once at startup from the host application's icon so every dialog
    /// shows the correct task-bar / title-bar icon without requiring callers to pass it
    /// on each call.
    /// </summary>
    public WindowIcon? DialogAppIcon { get; set; }

    /// <summary>
    /// Registers the factory that will be called by <see cref="ShowSaveChangesDialogAsync"/>.
    /// Call this once during application startup before any dialogs can be shown.
    /// </summary>
    /// <param name="factory">
    /// A delegate that receives the <see cref="ISaveChangesPrompt"/> and the nullable owner
    /// <see cref="Window"/>, shows the host-specific dialog, and returns <c>true</c> if the
    /// user chose Save.
    /// </param>
    public void RegisterSaveChangesDialog(Func<ISaveChangesPrompt, Window?, Task<bool>> factory)
    {
        _saveChangesFactory = factory;
    }

    // -----------------------------------------------------------------------
    // Window access
    // -----------------------------------------------------------------------

    /// <summary>
    /// Finds the window a dialog or picker is shown over, asked each time one is wanted; when it finds
    /// none, the method returns its answer for no window and builds nothing. A seam for tests
    /// (<c>plans/00007</c>, decision 2): <see cref="DesktopMainWindow"/> of the running application,
    /// unless a test replaces it.
    /// </summary>
    internal Func<Window?> FindOwner { get; set; } = static () => DesktopMainWindow(Application.Current);

    /// <summary>
    /// The main window of <paramref name="application"/>'s desktop lifetime, or <see langword="null"/>
    /// when there is no application, it has no desktop lifetime, or its lifetime has no main window.
    /// </summary>
    internal static Window? DesktopMainWindow(Application? application) =>
        (application?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    private Window? Window => FindOwner();

    // -----------------------------------------------------------------------
    // IDialogService — file / folder pickers
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<string?> PickFolderAsync(string? title = null)
    {
        Window? window = Window;
        if (window == null)
        {
            return null;
        }

        FolderPickerOpenOptions options = new()
        {
            Title = title ?? "Select Folder",
            AllowMultiple = false,
        };

        IReadOnlyList<IStorageFolder> result = await window.StorageProvider.OpenFolderPickerAsync(options);
        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    /// <inheritdoc/>
    public async Task<string?> PickFileAsync(string? title = null, IReadOnlyList<FilePickerFilter>? filters = null)
    {
        Window? window = Window;
        if (window == null)
        {
            return null;
        }

        FilePickerOpenOptions options = new()
        {
            Title = title ?? "Select File",
            AllowMultiple = false,
            FileTypeFilter = filters?.Select(f => new FilePickerFileType(f.Name)
            {
                Patterns = f.Extensions.Select(e => $"*.{e}").ToList()
            }).ToList(),
        };

        IReadOnlyList<IStorageFile> result = await window.StorageProvider.OpenFilePickerAsync(options);
        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    /// <inheritdoc />
    public async Task<string?> PickSaveFileAsync(
        string? title,
        string defaultFileName,
        IReadOnlyList<FilePickerFilter>? filters = null)
    {
        Window? window = Window;
        if (window == null)
        {
            return null;
        }

        FilePickerSaveOptions options = new()
        {
            Title = title ?? "Save As",
            SuggestedFileName = defaultFileName,
            FileTypeChoices = filters?.Select(f => new FilePickerFileType(f.Name)
            {
                Patterns = f.Extensions.Select(e => $"*.{e}").ToList(),
            }).ToList(),
            ShowOverwritePrompt = true,
        };

        IStorageFile? result = await window.StorageProvider.SaveFilePickerAsync(options);
        return result?.TryGetLocalPath();
    }

    // -----------------------------------------------------------------------
    // IDialogService — simple dialogs
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task ShowAlertAsync(string title, string message)
    {
        await ShowAlertCoreAsync(title, message, icon: null, isError: false);
    }

    /// <summary>
    /// Extended overload that attaches an application icon and optionally renders a
    /// styled error header.  Called by ClaudeForge for the "Cannot Save" alert.
    /// </summary>
    public async Task ShowAlertAsync(string title, string message,
                                     WindowIcon? icon, bool isError = false)
    {
        await ShowAlertCoreAsync(title, message, icon, isError);
    }

    private async Task ShowAlertCoreAsync(string title, string message,
                                          WindowIcon? icon, bool isError)
    {
        Window? window = Window;
        if (window == null)
        {
            return;
        }

        AlertDialog dialog = new(title, message, icon ?? DialogAppIcon, isError);
        await dialog.ShowDialog(window);
    }

    // -----------------------------------------------------------------------
    // IDialogService — rich-message + category overloads
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public async Task ShowAlertAsync(string title, DialogMessage message,
                                     DialogCategory category = DialogCategory.Information)
    {
        Window? window = Window;
        if (window == null)
        {
            return;
        }

        MessageAlertDialog dialog = new(title, message, category, DialogAppIcon);
        await dialog.ShowDialog(window);
    }

    /// <inheritdoc />
    public async Task<bool?> ShowConfirmAsync(
        string title,
        DialogMessage message,
        DialogCategory category = DialogCategory.Confirmation,
        string confirmLabel = "Confirm",
        string cancelLabel = "Cancel")
    {
        Window? window = Window;
        if (window == null)
        {
            return null;
        }

        MessageConfirmDialog dialog = new(title, message, category, confirmLabel, cancelLabel, DialogAppIcon);
        await dialog.ShowDialog(window);
        return dialog.Result;
    }

    /// <inheritdoc />
    public async Task<string?> ShowInputAsync(string title, string prompt, string? placeholder = null)
    {
        return await ShowInputAsync(title, prompt, placeholder, icon: null);
    }

    /// <summary>
    /// Extended overload that attaches an application icon to the input dialog.
    /// Called by ClaudeForge for the "New Profile" dialog.
    /// </summary>
    public async Task<string?> ShowInputAsync(string title, string prompt,
                                              string? placeholder, WindowIcon? icon)
    {
        Window? window = Window;
        if (window == null)
        {
            return null;
        }

        InputDialog dialog = new(title, prompt, placeholder, icon ?? DialogAppIcon);
        await dialog.ShowDialog(window);
        return dialog.Result;
    }

    /// <inheritdoc />
    public async Task<bool?> ShowConfirmAsync(string title, string message,
                                              string confirmLabel = "Confirm",
                                              string cancelLabel = "Cancel")
    {
        Window? window = Window;
        if (window == null)
        {
            return null;
        }

        ConfirmDialog dialog = new(title, message, confirmLabel, cancelLabel, DialogAppIcon);
        await dialog.ShowDialog(window);
        return dialog.Result;
    }

    // -----------------------------------------------------------------------
    // IDialogService — Save Changes (host-supplied factory)
    // -----------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<bool> ShowSaveChangesDialogAsync(ISaveChangesPrompt prompt)
    {
        if (_saveChangesFactory is null)
        {
            return false;
        }

        return await _saveChangesFactory(prompt, Window);
    }

    /// <inheritdoc />
    public async Task<UnsavedChangesChoice> ShowUnsavedChangesAsync(
        string title, DialogMessage message)
    {
        Window? window = Window;
        if (window == null)
        {
            return UnsavedChangesChoice.Cancel;
        }

        UnsavedChangesDialog dialog = new(title, message, DialogAppIcon);
        await dialog.ShowDialog(window);
        return dialog.Result;
    }
}
