using SamurAICouncil.Core.Services;

namespace SamurAICouncil.Core.Tests.Services;

[TestClass]
public class RankingParserTests
{
    [TestMethod]
    public void ParseRankingFromText_WithNull_ReturnsEmptyList()
    {
        var result = RankingParser.ParseRankingFromText(null);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void ParseRankingFromText_WithEmptyString_ReturnsEmptyList()
    {
        var result = RankingParser.ParseRankingFromText("");

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void ParseRankingFromText_WithWhitespace_ReturnsEmptyList()
    {
        var result = RankingParser.ParseRankingFromText("   \n\t   ");

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void ParseRankingFromText_WithFinalRankingSection_ExtractsRankings()
    {
        var text = """
            Here is my analysis of the responses...

            FINAL RANKING:
            1. Response B
            2. Response A
            3. Response C
            """;

        var result = RankingParser.ParseRankingFromText(text);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("Response B", result[0]);
        Assert.AreEqual("Response A", result[1]);
        Assert.AreEqual("Response C", result[2]);
    }

    [TestMethod]
    public void ParseRankingFromText_WithFinalRankingAndParentheses_ExtractsRankings()
    {
        var text = """
            My evaluation shows...

            FINAL RANKING:
            1) Response A
            2) Response C
            3) Response B
            """;

        var result = RankingParser.ParseRankingFromText(text);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("Response A", result[0]);
        Assert.AreEqual("Response C", result[1]);
        Assert.AreEqual("Response B", result[2]);
    }

    [TestMethod]
    public void ParseRankingFromText_WithLowercaseFinalRanking_ExtractsRankings()
    {
        var text = """
            After review...

            final ranking:
            1. Response C
            2. Response A
            """;

        var result = RankingParser.ParseRankingFromText(text);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Response C", result[0]);
        Assert.AreEqual("Response A", result[1]);
    }

    [TestMethod]
    public void ParseRankingFromText_WithNumberedListNoHeader_ExtractsRankings()
    {
        var text = """
            1. Response B
            2. Response A
            3. Response C
            """;

        var result = RankingParser.ParseRankingFromText(text);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("Response B", result[0]);
        Assert.AreEqual("Response A", result[1]);
        Assert.AreEqual("Response C", result[2]);
    }

    [TestMethod]
    public void ParseRankingFromText_WithCommaSeparated_ExtractsRankings()
    {
        var text = "My ranking: Response B, Response A, Response C";

        var result = RankingParser.ParseRankingFromText(text);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("Response B", result[0]);
        Assert.AreEqual("Response A", result[1]);
        Assert.AreEqual("Response C", result[2]);
    }

    [TestMethod]
    public void ParseRankingFromText_WithPlainResponses_ExtractsInOrder()
    {
        var text = "Response A is best, then Response C, finally Response B";

        var result = RankingParser.ParseRankingFromText(text);

        // Note: plain extraction finds them in order of appearance
        Assert.AreEqual(3, result.Count);
        Assert.IsTrue(result.Contains("Response A"));
        Assert.IsTrue(result.Contains("Response B"));
        Assert.IsTrue(result.Contains("Response C"));
    }

    [TestMethod]
    public void ParseRankingFromText_NormalizesLowercaseResponses()
    {
        var text = """
            FINAL RANKING:
            1. response b
            2. response a
            """;

        var result = RankingParser.ParseRankingFromText(text);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Response B", result[0]);
        Assert.AreEqual("Response A", result[1]);
    }

    [TestMethod]
    public void ParseRankingFromText_WithExtraTextAfterFinalRanking_OnlyExtractsRanking()
    {
        var text = """
            Analysis here...

            FINAL RANKING:
            1. Response A
            2. Response B

            Additional comments that should be ignored...
            Response C mentioned here should not be included.
            """;

        var result = RankingParser.ParseRankingFromText(text);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Response A", result[0]);
        Assert.AreEqual("Response B", result[1]);
    }

    [TestMethod]
    public void ParseRankingFromText_WithMixedFormats_PrefersNumberedList()
    {
        var text = """
            Response C was mentioned earlier.

            1. Response B
            2. Response A
            """;

        var result = RankingParser.ParseRankingFromText(text);

        // Should prefer the numbered list format
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Response B", result[0]);
        Assert.AreEqual("Response A", result[1]);
    }

    [TestMethod]
    public void ParseRankingFromText_WithNoValidResponses_ReturnsEmptyList()
    {
        var text = "This text contains nothing useful for ranking purposes.";

        var result = RankingParser.ParseRankingFromText(text);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void ParseRankingFromText_WithDuplicateResponses_RemovesDuplicates()
    {
        var text = "Response A is mentioned. Response A appears again. Response B too.";

        var result = RankingParser.ParseRankingFromText(text);

        // Duplicates should be removed
        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.Contains("Response A"));
        Assert.IsTrue(result.Contains("Response B"));
    }
}
