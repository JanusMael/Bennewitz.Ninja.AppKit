using AppServices.Tests.BenignExceptions;
using Bennewitz.Ninja.AppServices.AvaloniaUI;
using Bennewitz.Ninja.AppServices.Dialogs;
using Bennewitz.Ninja.AppServices.EntryPoint;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AppServices.Tests.AvaloniaUI;

/// <summary>
/// <see cref="AvaloniaDiagnostics.EntryPointOptions"/>: what an Avalonia app's entry point does with a
/// failure, with a task exception nobody observed, and with its log, and the filter it passes on.
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
/// </remarks>
public sealed class AvaloniaDiagnosticsEntryPointTests : IDisposable
{
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
        Assert.Same(failure, logged.Exception);
        Assert.Equal("entry-tests — Critical Error", NativeErrorDialog.LastSuppressedCall.Title);
        Assert.Contains("the window failed to open", NativeErrorDialog.LastSuppressedCall.Message);
    }

    [Fact]
    public void A_task_exception_nobody_observed_is_logged_as_a_warning()
    {
        InvalidOperationException unobserved = new("nobody awaited this");

        AvaloniaDiagnostics.EntryPointOptions().OnUnobservedTaskException!(unobserved);

        LogEvent logged = Assert.Single(_sink.Events);
        Assert.Equal(LogEventLevel.Warning, logged.Level);
        Assert.Same(unobserved, logged.Exception);
        Assert.Null(NativeErrorDialog.LastSuppressedCall.Title);
    }

    [Fact]
    public void A_desktop_run_that_fails_at_start_up_logs_it_shows_the_dialog_and_exits_1()
    {
        // The case bbavalonia's own Main never showed a dialog for: a failure escaping start-up.
        int code = AppMain.RunDesktop(typeof(AvaloniaDiagnosticsEntryPointTests).Assembly, [],
            _ => throw new InvalidOperationException("start-up failed"),
            AvaloniaDiagnostics.EntryPointOptions());

        Assert.Equal(1, code);
        Assert.Contains(_sink.Events, e => e.Level == LogEventLevel.Fatal);
        Assert.Contains("start-up failed", NativeErrorDialog.LastSuppressedCall.Message);
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
    public void The_callback_logs_an_aggregate_wrapping_a_benign_exception_at_Debug()
    {
        Reconfigure(BenignExceptionTable.Filter);
        AggregateException unobserved = new(new BenignExceptionTable.BenignNoise());

        AvaloniaDiagnostics.EntryPointOptions().OnUnobservedTaskException!(unobserved);

        LogEvent logged = Assert.Single(_sink.Events);
        Assert.Equal(LogEventLevel.Debug, logged.Level);
        Assert.Same(unobserved, logged.Exception);
        Assert.Null(NativeErrorDialog.LastSuppressedCall.Title);
    }

    [Fact]
    public void The_callback_logs_an_aggregate_wrapping_a_non_benign_exception_at_Warning()
    {
        Reconfigure(BenignExceptionTable.Filter);
        AggregateException unobserved = new(new InvalidOperationException(BenignExceptionTable.NotBenign));

        AvaloniaDiagnostics.EntryPointOptions().OnUnobservedTaskException!(unobserved);

        LogEvent logged = Assert.Single(_sink.Events);
        Assert.Equal(LogEventLevel.Warning, logged.Level);
        Assert.Same(unobserved, logged.Exception);
        Assert.Null(NativeErrorDialog.LastSuppressedCall.Title);
    }

    [Theory]
    [MemberData(nameof(BenignExceptionTable.AllRows), MemberType = typeof(BenignExceptionTable))]
    public void The_callback_logs_every_row_of_the_shared_table_at_the_level_its_verdict_gives(string row)
    {
        BenignExceptionTable.Row expected = BenignExceptionTable.Get(row);
        Reconfigure(BenignExceptionTable.Filter);
        // Wrapped, as the runtime raises a task exception nobody observed.
        AggregateException unobserved = new(expected.Make());

        AvaloniaDiagnostics.EntryPointOptions().OnUnobservedTaskException!(unobserved);

        LogEvent logged = Assert.Single(_sink.Events, e => ReferenceEquals(e.Exception, unobserved));
        Assert.Equal(expected.Benign ? LogEventLevel.Debug : LogEventLevel.Warning, logged.Level);
        LogEvent[] others = [.. _sink.Events.Where(e => !ReferenceEquals(e, logged))];
        if (expected.FilterThrows)
        {
            LogEvent filterThrew = Assert.Single(others);
            Assert.Equal(LogEventLevel.Warning, filterThrew.Level);
            Assert.Equal(BenignExceptionTable.FilterBug, filterThrew.Exception?.Message);
        }
        else
        {
            Assert.Empty(others);
        }
    }

    /// <summary>Configures logging into <see cref="_sink"/>; with a filter, at Debug.</summary>
    private void Configure(Func<Exception, bool>? filter = null) =>
        AvaloniaDiagnostics.ConfigureLogging(new AvaloniaDiagnosticsOptions
        {
            AppName = "entry-tests",
            LogsDirectory = _logs,
            MinimumLevel = filter is null ? LogEventLevel.Information : LogEventLevel.Debug,
            EnableTraceSink = false,
            // Avalonia's logger sink is process-wide and not the subject here.
            BridgeAvaloniaLogger = false,
            IsBenignException = filter,
            ConfigureLogger = configuration => configuration.WriteTo.Sink(_sink),
        });

    /// <summary>Configures logging again, ConfigureLogging being configure-once.</summary>
    private void Reconfigure(Func<Exception, bool> filter)
    {
        AvaloniaDiagnostics.ResetForTests();
        Configure(filter);
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
