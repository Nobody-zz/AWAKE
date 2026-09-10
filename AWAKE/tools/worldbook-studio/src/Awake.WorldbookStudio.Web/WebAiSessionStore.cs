using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Awake.WorldbookStudio.Core;

namespace Awake.WorldbookStudio.Web;

public sealed record WebAiSession(string SessionId, string CsrfToken, DateTimeOffset ExpiresAt, string? CsrfTokenHash = null);

public sealed record WebAiConsent(
    string TokenHash,
    string SessionId,
    AssistanceRequest Request,
    string BufferId,
    DateTimeOffset ExpiresAt);

public sealed record WebAiConsentTicket(
    string Token,
    string SessionId,
    AssistanceRequest Request,
    string BufferId,
    DateTimeOffset ExpiresAt);
public sealed class WebAiSessionStore
{
    public const string SessionCookieName = "AWAKE_AI_SESSION";
    public const string CsrfHeaderName = "X-AWAKE-CSRF";
    public const string ExactOrigin = "http://127.0.0.1:5077";
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
    private static readonly TimeSpan ConsentLifetime = TimeSpan.FromMinutes(5);
    private readonly Dictionary<string, WebAiSession> _sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WebAiConsent> _consents = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly string _exactOrigin;
    private readonly string? _statePath;
    private sealed record PersistedSession(string SessionId, string CsrfHash, DateTimeOffset ExpiresAt);
    private static readonly JsonSerializerOptions StateJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public WebAiSessionStore(string? exactOrigin = null, WorkspaceService? workspace = null)
    {
        _exactOrigin = string.IsNullOrWhiteSpace(exactOrigin) ? ExactOrigin : exactOrigin.TrimEnd('/');
        if (workspace is not null)
        {
            var stateRoot = workspace.Policy.RequireAllowed(
                Path.Combine(workspace.Root, "authoring", "session-state"),
                "初始化 AI 会话状态");
            Directory.CreateDirectory(stateRoot);
            _statePath = Path.Combine(stateRoot, "sessions.v1.json");
            LoadState();
        }
    }

    public string Origin => _exactOrigin;

    public WebAiSession Bootstrap(HttpContext context)
    {
        RequireExactOrigin(context);
        var session = new WebAiSession(NewToken(32), NewToken(24), DateTimeOffset.UtcNow.Add(SessionLifetime));
        lock (_gate)
        {
            CleanupExpired();
            _sessions[session.SessionId] = session;
            PersistStateUnsafe();
        }
        context.Response.Cookies.Append(SessionCookieName, session.SessionId, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = false,
            IsEssential = true,
            Path = "/"
        });
        return session;
    }

    public string RequireSession(HttpContext context)
    {
        RequireExactOrigin(context);
        return RequireSessionCredentials(context);
    }

    public string RequireReadSession(HttpContext context)
        => RequireSessionCredentials(context);

    private string RequireSessionCredentials(HttpContext context)
    {
        var sessionId = context.Request.Cookies[SessionCookieName];
        var csrf = context.Request.Headers[CsrfHeaderName].ToString();
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(csrf)) throw Forbidden();
        lock (_gate)
        {
            CleanupExpired();
            if (!_sessions.TryGetValue(sessionId, out var session)
                || !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(session.CsrfTokenHash ?? Hashing.Sha256Text(session.CsrfToken)),
                    Encoding.UTF8.GetBytes(Hashing.Sha256Text(csrf))))
                throw Forbidden();
        }
        return sessionId;
    }

    public WebAiConsentTicket IssueConsent(string sessionId, AssistanceRequest request, string bufferId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(bufferId)) throw Forbidden();
        var token = NewToken(32);
        var consent = new WebAiConsent(Hashing.Sha256Text(token), sessionId, request, bufferId, DateTimeOffset.UtcNow.Add(ConsentLifetime));
        lock (_gate)
        {
            CleanupExpired();
            _consents[consent.TokenHash] = consent;
        }
        return new WebAiConsentTicket(token, consent.SessionId, consent.Request, consent.BufferId, consent.ExpiresAt);
    }

    public WebAiConsent ConsumeConsent(string token, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(sessionId)) throw Forbidden();
        var tokenHash = Hashing.Sha256Text(token);
        lock (_gate)
        {
            CleanupExpired();
            if (!_consents.Remove(tokenHash, out var consent)
                || !string.Equals(consent.SessionId, sessionId, StringComparison.Ordinal))
                throw NotFound();
            return consent;
        }
    }

    private void RequireExactOrigin(HttpContext context)
    {
        var origins = context.Request.Headers.Origin;
        if (origins.Count != 1 || !string.Equals(origins[0], _exactOrigin, StringComparison.Ordinal)) throw Forbidden();
    }

    public static InvalidOperationException Forbidden()
        => new("WB-AI-CSRF-403: AI 会话来源或 CSRF 校验失败。");

    public static InvalidOperationException NotFound()
        => new("WB-AI-CONSENT-404: AI 同意会话不存在或已失效。");

    private void CleanupExpired()
    {
        var now = DateTimeOffset.UtcNow;
        var removed = false;
        foreach (var key in _sessions.Where(item => item.Value.ExpiresAt <= now).Select(item => item.Key).ToArray())
        {
            _sessions.Remove(key);
            removed = true;
        }
        foreach (var key in _consents.Where(item => item.Value.ExpiresAt <= now).Select(item => item.Key).ToArray()) _consents.Remove(key);
        if (removed) PersistStateUnsafe();
    }

    private void LoadState()
    {
        if (string.IsNullOrWhiteSpace(_statePath) || !File.Exists(_statePath)) return;
        try
        {
            var persisted = JsonSerializer.Deserialize<List<PersistedSession>>(
                File.ReadAllText(_statePath),
                StateJsonOptions) ?? [];
            foreach (var item in persisted)
            {
                if (string.IsNullOrWhiteSpace(item.SessionId)
                    || string.IsNullOrWhiteSpace(item.CsrfHash)
                    || item.CsrfHash.Length != 64)
                    continue;
                _sessions[item.SessionId] = new WebAiSession(item.SessionId, string.Empty, item.ExpiresAt, item.CsrfHash);
            }
            var now = DateTimeOffset.UtcNow;
            foreach (var key in _sessions.Where(item => item.Value.ExpiresAt <= now).Select(item => item.Key).ToArray())
                _sessions.Remove(key);
        }
        catch (JsonException)
        {
            var quarantine = _statePath + ".corrupt-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            try { File.Move(_statePath, quarantine, false); } catch { }
        }
    }

    private void PersistStateUnsafe()
    {
        if (string.IsNullOrWhiteSpace(_statePath)) return;
        var persisted = _sessions.Values.Select(session =>
            new PersistedSession(session.SessionId, session.CsrfTokenHash ?? Hashing.Sha256Text(session.CsrfToken), session.ExpiresAt)).ToList();
        var temporary = _statePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(persisted, StateJsonOptions), new UTF8Encoding(false));
        File.Move(temporary, _statePath, true);
    }

    private static string NewToken(int bytes)
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
