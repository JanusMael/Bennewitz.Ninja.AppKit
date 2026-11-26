using Bennewitz.Ninja.AppServices.Abstractions;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AppServices.Tests.Logging;

/// <summary>
/// <see cref="LogRedaction"/> (<c>plans/00009</c>, decision 6): its marker; each entry of its two lists, each way
/// and in any case; the examples of ClaudeForge's <c>SensitiveKeys</c>, whose rule it takes unchanged; and
/// <see cref="LogRedaction.Redact"/> both ways.
/// </summary>
/// <remarks>
/// The two lists are kept here, not read from the class, so that a test of an entry can fail; one test holds the
/// class to them, so that an entry it gains without a test is seen too. The examples are those ClaudeForge's
/// <c>SensitiveKeys</c> names in its comments and its tests hold, at ClaudeForge's <c>35989a47</c>, each with the
/// verdict ClaudeForge gives it: by an assertion in its tests, by the group its parity test lists it in, or, for
/// <c>rsa_private</c>, <c>X-Api-Key</c> and <c>headerSize</c>, which none of its tests holds, in the comment that
/// names it.
/// </remarks>
public sealed class LogRedactionTests
{
    private const string SettingsChange = "[Settings.Change] key={Key} value=\"{Value}\"";

    private static readonly string[] Segments = ["env", "headers", "credentials", "auth", "authorization"];

    private static readonly string[] Substrings =
        ["token", "secret", "password", "apikey", "api_key", "api-key", "bearer", "private", "accesskey", "access_key", "access-key"];

    public static TheoryData<string> SegmentEntries => new(Segments);

    public static TheoryData<string> SubstringEntries => new(Substrings);

    [Fact]
    public void The_marker_is_three_asterisks()
    {
        // Pinned: Templates confirmed it on 2026-11-23, and every app's log writes it.
        Assert.Equal("***", LogRedaction.Redacted);
    }

    [Fact]
    public void The_class_holds_the_two_lists_and_nothing_else()
    {
        Assert.Equal(Segments, LogRedaction.Segments.ToArray());
        Assert.Equal(Substrings, LogRedaction.Substrings.ToArray());
    }

    [Theory]
    [MemberData(nameof(SegmentEntries))]
    public void A_segment_entry_is_sensitive_as_a_whole_segment_wherever_it_sits(string entry)
    {
        Assert.True(LogRedaction.IsSensitive(entry));
        Assert.True(LogRedaction.IsSensitive($"{entry}.value"));
        Assert.True(LogRedaction.IsSensitive($"settings.{entry}"));
        Assert.True(LogRedaction.IsSensitive($"settings.{entry}.value"));
    }

    [Theory]
    [MemberData(nameof(SegmentEntries))]
    public void A_segment_entry_inside_a_longer_segment_is_not_sensitive(string entry)
    {
        Assert.False(LogRedaction.IsSensitive($"{entry}Count"));
        Assert.False(LogRedaction.IsSensitive($"my{entry}"));
        Assert.False(LogRedaction.IsSensitive($"settings.my{entry}Count.value"));
    }

    [Theory]
    [MemberData(nameof(SegmentEntries))]
    public void A_segment_entry_is_matched_ignoring_case(string entry)
    {
        Assert.True(LogRedaction.IsSensitive(entry.ToUpperInvariant()));
        Assert.True(LogRedaction.IsSensitive($"settings.{Capitalized(entry)}.value"));
    }

    [Theory]
    [MemberData(nameof(SubstringEntries))]
    public void A_substring_entry_is_sensitive_anywhere_in_a_key(string entry)
    {
        Assert.True(LogRedaction.IsSensitive(entry));
        Assert.True(LogRedaction.IsSensitive($"my{entry}"));
        Assert.True(LogRedaction.IsSensitive($"{entry}Value"));
        Assert.True(LogRedaction.IsSensitive($"settings.my{entry}Value"));
    }

    [Theory]
    [MemberData(nameof(SubstringEntries))]
    public void A_substring_entry_is_matched_ignoring_case(string entry)
    {
        Assert.True(LogRedaction.IsSensitive(entry.ToUpperInvariant()));
        Assert.True(LogRedaction.IsSensitive($"my{Capitalized(entry)}"));
    }

    [Theory]
    [InlineData("keyboardLayout")]
    [InlineData("apiVersion")]
    [InlineData("api_url")]
    [InlineData("accessLevel")]
    [InlineData("access-log")]
    [InlineData("permissions.disableBypassPermissionsMode")]
    public void A_key_holding_only_a_word_an_entry_is_made_of_is_not_sensitive(string key)
    {
        // key, api, access and pass alone, the last from ClaudeForge's parity test, which gives it no verdict.
        Assert.False(LogRedaction.IsSensitive(key));
    }

    [Fact]
    public void Only_a_dot_separates_segments()
    {
        Assert.True(LogRedaction.IsSensitive("credentials.user"));
        Assert.False(LogRedaction.IsSensitive("Credentials:User"));
        Assert.False(LogRedaction.IsSensitive("Credentials__User"));
    }

    [Theory]
    // Named in its comments.
    [InlineData("env")]
    [InlineData("env.ANTHROPIC_API_KEY")]
    [InlineData("mcpServers.gh.headers.Authorization")]
    [InlineData("credentials.refresh_token")]
    [InlineData("githubAccessToken")]
    [InlineData("clientSecret")]
    [InlineData("password")]
    [InlineData("privateKey")]
    [InlineData("rsa_private")]
    [InlineData("X-Api-Key")]
    // Held sensitive by its tests.
    [InlineData("ENV")]
    [InlineData("Env")]
    [InlineData("headers")]
    [InlineData("HEADERS")]
    [InlineData("credentials")]
    [InlineData("auth")]
    [InlineData("authorization")]
    [InlineData("token")]
    [InlineData("Token")]
    [InlineData("refreshToken")]
    [InlineData("secret")]
    [InlineData("user_password")]
    [InlineData("apiKey")]
    [InlineData("ANTHROPIC_API_KEY")]
    [InlineData("api-key")]
    [InlineData("x-api-key")]
    [InlineData("X-API-Key")]
    [InlineData("bearer")]
    [InlineData("BearerToken")]
    [InlineData("rsaPrivate")]
    [InlineData("rsa_private_key")]
    [InlineData("awsAccessKey")]
    [InlineData("aws_access_key_id")]
    [InlineData("aws-access-key-id")]
    [InlineData("AWS_ACCESS_KEY_ID")]
    [InlineData("env.OPAQUE_TOKEN_FOR_THIRD_PARTY")]
    [InlineData("env.MAX_OUTPUT_TOKENS")]
    [InlineData("env.NOT_A_SECRET")]
    [InlineData("mcpServers.gh.headers.X-API-Key")]
    [InlineData("mcpServers.gh.headers.x-api-key")]
    [InlineData("mcpServers.gh.headers.Cookie")]
    [InlineData("permissions.allow.0.headers.Authorization")]
    [InlineData("credentials.access_token")]
    [InlineData("auth.bearer")]
    [InlineData("settings.authorization")]
    public void Each_key_ClaudeForge_holds_sensitive_is_sensitive(string key)
    {
        Assert.True(LogRedaction.IsSensitive(key));
    }

    [Theory]
    // Its three negatives: "key" alone, twice, and a segment entry inside a longer segment.
    [InlineData("uniqueKey")]
    [InlineData("locKey")]
    [InlineData("headerSize")]
    // Held harmless by its tests.
    [InlineData("")]
    [InlineData("model")]
    [InlineData("theme")]
    [InlineData("verbose")]
    [InlineData("permissions")]
    [InlineData("hooks")]
    [InlineData("mcpServers")]
    [InlineData("availableModels")]
    [InlineData("includeCoworkScheduledTasks")]
    [InlineData("permissions.allow")]
    [InlineData("hooks.PreToolUse")]
    [InlineData("mcpServers.gh.command")]
    [InlineData("mcpServers.gh.args")]
    [InlineData("modelOverrides.opus")]
    [InlineData("settings.theme")]
    public void Each_key_ClaudeForge_holds_harmless_is_not_sensitive(string key)
    {
        Assert.False(LogRedaction.IsSensitive(key));
    }

    [Fact]
    public void Redact_gives_the_marker_for_a_sensitive_key_whatever_its_value()
    {
        Assert.Equal(LogRedaction.Redacted, LogRedaction.Redact("apiKey", "sk-typed-secret-1"));
        Assert.Equal(LogRedaction.Redacted, LogRedaction.Redact("env.PATH", "/usr/bin"));
        Assert.Equal(LogRedaction.Redacted, LogRedaction.Redact("clientSecret", ""));
        Assert.Equal(LogRedaction.Redacted, LogRedaction.Redact("password", null));
    }

    [Fact]
    public void Redact_gives_any_other_key_s_value_as_it_is()
    {
        // Typed string, not string?: without [NotNullIfNotNull] on Redact's return this would not build (CS8600).
        string shown = LogRedaction.Redact("theme", "Dark");

        Assert.Same("Dark", shown);
        Assert.Equal("", LogRedaction.Redact("model", ""));
        Assert.Null(LogRedaction.Redact("uniqueKey", null));
    }

    [Fact]
    public void A_settings_line_holds_the_form_with_the_marker_where_the_secret_was()
    {
        Capture capture = new();
        using (Logger logger = new LoggerConfiguration().WriteTo.Sink(capture).CreateLogger())
        {
            logger.Information(SettingsChange, "env.GITHUB_TOKEN", LogRedaction.Redact("env.GITHUB_TOKEN", "ghp_typed-secret-2"));
            logger.Information(SettingsChange, "theme", LogRedaction.Redact("theme", "Dark"));
        }

        Assert.Equal(2, capture.Events.Count);
        LogLineForm.Is(capture.Events[0], LogEventLevel.Information, SettingsChange, null);
        Assert.Equal("env.GITHUB_TOKEN", LogLineForm.ValueOf(capture.Events[0], "Key"));
        Assert.Equal("***", LogLineForm.ValueOf(capture.Events[0], "Value"));
        LogLineForm.Is(capture.Events[1], LogEventLevel.Information, SettingsChange, null);
        Assert.Equal("Dark", LogLineForm.ValueOf(capture.Events[1], "Value"));
    }

    private static string Capitalized(string entry) => char.ToUpperInvariant(entry[0]) + entry[1..];

    private sealed class Capture : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
