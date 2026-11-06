using Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Controls;

namespace Bennewitz.Ninja.ScopedEditors.AvaloniaUI.Tests.Controls;

/// <summary>
/// Pins the sentence-splitter regex contract used by <see cref="LinkifiedTextBlock"/>
/// to wrap multi-sentence property descriptions onto separate paragraphs.
/// Each test represents a class of input the real schema descriptions contain —
/// decimals, version numbers, abbreviations, and URLs must stay intact.
/// </summary>
public sealed class DescriptionFormatterTests
{
    [Fact]
    public void SplitsOnSentenceBoundary()
    {
        string? result = DescriptionFormatter.SplitSentencesOntoLines(
            "First sentence. Second sentence.");
        Assert.Equal("First sentence.\n\nSecond sentence.", result);
    }

    [Fact]
    public void SplitsOnMultipleSentenceBoundaries()
    {
        string? result = DescriptionFormatter.SplitSentencesOntoLines(
            "One. Two. Three.");
        Assert.Equal("One.\n\nTwo.\n\nThree.", result);
    }

    [Fact]
    public void SplitsAfterQuestionMarkAndExclamation()
    {
        string? result = DescriptionFormatter.SplitSentencesOntoLines(
            "Ready? Go! Finish.");
        Assert.Equal("Ready?\n\nGo!\n\nFinish.", result);
    }

    [Fact]
    public void DoesNotSplitOnDecimalNumber()
    {
        // Next char after "0." is "5" (digit), not uppercase — regex leaves it alone.
        string input = "Value 0.5 means 50%.";
        string? result = DescriptionFormatter.SplitSentencesOntoLines(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void DoesNotSplitOnVersionNumber()
    {
        string input = "Requires 1.0.0 or later.";
        string? result = DescriptionFormatter.SplitSentencesOntoLines(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void DoesNotSplitAfterAbbreviation_Eg()
    {
        // "e.g." — the lookbehind "[A-Za-z]\.[A-Za-z]" before the terminal period
        // blocks the split, so "Bash(*)" stays attached.
        string input = "Use e.g. Bash(*).";
        string? result = DescriptionFormatter.SplitSentencesOntoLines(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void DoesNotSplitAfterAbbreviation_Ie()
    {
        string input = "Full paths i.e. Absolute ones.";
        string? result = DescriptionFormatter.SplitSentencesOntoLines(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void DoesNotSplitInsideUrl()
    {
        // No whitespace between "com" and the period, so the split regex never
        // matches inside the URL. The period at the end of the sentence is
        // followed by " for" (lowercase) so that also doesn't match.
        string input = "See https://example.com/path for details.";
        string? result = DescriptionFormatter.SplitSentencesOntoLines(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void SplitsAfterUrl_WhenFollowedByNewSentence()
    {
        // "docs." at the end of a sentence, next word capitalized — this *should*
        // split so the next sentence starts on its own line.
        string input = "See https://example.com/docs. Then read the notes.";
        string? result = DescriptionFormatter.SplitSentencesOntoLines(input);
        Assert.Equal("See https://example.com/docs.\n\nThen read the notes.", result);
    }

    [Fact]
    public void HandlesEmptyString()
    {
        Assert.Equal("", DescriptionFormatter.SplitSentencesOntoLines(""));
    }

    [Fact]
    public void HandlesNull()
    {
        Assert.Null(DescriptionFormatter.SplitSentencesOntoLines(null));
    }

    [Fact]
    public void DoesNotSplit_SingleSentence()
    {
        string input = "Just one sentence here.";
        string? result = DescriptionFormatter.SplitSentencesOntoLines(input);
        Assert.Equal(input, result);
    }
}