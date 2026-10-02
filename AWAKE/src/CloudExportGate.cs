using System;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;

namespace Awake;

/// <summary>
/// 云外发门：任何把本机文本交给云端 Provider 的路径都必须先过这里。
///
/// <para>文本对话（<see cref="AiTaskGateway"/>）与立绘生图共用这一份实现 —— 两条路径各写一套策略
/// 正是这类治理最容易出事的地方（一处改了、另一处忘了），所以策略只留这一处。</para>
///
/// <para>三步与文本路径逐条一致：分类必须已知（否则 <c>awake.cloud_export_unknown</c>）；
/// 必须被配置允许（否则 <c>awake.cloud_export_disabled</c>）；非 <c>none</c> 还必须拿到
/// <c>ai.cloud_export:&lt;classification&gt;</c> 权限（否则 <c>awake.permission_denied</c>）。
/// 权限请求由 <see cref="PermissionGate"/> 自己 marshal 回游戏线程，所以在后台任务里 await 是安全的。</para>
/// </summary>
internal sealed class CloudExportGate
{
    private const string Owner = AwakeConstants.OwnerValue;

    private readonly PermissionGate _permissionGate;

    internal CloudExportGate(PermissionGate permissionGate)
    {
        _permissionGate = permissionGate ?? throw new ArgumentNullException(nameof(permissionGate));
    }

    /// <summary>返回 <c>null</c> 表示放行；否则返回应当直接交给调用方的 typed 错误。</summary>
    internal async Task<FrameworkError> EnsureAsync(
        string classification,
        AwakeConfig config,
        RequestContext context,
        CancellationToken cancellationToken,
        string purpose = null)
    {
        if (!CloudExportPolicy.IsKnownClassification(classification))
        {
            return FrameworkErrors.Create(
                "awake.cloud_export_unknown",
                FrameworkErrorCategory.InvalidRequest,
                "The cloud export classification is unknown: " + classification,
                context?.CorrelationId,
                owner: Owner);
        }

        if (!CloudExportPolicy.IsClassificationAllowed(config, classification))
        {
            return FrameworkErrors.Create(
                "awake.cloud_export_disabled",
                FrameworkErrorCategory.Denied,
                "The cloud export classification is disabled: " + classification,
                context?.CorrelationId,
                owner: Owner);
        }

        if (StringComparer.Ordinal.Equals(classification, CloudExportPolicy.None)) return null;

        PermissionGateResult granted = await _permissionGate.EnsureAsync(
            PermissionCatalog.CloudExportPermission(classification),
            context,
            cancellationToken,
            purpose ?? ("将当前内容外发到云 AI Provider：" + classification + "。")).ConfigureAwait(false);
        if (granted.Granted) return null;

        return granted.Error ?? FrameworkErrors.Create(
            "awake.permission_denied",
            FrameworkErrorCategory.Denied,
            "The cloud export permission was not granted.",
            context?.CorrelationId,
            owner: Owner);
    }

    /// <summary>权限标识，用于给玩家看的提示文案。</summary>
    internal static string PermissionId(string classification)
    {
        return PermissionCatalog.CloudExportPermissionId(classification);
    }
}
