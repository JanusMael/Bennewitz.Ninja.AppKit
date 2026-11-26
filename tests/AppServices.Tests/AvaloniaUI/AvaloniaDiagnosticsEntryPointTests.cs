using AppServices.Tests.BenignExceptions;
using AppServices.Tests.Logging;
using Bennewitz.Ninja.AppServices.AvaloniaUI;
using Bennewitz.Ninja.AppServices.Dialogs;
using Bennewitz.Ninja.AppServices.EntryPoint;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AppServices.Tests.AvaloniaUI;

/// <summary>
/// <see cref="AvaloniaDiagnostics.EntryPointOptions"/>: what an Avalonia app's entry point does with a
/// failure, with a task exception nobody observed, with its own two lines and with its log, and the
/// filter it passes on.
/// </summary>
/// <remarks>
/// <para>
/// ⚠ The native dialog is suppressed and recorded (<c>NativeErrorDialog.SuppressForTests</c>), and the
/// static bootstrap is reset around every test, as <see cref="AvaloniaDiagnosticsHookTests"/> does.
/// This assembly runs serially (Parallelization.cs), which is what makes that safe.
/// </para>
/// <para>
/// The tests of the filter configure again, with <see cref="BenignExceptionTable"/>'s filter and the
/// minimum level at Debug, so that a benign exception's line can be seen.
/// </para>
/// <para>
/// ⭐ The tests of the held start line reset the bootstrap and leave <see cref="Log.Logger"/> as the
/// constructor's logger, which writes into the same sink: a line the options wrote at once, where they
/// should have held it, would be seen there.
/// </para>
/// </remarks>
public sealed class AvaloniaDiagnosticsEntryPointTests : IDisposable
{
    private const string AppName = "AppServices.Tests";

    private readonly ILogger _originalLogger = Log.Logger;
    private readonly TextWriter _originalError = Console.Error;
    private readonly string _logs =
        Path.Combine(Path.GetTempPath(), "bb-appservices-entry-" + Guid.NewGuid().ToString("N"));
    private readonly CollectingSink _sink = new();

    public AvaloniaDiagnosticsEntryPointTests()
    {
        AvaloniaDiagnostics.ResetForTests();
        NativeErrorDialog.SuppressForTests = true;

        // The entry point's own report goes to stderr; it is not the subject here.
        Console.SetError(new StringWriter());

        Configure();
    }

    public void Dispose()
    {
        Console.SetError(_originalError);
        NativeErrorDialog.Reset();
        Log.Logger = _originalLogger;
        AvaloniaDiagnostics.ResetForTests();
        try
        {
            Directory.Delete(_logs, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }

    [Fact]
    public void A_failure_is_logged_as_fatal_and_shown_in_the_native_dialog()
    {
        InvalidOperationException failure = new("the window failed to open");

        AvaloniaDiagnostics.EntryPointOptions().OnFatal!(failure);

        LogEvent logged = Assert.Single(_sink.Events, e => e.Level == LogEventLevel.Fatal);
        LogLineForm.Is(logged, LogEventLevel.Fatal, PackageLogLines.AppFatal, failure);
        Assert.Equal("entry-tests — Critical Error", NativeErrorDialog.LastSuppressedCall.Title);
        Assert.Contains("the window failed to open", NativeErrorDialog.LastSuppressedCall.Message);
    }

    [Fact]
    public void A_task_exception_nobody_observed_is_logged_at_Error()
    {
        InvalidOperationException unobserved = new("nobody awaited this");

        AvaloniaDiagnostics.EntryPointOptions().OnUnobservedTaskException!(unobserved);

        LogLineForm.Is(Assert.Single(_sink.Events), LogEventLevel.Error, PackageLogLines.TaskUnobserved, unobserved);
        Assert.Null(NativeErrorDialog.LastSuppressedCall.Title);
    }

    [Fact]
    public void With_ShowUnobservedTaskExceptions_off_a_task_exception_nobody_observed_is_logged_as_a_warning()
    {
        AvaloniaDiagnostics.ResetForTests();
        Configure(show: false);
        InvalidOperationException unobserved = new("nobody awaited this");

        AvaloniaDiagnostics.EntryPointOptions().OnUnobservedTaskException!(unobserved);

        LogLineForm.Is(Assert.Single(_sink.Events), LogEventLevel.Warning, PackageLogLines.TaskUnobserved, unobserved);
        Assert.Null(NativeErrorDialog.LastSuppressedCall.Title);
    }

    [Fact]
    public void A_desktop_run_that_fails_at_start_up_logs_it_shows_the_dialog_and_exits_1()
    {
        // The case bbavalonia's own Main never showed a dialog for: a failure escaping start-up.
        InvalidOperationException failure = new("start-up failed");
        CollectBeforeTheRun();
        int code = AppMain.RunDesktop(typeof(AvaloniaDiagnosticsEntryPointTests).Assembly, [],
            _ => throw failure,
            AvaloniaDiagnostics.EntryPointOptions());

        Assert.Equal(1, code);

        // With the log made before the run, both of the entry point's lines are written at once.
        Assert.Equal(
            [PackageLogLines.AppStart, PackageLogLines.AppFatal, PackageLogLines.AppExitWithCode],
            _sink.Events.Select(e => e.MessageTemplate.Text));
        LogLineForm.Is(_sink.Events[1], LogEventLevel.Fatal, PackageLogLines.AppFatal, failure);
        Assert.Equal(1, LogLineForm.ValueOf(_sink.Events[2], "Code"));
        Assert.Contains("start-up failed", NativeErrorDialog.LastSuppressedCall.Message);
    }

    [Fact]
    public void A_run_that_makes_its_log_in_its_work_opens_it_with_its_start_line_and_ends_it_with_its_exit_line_after_every_repeat_count()
    {
        // Built in Main, before the log is made, as bbavalonia builds them.
        AvaloniaDiagnostics.ResetForTests();
        AppMainOptions options = AvaloniaDiagnostics.EntryPointOptions();
        int seenBeforeTheLog = -1;

        CollectBeforeTheRun();
        int code = AppMain.RunDesktop(typeof(AvaloniaDiagnosticsEntryPointTests).Assembly, [], _ =>
        {
            // The start line is held: the constructor's logger, still Log.Logger, would have seen it written.
            seenBeforeTheLog = _sink.Events.Count;
            Configure();

            // One failure reported twice, whose second report is a repeat still pending when the run ends.
            UiThreadExceptions.ReportUnobserved(new AggregateException(Unobserved()), null);
            UiThreadExceptions.ReportUnobserved(new AggregateException(Unobserved()), null);
            return 0;
        }, options);

        Assert.Equal(0, code);
        Assert.Equal(0, seenBeforeTheLog);
        Assert.Equal(
            [PackageLogLines.AppStart, PackageLogLines.TaskUnobserved, PackageLogLines.FailureRepeat, PackageLogLines.AppExitWithCode],
            _sink.Events.Select(e => e.MessageTemplate.Text));
        Assert.All(_sink.Events, LogLineForm.Holds);

        LogEvent start = _sink.Events[0];
        Assert.Equal(LogEventLevel.Information, start.Level);
        Assert.Equal(AppName, LogLineForm.ValueOf(start, "App"));
        Assert.Equal(Environment.ProcessId, LogLineForm.ValueOf(start, "Pid"));
        Assert.Equal(1L, LogLineForm.ValueOf(_sink.Events[2], "Count"));

        // A failure never thrown has no throw site, which its line writes as a value the package does not have.
        Assert.Equal("unknown", LogLineForm.ValueOf(_sink.Events[2], "Site"));
        Assert.Equal(0, LogLineForm.ValueOf(_sink.Events[3], "Code"));
    }

    [Fact]
    public void A_held_start_line_is_written_once_as_the_first_line_of_the_log_ConfigureLogging_makes()
    {
        AvaloniaDiagnostics.ResetForTests();
        LogLineWriter write = AvaloniaDiagnostics.EntryPointOptions().WriteLogLine!;

        write(PackageLogLines.AppStart, AppName, "2026.4.1126", 4242);

        Assert.Empty(_sink.Events);

        Configure();
        write(PackageLogLines.AppExitWithCode, 0, 12L);
        Configure();

        Assert.Equal(
            [PackageLogLines.AppStart, PackageLogLines.AppExitWithCode],
            _sink.Events.Select(e => e.MessageTemplate.Text));
        Assert.Equal(4242, LogLineForm.ValueOf(_sink.Events[0], "Pid"));
    }

    [Fact]
    public void A_start_line_whose_run_ends_before_its_log_is_made_is_never_written_into_a_later_log()
    {
        AvaloniaDiagnostics.ResetForTests();
        LogLineWriter write = AvaloniaDiagnostics.EntryPointOptions().WriteLogLine!;

        // A run that never made its log: its exit line drops its start line.
        write(PackageLogLines.AppStart, AppName, "2026.4.1126", 4242);
        write(PackageLogLines.AppExitWithCode, 0, 12L);

        Configure();

        Assert.DoesNotContain(_sink.Events, e => e.MessageTemplate.Text == PackageLogLines.AppStart);
    }

    [Fact]
    public void ResetForTests_drops_a_held_start_line()
    {
        AvaloniaDiagnostics.ResetForTests();
        LogLineWriter write = AvaloniaDiagnostics.EntryPointOptions().WriteLogLine!;
        write(PackageLogLines.AppStart, AppName, "2026.4.1126", 4242);

        AvaloniaDiagnostics.ResetForTests();
        Configure();

        Assert.Empty(_sink.Events);
    }

    [Fact]
    public void Once_the_log_is_made_each_line_is_written_at_once_and_an_exit_line_after_the_repeat_counts_pending_then()
    {
        LogLineWriter write = AvaloniaDiagnostics.EntryPointOptions().WriteLogLine!;
        write(PackageLogLines.AppStart, AppName, "2026.4.1126", 4242);
        UiThreadExceptions.ReportUnobserved(new AggregateException(Unobserved()), null);
        UiThreadExceptions.ReportUnobserved(new AggregateException(Unobserved()), null);

        write(PackageLogLines.AppExitWithCode, 0, 12L);
        AvaloniaDiagnostics.EntryPointOptions().FlushLog!();

        // The flush finds no count left to write after the exit line.
        Assert.Equal(
            [PackageLogLines.AppStart, PackageLogLines.TaskUnobserved, PackageLogLines.FailureRepeat, PackageLogLines.AppExitWithCode],
            _sink.Events.Select(e => e.MessageTemplate.Text));
    }

    [Fact]
    public void The_filter_reads_false_before_ConfigureLogging_and_the_host_s_answer_after_it()
    {
        // Built as an app builds them, in Main, before logging is configured.
        AvaloniaDiagnostics.ResetForTests();
        AppMainOptions options = AvaloniaDiagnostics.EntryPointOptions();

        Assert.False(options.IsBenignException!(new BenignExceptionTable.BenignNoise()));

        Configure(BenignExceptionTable.Filter);

        Assert.True(options.IsBenignException!(new BenignExceptionTable.BenignNoise()));
        Assert.False(options.IsBenignException!(new InvalidOperationException(BenignExceptionTable.NotBenign)));
    }

    [Fact]
    public void The_filter_calls_a_cancellation_benign_without_asking_the_host_s_filter()
    {
        List<Exception> asked = [];
        Reconfigure(exception =>
        {
            asked.Add(exception);
            return false;
        });
        AppMainOptions options = AvaloniaDiagnostics.EntryPointOptions();

        Assert.True(options.IsBenignException!(new OperationCanceledException(BenignExceptionTable.Cancelled)));
        Assert.True(options.IsBenignException!(new TaskCanceledException(BenignExceptionTable.Cancelled)));
        Assert.Empty(asked);

        // The control: anything else is the host's filter's to call.
        InvalidOperationException other = new(BenignExceptionTable.NotBenign);
        Assert.False(options.IsBenignException!(other));
        Assert.Same(other, Assert.Single(asked));
    }

    [Fact]
    public void The_callback_logs_an_aggregate_wrapping_a_benign_exception_at_Debug()
    {
        Reconfigure(BenignExceptionTable.Filter);
        AggregateException unobserved = new(new BenignExceptionTable.BenignNoise());

        AvaloniaDiagnostics.EntryPointOptions().OnUnobservedTaskException!(unobserved);

        LogLineForm.Is(Assert.Single(_sink.Events), LogEventLevel.Debug, PackageLogLines.TaskUnobservedBenign, unobserved);
        Assert.Null(NativeErrorDialog.LastSuppressedCall.Title);
    }

    [Fact]
    public void The_callback_logs_an_aggregate_wrapping_a_non_benign_exception_at_Error()
    {
        Reconfigure(BenignExceptionTable.Filter);
        AggregateException unobserved = new(new InvalidOperationException(BenignExceptionTable.NotBenign));

        AvaloniaDiagnostics.EntryPointOptions().OnUnobservedTaskException!(unobserved);

        LogLineForm.Is(Assert.Single(_sink.Events), LogEventLevel.Error, PackageLogLines.TaskUnobserved, unobserved);
        Assert.Null(NativeErrorDialog.LastSuppressedCall.Title);
    }

    [Theory]
    [MemberData(nameof(BenignExceptionTable.AllRowsWithShowOnAndOff), MemberType = typeof(BenignExceptionTable))]
    public void The_callback_logs_every_row_of_the_shared_table_at_the_level_its_verdict_gives(string row, bool show)
    {
        BenignExceptionTable.Row expected = BenignExceptionTable.Get(row);
        Reconfigure(BenignExceptionTable.Filter, show);
        // Wrapped, as the runtime raises a task exception nobody observed.
        AggregateException unobserved = new(expected.Make());

        AvaloniaDiagnostics.EntryPointOptions().OnUnobservedTaskException!(unobserved);

        LogEvent logged = Assert.Single(_sink.Events, e => ReferenceEquals(e.Exception, unobserved));
        LogEventLevel notBenign = show ? LogEventLevel.Error : LogEventLevel.Warning;
        if (expected.BenignUnobserved)
        {
            LogLineForm.Is(logged, LogEventLevel.Debug, PackageLogLines.TaskUnobservedBenign, unobserved);
        }
        else
        {
            LogLineForm.Is(logged, notBenign, PackageLogLines.TaskUnobserved, unobserved);
        }

        LogEvent[] others = [.. _sink.Events.Where(e => !ReferenceEquals(e, logged))];
        if (expected.FilterThrows)
        {
            LogEvent filterThrew = Assert.Single(others);
            Assert.Equal(PackageLogLines.TaskBenignFilterThrew, filterThrew.MessageTemplate.Text);
            Assert.Equal(LogEventLevel.Warning, filterThrew.Level);
            Assert.Equal(BenignExceptionTable.FilterBug, filterThrew.Exception?.Message);
            LogLineForm.Holds(filterThrew);
        }
        else
        {
            Assert.Empty(others);
        }
    }

    /// <summary>A fresh exception that is one failure with every other one it returns: never thrown, as a task's never is here.</summary>
    private static InvalidOperationException Unobserved() => new("a background task failed, and nobody awaited it");

    /// <summary>
    /// Raises now whatever an earlier test left unobserved, before a run hooks in: its line would otherwise
    /// land among the run's (tests/AGENTS.md).
    /// </summary>
    private static void CollectBeforeTheRun()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    /// <summary>
    /// Configures logging into <see cref="_sink"/>; with a filter, at Debug. No UI thread's dispatcher is
    /// recorded here, so a task exception's dialog is never posted: what it shows is tested in
    /// <c>UiThreadExceptionHandlerTests</c>.
    /// </summary>
    private void Configure(Func<Exception, bool>? filter = null, bool show = true) =>
        AvaloniaDiagnostics.ConfigureLogging(new AvaloniaDiagnosticsOptions
        {
            AppName = "entry-tests",
            LogsDirectory = _logs,
            MinimumLevel = filter is null ? LogEventLevel.Information : LogEventLevel.Debug,
            EnableTraceSink = false,
            // Avalonia's logger sink is process-wide and not the subject here.
            BridgeAvaloniaLogger = false,
            ShowUnobservedTaskExceptions = show,
            IsBenignException = filter,
            ConfigureLogger = configuration => configuration.WriteTo.Sink(_sink),
        });

    /// <summary>Configures logging again, ConfigureLogging being configure-once.</summary>
    private void Reconfigure(Func<Exception, bool> filter, bool show = true)
    {
        AvaloniaDiagnostics.ResetForTests();
        Configure(filter, show);
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
