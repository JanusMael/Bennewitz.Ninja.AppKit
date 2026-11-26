using System.Globalization;
using System.Reflection;
using AppServices.Tests.Logging;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Headless;
using Avalonia.Threading;
using Bennewitz.Ninja.AppServices.AvaloniaUI;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using AvaloniaLevel = Avalonia.Logging.LogEventLevel;
using AvaloniaLogger = Avalonia.Logging.Logger;
using SerilogLevel = Serilog.Events.LogEventLevel;

namespace AppServices.Tests.AvaloniaUI;

/// <summary>
/// <see cref="SerilogAvaloniaSink"/>: the level it maps each of Avalonia's to, the areas it mutes, and the
/// line it writes in the family's form (<c>plans/00009</c>, decision 8), Avalonia's values rendered each as
/// its own text, and a value a binding's converter could not convert left out, by the maintainer's choice
/// of 2026-11-24.
/// </summary>
/// <remarks>
/// ⚠ <see cref="Log.Logger"/>, Avalonia's logger sink and the current culture are process-wide: a test that
/// sets one puts it back in <see cref="Dispose"/>. This assembly runs serially (Parallelization.cs).
/// </remarks>
public sealed class SerilogAvaloniaSinkTests : IDisposable
{
    private const string Typed = "typed-secret-7";

    private readonly ILogger _originalLogger = Log.Logger;
    private readonly Avalonia.Logging.ILogSink? _originalSink = AvaloniaLogger.Sink;
    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
    private readonly CollectingSink _captured = new();

    public SerilogAvaloniaSinkTests()
    {
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(_captured).CreateLogger();
    }

    private static HeadlessUnitTestSession Session =>
        HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

    public void Dispose()
    {
        AvaloniaLogger.Sink = _originalSink;
        Log.Logger = _originalLogger;
        CultureInfo.CurrentCulture = _originalCulture;
    }

    // -----------------------------------------------------------------------
    // Level mapping
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(AvaloniaLevel.Verbose, SerilogLevel.Verbose)]
    [InlineData(AvaloniaLevel.Debug, SerilogLevel.Debug)]
    [InlineData(AvaloniaLevel.Information, SerilogLevel.Information)]
    [InlineData(AvaloniaLevel.Warning, SerilogLevel.Warning)]
    [InlineData(AvaloniaLevel.Error, SerilogLevel.Error)]
    [InlineData(AvaloniaLevel.Fatal, SerilogLevel.Fatal)]
    public void Map_ConvertsEveryLevel_Correctly(AvaloniaLevel avalonia, SerilogLevel expected)
    {
        Assert.Equal(expected, SerilogAvaloniaSink.Map(avalonia));
    }

    [Fact]
    public void Map_UnknownLevel_ReturnInformation()
    {
        AvaloniaLevel unknown = (AvaloniaLevel)999;
        Assert.Equal(SerilogLevel.Information, SerilogAvaloniaSink.Map(unknown));
    }

    // -----------------------------------------------------------------------
    // IsEnabled
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(AvaloniaLevel.Verbose)]
    [InlineData(AvaloniaLevel.Debug)]
    [InlineData(AvaloniaLevel.Information)]
    [InlineData(AvaloniaLevel.Warning)]
    [InlineData(AvaloniaLevel.Error)]
    [InlineData(AvaloniaLevel.Fatal)]
    public void IsEnabled_True_ForUnmutedArea_AtAnyLevel(AvaloniaLevel level)
    {
        SerilogAvaloniaSink sink = new();
        Assert.True(sink.IsEnabled(level, "Binding"));
    }

    [Fact]
    public void IsEnabled_True_ForUnmutedAreas_BelowWarning()
    {
        SerilogAvaloniaSink sink = new();
        Assert.True(sink.IsEnabled(AvaloniaLevel.Debug, string.Empty));
        Assert.True(sink.IsEnabled(AvaloniaLevel.Debug, "Binding"));
        Assert.True(sink.IsEnabled(AvaloniaLevel.Information, "Binding"));
    }

    [Theory]
    [InlineData("Layout")]
    [InlineData("Property")]
    [InlineData("Visual")]
    public void IsEnabled_False_ForMutedAreas_BelowWarning(string area)
    {
        SerilogAvaloniaSink sink = new();
        Assert.False(sink.IsEnabled(AvaloniaLevel.Verbose, area));
        Assert.False(sink.IsEnabled(AvaloniaLevel.Debug, area));
        Assert.False(sink.IsEnabled(AvaloniaLevel.Information, area));
    }

    [Theory]
    [InlineData("Layout")]
    [InlineData("Property")]
    [InlineData("Visual")]
    public void IsEnabled_True_ForMutedAreas_AtWarningAndAbove(string area)
    {
        SerilogAvaloniaSink sink = new();
        Assert.True(sink.IsEnabled(AvaloniaLevel.Warning, area));
        Assert.True(sink.IsEnabled(AvaloniaLevel.Error, area));
        Assert.True(sink.IsEnabled(AvaloniaLevel.Fatal, area));
    }

    [Fact]
    public void IsEnabled_CustomMutedAreas_OverrideDefault()
    {
        // Layout is no longer muted — only "MyNoisyArea" is.
        SerilogAvaloniaSink sink = new(["MyNoisyArea"]);

        Assert.True(sink.IsEnabled(AvaloniaLevel.Debug, "Layout"));
        Assert.False(sink.IsEnabled(AvaloniaLevel.Debug, "MyNoisyArea"));
        Assert.True(sink.IsEnabled(AvaloniaLevel.Warning, "MyNoisyArea"));
    }

    [Fact]
    public void IsEnabled_EmptyMutedCollection_DisablesMutingEntirely()
    {
        SerilogAvaloniaSink sink = new([]);

        // Every default-muted area is now un-muted.
        Assert.True(sink.IsEnabled(AvaloniaLevel.Debug, "Layout"));
        Assert.True(sink.IsEnabled(AvaloniaLevel.Debug, "Property"));
        Assert.True(sink.IsEnabled(AvaloniaLevel.Debug, "Visual"));
    }

    // -----------------------------------------------------------------------
    // The line
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(AvaloniaLevel.Verbose, SerilogLevel.Verbose)]
    [InlineData(AvaloniaLevel.Information, SerilogLevel.Information)]
    [InlineData(AvaloniaLevel.Warning, SerilogLevel.Warning)]
    [InlineData(AvaloniaLevel.Fatal, SerilogLevel.Fatal)]
    public void Each_message_is_one_line_in_the_family_s_form_at_Avalonia_s_level(AvaloniaLevel avalonia, SerilogLevel expected)
    {
        new SerilogAvaloniaSink([]).Log(avalonia, "Control", null, "Measured {Name} at {Width}", "Panel", 12.5);

        LogEvent line = Assert.Single(_captured.Events);
        LogLineForm.Is(line, expected, PackageLogLines.AvaloniaLog, null);
        Assert.Equal("Control", LogLineForm.ValueOf(line, "Area"));
        Assert.Equal("Measured Panel at 12.5", LogLineForm.ValueOf(line, "Text"));
    }

    [Fact]
    public void Each_value_is_its_own_text_an_enum_by_its_name_and_the_rest_in_the_invariant_culture()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

        Assert.Equal(
            "Set DataContext to Vm with priority LocalValue",
            Text("Set {Property} to {$Value} with priority {Priority}", "DataContext", new NamedVm(), BindingPriority.LocalValue));
        Assert.Equal("Layout pass finished in 00:00:00.0022525", Text("Layout pass finished in {Time}", TimeSpan.FromTicks(22525)));
        Assert.Equal("Scale 2.50 of 1.5", Text("Scale {Value:F2} of {Other}", 2.5, 1.5));
        Assert.Equal("Since 2026-11-26", Text("Since {When:yyyy-MM-dd}", new DateTime(2026, 11, 26, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal("Item null of {Missing}", Text("Item {Value} of {Missing}", [null]));
        Assert.Equal("A {literal} and Panel", Text("A {{literal}} and {Name}", "Panel"));
    }

    [Fact]
    public void Positional_holes_take_their_values_by_number_and_an_exception_its_whole_text()
    {
        InvalidOperationException failure = new("the device was lost");

        Assert.Equal("second, then first", Text("{1}, then {0}", "first", "second"));
        Assert.Equal("Unable to initialize rendering: " + failure, Text("Unable to initialize rendering: {0}", failure));
    }

    [Fact]
    public void A_message_without_values_is_Avalonia_s_text_as_it_is()
    {
        new SerilogAvaloniaSink([]).Log(AvaloniaLevel.Warning, "Control", null, "Kept as {Written} and {{so}}");

        Assert.Equal("Kept as {Written} and {{so}}", LogLineForm.ValueOf(Assert.Single(_captured.Events), "Text"));
    }

    [Fact]
    public void A_value_whose_text_cannot_be_read_is_written_as_its_type_and_nothing_escapes_into_Avalonia()
    {
        new SerilogAvaloniaSink([]).Log(AvaloniaLevel.Warning, "Control", null, "Broken {Value}", new Unreadable());

        Assert.Equal("Broken " + typeof(Unreadable).FullName, LogLineForm.ValueOf(Assert.Single(_captured.Events), "Text"));
    }

    [Fact]
    public void In_the_Binding_area_a_failed_conversion_is_written_up_to_the_value_it_could_not_convert()
    {
        const string template = "An error occurred binding {Property} to {Expression}: {Message}";
        string conversion =
            $"Could not convert '{Typed}' (System.String) to 'System.Double' using 'ParseConverter': The input string '{Typed}' was not in a correct format.";

        Assert.Equal("An error occurred binding Text to Number", Text(template, "Text", "Number", conversion));

        // The controls: another Binding message keeps its text, and so does the same text in another area.
        Assert.Equal(
            "An error occurred binding Text to Number: no property",
            Text(template, "Text", "Number", "no property"));
        new SerilogAvaloniaSink([]).Log(AvaloniaLevel.Warning, "Property", null, template, "Text", "Number", conversion);
        Assert.Contains(Typed, Assert.IsType<string>(LogLineForm.ValueOf(_captured.Events[^1], "Text")), StringComparison.Ordinal);
    }

    [Fact]
    public Task Avalonia_12_1_3_s_warning_for_a_converter_that_threw_is_written_without_the_text_it_could_not_convert() =>
        Session.Dispatch(() =>
        {
            AvaloniaLogger.Sink = new SerilogAvaloniaSink();
            TextBox box = new() { DataContext = new NumberVm() };
            box.Bind(TextBox.TextProperty, new Binding(nameof(NumberVm.Number))
            {
                Mode = BindingMode.TwoWay,
                Converter = new ParseConverter(),
            });
            Window window = new() { Content = box };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            box.Text = Typed;
            Dispatcher.UIThread.RunJobs();
            window.Close();
            AvaloniaLogger.Sink = _originalSink;

            // ⛔ Pins Avalonia 12.1.3's wording: a newer Avalonia that words its conversion message otherwise
            // fails here, before the value it holds can reach a log.
            LogEvent line = Assert.Single(_captured.Events, e => AreaOf(e) == "Binding");
            LogLineForm.Is(line, SerilogLevel.Warning, PackageLogLines.AvaloniaLog, null);
            Assert.Equal("An error occurred binding Text to Number", LogLineForm.ValueOf(line, "Text"));
            Assert.All(_captured.Events, logEvent => Assert.DoesNotContain(Typed, Everything(logEvent), StringComparison.Ordinal));
        }, CancellationToken.None);

    /// <summary>The text of the one line a message gives, through a sink that mutes nothing.</summary>
    private string Text(string template, params object?[] values)
    {
        _captured.Events.Clear();
        new SerilogAvaloniaSink([]).Log(AvaloniaLevel.Warning, "Binding", null, template, values);
        return Assert.IsType<string>(LogLineForm.ValueOf(Assert.Single(_captured.Events), "Text"));
    }

    /// <summary>Everything an event carries as text: its template, each property's value and its exception.</summary>
    private static string Everything(LogEvent logEvent)
    {
        List<string> parts = [logEvent.MessageTemplate.Text];
        parts.AddRange(logEvent.Properties.Values.Select(value => value.ToString()));
        parts.Add(logEvent.Exception?.ToString() ?? "");
        return string.Join('\n', parts);
    }

    /// <summary>The Avalonia area a line names, or <see langword="null"/> for a line that names none.</summary>
    private static string? AreaOf(LogEvent logEvent) =>
        logEvent.Properties.TryGetValue("Area", out LogEventPropertyValue? value) && value is ScalarValue { Value: string area }
            ? area
            : null;

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    /// <summary>A value whose text is <c>Vm</c>, as a view model's type name reads.</summary>
    private sealed class NamedVm
    {
        public override string ToString() => "Vm";
    }

    /// <summary>A value whose text cannot be read.</summary>
    private sealed class Unreadable
    {
        public override string ToString() => throw new InvalidOperationException("no text");
    }

    /// <summary>What the binding reads: a number a text box edits.</summary>
    public sealed class NumberVm
    {
        /// <summary>The number.</summary>
        public double Number { get; set; } = 1.5;
    }

    /// <summary>Formats a number, and parses text back, throwing on text that is no number.</summary>
    private sealed class ParseConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is double number ? number.ToString(CultureInfo.InvariantCulture) : value?.ToString();

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            double.Parse(value as string ?? "", CultureInfo.InvariantCulture);

        public override string ToString() => "ParseConverter";
    }
}
