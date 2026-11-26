using System.Globalization;
using System.Text;
using Bennewitz.Ninja.AppServices.EntryPoint;

// The console probe the entry-point tests run as a child process, and CI's aot jobs publish with
// native AOT (plans/00001, decision 10). A first argument of `host` or `desktop` picks that AppMain
// method, so the native binary runs all three; the argument after it picks what the work does.
// WriteLogLine, FlushLog and OnFatal write markers to stderr, so a test can see that each ran and in
// what order.
AppMainOptions options = new()
{
    Usage = "usage: EntryPointProbe [host|desktop] [ok|fail|wait|background|exit|--version]",
    WriteLogLine = WriteLogLineOnStderr,
    FlushLog = () => Console.Error.WriteLine("probe: flushed"),
    OnFatal = _ => Console.Error.WriteLine("probe: OnFatal"),
};

return args switch
{
    ["host", .. var rest] => await AppMain.RunHostAsync(typeof(Program).Assembly, rest, HostAsync, options),
    ["desktop", .. var rest] => AppMain.RunDesktop(typeof(Program).Assembly, rest, Desktop, options),
    _ => await AppMain.RunConsoleAsync(typeof(Program).Assembly, args, ConsoleAsync, options),
};

static async Task<int> ConsoleAsync(string[] args, CancellationToken cancellationToken)
{
    switch (args)
    {
        case [] or ["ok"]:
            Console.Out.WriteLine("ok");
            return 0;

        case ["fail"]:
            throw new InvalidOperationException("the probe failed on purpose");

        case ["wait"]:
            // The test reads this line before it sends SIGINT or SIGTERM.
            Console.Out.WriteLine("waiting");
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;

        case ["background"]:
            // BNCQ1009: this thread throws on purpose, so the tests can see the entry point report
            // an exception no caller catches.
#pragma warning disable BNCQ1009
            new Thread(() => throw new InvalidOperationException("the probe failed on a background thread")).Start();
#pragma warning restore BNCQ1009
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;

        case ["exit"]:
            // An exit no finally sees: the process ends here, and the run ends from ProcessExit, with
            // the code this sets.
            Environment.Exit(3);
            return 0;

        default:
            throw new UsageException($"unknown argument: {string.Join(' ', args)}");
    }
}

static Task HostAsync(string[] args)
{
    if (args is ["fail"])
    {
        throw new InvalidOperationException("the probe's host failed on purpose");
    }

    // A web host never rejects an argument: everything else on its command line is configuration.
    Console.Out.WriteLine("ok");
    return Task.CompletedTask;
}

static int Desktop(string[] args)
{
    switch (args)
    {
        case ["fail"]:
            throw new InvalidOperationException("the probe's desktop app failed on purpose");

        case ["wait"]:
            // The test reads this line before it sends SIGTERM. A desktop app's cancellation is its
            // window's close, and this one has no window, so only a signal ends it.
            Console.Out.WriteLine("waiting");
            Thread.Sleep(Timeout.Infinite);
            return 0;

        default:
            Console.Out.WriteLine("ok");
            return 0;
    }
}

// A LogLineWriter, passed as a method group as an app passes its logger's: writes the line on stderr as
// a {Message:lj} sink renders it, each named hole in turn filled with its value in the invariant culture.
static void WriteLogLineOnStderr(string messageTemplate, params object?[] propertyValues)
{
    StringBuilder line = new("probe: log ");
    int at = 0;
    foreach (object? value in propertyValues)
    {
        int open = messageTemplate.IndexOf('{', at);
        int close = messageTemplate.IndexOf('}', open);
        line.Append(messageTemplate, at, open - at).Append(Convert.ToString(value, CultureInfo.InvariantCulture));
        at = close + 1;
    }

    Console.Error.WriteLine(line.Append(messageTemplate, at, messageTemplate.Length - at).ToString());
}
