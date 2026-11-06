namespace Bennewitz.Ninja.ScopedEditors.Messages;

/// <summary>
/// Sent via <see cref="CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger"/> when
/// the user clicks an environment-variable token inside a
/// <c>LinkifiedTextBlock</c> description — a control in the Avalonia package, which this one
/// deliberately cannot reference.
/// The receiver should navigate to the Environment editor section and highlight
/// <see cref="VarName"/>.
/// </summary>
public sealed record NavigateToEnvVarMessage(string VarName);