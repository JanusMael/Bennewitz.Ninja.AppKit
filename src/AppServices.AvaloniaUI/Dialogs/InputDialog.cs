using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Bennewitz.Ninja.AppServices.AvaloniaUI.Dialogs;

/// <summary>
/// The input <see cref="AvaloniaDialogService.ShowInputAsync(string, string, string)"/> shows, and its
/// overload with an icon: a prompt, a one-line text box, and an OK and a cancel button.
/// </summary>
/// <remarks>
/// A window class a test builds without an owner or a lifetime, as it builds
/// <see cref="FatalErrorDialog"/>; the service shows it over its owner and returns
/// <see cref="Result"/> (<c>plans/00007</c>, decision 1).
/// </remarks>
internal sealed class InputDialog : Window
{
    private string? _answer;

    /// <summary>Builds the input, unshown.</summary>
    /// <param name="title">The window's title.</param>
    /// <param name="prompt">The text above the box.</param>
    /// <param name="placeholder">The box's placeholder, or <see langword="null"/> for none.</param>
    /// <param name="icon">The window's icon, or <see langword="null"/> to leave the platform's.</param>
    internal InputDialog(string title, string prompt, string? placeholder, WindowIcon? icon)
    {
        InputBox = new TextBox
        {
            PlaceholderText = placeholder,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        // The box is named by the prompt above it, which is what the user is asked to type.
        AutomationProperties.SetName(InputBox, prompt);
        AutomationProperties.SetAutomationId(InputBox, DialogAutomationIds.Input);
        ToolTip.SetTip(InputBox, DialogTips.Input);

        OkButton = new Button { Content = "OK", IsDefault = true, MinWidth = 70 };
        AutomationProperties.SetAutomationId(OkButton, DialogAutomationIds.Ok);
        ToolTip.SetTip(OkButton, DialogTips.Accept);
        CancelButton = new Button { Content = "Cancel", IsCancel = true, MinWidth = 70 };
        AutomationProperties.SetAutomationId(CancelButton, DialogAutomationIds.Cancel);
        ToolTip.SetTip(CancelButton, DialogTips.Cancel);

        StackPanel buttonRow = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        buttonRow.Children.Add(CancelButton);
        buttonRow.Children.Add(OkButton);

        StackPanel body = new() { Margin = new Thickness(16), Spacing = 10 };
        body.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(InputBox);
        body.Children.Add(buttonRow);

        Title = title;
        Width = 420;
        MinHeight = 120;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        Content = body;

        if (icon is not null)
        {
            Icon = icon;
        }

        OkButton.Click += (_, _) =>
        {
            _answer = InputBox.Text?.Trim();
            Close();
        };
        CancelButton.Click += (_, _) => Close();

        InputBox.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Return or Key.Enter)
            {
                _answer = InputBox.Text?.Trim();
                Close();
            }
            else if (e.Key == Key.Escape)
            {
                Close();
            }
        };
    }

    /// <summary>The one-line box the answer is typed in.</summary>
    internal TextBox InputBox { get; }

    /// <summary>Takes what was typed and closes; Enter presses it.</summary>
    internal Button OkButton { get; }

    /// <summary>Closes without an answer; Escape presses it.</summary>
    internal Button CancelButton { get; }

    /// <summary>
    /// The answer once the window has closed: the text typed, trimmed, when OK or Enter took it, and
    /// <see langword="null"/> when that text is blank or the window closed any other way.
    /// </summary>
    internal string? Result => string.IsNullOrWhiteSpace(_answer) ? null : _answer;
}
