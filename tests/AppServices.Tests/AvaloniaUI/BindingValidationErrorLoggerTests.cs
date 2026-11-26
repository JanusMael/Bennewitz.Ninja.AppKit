using System.Reflection;
using AppServices.Tests.Logging;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Threading;
using Bennewitz.Ninja.AppServices.AvaloniaUI.Binding;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AppServices.Tests.AvaloniaUI;

/// <summary>
/// <see cref="BindingValidationErrorLogger"/>'s two lines (<c>plans/00009</c>, decision 7): an error a
/// binding's validation holds is written in the family's form, an exception's by its type and a stack, and a
/// value the binding could not convert is in nothing the line carries, its text box's text given as its length.
/// </summary>
/// <remarks>
/// ⚠ The class handler is process-wide: each test installs it and <see cref="Dispose"/> takes it off, as it
/// puts <see cref="Log.Logger"/> back. This assembly runs serially (Parallelization.cs).
/// </remarks>
public sealed class BindingValidationErrorLoggerTests : IDisposable
{
    private const string Typed = "typed-secret-9";

    private readonly ILogger _originalLogger = Log.Logger;
    private readonly CollectingSink _captured = new();

    public BindingValidationErrorLoggerTests()
    {
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(_captured).CreateLogger();
        BindingValidationErrorLogger.Install();
    }

    private static HeadlessUnitTestSession Session =>
        HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

    public void Dispose()
    {
        BindingValidationErrorLogger.ResetForTests();
        Log.Logger = _originalLogger;
    }

    [Fact]
    public Task A_value_the_binding_could_not_convert_is_in_nothing_its_line_carries() => Session.Dispatch(() =>
    {
        TextBox box = new() { Name = "Amount", DataContext = new AmountVm() };
        box.Bind(TextBox.TextProperty, new Binding(nameof(AmountVm.Amount)) { Mode = BindingMode.TwoWay });
        Window window = new() { Content = box };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        box.Text = Typed;
        Dispatcher.UIThread.RunJobs();
        window.Close();

        // The control: Avalonia did hold the error, and its exception's message does carry the text typed.
        Exception held = Assert.IsAssignableFrom<Exception>(Assert.Single(DataValidationErrors.GetErrors(box) ?? []));
        Assert.Contains(Typed, held.Message, StringComparison.Ordinal);

        LogEvent line = Assert.Single(_captured.Events);
        LogLineForm.Is(line, LogEventLevel.Warning, PackageLogLines.BindingValidationException, null);
        Assert.Equal("TextBox#Amount", LogLineForm.ValueOf(line, "Control"));
        Assert.Equal(typeof(AmountVm).FullName, LogLineForm.ValueOf(line, "DataContext"));
        Assert.Equal(held.GetType().FullName, LogLineForm.ValueOf(line, "Type"));
        Assert.Contains($"TextLength={Typed.Length}", Assert.IsType<string>(LogLineForm.ValueOf(line, "Context")), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(Assert.IsType<string>(LogLineForm.ValueOf(line, "Stack"))));
        Assert.DoesNotContain(Typed, Everything(line), StringComparison.Ordinal);
    }, CancellationToken.None);

    [Fact]
    public Task An_error_that_is_no_exception_is_written_as_the_app_gives_it() => Session.Dispatch(() =>
    {
        TextBox box = new() { Name = "Amount", DataContext = new AmountVm() };

        DataValidationErrors.SetErrors(box, ["the amount must be positive"]);

        LogEvent line = Assert.Single(_captured.Events);
        LogLineForm.Is(line, LogEventLevel.Warning, PackageLogLines.BindingValidationError, null);
        Assert.Equal("TextBox#Amount", LogLineForm.ValueOf(line, "Control"));
        Assert.Equal("the amount must be positive", LogLineForm.ValueOf(line, "Error"));
        Assert.Contains("TextLength=0", Assert.IsType<string>(LogLineForm.ValueOf(line, "Context")), StringComparison.Ordinal);
    }, CancellationToken.None);

    /// <summary>Everything an event carries as text: its template, each property's value and its exception.</summary>
    private static string Everything(LogEvent logEvent)
    {
        List<string> parts = [logEvent.MessageTemplate.Text];
        parts.AddRange(logEvent.Properties.Values.Select(value => value.ToString()));
        parts.Add(logEvent.Exception?.ToString() ?? "");
        return string.Join('\n', parts);
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    /// <summary>What the binding reads: an amount a text box edits.</summary>
    public sealed class AmountVm
    {
        /// <summary>The amount.</summary>
        public double Amount { get; set; } = 1.5;
    }
}
