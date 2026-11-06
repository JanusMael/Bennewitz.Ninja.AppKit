using System.Diagnostics.CodeAnalysis;
using Xunit.Sdk;

namespace Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests;

/// <summary>
/// xunit's own assertions, with the explanatory message the original MSTest assertion carried.
/// </summary>
/// <remarks>
/// <para>
/// xunit deliberately gives <c>Equal</c>, <c>Null</c>, <c>Same</c> and <c>Contains</c> no message
/// parameter. These tests were ported from MSTest, where a message is routinely the whole point of
/// the assertion: it says WHY the value matters, which is what a reader needs when it fails.
/// Dropping 67 of them to fit the API would have thrown that away.
/// </para>
/// <para>
/// ⭐ Each method CALLS xunit's assertion and prefixes the message only when it fails, so a failure
/// still shows xunit's expected/actual diff, and every argument is evaluated exactly once. The
/// alternative, <c>Assert.True(Equals(e, a), message)</c>, loses the diff and evaluates twice.
/// </para>
/// <para>
/// ⚠ Not an MSTest compatibility layer, and not for new tests: it exists for the ported assertions
/// that carried a message. New tests use xunit's Assert directly.
/// </para>
/// </remarks>
internal static class MessageAssert
{
    public static void Equal<T>(T expected, T actual, string message) =>
        With(message, () => Assert.Equal(expected, actual));

    /// <summary>
    /// MSTest's AreEqual(double, double, delta, message). xunit's tolerance overload is the same
    /// comparison, |expected - actual| &lt;= tolerance, without the message.
    /// </summary>
    public static void Equal(double expected, double actual, double tolerance, string message) =>
        With(message, () => Assert.Equal(expected, actual, tolerance));

    public static void NotEqual<T>(T expected, T actual, string message) =>
        With(message, () => Assert.NotEqual(expected, actual));

    public static void Null(object? value, string message) =>
        With(message, () => Assert.Null(value));

    /// <remarks>
    /// Written out rather than delegated: [NotNull] promises the caller's flow analysis that the
    /// value is non-null afterwards, and the compiler cannot see that promise kept through a lambda.
    /// </remarks>
    public static void NotNull([NotNull] object? value, string message)
    {
        if (value is null)
        {
            throw new XunitException(message + Environment.NewLine + "Assert.NotNull() Failure: Value is null");
        }
    }

    public static void Same(object? expected, object? actual, string message) =>
        With(message, () => Assert.Same(expected, actual));

    public static void Contains<T>(T expected, IEnumerable<T> collection, string message) =>
        With(message, () => Assert.Contains(expected, collection));

    public static void Contains(string expectedSubstring, string? actualString, string message) =>
        With(message, () => Assert.Contains(expectedSubstring, actualString));

    public static T IsAssignableFrom<T>(object? value, string message)
    {
        T result = default!;
        With(message, () => result = Assert.IsAssignableFrom<T>(value));
        return result;
    }

    private static void With(string message, Action assertion)
    {
        try
        {
            assertion();
        }
        catch (XunitException failure)
        {
            throw new XunitException(message + Environment.NewLine + failure.Message, failure);
        }
    }
}
