using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using AppServices.Tests.BenignExceptions;
using Bennewitz.Ninja.AppServices.EntryPoint;

namespace AppServices.Tests.EntryPoint;

/// <summary>
/// <see cref="AppMain"/> in process: the exit code for each way a run ends, what each writes and
/// where, and the hooks a run installs and removes.
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
/// process (<see cref="EntryPointProbeTests"/>), and only on Linux and macOS.
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
    private readonly TextWriter _originalOut = Console.Out;
    private readonly TextWriter _originalError = Console.Error;
    private readonly StringWriter _stdout = new();
    private readonly StringWriter _stderr = new();

    public AppMainTests()
    {
        Console.SetOut(_stdout);
        Console.SetError(_stderr);
    }

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

        string expected = Versioned.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "PublicVersion").Value!;
        Assert.Equal(0, code);
        Assert.Equal(expected + Environment.NewLine, _stdout.ToString());
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

    [Fact]
    public void With_changes_one_member_and_keeps_the_rest()
    {
        Action flush = () => { };
        Action<Exception> onFatal = _ => { };
        Action<Exception> onUnobserved = _ => { };
        Func<Exception, bool> isBenign = _ => true;
        AppMainOptions defaults = new()
        {
            Usage = "usage: app",
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

    /// <summary>
    /// Stands in for the private type <c>HostFactoryResolver</c> throws when the public one will not
    /// load: the same name in another namespace.
    /// </summary>
    private sealed class HostAbortedException : Exception;

    private sealed class BrokenWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => throw new IOException("stderr is closed");

        public override void Write(string? value) => throw new IOException("stderr is closed");

        public override void WriteLine(string? value) => throw new IOException("stderr is closed");
    }
}
