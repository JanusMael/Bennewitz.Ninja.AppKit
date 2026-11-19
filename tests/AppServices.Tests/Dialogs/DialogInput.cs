using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AppServices.Tests.Dialogs;

/// <summary>
/// What a person, or UI Automation, does to a dialog on the headless platform: a button pressed, a key
/// pressed, text typed.
/// </summary>
/// <remarks>
/// <para>
/// No theme is loaded, and none is needed. Avalonia 12.1.3 gives every <see cref="ContentControl"/>,
/// a window and a button among them, a default template holding a <c>PART_ContentPresenter</c>
/// (<c>ContentControl.cs</c>, 47 to 63), so a shown window puts its content in the visual tree, where a
/// button's <see cref="Button.IsDefault"/> and <see cref="Button.IsCancel"/> listen for Enter and Escape
/// on the window. A text box has no template, and still takes focus and typed text. Measured on
/// 2026-11-19.
/// </para>
/// <para>
/// A key pressed on a shown window reaches the element focused in it, or the window itself, and Enter
/// on a dialog shown over a window whose default button has focus presses the dialog's default button,
/// never the owner's (measured the same day).
/// </para>
/// </remarks>
internal static class DialogInput
{
    /// <summary>Presses <paramref name="button"/> as UI Automation does: through its peer's invoke.</summary>
    internal static void Press(Button button)
    {
        AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(button);
        Assert.IsAssignableFrom<IInvokeProvider>(peer).Invoke();
    }

    /// <summary>
    /// Presses and releases <paramref name="key"/> on <paramref name="window"/>, which is shown. A key
    /// that closes the window is not released: a closed window has nowhere left to take it.
    /// </summary>
    internal static void PressKey(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        if (window.IsVisible)
        {
            window.KeyReleaseQwerty(key, RawInputModifiers.None);
        }
    }

    /// <summary>Focuses <paramref name="box"/> and types <paramref name="text"/>, as a keyboard would.</summary>
    internal static void Type(Window window, TextBox box, string text)
    {
        Assert.True(box.Focus(), "The text box took no focus, so nothing typed would reach it.");
        window.KeyTextInput(text);
    }

    /// <summary>A window icon of one pixel, a new instance on every call.</summary>
    internal static WindowIcon NewIcon()
    {
        using WriteableBitmap bitmap = new(
            new PixelSize(1, 1), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        return new WindowIcon(bitmap);
    }
}

/// <summary>Counts a window's <see cref="Window.Closed"/> events, from the moment it is made.</summary>
internal sealed class CloseCount
{
    private CloseCount(Window window)
    {
        window.Closed += (_, _) => Count++;
    }

    /// <summary>How many times the window has closed.</summary>
    internal int Count { get; private set; }

    /// <summary>Starts counting <paramref name="window"/>'s closes.</summary>
    internal static CloseCount Of(Window window)
    {
        return new CloseCount(window);
    }
}
