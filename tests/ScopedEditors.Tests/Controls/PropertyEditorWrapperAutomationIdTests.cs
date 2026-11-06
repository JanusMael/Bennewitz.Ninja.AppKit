using System.Reflection;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Controls;

namespace Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests.Controls;

/// <summary>
/// The automation ids <c>PropertyEditorWrapper.axaml</c> declares resolve, on a rendered row, to the
/// property's schema path and the control's role: the strings a test or an agent searches by.
/// </summary>
/// <remarks>
/// <para>
/// ⚠ <b>The markup rule cannot see this.</b> <c>AxamlAutomationIdCoverageTests</c>, XamlQuality's
/// <c>BNXQ1007</c>, shows that each control declares an id, bound or not. A binding that resolves to
/// nothing, or to the same string on every row, passes it. So a row is built for real and its ids
/// are read back.
/// </para>
/// <para>
/// ⭐ The array case exercises the one id that is not a plain binding: an item's remove button takes
/// the row's path from the array's view-model, one <c>ItemsControl</c> up, and the item from its own
/// context, through a <c>MultiBinding</c>.
/// </para>
/// </remarks>
public sealed class PropertyEditorWrapperAutomationIdTests
{
    private static HeadlessUnitTestSession Session =>
        HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

    [Fact]
    public Task A_value_input_takes_the_path_and_the_reset_button_adds_its_role()
    {
        return Session.Dispatch(() =>
        {
            StringPropertyEditorViewModel vm = new(new FakeEditorSchema("settings.name"), FakeEditorScope.User);

            string[] ids = IdsOnARow(vm);

            MessageAssert.Equal(
                ["settings.name", "settings.name#reset"],
                ids,
                $"A string row declared these automation ids: {string.Join(", ", ids)}. Its text box "
                + "should take the property's path, and its reset button the path and #reset.");
        }, CancellationToken.None);
    }

    [Fact]
    public Task An_array_item_s_remove_button_takes_the_path_and_the_item()
    {
        return Session.Dispatch(() =>
        {
            StringArrayPropertyEditorViewModel vm =
                new(new FakeEditorSchema("settings.tags", EditorValueType.StringArray), FakeEditorScope.User);
            vm.LoadFromValue(
                new FakeEditorValue("settings.tags").With(FakeEditorScope.User, new object?[] { "a", "b" }),
                FakeEditorScope.User);

            string[] ids = IdsOnARow(vm);

            MessageAssert.Equal(
                [
                    "settings.tags#add",
                    "settings.tags#new-item",
                    "settings.tags#remove:a",
                    "settings.tags#remove:b",
                    "settings.tags#reset",
                ],
                ids,
                $"An array row with the items a and b declared these automation ids: {string.Join(", ", ids)}. "
                + "Each remove button should take the path, #remove: and its item; a blank or repeated "
                + "one means the MultiBinding no longer reaches the array's view-model.");
        }, CancellationToken.None);
    }

    /// <summary>
    /// Renders one row for <paramref name="vm"/> and returns every non-blank automation id on it, in
    /// ordinal order. A hidden control still counts: its id is declared whether or not it is shown.
    /// </summary>
    private static string[] IdsOnARow(PropertyEditorViewModel vm)
    {
        PropertyEditorWrapper row = new() { DataContext = vm };
        Window window = new() { Width = 800, Height = 400, Content = row };
        window.Show();

        try
        {
            return
            [
                .. row.GetVisualDescendants()
                    .OfType<Control>()
                    .Select(c => AutomationProperties.GetAutomationId(c))
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id!)
                    .Order(StringComparer.Ordinal),
            ];
        }
        finally
        {
            window.Close();
        }
    }
}
