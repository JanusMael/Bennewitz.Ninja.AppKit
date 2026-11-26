namespace AppServices.Tests.Logging;

/// <summary>
/// The template of each line the packages write that a test reaches, as <c>plans/00009</c>'s "The lines"
/// gives it. A test compares what the package wrote with one of these, through
/// <see cref="LogLineForm.Is"/>, and <see cref="LogLineFormTests"/> holds every one to the form.
/// </summary>
/// <remarks>
/// The entry point's own two lines are held by <c>AppMainTests</c>, beside the tests of its run; the start
/// line and the exit line with a code are here too, for the tests of <c>EntryPointOptions()</c>' writer.
/// The host's event lines and the rolling file's rendered lines are written as they came (decision 8), and
/// <c>ShellLauncher</c>'s line is text, not a template: <c>ShellLauncherDiagnosticsTests</c> holds it.
/// </remarks>
internal static class PackageLogLines
{
    public const string AppStart = "[App.Start] app={App} version={Version} pid={Pid}";
    public const string AppExitWithCode = "[App.Exit] code={Code} elapsedMs={ElapsedMs}";

    public const string AppFatal = "[App.Fatal] reason=unhandledException";
    public const string TaskUnobservedBenign = "[Task.Unobserved] benign=true";
    public const string TaskUnobserved = "[Task.Unobserved] reason=notObserved benign=false";
    public const string TaskBenignFilterThrew = "[Task.BenignFilter] reason=threw";
    public const string SmokeNoExitEvent = "[App.Smoke] reason=noExitEvent lifetime={Lifetime}";
    public const string SmokeUnobservedTaskExceptions = "[App.Smoke] reason=unobservedTaskExceptions count={Count} code=1";

    public const string BindingValidationException =
        "[Binding.Validation] reason=exception control=\"{Control}\" dataContext=\"{DataContext}\" context=\"{Context}\" type=\"{Type}\" stack=\"{Stack}\"";

    public const string BindingValidationError =
        "[Binding.Validation] reason=error control=\"{Control}\" dataContext=\"{DataContext}\" context=\"{Context}\" error=\"{Error}\"";

    public const string AvaloniaLog = "[Avalonia.Log] area={Area} text=\"{Text}\"";

    public const string DialogStrings = "[Dialog.Strings] key={Key} reason={Reason}";

    public const string DialogCannotPost = "[Dialog.Fatal] reason=cannotPost";
    public const string DialogCannotShow = "[Dialog.Fatal] reason=cannotShow";
    public const string UiThreadBenign = "[UiThread.Exception] benign=true";
    public const string UiThreadUnhandled = "[UiThread.Exception] reason=unhandled benign=false";
    public const string UiThreadBenignFilterThrew = "[UiThread.BenignFilter] reason=threw";

    public const string FailureRepeat =
        "[Failure.Repeat] reason=repeated count={Count} type=\"{Type}\" message=\"{Message}\" site=\"{Site}\"";
}
