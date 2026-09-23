using Avalonia;
using Avalonia.Headless;

[assembly: AssemblyFixture(typeof(Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests.Headless.HeadlessSessionBootstrap))]

namespace Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests.Headless;

/// <summary>
/// Starts the assembly's shared <see cref="HeadlessUnitTestSession"/> <em>and forces the Avalonia
/// application to be built</em>, once, before any test in this assembly runs.
/// </summary>
/// <remarks>
/// <para>
/// ⓘ <b>Ported from OpenForge2k's MSTest bootstrap</b>, where it was a static class whose
/// <c>[AssemblyInitialize]</c> did the work and was LINKED into three test projects. Here there is
/// one test project, so it lives in it, and the mechanism is xunit v3's <b>assembly fixture</b>:
/// constructed and initialised before the first test in the assembly runs, and disposed after the
/// last — the same guarantee <c>[AssemblyInitialize]</c> gave. What it DOES is unchanged.
/// <c>HeadlessSessionBootstrapTests</c> came with it and proves the ordering still holds, rather
/// than trusting the translation.
/// </para>
/// <para>
/// Every headless fixture reaches the session through
/// <c>HeadlessUnitTestSession.GetOrStartForAssembly(...)</c>, which starts it lazily on first use.
/// That makes the <em>starting</em> of the Avalonia platform the responsibility of whichever test
/// happens to be scheduled first — which differs between runs and between operating systems.
/// </para>
/// <para>
/// The cost of that showed up the first time CI ran on macOS: platform start-up threw
/// <c>InvalidOperationException: The calling thread cannot access this object because a different
/// thread owns it</c> from <c>Compositor..ctor</c> → <c>DefaultRenderLoop.Add</c> →
/// <c>Dispatcher.VerifyAccess</c>, reported against an unrelated status-bar assertion. A start-up
/// fault attributed to an arbitrary unrelated assertion is close to undiagnosable from a CI log.
/// </para>
/// <para>
/// ⛔⛔ <b><c>GetOrStartForAssembly</c> ALONE DOES NOT PREVENT THAT.</b> It starts the session
/// object and its dispatcher <em>thread</em>; it does not build the Avalonia application.
/// <c>HeadlessUnitTestSession.EnsureIsolatedApplication()</c> — the call that runs
/// <c>AppBuilder.SetupUnsafe()</c> and constructs the compositor — is invoked lazily from
/// <c>DispatchCore</c>, i.e. on the <b>first <c>Dispatch</c></b>. So the warm-up
/// <c>Dispatch</c> below is the load-bearing line: it makes the session thread the first toucher of
/// <c>Dispatcher.UIThread</c>, deterministically, on the one code path guaranteed to run before
/// every test. Measured in the original repository on 2026-09-22, not reasoned.
/// </para>
/// <para>
/// ⚠ <b>Why a wrong thread at all:</b> <c>Dispatcher.UIThread</c> is a lazily-resolved
/// process-global singleton that binds to whichever thread touches it first. In a full-suite run a
/// non-headless test that touches Avalonia can bind it to a runner thread; the session thread then
/// builds the compositor and <c>VerifyAccess</c> fails. In an isolated class run nothing gets there
/// first, which is why affected classes are green alone and flaky together.
/// </para>
/// <para>
/// This complements, and does not replace, the assembly's disabled parallelisation: that keeps the
/// single headless dispatcher from being driven concurrently once it is up; this decides when it
/// comes up.
/// </para>
/// </remarks>
public sealed class HeadlessSessionBootstrap : IAsyncLifetime
{
    /// <summary>
    /// Whether the warm-up dispatch actually observed a built Avalonia application. Read by
    /// <c>HeadlessSessionBootstrapTests</c>; it is the only way to prove from inside the suite that
    /// set-up happened before the first test rather than in it.
    /// </summary>
    internal static bool ApplicationBuiltDuringAssemblyInitialize { get; private set; }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        // The session is cached per assembly, so this is the one start; every GetOrStartForAssembly
        // call in a fixture then returns the same instance.
        HeadlessUnitTestSession session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HeadlessSessionBootstrap).Assembly);

        // ⛔ DO NOT DELETE. This dispatch is what builds the application — see the type remarks.
        // Without it the session exists but Avalonia does not, and the first test to dispatch pays
        // for SetupUnsafe() while a wrongly-bound Dispatcher.UIThread makes it throw. The result is
        // captured rather than discarded so the guard test can assert it.
        ApplicationBuiltDuringAssemblyInitialize =
            await session.Dispatch(() => Application.Current is not null, CancellationToken.None);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
