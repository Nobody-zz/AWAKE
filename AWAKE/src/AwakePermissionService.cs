using System;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;

namespace Awake;

/// <summary>
/// AWAKE 的本地权限策略，替代框架默认的 UnavailablePermissionService。
/// 软权限（本机读取 / 提示词登记 / 本地落盘）直接放行；硬权限（AI 路由、云外发、
/// 世界状态命令）必须由用户在 MCM 显式开启 AllowAiRouting。
/// 授权入口只有 MCM 开关一处，不做运行时弹窗，因此 Request 与 Evaluate 结论一致。
/// </summary>
internal sealed class AwakePermissionService : IPermissionService
{
    private const string CloudExportPrefix = "ai.cloud_export:";

    private static readonly ExtensionId OwnerId = new ExtensionId(AwakeConstants.OwnerValue);

    public PermissionEvaluation Evaluate(string permissionId, RequestContext context)
    {
        return Decide(permissionId, "evaluate");
    }

    public Task<OperationResult<PermissionEvaluation>> RequestAsync(
        string permissionId,
        string purpose,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(OperationResult<PermissionEvaluation>.Succeeded(Decide(permissionId, "request")));
    }

    public OperationResult<bool> Revoke(string permissionId, ExtensionId extensionId)
    {
        // 策略无状态：撤销 = 关闭 MCM 开关。这里不做任何隐藏放行。
        return OperationResult<bool>.Succeeded(true);
    }

    private static PermissionEvaluation Decide(string permissionId, string surface)
    {
        PermissionDefinition definition;
        if (!PermissionCatalog.TryGet(permissionId, out definition))
        {
            return Record(permissionId, false, surface, "awake.permission_unknown");
        }

        AwakeConfig config = AwakeSettings.Current;
        bool allow;
        string reason;
        switch (definition.Category)
        {
            case PermissionCategory.PlayerKnown:
            case PermissionCategory.Prompt:
            case PermissionCategory.Storage:
            case PermissionCategory.Rag:
                allow = true;
                reason = "awake.permission_local_capability";
                break;
            case PermissionCategory.Route:
                allow = config != null && config.AllowAiRouting;
                reason = allow ? "awake.permission_ai_routing_granted" : "awake.permission_ai_routing_disabled";
                break;
            case PermissionCategory.Command:
                // 世界状态命令只可能来自 AI 回合；AI 关闭时命令根本不会产生，故共用同一开关。
                allow = config != null && config.AllowAiRouting;
                reason = allow ? "awake.permission_world_command_granted" : "awake.permission_ai_routing_disabled";
                break;
            case PermissionCategory.CloudExport:
                allow = config != null
                    && config.AllowAiRouting
                    && CloudExportPolicy.IsClassificationAllowed(config, ClassificationOf(permissionId));
                reason = allow ? "awake.permission_cloud_export_granted" : "awake.permission_cloud_export_disabled";
                break;
            default:
                allow = false;
                reason = "awake.permission_unknown";
                break;
        }

        return Record(permissionId, allow, surface, reason);
    }

    private static PermissionEvaluation Record(string permissionId, bool allow, string surface, string reason)
    {
        string id = string.IsNullOrWhiteSpace(permissionId) ? "unavailable.permission" : permissionId;
        AwakeLog.Write((allow ? "permission_granted" : "permission_denied")
            + " permission=" + id
            + " surface=" + surface
            + " reason=" + reason);
        return new PermissionEvaluation(
            id,
            OwnerId,
            allow ? PermissionDecision.Granted : PermissionDecision.Denied,
            reason,
            null);
    }

    private static string ClassificationOf(string permissionId)
    {
        return permissionId != null && permissionId.StartsWith(CloudExportPrefix, StringComparison.Ordinal)
            ? permissionId.Substring(CloudExportPrefix.Length)
            : string.Empty;
    }
}
