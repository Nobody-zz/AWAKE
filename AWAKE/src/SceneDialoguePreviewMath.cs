using System;
using System.Collections.Generic;
using TaleWorlds.Library;

namespace Awake;

internal static class SceneDialoguePreviewMath
{
    internal const float MinHalfAngleDegrees = 45f;
    internal const float MaxHalfAngleDegrees = 150f;
    internal const float MaxHoldSeconds = 4f;
    internal const int MaximumFanSegments = 180;

    internal static float CurrentHalfAngle(float holdSeconds)
    {
        float progress = holdSeconds <= 0f
            ? 0f
            : Math.Min(1f, holdSeconds / MaxHoldSeconds);
        float eased = progress * progress;
        return MinHalfAngleDegrees + (MaxHalfAngleDegrees - MinHalfAngleDegrees) * eased;
    }

    internal static List<Vec3> BuildFanSegments(
        Vec3 origin,
        Vec3 forward,
        float rangeMeters,
        float halfAngleDegrees,
        int maximumSegments)
    {
        List<Vec3> result = new List<Vec3>();
        if (rangeMeters <= 0f)
        {
            return result;
        }
        int segments = Math.Max(1, Math.Min(maximumSegments, MaximumFanSegments));
        float forwardLength = forward.Length;
        Vec3 direction = forwardLength > 0.001f
            ? forward / forwardLength
            : new Vec3(1f, 0f, 0f);
        Vec3 flat = new Vec3(direction.X, direction.Y, 0f);
        if (flat.Length > 0.001f)
        {
            direction = flat / flat.Length;
        }
        float angleStep = (halfAngleDegrees * 2f) / segments;
        float startAngle = -halfAngleDegrees;
        for (int i = 0; i <= segments; i++)
        {
            float angle = startAngle + angleStep * i;
            float radians = angle * (float)Math.PI / 180f;
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            Vec3 rotated = new Vec3(
                direction.X * cos - direction.Y * sin,
                direction.X * sin + direction.Y * cos,
                direction.Z);
            Vec3 point = origin + rotated * rangeMeters;
            point = new Vec3(point.X, point.Y, origin.Z);
            result.Add(point);
        }
        return result;
    }

    internal static bool IsWithinCone(Vec3 origin, Vec3 forward, Vec3 target, float halfAngleDegrees)
    {
        float forwardLength = forward.Length;
        if (forwardLength <= 0.001f) return false;
        Vec3 toTarget = target - origin;
        float targetLength = toTarget.Length;
        if (targetLength <= 0.001f) return true;
        float dot = Vec3.DotProduct(forward, toTarget);
        float cosAngle = dot / (forwardLength * targetLength);
        cosAngle = Math.Max(-1f, Math.Min(1f, cosAngle));
        float angleDegrees = (float)Math.Acos(cosAngle) * 180f / (float)Math.PI;
        return angleDegrees <= halfAngleDegrees;
    }
}
