using System;

namespace Awake;

internal static class AwakeDialogueSessionCoordinator
{
    private static readonly object Gate = new object();
    private static string _activeSource = string.Empty;
    private static string _activeTargetId = string.Empty;
    private static bool _active;

    internal static bool IsActive
    {
        get { lock (Gate) return _active; }
    }

    internal static string ActiveSource
    {
        get { lock (Gate) return _activeSource; }
    }

    internal static string ActiveTargetId
    {
        get { lock (Gate) return _activeTargetId; }
    }

    internal static bool TryAcquire(string source, string targetId)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;
        lock (Gate)
        {
            if (_active) return false;
            _active = true;
            _activeSource = source;
            _activeTargetId = targetId ?? string.Empty;
            return true;
        }
    }

    internal static void Close(string source, string targetId)
    {
        lock (Gate)
        {
            if (!_active) return;
            bool sourceMatches = string.IsNullOrWhiteSpace(source)
                || StringComparer.Ordinal.Equals(source, _activeSource);
            bool targetMatches = string.IsNullOrWhiteSpace(targetId)
                || StringComparer.Ordinal.Equals(targetId, _activeTargetId);
            if (sourceMatches && targetMatches)
            {
                _active = false;
                _activeSource = string.Empty;
                _activeTargetId = string.Empty;
            }
        }
    }

    internal static void CloseAll()
    {
        lock (Gate)
        {
            _active = false;
            _activeSource = string.Empty;
            _activeTargetId = string.Empty;
        }
    }

    internal static void ResetForTesting()
    {
        CloseAll();
    }
}
