using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Core.Services;

/// <summary>
/// Handles anonymization and de-anonymization of model responses for Stage 2 peer review.
/// </summary>
public static class AnonymizationHelper
{
    private const string ResponsePrefix = "Response";

    /// <summary>
    /// Generate an anonymous label for a given index (0 = A, 1 = B, etc.).
    /// </summary>
    /// <param name="index">Zero-based index.</param>
    /// <returns>Label like "Response A", "Response B", etc.</returns>
    public static string GenerateLabel(int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index must be non-negative");
        }

        // Support up to 26 responses (A-Z)
        if (index >= 26)
        {
            // For more than 26, use AA, AB, etc.
            var firstLetter = (char)('A' + (index / 26) - 1);
            var secondLetter = (char)('A' + (index % 26));
            return $"{ResponsePrefix} {firstLetter}{secondLetter}";
        }

        var letter = (char)('A' + index);
        return $"{ResponsePrefix} {letter}";
    }

    /// <summary>
    /// Creates anonymized mapping from Stage 1 responses.
    /// Randomizes the order to prevent bias.
    /// </summary>
    /// <param name="stage1Results">The Stage 1 responses to anonymize.</param>
    /// <param name="randomize">Whether to randomize the order (default: true).</param>
    /// <returns>Tuple of (anonymized responses, label-to-model mapping).</returns>
    public static (List<AnonymizedResponse> Responses, Dictionary<string, string> LabelToModel)
        AnonymizeResponses(List<Stage1Response> stage1Results, bool randomize = true)
    {
        if (stage1Results.Count == 0)
        {
            return ([], new Dictionary<string, string>());
        }

        // Optionally randomize order to prevent position bias
        var orderedResults = randomize
            ? stage1Results.OrderBy(_ => Random.Shared.Next()).ToList()
            : stage1Results;

        var anonymizedResponses = new List<AnonymizedResponse>();
        var labelToModel = new Dictionary<string, string>();

        for (var i = 0; i < orderedResults.Count; i++)
        {
            var label = GenerateLabel(i);
            var response = orderedResults[i];

            anonymizedResponses.Add(new AnonymizedResponse
            {
                Label = label,
                Content = response.Response
            });

            labelToModel[label] = response.Model;
        }

        return (anonymizedResponses, labelToModel);
    }

    /// <summary>
    /// Creates the prompt text for Stage 2 with anonymized responses.
    /// </summary>
    /// <param name="userQuery">The original user question.</param>
    /// <param name="anonymizedResponses">The anonymized responses.</param>
    /// <returns>Formatted prompt for Stage 2 reviewers.</returns>
    public static string CreateStage2Prompt(string userQuery, List<AnonymizedResponse> anonymizedResponses)
    {
        var responsesText = string.Join("\n\n", anonymizedResponses.Select(r =>
            $"--- {r.Label} ---\n{r.Content}"));

        return $"""
            You are evaluating responses to the following question:

            **Question:** {userQuery}

            Below are {anonymizedResponses.Count} responses from different AI assistants. Your task is to rank them from best to worst based on:
            1. Accuracy and correctness
            2. Completeness and thoroughness
            3. Clarity and helpfulness
            4. Relevance to the question

            {responsesText}

            Please provide your ranking. You MUST end your response with a "FINAL RANKING:" section that lists the responses in order from best to worst.

            Example format:
            FINAL RANKING:
            1. Response B
            2. Response A
            3. Response C
            """;
    }

    /// <summary>
    /// De-anonymizes text by replacing anonymous labels with model names.
    /// </summary>
    /// <param name="text">Text containing anonymous labels.</param>
    /// <param name="labelToModel">Mapping from labels to model names.</param>
    /// <returns>Text with labels replaced by model names.</returns>
    public static string DeAnonymize(string text, Dictionary<string, string> labelToModel)
    {
        if (string.IsNullOrEmpty(text) || labelToModel.Count == 0)
        {
            return text;
        }

        var result = text;

        foreach (var (label, model) in labelToModel)
        {
            // Replace "Response A" with "Response A (model-name)"
            result = result.Replace(label, $"{label} (**{model}**)");
        }

        return result;
    }

    /// <summary>
    /// Gets the short display name from a full model ID.
    /// </summary>
    /// <param name="fullModelId">Full model ID like "openai/gpt-4".</param>
    /// <returns>Short name like "gpt-4".</returns>
    public static string GetShortModelName(string fullModelId)
    {
        if (string.IsNullOrEmpty(fullModelId))
        {
            return fullModelId;
        }

        var slashIndex = fullModelId.LastIndexOf('/');
        return slashIndex >= 0 ? fullModelId[(slashIndex + 1)..] : fullModelId;
    }
}

/// <summary>
/// Represents an anonymized response for Stage 2 review.
/// </summary>
public class AnonymizedResponse
{
    /// <summary>
    /// Anonymous label (e.g., "Response A").
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// The response content.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
