using SamurAICouncil.Core.Models;
using SamurAICouncil.Core.Services;

namespace SamurAICouncil.Core.Tests.Services;

[TestClass]
public class AnonymizationHelperTests
{
    #region GenerateLabel Tests

    [TestMethod]
    public void GenerateLabel_Index0_ReturnsResponseA()
    {
        var result = AnonymizationHelper.GenerateLabel(0);

        Assert.AreEqual("Response A", result);
    }

    [TestMethod]
    public void GenerateLabel_Index1_ReturnsResponseB()
    {
        var result = AnonymizationHelper.GenerateLabel(1);

        Assert.AreEqual("Response B", result);
    }

    [TestMethod]
    public void GenerateLabel_Index25_ReturnsResponseZ()
    {
        var result = AnonymizationHelper.GenerateLabel(25);

        Assert.AreEqual("Response Z", result);
    }

    [TestMethod]
    public void GenerateLabel_Index26_ReturnsResponseAA()
    {
        var result = AnonymizationHelper.GenerateLabel(26);

        Assert.AreEqual("Response AA", result);
    }

    [TestMethod]
    public void GenerateLabel_NegativeIndex_ThrowsException()
    {
        var exception = false;
        try
        {
            AnonymizationHelper.GenerateLabel(-1);
        }
        catch (ArgumentOutOfRangeException)
        {
            exception = true;
        }
        Assert.IsTrue(exception, "Expected ArgumentOutOfRangeException");
    }

    #endregion

    #region AnonymizeResponses Tests

    [TestMethod]
    public void AnonymizeResponses_WithEmptyList_ReturnsEmptyResults()
    {
        var stage1Results = new List<Stage1Response>();

        var (responses, labelToModel) = AnonymizationHelper.AnonymizeResponses(stage1Results);

        Assert.AreEqual(0, responses.Count);
        Assert.AreEqual(0, labelToModel.Count);
    }

    [TestMethod]
    public void AnonymizeResponses_WithSingleResponse_CreatesCorrectMapping()
    {
        var stage1Results = new List<Stage1Response>
        {
            new() { Model = "openai/gpt-4", Response = "Test response" }
        };

        var (responses, labelToModel) = AnonymizationHelper.AnonymizeResponses(stage1Results, randomize: false);

        Assert.AreEqual(1, responses.Count);
        Assert.AreEqual("Response A", responses[0].Label);
        Assert.AreEqual("Test response", responses[0].Content);
        Assert.AreEqual("openai/gpt-4", labelToModel["Response A"]);
    }

    [TestMethod]
    public void AnonymizeResponses_WithMultipleResponses_CreatesAllMappings()
    {
        var stage1Results = new List<Stage1Response>
        {
            new() { Model = "model-1", Response = "Response 1" },
            new() { Model = "model-2", Response = "Response 2" },
            new() { Model = "model-3", Response = "Response 3" }
        };

        var (responses, labelToModel) = AnonymizationHelper.AnonymizeResponses(stage1Results, randomize: false);

        Assert.AreEqual(3, responses.Count);
        Assert.AreEqual(3, labelToModel.Count);

        // Verify all labels are present
        Assert.IsTrue(labelToModel.ContainsKey("Response A"));
        Assert.IsTrue(labelToModel.ContainsKey("Response B"));
        Assert.IsTrue(labelToModel.ContainsKey("Response C"));
    }

    [TestMethod]
    public void AnonymizeResponses_WithRandomization_ShufflesOrder()
    {
        var stage1Results = new List<Stage1Response>
        {
            new() { Model = "model-1", Response = "Response 1" },
            new() { Model = "model-2", Response = "Response 2" },
            new() { Model = "model-3", Response = "Response 3" },
            new() { Model = "model-4", Response = "Response 4" },
            new() { Model = "model-5", Response = "Response 5" }
        };

        // Run multiple times to check for randomization
        var orderings = new HashSet<string>();
        for (var i = 0; i < 20; i++)
        {
            var (responses, _) = AnonymizationHelper.AnonymizeResponses(stage1Results, randomize: true);
            var ordering = string.Join(",", responses.Select(r => r.Content));
            orderings.Add(ordering);
        }

        // With 5 items and 20 runs, we should see multiple different orderings
        Assert.IsTrue(orderings.Count > 1, "Expected randomization to produce different orderings");
    }

    [TestMethod]
    public void AnonymizeResponses_WithoutRandomization_PreservesOrder()
    {
        var stage1Results = new List<Stage1Response>
        {
            new() { Model = "model-1", Response = "First" },
            new() { Model = "model-2", Response = "Second" },
            new() { Model = "model-3", Response = "Third" }
        };

        var (responses, _) = AnonymizationHelper.AnonymizeResponses(stage1Results, randomize: false);

        Assert.AreEqual("First", responses[0].Content);
        Assert.AreEqual("Second", responses[1].Content);
        Assert.AreEqual("Third", responses[2].Content);
    }

    #endregion

    #region CreateStage2Prompt Tests

    [TestMethod]
    public void CreateStage2Prompt_ContainsUserQuery()
    {
        var userQuery = "What is the capital of France?";
        var responses = new List<AnonymizedResponse>
        {
            new() { Label = "Response A", Content = "Paris" }
        };

        var prompt = AnonymizationHelper.CreateStage2Prompt(userQuery, responses);

        Assert.IsTrue(prompt.Contains(userQuery));
    }

    [TestMethod]
    public void CreateStage2Prompt_ContainsAllResponses()
    {
        var userQuery = "Test question";
        var responses = new List<AnonymizedResponse>
        {
            new() { Label = "Response A", Content = "Content A" },
            new() { Label = "Response B", Content = "Content B" }
        };

        var prompt = AnonymizationHelper.CreateStage2Prompt(userQuery, responses);

        Assert.IsTrue(prompt.Contains("Response A"));
        Assert.IsTrue(prompt.Contains("Content A"));
        Assert.IsTrue(prompt.Contains("Response B"));
        Assert.IsTrue(prompt.Contains("Content B"));
    }

    [TestMethod]
    public void CreateStage2Prompt_ContainsFinalRankingInstruction()
    {
        var userQuery = "Test";
        var responses = new List<AnonymizedResponse>
        {
            new() { Label = "Response A", Content = "Test" }
        };

        var prompt = AnonymizationHelper.CreateStage2Prompt(userQuery, responses);

        Assert.IsTrue(prompt.Contains("FINAL RANKING:"));
    }

    [TestMethod]
    public void CreateStage2Prompt_IncludesResponseCount()
    {
        var userQuery = "Test";
        var responses = new List<AnonymizedResponse>
        {
            new() { Label = "Response A", Content = "A" },
            new() { Label = "Response B", Content = "B" },
            new() { Label = "Response C", Content = "C" }
        };

        var prompt = AnonymizationHelper.CreateStage2Prompt(userQuery, responses);

        Assert.IsTrue(prompt.Contains("3 responses"));
    }

    #endregion

    #region DeAnonymize Tests

    [TestMethod]
    public void DeAnonymize_WithEmptyText_ReturnsEmpty()
    {
        var labelToModel = new Dictionary<string, string>
        {
            ["Response A"] = "gpt-4"
        };

        var result = AnonymizationHelper.DeAnonymize("", labelToModel);

        Assert.AreEqual("", result);
    }

    [TestMethod]
    public void DeAnonymize_WithEmptyMapping_ReturnsOriginalText()
    {
        var text = "Response A is the best";
        var labelToModel = new Dictionary<string, string>();

        var result = AnonymizationHelper.DeAnonymize(text, labelToModel);

        Assert.AreEqual(text, result);
    }

    [TestMethod]
    public void DeAnonymize_ReplacesLabelsWithModelNames()
    {
        var text = "Response A is better than Response B";
        var labelToModel = new Dictionary<string, string>
        {
            ["Response A"] = "gpt-4",
            ["Response B"] = "claude-3"
        };

        var result = AnonymizationHelper.DeAnonymize(text, labelToModel);

        Assert.IsTrue(result.Contains("Response A (**gpt-4**)"));
        Assert.IsTrue(result.Contains("Response B (**claude-3**)"));
    }

    [TestMethod]
    public void DeAnonymize_HandlesMultipleOccurrences()
    {
        var text = "Response A wins. Response A is great!";
        var labelToModel = new Dictionary<string, string>
        {
            ["Response A"] = "gpt-4"
        };

        var result = AnonymizationHelper.DeAnonymize(text, labelToModel);

        // Both occurrences should be replaced
        var count = result.Split("(**gpt-4**)").Length - 1;
        Assert.AreEqual(2, count);
    }

    #endregion

    #region GetShortModelName Tests

    [TestMethod]
    public void GetShortModelName_WithSlash_ReturnsPartAfterSlash()
    {
        var result = AnonymizationHelper.GetShortModelName("openai/gpt-4");

        Assert.AreEqual("gpt-4", result);
    }

    [TestMethod]
    public void GetShortModelName_WithMultipleSlashes_ReturnsPartAfterLastSlash()
    {
        var result = AnonymizationHelper.GetShortModelName("provider/category/model-name");

        Assert.AreEqual("model-name", result);
    }

    [TestMethod]
    public void GetShortModelName_WithNoSlash_ReturnsFullName()
    {
        var result = AnonymizationHelper.GetShortModelName("gpt-4");

        Assert.AreEqual("gpt-4", result);
    }

    [TestMethod]
    public void GetShortModelName_WithEmptyString_ReturnsEmpty()
    {
        var result = AnonymizationHelper.GetShortModelName("");

        Assert.AreEqual("", result);
    }

    [TestMethod]
    public void GetShortModelName_WithNull_ReturnsNull()
    {
        var result = AnonymizationHelper.GetShortModelName(null!);

        Assert.IsNull(result);
    }

    #endregion
}
