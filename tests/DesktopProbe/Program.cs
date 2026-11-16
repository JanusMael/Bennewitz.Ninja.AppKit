using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Bennewitz.Ninja.AppServices.AvaloniaUI;
using Bennewitz.Ninja.AppServices.EntryPoint;
using DesktopProbe;

// The desktop probe DesktopProbeTests runs as a child process (plans/00004, step 3): an Avalonia app on
// the headless platform, run by AppMain.RunDesktop with EntryPointOptions() as a template's app is, that
// ends itself as bbavalonia's smoke run does. Its arguments, in any order: `fail` sets
// FailOnUnobservedTaskExceptions; `drop` abandons a faulted task once the window has opened; `collect`
// collects it there, before the exit.
string logs = Path.Combine(Path.GetTempPath(), "bb-desktop-probe-" + Guid.NewGuid().ToString("N"));

int code = AppMain.RunDesktop(typeof(Program).Assembly, args, Run, AvaloniaDiagnostics.EntryPointOptions());
try
{
    Directory.Delete(logs, recursive: true);
}
catch (IOException)
{
    // Best-effort: a file still held, by a scanner say, must not change the exit code under test.
}

return code;

int Run(string[] arguments)
{
    ProbeApp.Drop = arguments.Contains("drop");
    ProbeApp.Collect = arguments.Contains("collect");

    AvaloniaDiagnostics.ConfigureLogging(new AvaloniaDiagnosticsOptions
    {
        AppName = "DesktopProbe",
        LogsDirectory = logs,
        EnableTraceSink = false,

        // A smoke run's three switches, as bbavalonia sets them.
        HandleUiThreadExceptions = false,
        ShowUnobservedTaskExceptions = false,
        FailOnUnobservedTaskExceptions = arguments.Contains("fail"),
    });

    return AppBuilder.Configure<ProbeApp>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
        .StartWithClassicDesktopLifetime(arguments, ShutdownMode.OnMainWindowClose);
}
