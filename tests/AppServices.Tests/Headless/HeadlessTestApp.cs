using AppServices.Tests.Headless;
using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(HeadlessTestApp))]

namespace AppServices.Tests.Headless;

/// <summary>
/// The Avalonia application behind this assembly's headless tests: dispatcher, layout and input,
/// no rendering, and no theme.
/// </summary>
/// <remarks>
/// ⓘ No theme, unlike ScopedEditors' test app. Avalonia 12.1.3 gives every <c>ContentControl</c> a
/// default template, so a dialog a test shows works without one (<c>Dialogs/DialogInput.cs</c>). A test
/// that measures under a theme adds Semi to its own application's styles, which reach no other test.
/// </remarks>
public sealed class HeadlessTestApp : Application
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<HeadlessTestApp>()
                         .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
    }
}
