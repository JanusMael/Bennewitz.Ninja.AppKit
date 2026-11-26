using System.Diagnostics.CodeAnalysis;

namespace Bennewitz.Ninja.AppServices.Abstractions;

/// <summary>
/// Keeps a secret out of a log line: decides by a value's key whether the value may be written, and gives
/// <see cref="Redacted"/> in its place when it may not.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>One rule for every line that writes a value under its key</b>, such as
/// <c>[Settings.Change] key={Key} value="{Value}"</c>, which takes <c>LogRedaction.Redact(key, value)</c> as its
/// value. It is here, in the package that references nothing, so that a library that logs only through an
/// <c>ILogger</c> can call it as well.
/// </para>
/// <para>
/// ⚠ <b>It reads the key, never the value.</b> A secret under a key it does not recognise is written as it is,
/// so a line that writes what a person typed or pasted needs a key that names it, or no value at all.
/// </para>
/// </remarks>
public static class LogRedaction
{
    /// <summary>What a log line writes in place of a value under a sensitive key: <c>***</c>.</summary>
    public const string Redacted = "***";

    private static readonly string[] _segments = ["env", "headers", "credentials", "auth", "authorization"];

    private static readonly string[] _substrings =
        ["token", "secret", "password", "apikey", "api_key", "api-key", "bearer", "private", "accesskey", "access_key", "access-key"];

    /// <summary>The segments under which every value is sensitive, each matched whole, for the test that pins them.</summary>
    internal static ReadOnlySpan<string> Segments => _segments;

    /// <summary>The words that make a key sensitive wherever they stand in it, for the test that pins them.</summary>
    internal static ReadOnlySpan<string> Substrings => _substrings;

    /// <summary>Whether a value under <paramref name="key"/> may carry a secret, and so must not be written.</summary>
    /// <param name="key">The value's key: a name, or a path whose segments a dot separates, as a JSON path's do.</param>
    /// <returns>
    /// <see langword="true"/> when one of the key's segments is <c>env</c>, <c>headers</c>, <c>credentials</c>,
    /// <c>auth</c> or <c>authorization</c>, or when the key holds <c>token</c>, <c>secret</c>, <c>password</c>,
    /// <c>apikey</c>, <c>api_key</c>, <c>api-key</c>, <c>bearer</c>, <c>private</c>, <c>accesskey</c>,
    /// <c>access_key</c> or <c>access-key</c> anywhere; case is ignored in both.
    /// </returns>
    /// <remarks>
    /// <para>
    /// A segment counts only whole: <c>headers.Size</c> is sensitive and <c>headerSize</c> is not. Everything under
    /// a sensitive segment is, <c>env.PATH</c> as much as <c>env.GITHUB_TOKEN</c>. A key is never sensitive for
    /// merely holding <c>key</c>, so <c>uniqueKey</c> and <c>locKey</c> are not.
    /// </para>
    /// <para>
    /// ⚠ Only a dot separates segments. A configuration key's <c>:</c> and an environment variable's <c>__</c> do
    /// not, so <c>Credentials:User</c> is not sensitive, where <c>credentials.user</c> is.
    /// </para>
    /// </remarks>
    public static bool IsSensitive(string key)
    {
        // A segment counts only whole, so that headerSize is not read as headers.
        foreach (string segment in key.Split('.'))
        {
            foreach (string name in _segments)
            {
                if (segment.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        // A word counts wherever it stands, for a key named for what it holds, wherever the key sits.
        foreach (string word in _substrings)
        {
            if (key.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The value a log line writes for <paramref name="key"/>: <see cref="Redacted"/> when
    /// <see cref="IsSensitive(string)"/> calls the key sensitive, and <paramref name="value"/> as it is otherwise.
    /// </summary>
    /// <param name="key">The value's key, read as <see cref="IsSensitive(string)"/> reads it.</param>
    /// <param name="value">The value, or <see langword="null"/>.</param>
    /// <returns><see cref="Redacted"/>, or <paramref name="value"/>.</returns>
    /// <remarks>
    /// A sensitive key gives <see cref="Redacted"/> whatever its value, an empty one or <see langword="null"/>
    /// included, so a line never says whether a secret is set.
    /// </remarks>
    [return: NotNullIfNotNull(nameof(value))]
    public static string? Redact(string key, string? value) => IsSensitive(key) ? Redacted : value;
}
