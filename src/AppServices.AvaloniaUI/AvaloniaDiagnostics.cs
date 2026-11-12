using Avalonia.Threading;
using Bennewitz.Ninja.AppServices.AvaloniaUI.Binding;
using Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;
using Bennewitz.Ninja.AppServices.Dialogs;
using Bennewitz.Ninja.AppServices.EntryPoint;
using Bennewitz.Ninja.AppServices.Logging;
using Serilog;
using AvaloniaLogger = Avalonia.Logging.Logger;

namespace Bennewitz.Ninja.AppServices.AvaloniaUI;

/// <summary>
/// One-stop bootstrap for the <c>Bennewitz.Ninja.AppServices.AvaloniaUI</c>
/// diagnostics pipeline.
/// <para>
/// Intended usage is three calls in total from a host Avalonia app:
/// </para>
/// <list type="number">
///   <item>
///     <description>
///       <see cref="ConfigureLogging"/> — called from <c>Program.Main</c>
///       <em>before</em> <c>BuildAvaloniaApp()</c>. Creates
///       <see cref="Log.Logger"/>, wires the rolling file sink + Trace sink,
///       and (if requested) redirects
///       <c>Avalonia.Logging.Logger.Sink</c> into Serilog.
///     </description>
///   </item>
///   <item>
///     <description>
///       <see cref="InstallAvaloniaHooks"/> — called from
///       <c>App.OnFrameworkInitializationCompleted</c> <em>after</em> the
///       framework has finished initialising (dispatchers, binding plugin list,
///       etc. are all live). Installs <see cref="BindingValidationErrorLogger"/> to capture
///       binding-coercion errors via the <c>DataValidationErrors.ErrorsProperty</c>
///       attached-property pipeline, and the handler for exceptions nothing on the UI
///       thread caught (<see cref="AvaloniaDiagnosticsOptions.HandleUiThreadExceptions"/>),
///       which also serves the dialogs for task exceptions nobody observed
///       (<see cref="AvaloniaDiagnosticsOptions.ShowUnobservedTaskExceptions"/>).
///     </description>
///   </item>
/// </list>
/// <para>
/// Every step is individually toggleable via
/// <see cref="AvaloniaDiagnosticsOptions"/>, so a host that wants only, say,
/// the file sink + Trace (no binding plugin swap) can
/// disable the pieces it doesn't need.
/// </para>
/// </summary>
public static class AvaloniaDiagnostics
{
    private static BucketedRollingFileSink? _fileSink;
    private static AvaloniaDiagnosticsOptions? _options;

    // The host-event file, and the private logger that feeds it. Deliberately NOT Log.Logger:
    // these events are outside the Serilog pipeline by design, and routing them through the
    // shared logger would put them in the app log too - the separation this exists to keep.
    private static BucketedRollingFileSink? _eventFileSink;
    private static global::Serilog.Core.Logger? _eventLogger;

    /// <summary>
    /// Application name supplied via
    /// <see cref="AvaloniaDiagnosticsOptions.AppName"/>. Exposed so host code
    /// (e.g. a crash handler) can build user-facing strings like
    /// <c>"Application Error — {AppName}"</c> without re-threading the options
    /// instance through the call stack.
    /// <para>
    /// Returns <c>null</c> before <see cref="ConfigureLogging"/> has been
    /// called.
    /// </para>
    /// </summary>
    public static string? AppName => _options?.AppName;

    /// <summary>
    /// Logs directory supplied via
    /// <see cref="AvaloniaDiagnosticsOptions.LogsDirectory"/>. Returns
    /// <c>null</c> before <see cref="ConfigureLogging"/> has been called.
    /// </summary>
    public static string? LogsDirectory => _options?.LogsDirectory;

    /// <summary>
    /// Full path of the file the rolling sink is currently writing to, or
    /// <c>null</c> if <see cref="ConfigureLogging"/> has not been called or no
    /// event has been emitted yet. Convenient for an "Open current log" menu
    /// item, or for a log viewer attached through
    /// <see cref="AvaloniaDiagnosticsOptions.ConfigureLogger"/>.
    /// </summary>
    public static string? CurrentLogFilePath => _fileSink?.CurrentFilePath;

    /// <summary>
    /// Full path of the host-event file, or <see langword="null"/> when
    /// <see cref="AvaloniaDiagnosticsOptions.EnableEventLogFile"/> was not set.
    /// </summary>
    public static string? CurrentEventLogFilePath => _eventFileSink?.CurrentFilePath;

    /// <summary>
    /// The title every <see cref="FatalErrorDialog"/> this package opens takes: <c>"Application Error —
    /// {AppName}"</c>, or a neutral <c>"Application Error"</c> before <see cref="ConfigureLogging"/> has run.
    /// </summary>
    internal static string FatalErrorDialogTitle =>
        string.IsNullOrWhiteSpace(AppName) ? "Application Error" : $"Application Error — {AppName}";

    /// <summary>
    /// The host's <see cref="AvaloniaDiagnosticsOptions.IsBenignException"/>, read when an exception
    /// arrives: <see langword="null"/>, so nothing is benign, before <see cref="ConfigureLogging"/> has run.
    /// </summary>
    internal static Func<Exception, bool>? BenignExceptionFilter => _options?.IsBenignException;

    /// <summary>
    /// The host's <see cref="AvaloniaDiagnosticsOptions.ShowUnobservedTaskExceptions"/>, read when an
    /// exception arrives: its default, <see langword="true"/>, before <see cref="ConfigureLogging"/> has run.
    /// </summary>
    internal static bool ShowsUnobservedTaskExceptions => _options?.ShowUnobservedTaskExceptions ?? true;

    /// <summary>
    /// Configures <see cref="Log.Logger"/> and (optionally) redirects
    /// Avalonia's internal logger into it. Safe to call only once per process;
    /// subsequent calls are ignored.
    /// <para>
    /// <strong>Must be called before <c>BuildAvaloniaApp()</c>.</strong> The
    /// Serilog pipeline needs to exist before any Avalonia type touches the
    /// logger, and <c>AvaloniaLogger.Sink</c> is a static that any later
    /// framework code may read once and cache.
    /// </para>
    /// </summary>
    /// <param name="options">Configuration. At minimum
    /// <see cref="AvaloniaDiagnosticsOptions.AppName"/> and
    /// <see cref="AvaloniaDiagnosticsOptions.LogsDirectory"/> must be
    /// supplied; everything else has a safe default.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> is <c>null</c>.
    /// </exception>
    public static void ConfigureLogging(AvaloniaDiagnosticsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (_options is not null)
        {
            return; // idempotent
        }

        _options = options;

        Directory.CreateDirectory(options.LogsDirectory);

        _fileSink = new BucketedRollingFileSink(
            options.LogsDirectory,
            options.BucketSize,
            options.Retention,
            options.FileNamePrefix);

        if (options.EnableEventLogFile)
        {
            _eventFileSink = new BucketedRollingFileSink(
                options.LogsDirectory,
                options.BucketSize,
                options.Retention,
                options.EventLogFileNamePrefix ?? "events");

            // Verbose minimum: the host decides what is worth enqueuing, not a level filter.
            _eventLogger = new LoggerConfiguration()
                           .MinimumLevel.Verbose()
                           .WriteTo.Sink(_eventFileSink)
                           .CreateLogger();
        }

        LoggerConfiguration config = new LoggerConfiguration()
                                     .MinimumLevel.Is(options.MinimumLevel)
                                     .Enrich.FromLogContext()
                                     .WriteTo.Sink(_fileSink);

        if (options.EnableTraceSink)
        {
            config = config.WriteTo.Trace();
        }

        // The consumer's extension point: after the built-in sinks, before the logger exists.
        // WriteTo/Enrich mutate this same configuration, so the callback's return is not needed.
        options.ConfigureLogger?.Invoke(config);

        Log.Logger = config.CreateLogger();

        if (options.BridgeAvaloniaLogger)
        {
            AvaloniaLogger.Sink = new SerilogAvaloniaSink(options.MutedAvaloniaAreas);
        }
    }

    /// <summary>
    /// Performs the post-framework-init half of the bootstrap:
    /// <list type="bullet">
    ///   <item>Installs <see cref="BindingValidationErrorLogger"/> (when
    ///         <see cref="AvaloniaDiagnosticsOptions.EnableBindingValidationLogger"/>
    ///         is set) to catch binding-coercion errors via the
    ///         <c>DataValidationErrors.ErrorsProperty</c> attached-property
    ///         pipeline.</item>
    ///   <item>Subscribes the handler for exceptions nothing on the UI thread caught
    ///         to <see cref="Dispatcher.UIThread"/>'s
    ///         <see cref="Dispatcher.UnhandledException"/>, and records that dispatcher (when
    ///         <see cref="AvaloniaDiagnosticsOptions.HandleUiThreadExceptions"/> or
    ///         <see cref="AvaloniaDiagnosticsOptions.ShowUnobservedTaskExceptions"/> is set,
    ///         as both are by default). With the first set, each exception is logged, and
    ///         shown unless it is benign, and the app keeps running. Either way, each is
    ///         remembered, so that Avalonia's second report of it is known, and the dialog
    ///         for a task exception nobody observed opens on that dispatcher.</item>
    /// </list>
    /// <para>
    /// <strong>Must be called from <c>App.OnFrameworkInitializationCompleted</c></strong>
    /// (or equivalent) <em>after</em> the framework has initialised, so the
    /// binding plugin list is in place before it is modified, and the UI thread's
    /// dispatcher is the app's.
    /// </para>
    /// <para>
    /// Must be preceded by a call to <see cref="ConfigureLogging"/>. When the
    /// options instance from that call is not available, this method is a
    /// no-op. Each hook is installed once, however often this is called.
    /// </para>
    /// <para>
    /// ⚠ An app's own UI-thread handler subscribed after this call runs after the
    /// package's, and finds every exception already handled;
    /// <see cref="AvaloniaDiagnosticsOptions.HandleUiThreadExceptions"/> says what
    /// that costs and the ways out.
    /// </para>
    /// </summary>
    public static void InstallAvaloniaHooks()
    {
        if (_options is null)
        {
            return;
        }

        AvaloniaDiagnosticsOptions options = _options;

        if (options.EnableBindingValidationLogger)
        {
            BindingValidationErrorLogger.Install();
        }

        // ⛔ One subscription whenever either switch is on (plans/00003, decision 4): the second report of
        // an exception an app's own handler dealt with is known only to a handler that saw it.
        if (options.HandleUiThreadExceptions || options.ShowUnobservedTaskExceptions)
        {
            UiThreadExceptions.Install(Dispatcher.UIThread, handle: options.HandleUiThreadExceptions);
        }
    }

    /// <summary>
    /// Appends one line to the host-fed event log (enabled via
    /// <see cref="AvaloniaDiagnosticsOptions.EnableEventLogFile"/>). Thread-safe,
    /// non-blocking, and a no-op when the file was not enabled. Independent of the
    /// Serilog pipeline — use it for host-domain events (e.g. file-watcher hits) that
    /// should be recorded without polluting the log.
    /// </summary>
    public static void EnqueueEvent(string line)
    {
        // The durable half: the file survives the session and can be handed to someone else.
        _eventLogger?.Information("{EventLine}", line);

        // The live half, if the host attached one. Independent of the file: either, both or
        // neither. A listener's exception is swallowed -- see EventListener's remarks.
        if (_options?.EventListener is { } listener)
        {
            try
            {
                listener(line);
            }
            catch
            {
                // Deliberately swallowed: this call is a diagnostic side channel.
            }
        }
    }

    /// <summary>
    /// Returns this static bootstrap to its unconfigured state, so a test can call
    /// <see cref="ConfigureLogging"/> again. Disposes the file sinks it created, and takes the
    /// UI-thread handler off the dispatcher it was installed on, with its gate and counts reset.
    /// </summary>
    /// <remarks>
    /// ⚠ Test-only. It does not restore <see cref="Log.Logger"/> or Avalonia's logger sink, which
    /// the caller owns; a test that configures logging must put those back itself.
    /// </remarks>
    internal static void ResetForTests()
    {
        UiThreadExceptions.ResetForTests();
        _eventLogger?.Dispose();
        _eventLogger = null;
        _eventFileSink?.Dispose();
        _eventFileSink = null;
        _fileSink?.Dispose();
        _fileSink = null;
        _options = null;
    }

    /// <summary>
    /// Shows a <see cref="FatalErrorDialog"/> using <see cref="AppName"/> for
    /// the window title. Convenience wrapper for host crash handlers;
    /// identical to calling <see cref="FatalErrorDialog.ShowSafe"/> with a
    /// caller-built title. Safe to call at any time — if
    /// <see cref="ConfigureLogging"/> has not run the title falls back to a
    /// neutral <c>"Application Error"</c>.
    /// </summary>
    /// <param name="message">User-facing header shown above the exception
    /// details. <c>null</c> uses
    /// <see cref="FatalErrorDialog.DefaultMessage"/>.</param>
    /// <param name="exception">The exception whose
    /// <see cref="Exception.ToString"/> output is displayed.</param>
    public static void ShowFatalErrorDialog(string? message, Exception exception)
    {
        FatalErrorDialog.ShowSafe(FatalErrorDialogTitle, message ?? FatalErrorDialog.DefaultMessage, exception);
    }

    /// <summary>
    /// Shows a modeless, NON-fatal <see cref="NonFatalNoticeDialog"/> with a
    /// copyable body. Convenience wrapper for host informational notices (e.g.
    /// "these settings have no structured editor"); identical to calling
    /// <see cref="NonFatalNoticeDialog.ShowSafe"/>. Safe to call at any time and
    /// from any thread.
    /// </summary>
    /// <param name="title">Window title.</param>
    /// <param name="message">User-facing header shown above the body.</param>
    /// <param name="bodyText">Copyable body (e.g. a newline-separated list).</param>
    public static void ShowNonFatalNotice(string title, string message, string bodyText)
    {
        NonFatalNoticeDialog.ShowSafe(title, message, bodyText);
    }

    /// <summary>
    /// Shows a native OS error dialog (last-resort crash surface for the case
    /// where the Avalonia runtime is dead). Convenience wrapper for host
    /// crash handlers; identical to calling
    /// <see cref="NativeErrorDialog.ShowFatalError"/> with a caller-built title.
    /// </summary>
    /// <param name="message">Text to display in the dialog body.</param>
    public static void ShowNativeFatalError(string message)
    {
        string title = string.IsNullOrWhiteSpace(AppName)
            ? "Application Error"
            : $"{AppName} — Critical Error";
        NativeErrorDialog.ShowFatalError(title, message);
    }

    /// <summary>
    /// The options <see cref="AppMain.RunDesktop"/> takes in an Avalonia app: a failure is logged as
    /// fatal and shown in the native fatal-error dialog; a task exception nobody observed is logged at
    /// Debug when it is benign, and otherwise at Error and shown in a <see cref="FatalErrorDialog"/>, or
    /// at Warning and not shown when <see cref="AvaloniaDiagnosticsOptions.ShowUnobservedTaskExceptions"/>
    /// is off, unless it is Avalonia's second report of an exception the UI thread let escape, which is
    /// neither logged, shown nor written on stderr; and on every exit the pending repeat counts are
    /// written, then the log is closed and flushed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>The wiring lives here so that a package bump fixes it in every app.</b> Written into each
    /// app's <c>Program.cs</c> instead, it would be copied from the template once and never updated.
    /// </para>
    /// <para>
    /// ⭐ <b>The host's one filter is passed on.</b> The options' <c>IsBenignException</c> reads
    /// <see cref="AvaloniaDiagnosticsOptions.IsBenignException"/> when an exception arrives, not when the
    /// options are built: they are built in <c>Main</c>, before <see cref="ConfigureLogging"/> has run,
    /// and until it has, the host's filter calls nothing benign. The entry point keeps a benign task
    /// exception nobody observed off stderr, and the callback logs it at Debug and shows nothing.
    /// </para>
    /// <para>
    /// ⭐ <b>A task exception nobody observed is shown as a UI-thread failure is</b> (<c>plans/00003</c>):
    /// a background save, backup or fetch that fails unobserved is otherwise silent in a desktop app.
    /// With <see cref="AvaloniaDiagnosticsOptions.ShowUnobservedTaskExceptions"/> on, the callback logs
    /// one that is not benign at Error and shows it through the UI-thread handler's gate and repeat
    /// counting: one dialog at a time, and one for each failure, whichever path reports it. The failure
    /// is the exception the task failed with, when it failed with exactly one. The callback runs on the
    /// finalizer thread and posts the dialog to the dispatcher <see cref="InstallAvaloniaHooks"/>
    /// recorded, never waiting for it; before that has run, and once the app has shut down, the
    /// exception is logged and not shown. With the switch off, it is logged at Warning and not shown.
    /// </para>
    /// <para>
    /// ⚠ <b>On this path a cancellation is benign.</b> The filter the options pass on also calls an
    /// <see cref="OperationCanceledException"/> benign, <see cref="TaskCanceledException"/> included, so a
    /// task exception nobody observed that is nothing but cancellations is logged at Debug, shown nothing
    /// and kept off stderr, whatever the switch says: by the time a garbage collection finds it, it is
    /// abandoned work, not a failure anyone can act on. One beside a real failure is not benign. The
    /// UI-thread handler does not read this rule, so a cancellation that escapes on the UI thread is the
    /// host's filter's alone to call benign.
    /// </para>
    /// <para>
    /// ⚠ <b>Avalonia reports a UI-thread exception twice.</b> Avalonia 12.1.3 also puts every exception a
    /// posted job throws in that job's task, which nothing reads, so the runtime raises it again, at a
    /// later garbage collection, as a task exception nobody observed. The options know that second report
    /// of whatever the UI thread let escape, whoever handled it, while either switch is on: the filter
    /// they pass on calls it benign, which keeps it off stderr, and their callback neither logs nor shows
    /// it, so the report on the UI thread is the only one. An empty aggregate thrown on the UI thread
    /// flattens to nothing, so the entry point reports its second report before any filter runs.
    /// </para>
    /// <para>
    /// ⚠ The fatal event is logged before the dialog opens, since the dialog blocks until it is
    /// dismissed and the log is the record that survives. Before <see cref="ConfigureLogging"/> has run,
    /// <see cref="Log.Logger"/> discards the event, and the dialog and the entry point's own report on
    /// stderr are what remain.
    /// </para>
    /// </remarks>
    /// <returns>New options on every call: a record, so a host changes one member with <c>with</c>.</returns>
    public static AppMainOptions EntryPointOptions() => new()
    {
        OnFatal = exception =>
        {
            Log.Fatal(exception, "{AppName} stopped on an unhandled exception", AppName ?? "The application");
            ShowNativeFatalError(exception.ToString());
        },
        OnUnobservedTaskException = LogUnobservedTaskException,
        FlushLog = () =>
        {
            try
            {
                UiThreadExceptions.WritePendingRepeats();
            }
            finally
            {
                Log.CloseAndFlush();
            }
        },
        IsBenignException = IsBenignUnobserved,
    };

    // The filter the options pass on, which decides what reaches stderr: whatever the UI thread let
    // escape, whose second report Avalonia raises as a task exception nobody observed and which is no new
    // failure; a cancellation, which on this path is abandoned work (plans/00003, decision 7); and what
    // the host's filter calls benign.
    private static bool IsBenignUnobserved(Exception exception) =>
        UiThreadExceptions.WasSeen(exception)
        || exception is OperationCanceledException
        || (BenignExceptionFilter is { } filter && filter(exception));

    // The callback is given the exception without the entry point's verdict, so it applies the rule
    // again, with the same filter, to choose what to do. Avalonia's second report of an exception the UI
    // thread let escape is neither logged nor shown: the report on the UI thread was the only one due.
    private static void LogUnobservedTaskException(Exception exception)
    {
        if (IsSecondReport(exception))
        {
            return;
        }

        bool benign = BenignExceptionRule.IsBenign(IsBenignUnobserved, exception, out Exception? filterFailure);
        if (benign)
        {
            Log.Debug(exception, "A benign task exception nobody observed");
            return;
        }

        if (ShowsUnobservedTaskExceptions)
        {
            UiThreadExceptions.ReportUnobserved(exception, filterFailure);
            return;
        }

        Log.Warning(exception, "A task exception nobody observed");
        if (filterFailure is not null)
        {
            Log.Warning(filterFailure,
                "The IsBenignException filter threw on the task exception above, so that exception counts as not benign");
        }
    }

    // A task exception nobody observed arrives wrapped, and Avalonia's second report wraps exactly the
    // exception the dispatcher reported; an aggregate it reported is known itself, empty or not.
    private static bool IsSecondReport(Exception exception) =>
        UiThreadExceptions.WasSeen(exception)
        || (exception is AggregateException { InnerExceptions.Count: > 0 } aggregate
            && aggregate.InnerExceptions.All(UiThreadExceptions.WasSeen));
}