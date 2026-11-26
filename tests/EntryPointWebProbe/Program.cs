using System.Collections.Concurrent;
using Bennewitz.Ninja.AppServices.EntryPoint;

// The web probe the entry-point tests start through WebApplicationFactory, and stop on HostBuilt the
// way a design-time tool stops an app (plans/00001, decision 10). It is shaped as bbweb and bbapi
// will be: the body in RunAsync, the app disposed on the way out. OnFatal writes to stderr, so a test
// that finds stderr empty also knows OnFatal never ran. Its log lines, its work's return and its
// flushes are recorded in Program.Calls, in order, so a test can see where the exit line falls
// (plans/00009, decision 5).
return await AppMain.RunHostAsync(typeof(Program).Assembly, args, RunAsync, new AppMainOptions
{
    WriteLogLine = Record,
    FlushLog = () => Record("flushed"),
    OnFatal = _ => Console.Error.WriteLine("probe: OnFatal"),
});

static async Task RunAsync(string[] args)
{
    await ServeAsync(args);

    // The app is disposed by now, and every logger it owned with it, as bbweb's are when its work returns.
    Record("work returned");
}

static async Task ServeAsync(string[] args)
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
    await using WebApplication app = builder.Build();
    app.MapGet("/", () => "ok");
    await app.RunAsync();
}

/// <summary>Public so the tests' <c>WebApplicationFactory&lt;Program&gt;</c> can start this app.</summary>
public partial class Program
{
    /// <summary>
    /// What the probe's runs did, in order: each log line, as its template and values, each work's
    /// return and each flush. A test clears it before it starts the app.
    /// </summary>
    public static ConcurrentQueue<(string What, object?[] Values)> Calls { get; } = new();

    /// <summary>A <c>LogLineWriter</c>, passed as a method group as an app passes its logger's.</summary>
    private static void Record(string what, params object?[] values) => Calls.Enqueue((what, values));
}
