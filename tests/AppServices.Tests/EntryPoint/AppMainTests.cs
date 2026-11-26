using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using AppServices.Tests.BenignExceptions;
using Bennewitz.Ninja.AppServices.EntryPoint;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AppServices.Tests.EntryPoint;

/// <summary>
/// <see cref="AppMain"/> in process: the exit code for each way a run ends, what each writes and
/// where, its two log lines, and the hooks a run installs and removes.
/// </summary>
/// <remarks>
/// <para>
/// ⚠ Each test swaps <see cref="Console.Out"/> and <see cref="Console.Error"/> for writers it reads and
/// puts them back in <see cref="Dispose"/>. Both are process-wide, which is safe only because this
/// assembly runs serially (Parallelization.cs).
/// </para>
/// <para>
/// ⛔ Ctrl+C is pressed through <c>EntryRun.Current.PressCtrlC</c>, the seam the real handler calls.
/// <c>ConsoleCancelEventArgs</c> has no public constructor, and a real SIGINT reaches only a child
/// process (<see cref="EntryPointProbeTests"/>), and only on Linux and macOS. In the same way SIGTERM,
/// an exit no <c>finally</c> sees and an exception ending the process from another thread reach a run
/// through <c>OnSigterm</c>, <c>OnProcessExit</c> and <c>OnUnhandledException</c>, which the real
/// registration and events call: each real one ends the test host, or reaches only a child process.
/// </para>
/// <para>
/// ⭐ The tests of the two log lines record, in order, each line a run writes, as its template and its
/// values, and each fatal report and each flush. The templates are held here by their text
/// (plans/00009, decision 1): read from the entry point's own constants, a changed one would pass.
/// </para>
/// <para>
/// ⭐ The tests of <see cref="AppMainOptions.IsBenignException"/> read <see cref="BenignExceptionTable"/>,
/// the one table both copies of the benign rule are held to. Each abandons a task faulted with a row's
/// exception and collects it, so the run's hook sees it as the runtime raises it, wrapped in an
/// <see cref="AggregateException"/>; anything abandoned before the run is collected before the run
/// hooks in.
/// </para>
/// </remarks>
public sealed class AppMainTests : IDisposable
{
    private const string StartLine = "[App.Start] app={App} version={Version} pid={Pid}";
    private const string ExitedWithCode = "[App.Exit] code={Code} elapsedMs={ElapsedMs}";
    private const string EndedBySigterm = "[App.Exit] signal=SIGTERM elapsedMs={ElapsedMs}";
    private const string EndedByUnhandledException = "[App.Exit] reason=unhandledException elapsedMs={ElapsedMs}";
    private const string StoppedByDesignTimeTool = "[App.Exit] reason=hostAborted elapsedMs={ElapsedMs}";

    // What a recorded run marks beside its lines.
    private const string WorkRan = "work";
    private const string OnFatalRan = "OnFatal";
    private const string Flushed = "FlushLog";

    private readonly TextWriter _originalOut = Console.Out;
    private readonly TextWriter _originalError = Console.Error;
    private readonly StringWriter _stdout = new();
    private readonly StringWriter _stderr = new();

    public AppMainTests()
    {
        Console.SetOut(_stdout);
        Console.SetError(_stderr);
    }

    /// <summary>The ways <see cref="RunEnding"/>'s work ends, in each kind, and the code each run returns.</summary>
    public static TheoryData<string, string, int> Endings => new()
    {
        { "console", "returns 7", 7 },
        { "console", "throws", 1 },
        { "console", "usage error", 2 },
        { "console", "Ctrl+C", 130 },
        { "host", "returns 7", 0 },
        { "host", "throws", 1 },
        { "desktop", "returns 7", 7 },
        { "desktop", "throws", 1 },
    };

    /// <summary>This test assembly: AutoVersioning stamps its PublicVersion, 1.0.0 on a local build.</summary>
    private static Assembly Versioned => typeof(AppMainTests).Assembly;

    /// <summary>The framework's core library, which carries no PublicVersion.</summary>
    private static Assembly Unversioned => typeof(object).Assembly;

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
    }

    [Fact]
    public async Task The_work_s_returned_code_is_the_exit_code()
    {
        int code = await AppMain.RunConsoleAsync(Versioned, [], (_, _) => Task.FromResult(7));

        Assert.Equal(7, code);
        Assert.Equal("", _stderr.ToString());
    }

    [Fact]
    public async Task An_unhandled_exception_is_reported_on_stderr_then_passed_to_OnFatal_and_exits_1()
    {
        InvalidOperationException thrown = new("the work failed");
        Exception? passed = null;
        string? stderrWhenOnFatalRan = null;

        int code = await AppMain.RunConsoleAsync(Versioned, [], (_, _) => throw thrown, new AppMainOptions
        {
            OnFatal = exception =>
            {
                passed = exception;
                stderrWhenOnFatalRan = _stderr.ToString();
            },
        });

        Assert.Equal(1, code);
        Assert.Same(thrown, passed);

        // ⛔ Already on stderr when OnFatal ran, so a failing logger or dialog cannot take the report with it.
        Assert.NotNull(stderrWhenOnFatalRan);
        Assert.StartsWith("fatal: AppServices.Tests stopped on an unhandled exception", stderrWhenOnFatalRan);
        Assert.Contains("the work failed", stderrWhenOnFatalRan);
        Assert.Equal("", _stdout.ToString());
    }

    [Fact]
    public async Task A_usage_error_prints_its_message_and_the_usage_on_stderr_and_exits_2()
    {
        bool fatal = false;

        int code = await AppMain.RunConsoleAsync(Versioned, ["--bogus"],
            (_, _) => throw new UsageException("unknown option: --bogus"),
            new AppMainOptions { Usage = "usage: app [--fail]", OnFatal = _ => fatal = true });

        Assert.Equal(2, code);
        string nl = Environment.NewLine;
        Assert.Equal($"error: unknown option: --bogus{nl}{nl}usage: app [--fail]{nl}", _stderr.ToString());
        Assert.False(fatal);
    }

    [Theory]
    [InlineData("host")]
    [InlineData("desktop")]
    public async Task A_usage_error_where_the_app_owns_no_command_line_is_a_failure(string kind)
    {
        int code = await Run(kind, Versioned, [], _ => throw new UsageException("no such option"));

        Assert.Equal(1, code);
        Assert.StartsWith("fatal: AppServices.Tests stopped on an unhandled exception", _stderr.ToString());
    }

    [Fact]
    public async Task Ctrl_C_cancels_the_token_and_exits_130()
    {
        int code = await AppMain.RunConsoleAsync(Versioned, [], async (_, token) =>
        {
            EntryRun.Current!.PressCtrlC();
            await Task.Delay(Timeout.Infinite, token);
            return 0;
        });

        Assert.Equal(130, code);
        Assert.Equal("", _stderr.ToString());
    }

    [Fact]
    public async Task After_Ctrl_C_a_returned_code_still_exits_130()
    {
        int code = await AppMain.RunConsoleAsync(Versioned, [], (_, _) =>
        {
            EntryRun.Current!.PressCtrlC();
            return Task.FromResult(0);
        });

        Assert.Equal(130, code);
    }

    [Fact]
    public async Task After_Ctrl_C_another_exception_is_reported_on_stderr_and_still_exits_130()
    {
        bool fatal = false;

        int code = await AppMain.RunConsoleAsync(Versioned, [], (_, _) =>
        {
            EntryRun.Current!.PressCtrlC();
            throw new InvalidOperationException("broke while stopping");
        }, new AppMainOptions { OnFatal = _ => fatal = true });

        Assert.Equal(130, code);
        Assert.StartsWith("fatal: AppServices.Tests stopped on an unhandled exception after Ctrl+C", _stderr.ToString());
        Assert.Contains("broke while stopping", _stderr.ToString());
        Assert.False(fatal);
    }

    [Fact]
    public async Task After_Ctrl_C_a_usage_error_still_exits_130()
    {
        // Ctrl+C outranks a usage error (plans/00001, decision 5): the run was cancelled, so it is
        // reported as one, and the usage is not printed.
        int code = await AppMain.RunConsoleAsync(Versioned, ["--bogus"], (_, _) =>
        {
            EntryRun.Current!.PressCtrlC();
            throw new UsageException("unknown option: --bogus");
        }, new AppMainOptions { Usage = "usage: app [--fail]" });

        Assert.Equal(130, code);
        Assert.StartsWith("fatal: AppServices.Tests stopped on an unhandled exception after Ctrl+C", _stderr.ToString());
        Assert.DoesNotContain("usage: app", _stderr.ToString());
    }

    [Fact]
    public async Task Only_the_first_Ctrl_C_is_intercepted_so_a_hung_run_can_still_be_killed()
    {
        List<bool> intercepted = [];

        await AppMain.RunConsoleAsync(Versioned, [], (_, _) =>
        {
            intercepted.Add(EntryRun.Current!.PressCtrlC());
            intercepted.Add(EntryRun.Current!.PressCtrlC());
            return Task.FromResult(0);
        });

        Assert.Equal([true, false], intercepted);
    }

    [Theory]
    [InlineData("console")]
    [InlineData("host")]
    [InlineData("desktop")]
    public async Task Version_prints_the_PublicVersion_on_stdout_without_running_the_work(string kind)
    {
        bool ran = false;

        int code = await Run(kind, Versioned, ["--version"], _ => ran = true);

        Assert.Equal(0, code);
        Assert.Equal(PublicVersionOf(Versioned) + Environment.NewLine, _stdout.ToString());
        Assert.DoesNotContain("Built with", _stdout.ToString());
        Assert.False(ran);
    }

    [Fact]
    public async Task Version_beside_other_arguments_reaches_the_work_untouched()
    {
        // A web host never rejects an argument, and `--version --urls x` is configuration.
        string[]? seen = null;

        int code = await Run("host", Versioned, ["--version", "--urls", "x"], args => seen = args);

        Assert.Equal(0, code);
        Assert.NotNull(seen);
        Assert.Equal(["--version", "--urls", "x"], seen);
        Assert.Equal("", _stdout.ToString());
    }

    [Fact]
    public async Task A_missing_PublicVersion_is_reported_on_stderr_and_exits_1()
    {
        int code = await Run("console", Unversioned, ["--version"]);

        Assert.Equal(1, code);
        Assert.StartsWith("error: System.Private.CoreLib carries no [AssemblyMetadata(\"PublicVersion\")]", _stderr.ToString());
        Assert.Equal("", _stdout.ToString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_HostAbortedException_passes_through_unreported(bool publicType)
    {
        // The resolver throws the public type when it loads, and otherwise a private one of the same name.
        Exception stop = publicType
            ? new Microsoft.Extensions.Hosting.HostAbortedException()
            : new HostAbortedException();
        int flushes = 0;
        bool fatal = false;

        Exception escaped = await Assert.ThrowsAnyAsync<Exception>(() => AppMain.RunHostAsync(Versioned, [],
            _ => throw stop,
            new AppMainOptions { FlushLog = () => flushes++, OnFatal = _ => fatal = true }));

        Assert.Same(stop, escaped);
        Assert.Equal("", _stderr.ToString());
        Assert.False(fatal);
        Assert.Equal(1, flushes);
    }

    [Fact]
    public async Task A_throwing_callback_is_reported_on_stderr_and_changes_nothing_else()
    {
        int flushes = 0;

        int code = await Run("console", Versioned, [], _ => throw new InvalidOperationException("the work failed"),
            new AppMainOptions
            {
                OnFatal = _ => throw new InvalidOperationException("the dialog failed"),
                FlushLog = () => flushes++,
            });

        Assert.Equal(1, code);
        Assert.Contains("warning: AppServices.Tests: the OnFatal callback threw", _stderr.ToString());
        Assert.Contains("the dialog failed", _stderr.ToString());
        Assert.Equal(1, flushes);
    }

    [Fact]
    public async Task An_exception_whose_text_cannot_be_read_is_reported_by_its_type_and_its_run_exits_1_with_its_code()
    {
        ConcurrentQueue<Call> calls = new();

        int code = await Run("console", Versioned, [], _ => throw new UnreadableException(), Recording(calls));

        // The report cannot throw, so the run keeps the code it decided, and its exit line says so.
        Call[] made = [.. calls];
        Assert.Equal(1, code);
        Assert.Equal([StartLine, OnFatalRan, ExitedWithCode, Flushed], made.Select(call => call.What));
        Assert.Equal(1, Assert.IsType<int>(made[2].Values[0]));
        Assert.Equal(
            ["fatal: AppServices.Tests stopped on an unhandled exception", typeof(UnreadableException).FullName!, ""],
            Lines(_stderr.ToString()));
    }

    [Fact]
    public async Task A_broken_stderr_does_not_change_the_exit_code()
    {
        Console.SetError(new BrokenWriter());

        int code = await Run("console", Versioned, [], _ => throw new InvalidOperationException("the work failed"));

        Assert.Equal(1, code);
    }

    [Theory]
    [InlineData("console")]
    [InlineData("host")]
    [InlineData("desktop")]
    public async Task The_log_is_flushed_once_per_run_however_the_run_ends(string kind)
    {
        int flushes = 0;
        AppMainOptions options = new() { FlushLog = () => flushes++ };

        await Run(kind, Versioned, [], options: options);
        await Run(kind, Versioned, [], _ => throw new InvalidOperationException("failed"), options);
        await Run(kind, Versioned, ["--version"], options: options);

        Assert.Equal(3, flushes);
    }

    [Fact]
    public async Task A_run_reports_unobserved_task_exceptions_and_unhooks_when_it_ends()
    {
        List<Exception> passed = [];

        await Run("host", Versioned, [], _ =>
        {
            AbandonAFaultedTask();
            CollectGarbage();
        }, new AppMainOptions { OnUnobservedTaskException = passed.Add });

        // The control: during the run, the hook sees an abandoned task's exception.
        Assert.Contains("warning: AppServices.Tests had a task exception nobody observed", _stderr.ToString());
        Assert.NotEmpty(passed);

        // After it, the same thing reaches no hook of this run.
        _stderr.GetStringBuilder().Clear();
        passed.Clear();
        AbandonAFaultedTask();
        CollectGarbage();

        Assert.Equal("", _stderr.ToString());
        Assert.Empty(passed);
    }

    [Fact]
    public async Task Overlapping_runs_keep_their_own_callbacks_and_both_unhook()
    {
        TaskCompletionSource bothStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int started = 0;
        List<string> fatal = [];

        async Task Work(bool fail)
        {
            if (Interlocked.Increment(ref started) == 2)
            {
                bothStarted.SetResult();
            }

            await bothStarted.Task;
            if (fail)
            {
                throw new InvalidOperationException("the first run failed");
            }
        }

        Task<int> first = AppMain.RunHostAsync(Versioned, [], _ => Work(fail: true),
            new AppMainOptions { OnFatal = _ => fatal.Add("first") });
        Task<int> second = AppMain.RunHostAsync(Versioned, [], _ => Work(fail: false),
            new AppMainOptions { OnFatal = _ => fatal.Add("second") });

        int[] codes = await Task.WhenAll(first, second);
        Assert.Equal([1, 0], codes);
        Assert.Equal(["first"], fatal);

        _stderr.GetStringBuilder().Clear();
        AbandonAFaultedTask();
        CollectGarbage();
        Assert.Equal("", _stderr.ToString());
    }

    [Theory]
    [MemberData(nameof(BenignExceptionTable.BenignRows), MemberType = typeof(BenignExceptionTable))]
    public async Task A_benign_unobserved_exception_leaves_stderr_empty_and_still_reaches_the_callback(string row)
    {
        (Exception thrown, List<Exception> passed) = await AbandonInARun(BenignExceptionTable.Get(row));

        Assert.Equal("", _stderr.ToString());
        AssertPassedOnAsRaised(thrown, passed);
    }

    [Theory]
    [MemberData(nameof(BenignExceptionTable.NotBenignRows), MemberType = typeof(BenignExceptionTable))]
    public async Task A_non_benign_unobserved_exception_is_reported_as_before(string row)
    {
        (Exception thrown, List<Exception> passed) = await AbandonInARun(BenignExceptionTable.Get(row));

        string stderr = _stderr.ToString();
        Assert.StartsWith("warning: AppServices.Tests had a task exception nobody observed", stderr);
        Assert.Contains(thrown.Message, stderr);
        Assert.DoesNotContain("filter threw", stderr);
        AssertPassedOnAsRaised(thrown, passed);
    }

    [Theory]
    [MemberData(nameof(BenignExceptionTable.FilterThrowsRows), MemberType = typeof(BenignExceptionTable))]
    public async Task A_throwing_filter_is_reported_along_with_the_exception(string row)
    {
        (Exception thrown, List<Exception> passed) = await AbandonInARun(BenignExceptionTable.Get(row));

        // A filter that throws counts as not benign, so the exception is reported as any other is, and
        // the filter's own throw beside it.
        string stderr = _stderr.ToString();
        Assert.StartsWith("warning: AppServices.Tests had a task exception nobody observed", stderr);
        Assert.Contains(thrown.Message, stderr);
        Assert.Contains("warning: AppServices.Tests: the IsBenignException filter threw", stderr);
        Assert.Contains(BenignExceptionTable.FilterBug, stderr);
        AssertPassedOnAsRaised(thrown, passed);
    }

    [Theory]
    [InlineData("console")]
    [InlineData("host")]
    [InlineData("desktop")]
    public async Task A_fatal_exception_is_reported_whatever_the_filter_says(string kind)
    {
        bool fatal = false;

        // The filter calls everything benign, and the work throws the very exception the table calls benign.
        int code = await Run(kind, Versioned, [], _ => throw new BenignExceptionTable.BenignNoise(),
            new AppMainOptions { IsBenignException = _ => true, OnFatal = _ => fatal = true });

        Assert.Equal(1, code);
        Assert.StartsWith("fatal: AppServices.Tests stopped on an unhandled exception", _stderr.ToString());
        Assert.Contains("benign noise", _stderr.ToString());
        Assert.True(fatal);
    }

    [Theory]
    [InlineData("console")]
    [InlineData("host")]
    [InlineData("desktop")]
    public async Task A_run_offers_the_start_line_before_its_work_and_writes_the_exit_line_after_it_and_before_the_flush(string kind)
    {
        ConcurrentQueue<Call> calls = new();
        long before = Stopwatch.GetTimestamp();

        int code = await Run(kind, Versioned, [], _ =>
        {
            calls.Enqueue(new Call(WorkRan, []));
            WaitAtLeast(TimeSpan.FromMilliseconds(50));
        }, Recording(calls));

        long aroundMs = (long)Stopwatch.GetElapsedTime(before).TotalMilliseconds;
        Call[] made = [.. calls];
        Assert.Equal(0, code);
        Assert.Equal([StartLine, WorkRan, ExitedWithCode, Flushed], made.Select(call => call.What));
        Assert.Equal(["AppServices.Tests", PublicVersionOf(Versioned), Environment.ProcessId], made[0].Values);

        // The code an int and the elapsed time a long, so a structured logger keeps each as a number: at
        // least as long as the work took, and no longer than the whole call did.
        Assert.Equal(2, made[2].Values.Length);
        Assert.Equal(0, Assert.IsType<int>(made[2].Values[0]));
        Assert.InRange(Assert.IsType<long>(made[2].Values[1]), 50, aroundMs);
    }

    [Theory]
    [MemberData(nameof(Endings))]
    public async Task The_exit_line_carries_the_code_the_run_returns(string kind, string ending, int expected)
    {
        ConcurrentQueue<Call> calls = new();

        int code = await RunEnding(kind, ending, Recording(calls));

        Call[] made = [.. calls];
        Assert.Equal(expected, code);
        string[] expectedCalls = ending == "throws"
            ? [StartLine, OnFatalRan, ExitedWithCode, Flushed]
            : [StartLine, ExitedWithCode, Flushed];
        Assert.Equal(expectedCalls, made.Select(call => call.What));
        Assert.Equal(expected, Assert.IsType<int>(made[^2].Values[0]));
    }

    [Theory]
    [InlineData("console")]
    [InlineData("desktop")]
    public async Task SIGTERM_writes_its_own_exit_line_before_the_flush_and_the_work_s_end_adds_nothing(string kind)
    {
        ConcurrentQueue<Call> calls = new();

        await Run(kind, Versioned, [], _ => EntryRun.Current!.OnSigterm(), Recording(calls));

        Call[] made = [.. calls];
        Assert.Equal([StartLine, EndedBySigterm, Flushed], made.Select(call => call.What));
        Assert.IsType<long>(Assert.Single(made[1].Values));
    }

    [Theory]
    [InlineData("console")]
    [InlineData("host")]
    [InlineData("desktop")]
    public async Task An_exit_no_finally_sees_writes_Environment_ExitCode_before_the_flush(string kind)
    {
        ConcurrentQueue<Call> calls = new();

        await Run(kind, Versioned, [],
            _ => WithExitCode(3, () => EntryRun.Current!.OnProcessExit(null, EventArgs.Empty)), Recording(calls));

        Call[] made = [.. calls];
        Assert.Equal([StartLine, ExitedWithCode, Flushed], made.Select(call => call.What));
        Assert.Equal(3, Assert.IsType<int>(made[1].Values[0]));
    }

    [Theory]
    [InlineData("console")]
    [InlineData("host")]
    [InlineData("desktop")]
    public async Task An_exception_ending_the_process_from_another_thread_writes_its_reason_after_OnFatal_and_before_the_flush(string kind)
    {
        ConcurrentQueue<Call> calls = new();

        await Run(kind, Versioned, [], _ => EntryRun.Current!.OnUnhandledException(null,
                new UnhandledExceptionEventArgs(new InvalidOperationException("another thread failed"), isTerminating: true)),
            Recording(calls));

        Call[] made = [.. calls];
        Assert.Equal([StartLine, OnFatalRan, EndedByUnhandledException, Flushed], made.Select(call => call.What));
        Assert.IsType<long>(Assert.Single(made[2].Values));
        Assert.StartsWith("fatal: AppServices.Tests stopped on an unhandled exception", _stderr.ToString());
    }

    [Theory]
    [InlineData("console")]
    [InlineData("host")]
    [InlineData("desktop")]
    public async Task A_design_time_stop_writes_reason_hostAborted_before_the_flush_and_still_passes_through(string kind)
    {
        ConcurrentQueue<Call> calls = new();
        Microsoft.Extensions.Hosting.HostAbortedException stop = new();

        Exception escaped = await Assert.ThrowsAnyAsync<Exception>(() =>
            Run(kind, Versioned, [], _ => throw stop, Recording(calls)));

        Call[] made = [.. calls];
        Assert.Same(stop, escaped);
        Assert.Equal([StartLine, StoppedByDesignTimeTool, Flushed], made.Select(call => call.What));
        Assert.IsType<long>(Assert.Single(made[1].Values));
        Assert.Equal("", _stderr.ToString());
    }

    [Theory]
    [InlineData("console")]
    [InlineData("host")]
    [InlineData("desktop")]
    public async Task One_exit_line_and_one_flush_however_many_ends_a_run_reaches(string kind)
    {
        ConcurrentQueue<Call> calls = new();

        await Run(kind, Versioned, [], _ =>
        {
            EntryRun run = EntryRun.Current!;
            WithExitCode(3, () => run.OnProcessExit(null, EventArgs.Empty));
            run.OnSigterm();
            run.OnUnhandledException(null,
                new UnhandledExceptionEventArgs(new InvalidOperationException("another thread failed"), isTerminating: true));
        }, Recording(calls));

        // The first end writes its line and flushes. The later ones, the work's own among them, add
        // neither, though the exception is still reported.
        Call[] made = [.. calls];
        Assert.Equal([StartLine, ExitedWithCode, Flushed, OnFatalRan], made.Select(call => call.What));
        Assert.Equal(3, Assert.IsType<int>(made[1].Values[0]));
    }

    [Theory]
    [InlineData("console")]
    [InlineData("host")]
    [InlineData("desktop")]
    public async Task A_run_that_answers_version_writes_neither_line_and_still_flushes(string kind)
    {
        ConcurrentQueue<Call> calls = new();

        int answered = await Run(kind, Versioned, ["--version"], options: Recording(calls));
        int missing = await Run(kind, Unversioned, ["--version"], options: Recording(calls));

        Assert.Equal(0, answered);
        Assert.Equal(1, missing);
        Assert.Equal([Flushed, Flushed], calls.Select(call => call.What));
    }

    [Fact]
    public async Task The_start_line_writes_unknown_for_a_program_with_no_PublicVersion()
    {
        ConcurrentQueue<Call> calls = new();

        await Run("console", Unversioned, [], options: Recording(calls));

        Call start = calls.First();
        Assert.Equal(StartLine, start.What);
        Assert.Equal(["System.Private.CoreLib", "unknown", Environment.ProcessId], start.Values);
    }

    [Theory]
    [InlineData("console")]
    [InlineData("host")]
    [InlineData("desktop")]
    public async Task A_writer_that_formats_its_template_is_reported_for_each_line_and_changes_nothing_else(string kind)
    {
        int flushes = 0;
        bool ran = false;

        // A format string's writer binds, as the plan's Limits say, and string.Format throws on a named
        // hole: each line's throw is reported on stderr, and the work, its code and the flush stand.
        int code = await Run(kind, Versioned, [], _ => ran = true,
            new AppMainOptions { WriteLogLine = Console.Out.WriteLine, FlushLog = () => flushes++ });

        string[] stderr = Lines(_stderr.ToString());
        Assert.Equal(0, code);
        Assert.True(ran);
        Assert.Equal(1, flushes);
        Assert.Equal(2, stderr.Count(line => line == "warning: AppServices.Tests: the WriteLogLine callback threw"));
        Assert.Equal(2, stderr.Count(line => line.StartsWith("System.FormatException", StringComparison.Ordinal)));
        Assert.Equal("", _stdout.ToString());
    }

    [Fact]
    public async Task A_Serilog_logger_s_method_is_a_writer_that_keeps_each_value_as_a_property_of_its_own_type()
    {
        CollectingSink sink = new();
        using Logger logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();

        // ⛔ These two lines are half the test: each logger's method compiles as a LogLineWriter only
        // because the writer takes a template and then its values, as both methods do.
        LogLineWriter throughSerilog = logger.Information;
        LogLineWriter throughAnILogger = NullLogger.Instance.LogInformation;

        int code = await AppMain.RunConsoleAsync(Versioned, [], (_, _) => Task.FromResult(7),
            new AppMainOptions { WriteLogLine = throughSerilog });

        Assert.Equal(7, code);
        Assert.NotNull(throughAnILogger);
        Assert.Equal([StartLine, ExitedWithCode], sink.Events.Select(logEvent => logEvent.MessageTemplate.Text));
        Assert.All(sink.Events, logEvent => Assert.Equal(LogEventLevel.Information, logEvent.Level));
        Assert.Equal("AppServices.Tests", ValueOf(sink.Events[0], "App"));
        Assert.Equal(PublicVersionOf(Versioned), ValueOf(sink.Events[0], "Version"));
        Assert.Equal(Environment.ProcessId, ValueOf(sink.Events[0], "Pid"));
        Assert.Equal(7, ValueOf(sink.Events[1], "Code"));
        Assert.IsType<long>(ValueOf(sink.Events[1], "ElapsedMs"));
    }

    [Fact]
    public void With_changes_one_member_and_keeps_the_rest()
    {
        LogLineWriter writeLogLine = (_, _) => { };
        Action flush = () => { };
        Action<Exception> onFatal = _ => { };
        Action<Exception> onUnobserved = _ => { };
        Func<Exception, bool> isBenign = _ => true;
        AppMainOptions defaults = new()
        {
            Usage = "usage: app",
            WriteLogLine = writeLogLine,
            FlushLog = flush,
            OnFatal = onFatal,
            OnUnobservedTaskException = onUnobserved,
            IsBenignException = isBenign,
        };
        Action replaced = () => { };

        // ⛔ This line is the test: `with` compiles only because AppMainOptions is a record.
        AppMainOptions changed = defaults with { FlushLog = replaced };

        Assert.Same(replaced, changed.FlushLog);
        Assert.Same(flush, defaults.FlushLog);
        Assert.Equal("usage: app", changed.Usage);
        Assert.Same(writeLogLine, changed.WriteLogLine);
        Assert.Same(onFatal, changed.OnFatal);
        Assert.Same(onUnobserved, changed.OnUnobservedTaskException);
        Assert.Same(isBenign, changed.IsBenignException);
    }

    /// <summary>
    /// Abandons a task faulted with <paramref name="row"/>'s exception inside a run whose filter is the
    /// table's, and collects it there, so that the run's hook sees it as the runtime raises it.
    /// </summary>
    private async Task<(Exception Thrown, List<Exception> Passed)> AbandonInARun(BenignExceptionTable.Row row)
    {
        Exception thrown = row.Make();
        List<Exception> passed = [];

        // Whatever an earlier test abandoned is collected now, before this run hooks in.
        CollectGarbage();
        await Run("host", Versioned, [], _ =>
        {
            AbandonAFaultedTask(thrown);
            CollectGarbage();
        }, new AppMainOptions { IsBenignException = BenignExceptionTable.Filter, OnUnobservedTaskException = passed.Add });

        return (thrown, passed);
    }

    /// <summary>The callback had the exception exactly once, wrapped as the runtime wraps it.</summary>
    private static void AssertPassedOnAsRaised(Exception thrown, List<Exception> passed)
    {
        AggregateException raised = Assert.IsType<AggregateException>(Assert.Single(passed));
        Assert.Same(thrown, Assert.Single(raised.InnerExceptions));
    }

    private static Task<int> Run(string kind, Assembly program, string[] args,
        Action<string[]>? work = null, AppMainOptions? options = null) => kind switch
    {
        "console" => AppMain.RunConsoleAsync(program, args, (a, _) =>
        {
            work?.Invoke(a);
            return Task.FromResult(0);
        }, options),
        "host" => AppMain.RunHostAsync(program, args, a =>
        {
            work?.Invoke(a);
            return Task.CompletedTask;
        }, options),
        "desktop" => Task.FromResult(AppMain.RunDesktop(program, args, a =>
        {
            work?.Invoke(a);
            return 0;
        }, options)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "console, host or desktop"),
    };

    /// <summary>Runs <paramref name="kind"/> with work that ends as <paramref name="ending"/>, a row of <see cref="Endings"/>, says.</summary>
    private static Task<int> RunEnding(string kind, string ending, AppMainOptions options)
    {
        int TheWork()
        {
            switch (ending)
            {
                case "returns 7":
                    return 7;

                case "throws":
                    throw new InvalidOperationException("the work failed");

                case "usage error":
                    throw new UsageException("unknown option: --bogus");

                case "Ctrl+C":
                    EntryRun.Current!.PressCtrlC();
                    return 0;

                default:
                    throw new ArgumentOutOfRangeException(nameof(ending), ending, "an ending of Endings");
            }
        }

        return kind switch
        {
            "console" => AppMain.RunConsoleAsync(Versioned, [], (_, _) => Task.FromResult(TheWork()), options),
            "host" => AppMain.RunHostAsync(Versioned, [], _ =>
            {
                TheWork();
                return Task.CompletedTask;
            }, options),
            "desktop" => Task.FromResult(AppMain.RunDesktop(Versioned, [], _ => TheWork(), options)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "console, host or desktop"),
        };
    }

    /// <summary>
    /// Options that record into <paramref name="calls"/>, in order, each line a run writes, as its
    /// template and values, each fatal report and each flush.
    /// </summary>
    private static AppMainOptions Recording(ConcurrentQueue<Call> calls) => new()
    {
        WriteLogLine = (template, values) => calls.Enqueue(new Call(template, values)),
        OnFatal = _ => calls.Enqueue(new Call(OnFatalRan, [])),
        FlushLog = () => calls.Enqueue(new Call(Flushed, [])),
    };

    /// <summary>Runs <paramref name="action"/> with <see cref="Environment.ExitCode"/> set to <paramref name="code"/>, and puts it back.</summary>
    private static void WithExitCode(int code, Action action)
    {
        int original = Environment.ExitCode;
        Environment.ExitCode = code;
        try
        {
            action();
        }
        finally
        {
            Environment.ExitCode = original;
        }
    }

    /// <summary>Waits until at least <paramref name="interval"/> has passed, by the clock, since a sleep may wake early.</summary>
    private static void WaitAtLeast(TimeSpan interval)
    {
        long start = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(start) < interval)
        {
            Thread.Sleep(5);
        }
    }

    private static string PublicVersionOf(Assembly program) =>
        program.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "PublicVersion").Value!;

    private static object? ValueOf(LogEvent logEvent, string property) =>
        Assert.IsType<ScalarValue>(logEvent.Properties[property]).Value;

    /// <summary>A stream's lines, whatever its line endings.</summary>
    private static string[] Lines(string text) => text.ReplaceLineEndings("\n").Split('\n');

    /// <summary>A task that failed and that nothing awaits: its exception stays unobserved until the GC finds it.</summary>
    /// <param name="thrown">What the task throws; a fresh exception when null.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AbandonAFaultedTask(Exception? thrown = null)
    {
        Exception exception = thrown ?? new InvalidOperationException("nobody observed this");
        Task task = Task.Run((Action)(() => throw exception));
        SpinWait.SpinUntil(() => task.IsCompleted);
    }

    private static void CollectGarbage()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    /// <summary>One thing a recorded run did: a line it wrote, as its template and values, or a mark such as <see cref="Flushed"/>.</summary>
    private sealed record Call(string What, object?[] Values);

    /// <summary>
    /// Stands in for the private type <c>HostFactoryResolver</c> throws when the public one will not
    /// load: the same name in another namespace.
    /// </summary>
    private sealed class HostAbortedException : Exception;

    /// <summary>An exception whose text cannot be read: its <see cref="ToString"/> throws.</summary>
    private sealed class UnreadableException : Exception
    {
        public override string ToString() => throw new InvalidOperationException("this exception's text cannot be read");
    }

    private sealed class BrokenWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => throw new IOException("stderr is closed");

        public override void Write(string? value) => throw new IOException("stderr is closed");

        public override void WriteLine(string? value) => throw new IOException("stderr is closed");
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
