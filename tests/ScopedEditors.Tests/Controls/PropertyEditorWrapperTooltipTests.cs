using System.Reflection;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Controls;

namespace Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests.Controls;

/// <summary>
/// The value input each editor's row draws shows the property's description as its tooltip, or the
/// property's display name when it has none, and shows it on a locked row too (<c>plans/00005</c>,
/// decision 3).
/// </summary>
/// <remarks>
/// <para>
/// ⚠ <b>The markup rule cannot see this.</b> <c>AxamlTooltipCoverageTests</c>, XamlQuality's
/// <c>BNXQ1011</c>, shows that each input declares a tooltip, and a tip bound to a description that is
/// missing is declared and renders nothing. So each row is built for real, with a description, with
/// none and with a blank one, editable and locked, and its value input's tip is read back.
/// </para>
/// <para>
/// ⚠ <b>A locked row disables its editor</b>, and a disabled control shows its tooltip only with
/// <c>ToolTip.ShowOnDisabled</c>. The input's being disabled on the locked row is asserted too, so the
/// flag is read where it matters.
/// </para>
/// <para>
/// Every control whose automation id is the property's path is read, the hidden one included: the
/// enum's combo box and auto-complete box share the path, and only one of them is shown.
/// </para>
/// </remarks>
public sealed class PropertyEditorWrapperTooltipTests
{
    private const string Path = "settings.cache";
    private const string Title = "Cache folder";
    private const string Described = "Where the cache lives between runs.";

    private static HeadlessUnitTestSession Session =>
        HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

    [Theory]
    [InlineData("Boolean")]
    [InlineData("String")]
    [InlineData("Path")]
    [InlineData("StrictEnum")]
    [InlineData("FreeFormEnum")]
    [InlineData("Number")]
    public Task A_value_input_s_tooltip_is_the_description_or_else_the_name_and_shows_on_a_locked_row(string editor)
    {
        return Session.Dispatch(() =>
        {
            bool[] lockStates = [false, true];
            string?[] descriptions = [Described, null, "  "];
            List<string> failures = [];

            foreach (bool locked in lockStates)
            {
                foreach (string? description in descriptions)
                {
                    string expected = string.IsNullOrWhiteSpace(description) ? Title : description;
                    string row = $"{editor} row, {(locked ? "locked" : "editable")}, "
                                 + (description is null ? "no description" : $"description \"{description}\"");

                    failures.AddRange(ReadValueInputs(editor, description, locked, expected, row));
                }
            }

            Assert.True(
                failures.Count == 0,
                $"The {editor} editor's value input does not show the property's description, or its name "
                + "when it has none, on every row:\n  " + string.Join("\n  ", failures)
                + "\n\nIn PropertyEditorWrapper.axaml the input takes ToolTip.Tip as a MultiBinding of "
                + "Description and then DisplayName through FirstNonBlankConverter, and "
                + "ToolTip.ShowOnDisabled=\"True\".");
        }, CancellationToken.None);
    }

    /// <summary>
    /// Renders one row and returns what is wrong with its value inputs' tooltips, or nothing.
    /// </summary>
    private static IEnumerable<string> ReadValueInputs(
        string editor, string? description, bool locked, string expected, string row)
    {
        PropertyEditorViewModel vm = Editor(editor, description, locked);
        PropertyEditorWrapper wrapper = new() { DataContext = vm };
        Window window = new() { Width = 800, Height = 400, Content = wrapper };
        window.Show();

        try
        {
            Control[] inputs =
            [
                .. wrapper.GetVisualDescendants()
                    .OfType<Control>()
                    .Where(c => string.Equals(AutomationProperties.GetAutomationId(c), Path, StringComparison.Ordinal)),
            ];

            Type shown = InputType(editor);
            List<string> failures = [];

            if (!inputs.Any(c => c.GetType() == shown && c.IsEffectivelyVisible))
            {
                failures.Add(
                    $"{row}: no visible {shown.Name} has the automation id {Path}; found "
                    + (inputs.Length == 0 ? "nothing" : string.Join(", ", inputs.Select(c => c.GetType().Name))));
            }

            foreach (Control input in inputs)
            {
                string control = $"{row}: the {input.GetType().Name}";
                object? tip = ToolTip.GetTip(input);

                if (tip is not string text || !string.Equals(text, expected, StringComparison.Ordinal))
                {
                    failures.Add($"{control} shows {Quote(tip)} where \"{expected}\" is meant");
                }

                if (!ToolTip.GetShowOnDisabled(input))
                {
                    failures.Add($"{control} does not set ToolTip.ShowOnDisabled, so a locked row hides its tip");
                }

                if (input.IsEffectivelyEnabled == locked)
                {
                    failures.Add(
                        $"{control} is {(input.IsEffectivelyEnabled ? "enabled" : "disabled")}, so this row "
                        + "does not test what it claims to");
                }
            }

            return failures;
        }
        finally
        {
            window.Close();
        }
    }

    private static PropertyEditorViewModel Editor(string editor, string? description, bool locked)
    {
        FakeEditorSchema schema = new(Path, ValueType(editor))
        {
            Title = Title,
            Description = description,
            IsReadOnly = locked,
            EnumValues = editor is "StrictEnum" or "FreeFormEnum" ? ["fast", "safe"] : null,
            Examples = editor is "FreeFormEnum" ? ["fast"] : [],
        };

        return editor switch
        {
            "Boolean" => new BooleanPropertyEditorViewModel(schema, FakeEditorScope.User),
            "String" => new StringPropertyEditorViewModel(schema, FakeEditorScope.User),
            "Path" => new PathPropertyEditorViewModel(schema, FakeEditorScope.User),
            "StrictEnum" or "FreeFormEnum" => new EnumPropertyEditorViewModel(schema, FakeEditorScope.User),
            "Number" => new NumberPropertyEditorViewModel(schema, FakeEditorScope.User),
            var _ => throw new ArgumentOutOfRangeException(nameof(editor), editor, "No such editor here."),
        };
    }

    private static EditorValueType ValueType(string editor) => editor switch
    {
        "Boolean" => EditorValueType.Boolean,
        "String" => EditorValueType.String,
        "Path" => EditorValueType.Path,
        "StrictEnum" or "FreeFormEnum" => EditorValueType.Enum,
        "Number" => EditorValueType.Number,
        var _ => throw new ArgumentOutOfRangeException(nameof(editor), editor, "No such editor here."),
    };

    private static Type InputType(string editor) => editor switch
    {
        "Boolean" => typeof(CheckBox),
        "String" or "Path" => typeof(TextBox),
        "StrictEnum" => typeof(ComboBox),
        "FreeFormEnum" => typeof(AutoCompleteBox),
        "Number" => typeof(NumericUpDown),
        var _ => throw new ArgumentOutOfRangeException(nameof(editor), editor, "No such editor here."),
    };

    private static string Quote(object? tip) => tip switch
    {
        null => "no tooltip",
        string s => $"\"{s}\"",
        var other => $"a {other.GetType().Name}",
    };
}
