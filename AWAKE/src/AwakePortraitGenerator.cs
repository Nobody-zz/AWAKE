using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;

namespace Awake;

/// <summary>
/// 立绘生成的**唯一入口**。两件事按固定顺序做：
///
/// <para>① 先过云外发门（与文本对话共用 <see cref="CloudExportGate"/>）；<br/>
/// ② 再选路：首选**框架路**（<c>Host.Media</c> 出图 → <c>Host.Assets</c> 取字节；钥匙与出网都在
/// 运行时进程里，游戏进程既不持钥也不出网），框架路**不可用**时退回 B 路
/// （<see cref="AwakeImageClient"/>：模组侧自建 HttpClient + 自己的 DPAPI 钥匙）。</para>
///
/// <para><b>退回 B 路不构成绕过治理</b>：门在选路之前就跑完了，两条路共用同一个判决。
/// 每次退回都写明原因 —— 静默降级是这类迁移里最容易出事的地方。</para>
///
/// <para><b>框架路「跑过但失败」不退回</b>：那种情况再打一发 B 路会**双倍消耗额度**，
/// 还可能把一次策略拒绝伪装成一次网络抖动。只有「这条路根本没法驱动」才退。</para>
/// </summary>
internal sealed class AwakePortraitGenerator
{
    private const string RouteId = "awake.portrait";
    private const string ProfileId = "awake.portrait";
    private const string ProviderId = "awake.portrait";
    private const string CredentialReference = "awake.portrait";
    private const string Provenance = "awake.portrait";
    private const string RetentionClass = "campaign";

    /// <summary>Player2 那条形状不认模型名（适配器用合成名），给个非空占位即可。</summary>
    private const string Player2PlaceholderModel = "player2";

    private readonly IMarcusAiFrameworkHost _host;
    private readonly CloudExportGate _cloudExportGate;

    internal AwakePortraitGenerator(IMarcusAiFrameworkHost host, CloudExportGate cloudExportGate)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _cloudExportGate = cloudExportGate ?? throw new ArgumentNullException(nameof(cloudExportGate));
    }

    internal async Task<AwakeImageOutcome> GenerateAsync(
        AwakeImageEndpoint endpoint,
        string prompt,
        int width,
        int height,
        string apiKey,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        if (endpoint == null)
        {
            return new AwakeImageOutcome { Ok = false, ErrorCode = "image_endpoint_missing", ErrorMessage = "出图端点缺失。" };
        }

        AwakeConfig config = AwakeSettings.Current;
        string classification = endpoint.IsCloud
            ? CloudExportPolicy.ResolvePortraitClassification(config)
            : CloudExportPolicy.None;

        FrameworkError gateError = await _cloudExportGate
            .EnsureAsync(classification, config, context, cancellationToken, "将 NPC 立绘提示词外发到云 AI Provider。")
            .ConfigureAwait(false);
        if (gateError != null)
        {
            AwakeLog.Write("portrait_cloud_export_denied classification=" + classification + " code=" + gateError.Code);
            return new AwakeImageOutcome { Ok = false, ErrorCode = gateError.Code, ErrorMessage = "云外发未获授权。" };
        }

        AwakeImageOutcome frameworkOutcome = await TryFrameworkAsync(endpoint, prompt, width, height, apiKey, classification, config, context, cancellationToken).ConfigureAwait(false);
        if (frameworkOutcome != null) return frameworkOutcome;

        AwakeLog.Write("portrait_fallback_to_module_client shape=" + AwakeImageEndpointResolver.ShapeId(endpoint.Shape));
        return await AwakeImageClient
            .GenerateAsync(endpoint, new AwakeImageRequest(prompt, null, width, height), apiKey, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 返回 <c>null</c> ⇒ 框架路**不可用**，调用方应当退回 B 路。
    /// 返回非 null ⇒ 框架路**跑过了**（成功或失败都算），调用方必须直接采纳，不许再打第二发。
    /// </summary>
    private async Task<AwakeImageOutcome> TryFrameworkAsync(
        AwakeImageEndpoint endpoint,
        string prompt,
        int width,
        int height,
        string apiKey,
        string classification,
        AwakeConfig config,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        FrameworkHost frameworkHost = _host as FrameworkHost;
        IProviderRuntimePort providerPort = frameworkHost == null ? null : frameworkHost.Runtime as IProviderRuntimePort;
        if (frameworkHost == null || providerPort == null)
        {
            AwakeLog.Write("portrait_framework_unavailable reason=runtime_port_missing");
            return null;
        }

        string model = ResolveModel(config, endpoint.Shape);
        if (model.Length == 0)
        {
            // OpenAI 兼容那条形状必须给模型名（profile 的 default_model 不能为空）。
            // 猜一个模型名等于往服务端发一个不存在的模型，所以这里只能退回。
            AwakeLog.Write("portrait_framework_unavailable reason=model_missing shape=" + AwakeImageEndpointResolver.ShapeId(endpoint.Shape));
            return null;
        }

        try
        {
            if (endpoint.IsCloud)
            {
                if (string.IsNullOrEmpty(apiKey))
                {
                    AwakeLog.Write("portrait_framework_unavailable reason=credential_missing");
                    return null;
                }

                // 钥匙交给运行时保管：模组侧那份 DPAPI 只是迁移来源，不再参与出网。
                OperationResult<ProviderCredentialResult> credential = await providerPort.UpsertCredentialAsync(
                    new ProviderCredentialRequest(ProfileId, ProviderId, RouteId, CredentialReference, apiKey),
                    context,
                    cancellationToken).ConfigureAwait(false);
                if (!credential.IsSuccess)
                {
                    AwakeLog.Write("portrait_framework_unavailable reason=credential_upsert_failed code=" + (credential.Error == null ? "unknown" : credential.Error.Code));
                    return null;
                }
            }

            OperationResult<ProviderProfileResult> profile = await providerPort.UpsertProfileAsync(
                new ProviderProfileRequest(
                    ProfileId,
                    ProviderId,
                    RouteId,
                    AwakeImageEndpointResolver.ShapeId(endpoint.Shape),
                    endpoint.BaseUrl,
                    model,
                    endpoint.IsCloud ? CredentialReference : string.Empty,
                    endpoint.IsCloud),
                context,
                cancellationToken).ConfigureAwait(false);
            if (!profile.IsSuccess)
            {
                AwakeLog.Write("portrait_framework_unavailable reason=profile_upsert_failed code=" + (profile.Error == null ? "unknown" : profile.Error.Code));
                return null;
            }

            long startedAt = Stopwatch.GetTimestamp();
            ImageGenerationRequest request = new ImageGenerationRequest(
                RouteId,
                prompt,
                null,
                width,
                height,
                0L,
                Provenance,
                RetentionClass,
                classification,
                DateTimeOffset.UtcNow + AwakeConstants.RequestTimeout,
                Guid.NewGuid().ToString("N"));

            OperationResult<GeneratedAssetResult> generated = await frameworkHost
                .Media
                .GenerateImageAsync(request, context, cancellationToken)
                .ConfigureAwait(false);
            if (!generated.IsSuccess)
            {
                return FrameworkFailure(generated.Error, startedAt, "出图失败。");
            }

            OperationResult<AssetContent> content = await frameworkHost
                .Assets
                .ReadAsync(generated.Value.Handle.AssetId, context, cancellationToken)
                .ConfigureAwait(false);
            if (!content.IsSuccess)
            {
                return FrameworkFailure(content.Error, startedAt, "读回立绘字节失败。");
            }

            byte[] bytes = content.Value.GetContentCopy();
            string format = AwakeImageShapeAdapter.SniffFormat(bytes);
            int actualWidth;
            int actualHeight;
            if (!AwakeImageShapeAdapter.TryReadDimensions(bytes, format, out actualWidth, out actualHeight))
            {
                actualWidth = 0;
                actualHeight = 0;
            }

            AwakeLog.Write("portrait_framework_ok shape=" + AwakeImageEndpointResolver.ShapeId(endpoint.Shape) + " bytes=" + bytes.Length);
            return new AwakeImageOutcome
            {
                Ok = true,
                Reply = new AwakeImageReply(bytes, format, actualWidth, actualHeight, content.Value.Handle.MediaType),
                ElapsedMs = Elapsed(startedAt),
                RequestUrl = "runtime:" + RouteId
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // 抛异常说明这条路连不起来（组合缺失、反射失败之类）⇒ 当作不可用，允许退回。
            AwakeLog.Write("portrait_framework_unavailable reason=exception error=" + ex.Message);
            return null;
        }
    }

    private static string ResolveModel(AwakeConfig config, AwakeImageShape shape)
    {
        string configured = config == null ? string.Empty : (config.PortraitImageModel ?? string.Empty).Trim();
        if (configured.Length > 0) return configured;
        return shape == AwakeImageShape.Player2 ? Player2PlaceholderModel : string.Empty;
    }

    private static AwakeImageOutcome FrameworkFailure(FrameworkError error, long startedAt, string fallbackMessage)
    {
        AwakeLog.Write("portrait_framework_failed code=" + (error == null ? "unknown" : error.Code));
        return new AwakeImageOutcome
        {
            Ok = false,
            ErrorCode = error == null ? "image_framework_failed" : error.Code,
            ErrorMessage = error == null || string.IsNullOrEmpty(error.SafeFallback) ? fallbackMessage : error.SafeFallback,
            ElapsedMs = Elapsed(startedAt)
        };
    }

    private static long Elapsed(long startedAt)
    {
        return (long)((Stopwatch.GetTimestamp() - startedAt) * 1000.0 / Stopwatch.Frequency);
    }
}
