using Awake.WorldbookStudio.Core;

namespace Awake.WorldbookStudio.Web;

internal sealed class WorkstationHandoffFaultInjection
{
    internal const string HeaderName = "X-AWAKE-Handoff-Fault";
    internal const string EnvironmentVariableName = "AWAKE_WBS_HANDOFF_FAULT";
    private const string ConsumeInDoubt = "consume_in_doubt";

    private readonly bool _enabled;
    private readonly string? _environmentValue;

    private WorkstationHandoffFaultInjection(bool enabled, string? environmentValue)
    {
        _enabled = enabled;
        _environmentValue = environmentValue;
    }

    internal static WorkstationHandoffFaultInjection ForEnvironment(IHostEnvironment environment)
        => new(
            environment.IsDevelopment() || environment.IsEnvironment("Test"),
            Environment.GetEnvironmentVariable(EnvironmentVariableName));

    internal bool ShouldInjectConsumeInDoubt(HttpRequest request)
    {
        if (!_enabled) return false;

        var requested = request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requested))
            requested = _environmentValue;
        if (string.IsNullOrWhiteSpace(requested))
            return false;
        if (string.Equals(requested.Trim(), ConsumeInDoubt, StringComparison.Ordinal))
            return true;
        throw new WorkstationHandoffException(
            "WB-HANDOFF-400",
            $"不支持的测试故障注入值：{requested}。",
            400,
            HeaderName);
    }
}
