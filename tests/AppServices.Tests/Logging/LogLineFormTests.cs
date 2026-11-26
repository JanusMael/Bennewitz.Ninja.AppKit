using System.Reflection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit.Sdk;

namespace AppServices.Tests.Logging;

/// <summary>
/// <see cref="LogLineForm"/>, held to what it must refuse, since a check that cannot fail would pass every
/// line; and every template of <see cref="PackageLogLines"/>, held to the form.
/// </summary>
public sealed class LogLineFormTests
{
    public static TheoryData<string> PackageTemplates => new(
        typeof(PackageLogLines)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(constant => constant.IsLiteral)
            .Select(constant => (string)constant.GetRawConstantValue()!));

    [Fact]
    public void The_table_holds_every_template_a_test_reaches()
    {
        // Pinned, so that a template dropped from the table, or one added without a test, is seen here.
        Assert.Equal(18, PackageTemplates.Count);
    }

    [Theory]
    [MemberData(nameof(PackageTemplates))]
    public void Every_package_template_holds_the_form(string template)
    {
        LogLineForm.Holds(template);
    }

    [Theory]
    [InlineData("[App.Start] app={App} version={Version} pid={Pid}")]
    [InlineData("[App.Exit] signal=SIGTERM elapsedMs={ElapsedMs}")]
    [InlineData("[Dialog.Copy] reason=clipboardFailed path=\"{Path}\"")]
    [InlineData("[Shell.Launch] action=terminal reason=NotFound")]
    [InlineData("[App.Smoke] reason=unobservedTaskExceptions count={Count} code=1")]
    public void The_family_s_examples_hold(string template)
    {
        LogLineForm.Holds(template);
    }

    [Theory]
    [InlineData("A task exception nobody observed")]
    [InlineData("[App.Start]app={App}")]
    [InlineData("[app.Start] app={App}")]
    [InlineData("[App] app={App}")]
    [InlineData("[App.Start] App={App}")]
    [InlineData("[App.Start] app={App} ")]
    [InlineData("[App.Start]  app={App}")]
    [InlineData("[App.Start] app={App:l}")]
    [InlineData("[App.Start] app={0}")]
    [InlineData("[App.Start] app={@App}")]
    [InlineData("[App.Start] app={$App}")]
    [InlineData("[Failure.Repeat] count={RepeatCount}")]
    [InlineData("[Failure.Repeat] count={count}")]
    [InlineData("[Failure.Repeat] type={Type} type={Type}")]
    [InlineData("[Failure.Repeat] message=\"{Message}")]
    [InlineData("[Failure.Repeat] message='{Message}'")]
    [InlineData("[Failure.Repeat] reason=repeated: more")]
    public void A_template_out_of_the_form_is_refused(string template)
    {
        Assert.ThrowsAny<XunitException>(() => LogLineForm.Holds(template));
    }

    [Theory]
    [InlineData(LogEventLevel.Warning)]
    [InlineData(LogEventLevel.Error)]
    [InlineData(LogEventLevel.Fatal)]
    public void A_line_at_Warning_or_above_without_its_reason_is_refused(LogEventLevel level)
    {
        Assert.ThrowsAny<XunitException>(() => LogLineForm.Holds(Write(level, "[Task.Unobserved] benign=false")));

        // The controls: with its reason, and Avalonia's own line, whose text is its reason.
        LogLineForm.Holds(Write(level, "[Task.Unobserved] reason=notObserved benign=false"));
        LogLineForm.Holds(Write(level, PackageLogLines.AvaloniaLog, "Binding", "An error occurred"));
    }

    [Fact]
    public void A_value_the_form_would_quote_or_cannot_write_is_refused()
    {
        Assert.ThrowsAny<XunitException>(() => LogLineForm.Holds(Write(LogEventLevel.Information, "[App.Smoke] level={Level}", LogEventLevel.Debug)));
        Assert.ThrowsAny<XunitException>(() => LogLineForm.Holds(Write(LogEventLevel.Information, "[App.Smoke] at={At}", DateTimeOffset.UnixEpoch)));
        Assert.ThrowsAny<XunitException>(() => LogLineForm.Holds(Write(LogEventLevel.Information, "[App.Smoke] took={Took}", TimeSpan.FromSeconds(1))));
        Assert.ThrowsAny<XunitException>(() => LogLineForm.Holds(Write(LogEventLevel.Information, "[App.Smoke] lifetime={Lifetime}", [null])));
        Assert.ThrowsAny<XunitException>(() => LogLineForm.Holds(Write(LogEventLevel.Information, "[App.Smoke] items={Items}", new[] { 1, 2 })));
        Assert.ThrowsAny<XunitException>(() => LogLineForm.Holds(Write(LogEventLevel.Information, "[App.Smoke] app={App}")));

        // The controls: text, a number and a bool, each as it is.
        LogLineForm.Holds(Write(LogEventLevel.Information, "[App.Smoke] app={App} count={Count} benign={Benign}", "Tool", 3L, true));
    }

    [Fact]
    public void A_bare_value_that_would_run_into_the_next_pair_is_refused()
    {
        Assert.ThrowsAny<XunitException>(() => LogLineForm.Holds(Write(LogEventLevel.Information, "[App.Start] app={App}", "My App")));
        Assert.ThrowsAny<XunitException>(() => LogLineForm.Holds(Write(LogEventLevel.Information, "[App.Start] app={App}", "say \"hi\"")));
        Assert.ThrowsAny<XunitException>(() => LogLineForm.Holds(Write(LogEventLevel.Information, "[App.Start] app={App}", "")));

        // The control: the same text quoted by hand.
        LogLineForm.Holds(Write(LogEventLevel.Information, "[Dialog.Copy] reason=clipboardFailed path=\"{Path}\"", "C:\\My Files\\a.txt"));
    }

    private static LogEvent Write(LogEventLevel level, string template, params object?[] values)
    {
        Capture capture = new();
        using Logger logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(capture).CreateLogger();
        logger.Write(level, template, values);
        return Assert.Single(capture.Events);
    }

    private sealed class Capture : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
