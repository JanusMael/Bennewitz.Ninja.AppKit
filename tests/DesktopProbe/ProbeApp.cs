using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Bennewitz.Ninja.AppServices.AvaloniaUI;

namespace DesktopProbe;

/// <summary>
/// The probe's application: one window, which ends the run once it has opened, as <c>bbavalonia</c>'s smoke
/// run does, after abandoning a faulted task when asked to.
/// </summary>
internal sealed class ProbeApp : Application
{
    /// <summary>Whether to abandon a faulted task once the window has opened.</summary>
    internal static bool Drop { get; set; }

    /// <summary>Whether to collect it there, before the exit, so that it is reported early in the run.</summary>
    internal static bool Collect { get; set; }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Subscribed before InstallAvaloniaHooks and after it, so that the time between the two is what
            // the package's exit handler took. Avalonia calls them in the order they subscribed.
            long before = 0;
            desktop.Exit += (_, _) => before = Stopwatch.GetTimestamp();
            AvaloniaDiagnostics.InstallAvaloniaHooks();
            desktop.Exit += (_, e) => Console.Out.WriteLine(
                $"probe: exit handlers took {Stopwatch.GetElapsedTime(before).TotalMilliseconds:F1} ms, exit code {e.ApplicationExitCode}");

            Window window = new();
            window.Opened += (_, _) =>
            {
                if (Drop)
                {
                    AbandonAFaultedTask();
                    Console.Out.WriteLine("probe: dropped a faulted task");
                }

                if (Collect)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    Console.Out.WriteLine("probe: collected");
                }

                // As bbavalonia ends a smoke run: posted from the window's Opened, at Background priority.
                Dispatcher.UIThread.Post(() => desktop.Shutdown(0), DispatcherPriority.Background);
            };
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// A task that failed and that nothing references. Faulted rather than run, so that no pool thread
    /// still holds it when the exit handler's collection comes.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AbandonAFaultedTask()
    {
        TaskCompletionSource source = new();
        source.SetException(new InvalidOperationException("the probe's background task failed, and nobody awaited it"));
    }
}
