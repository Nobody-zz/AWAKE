using System.Security.Cryptography;
using System.Text;

namespace PersonaWorkbench.Web;

public sealed class WorkbenchSessionGrant
{
    public string SessionToken { get; init; } = string.Empty;
    public string CsrfToken { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
}

public sealed class WorkbenchSessionManager
{
    private const int MaximumBootstrapTokens = 8;
    private static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromHours(8);
    private readonly object _gate = new object();
    private readonly string _bootstrapToken;
    private readonly HashSet<string> _bootstrapTokens = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _bootstrapOrder = new();
    private readonly Func<DateTime> _utcNow;
    private readonly TimeSpan _idleTimeout;
    private WorkbenchSessionGrant? _grant;
    private DateTime _expiresUtc;
    private long _lastFence;
    private string _lastFenceOperation = string.Empty;

    public WorkbenchSessionManager(
        string? bootstrapToken = null,
        Func<DateTime>? utcNow = null,
        TimeSpan? idleTimeout = null)
    {
        _bootstrapToken = string.IsNullOrWhiteSpace(bootstrapToken) ? CreateToken() : bootstrapToken;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _idleTimeout = idleTimeout ?? DefaultIdleTimeout;
        if (_idleTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(idleTimeout));
        AddBootstrapToken(_bootstrapToken);
    }

    public string BootstrapToken => _bootstrapToken;

    public string IssueBootstrapToken()
    {
        lock (_gate)
        {
            string token = CreateToken();
            AddBootstrapToken(token);
            return token;
        }
    }

    public bool TryExchange(string? bootstrapToken, out WorkbenchSessionGrant grant)
    {
        lock (_gate)
        {
            grant = new WorkbenchSessionGrant();
            string? matchedToken = _bootstrapTokens.FirstOrDefault(token => TokensEqual(token, bootstrapToken));
            if (matchedToken == null) return false;
            _bootstrapTokens.Remove(matchedToken);
            _bootstrapOrder.Remove(matchedToken);

            DateTime now = _utcNow();
            if (_grant == null || now > _expiresUtc)
            {
                _lastFence = 0;
                _lastFenceOperation = string.Empty;
                _grant = new WorkbenchSessionGrant
                {
                    SessionToken = CreateToken(),
                    CsrfToken = CreateToken(),
                    SessionId = "pwb-session-" + Guid.NewGuid().ToString("N")
                };
            }
            _expiresUtc = now.Add(_idleTimeout);
            grant = _grant;
            return true;
        }
    }

    public bool TryAuthorizeFence(string? sessionToken, string? csrfToken, long fence, string operationFingerprint, out string sessionId, out string issuerId, out string errorCode)
    {
        lock (_gate)
        {
            sessionId = string.Empty;
            issuerId = string.Empty;
            errorCode = string.Empty;
            if (!IsAuthorizedCore(sessionToken, csrfToken, out WorkbenchSessionGrant? grant)) { errorCode = "session.unauthorized"; return false; }
            _expiresUtc = _utcNow().Add(_idleTimeout);
            if (fence < 1) { errorCode = "session.fence_invalid"; return false; }
            if (fence < _lastFence) { errorCode = "session.fence_old"; return false; }
            if (fence == _lastFence && !string.Equals(operationFingerprint, _lastFenceOperation, StringComparison.Ordinal)) { errorCode = "session.replay_conflict"; return false; }
            if (fence > _lastFence) { _lastFence = fence; _lastFenceOperation = operationFingerprint ?? string.Empty; }
            sessionId = grant!.SessionId;
            issuerId = "pwb-issuer-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(grant.SessionId))).ToLowerInvariant()[..32];
            return true;
        }
    }

    public bool IsAuthorized(string? sessionToken, string? csrfToken)
    {
        lock (_gate)
        {
            DateTime now = _utcNow();
            bool authorized = IsAuthorizedCore(sessionToken, csrfToken, out _);
            if (authorized) _expiresUtc = now.Add(_idleTimeout);
            return authorized;
        }
    }

    private bool IsAuthorizedCore(string? sessionToken, string? csrfToken, out WorkbenchSessionGrant? grant)
    {
        grant = _grant;
        bool authorized = grant != null
            && _utcNow() <= _expiresUtc
            && TokensEqual(grant.SessionToken, sessionToken)
            && TokensEqual(grant.CsrfToken, csrfToken);
        return authorized;
    }
    private void AddBootstrapToken(string token)
    {
        while (_bootstrapTokens.Count >= MaximumBootstrapTokens && _bootstrapOrder.Count > 0)
        {
            string oldestToken = _bootstrapOrder.First!.Value;
            _bootstrapOrder.RemoveFirst();
            _bootstrapTokens.Remove(oldestToken);
        }
        _bootstrapTokens.Add(token);
        _bootstrapOrder.AddLast(token);
    }

    private static string CreateToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool TokensEqual(string expected, string? actual)
    {
        if (string.IsNullOrWhiteSpace(actual)) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
    }
}


