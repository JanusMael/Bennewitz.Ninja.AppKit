using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AppServices.Tests.EntryPoint;

/// <summary>
/// <c>tests/EntryPointWebProbe</c>, a web app whose entry point is <c>AppMain.RunHostAsync</c>, under
/// the two things that run a web app's entry point in process.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b><c>WebApplicationFactory</c> never throws <c>HostAbortedException</c>.</b> It resolves the
/// host with <c>stopApplication: false</c>, so the entry point simply runs until the factory is
/// disposed. What throws it is a design-time tool, such as <c>dotnet ef</c>, which stops the entry
/// point on the host's <c>HostBuilt</c> diagnostic event. The second test does exactly that, because
/// the package <c>HostFactoryResolver</c> ships in is not on nuget.org.
/// </para>
/// <para>
/// ⚠ The probe's <c>OnFatal</c> writes to stderr, so an empty stderr also shows it never ran.
/// </para>
/// <para>
/// ⭐ The probe records its log lines, its work's return and its flushes in <c>Program.Calls</c>, in
/// order, so a test sees where each run's exit line falls: after its work has returned, the app the work
/// built disposed, and before its flush (plans/00009, decision 5). Each test clears it first, and waits
/// for every run it starts to end, so that no run of one test records into the next one's.
/// </para>
/// </remarks>
public sealed class EntryPointWebProbeTests : IDisposable
{
    private const string StartLine = "[App.Start] app={App} version={Version} pid={Pid}";
    private const string ExitedWithCode = "[App.Exit] code={Code} elapsedMs={ElapsedMs}";
    private const string StoppedByDesignTimeTool = "[App.Exit] reason=hostAborted elapsedMs={ElapsedMs}";

    private readonly TextWriter _originalError = Console.Error;
    private readonly StringWriter _stderr = new();

    public EntryPointWebProbeTests()
    {
        Console.SetError(_stderr);
        Program.Calls.Clear();
    }

    public void Dispose() => Console.SetError(_originalError);

    [Fact]
    public async Task WebApplicationFactory_runs_the_app_twice_each_exit_line_after_its_work_returned_with_nothing_on_stderr()
    {
        for (int run = 1; run <= 2; run++)
        {
            await using (WebApplicationFactory<Program> factory = new())
            {
                using HttpClient client = factory.CreateClient();
                Assert.Equal("ok", await client.GetStringAsync("/", TestContext.Current.CancellationToken));
            }

            // The factory stops the host and returns; the entry point goes on, on a thread of its own, to
            // dispose the app, return from its work and end the run.
            Assert.True(SpinWait.SpinUntil(() => Flushes() == run, TimeSpan.FromMinutes(1)),
                $"Run {run} of the entry point did not end within a minute of its factory's disposal.");
        }

        (string What, object?[] Values)[] calls = [.. Program.Calls];
        string[] oneRun = [StartLine, "work returned", ExitedWithCode, "flushed"];
        Assert.Equal([.. oneRun, .. oneRun], calls.Select(call => call.What));
        Assert.Equal(0, Assert.IsType<int>(calls[2].Values[0]));
        Assert.Equal(0, Assert.IsType<int>(calls[6].Values[0]));
        Assert.Equal("", _stderr.ToString());
    }

    [Fact]
    public void A_stop_on_HostBuilt_passes_its_HostAbortedException_back_with_nothing_on_stderr()
    {
        using StopOnHostBuilt stop = new();
        using IDisposable subscription = DiagnosticListener.AllListeners.Subscribe(stop);

        MethodInfo entryPoint = typeof(Program).Assembly.EntryPoint
            ?? throw new InvalidOperationException("The web probe has no entry point.");
        Exception? escaped = null;

        // On a thread of its own, as HostFactoryResolver runs an entry point.
        // BNCQ1009: invoking the probe's own entry point with its one string[] argument throws only
        // TargetInvocationException, wrapping what the entry point threw, and that is caught.
#pragma warning disable BNCQ1009
        Thread thread = new(() =>
        {
            try
            {
                entryPoint.Invoke(null, [Array.Empty<string>()]);
            }
            catch (TargetInvocationException exception)
            {
                escaped = exception.InnerException;
            }
        });
#pragma warning restore BNCQ1009
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromMinutes(1)), "The entry point did not stop at HostBuilt.");
        Assert.IsType<Microsoft.Extensions.Hosting.HostAbortedException>(escaped);
        Assert.Equal("", _stderr.ToString());

        // The run ends as a design-time tool's stop, before its flush; its work never returned.
        Assert.Equal([StartLine, StoppedByDesignTimeTool, "flushed"], Program.Calls.Select(call => call.What));
    }

    private static int Flushes() => Program.Calls.Count(call => call.What == "flushed");

    /// <summary>
    /// What <c>HostFactoryResolver</c> does with <c>stopApplication</c>, reduced to the stop: on the
    /// <c>Microsoft.Extensions.Hosting</c> listener's <c>HostBuilt</c> event, throw the public
    /// <c>HostAbortedException</c> inside the entry point that is building the host.
    /// </summary>
    private sealed class StopOnHostBuilt
        : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly List<IDisposable> _subscriptions = [];

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == "Microsoft.Extensions.Hosting")
            {
                _subscriptions.Add(value.Subscribe(this));
            }
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Key == "HostBuilt")
            {
                throw new Microsoft.Extensions.Hosting.HostAbortedException();
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void Dispose()
        {
            foreach (IDisposable subscription in _subscriptions)
            {
                subscription.Dispose();
            }
        }
    }
}
