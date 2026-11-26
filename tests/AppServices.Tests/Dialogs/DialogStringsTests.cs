using System.Reflection;
using System.Text;
using AppServices.Tests.Logging;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Bennewitz.Ninja.AppServices.Abstractions;
using Bennewitz.Ninja.AppServices.Abstractions.Dialogs;
using Bennewitz.Ninja.AppServices.AvaloniaUI;
using Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;
using Bennewitz.Ninja.AppServices.AvaloniaUI.Localization;
using Bennewitz.Ninja.AppServices.Dialogs;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AppServices.Tests.Dialogs;

/// <summary>
/// <see cref="DialogStrings"/> (<c>plans/00008</c>): every string the package gives, in a window and outside
/// one, is the host's once its resolver gives it, and the package's English until then; and a resolver that
/// fails never fails a window.
/// </summary>
/// <remarks>
/// <para>
/// The walk builds every window in <see cref="DialogWindows"/> with a resolver that marks every key, presses
/// each 📋, and reads every title, name, help text, tooltip, label and placeholder in it. Once the markers,
/// the strings the table passes and the package's glyphs are taken out, nothing may be left: what is left is
/// English a host cannot replace. Each window's keys are pinned in <see cref="KeysShown"/>, as its ids are in
/// the table of windows.
/// </para>
/// <para>
/// A string outside a window is held by a test of its own: the pickers' titles, read from the options each
/// picker opens with, since on the headless platform a window has no picker a test can watch; the fatal
/// dialogs' titles and message; and a confirmation's two labels when its caller passes none.
/// </para>
/// <para>
/// The resolver and the logger are static, so each test starts from the defaults and puts both back in
/// <see cref="Dispose"/>.
/// </para>
/// </remarks>
public sealed class DialogStringsTests : IDisposable
{
    /// <summary><c>plans/00008</c>'s table of strings: every key, with its default.</summary>
    private static readonly (string Key, string Default)[] Declared =
    [
        ("LabelOk", "OK"),
        ("LabelCancel", "Cancel"),
        ("LabelConfirm", "Confirm"),
        ("LabelSave", "Save"),
        ("LabelDontSave", "Don't Save"),
        ("TitleSelectFolder", "Select Folder"),
        ("TitleSelectFile", "Select File"),
        ("TitleSaveAs", "Save As"),
        ("TipCopyPath", "Copy {0}"),
        ("TipCopied", "Copied!"),
        ("TipOpenLink", "Open {0}"),
        ("LabelClose", "Close"),
        ("TipCloseWindow", "Close this window"),
        ("LabelCopyToClipboard", "Copy to Clipboard"),
        ("NameErrorDetails", "Error details"),
        ("HelpErrorDetails", "Read-only exception text; Copy to Clipboard copies all of it"),
        ("HelpCopyErrorDetails", "Copy the full error details to the clipboard"),
        ("NameNoticeDetails", "Notice details"),
        ("HelpNoticeDetails", "Read-only notice text; Copy to Clipboard copies all of it"),
        ("HelpCopyNotice", "Copy the full notice text to the clipboard"),
        ("TextFatalError", "The application encountered an unexpected error.\nYou can copy the details below for bug reporting."),
        ("TitleFatalError", "Application Error — {0}"),
        ("TitleFatalErrorNoApp", "Application Error"),
        ("TitleNativeFatalError", "{0} — Critical Error"),
        ("TipOk", "Close this message"),
        ("TipAccept", "Use the text you typed"),
        ("TipConfirm", "Do what this dialog asks, and close it"),
        ("TipCancel", "Close this dialog without doing anything"),
        ("TipSave", "Save your changes, then close"),
        ("TipDontSave", "Close without saving your changes"),
        ("TipKeepOpen", "Keep the window open, with your changes unsaved"),
        ("TipInput", "Type here; Enter accepts it and Escape cancels"),
        ("TipSelectText", "Select text here to copy it"),
        ("TipSelectCode", "Select code here to copy it"),
    ];

    /// <summary>The keys whose text carries a value as <c>{0}</c> (<c>plans/00008</c>, decision 5).</summary>
    private static readonly HashSet<string> TemplateKeys =
    [
        nameof(DialogStrings.TipCopyPath),
        nameof(DialogStrings.TipOpenLink),
        nameof(DialogStrings.TitleFatalError),
        nameof(DialogStrings.TitleNativeFatalError),
    ];

    /// <summary>The keys each window in the table shows, once each 📋 in it has been pressed.</summary>
    private static readonly Dictionary<string, string[]> KeysShown = new()
    {
        [nameof(AlertDialog)] = ["LabelOk", "TipOk", "TipSelectText"],
        [nameof(MessageAlertDialog)] =
            ["LabelOk", "TipOk", "TipSelectText", "TipSelectCode", "TipCopyPath", "TipCopied", "TipOpenLink"],
        [nameof(ConfirmDialog)] = ["TipConfirm", "TipCancel"],
        [nameof(MessageConfirmDialog)] =
            ["TipConfirm", "TipCancel", "TipSelectText", "TipSelectCode", "TipCopyPath", "TipCopied", "TipOpenLink"],
        [nameof(InputDialog)] = ["LabelOk", "LabelCancel", "TipAccept", "TipCancel", "TipInput"],
        [nameof(UnsavedChangesDialog)] =
            ["LabelSave", "LabelDontSave", "LabelCancel", "TipSave", "TipDontSave", "TipKeepOpen", "TipSelectText",
             "TipCopyPath", "TipCopied", "TipOpenLink"],
        [nameof(FatalErrorDialog)] =
            ["NameErrorDetails", "HelpErrorDetails", "LabelCopyToClipboard", "HelpCopyErrorDetails", "LabelClose",
             "TipCloseWindow"],
        [nameof(NonFatalNoticeDialog)] =
            ["NameNoticeDetails", "HelpNoticeDetails", "LabelCopyToClipboard", "HelpCopyNotice", "LabelClose",
             "TipCloseWindow"],
    };

    /// <summary>The keys read outside a window, each held by a test of its own below.</summary>
    private static readonly string[] KeysOutsideWindows =
    [
        "TitleSelectFolder", "TitleSelectFile", "TitleSaveAs", "LabelConfirm", "LabelCancel", "TextFatalError",
        "TitleFatalError", "TitleFatalErrorNoApp", "TitleNativeFatalError",
    ];

    /// <summary>
    /// Names no default may hold: a host that sets no resolver ships the defaults as they are. The family's
    /// own apps among them, since the package stays neutral for every host.
    /// </summary>
    private static readonly string[] ProductNames =
        ["Claude", "Anthropic", "OpenCode", "ClaudeForge", "OpenForge", "DiffView", "Bennewitz", "Ninja"];

    /// <summary>Each picker, with its key and the title its options take for a title given or none.</summary>
    private static readonly Dictionary<string, (string Key, Func<string?, string?> TitleFor)> Pickers = new()
    {
        ["folder"] = (nameof(DialogStrings.TitleSelectFolder), title => AvaloniaDialogService.FolderPickerOptions(title).Title),
        ["file"] = (nameof(DialogStrings.TitleSelectFile), title => AvaloniaDialogService.FilePickerOptions(title, null).Title),
        ["save"] = (nameof(DialogStrings.TitleSaveAs),
            title => AvaloniaDialogService.SaveFilePickerOptions(title, "notes.txt", null).Title),
    };

    /// <summary>
    /// Each way a host asks for a confirmation, with no labels and with two: on the service and through the
    /// contract, whose defaults a call compiles into itself, with a string and with a message.
    /// </summary>
    private static readonly Dictionary<string, (Func<AvaloniaDialogService, Task<bool?>> Unlabelled,
        Func<AvaloniaDialogService, Task<bool?>> Labelled)> Confirmations = new()
    {
        ["the service, with a string"] = (
            s => s.ShowConfirmAsync(DialogWindows.Title, DialogWindows.MessageText),
            s => s.ShowConfirmAsync(DialogWindows.Title, DialogWindows.MessageText, DialogWindows.ConfirmLabel,
                DialogWindows.CancelLabel)),
        ["the service, with a message"] = (
            s => s.ShowConfirmAsync(DialogWindows.Title, DialogWindows.Message()),
            s => s.ShowConfirmAsync(DialogWindows.Title, DialogWindows.Message(), DialogCategory.Confirmation,
                DialogWindows.ConfirmLabel, DialogWindows.CancelLabel)),
        ["the contract, with a string"] = (
            s => ((IDialogService)s).ShowConfirmAsync(DialogWindows.Title, DialogWindows.MessageText),
            s => ((IDialogService)s).ShowConfirmAsync(DialogWindows.Title, DialogWindows.MessageText,
                DialogWindows.ConfirmLabel, DialogWindows.CancelLabel)),
        ["the contract, with a message"] = (
            s => ((IDialogService)s).ShowConfirmAsync(DialogWindows.Title, DialogWindows.Message()),
            s => ((IDialogService)s).ShowConfirmAsync(DialogWindows.Title, DialogWindows.Message(),
                DialogCategory.Confirmation, DialogWindows.ConfirmLabel, DialogWindows.CancelLabel)),
    };

    private readonly ILogger _originalLogger = Log.Logger;
    private readonly CollectingSink _log = new();
    private readonly string _logs =
        Path.Combine(Path.GetTempPath(), "bb-appservices-strings-" + Guid.NewGuid().ToString("N"));

    public DialogStringsTests()
    {
        DialogStrings.ResetForTesting();
        AvaloniaDiagnostics.ResetForTests();
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(_log).CreateLogger();
    }

    public static TheoryData<string> Windows => new(DialogWindows.All.Select(entry => entry.Type.Name));

    public static TheoryData<string> PickerNames => new(Pickers.Keys);

    public static TheoryData<string> ConfirmationCalls => new(Confirmations.Keys);

    public static TheoryData<string> Failures => new("throws", "gives null", "gives blank text");

    private static HeadlessUnitTestSession Session =>
        HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

    public void Dispose()
    {
        DialogStrings.ResetForTesting();
        NativeErrorDialog.Reset();
        Log.Logger = _originalLogger;
        AvaloniaDiagnostics.ResetForTests();
        try
        {
            if (Directory.Exists(_logs))
            {
                Directory.Delete(_logs, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort: a log file still open leaves the folder in the temporary directory.
        }
    }

    // -----------------------------------------------------------------------
    // The keys
    // -----------------------------------------------------------------------

    [Fact]
    public void Every_key_has_a_property_and_the_plan_s_default_and_there_is_no_other_key()
    {
        Assert.Equal(
            Declared.Select(declared => declared.Key).Order(StringComparer.Ordinal),
            StringProperties().Select(property => property.Name).Order(StringComparer.Ordinal));

        foreach ((string key, string expected) in Declared)
        {
            Assert.True(expected == Read(key), $"{key} reads \"{Read(key)}\" by default, where plans/00008 gives \"{expected}\".");
        }
    }

    [Fact]
    public void Each_property_asks_the_resolver_for_its_own_name_and_shows_what_it_gives()
    {
        List<string> asked = [];
        DialogStrings.Resolver = key =>
        {
            asked.Add(key);
            return $"host's {key}";
        };

        foreach (PropertyInfo property in StringProperties())
        {
            asked.Clear();

            Assert.Equal($"host's {property.Name}", property.GetValue(null));
            Assert.Equal([property.Name], asked);
        }
    }

    [Fact]
    public void No_default_names_a_product()
    {
        List<string> named =
        [
            .. from property in StringProperties()
               let text = (string)property.GetValue(null)!
               from product in ProductNames
               where text.Contains(product, StringComparison.OrdinalIgnoreCase)
               select $"{property.Name} = \"{text}\" names {product}",
        ];

        Assert.True(StringProperties().Length >= Declared.Length, "The scan found fewer strings than the table, so it proves nothing.");
        Assert.True(named.Count == 0, "A host that sets no resolver ships these as they are:\n  " + string.Join("\n  ", named));
    }

    [Fact]
    public void Every_window_in_the_table_of_windows_has_its_strings_listed()
    {
        Assert.Equal(
            DialogWindows.All.Select(entry => entry.Type.Name).Order(StringComparer.Ordinal),
            KeysShown.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_key_is_shown_in_a_window_of_the_table_or_held_by_a_test_of_its_own()
    {
        Assert.Equal(
            StringProperties().Select(property => property.Name).Order(StringComparer.Ordinal),
            KeysShown.Values.SelectMany(keys => keys).Concat(KeysOutsideWindows).Distinct().Order(StringComparer.Ordinal));
    }

    // -----------------------------------------------------------------------
    // In a window: the walk
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Windows))]
    public Task Every_string_the_package_gives_in_a_window_is_the_host_s(string window) => Session.Dispatch(() =>
    {
        DialogStrings.Resolver = Marker;
        Window dialog = DialogWindows.Named(window).Build();
        dialog.Show();
        Dispatcher.UIThread.RunJobs();

        // A 📋 shows TipCopied once it has copied.
        foreach (Button copy in dialog.GetLogicalDescendants().OfType<Button>().Where(button =>
                     AutomationProperties.GetAutomationId(button)?.StartsWith("DialogCopyPath", StringComparison.Ordinal) is true))
        {
            DialogInput.Press(copy);
        }

        Dispatcher.UIThread.RunJobs();

        List<string> english = [];
        HashSet<string> shown = new(StringComparer.Ordinal);
        foreach ((string where, string text) in StringsIn(dialog))
        {
            shown.UnionWith(MarkedKeys(text));
            if (Unmarked(text) is { Length: > 0 } left)
            {
                english.Add($"{where}: \"{text}\" leaves \"{left}\"");
            }
        }

        Assert.True(english.Count == 0,
            $"{window} shows words that are neither the host's nor its caller's, so no host can replace them:\n  "
            + string.Join("\n  ", english));
        Assert.Equal(KeysShown[window].Order(StringComparer.Ordinal), shown.Order(StringComparer.Ordinal));
    }, CancellationToken.None);

    [Fact]
    public Task A_change_of_the_host_s_words_reaches_the_next_window_and_an_open_one_keeps_its_own() =>
        Session.Dispatch(() =>
        {
            DialogStrings.Resolver = key => $"first {key}";
            MessageAlertDialog first = Assert.IsType<MessageAlertDialog>(DialogWindows.Named(nameof(MessageAlertDialog)).Build());
            first.Show();
            DialogStrings.Resolver = key => $"second {key}";
            MessageAlertDialog second = Assert.IsType<MessageAlertDialog>(DialogWindows.Named(nameof(MessageAlertDialog)).Build());

            // The open window's 📋 is pressed after the change, and still says what the window was built with.
            Button copy = Assert.IsType<Button>(WithId(first, "DialogCopyPath"));
            DialogInput.Press(copy);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("first LabelOk", first.OkButton.Content);
            Assert.Equal("first TipOk", ToolTip.GetTip(first.OkButton));
            Assert.Equal("first TipCopied", ToolTip.GetTip(copy));
            Assert.Equal("second LabelOk", second.OkButton.Content);
            Assert.Equal("second TipOk", ToolTip.GetTip(second.OkButton));
        }, CancellationToken.None);

    // -----------------------------------------------------------------------
    // Outside a window
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(PickerNames))]
    public void A_picker_with_no_title_takes_the_host_s_and_a_title_given_shows_as_given(string picker)
    {
        DialogStrings.Resolver = Marker;
        (string key, Func<string?, string?> titleFor) = Pickers[picker];

        Assert.Equal(Marked(key), titleFor(null));
        Assert.Equal("Pick the backup", titleFor("Pick the backup"));
    }

    [Fact]
    public void The_fatal_dialogs_titles_are_the_host_s_before_and_after_the_application_is_named()
    {
        DialogStrings.Resolver = Marker;
        NativeErrorDialog.SuppressForTests = true;

        Assert.Equal(Marked(nameof(DialogStrings.TitleFatalErrorNoApp)), AvaloniaDiagnostics.FatalErrorDialogTitle);
        AvaloniaDiagnostics.ShowNativeFatalError("start-up failed");
        Assert.Equal(Marked(nameof(DialogStrings.TitleFatalErrorNoApp)), NativeErrorDialog.LastSuppressedCall.Title);

        AvaloniaDiagnostics.ConfigureLogging(new AvaloniaDiagnosticsOptions
        {
            AppName = "strings-tests",
            LogsDirectory = _logs,
            EnableTraceSink = false,
            BridgeAvaloniaLogger = false,
        });

        Assert.Equal(Marked(nameof(DialogStrings.TitleFatalError), "strings-tests"), AvaloniaDiagnostics.FatalErrorDialogTitle);
        AvaloniaDiagnostics.ShowNativeFatalError("the window failed to open");
        Assert.Equal(Marked(nameof(DialogStrings.TitleNativeFatalError), "strings-tests"), NativeErrorDialog.LastSuppressedCall.Title);
    }

    [Fact]
    public Task The_fatal_dialogs_the_package_opens_show_the_host_s_message_and_a_message_given_as_given() =>
        Session.Dispatch(() =>
        {
            DialogStrings.Resolver = Marker;
            List<Window> opened = [];
            using IDisposable tracking = Window.WindowOpenedEvent.AddClassHandler(
                typeof(Window), (sender, _) => opened.Add((Window)sender!));

            AvaloniaDiagnostics.ShowFatalErrorDialog(null, new InvalidOperationException(DialogWindows.Failure));
            UiThreadExceptions.OpenFatalErrorDialog(new InvalidOperationException(DialogWindows.Failure), () => { });
            AvaloniaDiagnostics.ShowFatalErrorDialog(DialogWindows.MessageText, new InvalidOperationException(DialogWindows.Failure));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(3, opened.Count);
            string[] expected =
            [
                Marked(nameof(DialogStrings.TextFatalError)), Marked(nameof(DialogStrings.TextFatalError)),
                DialogWindows.MessageText,
            ];
            for (int i = 0; i < opened.Count; i++)
            {
                FatalErrorDialog dialog = Assert.IsType<FatalErrorDialog>(opened[i]);
                Assert.Equal(Marked(nameof(DialogStrings.TitleFatalErrorNoApp)), dialog.Title);
                Assert.Contains(dialog.GetLogicalDescendants().OfType<TextBlock>(), block => block.Text == expected[i]);
                dialog.Close();
            }
        }, CancellationToken.None);

    [Theory]
    [MemberData(nameof(ConfirmationCalls))]
    public Task A_confirmation_with_no_labels_shows_the_host_s_words_and_labels_given_as_given(string call) =>
        Session.Dispatch(() =>
        {
            DialogStrings.Resolver = Marker;
            (Func<AvaloniaDialogService, Task<bool?>> unlabelled, Func<AvaloniaDialogService, Task<bool?>> labelled) =
                Confirmations[call];
            Window owner = new();
            owner.Show();
            AvaloniaDialogService service = new() { FindOwner = () => owner };

            Task<bool?> first = unlabelled(service);
            (Button confirm, Button cancel) = ButtonsOf(Assert.Single(owner.OwnedWindows));
            Assert.Equal(Marked(nameof(DialogStrings.LabelConfirm)), confirm.Content);
            Assert.Equal(Marked(nameof(DialogStrings.LabelCancel)), cancel.Content);
            DialogInput.Press(cancel);
            Dispatcher.UIThread.RunJobs();
            Assert.True(first.IsCompletedSuccessfully);

            Task<bool?> second = labelled(service);
            (confirm, cancel) = ButtonsOf(Assert.Single(owner.OwnedWindows));
            Assert.Equal(DialogWindows.ConfirmLabel, confirm.Content);
            Assert.Equal(DialogWindows.CancelLabel, cancel.Content);
            DialogInput.Press(cancel);
            Dispatcher.UIThread.RunJobs();
            Assert.True(second.IsCompletedSuccessfully);
        }, CancellationToken.None);

    // -----------------------------------------------------------------------
    // A host's strings that fail
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Failures))]
    public Task A_resolver_that_fails_gives_each_string_its_default_and_one_Warning_for_each_key(string failure) =>
        Session.Dispatch(() =>
        {
            DialogStrings.Resolver = failure switch
            {
                "throws" => key => throw new InvalidOperationException($"no text for {key}"),
                "gives null" => _ => null!,
                _ => _ => "  ",
            };

            _ = DialogWindows.Named(nameof(InputDialog)).Build();
            InputDialog input = Assert.IsType<InputDialog>(DialogWindows.Named(nameof(InputDialog)).Build());

            Assert.Equal("OK", input.OkButton.Content);
            Assert.Equal("Use the text you typed", ToolTip.GetTip(input.OkButton));
            Assert.Equal("Cancel", input.CancelButton.Content);
            Assert.Equal("Close this dialog without doing anything", ToolTip.GetTip(input.CancelButton));
            Assert.Equal("Type here; Enter accepts it and Escape cancels", ToolTip.GetTip(input.InputBox));

            LogEvent[] warnings = Warnings();
            Assert.Equal(
                new[] { "LabelCancel", "LabelOk", "TipAccept", "TipCancel", "TipInput" },
                warnings.Select(KeyOf).Order(StringComparer.Ordinal));
            string reason = failure switch
            {
                "throws" => "threw",
                "gives null" => "gaveNull",
                _ => "gaveBlank",
            };
            Assert.All(warnings, warning =>
            {
                LogLineForm.Is(warning, LogEventLevel.Warning, PackageLogLines.DialogStrings, warning.Exception);
                Assert.Equal(reason, LogLineForm.ValueOf(warning, "Reason"));
                Assert.Equal(failure == "throws", warning.Exception is InvalidOperationException);
            });
        }, CancellationToken.None);

    [Fact]
    public Task A_text_whose_placeholder_cannot_be_filled_gives_its_default_and_one_without_it_drops_the_value() =>
        Session.Dispatch(() =>
        {
            DialogStrings.Resolver = key => key switch
            {
                nameof(DialogStrings.TipCopyPath) => "Kopieren {1}",
                nameof(DialogStrings.TipOpenLink) => "Öffnen",
                var _ => key,
            };

            Window dialog = DialogWindows.Named(nameof(MessageAlertDialog)).Build();

            Assert.Equal($"Copy {DialogWindows.FirstPath}", ToolTip.GetTip(WithId(dialog, "DialogCopyPath")));
            Assert.Equal($"Copy {DialogWindows.SecondPath}", ToolTip.GetTip(WithId(dialog, "DialogCopyPath2")));
            Assert.Equal("Öffnen", ToolTip.GetTip(WithId(dialog, "DialogOpenLink")));
            LogEvent warning = Assert.Single(Warnings());
            LogLineForm.Is(warning, LogEventLevel.Warning, PackageLogLines.DialogStrings, warning.Exception);
            Assert.Equal(nameof(DialogStrings.TipCopyPath), KeyOf(warning));
            Assert.Equal("badPlaceholder", LogLineForm.ValueOf(warning, "Reason"));
            Assert.IsType<FormatException>(warning.Exception);
        }, CancellationToken.None);

    [Fact]
    public void A_key_given_back_shows_its_default_and_logs_nothing()
    {
        DialogStrings.Resolver = key => key;

        foreach ((string key, string expected) in Declared)
        {
            Assert.Equal(expected, Read(key));
        }

        Assert.Empty(Warnings());
    }

    [Fact]
    public Task The_fatal_dialog_is_built_with_its_defaults_when_the_resolver_and_the_logger_both_throw() =>
        Session.Dispatch(() =>
        {
            DialogStrings.Resolver = _ => throw new InvalidOperationException("the host's strings are broken");
            Log.Logger = new ThrowingLogger();

            FatalErrorDialog dialog = new(DialogWindows.Title, DialogWindows.MessageText, new InvalidOperationException(DialogWindows.Failure));

            Button copy = Assert.IsType<Button>(WithId(dialog, "DialogCopy"));
            Button close = Assert.IsType<Button>(WithId(dialog, "DialogClose"));
            Control details = WithId(dialog, "DialogDetails");
            Assert.Equal("📋 Copy to Clipboard", copy.Content);
            Assert.Equal("Copy to Clipboard", AutomationProperties.GetName(copy));
            Assert.Equal("Copy the full error details to the clipboard", ToolTip.GetTip(copy));
            Assert.Equal("Close", close.Content);
            Assert.Equal("Close this window", ToolTip.GetTip(close));
            Assert.Equal("Error details", AutomationProperties.GetName(details));
            Assert.Equal("Read-only exception text; Copy to Clipboard copies all of it", ToolTip.GetTip(details));
        }, CancellationToken.None);

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>The marker a resolver gives for <paramref name="key"/>, with <c>{0}</c> where it carries a value.</summary>
    private static string Marker(string key)
    {
        return TemplateKeys.Contains(key) ? $"‹{key}› {{0}}" : $"‹{key}›";
    }

    private static string Marked(string key)
    {
        return $"‹{key}›";
    }

    private static string Marked(string key, string value)
    {
        return $"‹{key}› {value}";
    }

    private static PropertyInfo[] StringProperties()
    {
        return
        [
            .. typeof(DialogStrings).GetProperties(BindingFlags.Public | BindingFlags.Static)
                                    .Where(property => property.PropertyType == typeof(string)),
        ];
    }

    private static string? Read(string key)
    {
        return (string?)typeof(DialogStrings).GetProperty(key, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
    }

    /// <summary>
    /// The title of <paramref name="window"/>, and every name, help text, tooltip, label and placeholder of
    /// every control in it, each with where it was read.
    /// </summary>
    private static IEnumerable<(string Where, string Text)> StringsIn(Window window)
    {
        yield return ("the window's title", window.Title ?? string.Empty);
        foreach (Control control in window.GetLogicalDescendants().OfType<Control>())
        {
            string id = AutomationProperties.GetAutomationId(control) ?? control.GetType().Name;
            if (ControlAutomationPeer.CreatePeerForElement(control).GetName() is { Length: > 0 } name)
            {
                yield return ($"{id}'s name", name);
            }

            if (AutomationProperties.GetHelpText(control) is { Length: > 0 } help)
            {
                yield return ($"{id}'s help text", help);
            }

            if (ToolTip.GetTip(control) is string tip)
            {
                yield return ($"{id}'s tooltip", tip);
            }

            if (control is ContentControl { Content: string label })
            {
                yield return ($"{id}'s label", label);
            }

            if (control is TextBox { PlaceholderText: { Length: > 0 } placeholder })
            {
                yield return ($"{id}'s placeholder", placeholder);
            }
        }
    }

    /// <summary>The keys marked in <paramref name="text"/>.</summary>
    private static IEnumerable<string> MarkedKeys(string text)
    {
        int start = text.IndexOf('‹', StringComparison.Ordinal);
        while (start >= 0)
        {
            int end = text.IndexOf('›', start);
            if (end < 0)
            {
                yield break;
            }

            yield return text[(start + 1)..end];
            start = text.IndexOf('‹', end);
        }
    }

    /// <summary>
    /// What is left of <paramref name="text"/> once its markers, the strings the table of windows passes, its
    /// white space and the package's glyphs are taken out.
    /// </summary>
    private static string Unmarked(string text)
    {
        string left = text;
        foreach (string key in MarkedKeys(text).ToArray())
        {
            left = left.Replace(Marked(key), string.Empty, StringComparison.Ordinal);
        }

        foreach (string given in DialogWindows.Given.OrderByDescending(given => given.Length))
        {
            left = left.Replace(given, string.Empty, StringComparison.Ordinal);
        }

        StringBuilder kept = new();
        foreach (Rune rune in left.EnumerateRunes())
        {
            if (!Rune.IsWhiteSpace(rune) && !IsGlyph(rune))
            {
                kept.Append(rune.ToString());
            }
        }

        return kept.ToString();
    }

    /// <summary>
    /// A glyph the package shows, which no host translates: a symbol, ⚠, ⛔ and 📋 among them and the U+FFFC a
    /// text block's text holds for each control in its line, or an arrow, ↗ among them.
    /// </summary>
    private static bool IsGlyph(Rune rune)
    {
        return Rune.GetUnicodeCategory(rune) == System.Globalization.UnicodeCategory.OtherSymbol
               || rune.Value is >= 0x2190 and <= 0x21FF;
    }

    /// <summary>The confirm and cancel buttons of the confirmation <paramref name="window"/> is.</summary>
    private static (Button Confirm, Button Cancel) ButtonsOf(Window window)
    {
        return window switch
        {
            ConfirmDialog dialog => (dialog.ConfirmButton, dialog.CancelButton),
            MessageConfirmDialog dialog => (dialog.ConfirmButton, dialog.CancelButton),
            var _ => throw new InvalidOperationException($"{window.GetType().Name} is not a confirmation."),
        };
    }

    private static Control WithId(Window window, string id)
    {
        return window.GetLogicalDescendants()
                     .OfType<Control>()
                     .Single(control => AutomationProperties.GetAutomationId(control) == id);
    }

    private LogEvent[] Warnings()
    {
        return [.. _log.Events.Where(logEvent => logEvent.Level == LogEventLevel.Warning)];
    }

    private static string? KeyOf(LogEvent logEvent)
    {
        return logEvent.Properties.TryGetValue("Key", out LogEventPropertyValue? value)
               && value is ScalarValue { Value: string key }
            ? key
            : null;
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    /// <summary>A logger whose every write throws.</summary>
    private sealed class ThrowingLogger : ILogger
    {
        public bool IsEnabled(LogEventLevel level) => true;

        public void Write(LogEvent logEvent) => throw new InvalidOperationException("the logger's own bug");

        public void Write(LogEventLevel level, Exception? exception, string messageTemplate, params object?[]? propertyValues) =>
            throw new InvalidOperationException("the logger's own bug");
    }
}
