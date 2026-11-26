using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Bennewitz.Ninja.AppServices.EntryPoint;

/// <summary>The kinds of app <see cref="AppMain"/> runs, which differ only in who owns what.</summary>
internal enum AppKind
{
    /// <summary>Owns its command line and its Ctrl+C.</summary>
    Console,

    /// <summary>A web host: its arguments are configuration, and its host owns shutdown.</summary>
    Host,

    /// <summary>No console: its window's close is its cancellation.</summary>
    Desktop,
}

/// <summary>
/// One run of an entry point: its hooks, its Ctrl+C, its reports, its two log lines, its flush and the
/// exit code it ends with.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>Everything is per run, nothing is shared between runs.</b> A test host runs one entry point
/// many times in a process, sometimes two at once, and each run owns its own callbacks. The cost is a
/// duplicate report when two overlapping runs both see one process-wide crash.
/// </para>
/// <para>
/// ⛔ <b>First match wins</b> when a run ends: a <c>HostAbortedException</c> passes through
/// untouched (the caller's exception filter); after Ctrl+C a console run exits 130 however its work
/// ended; a console <see cref="UsageException"/> exits 2; any other exception is a failure and exits 1;
/// otherwise the work's own code stands.
/// </para>
/// <para>
/// ⛔ <b>A run ends once</b>, at the first of its ends: its work's, in <see cref="AppMain"/>'s
/// <c>finally</c>; SIGTERM; the runtime's, on an exception from another thread; or the process's exit.
/// That end writes the exit line, when the run offered a start line, and then flushes the log, and every
/// later end does nothing. So the exit line comes once and before the flush, and a start line is never
/// offered once the run has ended.
/// </para>
/// <para>
/// ⛔ <b>The app's <see cref="AppMainOptions.IsBenignException"/> decides one thing only</b>: whether a
/// task exception nobody observed is reported on stderr. Every failure that ends the run is reported
/// whatever it says, and every unobserved exception still reaches the app's callback.
/// </para>
/// </remarks>
internal sealed class EntryRun : IDisposable
{
    internal const int Failure = 1;
    internal const int UsageError = 2;
    internal const int Cancelled = 130;
    internal const string VersionArgument = "--version";

    // The entry point's two lines (plans/00009, decisions 2 and 4): the start line, and the exit line for
    // each way a run ends, whose last hole is the whole milliseconds since the run started.
    private const string StartLine = "[App.Start] app={App} version={Version} pid={Pid}";
    private const string ExitedWithCode = "[App.Exit] code={Code} elapsedMs={ElapsedMs}";
    private const string EndedBySigterm = "[App.Exit] signal=SIGTERM elapsedMs={ElapsedMs}";
    private const string EndedByUnhandledException = "[App.Exit] reason=unhandledException elapsedMs={ElapsedMs}";
    private const string StoppedByDesignTimeTool = "[App.Exit] reason=hostAborted elapsedMs={ElapsedMs}";

    // How far a run has got, so that its start line is offered once, and only before its end, which
    // comes once.
    private const int NotBegun = 0;
    private const int Begun = 1;
    private const int Ended = 2;

    private static readonly AsyncLocal<EntryRun?> s_current = new();

    private readonly long _startedAt = Stopwatch.GetTimestamp();
    private readonly AppKind _kind;
    private readonly AppMainOptions _options;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly EntryRun? _previous;
    private readonly PosixSignalRegistration? _sigterm;
    private int _stage;
    private int? _code;
    private int _ctrlCPresses;

    private EntryRun(Assembly program, AppKind kind, AppMainOptions options)
    {
        Program = program;
        AppName = program.GetName().Name ?? "The application";
        _kind = kind;
        _options = options;

        // ⛔ First, before the version check or the work: a failure in either must be reported.
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        if (_kind == AppKind.Console)
        {
            Console.CancelKeyPress += OnCancelKeyPress;
        }

        // A web host handles SIGTERM itself and lets its run end, which flushes; the other two kinds
        // would otherwise be killed by it with their log unflushed.
        if (_kind != AppKind.Host)
        {
            _sigterm = RegisterSigterm();
        }

        _previous = s_current.Value;
        s_current.Value = this;
    }

    /// <summary>
    /// The run the calling code is inside, if any. Test-only: it is how a test reaches
    /// <see cref="PressCtrlC"/>, <see cref="OnSigterm"/>, <see cref="OnProcessExit"/> and
    /// <see cref="OnUnhandledException"/> from inside the work it hands a run.
    /// </summary>
    internal static EntryRun? Current => s_current.Value;

    internal Assembly Program { get; }

    internal string AppName { get; }

    internal CancellationToken Token => _cancellation.Token;

    private bool CancelledByCtrlC => _kind == AppKind.Console && Volatile.Read(ref _ctrlCPresses) > 0;

    internal static EntryRun Start(Assembly program, AppKind kind, AppMainOptions? options)
    {
        ArgumentNullException.ThrowIfNull(program);
        return new EntryRun(program, kind, options ?? new AppMainOptions());
    }

    /// <summary><c>--version</c>, and only when it is the only argument.</summary>
    internal static bool AsksForVersion(string[] args) => args is [VersionArgument];

    /// <summary>
    /// Whether <paramref name="exception"/> is a design-time tool stopping the entry point. By name,
    /// as <c>HostFactoryResolver</c> checks it: it throws the public
    /// <c>Microsoft.Extensions.Hosting.HostAbortedException</c> when that loads, and otherwise a
    /// private type of the same name.
    /// </summary>
    internal static bool IsHostAborted(Exception exception) =>
        exception.GetType().Name == "HostAbortedException";

    /// <summary>
    /// Prints the release version on stdout and returns 0, or reports that there is none and returns 1.
    /// </summary>
    /// <remarks>
    /// ⛔ <c>PublicVersion</c>, never <c>AssemblyInformationalVersion</c>, which AutoVersioning sets to
    /// "Built with ♥ &lt;commit&gt;". There is no fallback: a missing one is a build defect, and any
    /// other number would be a version nobody released.
    /// </remarks>
    internal int AnswerVersion()
    {
        if (ReadPublicVersion() is not { } version)
        {
            Error($"error: {AppName} carries no [AssemblyMetadata(\"PublicVersion\")], so it has no release version to print");
            return Decided(Failure);
        }

        Console.Out.WriteLine(version);
        return Decided(0);
    }

    /// <summary>
    /// Offers the start line, just before the work runs: once, and never once the run has ended, as
    /// SIGTERM may end it first.
    /// </summary>
    internal void Begin()
    {
        if (Interlocked.CompareExchange(ref _stage, Begun, NotBegun) == NotBegun
            && _options.WriteLogLine is { } write)
        {
            // The version is read inside the guard, so that an assembly whose attributes cannot be read
            // costs the line and never the run.
            Invoke(nameof(AppMainOptions.WriteLogLine),
                () => write(StartLine, AppName, ReadPublicVersion() ?? "unknown", Environment.ProcessId));
        }
    }

    /// <summary>The exit code for work that returned <paramref name="returned"/>.</summary>
    internal int Completed(int returned) => Decided(CancelledByCtrlC ? Cancelled : returned);

    /// <summary>The exit code for work that threw <paramref name="exception"/>, after reporting it.</summary>
    internal int Failed(Exception exception)
    {
        if (CancelledByCtrlC)
        {
            // Ctrl+C was pressed, so the run is cancelled however its work ended. A cancellation is
            // the expected way to end; anything else is still worth seeing.
            if (exception is not OperationCanceledException)
            {
                Error($"fatal: {AppName} stopped on an unhandled exception after Ctrl+C", exception);
            }

            return Decided(Cancelled);
        }

        if (_kind == AppKind.Console && exception is UsageException usage)
        {
            Error(string.IsNullOrWhiteSpace(_options.Usage)
                ? $"error: {usage.Message}"
                : $"error: {usage.Message}{Environment.NewLine}{Environment.NewLine}{_options.Usage}");
            return Decided(UsageError);
        }

        ReportFatal(exception);
        return Decided(Failure);
    }

    /// <summary>
    /// Ends the run from <see cref="AppMain"/>'s <c>finally</c>, with the exit code its way out decided,
    /// or, when none did, as a design-time tool's stop: the <c>catch</c> before it takes every exception
    /// but a <c>HostAbortedException</c>, and every other way out decides a code.
    /// </summary>
    internal void End()
    {
        if (_code is { } code)
        {
            EndWith(ExitedWithCode, code);
        }
        else
        {
            EndWith(StoppedByDesignTimeTool);
        }
    }

    /// <summary>
    /// What pressing Ctrl+C does: the first press cancels the token and is intercepted; any later one
    /// is not, so the runtime ends a run that did not stop.
    /// </summary>
    /// <returns>Whether this press is intercepted.</returns>
    internal bool PressCtrlC()
    {
        if (Interlocked.Increment(ref _ctrlCPresses) != 1)
        {
            return false;
        }

        try
        {
            _cancellation.Cancel();
        }
        catch (Exception exception)
        {
            // A callback registered on the token threw, or the run has just ended. Either way this
            // runs on the runtime's signal thread, where an escaping exception would end the process.
            Error($"warning: {AppName}: cancellation did not complete cleanly", exception);
        }

        return true;
    }

    /// <summary>
    /// What SIGTERM does to a console or desktop run: ends it, its exit line and its flush, and leaves
    /// the signal to end the process as it does by default.
    /// </summary>
    internal void OnSigterm() => EndWith(EndedBySigterm);

    // The runtime ends the process when this returns, on an exception from another thread, and no
    // finally runs; a test calls it from inside the work, since the real event ends the test host.
    internal void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        Exception exception = e.ExceptionObject as Exception
            ?? new InvalidOperationException($"A non-exception object was thrown: {e.ExceptionObject}");

        ReportFatal(exception);

        if (e.IsTerminating)
        {
            EndWith(EndedByUnhandledException);
        }
    }

    // An exit no finally sees still ends the run, with the code it leaves: Environment.Exit called from
    // the work sets it. On a normal exit the run has already ended and unhooked this, so it does not
    // end twice. A test calls it from inside the work.
    // ⚠ SIGTERM is not such an exit: on Linux it ends the process without raising ProcessExit, so
    // RegisterSigterm ends the run for it instead.
    internal void OnProcessExit(object? sender, EventArgs e) => EndWith(ExitedWithCode, Environment.ExitCode);

    public void Dispose()
    {
        _sigterm?.Dispose();
        if (_kind == AppKind.Console)
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
        }

        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        s_current.Value = _previous;
        _cancellation.Dispose();
    }

    /// <summary>Records <paramref name="code"/> as the one the run's way out decided, for its exit line.</summary>
    private int Decided(int code)
    {
        _code = code;
        return code;
    }

    /// <summary>
    /// Ends the run, at the first of its ends and never again: writes the exit line, when the run
    /// offered a start line, then flushes the app's log.
    /// </summary>
    /// <param name="exitLine">The exit line's template, whose last hole is the elapsed time.</param>
    /// <param name="values">The values of its holes before that one.</param>
    private void EndWith(string exitLine, params object?[] values)
    {
        int stage = Interlocked.Exchange(ref _stage, Ended);
        if (stage == Ended)
        {
            return;
        }

        if (stage == Begun && _options.WriteLogLine is { } write)
        {
            object?[] line = [.. values, (long)Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds];
            Invoke(nameof(AppMainOptions.WriteLogLine), () => write(exitLine, line));
        }

        if (_options.FlushLog is { } flush)
        {
            Invoke(nameof(AppMainOptions.FlushLog), flush);
        }
    }

    /// <summary>
    /// The program's <c>[AssemblyMetadata("PublicVersion")]</c>, or null when it carries none:
    /// <c>--version</c> reports that as a defect, and the start line writes <c>unknown</c>.
    /// </summary>
    private string? ReadPublicVersion()
    {
        string? version = null;
        foreach (AssemblyMetadataAttribute metadata in Program.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (metadata.Key == "PublicVersion")
            {
                version = metadata.Value;
            }
        }

        return string.IsNullOrWhiteSpace(version) ? null : version;
    }

    private void ReportFatal(Exception exception)
    {
        Error($"fatal: {AppName} stopped on an unhandled exception", exception);
        if (_options.OnFatal is { } onFatal)
        {
            Invoke(nameof(AppMainOptions.OnFatal), () => onFatal(exception));
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        bool benign = BenignExceptionRule.IsBenign(_options.IsBenignException, e.Exception, out Exception? filterFailure);
        if (!benign)
        {
            Error($"warning: {AppName} had a task exception nobody observed", e.Exception);
        }

        // A filter that throws has a bug of its own, and the exception it was asked about counts as
        // not benign, so both are on stderr.
        if (filterFailure is not null)
        {
            Error($"warning: {AppName}: the {nameof(AppMainOptions.IsBenignException)} filter threw", filterFailure);
        }

        // A benign one still reaches the callback: whether to log it, and at what level, is the app's.
        if (_options.OnUnobservedTaskException is { } onUnobserved)
        {
            Invoke(nameof(AppMainOptions.OnUnobservedTaskException), () => onUnobserved(e.Exception));
        }
    }

    // Ends the run on SIGTERM and leaves the signal to do what it does by default, so the process still
    // ends as SIGTERM ends it, its exit line written and its log flushed first.
    // ⛔ Never set the context's Cancel: a cancelled SIGTERM would leave the app running.
    private PosixSignalRegistration? RegisterSigterm()
    {
        try
        {
            return PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => OnSigterm());
        }
        catch (Exception)
        {
            // ⛔ Nothing that goes wrong here may stop the app from starting, and Start runs outside
            // AppMain's try: a platform without SIGTERM, a handler it cannot install, access refused.
            // The run goes on, and SIGTERM ends it unflushed.
            return null;
        }
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        if (PressCtrlC())
        {
            e.Cancel = true;
        }
    }

    private void Invoke(string callback, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            Error($"warning: {AppName}: the {callback} callback threw", exception);
        }
    }

    private static void Error(string headline, Exception exception)
    {
        string text;
        try
        {
            text = exception.ToString();
        }
        catch (Exception)
        {
            // ⛔ An exception whose text cannot be read is still reported, by its type, so that no report
            // throws: one that did would leave AppMain with no code, its finally would end the run as a
            // design-time tool's stop, and the run would not exit 1.
            text = exception.GetType().FullName ?? "an exception whose text could not be read";
        }

        Error(headline + Environment.NewLine + text);
    }

    private static void Error(string text)
    {
        try
        {
            Console.Error.WriteLine(text);
        }
        catch (Exception)
        {
            // ⛔ A closed or broken stderr must not replace the exit code with an exception.
        }
    }
}
