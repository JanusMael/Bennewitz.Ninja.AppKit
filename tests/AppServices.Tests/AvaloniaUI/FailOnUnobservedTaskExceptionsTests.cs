using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using AppServices.Tests.BenignExceptions;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Threading;
using Bennewitz.Ninja.AppServices.AvaloniaUI;
using Bennewitz.Ninja.AppServices.EntryPoint;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AppServices.Tests.AvaloniaUI;

/// <summary>
/// <see cref="AvaloniaDiagnosticsOptions.FailOnUnobservedTaskExceptions"/> (<c>plans/00004</c>): what the
/// callback <see cref="AvaloniaDiagnostics.EntryPointOptions"/> returns counts, and what the exit handler
/// does with the count and the exit code.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>The exit handler is called, never raised.</b> Avalonia raises a real lifetime's <c>Exit</c> from
/// its shutdown, which then shuts the UI thread's dispatcher down, and the headless tests share that
/// dispatcher. So each test calls <c>AvaloniaDiagnostics.OnLifetimeExit</c> with constructed args, inside
/// an <c>AppMain</c> run whose work returns the code the args carry, as a desktop app's work returns its
/// lifetime's. That <c>InstallAvaloniaHooks</c> subscribes it to a real lifetime, and that Avalonia hands
/// its code back, is proved by <see cref="DesktopProbeTests"/>, which runs a desktop app as a child process.
/// </para>
/// <para>
/// ⛔ <b>A task is abandoned faulted, never run.</b> <see cref="AbandonAFaultedTask"/> faults it through a
/// <see cref="TaskCompletionSource"/>, so that no pool thread still holds it when the exit handler's
/// collection comes: that collection, not the test's, is what must find it.
/// </para>
/// <para>
/// ⚠ <see cref="AvaloniaDiagnostics"/>, <see cref="Log.Logger"/> and stderr are process-wide: every test
/// starts from <c>ResetForTests</c>, collects what earlier tests left before its run hooks in, and puts
/// them back. This assembly runs serially (Parallelization.cs).
/// </para>
/// </remarks>
public sealed class FailOnUnobservedTaskExceptionsTests : IDisposable
{
    private const string AppName = "fail-on-unobserved-tests";

    private const string Failed = "a background task failed, and nobody awaited it";

    private readonly ILogger _originalLogger = Log.Logger;
    private readonly string _logs =
        Path.Combine(Path.GetTempPath(), "bb-appservices-fail-on-unobserved-" + Guid.NewGuid().ToString("N"));
    private readonly CollectingSink _sink = new();

    public FailOnUnobservedTaskExceptionsTests() => AvaloniaDiagnostics.ResetForTests();

    private static HeadlessUnitTestSession Session =>
        HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

    public void Dispose()
    {
        Log.Logger = _originalLogger;
        AvaloniaDiagnostics.ResetForTests();

        // What this test left unobserved, Avalonia's second reports among it, is raised now, while no run is
        // hooked in.
        GC.Collect();
        GC.WaitForPendingFinalizers();

        try
        {
            Directory.Delete(_logs, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }

    [Theory]
    [MemberData(nameof(BenignExceptionTable.AllRowsWithShowOnAndOff), MemberType = typeof(BenignExceptionTable))]
    public void Every_row_abandoned_in_a_task_and_found_by_the_exit_handler_fails_the_run_as_its_verdict_says(string row, bool show)
    {
        BenignExceptionTable.Row expected = BenignExceptionTable.Get(row);
        Exception thrown = expected.Make();
        Configure(fail: true, show: show);
        bool arrived = false;

        (int code, string stderr) = InAnAppMainRun(raised =>
        {
            AbandonAFaultedTask(thrown);
            int returned = CallTheExitHandler(0);

            // No collection but the exit handler's has run since the task was abandoned.
            arrived = raised.Any(each => WrapsOnly(thrown, each));
            return returned;
        });

        Assert.True(arrived, "The exit handler's collection did not find the abandoned task.");
        if (expected.BenignUnobserved)
        {
            Assert.Equal(0, code);
            Assert.Empty(CountLines());
            Assert.Equal("", stderr);
        }
        else
        {
            Assert.Equal(1, code);
            Assert.Equal(1L, UnobservedCount(Assert.Single(CountLines())));
            Assert.StartsWith("warning: AppServices.Tests had a task exception nobody observed", stderr);
        }
    }

    [Theory]
    [MemberData(nameof(BenignExceptionTable.AllRows), MemberType = typeof(BenignExceptionTable))]
    public void Avalonia_s_second_report_of_a_UI_thread_exception_is_not_counted(string row)
    {
        Exception thrown = BenignExceptionTable.Get(row).Make();
        bool arrived = false;

        (int code, _) = InAnAppMainRun(raised => InADispatch(() =>
        {
            // The handler is what remembers the exception, so it is installed; its dialog is not the subject.
            Configure(fail: true, handle: true);
            UiThreadExceptions.OpenDialog = (_, _) => { };
            AvaloniaDiagnostics.InstallAvaloniaHooks();

            Dispatcher.UIThread.Post(() => throw thrown);
            Dispatcher.UIThread.RunJobs();
            arrived = CollectUntil(() => raised.Any(each => WrapsOnly(thrown, each)));
            return CallTheExitHandler(0);
        }));

        // The control: Avalonia did report the exception a second time, before the exit handler ran.
        Assert.True(arrived, "Avalonia raised no second report, so this test proves nothing.");
        Assert.Equal(0, code);
        Assert.Empty(CountLines());
    }

    [Fact]
    public void A_task_a_finalizer_abandons_during_the_first_round_is_found_by_the_second()
    {
        Exception thrown = new InvalidOperationException(Failed);
        Configure(fail: true);
        bool arrived = false;

        (int code, _) = InAnAppMainRun(raised =>
        {
            AbandonAnObjectThatAbandonsAFaultedTaskWhenFinalized(thrown);
            int returned = CallTheExitHandler(0);
            arrived = raised.Any(each => WrapsOnly(thrown, each));
            return returned;
        });

        Assert.True(arrived, "The exit handler's collection did not find the task the finalizer abandoned.");
        Assert.Equal(1, code);
    }

    [Theory]
    [InlineData("before ConfigureLogging")]
    [InlineData("before InstallAvaloniaHooks")]
    public void A_task_exception_reported_earlier_in_the_run_is_counted(string when)
    {
        Exception thrown = new InvalidOperationException(Failed);
        bool arrived = false;

        (int code, _) = InAnAppMainRun(raised => InADispatch(() =>
        {
            if (when == "before InstallAvaloniaHooks")
            {
                Configure(fail: true);
            }

            AbandonAFaultedTask(thrown);
            arrived = CollectUntil(() => raised.Any(each => WrapsOnly(thrown, each)));

            if (when == "before ConfigureLogging")
            {
                Configure(fail: true);
            }

            AvaloniaDiagnostics.InstallAvaloniaHooks();
            return CallTheExitHandler(0);
        }));

        Assert.True(arrived, "The abandoned task's exception was never raised.");
        Assert.Equal(1, code);
    }

    [Fact]
    public void A_code_the_app_set_is_kept()
    {
        Configure(fail: true);

        (int code, _) = InAnAppMainRun(_ =>
        {
            AbandonAFaultedTask(new InvalidOperationException(Failed));
            return CallTheExitHandler(3);
        });

        Assert.Equal(3, code);
        Assert.Empty(CountLines());
    }

    [Fact]
    public void With_the_switch_off_the_exit_handler_leaves_the_code_alone()
    {
        Exception thrown = new InvalidOperationException(Failed);
        Configure(fail: false);
        bool arrived = false;

        (int code, string stderr) = InAnAppMainRun(raised =>
        {
            AbandonAFaultedTask(thrown);
            arrived = CollectUntil(() => raised.Any(each => WrapsOnly(thrown, each)));
            return CallTheExitHandler(0);
        });

        // The control: the exception was reported, logged and written on stderr, and still changes nothing.
        Assert.True(arrived, "The abandoned task's exception was never raised.");
        Assert.Contains(_sink.Events, e => e.Exception is AggregateException wrapped && WrapsOnly(thrown, wrapped));
        Assert.StartsWith("warning: AppServices.Tests had a task exception nobody observed", stderr);
        Assert.Equal(0, code);
        Assert.Empty(CountLines());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task A_lifetime_that_raises_no_Exit_gets_a_Warning_only_while_the_switch_is_on(bool fail) =>
        Session.Dispatch(() =>
        {
            // A headless test's application has no lifetime at all, as a single view's has no Exit.
            Assert.Null(global::Avalonia.Application.Current?.ApplicationLifetime);
            Configure(fail: fail);

            AvaloniaDiagnostics.InstallAvaloniaHooks();

            LogEvent[] warnings =
            [
                .. _sink.Events.Where(e => e.Level == LogEventLevel.Warning
                    && e.MessageTemplate.Text.StartsWith("FailOnUnobservedTaskExceptions is set", StringComparison.Ordinal)),
            ];
            if (fail)
            {
                Assert.Equal("none", Assert.IsType<ScalarValue>(Assert.Single(warnings).Properties["Lifetime"]).Value);
            }
            else
            {
                Assert.Empty(warnings);
            }
        }, CancellationToken.None);

    [Fact]
    public void ResetForTests_forgets_the_count()
    {
        Exception thrown = new InvalidOperationException(Failed);
        Configure(fail: true);
        bool arrived = false;

        InAnAppMainRun(raised =>
        {
            AbandonAFaultedTask(thrown);
            arrived = CollectUntil(() => raised.Any(each => WrapsOnly(thrown, each)));
            return 0;
        });
        Assert.True(arrived, "The abandoned task's exception was never raised.");

        AvaloniaDiagnostics.ResetForTests();
        Configure(fail: true);
        (int code, _) = InAnAppMainRun(_ => CallTheExitHandler(0));

        Assert.Equal(0, code);
    }

    /// <summary>Calls the exit handler as Avalonia's lifetime does, and returns the code it leaves in the args.</summary>
    private static int CallTheExitHandler(int code)
    {
        ControlledApplicationLifetimeExitEventArgs args = new(code);
        AvaloniaDiagnostics.OnLifetimeExit(null, args);
        return args.ApplicationExitCode;
    }

    /// <summary>Configures logging into <see cref="_sink"/> at Debug, with the table's filter, and installs nothing.</summary>
    /// <param name="fail">The switch under test.</param>
    /// <param name="show">Whether a task exception nobody observed is shown; off, as in a smoke run.</param>
    /// <param name="handle">Whether the UI-thread handler handles; off, as in a smoke run.</param>
    private void Configure(bool fail, bool show = false, bool handle = false) =>
        AvaloniaDiagnostics.ConfigureLogging(new AvaloniaDiagnosticsOptions
        {
            AppName = AppName,
            LogsDirectory = _logs,
            MinimumLevel = LogEventLevel.Debug,
            EnableTraceSink = false,
            // Avalonia's logger sink and the binding class handler are process-wide, and not the subject.
            BridgeAvaloniaLogger = false,
            EnableBindingValidationLogger = false,
            HandleUiThreadExceptions = handle,
            ShowUnobservedTaskExceptions = show,
            FailOnUnobservedTaskExceptions = fail,
            IsBenignException = BenignExceptionTable.Filter,
            ConfigureLogger = configuration => configuration.WriteTo.Sink(_sink),
        });

    /// <summary>
    /// Runs <paramref name="work"/> as a desktop app's work inside an <c>AppMain</c> run with
    /// <c>EntryPointOptions()</c>, and returns the run's exit code and what reached stderr. The work is
    /// handed every task exception nobody observed that the run's callback is given, as it arrives.
    /// </summary>
    private static (int Code, string Stderr) InAnAppMainRun(Func<ConcurrentQueue<Exception>, int> work)
    {
        ConcurrentQueue<Exception> unobserved = new();
        AppMainOptions options = AvaloniaDiagnostics.EntryPointOptions();
        TextWriter originalError = Console.Error;
        StringWriter stderr = new();

        // Whatever an earlier test left unobserved is raised now, before the run hooks in.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Console.SetError(stderr);
        try
        {
            int code = AppMain.RunDesktop(typeof(FailOnUnobservedTaskExceptionsTests).Assembly, [],
                _ => work(unobserved),
                options with
                {
                    OnUnobservedTaskException = raised =>
                    {
                        unobserved.Enqueue(raised);
                        options.OnUnobservedTaskException!(raised);
                    },
                });
            return (code, stderr.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>Runs <paramref name="body"/> inside one <c>Session.Dispatch</c>, as a desktop app's work runs on its UI thread, and waits for it.</summary>
    private static int InADispatch(Func<int> body) =>
        Session.Dispatch(body, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// A task that failed and that nothing references: its exception stays unobserved until a collection
    /// finds it. Faulted rather than run, so that no pool thread still holds it.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AbandonAFaultedTask(Exception thrown)
    {
        TaskCompletionSource source = new();
        source.SetException(thrown);
    }

    /// <summary>
    /// An object nothing references, whose finalizer abandons a faulted task: the first collection finds
    /// the object, its finalizer runs in the first wait, and only a second collection finds the task.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AbandonAnObjectThatAbandonsAFaultedTaskWhenFinalized(Exception thrown) =>
        GC.KeepAlive(new AbandonsAFaultedTaskWhenFinalized(thrown));

    /// <summary>
    /// Whether <paramref name="raised"/> is <paramref name="thrown"/> alone, wrapped as the runtime wraps a
    /// task's: a task faulted with it, or Avalonia's second report of it.
    /// </summary>
    private static bool WrapsOnly(Exception thrown, Exception raised) =>
        raised is AggregateException { InnerExceptions: [Exception only] } && ReferenceEquals(only, thrown);

    /// <summary>
    /// Collects garbage until <paramref name="arrived"/> holds, for two seconds at most, and says whether
    /// it did: the runtime raises a task's exception only once a collection finds the task unreachable.
    /// </summary>
    private static bool CollectUntil(Func<bool> arrived)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (arrived())
            {
                return true;
            }

            Thread.Sleep(20);
        }

        return false;
    }

    /// <summary>The lines in which the exit handler gives the count it turned into an exit code of 1.</summary>
    private LogEvent[] CountLines() =>
        [.. _sink.Events.Where(e => e.Level == LogEventLevel.Error && e.Properties.ContainsKey("UnobservedCount"))];

    private static long UnobservedCount(LogEvent line) =>
        Assert.IsType<long>(Assert.IsType<ScalarValue>(line.Properties["UnobservedCount"]).Value);

    /// <summary>Abandons a faulted task when it is finalized; see <see cref="AbandonAnObjectThatAbandonsAFaultedTaskWhenFinalized"/>.</summary>
    private sealed class AbandonsAFaultedTaskWhenFinalized(Exception thrown)
    {
        ~AbandonsAFaultedTaskWhenFinalized()
        {
            try
            {
                AbandonAFaultedTask(thrown);
            }
            catch (Exception exception)
            {
                // Faulting a fresh task throws nothing; were it to, a finalizer's throw would end the test run.
                Console.Error.WriteLine(exception);
            }
        }
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
