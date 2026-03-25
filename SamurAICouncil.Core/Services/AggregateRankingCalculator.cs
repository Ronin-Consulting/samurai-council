using SamurAICouncil.Core.Models;

namespace SamurAICouncil.Core.Services;

/// <summary>
/// Calculates aggregate rankings from Stage 2 peer reviews.
/// </summary>
public static class AggregateRankingCalculator
{
    /// <summary>
    /// Calculate aggregate rankings from Stage 2 results.
    /// Lower average rank is better (1st place = 1, 2nd place = 2, etc.).
    /// </summary>
    /// <param name="stage2Results">The Stage 2 rankings from all reviewers.</param>
    /// <param name="labelToModel">Mapping from anonymous labels (Response A) to model IDs.</param>
    /// <returns>Aggregate rankings sorted by average rank (ascending - best first).</returns>
    public static List<AggregateRanking> Calculate(
        List<Stage2Ranking> stage2Results,
        Dictionary<string, string> labelToModel)
    {
        if (stage2Results.Count == 0 || labelToModel.Count == 0)
        {
            return [];
        }

        // Dictionary to track rankings per model: model -> list of ranks
        var modelRanks = new Dictionary<string, List<int>>();

        // Initialize with all models
        foreach (var model in labelToModel.Values)
        {
            modelRanks[model] = [];
        }

        // Process each reviewer's rankings
        foreach (var ranking in stage2Results)
        {
            var parsedRanking = ranking.ParsedRanking;
            if (parsedRanking.Count == 0)
            {
                continue;
            }

            // Assign rank positions (1-based: 1st, 2nd, 3rd, etc.)
            for (var position = 0; position < parsedRanking.Count; position++)
            {
                var label = parsedRanking[position];
                var rank = position + 1; // 1-based ranking

                if (labelToModel.TryGetValue(label, out var model))
                {
                    modelRanks[model].Add(rank);
                }
            }
        }

        // Calculate average ranks
        var aggregateRankings = new List<AggregateRanking>();

        foreach (var (model, ranks) in modelRanks)
        {
            if (ranks.Count == 0)
            {
                continue;
            }

            var averageRank = ranks.Average();
            aggregateRankings.Add(new AggregateRanking
            {
                Model = model,
                AverageRank = Math.Round(averageRank, 2),
                RankingsCount = ranks.Count
            });
        }

        // Sort by average rank (ascending - lower is better)
        return aggregateRankings
            .OrderBy(r => r.AverageRank)
            .ThenBy(r => r.Model) // Secondary sort by model name for consistency
            .ToList();
    }

    /// <summary>
    /// Calculate aggregate rankings with weighted scoring.
    /// This uses a points system where 1st place = N points, 2nd = N-1, etc.
    /// </summary>
    /// <param name="stage2Results">The Stage 2 rankings from all reviewers.</param>
    /// <param name="labelToModel">Mapping from anonymous labels to model IDs.</param>
    /// <returns>Aggregate rankings sorted by total score (descending - highest first).</returns>
    public static List<AggregateRanking> CalculateWithPoints(
        List<Stage2Ranking> stage2Results,
        Dictionary<string, string> labelToModel)
    {
        if (stage2Results.Count == 0 || labelToModel.Count == 0)
        {
            return [];
        }

        var modelPoints = new Dictionary<string, (int totalPoints, int count)>();

        // Initialize with all models
        foreach (var model in labelToModel.Values)
        {
            modelPoints[model] = (0, 0);
        }

        // Process each reviewer's rankings
        foreach (var ranking in stage2Results)
        {
            var parsedRanking = ranking.ParsedRanking;
            if (parsedRanking.Count == 0)
            {
                continue;
            }

            var maxPoints = parsedRanking.Count;

            // Assign points (1st place = maxPoints, 2nd = maxPoints-1, etc.)
            for (var position = 0; position < parsedRanking.Count; position++)
            {
                var label = parsedRanking[position];
                var points = maxPoints - position;

                if (labelToModel.TryGetValue(label, out var model))
                {
                    var (currentPoints, currentCount) = modelPoints[model];
                    modelPoints[model] = (currentPoints + points, currentCount + 1);
                }
            }
        }

        // Create aggregate rankings
        var aggregateRankings = new List<AggregateRanking>();

        foreach (var (model, (totalPoints, count)) in modelPoints)
        {
            if (count == 0)
            {
                continue;
            }

            // AverageRank here represents inverse score (for display compatibility)
            // Higher total points = lower average rank value
            var maxPossibleAvg = labelToModel.Count; // e.g., 3 models = max avg of 3
            var avgPointsPerRanking = count > 0 ? (double)totalPoints / count : 0;
            var normalizedRank = maxPossibleAvg - avgPointsPerRanking + 1;

            aggregateRankings.Add(new AggregateRanking
            {
                Model = model,
                AverageRank = Math.Round(normalizedRank, 2),
                RankingsCount = count
            });
        }

        // Sort by average rank (ascending - lower is better, which means higher points)
        return aggregateRankings
            .OrderBy(r => r.AverageRank)
            .ThenBy(r => r.Model)
            .ToList();
    }
}
