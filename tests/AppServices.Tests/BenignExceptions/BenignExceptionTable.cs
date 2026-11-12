namespace AppServices.Tests.BenignExceptions;

/// <summary>
/// The one table both copies of the benign-exception rule are held to (<c>plans/00002</c>, decision
/// 3): the copy in <c>AppServices.EntryPoint</c>, which decides what reaches stderr, and the copy in
/// <c>AppServices.AvaloniaUI</c>, which decides the UI-thread handler's level and dialog, and what
/// <c>EntryPointOptions()</c>' callback logs and shows.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>One table, never a copy per test class.</b> The two copies of the rule exist because the
/// packages may not share an internal; what keeps them the same rule is that every test of either
/// copy, and of the behaviour it drives, reads these rows, so a row added here reaches all of them.
/// </para>
/// <para>
/// Every row is classified with <see cref="Filter"/>: a <see cref="BenignNoise"/> is benign, a
/// <see cref="FilterBreaker"/> makes the filter throw, and an <see cref="AggregateException"/> makes it
/// throw too, because the rule must offer the filter what is inside an aggregate and never the
/// aggregate itself.
/// </para>
/// <para>
/// ⚠ <b>Each row has a verdict for each path</b> (<c>plans/00003</c>, decision 7). The filter calls a
/// cancellation not benign, as a host that shows the cancellations escaping on the UI thread does. On
/// an Avalonia app's unobserved path, the filter <c>EntryPointOptions()</c> passes on also calls a
/// cancellation benign, so there an exception that is nothing but cancellations is benign
/// (<see cref="Row.BenignUnobserved"/>).
/// </para>
/// </remarks>
internal static class BenignExceptionTable
{
    /// <summary>What the filter throws on a <see cref="FilterBreaker"/>.</summary>
    internal const string FilterBug = "the filter's own bug";

    /// <summary>The message of every exception in the table the filter does not call benign.</summary>
    internal const string NotBenign = "not benign: a real failure";

    /// <summary>The message of every cancellation in the table.</summary>
    internal const string Cancelled = "cancelled: abandoned work";

    /// <summary>Every case of the rule, each with the verdict it must get.</summary>
    internal static IReadOnlyList<Row> Rows { get; } =
    [
        new("a benign exception", () => new BenignNoise(), Benign: true, FilterThrows: false),
        new("any other exception", () => new InvalidOperationException(NotBenign), Benign: false, FilterThrows: false),
        new("an exception the filter throws on", () => new FilterBreaker(), Benign: false, FilterThrows: true),
        new("a benign exception inside one that is not",
            () => new InvalidOperationException(NotBenign, new BenignNoise()), Benign: false, FilterThrows: false),
        new("an aggregate of one benign exception",
            () => new AggregateException(new BenignNoise()), Benign: true, FilterThrows: false),
        new("an aggregate of benign exceptions only",
            () => new AggregateException(new BenignNoise(), new BenignNoise()), Benign: true, FilterThrows: false),
        new("an aggregate with one exception that is not benign",
            () => new AggregateException(new BenignNoise(), new InvalidOperationException(NotBenign)),
            Benign: false, FilterThrows: false),
        new("an empty aggregate", () => new AggregateException(), Benign: false, FilterThrows: false),
        new("nested aggregates of benign exceptions only",
            () => new AggregateException(new AggregateException(new BenignNoise()), new BenignNoise()),
            Benign: true, FilterThrows: false),
        new("nested aggregates with one exception that is not benign, deep inside",
            () => new AggregateException(
                new BenignNoise(),
                new AggregateException(new BenignNoise(), new InvalidOperationException(NotBenign))),
            Benign: false, FilterThrows: false),
        new("an aggregate with one exception the filter throws on",
            () => new AggregateException(new BenignNoise(), new FilterBreaker()), Benign: false, FilterThrows: true),
        new("a cancellation", () => new OperationCanceledException(Cancelled),
            Benign: false, FilterThrows: false, Unobserved: true),
        new("a task's cancellation", () => new TaskCanceledException(Cancelled),
            Benign: false, FilterThrows: false, Unobserved: true),
        new("an aggregate of cancellations only",
            () => new AggregateException(new OperationCanceledException(Cancelled), new TaskCanceledException(Cancelled)),
            Benign: false, FilterThrows: false, Unobserved: true),
        new("a cancellation beside a real failure",
            () => new AggregateException(new OperationCanceledException(Cancelled), new InvalidOperationException(NotBenign)),
            Benign: false, FilterThrows: false, Unobserved: false),
        new("a cancellation beside a benign exception",
            () => new AggregateException(new OperationCanceledException(Cancelled), new BenignNoise()),
            Benign: false, FilterThrows: false, Unobserved: true),
    ];

    /// <summary>
    /// The filter every row is classified with. Quick and thread-safe, as a host's must be: it reads
    /// nothing but its argument.
    /// </summary>
    internal static bool Filter(Exception exception) => exception switch
    {
        AggregateException => throw new InvalidOperationException(
            "The rule offered the filter an AggregateException; it must offer what is inside one."),
        BenignNoise => true,
        FilterBreaker => throw new InvalidOperationException(FilterBug),
        _ => false,
    };

    /// <summary>Every row's name, as theory data. The theory data is public, as <c>MemberData</c> reads it.</summary>
    public static TheoryData<string> AllRows => Names(_ => true);

    /// <summary>The rows the rule calls benign.</summary>
    public static TheoryData<string> BenignRows => Names(row => row.Benign);

    /// <summary>The rows that are not benign, and on which the filter does not throw.</summary>
    public static TheoryData<string> NotBenignRows => Names(row => !row.Benign && !row.FilterThrows);

    /// <summary>The rows on which the filter throws, which are therefore not benign.</summary>
    public static TheoryData<string> FilterThrowsRows => Names(row => row.FilterThrows);

    /// <summary>
    /// Every row's name, once with <c>ShowUnobservedTaskExceptions</c> on and once with it off, for the
    /// tests of an Avalonia app's unobserved path.
    /// </summary>
    public static TheoryData<string, bool> AllRowsWithShowOnAndOff
    {
        get
        {
            TheoryData<string, bool> data = [];
            foreach (Row row in Rows)
            {
                data.Add(row.Name, true);
                data.Add(row.Name, false);
            }

            return data;
        }
    }

    /// <summary>The row named <paramref name="name"/>.</summary>
    internal static Row Get(string name) => Rows.Single(row => row.Name == name);

    private static TheoryData<string> Names(Func<Row, bool> which) => [.. Rows.Where(which).Select(row => row.Name)];

    /// <summary>One case of the rule.</summary>
    /// <param name="Name">What the case is, which names the test that runs it.</param>
    /// <param name="Make">Makes a fresh exception for the case: Avalonia's dispatcher offers an
    /// exception object to its handlers once, so a test never throws one twice.</param>
    /// <param name="Benign">The verdict the rule must give with <see cref="Filter"/>: the UI-thread
    /// handler's, and the entry point's when the table's filter is its own.</param>
    /// <param name="FilterThrows">Whether the filter throws while the rule classifies the case.</param>
    /// <param name="Unobserved">The verdict on an Avalonia app's unobserved path, where it differs from
    /// <paramref name="Benign"/> or the row is about that path; read through <see cref="BenignUnobserved"/>.</param>
    internal sealed record Row(string Name, Func<Exception> Make, bool Benign, bool FilterThrows, bool? Unobserved = null)
    {
        /// <summary>
        /// The verdict on an Avalonia app's unobserved path: the rule with the filter
        /// <c>EntryPointOptions()</c> passes on, which also calls a cancellation benign.
        /// </summary>
        internal bool BenignUnobserved => Unobserved ?? Benign;
    }

    /// <summary>Stands in for the exceptions a host knows to be harmless, the Linux D-Bus portal ones.</summary>
    internal sealed class BenignNoise() : Exception("benign noise, as the D-Bus portal ones are");

    /// <summary>The one exception the filter throws on.</summary>
    internal sealed class FilterBreaker() : Exception("the filter throws on this one");
}
