using System.Text.RegularExpressions;

namespace SamurAICouncil.Core.Services;

/// <summary>
/// Parses ranking information from LLM response text.
/// </summary>
public static partial class RankingParser
{
    /// <summary>
    /// Extracts the ranking list from the model's response text.
    /// Looks for "FINAL RANKING:" section, then falls back to parsing common formats.
    /// </summary>
    /// <param name="text">The model's full response text.</param>
    /// <returns>Ordered list of responses (e.g., ["Response A", "Response B", "Response C"]).</returns>
    public static List<string> ParseRankingFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        // Try to find "FINAL RANKING:" section first
        var finalRankingMatch = FinalRankingRegex().Match(text);
        if (finalRankingMatch.Success)
        {
            var rankingSection = finalRankingMatch.Groups[1].Value;
            var rankings = ExtractResponsesFromSection(rankingSection);
            if (rankings.Count > 0)
            {
                return rankings;
            }
        }

        // Fallback: Look for numbered list anywhere in text
        var numberedListRankings = ExtractFromNumberedList(text);
        if (numberedListRankings.Count > 0)
        {
            return numberedListRankings;
        }

        // Fallback: Look for comma-separated responses
        var commaSeparated = ExtractFromCommaSeparated(text);
        if (commaSeparated.Count > 0)
        {
            return commaSeparated;
        }

        // Fallback: Look for any "Response X" patterns
        return ExtractAnyResponses(text);
    }

    /// <summary>
    /// Extracts response references from a text section.
    /// </summary>
    private static List<string> ExtractResponsesFromSection(string section)
    {
        var responses = new List<string>();

        // Try numbered list format: "1. Response A" or "1) Response A"
        var numberedMatches = NumberedResponseRegex().Matches(section);
        if (numberedMatches.Count > 0)
        {
            foreach (Match match in numberedMatches)
            {
                responses.Add(NormalizeResponse(match.Groups[1].Value));
            }
            return responses;
        }

        // Try plain Response X format
        var plainMatches = PlainResponseRegex().Matches(section);
        foreach (Match match in plainMatches)
        {
            var response = NormalizeResponse(match.Groups[0].Value);
            if (!responses.Contains(response))
            {
                responses.Add(response);
            }
        }

        return responses;
    }

    /// <summary>
    /// Extracts rankings from a numbered list format.
    /// </summary>
    private static List<string> ExtractFromNumberedList(string text)
    {
        var responses = new List<string>();
        var matches = NumberedResponseRegex().Matches(text);

        foreach (Match match in matches)
        {
            responses.Add(NormalizeResponse(match.Groups[1].Value));
        }

        return responses;
    }

    /// <summary>
    /// Extracts rankings from comma-separated format like "Response B, Response A, Response C".
    /// </summary>
    private static List<string> ExtractFromCommaSeparated(string text)
    {
        var match = CommaSeparatedResponsesRegex().Match(text);
        if (!match.Success)
        {
            return [];
        }

        var responsesList = match.Value;
        var responses = new List<string>();
        var individualMatches = PlainResponseRegex().Matches(responsesList);

        foreach (Match m in individualMatches)
        {
            responses.Add(NormalizeResponse(m.Groups[0].Value));
        }

        return responses;
    }

    /// <summary>
    /// Extracts any Response X patterns from text.
    /// </summary>
    private static List<string> ExtractAnyResponses(string text)
    {
        var responses = new List<string>();
        var matches = PlainResponseRegex().Matches(text);

        foreach (Match match in matches)
        {
            var response = NormalizeResponse(match.Groups[0].Value);
            if (!responses.Contains(response))
            {
                responses.Add(response);
            }
        }

        return responses;
    }

    /// <summary>
    /// Normalizes a response reference to standard format "Response X".
    /// </summary>
    private static string NormalizeResponse(string response)
    {
        // Trim and normalize whitespace
        response = response.Trim();

        // Extract the letter/identifier
        var letterMatch = ResponseLetterRegex().Match(response);
        if (letterMatch.Success)
        {
            var letter = letterMatch.Groups[1].Value.ToUpperInvariant();
            return $"Response {letter}";
        }

        return response;
    }

    // Regex patterns using GeneratedRegex for performance

    [GeneratedRegex(@"FINAL\s*RANKING\s*:\s*(.*?)(?=\n\n|\z)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex FinalRankingRegex();

    [GeneratedRegex(@"(?:\d+[.\)]\s*)(Response\s+[A-Z])", RegexOptions.IgnoreCase)]
    private static partial Regex NumberedResponseRegex();

    [GeneratedRegex(@"Response\s+[A-Z]", RegexOptions.IgnoreCase)]
    private static partial Regex PlainResponseRegex();

    [GeneratedRegex(@"Response\s+[A-Z](?:\s*,\s*Response\s+[A-Z])+", RegexOptions.IgnoreCase)]
    private static partial Regex CommaSeparatedResponsesRegex();

    [GeneratedRegex(@"Response\s+([A-Z])", RegexOptions.IgnoreCase)]
    private static partial Regex ResponseLetterRegex();
}
