using System.Net.Http.Headers;

namespace PersonaWorkbench.Web;

internal enum ProviderRequestGateStatus
{
    Acquired,
    Busy,
    CoolingDown
}

internal sealed class ProviderRequestGate
{
    private const int DefaultCooldownSeconds = 30;
    private readonly object _sync = new object();
    private bool _requestInFlight;
    private DateTimeOffset _cooldownUntilUtc;

    public bool TryEnter(out ProviderRequestGateStatus status, out DateTimeOffset? cooldownUntilUtc)
    {
        lock (_sync)
        {
            if (_requestInFlight)
            {
                status = ProviderRequestGateStatus.Busy;
                cooldownUntilUtc = null;
                return false;
            }

            if (_cooldownUntilUtc > DateTimeOffset.UtcNow)
            {
                status = ProviderRequestGateStatus.CoolingDown;
                cooldownUntilUtc = _cooldownUntilUtc;
                return false;
            }

            _requestInFlight = true;
            status = ProviderRequestGateStatus.Acquired;
            cooldownUntilUtc = null;
            return true;
        }
    }

    public void SetCooldown(DateTimeOffset cooldownUntilUtc)
    {
        lock (_sync)
        {
            _cooldownUntilUtc = cooldownUntilUtc;
        }
    }

    public void Exit()
    {
        lock (_sync)
        {
            _requestInFlight = false;
        }
    }

    public static DateTimeOffset CalculateCooldown(RetryConditionHeaderValue? retryAfter)
    {
        if (retryAfter?.Date is DateTimeOffset date && date > DateTimeOffset.UtcNow) return date;
        TimeSpan delay = retryAfter?.Delta is TimeSpan delta && delta > TimeSpan.Zero
            ? delta
            : TimeSpan.FromSeconds(DefaultCooldownSeconds);
        return DateTimeOffset.UtcNow.Add(delay);
    }
}