using System.Text.RegularExpressions;
using Serilog.Events;
using Serilog.Parsing;

namespace AppServices.Tests.Logging;

/// <summary>
/// The family's form for a log line (Bennewitz.Ninja.Templates' <c>plans/00014</c>, decisions 2 and 3;
/// this repository's <c>plans/00009</c>, decision 7), held over the template of every line a test reaches:
/// an <c>[Area.Kind]</c> tag, then camelCase pairs, each value a hole named for its key in PascalCase, the
/// same hole quoted by hand, or a word; no format, so no <c>:l</c>, no positional hole and no destructuring.
/// </summary>
/// <remarks>
/// <para>
/// Over an event it also holds what a template cannot show: a Warning's, an Error's or a Fatal's cause is in
/// <c>reason=</c>, but for Avalonia's own line, whose text is its only reason (<c>plans/00009</c>, Limits);
/// and every hole's value is text, a number or a <see cref="bool"/>, never an enum, a date, a time span or a
/// guid, which <c>{Message:lj}</c> would quote, and never <see langword="null"/>, which a value the package
/// does not have writes as <c>unknown</c> instead. A hole left bare holds no space and no quote, so its pair
/// ends where the next begins.
/// </para>
/// <para>
/// ⛔ It reads the template and the values, never <c>RenderMessage()</c>, which quotes every string itself, so
/// that a test reading through it reads a line no sink writes. <see cref="LogLineFormTests"/> holds it to
/// what it must refuse.
/// </para>
/// </remarks>
internal static partial class LogLineForm
{
    private const string AvaloniaTag = "[Avalonia.Log]";

    /// <summary>Asserts that <paramref name="messageTemplate"/> is in the family's form.</summary>
    public static void Holds(string messageTemplate)
    {
        Match line = Line().Match(messageTemplate);
        Assert.True(line.Success, $"Not an [Area.Kind] tag followed by key=value pairs: {messageTemplate}");

        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (Capture pair in line.Groups["pair"].Captures)
        {
            Match parsed = Pair().Match(pair.Value);
            Assert.True(parsed.Success, $"Not a camelCase key=value pair: {pair.Value}, in {messageTemplate}");

            string key = parsed.Groups["key"].Value;
            Assert.True(keys.Add(key), $"The key {key} is given twice: {messageTemplate}");

            Group hole = parsed.Groups["quoted"].Success ? parsed.Groups["quoted"] : parsed.Groups["bare"];
            if (hole.Success)
            {
                Assert.Equal(char.ToUpperInvariant(key[0]) + key[1..], hole.Value);
            }
        }
    }

    /// <summary>
    /// Asserts that <paramref name="logEvent"/>'s template is in the family's form, that it gives its cause
    /// in <c>reason=</c> at Warning and above, and that each hole's value is one the form writes as it is.
    /// </summary>
    public static void Holds(LogEvent logEvent)
    {
        string template = logEvent.MessageTemplate.Text;
        Holds(template);

        if (logEvent.Level >= LogEventLevel.Warning && !template.StartsWith(AvaloniaTag, StringComparison.Ordinal))
        {
            Assert.True(template.Contains(" reason=", StringComparison.Ordinal),
                $"A {logEvent.Level} line without its reason: {template}");
        }

        foreach (PropertyToken hole in logEvent.MessageTemplate.Tokens.OfType<PropertyToken>())
        {
            Assert.True(logEvent.Properties.TryGetValue(hole.PropertyName, out LogEventPropertyValue? value),
                $"No value for {{{hole.PropertyName}}}: {template}");
            ScalarValue scalar = Assert.IsType<ScalarValue>(value);
            Assert.True(scalar.Value is string or bool or int or long or double or decimal or float or short or byte or uint or ulong,
                $"{{{hole.PropertyName}}} holds {scalar.Value?.GetType().Name ?? "null"}, which the form does not write as it is: {template}");

            bool quoted = template.Contains("\"{" + hole.PropertyName + "}\"", StringComparison.Ordinal);
            if (!quoted && scalar.Value is string text)
            {
                Assert.False(text.Length == 0 || text.Any(c => char.IsWhiteSpace(c) || c == '"'),
                    $"{{{hole.PropertyName}}} is bare, and its value '{text}' would run into the next pair: {template}");
            }
        }
    }

    /// <summary>
    /// Asserts that <paramref name="logEvent"/> is the line <paramref name="messageTemplate"/>, at
    /// <paramref name="level"/>, carrying <paramref name="exception"/>, and that it holds the form.
    /// </summary>
    public static void Is(LogEvent logEvent, LogEventLevel level, string messageTemplate, Exception? exception)
    {
        Assert.Equal(messageTemplate, logEvent.MessageTemplate.Text);
        Assert.Equal(level, logEvent.Level);
        Assert.Same(exception, logEvent.Exception);
        Holds(logEvent);
    }

    /// <summary>The value of <paramref name="logEvent"/>'s property <paramref name="name"/>, which must be a scalar.</summary>
    public static object? ValueOf(LogEvent logEvent, string name) =>
        Assert.IsType<ScalarValue>(logEvent.Properties[name]).Value;

    [GeneratedRegex(@"^\[[A-Z][A-Za-z]*\.[A-Z][A-Za-z]*\](?: (?<pair>[^ ]+))*$", RegexOptions.CultureInvariant)]
    private static partial Regex Line();

    [GeneratedRegex(
        @"^(?<key>[a-z][A-Za-z0-9]*)=(?:""\{(?<quoted>[A-Z][A-Za-z0-9]*)\}""|\{(?<bare>[A-Z][A-Za-z0-9]*)\}|(?<word>[A-Za-z0-9]+))$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Pair();
}
