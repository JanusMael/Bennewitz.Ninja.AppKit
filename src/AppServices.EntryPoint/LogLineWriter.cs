namespace Bennewitz.Ninja.AppServices.EntryPoint;

/// <summary>
/// Writes one line to the app's log: a message template in the family's form, an <c>[Area.Kind]</c> tag
/// and then camelCase pairs, whose named holes are filled from <paramref name="propertyValues"/> in
/// order, as Serilog's and Microsoft.Extensions.Logging's methods fill them.
/// </summary>
/// <param name="messageTemplate">The line's message template, such as
/// <c>[App.Exit] code={Code} elapsedMs={ElapsedMs}</c>: a constant, whose holes are named.</param>
/// <param name="propertyValues">The holes' values, in order. A structured logger keeps each as a property
/// of its own type, so a sink can query <c>Code</c> as a number.</param>
/// <remarks>
/// <para>
/// ⭐ <b>A logger's method, passed as it is.</b> A Serilog app passes <c>Log.Information</c>, and an app
/// with an <c>ILogger</c> passes <c>logger.LogInformation</c>: each binds as a method group. A method
/// group is no call, so an analyser that looks for a template that is not a constant, CA2254 among
/// them, has nothing to report, where a lambda would show it one.
/// </para>
/// <para>
/// ⛔ <b>Never a format string's writer.</b> <c>Console.Out.WriteLine</c> binds too, and throws
/// <c>FormatException</c> on the named holes, which <see cref="AppMain"/> reports on stderr as it reports
/// any callback's throw.
/// </para>
/// </remarks>
public delegate void LogLineWriter(string messageTemplate, params object?[] propertyValues);
