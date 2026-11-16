using System.Diagnostics;
using System.Globalization;

namespace AppServices.Tests.AvaloniaUI;

/// <summary>
/// <see cref="Bennewitz.Ninja.AppServices.AvaloniaUI.AvaloniaDiagnosticsOptions.FailOnUnobservedTaskExceptions"/>
/// seen from outside (<c>plans/00004</c>, step 3): <c>tests/DesktopProbe</c>, an Avalonia app on the
/// headless platform, run as a child process, its exit code and its two streams read the way a smoke
/// check reads them.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>The one proof of Avalonia's own wiring.</b> The probe's work returns what
/// <c>StartWithClassicDesktopLifetime</c> returns, and its window ends the run with <c>Shutdown(0)</c>, as
/// <c>bbavalonia</c>'s smoke run does: so the package's handler must have been subscribed to the real
/// lifetime's <c>Exit</c>, and Avalonia must have handed the code it set back, for a run to exit with 1.
/// <see cref="FailOnUnobservedTaskExceptionsTests"/> calls the handler itself.
/// </para>
/// <para>
/// ⚠ <b>The pause is measured, not bounded.</b> The probe subscribes to <c>Exit</c> before the package and
/// after it, and prints the time between, which each test writes to its output.
/// </para>
/// </remarks>
public sealed class DesktopProbeTests
{
    private const string Reported = "warning: DesktopProbe had a task exception nobody observed";

    private const string Dropped = "the probe's background task failed, and nobody awaited it";

    [Fact]
    public async Task With_the_switch_on_a_task_found_only_by_the_exit_handler_s_collection_fails_the_run()
    {
        Result result = await RunAsync("fail", "drop");

        Assert.Equal(1, result.ExitCode);
        Assert.StartsWith(Reported, result.Stderr);
        Assert.Contains(Dropped, result.Stderr);
        Assert.Equal(["probe: dropped a faulted task", ExitLine(result, 1)], Lines(result.Stdout));
    }

    [Fact]
    public async Task With_the_switch_on_a_task_exception_reported_earlier_in_the_run_fails_it()
    {
        Result result = await RunAsync("fail", "drop", "collect");

        Assert.Equal(1, result.ExitCode);
        Assert.StartsWith(Reported, result.Stderr);
        Assert.Equal(["probe: dropped a faulted task", "probe: collected", ExitLine(result, 1)], Lines(result.Stdout));
    }

    [Fact]
    public async Task With_the_switch_on_a_run_with_nothing_to_report_exits_0()
    {
        Result result = await RunAsync("fail");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("", result.Stderr);
        Assert.Equal([ExitLine(result, 0)], Lines(result.Stdout));
    }

    [Fact]
    public async Task With_the_switch_off_a_reported_task_exception_leaves_the_exit_code_0()
    {
        Result result = await RunAsync("drop", "collect");

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith(Reported, result.Stderr);
        Assert.Contains(Dropped, result.Stderr);
        Assert.Equal(["probe: dropped a faulted task", "probe: collected", ExitLine(result, 0)], Lines(result.Stdout));
    }

    /// <summary>
    /// The line the probe prints from the last <c>Exit</c> handler, with the code the args then carried,
    /// read off the run's own stdout because its time varies; the time is written to the test's output.
    /// </summary>
    private static string ExitLine(Result result, int code)
    {
        string line = Assert.Single(Lines(result.Stdout), each => each.StartsWith("probe: exit handlers took ", StringComparison.Ordinal));
        Assert.EndsWith($" ms, exit code {code.ToString(CultureInfo.InvariantCulture)}", line);
        TestContext.Current.TestOutputHelper?.WriteLine(line);
        return line;
    }

    /// <summary>
    /// The probe's framework-dependent build, read off this assembly's own output directory:
    /// tests/AppServices.Tests/bin/&lt;configuration&gt;/&lt;framework&gt;/.
    /// </summary>
    private static string Probe()
    {
        DirectoryInfo output = new(AppContext.BaseDirectory);
        string probe = Path.Combine(RepoRoot(), "tests", "DesktopProbe", "bin", output.Parent!.Name, output.Name,
            OperatingSystem.IsWindows() ? "DesktopProbe.exe" : "DesktopProbe");
        Assert.True(File.Exists(probe), $"{probe} is missing; the solution build builds it.");
        return probe;
    }

    private static async Task<Result> RunAsync(params string[] args)
    {
        ProcessStartInfo info = new(Probe())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using Process process = Process.Start(info) ?? throw new InvalidOperationException("The desktop probe did not start.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);

        using CancellationTokenSource timeout =
            CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(1));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail("The desktop probe did not exit within a minute; it was killed.");
        }

        return new Result(process.ExitCode, await stdout, await stderr);
    }

    /// <summary>A stream's lines, whatever its line endings, with no empty last line.</summary>
    private static string[] Lines(string text) =>
        text.Length == 0 ? [] : text.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !Directory.EnumerateFiles(directory.FullName, "*.slnx").Any())
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    private sealed record Result(int ExitCode, string Stdout, string Stderr);
}
