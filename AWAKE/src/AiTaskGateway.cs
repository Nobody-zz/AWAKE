using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarcusAIFramework.Api;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Awake;

internal sealed class AiTaskSubmitResult
{
    internal bool Ok { get; set; }
    internal IAiTaskHandle Handle { get; set; }
    internal int Generation { get; set; }
    internal string ErrorCode { get; set; } = string.Empty;
    internal string ErrorDisplay { get; set; } = string.Empty;
    internal FrameworkError Error { get; set; }
    internal string CorrelationId { get; set; } = string.Empty;
}

internal sealed class AiRouteTurnEvents
{
    internal int Generation { get; set; }
    internal IAiTaskHandle Handle { get; set; }
    internal IDisposable Subscription { get; set; }
    internal CancellationTokenSource Cancellation { get; set; }
}

internal sealed class ContextEnvelopeResult
{
    internal string Input { get; set; }
    internal AiTaskSubmitResult Failure { get; set; }
}

internal sealed class AiTaskGateway : IDisposable
{
    private readonly IMarcusAiFrameworkHost _host;
    private readonly ExtensionId _caller;
    private readonly object _gate = new object();
    private readonly Dictionary<string, AiRouteTurnEvents> _active = new Dictionary<string, AiRouteTurnEvents>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _generations = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly PermissionGate _permissionGate;
    private bool _disposed;

    internal AiTaskGateway(IMarcusAiFrameworkHost host, PermissionGate permissionGate = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _caller = new ExtensionId(AwakeConstants.OwnerValue);
        _permissionGate = permissionGate ?? new PermissionGate(host);
    }

    internal PermissionGate PermissionGate => _permissionGate;

        internal bool HasPermission(string permission, RequestContext context)
    {
        try
        {
            if (context == null) return false;
            PermissionDefinition definition;
            if (!PermissionCatalog.TryGet(permission, out definition))
            {
                definition = new PermissionDefinition(
                    permission,
                    PermissionCategory.PlayerKnown,
                    PermissionEnforcement.Soft,
                    string.Empty,
                    string.Empty);
            }
            PermissionGateResult result = _permissionGate.Evaluate(definition, context);
            return result.Granted;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("ai_task_permission_evaluate_error permission=" + permission + " error=" + ex.Message);
            return false;
        }
    }

    internal async Task<PermissionGateResult> EnsureRoutePermissionAsync(
        string routeId,
        RequestContext context,
        CancellationToken cancellationToken,
        string purpose = null)
    {
        PermissionDefinition definition = PermissionCatalog.RoutePermission(routeId);
        return await _permissionGate.EnsureAsync(
            definition,
            context,
            cancellationToken,
            purpose ?? ("AWAKE 需要调用 AI 路由：" + routeId + "。")).ConfigureAwait(false);
    }

    internal async Task<PermissionGateResult> HasSoftPermissionAsync(
        string permission,
        string purpose,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        PermissionDefinition definition;
        if (!PermissionCatalog.TryGet(permission, out definition))
        {
            definition = new PermissionDefinition(
                permission,
                PermissionCategory.Command,
                PermissionEnforcement.Soft,
                purpose ?? string.Empty,
                string.Empty);
        }
        return await _permissionGate.EnsureAsync(definition, context, cancellationToken, purpose).ConfigureAwait(false);
    }

        internal async Task<AiTaskSubmitResult> SubmitAsync(
        string routeId,
        string inputText,
        string outputSchemaId,
        string cloudExportClassification,
        bool includeContext,
        Action<AiTaskEvent> onEvent,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        AiTaskSubmitResult fail = new AiTaskSubmitResult { Ok = false };
        if (context == null)
        {
            fail.Error = FrameworkErrors.Create("awake.context_missing", FrameworkErrorCategory.InvalidRequest, "A request context is required.", null, owner: AwakeConstants.OwnerValue);
            fail.ErrorCode = fail.Error.Code;
            fail.ErrorDisplay = "请求上下文缺失。";
            return fail;
        }
        fail.CorrelationId = context.CorrelationId;
        if (cancellationToken.IsCancellationRequested)
        {
            fail.Error = CancelledError(context.CorrelationId);
            fail.ErrorCode = fail.Error.Code;
            fail.ErrorDisplay = "AI 任务已取消。";
            return fail;
        }
        if (string.IsNullOrWhiteSpace(routeId))
        {
            fail.Error = FrameworkErrors.Create("awake.route_missing", FrameworkErrorCategory.InvalidRequest, "Route ID is required.", context.CorrelationId, owner: AwakeConstants.OwnerValue);
            fail.ErrorCode = fail.Error.Code;
            fail.ErrorDisplay = "路由缺失。";
            return fail;
        }

        PermissionGateResult routePermission = await EnsureRoutePermissionAsync(routeId, context, cancellationToken).ConfigureAwait(false);
        if (!routePermission.Granted)
        {
            fail.Error = routePermission.Error ?? FrameworkErrors.Create(
                "awake.permission_denied",
                FrameworkErrorCategory.Denied,
                "The route was not authorized.",
                context.CorrelationId,
                owner: AwakeConstants.OwnerValue);
            fail.ErrorCode = fail.Error.Code;
            fail.ErrorDisplay = "路由权限未授予：" + AiTaskConstants.RoutePermission(routeId);
            return fail;
        }

        string effectiveCloudExport = string.IsNullOrWhiteSpace(cloudExportClassification)
            ? CloudExportPolicy.None
            : cloudExportClassification;
        AwakeConfig cloudConfig = AwakeSettings.Current;
        AiTaskSubmitResult cloudFail = await EnforceCloudExportAsync(effectiveCloudExport, cloudConfig, context, cancellationToken).ConfigureAwait(false);
        if (cloudFail != null)
        {
            return cloudFail;
        }

        bool bound = await AwakeRuntime.EnsureCurrentHeroBoundAsync(_host, cancellationToken).ConfigureAwait(false);
        if (!bound)
        {
            fail.Error = FrameworkErrors.Create("awake.player_unbound", FrameworkErrorCategory.Unavailable, "The current player hero could not be bound.", context.CorrelationId, retryable: true, owner: AwakeConstants.OwnerValue);
            fail.ErrorCode = fail.Error.Code;
            fail.ErrorDisplay = "当前玩家尚未绑定。";
            return fail;
        }

        string effectiveInput = inputText;
        if (includeContext)
        {
            ContextEnvelopeResult envelope = await BuildContextEnvelopeAsync(inputText, effectiveCloudExport, cloudConfig, context, cancellationToken).ConfigureAwait(false);
            if (envelope.Failure != null)
            {
                return envelope.Failure;
            }
            effectiveInput = envelope.Input;
        }

        AiTaskRequest request = new AiTaskRequest(
            Guid.NewGuid().ToString("N"),
            routeId,
            effectiveInput,
            outputSchemaId,
            effectiveCloudExport,
            DateTimeOffset.UtcNow + AwakeConstants.RequestTimeout,
            Guid.NewGuid().ToString("N"),
            false);

        OperationResult<IAiTaskHandle> submitted;
        try
        {
            submitted = await _host.Ai.SubmitAsync(request, context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            AwakeLog.Write("ai_task_submit_cancelled route=" + routeId);
            fail.Error = CancelledError(context.CorrelationId);
            fail.ErrorCode = fail.Error.Code;
            fail.ErrorDisplay = "AI 任务已取消。";
            return fail;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("ai_task_submit_error route=" + routeId + " error=" + ex.Message);
            fail.Error = FrameworkErrors.Create("awake.ai_error", FrameworkErrorCategory.ProviderFailure, "AI submission failed.", context.CorrelationId, owner: AwakeConstants.OwnerValue);
            fail.ErrorCode = fail.Error.Code;
            fail.ErrorDisplay = "AI 暂时不可用。";
            return fail;
        }

        if (!submitted.IsSuccess || submitted.Value == null)
        {
            AwakeLog.Write("ai_task_submit_failed route=" + routeId + " code=" + (submitted.Error?.Code ?? "unknown"));
            fail.Error = submitted.Error ?? FrameworkErrors.Create(
                "awake.ai_unavailable",
                FrameworkErrorCategory.Unavailable,
                "AI submission returned no usable result.",
                context.CorrelationId,
                owner: AwakeConstants.OwnerValue);
            fail.ErrorCode = submitted.Error?.Code ?? "awake.ai_unavailable";
            fail.ErrorDisplay = DescribeError(submitted.Error);
            return fail;
        }

        IAiTaskHandle handle = submitted.Value;
        int generation;
        lock (_gate)
        {
            int current;
            if (!_generations.TryGetValue(routeId, out current)) current = 0;
            generation = current + 1;
            _generations[routeId] = generation;
        }
        IDisposable subscription;
        try
        {
            subscription = handle.Subscribe(evt => onEvent?.Invoke(evt));
        }
        catch (Exception ex)
        {
            AwakeLog.Write("ai_task_subscribe_error route=" + routeId + " error=" + ex.Message);
            _ = handle.CancelAsync(CancellationToken.None);
            fail.Error = FrameworkErrors.Create("awake.subscribe_error", FrameworkErrorCategory.InternalFailure, "Task subscription failed.", context.CorrelationId, owner: AwakeConstants.OwnerValue);
            fail.ErrorCode = fail.Error.Code;
            fail.ErrorDisplay = "AI 任务订阅失败。";
            return fail;
        }

        lock (_gate)
        {
            if (_disposed)
            {
                _active.Remove(routeId);
                subscription.Dispose();
                _ = handle.CancelAsync(CancellationToken.None);
                fail.Error = CancelledError(context.CorrelationId);
                fail.ErrorCode = fail.Error.Code;
                fail.ErrorDisplay = "AI 网关已关闭。";
                return fail;
            }

            AiRouteTurnEvents previous;
            if (_active.TryGetValue(routeId, out previous))
            {
                CancelLocked(previous);
                _active.Remove(routeId);
            }

            _active[routeId] = new AiRouteTurnEvents
            {
                Generation = generation,
                Handle = handle,
                Subscription = subscription,
                Cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            };
            _generations[routeId] = generation;
        }

        AwakeLog.Write("ai_task_submit_accepted route=" + routeId + " generation=" + generation);
        return new AiTaskSubmitResult { Ok = true, Handle = handle, Generation = generation, CorrelationId = context.CorrelationId };
    }

    internal void CancelRoute(string routeId)
    {
        AiRouteTurnEvents active;
        lock (_gate)
        {
            if (!_active.TryGetValue(routeId, out active))
            {
                if (_generations.ContainsKey(routeId)) _generations[routeId]++;
                return;
            }
            _active.Remove(routeId);
            _generations[routeId]++;
        }
        CancelLocked(active);
    }

    internal void FinishTurn(string routeId, int generation)
    {
        AiRouteTurnEvents active;
        lock (_gate)
        {
            AiRouteTurnEvents current;
            if (!_active.TryGetValue(routeId, out current) || current.Generation != generation) return;
            _active.Remove(routeId);
            active = current;
        }
        try { active.Subscription?.Dispose(); } catch { }
        try { active.Cancellation?.Dispose(); } catch { }
    }

    internal RequestContext CreateContext(TimeSpan budget)
    {
        SessionRef session = _host.CurrentSession ?? new SessionRef(string.Empty, string.Empty, string.Empty);
        return new RequestContext(
            _caller,
            session,
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow + budget);
    }

    internal string DescribeError(FrameworkError error)
    {
        if (error == null) return "AI 暂时不可用。";
        switch (error.Category)
        {
            case FrameworkErrorCategory.Unavailable: return "AI 暂时不可用。";
            case FrameworkErrorCategory.Denied: return "AI 被拒绝了。";
            case FrameworkErrorCategory.Timeout: return "AI 回应超时了。";
            case FrameworkErrorCategory.Expired: return "AI 的时机已经过去。";
            case FrameworkErrorCategory.Cancelled: return "AI 静默了。";
            default: return "AI 似乎陷入了沉思。";
        }
    }

    public void Dispose()
    {
        List<AiRouteTurnEvents> all;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            all = new List<AiRouteTurnEvents>(_active.Values);
            _active.Clear();
        }
        foreach (AiRouteTurnEvents active in all)
        {
            CancelLocked(active);
        }
    }

    private int GetRouteGeneration(string routeId)
    {
        lock (_gate)
        {
            int generation;
            if (!_generations.TryGetValue(routeId, out generation)) generation = 0;
            return generation;
        }
    }

    private static void CancelLocked(AiRouteTurnEvents active)
    {
        if (active == null) return;
        try { active.Cancellation?.Cancel(); } catch { }
        try { active.Subscription?.Dispose(); } catch { }
        _ = active.Handle?.CancelAsync(CancellationToken.None);
        try { active.Cancellation?.Dispose(); } catch { }
    }

    private async Task<ContextEnvelopeResult> BuildContextEnvelopeAsync(string inputText, string effectiveCloudExportClassification, AwakeConfig config, RequestContext context, CancellationToken cancellationToken)
    {
        if (_host.Context == null)
        {
            AwakeLog.Write("ai_task_context_unavailable fallback=raw_input");
            return new ContextEnvelopeResult { Input = inputText };
        }

        ContextPlanRequest request = new ContextPlanRequest(
            AiTaskConstants.ContextProviderIds,
            Array.Empty<string>(),
            new[] { AiTaskConstants.PlayerKnownScope },
            CloudExportPolicy.AllowedContextClassifications(config, effectiveCloudExportClassification),
            AiTaskConstants.ContextMaximumTokens);

        OperationResult<ContextPlan> planned;
        try
        {
            planned = await _host.Context.PlanAsync(request, context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("ai_task_context_plan_error error=" + ex.Message + " fallback=raw_input");
            return new ContextEnvelopeResult { Input = inputText };
        }

        if (!planned.IsSuccess || planned.Value == null)
        {
            FrameworkError error = planned.Error;
            if (error != null && error.Category == FrameworkErrorCategory.Cancelled)
            {
                AiTaskSubmitResult fail = new AiTaskSubmitResult
                {
                    Ok = false,
                    Error = error,
                    ErrorCode = error.Code,
                    ErrorDisplay = "上下文规划已取消。",
                    CorrelationId = context.CorrelationId
                };
                return new ContextEnvelopeResult { Failure = fail };
            }
            AwakeLog.Write("ai_task_context_plan_failed code=" + (error?.Code ?? "unknown") + " fallback=raw_input");
            return new ContextEnvelopeResult { Input = inputText };
        }

        string[] allowedClassifications = CloudExportPolicy.AllowedContextClassifications(config, effectiveCloudExportClassification);
        List<ContextContribution> candidates = new List<ContextContribution>();
        foreach (ContextContribution contribution in planned.Value.Included ?? Array.Empty<ContextContribution>())
        {
            if (contribution == null || string.IsNullOrWhiteSpace(contribution.ProviderId) || contribution.PayloadJson == null) continue;
            if (allowedClassifications.Length > 0 && !ContainsClassification(allowedClassifications, contribution.CloudExportClassification))
            {
                AwakeLog.Write("ai_task_context_contribution_filtered provider=" + contribution.ProviderId + " classification=" + (contribution.CloudExportClassification ?? "none"));
                continue;
            }
            candidates.Add(contribution);
        }

        List<ContextContribution> included = new List<ContextContribution>();
        Dictionary<string, int> providerCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (ContextContribution contribution in candidates)
        {
            int count;
            providerCounts.TryGetValue(contribution.ProviderId, out count);
            if (count >= AiTaskConstants.ContextMaximumContributionsPerProvider) continue;
            providerCounts[contribution.ProviderId] = count + 1;
            included.Add(contribution);
        }

        JObject root = new JObject
        {
            ["task"] = inputText ?? string.Empty,
            ["context"] = new JArray()
        };
        JArray contextArray = (JArray)root["context"];
        int totalBytes = Encoding.UTF8.GetByteCount(root.ToString(Formatting.None));
        foreach (ContextContribution contribution in included)
        {
            JObject item = new JObject
            {
                ["contributionId"] = contribution.ContributionId ?? string.Empty,
                ["providerId"] = contribution.ProviderId ?? string.Empty,
                ["accessScope"] = contribution.AccessScope ?? string.Empty,
                ["payloadJson"] = contribution.PayloadJson ?? "{}"
            };
            JArray candidate = new JArray(contextArray.ToArray<JToken>());
            candidate.Add(item);
            JObject candidateRoot = new JObject
            {
                ["task"] = root["task"],
                ["context"] = candidate
            };
            int candidateBytes = Encoding.UTF8.GetByteCount(candidateRoot.ToString(Formatting.None));
            if (candidateBytes > AiTaskConstants.ContextTotalMaximumBytes) break;
            contextArray.Add(item);
            totalBytes = candidateBytes;
        }

        if (contextArray.Count > 0)
        {
            AwakeLog.Write("ai_task_context_included route=all contributions=" + contextArray.Count + " bytes=" + totalBytes);
        }
        return new ContextEnvelopeResult { Input = root.ToString(Formatting.None) };
    }

    private async Task<AiTaskSubmitResult> EnforceCloudExportAsync(
        string classification,
        AwakeConfig config,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        AiTaskSubmitResult fail = new AiTaskSubmitResult { Ok = false, CorrelationId = context?.CorrelationId };
        if (!CloudExportPolicy.IsKnownClassification(classification))
        {
            fail.Error = FrameworkErrors.Create(
                "awake.cloud_export_unknown",
                FrameworkErrorCategory.InvalidRequest,
                "The cloud export classification is unknown: " + classification,
                context?.CorrelationId,
                owner: AwakeConstants.OwnerValue);
            fail.ErrorCode = fail.Error.Code;
            fail.ErrorDisplay = "云外发分类无效。";
            AwakeLog.Write("ai_task_cloud_export_unknown classification=" + classification + " correlation=" + (context?.CorrelationId ?? "none"));
            return fail;
        }
        if (!CloudExportPolicy.IsClassificationAllowed(config, classification))
        {
            fail.Error = FrameworkErrors.Create(
                "awake.cloud_export_disabled",
                FrameworkErrorCategory.Denied,
                "The cloud export classification is disabled: " + classification,
                context?.CorrelationId,
                owner: AwakeConstants.OwnerValue);
            fail.ErrorCode = fail.Error.Code;
            fail.ErrorDisplay = "云外发未启用：" + classification;
            AwakeLog.Write("ai_task_cloud_export_disabled classification=" + classification + " correlation=" + (context?.CorrelationId ?? "none"));
            return fail;
        }
        if (!StringComparer.Ordinal.Equals(classification, CloudExportPolicy.None))
        {
            PermissionGateResult cloudPermission = await _permissionGate.EnsureAsync(
                PermissionCatalog.CloudExportPermission(classification),
                context,
                cancellationToken,
                "将当前对话与角色状态外发到云 AI Provider。").ConfigureAwait(false);
            if (!cloudPermission.Granted)
            {
                fail.Error = cloudPermission.Error ?? FrameworkErrors.Create(
                    "awake.permission_denied",
                    FrameworkErrorCategory.Denied,
                    "The cloud export permission was not granted.",
                    context?.CorrelationId,
                    owner: AwakeConstants.OwnerValue);
                fail.ErrorCode = fail.Error.Code;
                fail.ErrorDisplay = "云外发权限未授予：" + PermissionCatalog.CloudExportPermissionId(classification);
                AwakeLog.Write("ai_task_cloud_export_permission_denied classification=" + classification + " code=" + fail.ErrorCode + " correlation=" + (context?.CorrelationId ?? "none"));
                return fail;
            }
        }
        return null;
    }

    private static bool ContainsClassification(string[] allowed, string classification)
    {
        if (allowed == null || allowed.Length == 0) return true;
        foreach (string candidate in allowed)
        {
            if (StringComparer.Ordinal.Equals(candidate, classification)) return true;
        }
        return false;
    }

    private static FrameworkError CancelledError(string correlationId)
    {
        return FrameworkErrors.Create("awake.cancelled", FrameworkErrorCategory.Cancelled, "The AI task was cancelled.", correlationId, owner: AwakeConstants.OwnerValue);
    }
}
