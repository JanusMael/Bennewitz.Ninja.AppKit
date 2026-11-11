namespace Bennewitz.Ninja.AppServices.EntryPoint;

/// <summary>
/// Whether the app's filter calls an exception benign: the rule of <c>plans/00002</c>, decision 3.
/// </summary>
/// <remarks>
/// <para>
/// An exception is benign when the filter matches it. An <see cref="AggregateException"/> is benign
/// when it flattens to at least one exception and the filter matches every one of them; the aggregate
/// itself is never offered to the filter. A task exception nobody observed always arrives wrapped, so
/// a filter written for the exception a task threw would otherwise never match one. A filter that
/// throws counts as not benign, and its throw is handed back, to be reported beside the exception.
/// </para>
/// <para>
/// ⛔ <b>One of two copies, held to one table.</b> <c>AppServices.AvaloniaUI</c> keeps the other, and
/// must never call this one: the two packages can resolve at different versions, and an internal
/// renamed here would fail there at run time, not at build time. The tests'
/// <c>BenignExceptionTable</c> holds both copies to the same rows, so change them together.
/// </para>
/// </remarks>
internal static class BenignExceptionRule
{
    /// <summary>Whether <paramref name="filter"/> calls <paramref name="exception"/> benign.</summary>
    /// <param name="filter">The app's filter. Without one, nothing is benign.</param>
    /// <param name="exception">The exception to classify.</param>
    /// <param name="filterFailure">What the filter threw, when it threw; the exception then counts as
    /// not benign.</param>
    internal static bool IsBenign(Func<Exception, bool>? filter, Exception exception, out Exception? filterFailure)
    {
        filterFailure = null;
        if (filter is null)
        {
            return false;
        }

        try
        {
            if (exception is not AggregateException aggregate)
            {
                return filter(exception);
            }

            IReadOnlyList<Exception> inner = aggregate.Flatten().InnerExceptions;
            if (inner.Count == 0)
            {
                return false;
            }

            foreach (Exception each in inner)
            {
                if (!filter(each))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception thrown)
        {
            filterFailure = thrown;
            return false;
        }
    }
}
