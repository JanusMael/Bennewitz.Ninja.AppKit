using System.Globalization;
using System.Text;
using Avalonia.Logging;
using Serilog.Parsing;
using AvaloniaLevel = Avalonia.Logging.LogEventLevel;
using SerilogLevel = Serilog.Events.LogEventLevel;
using SerilogLog = Serilog.Log;
using SerilogTemplate = Serilog.Events.MessageTemplate;

namespace Bennewitz.Ninja.AppServices.AvaloniaUI;

/// <summary>
/// Bridges Avalonia's internal logging (<see cref="global::Avalonia.Logging.Logger"/>)
/// into the Serilog pipeline.
/// <para>
/// Avalonia exposes <see cref="global::Avalonia.Logging.Logger.Sink"/> as a single
/// pluggable entry point. Pointing it at an instance of this class causes every
/// Avalonia log event (binding errors, layout warnings, rendering diagnostics, etc.)
/// to be re-emitted on <see cref="SerilogLog.Logger"/>. From there the normal
/// Serilog sinks (file, Trace, F12 window, …) pick the events up.
/// </para>
/// <para>
/// Typical usage replaces the default <c>.LogToTrace()</c> call in the Avalonia
/// bootstrap — events still end up on <see cref="System.Diagnostics.Trace"/> if
/// the Serilog pipeline includes <c>WriteTo.Trace()</c>, so the debugger's Output
/// window remains populated.
/// </para>
/// <para>
/// <strong>The line.</strong> Each event is written at Avalonia's level as one line in the family's form,
/// <c>[Avalonia.Log] area={Area} text="{Text}"</c> (<c>plans/00009</c>, decision 8). Its text is Avalonia's
/// message with its values in it, each as its own text: a string as it is, an enum by its name, an exception
/// as its whole text, and anything else that formats in the invariant culture. Avalonia's template is parsed
/// as Serilog parses one: named holes take the values in order, positional ones, when every hole is
/// positional, take theirs by number, and a hole with no value keeps its own text. Serilog's rendering would
/// quote a string, and <c>{Message:lj}</c> an enum, a date, a time span or a guid, inside the quoted text.
/// Avalonia's own properties are kept in the text alone.
/// </para>
/// <para>
/// ⚠ <strong>A failed conversion leaves its value out.</strong> When a binding's converter throws, Avalonia
/// 12.1.3 writes a <c>Binding</c> warning that holds the value it could not convert, twice: in its own words
/// and in the converter's message. In the <c>Binding</c> area the line keeps Avalonia's text up to that
/// conversion message, by the maintainer's choice of 2026-11-24, so that a text a person typed never reaches
/// the log through it.
/// </para>
/// <para>
/// <strong>Area muting.</strong> Avalonia's <c>Layout</c>, <c>Property</c>, and
/// <c>Visual</c> areas fire per measure/arrange/property-change pass at Information
/// level and saturate any log view during normal UI activity. By default this sink
/// suppresses those three areas below <see cref="AvaloniaLevel.Warning"/>. Warnings
/// and above still pass through so genuine problems aren't hidden. Pass a different
/// collection to the constructor to override.
/// </para>
/// </summary>
public sealed class SerilogAvaloniaSink : ILogSink
{
    /// <summary>The family's line for Avalonia's message (<c>plans/00009</c>, decision 8).</summary>
    internal const string LineTemplate = "[Avalonia.Log] area={Area} text=\"{Text}\"";

    /// <summary>
    /// How Avalonia 12.1.3 begins its message for a value a binding's converter could not convert
    /// (<c>UntypedBindingExpressionBase.Convert</c> and <c>ConvertBack</c>): the value follows it.
    /// </summary>
    internal const string FailedConversion = "Could not convert '";

    private static readonly MessageTemplateParser Parser = new();

    /// <summary>
    /// Default set of Avalonia areas whose sub-Warning events are suppressed:
    /// <c>Layout</c>, <c>Property</c>, <c>Visual</c>. Rationale — each one fires
    /// at Information level on every layout/property/rendering pass and
    /// saturates log viewers during normal UI activity.
    /// </summary>
    public static IReadOnlyCollection<string> DefaultMutedAreas { get; } =
        ["Layout", "Property", "Visual"];

    private readonly HashSet<string> _mutedBelowWarning;

    /// <summary>
    /// Creates the sink. Pass <paramref name="mutedAreas"/> to override the
    /// default set of areas whose sub-Warning events are suppressed.
    /// </summary>
    /// <param name="mutedAreas">Areas whose events below
    /// <see cref="AvaloniaLevel.Warning"/> are dropped before reaching Serilog.
    /// Passing <c>null</c> (the default) uses <see cref="DefaultMutedAreas"/>.
    /// Pass an empty collection to disable muting entirely.</param>
    public SerilogAvaloniaSink(IReadOnlyCollection<string>? mutedAreas = null)
    {
        _mutedBelowWarning = new HashSet<string>(
            mutedAreas ?? DefaultMutedAreas,
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Returns <c>false</c> for muted Avalonia areas below
    /// <see cref="AvaloniaLevel.Warning"/>, so they never even reach the Serilog
    /// pipeline. Warnings and errors always pass. All other areas defer to
    /// Serilog's own minimum-level filter.
    /// </summary>
    public bool IsEnabled(AvaloniaLevel level, string area)
    {
        if (level < AvaloniaLevel.Warning && _mutedBelowWarning.Contains(area))
        {
            return false;
        }

        return true;
    }

    /// <inheritdoc/>
    /// <remarks>A message with no values is Avalonia's text as it is, holes and braces included.</remarks>
    public void Log(AvaloniaLevel level, string area, object? source, string messageTemplate)
    {
        if (!IsEnabled(level, area))
        {
            return;
        }

        SerilogLevel sLevel = Map(level);
        if (!SerilogLog.Logger.IsEnabled(sLevel))
        {
            return;
        }

        Write(sLevel, area, messageTemplate, null);
    }

    /// <inheritdoc/>
    public void Log(AvaloniaLevel level, string area, object? source, string messageTemplate,
                    params object?[] propertyValues)
    {
        if (!IsEnabled(level, area))
        {
            return;
        }

        SerilogLevel sLevel = Map(level);
        if (!SerilogLog.Logger.IsEnabled(sLevel))
        {
            return;
        }

        Write(sLevel, area, messageTemplate, propertyValues);
    }

    /// <summary>
    /// Maps Avalonia's <see cref="AvaloniaLevel"/> to Serilog's
    /// <see cref="SerilogLevel"/> (same names, different namespaces).
    /// Exposed as <c>internal static</c> so unit tests can exercise every
    /// branch without spinning up a real log sink.
    /// </summary>
    internal static SerilogLevel Map(AvaloniaLevel level)
    {
        return level switch
        {
            AvaloniaLevel.Verbose => SerilogLevel.Verbose,
            AvaloniaLevel.Debug => SerilogLevel.Debug,
            AvaloniaLevel.Information => SerilogLevel.Information,
            AvaloniaLevel.Warning => SerilogLevel.Warning,
            AvaloniaLevel.Error => SerilogLevel.Error,
            AvaloniaLevel.Fatal => SerilogLevel.Fatal,
            var _ => SerilogLevel.Information,
        };
    }

    /// <summary>
    /// Avalonia's <paramref name="messageTemplate"/> with <paramref name="values"/> in it, each as its own
    /// text, parsed and bound as Serilog parses and binds a template (see the class's remarks).
    /// </summary>
    internal static string Render(string messageTemplate, object?[] values)
    {
        SerilogTemplate parsed = Parser.Parse(messageTemplate);
        List<PropertyToken> holes = [.. parsed.Tokens.OfType<PropertyToken>()];
        bool positional = holes.Count > 0 && holes.TrueForAll(hole => hole.IsPositional);

        // By name, as Serilog renders a bound template: a name a template repeats takes its last value.
        Dictionary<string, object?> bound = new(StringComparer.Ordinal);
        for (int i = 0; i < holes.Count; i++)
        {
            PropertyToken hole = holes[i];
            int index = positional && hole.TryGetPositionalValue(out int position) ? position : i;
            if (index >= 0 && index < values.Length)
            {
                bound[hole.PropertyName] = values[index];
            }
        }

        StringBuilder text = new();
        foreach (MessageTemplateToken token in parsed.Tokens)
        {
            if (token is not PropertyToken hole)
            {
                text.Append(((TextToken)token).Text);
                continue;
            }

            if (!bound.TryGetValue(hole.PropertyName, out object? value))
            {
                text.Append(hole.ToString());
                continue;
            }

            string own = OwnText(value, hole.Format);
            text.Append(hole.Alignment is { } alignment ? Aligned(own, alignment) : own);
        }

        return text.ToString();
    }

    /// <summary>
    /// <paramref name="text"/> as the line writes it for <paramref name="area"/>: in the <c>Binding</c> area,
    /// up to the conversion message, when it holds one, since that message holds the value that could not be
    /// converted, in Avalonia's words and in the converter's (the maintainer's choice of 2026-11-24).
    /// </summary>
    internal static string LeaveOutFailedConversion(string area, string text)
    {
        if (!string.Equals(area, LogArea.Binding, StringComparison.Ordinal))
        {
            return text;
        }

        int at = text.IndexOf(FailedConversion, StringComparison.Ordinal);
        return at < 0 ? text : text[..at].TrimEnd(' ', ':');
    }

    private static void Write(SerilogLevel level, string area, string messageTemplate, object?[]? values)
    {
        try
        {
            string text = values is null ? messageTemplate : Render(messageTemplate, values);
            SerilogLog.Logger.Write(level, LineTemplate, area, LeaveOutFailedConversion(area, text));
        }
        catch (Exception)
        {
            // ⛔ Called from inside Avalonia, from a render loop's handler among others: nothing may escape
            // into it, as Serilog's own capture of a value lets nothing escape.
        }
    }

    // A value as its own text: a string as it is, an enum by its name, an exception as its whole text, and
    // anything else that formats in the invariant culture, with its hole's format, as {0:F2} has it.
    private static string OwnText(object? value, string? format)
    {
        try
        {
            return value switch
            {
                null => "null",
                string text => text,
                Enum named => named.ToString(),
                IFormattable formattable => formattable.ToString(format, CultureInfo.InvariantCulture),
                var _ => value.ToString() ?? "",
            };
        }
        catch (Exception)
        {
            // A value whose text cannot be read is written as its type, as the entry point reports such an
            // exception.
            return value!.GetType().FullName ?? value.GetType().Name;
        }
    }

    private static string Aligned(string text, Alignment alignment) =>
        alignment.Direction == AlignmentDirection.Left
            ? text.PadRight(alignment.Width)
            : text.PadLeft(alignment.Width);
}
