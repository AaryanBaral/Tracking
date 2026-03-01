using Agent.Shared.Models;

namespace Agent.Shared.Tracking;

public static class AttentionScoreCalculator
{
    public static int Compute(
        bool pipActive,
        bool browserForeground,
        bool splitScreenAttention,
        bool videoPlaying)
    {
        var score = 100;

        if (pipActive && !browserForeground)
        {
            score -= 30;
        }

        if (splitScreenAttention)
        {
            score -= 15;
        }

        if (videoPlaying && !browserForeground)
        {
            score -= 10;
        }

        return Math.Clamp(score, 0, 100);
    }
}
