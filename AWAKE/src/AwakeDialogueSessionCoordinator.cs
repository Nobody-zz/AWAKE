using System;

namespace Awake;

internal sealed class AwakeDialogueStartPayload
{
    internal string TargetId { get; set; } = string.Empty;
    internal string Source { get; set; } = string.Empty;
    internal string OpeningHint { get; set; } = string.Empty;
    internal string EventId { get; set; } = string.Empty;
    internal string Motive { get; set; } = string.Empty;
}

internal sealed class AwakeDialogueSessionState
{
    internal string SessionId { get; set; } = string.Empty;
    internal string TargetId { get; set; } = string.Empty;
    internal string EntrySource { get; set; } = string.Empty;
    internal string Token { get; set; } = string.Empty;
    internal int Generation { get; set; }
    internal int StartedDay { get; set; }
    internal string OpeningHint { get; set; } = string.Empty;
    internal string EventId { get; set; } = string.Empty;
    internal string Motive { get; set; } = string.Empty;
}

internal static class AwakeDialogueSessionCoordinator
{
    internal static Func<bool> IsOverlayOpen;
    private static readonly object Gate = new object();
    private static AwakeDialogueSessionState _session;
    private static int _generation;

    internal static bool IsActive
    {
        get { lock (Gate) return _session != null; }
    }

    internal static string ActiveSource
    {
        get { lock (Gate) return _session?.EntrySource ?? string.Empty; }
    }

    internal static string ActiveTargetId
    {
        get { lock (Gate) return _session?.TargetId ?? string.Empty; }
    }

    internal static string ActiveToken
    {
        get { lock (Gate) return _session?.Token ?? string.Empty; }
    }

    internal static string ActiveSessionId
    {
        get { lock (Gate) return _session?.SessionId ?? string.Empty; }
    }

    internal static AwakeDialogueSessionState Active
    {
        get
        {
            lock (Gate)
            {
                return _session == null ? null : Clone(_session);
            }
        }
    }

    internal static AwakeDialogueSessionState TryStart(AwakeDialogueStartPayload payload)
    {
        if (payload == null || string.IsNullOrWhiteSpace(payload.TargetId))
        {
            return null;
        }
        if (IsOverlayOpen?.Invoke() ?? false)
        {
            return null;
        }
        lock (Gate)
        {
            if (_session != null) return null;
            _generation++;
            AwakeDialogueSessionState state = new AwakeDialogueSessionState
            {
                SessionId = Guid.NewGuid().ToString("N"),
                TargetId = payload.TargetId,
                EntrySource = string.IsNullOrWhiteSpace(payload.Source) ? "unknown" : payload.Source,
                Token = Guid.NewGuid().ToString("N"),
                Generation = _generation,
                StartedDay = AwakeRuntime.CurrentGameDay(),
                OpeningHint = payload.OpeningHint ?? string.Empty,
                EventId = payload.EventId ?? string.Empty,
                Motive = payload.Motive ?? string.Empty
            };
            _session = state;
            return Clone(state);
        }
    }

    internal static bool TryAcquire(string source, string targetId)
    {
        return TryStart(new AwakeDialogueStartPayload
        {
            Source = source,
            TargetId = targetId
        }) != null;
    }

    internal static bool CloseByToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        lock (Gate)
        {
            if (_session == null || !StringComparer.Ordinal.Equals(_session.Token, token)) return false;
            _session = null;
            return true;
        }
    }

    internal static void Close(string source, string targetId)
    {
        lock (Gate)
        {
            if (_session == null) return;
            bool sourceMatches = string.IsNullOrWhiteSpace(source)
                || StringComparer.Ordinal.Equals(source, _session.EntrySource);
            bool targetMatches = string.IsNullOrWhiteSpace(targetId)
                || StringComparer.Ordinal.Equals(targetId, _session.TargetId);
            if (sourceMatches && targetMatches)
            {
                _session = null;
            }
        }
    }

    internal static void CloseAll()
    {
        lock (Gate)
        {
            _session = null;
        }
    }

    internal static void ResetForTesting()
    {
        lock (Gate)
        {
            _session = null;
            _generation = 0;
        }
    }

    private static AwakeDialogueSessionState Clone(AwakeDialogueSessionState state)
    {
        return new AwakeDialogueSessionState
        {
            SessionId = state.SessionId,
            TargetId = state.TargetId,
            EntrySource = state.EntrySource,
            Token = state.Token,
            Generation = state.Generation,
            StartedDay = state.StartedDay,
            OpeningHint = state.OpeningHint,
            EventId = state.EventId,
            Motive = state.Motive
        };
    }
}
