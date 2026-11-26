using System.Diagnostics;
using AppServices.Tests.Logging;
using Bennewitz.Ninja.AppServices;
using Bennewitz.Ninja.AppServices.Abstractions;

namespace AppServices.Tests.Shell;

/// <summary>
/// <see cref="ShellLauncher"/>'s line to a <see cref="DiagnosticSink"/> (<c>plans/00009</c>, decision 7):
/// for each of its four operations, one line in the family's form, whole, with the operation's exception,
/// at Information for an operation the platform does not support and at Warning otherwise.
/// </summary>
/// <remarks>
/// ⛔ Each operation fails through <c>ShellLauncher.StartProcess</c>, which every test here replaces and
/// <see cref="Dispose"/> puts back, so that nothing is launched on any platform. The throw is one no
/// operation's own fallbacks catch, so every platform's path reaches the line.
/// </remarks>
public sealed class ShellLauncherDiagnosticsTests : IDisposable
{
    private readonly Func<ProcessStartInfo, Process?> _originalStart = ShellLauncher.StartProcess;
    private readonly List<(DiagnosticLevel Level, string Message, Exception? Error)> _seen = [];
    private readonly List<ProcessStartInfo> _asked = [];

    public static TheoryData<string> Actions => new("terminal", "reveal", "editor", "url");

    public void Dispose() => ShellLauncher.StartProcess = _originalStart;

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task Each_operation_that_fails_tells_the_sink_its_line_in_the_family_s_form(string action)
    {
        UnauthorizedAccessException refused = new("the test starts nothing");
        FailEveryStart(refused);

        LaunchResult result = await Run(Launcher(), action);

        // The control: the operation did try to start a process, and so reached the seam.
        Assert.NotEmpty(_asked);
        Assert.Equal(LaunchStatus.Denied, result.Status);
        (DiagnosticLevel level, string message, Exception? error) = Assert.Single(_seen);
        Assert.Equal(DiagnosticLevel.Warning, level);
        Assert.Equal($"[Shell.Launch] action={action} reason=Denied", message);
        LogLineForm.Holds(message);
        Assert.Same(refused, error);
    }

    [Fact]
    public async Task An_operation_the_platform_does_not_support_is_told_at_Information()
    {
        PlatformNotSupportedException unsupported = new("not on this platform");
        FailEveryStart(unsupported);

        LaunchResult result = await Run(Launcher(), "url");

        Assert.Equal(LaunchStatus.Unsupported, result.Status);
        (DiagnosticLevel level, string message, Exception? error) = Assert.Single(_seen);
        Assert.Equal(DiagnosticLevel.Information, level);
        Assert.Equal("[Shell.Launch] action=url reason=Unsupported", message);
        LogLineForm.Holds(message);
        Assert.Same(unsupported, error);
    }

    private ShellLauncher Launcher() => new((level, message, error) => _seen.Add((level, message, error)));

    private void FailEveryStart(Exception exception) =>
        ShellLauncher.StartProcess = startInfo =>
        {
            _asked.Add(startInfo);
            throw exception;
        };

    private static ValueTask<LaunchResult> Run(ShellLauncher launcher, string action)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string file = Path.Combine(Path.GetTempPath(), "bb-appservices-never-launched.txt");
        return action switch
        {
            "terminal" => launcher.LaunchTerminalWithCommandAsync("echo never run", token),
            "reveal" => launcher.RevealInFileManagerAsync(file, token),
            "editor" => launcher.OpenInDefaultEditorAsync(file, token),
            "url" => launcher.LaunchUrlAsync("https://example.invalid", token),
            var _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Not one of the four operations."),
        };
    }
}
