using EntryPointRule = Bennewitz.Ninja.AppServices.EntryPoint.BenignExceptionRule;

namespace AppServices.Tests.BenignExceptions;

/// <summary>
/// The benign-exception rule (<c>plans/00002</c>, decision 3) on its own, each copy of it held to every
/// row of <see cref="BenignExceptionTable"/>.
/// </summary>
/// <remarks>
/// The behaviour each copy drives is held to the same rows where it is tested: what reaches stderr in
/// <c>AppMainTests</c>.
/// </remarks>
public sealed class BenignExceptionRuleTests
{
    [Theory]
    [MemberData(nameof(BenignExceptionTable.AllRows), MemberType = typeof(BenignExceptionTable))]
    public void The_EntryPoint_copy_gives_every_row_its_verdict(string row)
    {
        BenignExceptionTable.Row expected = BenignExceptionTable.Get(row);

        bool benign = EntryPointRule.IsBenign(BenignExceptionTable.Filter, expected.Make(), out Exception? filterFailure);

        Assert.Equal(expected.Benign, benign);
        AssertFilterFailure(expected, filterFailure);
    }

    [Fact]
    public void Without_a_filter_the_EntryPoint_copy_calls_nothing_benign()
    {
        foreach (BenignExceptionTable.Row row in BenignExceptionTable.Rows)
        {
            Assert.False(EntryPointRule.IsBenign(null, row.Make(), out Exception? filterFailure), row.Name);
            Assert.Null(filterFailure);
        }
    }

    /// <summary>
    /// Every theory above iterates the table, and an empty or shrunken table passes all of them.
    /// </summary>
    [Fact]
    public void The_table_holds_every_case_of_the_rule()
    {
        Assert.True(BenignExceptionTable.Rows.Count == 11,
            $"The table has {BenignExceptionTable.Rows.Count} rows where 11 are expected. A row added or "
            + "removed on purpose updates this count; a lower count without a removal means rows were lost.");
        Assert.NotEmpty(BenignExceptionTable.BenignRows);
        Assert.NotEmpty(BenignExceptionTable.NotBenignRows);
        Assert.NotEmpty(BenignExceptionTable.FilterThrowsRows);
        Assert.Contains(BenignExceptionTable.Rows, row => row.Make() is AggregateException { InnerExceptions.Count: 0 });
        Assert.Contains(BenignExceptionTable.Rows,
            row => row.Make() is AggregateException aggregate && aggregate.InnerExceptions.Any(e => e is AggregateException));
    }

    private static void AssertFilterFailure(BenignExceptionTable.Row expected, Exception? filterFailure)
    {
        if (expected.FilterThrows)
        {
            Assert.NotNull(filterFailure);
            Assert.Equal(BenignExceptionTable.FilterBug, filterFailure.Message);
        }
        else
        {
            Assert.Null(filterFailure);
        }
    }
}
