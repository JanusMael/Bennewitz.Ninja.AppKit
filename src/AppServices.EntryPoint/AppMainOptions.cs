namespace Bennewitz.Ninja.AppServices.EntryPoint;

/// <summary>
/// What an app hands <see cref="AppMain"/> beside its work: its usage text, the callbacks that
/// connect the entry point to the app's own logging and dialogs, and its filter for exceptions it
/// knows to be harmless.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Every callback is optional, and none is needed for the report.</b> The fatal report is
/// written to stderr before any callback runs, so a failure before logging is configured, or a
/// logger that fails itself, still leaves the report behind.
/// </para>
/// <para>
/// ⚠ <b>A callback that throws is reported on stderr and changes nothing else</b>: not the exit
/// code, and not the callbacks after it.
/// </para>
/// <para>
/// ⭐ <b>A record, so one member changes with <c>with</c></b>: <c>defaults with { FlushLog = … }</c>
/// keeps every other member of <c>defaults</c>. Two options are equal when all their members are.
/// </para>
/// </remarks>
public sealed record AppMainOptions
{
    /// <summary>
    /// The usage text a console app prints on stderr, after a <see cref="UsageException"/>'s message,
    /// before exiting 2. Ignored by the web and desktop kinds, which own no command line.
    /// </summary>
    public string? Usage { get; init; }

    /// <summary>
    /// Flushes the app's log. Runs once per run: when the run ends however it ends, when the runtime
    /// is terminating on an exception from another thread, or when the process exits without the run
    /// ending, as on SIGTERM.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>A run flushes once, and a console or desktop run does it the moment SIGTERM arrives</b>,
    /// then lets the signal end the process. An app that handles SIGTERM itself and goes on running,
    /// a generic host for one, logs its shutdown after that flush, and nothing flushes it again; after
    /// <c>Log.CloseAndFlush</c> those events are dropped. Such an app belongs in
    /// <see cref="AppMain.RunHostAsync"/>, whose host owns SIGTERM.
    /// </remarks>
    public Action? FlushLog { get; init; }

    /// <summary>
    /// Called with an exception that ends the run as a failure, after its report is on stderr: to log
    /// it, or to show a dialog. Also called for an exception on another thread that is terminating the
    /// process.
    /// </summary>
    public Action<Exception>? OnFatal { get; init; }

    /// <summary>
    /// Called with every task exception nobody observed: after its report is on stderr, or, when
    /// <see cref="IsBenignException"/> calls it benign, with no report at all. It does not end the run.
    /// </summary>
    /// <remarks>
    /// The exception arrives as the runtime raised it, an <see cref="AggregateException"/>, and without
    /// the filter's verdict: a callback that logs a benign one at a lower level classifies it again.
    /// </remarks>
    public Action<Exception>? OnUnobservedTaskException { get; init; }

    /// <summary>
    /// Marks the exceptions the app knows to be harmless, so that a task exception nobody observed is
    /// not reported on stderr when it is one of them. It is still passed to
    /// <see cref="OnUnobservedTaskException"/>. Without a filter, nothing is benign.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An <see cref="AggregateException"/> is benign when it flattens to at least one exception and
    /// the filter matches every one of them; the aggregate itself is never passed to the filter. A task
    /// exception nobody observed always arrives as an aggregate, so the filter is written for the
    /// exceptions inside it.
    /// </para>
    /// <para>
    /// ⛔ <b>It has no say over a failure that ends the run</b>, which is reported whatever it returns.
    /// </para>
    /// <para>
    /// ⚠ <b>It runs on the thread that found the exception</b>: for a task exception nobody observed,
    /// the finalizer thread. So it must be quick and thread-safe. A filter that throws counts as not
    /// benign, and its throw is reported on stderr beside the exception.
    /// </para>
    /// </remarks>
    public Func<Exception, bool>? IsBenignException { get; init; }
}
