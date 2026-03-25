using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Core.Interfaces;

/// <summary>
/// Service for orchestrating the 3-stage LLM Council deliberation process.
/// </summary>
public interface ICouncilService
{
    /// <summary>
    /// Stage 1: Collect individual responses from all council models.
    /// </summary>
    /// <param name="userQuery">The user's question.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of responses from each model.</returns>
    Task<List<Stage1Response>> Stage1CollectResponsesAsync(
        string userQuery,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stage 2: Collect anonymized peer rankings from all council models.
    /// </summary>
    /// <param name="userQuery">The original user query.</param>
    /// <param name="stage1Results">The responses from Stage 1.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of rankings list and label-to-model mapping.</returns>
    Task<(List<Stage2Ranking> Rankings, Dictionary<string, string> LabelToModel)> Stage2CollectRankingsAsync(
        string userQuery,
        List<Stage1Response> stage1Results,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stage 3: Synthesize final answer from chairman model.
    /// </summary>
    /// <param name="userQuery">The original user query.</param>
    /// <param name="stage1Results">The responses from Stage 1.</param>
    /// <param name="stage2Results">The rankings from Stage 2.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The chairman's synthesized response.</returns>
    Task<Stage3Response> Stage3SynthesizeFinalAsync(
        string userQuery,
        List<Stage1Response> stage1Results,
        List<Stage2Ranking> stage2Results,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Run the complete 3-stage council process.
    /// </summary>
    /// <param name="userQuery">The user's question.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete council result with all stages and metadata.</returns>
    Task<CouncilResult> RunFullCouncilAsync(
        string userQuery,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a short title for a conversation based on the first user message.
    /// </summary>
    /// <param name="userQuery">The first user message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A short title (3-5 words).</returns>
    Task<string> GenerateConversationTitleAsync(
        string userQuery,
        CancellationToken cancellationToken = default);
}
