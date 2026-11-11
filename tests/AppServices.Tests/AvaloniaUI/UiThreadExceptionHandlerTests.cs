using System.Collections.Concurrent;
using System.Reflection;
using AppServices.Tests.BenignExceptions;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Bennewitz.Ninja.AppServices.AvaloniaUI;
using Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;
using Bennewitz.Ninja.AppServices.EntryPoint;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AppServices.Tests.AvaloniaUI;

/// <summary>
/// The handler <see cref="AvaloniaDiagnostics.InstallAvaloniaHooks"/> subscribes to the UI thread's
/// dispatcher (<c>plans/00002</c>, decision 4): what it logs, what it shows, what it counts, and what it
/// leaves alone.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>Each test installs, throws and asserts inside one <c>Session.Dispatch</c></b>, since each
/// dispatch gets a dispatcher of its own. A throwing job is <c>Post</c>ed and run with <c>RunJobs()</c>:
/// only a posted job's exception reaches <c>UnhandledException</c>, where <c>Invoke</c> would put it in
/// the operation's task, and one nothing handles comes back out of <c>RunJobs()</c>. Every throw is a
/// fresh exception, because the dispatcher offers an exception object to its handlers once.
/// </para>
/// <para>
/// The dialog opens through <c>UiThreadExceptions.OpenDialog</c>, which <see cref="FakeDialogs"/>
/// replaces: it records each request, and closes a dialog, or fails to show one, when a test says so.
/// <see cref="The_default_dialog_is_a_FatalErrorDialog_titled_for_the_app_and_its_close_reopens_the_gate"/>
/// opens the real one. The minimum level is Debug, so that a benign exception's line can be seen.
/// </para>
/// <para>
/// ⚠ <see cref="AvaloniaDiagnostics"/> and <see cref="Log.Logger"/> are process-wide: every test starts
/// from <c>ResetForTests</c>, which also takes the handler off its dispatcher and puts the seam back,
/// and puts the logger back. This assembly runs serially (Parallelization.cs).
/// </para>
/// <para>
/// ⚠ <b>Every throw here also leaves a task exception nobody observed.</b> Avalonia 12.1.3's
/// <c>DispatcherOperation.InvokeCore</c> puts a posted job's exception in the job's task before
/// <c>UnhandledException</c> sees it, and nothing reads that task. The runtime raises it at whichever
/// collection finds the task, into whichever run is hooked in then, so <see cref="Dispose"/> collects
/// before the next test starts.
/// <see cref="Avalonia_s_second_report_of_a_handled_exception_is_neither_logged_nor_written_on_stderr"/>
/// raises it on purpose, inside an <c>AppMain</c> run, since that second report is what
/// <c>EntryPointOptions()</c> must recognise.
/// </para>
/// </remarks>
public sealed class UiThreadExceptionHandlerTests : IDisposable
{
    private const string AppName = "ui-thread-tests";

    private readonly ILogger _originalLogger = Log.Logger;
    private readonly string _logs =
        Path.Combine(Path.GetTempPath(), "bb-appservices-ui-thread-" + Guid.NewGuid().ToString("N"));
    private readonly CollectingSink _sink = new();
    private readonly FakeDialogs _dialogs = new();

    public UiThreadExceptionHandlerTests() => AvaloniaDiagnostics.ResetForTests();

    private static HeadlessUnitTestSession Session =>
        HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

    public void Dispose()
    {
        Log.Logger = _originalLogger;
        AvaloniaDiagnostics.ResetForTests();

        // The task exceptions this test's throws left unobserved are raised now, while no run is hooked
        // in (see the remarks).
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

    [Fact]
    public Task A_non_benign_exception_is_logged_at_Error_shown_and_handled() => Session.Dispatch(() =>
    {
        Install();
        List<bool> handled = WatchHandled();
        InvalidOperationException thrown = new("the command failed");

        Throw(thrown);

        Assert.Equal([true], handled);
        Assert.Same(thrown, Assert.Single(_sink.At(LogEventLevel.Error)).Exception);
        Assert.Same(thrown, Assert.Single(_dialogs.Requests).Exception);
    }, CancellationToken.None);

    [Fact]
    public Task A_benign_exception_is_logged_at_Debug_and_shows_nothing() => Session.Dispatch(() =>
    {
        Install();
        List<bool> handled = WatchHandled();
        BenignExceptionTable.BenignNoise thrown = new();

        Throw(thrown);

        Assert.Equal([true], handled);
        Assert.Same(thrown, Assert.Single(_sink.At(LogEventLevel.Debug)).Exception);
        Assert.Empty(_sink.At(LogEventLevel.Error));
        Assert.Empty(_dialogs.Requests);
    }, CancellationToken.None);

    [Theory]
    [MemberData(nameof(BenignExceptionTable.AllRows), MemberType = typeof(BenignExceptionTable))]
    public Task Every_row_of_the_shared_table_is_logged_and_shown_as_its_verdict_says(string row) => Session.Dispatch(() =>
    {
        BenignExceptionTable.Row expected = BenignExceptionTable.Get(row);
        Install();
        List<bool> handled = WatchHandled();
        Exception thrown = expected.Make();

        Throw(thrown);

        Assert.Equal([true], handled);
        if (expected.Benign)
        {
            Assert.Same(thrown, Assert.Single(_sink.At(LogEventLevel.Debug)).Exception);
            Assert.Empty(_sink.At(LogEventLevel.Error));
            Assert.Empty(_dialogs.Requests);
        }
        else
        {
            Assert.Same(thrown, Assert.Single(_sink.At(LogEventLevel.Error)).Exception);
            Assert.Same(thrown, Assert.Single(_dialogs.Requests).Exception);
        }

        // A filter that throws is logged beside the exception, which counts as not benign.
        LogEvent[] warnings = _sink.At(LogEventLevel.Warning);
        if (expected.FilterThrows)
        {
            Assert.Equal(BenignExceptionTable.FilterBug, Assert.Single(warnings).Exception?.Message);
        }
        else
        {
            Assert.Empty(warnings);
        }
    }, CancellationToken.None);

    [Fact]
    public Task A_second_exception_while_the_dialog_is_open_is_logged_not_shown_and_one_after_it_closes_is_shown() =>
        Session.Dispatch(() =>
        {
            Install();
            InvalidOperationException first = new("the first failure");
            InvalidOperationException second = new("the second failure, while the first dialog is open");
            InvalidOperationException third = new("the third failure, after the first dialog closed");

            Throw(first);
            Throw(second);

            Assert.Equal<Exception?>([first, second], _sink.At(LogEventLevel.Error).Select(e => e.Exception));
            Assert.Same(first, Assert.Single(_dialogs.Requests).Exception);

            _dialogs.Requests[0].Close();
            Throw(third);

            Assert.Equal<Exception>([first, third], _dialogs.Requests.Select(request => request.Exception));
        }, CancellationToken.None);

    [Fact]
    public Task A_dialog_that_fails_to_show_reopens_the_gate() => Session.Dispatch(() =>
    {
        Install();
        _dialogs.FailNext = true;
        InvalidOperationException first = new("the first failure, whose dialog fails to show");
        InvalidOperationException second = new("the second failure");

        Throw(first);
        Throw(second);

        Assert.Equal<Exception>([first, second], _dialogs.Requests.Select(request => request.Exception));
        Assert.Contains(_sink.At(LogEventLevel.Error), e => e.Exception is FakeDialogs.CannotShow);
    }, CancellationToken.None);

    [Fact]
    public Task A_failure_first_seen_while_a_dialog_is_open_is_shown_at_its_first_repeat_after_it_closes_and_never_again() =>
        Session.Dispatch(() =>
        {
            Install();
            InvalidOperationException first = new("the first failure");

            Throw(first);
            Throw(Second());

            Assert.Equal<Exception>([first], _dialogs.Requests.Select(request => request.Exception));

            _dialogs.Requests[0].Close();
            InvalidOperationException secondAgain = Second();
            Throw(secondAgain);

            // A different failure still gets its dialog, one at a time: at its first repeat once the gate
            // has reopened. It was logged in full only the once, when it was first seen.
            Assert.Equal<Exception>([first, secondAgain], _dialogs.Requests.Select(request => request.Exception));
            Assert.Equal(2, Failures().Length);

            _dialogs.Requests[1].Close();
            Throw(Second());

            // Never shown twice, and its count line came when its dialog closed.
            Assert.Equal(2, _dialogs.Requests.Count);
            Assert.Equal([1L], RepeatLines().Select(RepeatCount));
        }, CancellationToken.None);

    [Fact]
    public Task A_failure_whose_dialog_failed_to_show_is_not_offered_it_again() => Session.Dispatch(() =>
    {
        Install();
        _dialogs.FailNext = true;
        const string message = "a failure whose dialog fails to show";

        Throw(new InvalidOperationException(message));
        Throw(new InvalidOperationException(message));

        // The gate is open again, but the repeat is not offered the dialog that failed, so a dialog that
        // cannot open is not tried, and its failure not logged, on every repeat.
        Assert.Single(_dialogs.Requests);
        Assert.Single(_sink.At(LogEventLevel.Error), e => e.Exception is FakeDialogs.CannotShow);
    }, CancellationToken.None);

    [Fact]
    public Task A_repeat_is_logged_once_counted_never_shown_twice_and_its_count_written_when_its_dialog_closes() =>
        Session.Dispatch(() =>
        {
            Install();

            Throw(Repeat());
            Throw(Repeat());
            Throw(Repeat());

            Assert.Single(Failures());
            Assert.Single(_dialogs.Requests);
            Assert.Empty(RepeatLines());

            _dialogs.Requests[0].Close();

            LogEvent count = Assert.Single(RepeatLines());
            Assert.Equal(2L, RepeatCount(count));
            Assert.Contains("System.InvalidOperationException: " + RepeatMessage, count.RenderMessage());
            Assert.Contains("thrown at ", count.RenderMessage());

            // With its dialog closed, a repeat is still only counted: the gate is open, but it is never
            // shown again, and never logged in full again.
            Throw(Repeat());

            Assert.Single(Failures());
            Assert.Single(_dialogs.Requests);
        }, CancellationToken.None);

    [Fact]
    public Task FlushLog_writes_the_count_of_every_repeat_since_its_last_count_line() => Session.Dispatch(() =>
    {
        Install();
        Throw(Repeat());
        Throw(Repeat());
        _dialogs.Requests[0].Close();
        Throw(Repeat());
        Throw(Repeat());
        Throw(Repeat());

        AvaloniaDiagnostics.EntryPointOptions().FlushLog!();

        Assert.Equal([1L, 3L], RepeatLines().Select(RepeatCount));
    }, CancellationToken.None);

    [Fact]
    public Task An_exception_another_handler_already_handled_is_left_alone() => Session.Dispatch(() =>
    {
        // The app's own handler, subscribed before the package's, as an app that keeps its handler can.
        Dispatcher.UIThread.UnhandledException += (_, e) => e.Handled = true;
        Install();
        InvalidOperationException thrown = new("the app's own handler dealt with this");

        Throw(thrown);

        Assert.Empty(_sink.Events);
        Assert.Empty(_dialogs.Requests);

        // Its second report included: the options EntryPointOptions() returns do not know it.
        Assert.False(AvaloniaDiagnostics.EntryPointOptions().IsBenignException!(thrown));
    }, CancellationToken.None);

    [Fact]
    public Task A_throwing_filter_does_not_escape_and_the_exception_counts_as_not_benign() => Session.Dispatch(() =>
    {
        Install(filter: _ => throw new InvalidOperationException(BenignExceptionTable.FilterBug));
        List<bool> handled = WatchHandled();
        BenignExceptionTable.BenignNoise thrown = new();

        Throw(thrown);

        Assert.Equal([true], handled);
        Assert.Same(thrown, Assert.Single(_sink.At(LogEventLevel.Error)).Exception);
        Assert.Equal(BenignExceptionTable.FilterBug, Assert.Single(_sink.At(LogEventLevel.Warning)).Exception?.Message);
        Assert.Same(thrown, Assert.Single(_dialogs.Requests).Exception);
    }, CancellationToken.None);

    [Fact]
    public Task A_throwing_logger_does_not_escape_and_the_dialog_is_still_shown() => Session.Dispatch(() =>
    {
        Install();
        Log.Logger = new ThrowingLogger();
        List<bool> handled = WatchHandled();
        InvalidOperationException thrown = new("logged by a broken logger");

        // The control: the logger really does throw.
        Assert.Throws<InvalidOperationException>(() => Log.Error(thrown, "the control"));

        Throw(thrown);

        Assert.Equal([true], handled);
        Assert.Same(thrown, Assert.Single(_dialogs.Requests).Exception);
    }, CancellationToken.None);

    [Fact]
    public Task False_installs_nothing() => Session.Dispatch(() =>
    {
        Install(handleUiThreadExceptions: false);
        InvalidOperationException thrown = new("nothing handles this");

        Dispatcher.UIThread.Post(() => throw thrown);

        Assert.Same(thrown, Assert.ThrowsAny<Exception>(() => Dispatcher.UIThread.RunJobs()));
        Assert.Empty(_sink.Events);
        Assert.Empty(_dialogs.Requests);
    }, CancellationToken.None);

    [Fact]
    public Task Two_calls_install_one_handler() => Session.Dispatch(() =>
    {
        Install();
        AvaloniaDiagnostics.InstallAvaloniaHooks();

        // One reset takes one subscription off; had there been two, the second would handle this.
        UiThreadExceptions.ResetForTests();
        InvalidOperationException thrown = new("nothing handles this");
        Dispatcher.UIThread.Post(() => throw thrown);

        Assert.Same(thrown, Assert.ThrowsAny<Exception>(() => Dispatcher.UIThread.RunJobs()));
    }, CancellationToken.None);

    [Fact]
    public Task ResetForTests_detaches_the_handler_and_resets_the_gate_and_the_counts() => Session.Dispatch(() =>
    {
        Install();
        InvalidOperationException first = Repeat();
        Throw(first);
        Throw(Repeat());
        Assert.True(AvaloniaDiagnostics.EntryPointOptions().IsBenignException!(first));

        UiThreadExceptions.ResetForTests();

        // It forgets what it handled, so the options no longer know the second report of it.
        Assert.False(AvaloniaDiagnostics.EntryPointOptions().IsBenignException!(first));

        // Detached: nothing handles an exception now.
        InvalidOperationException unhandled = new("nothing handles this");
        Dispatcher.UIThread.Post(() => throw unhandled);
        Assert.Same(unhandled, Assert.ThrowsAny<Exception>(() => Dispatcher.UIThread.RunJobs()));

        // Installed again, the failure is new again, though its first dialog never closed: the gate is
        // open, the failure is logged and shown, and the repeat counted before the reset is gone.
        UiThreadExceptions.OpenDialog = _dialogs.Open;
        AvaloniaDiagnostics.InstallAvaloniaHooks();
        Throw(Repeat());

        Assert.Equal(2, Failures().Length);
        Assert.Equal(2, _dialogs.Requests.Count);
        UiThreadExceptions.WritePendingRepeats();
        Assert.Empty(RepeatLines());
    }, CancellationToken.None);

    [Fact]
    public Task The_default_dialog_is_a_FatalErrorDialog_titled_for_the_app_and_its_close_reopens_the_gate() =>
        Session.Dispatch(() =>
        {
            List<Window> opened = [];
            using IDisposable tracking = Window.WindowOpenedEvent.AddClassHandler(
                typeof(Window), (sender, _) => opened.Add((Window)sender!));
            Install(fakeDialogs: false);

            Throw(new InvalidOperationException("shown in the real dialog"));
            Throw(new InvalidOperationException("logged, and not shown while that dialog is open"));

            FatalErrorDialog dialog = Assert.IsType<FatalErrorDialog>(Assert.Single(opened));
            Assert.Equal($"Application Error — {AppName}", dialog.Title);

            dialog.Close();
            Dispatcher.UIThread.RunJobs();
            Throw(new InvalidOperationException("shown once the first dialog has closed"));

            Assert.Equal(2, opened.Count);
            Assert.IsType<FatalErrorDialog>(opened[1]).Close();
        }, CancellationToken.None);

    [Theory]
    [MemberData(nameof(BenignExceptionTable.AllRows), MemberType = typeof(BenignExceptionTable))]
    public async Task Avalonia_s_second_report_of_a_handled_exception_is_neither_logged_nor_written_on_stderr(string row)
    {
        Exception thrown = BenignExceptionTable.Get(row).Make();
        ConcurrentQueue<Exception> unobserved = new();
        bool arrived = false;
        AppMainOptions options = AvaloniaDiagnostics.EntryPointOptions();
        TextWriter originalError = Console.Error;
        StringWriter stderr = new();

        // Whatever an earlier test left unobserved is raised now, before the run hooks in.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Console.SetError(stderr);
        try
        {
            await AppMain.RunHostAsync(typeof(UiThreadExceptionHandlerTests).Assembly, [], async _ =>
            {
                await Session.Dispatch(() =>
                {
                    Install();
                    Throw(thrown);
                }, CancellationToken.None);

                arrived = CollectUntil(() => unobserved.Any(raised => IsSecondReportOf(thrown, raised)));
            }, options with
            {
                OnUnobservedTaskException = raised =>
                {
                    unobserved.Enqueue(raised);
                    options.OnUnobservedTaskException!(raised);
                },
            });
        }
        finally
        {
            Console.SetError(originalError);
        }

        // The control: Avalonia did raise the exception again, wrapped as the runtime wraps a task's.
        Assert.True(arrived,
            "Avalonia raised no second report. If a newer Avalonia no longer puts a posted job's exception "
            + "in the job's task, the second report is gone, and with it the reason UiThreadExceptions "
            + "remembers what it handled.");
        Exception second = Assert.Single(unobserved, raised => IsSecondReportOf(thrown, raised));

        // Not logged: the handler's own line is the only one that carries the failure.
        Assert.DoesNotContain(_sink.Events, logged => ReferenceEquals(logged.Exception, second));

        // Off stderr, unless it flattens to nothing: the rule calls an empty aggregate not benign before
        // any filter runs.
        if (((AggregateException)second).Flatten().InnerExceptions.Count > 0)
        {
            Assert.Equal("", stderr.ToString());
        }
        else
        {
            Assert.StartsWith("warning: AppServices.Tests had a task exception nobody observed", stderr.ToString());
        }
    }

    private const string RepeatMessage = "the same failure, on every frame";

    /// <summary>A fresh exception with the type, message and throw site of every other one it returns.</summary>
    private static InvalidOperationException Repeat() => new(RepeatMessage);

    private const string SecondMessage = "the second failure, first seen while the first dialog is open";

    /// <summary>A fresh exception that is one failure with every other one it returns, and a different one from <see cref="Repeat"/>'s.</summary>
    private static InvalidOperationException Second() => new(SecondMessage);

    /// <summary>Configures logging into <see cref="_sink"/> at Debug, and installs the hooks.</summary>
    private void Install(
        Func<Exception, bool>? filter = null, bool handleUiThreadExceptions = true, bool fakeDialogs = true)
    {
        AvaloniaDiagnostics.ConfigureLogging(new AvaloniaDiagnosticsOptions
        {
            AppName = AppName,
            LogsDirectory = _logs,
            MinimumLevel = LogEventLevel.Debug,
            EnableTraceSink = false,
            // Avalonia's logger sink and the binding class handler are process-wide, and not the subject.
            BridgeAvaloniaLogger = false,
            EnableBindingValidationLogger = false,
            HandleUiThreadExceptions = handleUiThreadExceptions,
            IsBenignException = filter ?? BenignExceptionTable.Filter,
            ConfigureLogger = configuration => configuration.WriteTo.Sink(_sink),
        });

        if (fakeDialogs)
        {
            UiThreadExceptions.OpenDialog = _dialogs.Open;
        }

        AvaloniaDiagnostics.InstallAvaloniaHooks();
    }

    /// <summary>Throws <paramref name="exception"/> from a posted job, and runs it.</summary>
    private static void Throw(Exception exception)
    {
        Dispatcher.UIThread.Post(() => throw exception);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Subscribes after the package's handler, which Avalonia calls first, and records the shared args'
    /// <c>Handled</c> as it finds it.
    /// </summary>
    private static List<bool> WatchHandled()
    {
        List<bool> handled = [];
        Dispatcher.UIThread.UnhandledException += (_, e) => handled.Add(e.Handled);
        return handled;
    }

    /// <summary>
    /// Whether <paramref name="raised"/> is Avalonia's second report of <paramref name="thrown"/>: that
    /// exception alone, wrapped as the runtime wraps a task's.
    /// </summary>
    private static bool IsSecondReportOf(Exception thrown, Exception raised) =>
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

    /// <summary>The Error lines that carry an exception: one per failure logged in full.</summary>
    private LogEvent[] Failures() => [.. _sink.At(LogEventLevel.Error).Where(e => e.Exception is not null)];

    /// <summary>The lines that give a failure's count of repeats.</summary>
    private LogEvent[] RepeatLines() =>
        [.. _sink.Events.Where(e => e.MessageTemplate.Text.StartsWith("A UI-thread exception repeated", StringComparison.Ordinal))];

    private static long RepeatCount(LogEvent line) =>
        Assert.IsType<long>(Assert.IsType<ScalarValue>(line.Properties["RepeatCount"]).Value);

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);

        public LogEvent[] At(LogEventLevel level) => [.. Events.Where(e => e.Level == level)];
    }

    /// <summary>
    /// Stands in for the dialog: records each request, closes one when its <see cref="Request.Close"/> is
    /// called, and fails to show the next one when <see cref="FailNext"/> is set.
    /// </summary>
    private sealed class FakeDialogs
    {
        public List<Request> Requests { get; } = [];

        public bool FailNext { get; set; }

        public void Open(Exception exception, Action closed)
        {
            Requests.Add(new Request(exception, closed));
            if (FailNext)
            {
                FailNext = false;
                throw new CannotShow();
            }
        }

        /// <param name="Exception">The exception the dialog was asked to show.</param>
        /// <param name="Close">Closes it, as the user would.</param>
        public sealed record Request(Exception Exception, Action Close);

        public sealed class CannotShow() : Exception("the dialog could not be shown");
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
