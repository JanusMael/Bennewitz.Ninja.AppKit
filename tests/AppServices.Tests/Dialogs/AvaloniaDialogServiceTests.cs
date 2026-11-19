using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Threading;
using Bennewitz.Ninja.AppServices.Abstractions;
using Bennewitz.Ninja.AppServices.Abstractions.Dialogs;
using Bennewitz.Ninja.AppServices.AvaloniaUI;
using Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;

namespace AppServices.Tests.Dialogs;

/// <summary>
/// Each dialog method of <see cref="AvaloniaDialogService"/>, end to end through its owner seam
/// (<c>plans/00007</c>, decision 2): the dialog it shows, modally, over the window the seam finds, and the
/// answer it returns once a button closes it; with no window, its answer for none, at once, with no window
/// opened.
/// </summary>
/// <remarks>
/// <para>
/// A dialog shown with <c>ShowDialog</c> over a shown window on the headless platform is listed among
/// that window's <see cref="Window.OwnedWindows"/> and reports <see cref="Window.IsDialog"/> while it is
/// open, and the task <c>ShowDialog</c> returns completes as it closes (measured 2026-11-19). The
/// method's own continuation is posted to the dispatcher, so each test runs its jobs before it reads the
/// method's answer.
/// </para>
/// <para>
/// "No window built" is read as no window opened: a window class handler on
/// <see cref="Window.WindowOpenedEvent"/> sees every window shown, but nothing a test can read sees a
/// window constructed and never shown.
/// </para>
/// </remarks>
public sealed class AvaloniaDialogServiceTests
{
    private static HeadlessUnitTestSession Session =>
        HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

    private static DialogMessage Message()
    {
        return DialogMessage.Builder().Text("Remove ").Path(@"C:\data\settings.json").Text(" from the list?").Build();
    }

    // -----------------------------------------------------------------------
    // The owner seam
    // -----------------------------------------------------------------------

    [Fact]
    public Task The_default_owner_is_the_main_window_of_the_application_s_desktop_lifetime() => Session.Dispatch(() =>
    {
        Window main = new();
        using ClassicDesktopStyleApplicationLifetime lifetime = new() { MainWindow = main };
        Application desktop = new() { ApplicationLifetime = lifetime };

        Assert.Same(main, AvaloniaDialogService.DesktopMainWindow(desktop));
        Assert.Null(AvaloniaDialogService.DesktopMainWindow(new Application()));
        Assert.Null(AvaloniaDialogService.DesktopMainWindow(null));

        // The headless application has no lifetime, so a service with the default seam finds no owner.
        Assert.Null(new AvaloniaDialogService().FindOwner());
    }, CancellationToken.None);

    // -----------------------------------------------------------------------
    // End to end, over an owner
    // -----------------------------------------------------------------------

    [Fact]
    public Task ShowAlertAsync_shows_an_alert_over_its_owner_until_OK_is_pressed() => Session.Dispatch(() =>
    {
        (AvaloniaDialogService service, Window owner) = ServiceWithOwner();

        Task shown = service.ShowAlertAsync("Title", "Message");
        AlertDialog dialog = OnlyDialogOver<AlertDialog>(owner);
        Assert.False(shown.IsCompleted);
        Assert.Equal("Title", dialog.Title);
        Assert.Same(service.DialogAppIcon, dialog.Icon);

        DialogInput.Press(dialog.OkButton);
        Dispatcher.UIThread.RunJobs();

        Assert.True(shown.IsCompletedSuccessfully);
        Assert.Empty(owner.OwnedWindows);
    }, CancellationToken.None);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task ShowAlertAsync_with_an_icon_shows_that_icon_or_else_the_service_s_and_the_error_header(bool passIcon) =>
        Session.Dispatch(() =>
        {
            (AvaloniaDialogService service, Window owner) = ServiceWithOwner();
            WindowIcon? icon = passIcon ? DialogInput.NewIcon() : null;

            Task shown = service.ShowAlertAsync("Title", "Message", icon, isError: true);
            AlertDialog dialog = OnlyDialogOver<AlertDialog>(owner);
            Assert.False(shown.IsCompleted);
            Assert.Equal("⚠ Title", dialog.Title);
            Assert.Same(icon ?? service.DialogAppIcon, dialog.Icon);

            DialogInput.Press(dialog.OkButton);
            Dispatcher.UIThread.RunJobs();

            Assert.True(shown.IsCompletedSuccessfully);
        }, CancellationToken.None);

    [Fact]
    public Task ShowAlertAsync_of_a_message_shows_its_category_over_its_owner_until_OK_is_pressed() =>
        Session.Dispatch(() =>
        {
            (AvaloniaDialogService service, Window owner) = ServiceWithOwner();

            Task shown = service.ShowAlertAsync("Title", Message(), DialogCategory.Error);
            MessageAlertDialog dialog = OnlyDialogOver<MessageAlertDialog>(owner);
            Assert.False(shown.IsCompleted);
            Assert.Equal("⛔ Title", dialog.Title);
            Assert.Same(service.DialogAppIcon, dialog.Icon);

            DialogInput.Press(dialog.OkButton);
            Dispatcher.UIThread.RunJobs();

            Assert.True(shown.IsCompletedSuccessfully);
        }, CancellationToken.None);

    [Fact]
    public Task ShowConfirmAsync_of_a_message_answers_true_for_its_confirm_button() => Session.Dispatch(() =>
    {
        (AvaloniaDialogService service, Window owner) = ServiceWithOwner();

        Task<bool?> answer = service.ShowConfirmAsync("Title", Message(), DialogCategory.Destructive, "Delete", "Keep");
        MessageConfirmDialog dialog = OnlyDialogOver<MessageConfirmDialog>(owner);
        Assert.False(answer.IsCompleted);
        Assert.Equal("⚠ Title", dialog.Title);
        Assert.Equal("Delete", dialog.ConfirmButton.Content);
        Assert.Equal("Keep", dialog.CancelButton.Content);
        Assert.Same(service.DialogAppIcon, dialog.Icon);

        DialogInput.Press(dialog.ConfirmButton);
        Dispatcher.UIThread.RunJobs();

        Assert.True(answer.IsCompletedSuccessfully);
        Assert.True(answer.Result);
    }, CancellationToken.None);

    [Fact]
    public Task ShowConfirmAsync_answers_false_for_its_cancel_button() => Session.Dispatch(() =>
    {
        (AvaloniaDialogService service, Window owner) = ServiceWithOwner();

        Task<bool?> answer = service.ShowConfirmAsync("Title", "Message", "Delete", "Keep");
        ConfirmDialog dialog = OnlyDialogOver<ConfirmDialog>(owner);
        Assert.False(answer.IsCompleted);
        Assert.Equal("Title", dialog.Title);
        Assert.Equal("Delete", dialog.ConfirmButton.Content);
        Assert.Equal("Keep", dialog.CancelButton.Content);
        Assert.Same(service.DialogAppIcon, dialog.Icon);

        DialogInput.Press(dialog.CancelButton);
        Dispatcher.UIThread.RunJobs();

        Assert.True(answer.IsCompletedSuccessfully);
        Assert.False(answer.Result);
    }, CancellationToken.None);

    [Fact]
    public Task ShowConfirmAsync_answers_null_for_a_close_without_a_button() => Session.Dispatch(() =>
    {
        (AvaloniaDialogService service, Window owner) = ServiceWithOwner();

        Task<bool?> answer = service.ShowConfirmAsync("Title", "Message");
        ConfirmDialog dialog = OnlyDialogOver<ConfirmDialog>(owner);

        dialog.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.True(answer.IsCompletedSuccessfully);
        Assert.Null(answer.Result);
    }, CancellationToken.None);

    [Fact]
    public Task ShowInputAsync_answers_what_was_typed_trimmed() => Session.Dispatch(() =>
    {
        (AvaloniaDialogService service, Window owner) = ServiceWithOwner();

        Task<string?> answer = service.ShowInputAsync("Title", "Prompt", "Profile name");
        InputDialog dialog = OnlyDialogOver<InputDialog>(owner);
        Assert.False(answer.IsCompleted);
        Assert.Equal("Title", dialog.Title);
        Assert.Equal("Profile name", dialog.InputBox.PlaceholderText);
        Assert.Same(service.DialogAppIcon, dialog.Icon);

        DialogInput.Type(dialog, dialog.InputBox, "  New profile  ");
        DialogInput.Press(dialog.OkButton);
        Dispatcher.UIThread.RunJobs();

        Assert.True(answer.IsCompletedSuccessfully);
        Assert.Equal("New profile", answer.Result);
    }, CancellationToken.None);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task ShowInputAsync_with_an_icon_shows_that_icon_or_else_the_service_s(bool passIcon) => Session.Dispatch(() =>
    {
        (AvaloniaDialogService service, Window owner) = ServiceWithOwner();
        WindowIcon? icon = passIcon ? DialogInput.NewIcon() : null;

        Task<string?> answer = service.ShowInputAsync("Title", "Prompt", placeholder: null, icon);
        InputDialog dialog = OnlyDialogOver<InputDialog>(owner);
        Assert.Same(icon ?? service.DialogAppIcon, dialog.Icon);

        DialogInput.Press(dialog.CancelButton);
        Dispatcher.UIThread.RunJobs();

        Assert.True(answer.IsCompletedSuccessfully);
        Assert.Null(answer.Result);
    }, CancellationToken.None);

    [Fact]
    public Task ShowUnsavedChangesAsync_answers_the_button_pressed() => Session.Dispatch(() =>
    {
        (AvaloniaDialogService service, Window owner) = ServiceWithOwner();

        Task<UnsavedChangesChoice> answer = service.ShowUnsavedChangesAsync("Title", Message());
        UnsavedChangesDialog dialog = OnlyDialogOver<UnsavedChangesDialog>(owner);
        Assert.False(answer.IsCompleted);
        Assert.Equal("Title", dialog.Title);
        Assert.Same(service.DialogAppIcon, dialog.Icon);

        DialogInput.Press(dialog.DontSaveButton);
        Dispatcher.UIThread.RunJobs();

        Assert.True(answer.IsCompletedSuccessfully);
        Assert.Equal(UnsavedChangesChoice.DontSave, answer.Result);
    }, CancellationToken.None);

    // -----------------------------------------------------------------------
    // With no owner
    // -----------------------------------------------------------------------

    /// <summary>Every method that asks for the owner, called with arguments a host would pass.</summary>
    private static readonly Dictionary<string, (Func<AvaloniaDialogService, Task<object?>> Call, object? NoWindowAnswer)> Calls =
        new()
        {
            ["ShowAlertAsync(title, message)"] =
                (async s => { await s.ShowAlertAsync("Title", "Message"); return null; }, null),
            ["ShowAlertAsync(title, message, icon, isError)"] =
                (async s => { await s.ShowAlertAsync("Title", "Message", icon: null, isError: true); return null; }, null),
            ["ShowAlertAsync(title, DialogMessage, category)"] =
                (async s => { await s.ShowAlertAsync("Title", Message(), DialogCategory.Error); return null; }, null),
            ["ShowConfirmAsync(title, DialogMessage, ...)"] =
                (async s => await s.ShowConfirmAsync("Title", Message(), DialogCategory.Destructive), null),
            ["ShowConfirmAsync(title, message, ...)"] =
                (async s => await s.ShowConfirmAsync("Title", "Message"), null),
            ["ShowInputAsync(title, prompt, placeholder)"] =
                (async s => await s.ShowInputAsync("Title", "Prompt", "Placeholder"), null),
            ["ShowInputAsync(title, prompt, placeholder, icon)"] =
                (async s => await s.ShowInputAsync("Title", "Prompt", "Placeholder", icon: null), null),
            ["ShowUnsavedChangesAsync(title, DialogMessage)"] =
                (async s => await s.ShowUnsavedChangesAsync("Title", Message()), UnsavedChangesChoice.Cancel),
            ["PickFolderAsync(title)"] =
                (async s => await s.PickFolderAsync("Title"), null),
            ["PickFileAsync(title, filters)"] =
                (async s => await s.PickFileAsync("Title"), null),
            ["PickSaveFileAsync(title, defaultFileName, filters)"] =
                (async s => await s.PickSaveFileAsync("Title", "file.txt"), null),
        };

    public static TheoryData<string> Methods => new(Calls.Keys);

    [Theory]
    [MemberData(nameof(Methods))]
    public Task With_no_owner_a_method_answers_for_no_window_at_once_and_opens_no_window(string method) =>
        Session.Dispatch(() =>
        {
            int asked = 0;
            int opened = 0;
            AvaloniaDialogService service = new()
            {
                FindOwner = () =>
                {
                    asked++;
                    return null;
                },
            };
            using IDisposable watch = Window.WindowOpenedEvent.AddClassHandler(typeof(Window), (_, _) => opened++);
            (Func<AvaloniaDialogService, Task<object?>> call, object? noWindowAnswer) = Calls[method];

            Task<object?> answer = call(service);

            Assert.True(answer.IsCompletedSuccessfully, $"{method} did not answer at once.");
            Assert.Equal(noWindowAnswer, answer.Result);
            Assert.Equal(1, asked);
            Assert.Equal(0, opened);
        }, CancellationToken.None);

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>A shown window, and a service whose owner seam finds it and whose dialogs take an icon.</summary>
    private static (AvaloniaDialogService Service, Window Owner) ServiceWithOwner()
    {
        Window owner = new();
        owner.Show();
        AvaloniaDialogService service = new()
        {
            FindOwner = () => owner,
            DialogAppIcon = DialogInput.NewIcon(),
        };
        return (service, owner);
    }

    /// <summary>The one window <paramref name="owner"/> owns, shown over it as a dialog, of the type given.</summary>
    private static T OnlyDialogOver<T>(Window owner)
        where T : Window
    {
        T dialog = Assert.IsType<T>(Assert.Single(owner.OwnedWindows));
        Assert.True(dialog.IsDialog, $"{typeof(T).Name} is owned but was not shown as a dialog.");
        Assert.Same(owner, dialog.Owner);
        return dialog;
    }
}
