using System;

namespace Awake;

internal static class SceneDialogueSelection
{
    internal const float MinRangeMeters = 4f;
    internal const float DefaultMaxRangeMeters = 60f;
    internal const float HardMaxRangeMeters = 150f;
    internal const float MaxHoldSeconds = 4f;

    internal static float CurrentRange(float holdSeconds, float maxRangeMeters)
    {
        float targetMax = ClampMax(maxRangeMeters);
        float progress = holdSeconds <= 0f
            ? 0f
            : Math.Min(1f, holdSeconds / MaxHoldSeconds);
        float eased = progress * progress;
        return MinRangeMeters + (targetMax - MinRangeMeters) * eased;
    }

    internal static float ClampMax(float maxRangeMeters)
    {
        if (float.IsNaN(maxRangeMeters) || float.IsInfinity(maxRangeMeters))
        {
            return DefaultMaxRangeMeters;
        }
        if (maxRangeMeters < MinRangeMeters)
        {
            return MinRangeMeters;
        }
        if (maxRangeMeters > HardMaxRangeMeters)
        {
            return HardMaxRangeMeters;
        }
        return maxRangeMeters;
    }
}
