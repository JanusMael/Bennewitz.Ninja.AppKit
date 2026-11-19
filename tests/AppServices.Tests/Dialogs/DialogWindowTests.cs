using System.Reflection;
using Avalonia.Headless;
using Avalonia.Input;
using Bennewitz.Ninja.AppServices.Abstractions;
using Bennewitz.Ninja.AppServices.Abstractions.Dialogs;
using Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;

namespace AppServices.Tests.Dialogs;

/// <summary>
/// Each of the six dialogs <c>AvaloniaDialogService</c> shows answers as it did when its method built it
/// (<c>plans/00007</c>, step 2): for each button, for Enter and Escape where they are wired, and for a
/// close without a button.
/// </summary>
/// <remarks>
/// Each dialog is built and shown on its own, with no owner and no lifetime. A button is pressed through
/// its automation peer, as UI Automation presses it, and a key through the headless platform's keyboard;
/// <see cref="DialogInput"/> says why no theme is needed for either. A close without a button is
/// <c>Close()</c>, which the window's own close button calls.
/// </remarks>
public sealed class DialogWindowTests
{
    private static HeadlessUnitTestSession Session =>
        HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

    /// <summary>A message holding a path, as the dialogs that take a message are shown one.</summary>
    private static DialogMessage Message()
    {
        return DialogMessage.Builder().Text("Remove ").Path(@"C:\data\settings.json").Text(" from the list?").Build();
    }

    // -----------------------------------------------------------------------
    // The two alerts
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task An_alert_closes_when_OK_is_pressed(bool isError) => Session.Dispatch(() =>
    {
        AlertDialog dialog = new("Title", "Message", icon: null, isError);
        CloseCount closes = CloseCount.Of(dialog);
        dialog.Show();

        DialogInput.Press(dialog.OkButton);

        Assert.Equal(1, closes.Count);
    }, CancellationToken.None);

    [Theory]
    [InlineData(DialogCategory.Information)]
    [InlineData(DialogCategory.Confirmation)]
    [InlineData(DialogCategory.Destructive)]
    [InlineData(DialogCategory.Error)]
    [InlineData(DialogCategory.Input)]
    public Task A_message_alert_closes_when_OK_is_pressed(DialogCategory category) => Session.Dispatch(() =>
    {
        MessageAlertDialog dialog = new("Title", Message(), category, icon: null);
        CloseCount closes = CloseCount.Of(dialog);
        dialog.Show();

        DialogInput.Press(dialog.OkButton);

        Assert.Equal(1, closes.Count);
    }, CancellationToken.None);

    // -----------------------------------------------------------------------
    // The confirmation of a string
    // -----------------------------------------------------------------------

    [Fact]
    public Task A_confirmation_answers_true_for_its_confirm_button() => Session.Dispatch(() =>
    {
        ConfirmDialog dialog = new("Title", "Message", "Delete", "Keep", icon: null);
        CloseCount closes = CloseCount.Of(dialog);
        dialog.Show();

        DialogInput.Press(dialog.ConfirmButton);

        Assert.Equal(1, closes.Count);
        Assert.True(dialog.Result);
    }, CancellationToken.None);

    [Fact]
    public Task A_confirmation_answers_false_for_its_cancel_button() => Session.Dispatch(() =>
    {
        ConfirmDialog dialog = new("Title", "Message", "Delete", "Keep", icon: null);
        CloseCount closes = CloseCount.Of(dialog);
        dialog.Show();

        DialogInput.Press(dialog.CancelButton);

        Assert.Equal(1, closes.Count);
        Assert.False(dialog.Result);
    }, CancellationToken.None);

    [Fact]
    public Task A_confirmation_answers_false_for_Escape() => Session.Dispatch(() =>
    {
        ConfirmDialog dialog = new("Title", "Message", "Delete", "Keep", icon: null);
        CloseCount closes = CloseCount.Of(dialog);
        dialog.Show();

        DialogInput.PressKey(dialog, PhysicalKey.Escape);

        Assert.Equal(1, closes.Count);
        Assert.False(dialog.Result);
    }, CancellationToken.None);

    [Fact]
    public Task Enter_does_not_answer_a_confirmation() => Session.Dispatch(() =>
    {
        ConfirmDialog dialog = new("Title", "Message", "Delete", "Keep", icon: null);
        CloseCount closes = CloseCount.Of(dialog);
        dialog.Show();

        DialogInput.PressKey(dialog, PhysicalKey.Enter);

        Assert.Equal(0, closes.Count);
        Assert.Null(dialog.Result);

        // The keys reach the window: Escape still answers it.
        DialogInput.PressKey(dialog, PhysicalKey.Escape);
        Assert.Equal(1, closes.Count);
        Assert.False(dialog.Result);
    }, CancellationToken.None);

    [Fact]
    public Task A_confirmation_closed_without_a_button_answers_null() => Session.Dispatch(() =>
    {
        ConfirmDialog dialog = new("Title", "Message", "Delete", "Keep", icon: null);
        CloseCount closes = CloseCount.Of(dialog);
        dialog.Show();

        dialog.Close();

        Assert.Equal(1, closes.Count);
        Assert.Null(dialog.Result);
    }, CancellationToken.None);

    // -----------------------------------------------------------------------
    // The confirmation of a DialogMessage
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(DialogCategory.Information)]
    [InlineData(DialogCategory.Confirmation)]
    [InlineData(DialogCategory.Destructive)]
    [InlineData(DialogCategory.Error)]
    [InlineData(DialogCategory.Input)]
    public Task A_message_confirmation_answers_true_for_its_confirm_button(DialogCategory category) =>
        Session.Dispatch(() =>
        {
            MessageConfirmDialog dialog = new("Title", Message(), category, "Delete", "Keep", icon: null);
            CloseCount closes = CloseCount.Of(dialog);
            dialog.Show();

            DialogInput.Press(dialog.ConfirmButton);

            Assert.Equal(1, closes.Count);
            Assert.True(dialog.Result);
        }, CancellationToken.None);

    [Theory]
    [InlineData(DialogCategory.Information)]
    [InlineData(DialogCategory.Confirmation)]
    [InlineData(DialogCategory.Destructive)]
    [InlineData(DialogCategory.Error)]
    [InlineData(DialogCategory.Input)]
    public Task A_message_confirmation_answers_false_for_its_cancel_button(DialogCategory category) =>
        Session.Dispatch(() =>
        {
            MessageConfirmDialog dialog = new("Title", Message(), category, "Delete", "Keep", icon: null);
            CloseCount closes = CloseCount.Of(dialog);
            dialog.Show();

            DialogInput.Press(dialog.CancelButton);

            Assert.Equal(1, closes.Count);
            Assert.False(dialog.Result);
        }, CancellationToken.None);

    [Theory]
    [InlineData(DialogCategory.Information)]
    [InlineData(DialogCategory.Confirmation)]
    [InlineData(DialogCategory.Destructive)]
    [InlineData(DialogCategory.Error)]
    [InlineData(DialogCategory.Input)]
    public Task A_message_confirmation_answers_false_for_Escape(DialogCategory category) =>
        Session.Dispatch(() =>
        {
            MessageConfirmDialog dialog = new("Title", Message(), category, "Delete", "Keep", icon: null);
            CloseCount closes = CloseCount.Of(dialog);
            dialog.Show();

            DialogInput.PressKey(dialog, PhysicalKey.Escape);

            Assert.Equal(1, closes.Count);
            Assert.False(dialog.Result);
        }, CancellationToken.None);

    [Theory]
    [InlineData(DialogCategory.Information)]
    [InlineData(DialogCategory.Confirmation)]
    [InlineData(DialogCategory.Error)]
    [InlineData(DialogCategory.Input)]
    public Task Enter_confirms_a_message_confirmation_that_is_not_destructive(DialogCategory category) =>
        Session.Dispatch(() =>
        {
            MessageConfirmDialog dialog = new("Title", Message(), category, "Delete", "Keep", icon: null);
            CloseCount closes = CloseCount.Of(dialog);
            dialog.Show();

            DialogInput.PressKey(dialog, PhysicalKey.Enter);

            Assert.Equal(1, closes.Count);
            Assert.True(dialog.Result);
        }, CancellationToken.None);

    [Fact]
    public Task Enter_does_not_answer_a_destructive_confirmation() => Session.Dispatch(() =>
    {
        MessageConfirmDialog dialog = new("Title", Message(), DialogCategory.Destructive, "Delete", "Keep", icon: null);
        CloseCount closes = CloseCount.Of(dialog);
        dialog.Show();

        DialogInput.PressKey(dialog, PhysicalKey.Enter);

        Assert.Equal(0, closes.Count);
        Assert.Null(dialog.Result);

        // The keys reach the window: Escape still answers it.
        DialogInput.PressKey(dialog, PhysicalKey.Escape);
        Assert.Equal(1, closes.Count);
        Assert.False(dialog.Result);
    }, CancellationToken.None);

    [Fact]
    public Task A_message_confirmation_closed_without_a_button_answers_null() => Session.Dispatch(() =>
    {
        MessageConfirmDialog dialog = new("Title", Message(), DialogCategory.Confirmation, "Delete", "Keep", icon: null);
        CloseCount closes = CloseCount.Of(dialog);
        dialog.Show();

        dialog.Close();

        Assert.Equal(1, closes.Count);
        Assert.Null(dialog.Result);
    }, CancellationToken.None);

    // -----------------------------------------------------------------------
    // The input
    // -----------------------------------------------------------------------

    [Fact]
    public Task The_input_answers_what_was_typed_trimmed_when_OK_is_pressed() => Session.Dispatch(() =>
    {
        InputDialog dialog = new("Title", "Prompt", placeholder: null, icon: null);
        CloseCount closes = CloseCount.Of(dialog);
        dialog.Show();
        DialogInput.Type(dialog, dialog.InputBox, "  New profile  ");

        DialogInput.Press(dialog.OkButton);

        Assert.Equal(1, closes.Count);
        Assert.Equal("New profile", dialog.Result);
    }, CancellationToken.None);

    [Fact]
    public Task The_input_answers_what_was_typed_trimmed_when_Enter_is_pressed() => Session.Dispatch(() =>
    {
        InputDialog dialog = new("Title", "Prompt", placeholder: null, icon: null);
        CloseCount closes = CloseCount.Of(dialog);
        dialog.Show();
        DialogInput.Type(dialog, dialog.InputBox, "  New profile  ");

        DialogInput.PressKey(dialog, PhysicalKey.Enter);

        Assert.Equal(1, closes.Count);
        Assert.Equal("New profile", dialog.Result);
    }, CancellationToken.None);

    [Theory]
    [InlineData(PhysicalKey.Enter)]
    [InlineData(PhysicalKey.Escape)]
    public Task With_nothing_focused_Enter_and_Escape_close_the_input_with_no_answer(PhysicalKey key) =>
        Session.Dispatch(() =>
        {
            InputDialog dialog = new("Title", "Prompt", placeholder: null, icon: null);
            CloseCount closes = CloseCount.Of(dialog);
            dialog.Show();
            Assert.Null(dialog.FocusManager?.GetFocusedElement());

            DialogInput.PressKey(dialog, key);

            Assert.Equal(1, closes.Count);
            Assert.Null(dialog.Result);
        }, CancellationToken.None);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" \t ")]
    public Task A_blank_input_answers_null(string typed) => Session.Dispatch(() =>
    {
        InputDialog dialog = new("Title", "Prompt", placeholder: null, icon: null);
        CloseCount closes = CloseCount.Of(dialog);
        dialog.Show();
        DialogInput.Type(dialog, dialog.InputBox, typed);

        DialogInput.Press(dialog.OkButton);

        Assert.Equal(1, closes.Count);
        Assert.Null(dialog.Result);
    }, CancellationToken.None);

    [Fact]
    public Task The_input_answers_null_for_its_cancel_button_whatever_was_typed() => Session.Dispatch(() =>
    {
        InputDialog dialog = new("Title", "Prompt", placeholder: null, icon: null);
        CloseCount closes = CloseCount.Of(dialog);
        dialog.Show();
        DialogInput.Type(dialog, dialog.InputBox, "New profile");

        DialogInput.Press(dialog.CancelButton);

        Assert.Equal(1, closes.Count);
        Assert.Null(dialog.Result);
    }, CancellationToken.None);

    [Fact]
    public Task The_input_answers_null_for_Escape_whatever_was_typed() => Session.Dispatch(() =>
    {
        InputDialog dialog = new("Title", "Prompt", placeholder: null, icon: null);
        CloseCount closes = CloseCount.Of(dialog);
        dialog.Show();
        DialogInput.Type(dialog, dialog.InputBox, "New profile");

        DialogInput.PressKey(dialog, PhysicalKey.Escape);

        Assert.Equal(1, closes.Count);
        Assert.Null(dialog.Result);
    }, CancellationToken.None);

    [Fact]
    public Task The_input_closed_without_a_button_answers_null_whatever_was_typed() => Session.Dispatch(() =>
    {
        InputDialog dialog = new("Title", "Prompt", placeholder: null, icon: null);
        CloseCount closes = CloseCount.Of(dialog);
        dialog.Show();
        DialogInput.Type(dialog, dialog.InputBox, "New profile");

        dialog.Close();

        Assert.Equal(1, closes.Count);
        Assert.Null(dialog.Result);
    }, CancellationToken.None);

    // -----------------------------------------------------------------------
    // The unsaved-changes prompt
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(nameof(UnsavedChangesDialog.SaveButton), UnsavedChangesChoice.Save)]
    [InlineData(nameof(UnsavedChangesDialog.DontSaveButton), UnsavedChangesChoice.DontSave)]
    [InlineData(nameof(UnsavedChangesDialog.CancelButton), UnsavedChangesChoice.Cancel)]
    public Task The_unsaved_changes_prompt_answers_the_button_pressed(string button, UnsavedChangesChoice expected) =>
        Session.Dispatch(() =>
        {
            UnsavedChangesDialog dialog = new("Title", Message(), icon: null);
            CloseCount closes = CloseCount.Of(dialog);
            dialog.Show();

            DialogInput.Press(button switch
            {
                nameof(UnsavedChangesDialog.SaveButton) => dialog.SaveButton,
                nameof(UnsavedChangesDialog.DontSaveButton) => dialog.DontSaveButton,
                _ => dialog.CancelButton,
            });

            Assert.Equal(1, closes.Count);
            Assert.Equal(expected, dialog.Result);
        }, CancellationToken.None);

    [Fact]
    public Task Enter_answers_the_unsaved_changes_prompt_with_Save() => Session.Dispatch(() =>
    {
        UnsavedChangesDialog dialog = new("Title", Message(), icon: null);
        CloseCount closes = CloseCount.Of(dialog);
        dialog.Show();

        DialogInput.PressKey(dialog, PhysicalKey.Enter);

        Assert.Equal(1, closes.Count);
        Assert.Equal(UnsavedChangesChoice.Save, dialog.Result);
    }, CancellationToken.None);

    [Fact]
    public Task Escape_answers_the_unsaved_changes_prompt_with_Cancel() => Session.Dispatch(() =>
    {
        UnsavedChangesDialog dialog = new("Title", Message(), icon: null);
        CloseCount closes = CloseCount.Of(dialog);
        dialog.Show();

        DialogInput.PressKey(dialog, PhysicalKey.Escape);

        Assert.Equal(1, closes.Count);
        Assert.Equal(UnsavedChangesChoice.Cancel, dialog.Result);
    }, CancellationToken.None);

    [Fact]
    public Task The_unsaved_changes_prompt_closed_without_a_button_answers_Cancel() => Session.Dispatch(() =>
    {
        UnsavedChangesDialog dialog = new("Title", Message(), icon: null);
        CloseCount closes = CloseCount.Of(dialog);
        dialog.Show();

        dialog.Close();

        Assert.Equal(1, closes.Count);
        Assert.Equal(UnsavedChangesChoice.Cancel, dialog.Result);
    }, CancellationToken.None);
}
