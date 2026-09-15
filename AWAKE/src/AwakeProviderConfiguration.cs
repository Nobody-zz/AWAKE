using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using MCM.Common;
using TaleWorlds.Library;

namespace Awake;

internal static class AwakeProviderConfiguration
{
    internal const string ProfileId = "awake.provider.default";
    internal const string ProviderId = "awake.provider.default";
    internal const string CredentialReference = "awake.provider.default";

    private static readonly string[] ProviderKindLabels =
    {
        "OpenAI 兼容",
        "Anthropic",
        "Ollama"
    };

    private static readonly object StateGate = new object();
    private static string latestStatus = "尚未执行 AI 连接检查。";
    private static string latestModelStatus = "尚未拉取可用模型。";
    private static bool operationActive;

    internal static string Status
    {
        get
        {
            lock (StateGate) return latestStatus;
        }
    }

    internal static string ModelStatus
    {
        get
        {
            lock (StateGate) return latestModelStatus;
        }
    }

    internal static Dropdown<string> CreateProviderKindDropdown()
    {
        return new Dropdown<string>(ProviderKindLabels, 0);
    }

    internal static string ResolveProviderKind(AwakeConfig config)
    {
        int selectedIndex = config?.ProviderKind == null ? 0 : config.ProviderKind.SelectedIndex;
        switch (selectedIndex)
        {
            case 1:
                return "anthropic";
            case 2:
                return "ollama";
            default:
                return "openai_compatible";
        }
    }

    internal static string ResolveProviderKindLabel(AwakeConfig config)
    {
        int selectedIndex = config?.ProviderKind == null ? 0 : config.ProviderKind.SelectedIndex;
        if (selectedIndex < 0 || selectedIndex >= ProviderKindLabels.Length) selectedIndex = 0;
        return ProviderKindLabels[selectedIndex];
    }

    internal static void PromptForApiKey()
    {
        lock (StateGate)
        {
            if (operationActive)
            {
                AwakeFeedback.ShowWarning("AI 配置操作正在进行，请稍候。");
                return;
            }
        }

        InformationManager.ShowTextInquiry(
            new TextInquiryData(
                "输入或替换 API Key",
                "输入当前 AI 服务的 API Key。输入框保持可见，便于你核对内容；保存后不会写入 MCM 配置、存档或日志。",
                true,
                true,
                "保存",
                "取消",
                secret => BeginCredentialSave((secret ?? string.Empty).Trim()),
                null,
                false,
                null,
                string.Empty,
                string.Empty),
            true,
            false);
    }

    internal static void ApplyProviderConfiguration()
    {
        if (!TryPrepareConfiguration(out ProviderConfigSnapshot snapshot, out string preparationError))
        {
            ShowImmediateFailure("provider_apply", preparationError);
            return;
        }

        StartOperation(
            "awake_mcm_provider_apply",
            async cancellationToken =>
            {
                if (!TryResolveRuntime(out IProviderRuntimePort provider, out RequestContext context, out string resolutionError))
                {
                    return RejectedProviderOperation("provider_apply", "provider_runtime_unavailable", resolutionError);
                }

                OperationResult<bool> result = await ApplyProfilesAsync(provider, context, snapshot, cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    return ProviderActionResult.Failed(DescribeFailure(result.Error, "provider_apply", "AI 配置应用失败"));
                }

                return ProviderActionResult.Succeeded("AI 配置已应用到 AWAKE 的对话、预处理、后处理和记忆路由。", null);
            },
            result =>
            {
                UpdateStatusOnUi(result.Message);
                ShowActionResult(result);
            });
    }

    internal static async Task<OperationResult<bool>> ApplyProviderConfigurationAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Failure<bool>("provider_cancelled", FrameworkErrorCategory.Cancelled, "AI 配置应用已取消。", null);
        }

        if (!TryPrepareConfiguration(out ProviderConfigSnapshot snapshot, out string preparationError))
        {
            return Failure<bool>("provider_settings_invalid", FrameworkErrorCategory.InvalidRequest, preparationError, null);
        }

        if (!TryResolveRuntime(out IProviderRuntimePort provider, out RequestContext context, out string resolutionError))
        {
            return Failure<bool>("provider_runtime_unavailable", FrameworkErrorCategory.Unavailable, resolutionError, null);
        }

        return await ApplyProfilesAsync(provider, context, snapshot, cancellationToken).ConfigureAwait(false);
    }

    internal static void PullModels()
    {
        if (!TryPrepareConfiguration(out ProviderConfigSnapshot snapshot, out string preparationError))
        {
            ShowImmediateFailure("provider_models", preparationError);
            return;
        }

        StartOperation(
            "awake_mcm_provider_models",
            async cancellationToken =>
            {
                if (!TryResolveRuntime(out IProviderRuntimePort provider, out RequestContext context, out string resolutionError))
                {
                    return RejectedProviderOperation("provider_models", "provider_runtime_unavailable", resolutionError);
                }

                OperationResult<ProviderModelsResult> result = await ListModelsAsync(provider, context, snapshot, cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess || result.Value == null)
                {
                    return ProviderActionResult.Failed(DescribeFailure(result.Error, "provider_models", "模型列表获取失败"));
                }

                return ProviderActionResult.Succeeded(
                    "连接成功，已读取 " + result.Value.Models.Count + " 个可用模型。",
                    result.Value.Models);
            },
            result =>
            {
                UpdateStatusOnUi(result.Message);
                UpdateModelStatusOnUi(result);
                if (result.Success)
                {
                    ShowModelList(result.Models);
                }
                else
                {
                    ShowActionResult(result);
                }
            });
    }

    internal static void TestConnection()
    {
        if (!TryPrepareConfiguration(out ProviderConfigSnapshot snapshot, out string preparationError))
        {
            ShowImmediateFailure("provider_test", preparationError);
            return;
        }

        StartOperation(
            "awake_mcm_provider_test",
            async cancellationToken =>
            {
                if (!TryResolveRuntime(out IProviderRuntimePort provider, out RequestContext context, out string resolutionError))
                {
                    return RejectedProviderOperation("provider_test", "provider_runtime_unavailable", resolutionError);
                }

                OperationResult<ProviderModelsResult> result = await ListModelsAsync(provider, context, snapshot, cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess || result.Value == null)
                {
                    return ProviderActionResult.Failed(DescribeFailure(result.Error, "provider_test", "连接测试失败"));
                }

                return ProviderActionResult.Succeeded(
                    "连接测试成功，Provider 返回 " + result.Value.Models.Count + " 个模型。",
                    result.Value.Models);
            },
            result =>
            {
                UpdateStatusOnUi(result.Message);
                UpdateModelStatusOnUi(result);
                ShowActionResult(result);
            });
    }

    internal static async Task<OperationResult<bool>> ApplyProfilesAsync(
        IProviderRuntimePort provider,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        if (provider == null)
        {
            return Failure<bool>("provider_port_missing", FrameworkErrorCategory.Unavailable, "Provider 运行端口不可用。", context);
        }

        if (context == null)
        {
            return Failure<bool>("provider_context_missing", FrameworkErrorCategory.InvalidRequest, "Provider 请求上下文不可用。", context);
        }

        if (!TryCaptureSnapshot(AwakeSettings.Current, out ProviderConfigSnapshot snapshot, out string snapshotError))
        {
            return Failure<bool>("provider_settings_invalid", FrameworkErrorCategory.InvalidRequest, snapshotError, context);
        }

        return await ApplyProfilesAsync(provider, context, snapshot, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<OperationResult<bool>> ApplyProfilesAsync(
        IProviderRuntimePort provider,
        RequestContext context,
        ProviderConfigSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (snapshot == null)
        {
            return Failure<bool>("provider_settings_invalid", FrameworkErrorCategory.InvalidRequest, "AWAKE 配置尚未加载。", context);
        }

        int appliedRoutes = 0;
        foreach (string routeId in AiTaskConstants.AllRouteIds)
        {
            ProviderProfileRequest request;
            try
            {
                request = new ProviderProfileRequest(
                    ProfileId,
                    ProviderId,
                    routeId,
                    snapshot.ProviderKind,
                    snapshot.BaseUrl,
                    snapshot.DefaultModel,
                    snapshot.IsCloud ? CredentialReference : string.Empty,
                    snapshot.IsCloud);
            }
            catch (Exception exception)
            {
                return Failure<bool>("provider_profile_invalid", FrameworkErrorCategory.InvalidRequest, exception.Message, context);
            }

            OperationResult<ProviderProfileResult> result = await provider.UpsertProfileAsync(request, context, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                if (appliedRoutes == 0) return OperationResult<bool>.Failed(result.Error);
                return PartialProfileFailure(appliedRoutes, result.Error, context);
            }

            appliedRoutes++;
        }

        return OperationResult<bool>.Succeeded(true);
    }

    internal static async Task<OperationResult<bool>> SaveCredentialAsync(
        IProviderRuntimePort provider,
        RequestContext context,
        string secret,
        CancellationToken cancellationToken)
    {
        if (provider == null)
        {
            return Failure<bool>("provider_port_missing", FrameworkErrorCategory.Unavailable, "Provider 运行端口不可用。", context);
        }

        if (context == null)
        {
            return Failure<bool>("provider_context_missing", FrameworkErrorCategory.InvalidRequest, "Provider 请求上下文不可用。", context);
        }

        if (string.IsNullOrWhiteSpace(secret))
        {
            return Failure<bool>("credential_empty", FrameworkErrorCategory.InvalidRequest, "API Key 不能为空。", context);
        }

        ProviderCredentialRequest request;
        try
        {
            request = new ProviderCredentialRequest(
                ProfileId,
                ProviderId,
                AiTaskConstants.RouteNpcDialogue,
                CredentialReference,
                secret);
        }
        catch (Exception exception)
        {
            return Failure<bool>("credential_invalid", FrameworkErrorCategory.InvalidRequest, exception.Message, context);
        }

        OperationResult<ProviderCredentialResult> result = await provider.UpsertCredentialAsync(request, context, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? OperationResult<bool>.Succeeded(true)
            : OperationResult<bool>.Failed(result.Error);
    }

    internal static async Task<OperationResult<ProviderModelsResult>> ListModelsAsync(
        IProviderRuntimePort provider,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        if (!TryCaptureSnapshot(AwakeSettings.Current, out ProviderConfigSnapshot snapshot, out string snapshotError))
        {
            return Failure<ProviderModelsResult>("provider_settings_invalid", FrameworkErrorCategory.InvalidRequest, snapshotError, context);
        }

        return await ListModelsAsync(provider, context, snapshot, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<OperationResult<ProviderModelsResult>> ListModelsAsync(
        IProviderRuntimePort provider,
        RequestContext context,
        ProviderConfigSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        OperationResult<bool> profiles = await ApplyProfilesAsync(provider, context, snapshot, cancellationToken).ConfigureAwait(false);
        if (!profiles.IsSuccess)
        {
            return OperationResult<ProviderModelsResult>.Failed(profiles.Error);
        }

        ProviderScopeRequest scope = new ProviderScopeRequest(ProfileId, ProviderId, AiTaskConstants.RouteNpcDialogue);
        return await provider.ListModelsAsync(scope, context, cancellationToken).ConfigureAwait(false);
    }

    internal static void RefreshRuntimeStatus()
    {
        StartOperation(
            "awake_mcm_runtime_health",
            async cancellationToken =>
            {
                IMarcusAwakeFrameworkHost fullHost = FrameworkHostLocator.Resolve();
                IMarcusAiFrameworkHost publicHost = fullHost as IMarcusAiFrameworkHost;
                RuntimeServiceClient runtime = fullHost?.Runtime as RuntimeServiceClient;
                if (fullHost == null || publicHost == null || runtime == null)
                {
                    return RejectedProviderOperation(
                        "runtime_health",
                        "provider_runtime_uninitialized",
                        "AWAKE Runtime 尚未初始化，请进入战役后再进行 AI 自检。");
                }

                if (runtime.Status == null || runtime.Status.State != RuntimeServiceState.Ready)
                {
                    if (runtime.Status != null
                        && runtime.Status.State == RuntimeServiceState.Stopped
                        && AwakeRuntimeRecovery.TryRequestRelaunch("mcm_runtime_health"))
                    {
                        return RejectedProviderOperation(
                            "runtime_health",
                            "provider_runtime_restarting",
                            "AWAKE Runtime 已停止，正在重新启动，请稍候再试一次。",
                            "runtime_state=Stopped");
                    }

                    return RejectedProviderOperation(
                        "runtime_health",
                        "provider_runtime_not_ready",
                        "AWAKE Runtime Service 尚未就绪，请稍候再试。",
                        "runtime_state=" + (runtime.Status == null ? "missing" : runtime.Status.State.ToString()));
                }

                RequestContext context = AwakeRuntime.CreateContext(publicHost, "awake.mcm.runtime.health." + Guid.NewGuid().ToString("N"));
                OperationResult<RuntimeServiceStatus> result = await runtime.CheckHealthAsync(context, cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess || result.Value == null)
                {
                    return ProviderActionResult.Failed(DescribeFailure(result.Error, "runtime_health", "AI 自检失败"));
                }

                return ProviderActionResult.Succeeded("AI 自检成功：AWAKE Runtime Service 已响应。", null);
            },
            result =>
            {
                UpdateStatusOnUi(result.Message);
                ShowActionResult(result);
            });
    }

    internal static void RecordAutomaticApply(OperationResult<bool> result)
    {
        if (result != null && result.IsSuccess)
        {
            SetStatus("AI 配置已随战役启动自动应用。");
        }
        else
        {
            SetStatus("AI 配置尚未应用；可在 AWAKE MCM 中点击“保存配置并应用”。");
        }

        AwakeUiDispatcher.Enqueue(() => AwakeSettings.UpdateRuntimeStatus(AwakeMarcusLinkService.BuildStatusText()));
    }

    private static void BeginCredentialSave(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            AwakeFeedback.ShowError("API Key 不能为空。");
            return;
        }

        StartOperation(
            "awake_mcm_provider_credential",
            async cancellationToken =>
            {
                if (!TryResolveRuntime(out IProviderRuntimePort provider, out RequestContext context, out string resolutionError))
                {
                    return RejectedProviderOperation("provider_credential", "provider_runtime_unavailable", resolutionError);
                }

                OperationResult<bool> result = await SaveCredentialAsync(provider, context, secret, cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    return ProviderActionResult.Failed(DescribeFailure(result.Error, "provider_credential", "API Key 保存失败"));
                }

                return ProviderActionResult.Succeeded("API Key 已写入本机保护存储。现在可以点击“保存配置并应用”或“拉取可用模型”。", null);
            },
            result =>
            {
                UpdateStatusOnUi(result.Message);
                ShowActionResult(result);
            });
    }

    private static bool TryPrepareConfiguration(out ProviderConfigSnapshot snapshot, out string error)
    {
        if (!TryCaptureSnapshot(AwakeSettings.Current, out snapshot, out error)) return false;
        if (!AwakeSettings.TrySaveCurrentConfiguration(out error)) return false;
        return true;
    }

    private static void ShowImmediateFailure(string operation, string message)
    {
        AwakeLog.Write("provider_operation_rejected operation=" + NormalizeOperation(operation)
            + " code=configuration_incomplete"
            + " details=none"
            + " message=" + (message ?? string.Empty));
        ProviderActionResult result = ProviderActionResult.Failed(message);
        UpdateStatusOnUi(result.Message);
        ShowActionResult(result);
    }

    private static void StartOperation(
        string label,
        Func<CancellationToken, Task<ProviderActionResult>> operation,
        Action<ProviderActionResult> completed)
    {
        lock (StateGate)
        {
            if (operationActive)
            {
                AwakeFeedback.ShowWarning("AI 配置操作正在进行，请稍候。");
                return;
            }

            operationActive = true;
        }

        AwakeBackgroundTask.Run(
            async () =>
            {
                ProviderActionResult result;
                try
                {
                    using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90)))
                    {
                        result = await operation(timeout.Token).ConfigureAwait(false);
                    }
                }
                catch (Exception exception)
                {
                    AwakeLog.Write("provider_mcm_operation_failed operation=" + label + " error=" + exception.Message);
                    result = ProviderActionResult.Failed("AI 配置操作失败，请检查 Runtime Service 是否已启动。");
                }
                finally
                {
                    lock (StateGate) operationActive = false;
                }

                AwakeUiDispatcher.Enqueue(() => completed(result));
            },
            label);
    }

    private static bool TryResolveRuntime(
        out IProviderRuntimePort provider,
        out RequestContext context,
        out string error)
    {
        provider = null;
        context = null;
        error = string.Empty;

        IMarcusAwakeFrameworkHost fullHost = FrameworkHostLocator.Resolve();
        IMarcusAiFrameworkHost publicHost = fullHost as IMarcusAiFrameworkHost;
        if (fullHost == null || publicHost == null)
        {
            error = "AWAKE Runtime 尚未初始化，请进入战役后再配置 AI。";
            return false;
        }

        provider = fullHost.Runtime as IProviderRuntimePort;
        if (provider == null)
        {
            error = "AWAKE Provider 运行端口不可用，请确认内置 Runtime Service 已部署。";
            return false;
        }

        if (fullHost.Runtime == null || fullHost.Runtime.Status == null || fullHost.Runtime.Status.State != RuntimeServiceState.Ready)
        {
            if (fullHost.Runtime != null
                && fullHost.Runtime.Status != null
                && fullHost.Runtime.Status.State == RuntimeServiceState.Stopped
                && AwakeRuntimeRecovery.TryRequestRelaunch("mcm_provider_gate"))
            {
                error = "AWAKE Runtime 已停止，正在重新启动，请稍候再试一次。";
                return false;
            }

            error = "AWAKE Runtime Service 尚未就绪，请稍候再试。";
            return false;
        }

        context = AwakeRuntime.CreateContext(publicHost, "awake.mcm.provider." + Guid.NewGuid().ToString("N"));
        if (context == null || context.Session == null || !context.Session.IsCampaign || context.SessionGeneration < 1)
        {
            error = "当前没有可用的战役会话，请进入已加载的存档后再配置 AI。";
            return false;
        }

        return true;
    }

    private static bool TryCaptureSnapshot(AwakeConfig config, out ProviderConfigSnapshot snapshot, out string error)
    {
        snapshot = null;
        error = string.Empty;
        if (config == null)
        {
            error = "AWAKE 配置尚未加载。";
            return false;
        }

        if (!TryNormalizeProviderBaseUrl(config.ProviderBaseUrl, out string baseUrl, out error))
        {
            return false;
        }

        string model = (config.ProviderModel ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(model) || model.Length > 256)
        {
            error = "模型名称不能为空，且不能超过 256 个字符。";
            return false;
        }

        snapshot = new ProviderConfigSnapshot(
            ResolveProviderKind(config),
            baseUrl,
            model,
            config.ProviderIsCloud);
        return true;
    }

    /// <summary>
    /// 归一化时要剥掉的尾部子路径。**全模组只此一张表**——AI 链路与生图共用，
    /// 免得两处各剥各的、算法一分叉就出现"同一条地址两边认出的 root 不一样"。
    /// </summary>
    private static readonly string[] ProviderEndpointSuffixes =
    {
        "/chat/completions",
        "/completions",
        "/models",
        "/image/generate",
        "/image/edit",
        "/images/generations",
        "/images/edits"
    };

    /// <summary>
    /// Accepts the shapes users actually paste - an API root, a versioned root, or a complete
    /// endpoint such as https://api.deepseek.com/chat/completions - and returns the API root that
    /// the runtime appends its fixed sub paths (models / chat/completions) to.
    /// </summary>
    internal static bool TryNormalizeProviderBaseUrl(string candidate, out string normalized, out string error)
    {
        normalized = string.Empty;
        error = string.Empty;

        string baseUrl = (candidate ?? string.Empty).Trim();
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            error = "服务地址必须是完整的 HTTP 或 HTTPS 地址，且不能包含账号、密码、查询参数或片段。";
            return false;
        }

        string path = uri.AbsolutePath.TrimEnd('/');
        foreach (string suffix in ProviderEndpointSuffixes)
        {
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(0, path.Length - suffix.Length).TrimEnd('/');
                break;
            }
        }

        var builder = new UriBuilder(uri)
        {
            Path = path,
            Query = string.Empty,
            Fragment = string.Empty
        };
        normalized = builder.Uri.AbsoluteUri;
        if (!StringComparer.Ordinal.Equals(baseUrl, normalized))
        {
            AwakeLog.Write("provider_base_url_normalized from=" + baseUrl + " to=" + normalized);
        }

        return true;
    }

    private static OperationResult<T> Failure<T>(
        string code,
        FrameworkErrorCategory category,
        string message,
        RequestContext context)
    {
        return OperationResult<T>.Failed(FrameworkErrors.Create(
            code,
            category,
            string.IsNullOrWhiteSpace(message) ? "Provider 配置请求失败。" : message,
            context?.CorrelationId ?? "awake-mcm-provider",
            owner: AwakeConstants.OwnerValue));
    }

    private static OperationResult<bool> PartialProfileFailure(int appliedRoutes, FrameworkError error, RequestContext context)
    {
        int totalRoutes = AiTaskConstants.AllRouteIds.Length;
        var details = new Dictionary<string, string>
        {
            ["applied_routes"] = appliedRoutes.ToString(),
            ["total_routes"] = totalRoutes.ToString(),
            ["underlying_error"] = error?.Code ?? "unknown"
        };
        return OperationResult<bool>.Failed(FrameworkErrors.Create(
            "provider_profiles_partial",
            error?.Category ?? FrameworkErrorCategory.Unavailable,
            "AI 配置只应用了 " + appliedRoutes + "/" + totalRoutes + " 条路由，请重试以完成全部应用。",
            context?.CorrelationId ?? "awake-mcm-provider",
            retryable: true,
            owner: AwakeConstants.OwnerValue,
            details: details));
    }

    private static string DescribeFailure(FrameworkError error, string operation, string prefix)
    {
        RecordProviderFailure(operation, error);
        if (error == null) return prefix + "，请检查地址、模型和 API Key。";
        if (StringComparer.Ordinal.Equals(error.Code, "provider_profiles_partial")) return error.SafeFallback;
        switch (error.Category)
        {
            case FrameworkErrorCategory.Denied:
                return prefix + "：API Key 无效或 Provider 拒绝了请求。";
            case FrameworkErrorCategory.NotFound:
                return prefix + "：服务地址或模型接口不存在。";
            case FrameworkErrorCategory.Timeout:
            case FrameworkErrorCategory.Expired:
                return prefix + "：请求超时，请检查服务是否可访问。";
            case FrameworkErrorCategory.Unavailable:
                return prefix + "：Runtime Service 或 Provider 当前不可用。";
            case FrameworkErrorCategory.Unsupported:
                return prefix + "：当前 Provider 不支持该操作。";
            default:
                return prefix + "，请检查地址、模型和 API Key。";
        }
    }

    // Structured diagnostics for MCM Provider operations. Records only identifiers, categories and
    // the framework's safe fallback text - never credentials, request bodies or model output.
    internal static void RecordProviderFailure(string operation, FrameworkError error)
    {
        AwakeLog.Write("provider_operation_failed operation=" + NormalizeOperation(operation)
            + " code=" + (error == null ? "unknown" : error.Code)
            + " category=" + (error == null ? "unknown" : error.Category.ToString())
            + " retryable=" + (error == null ? "unknown" : error.Retryable ? "true" : "false")
            + " correlation=" + (error == null ? string.Empty : error.CorrelationId)
            + " details=" + DescribeErrorDetails(error)
            + " message=" + (error == null ? string.Empty : error.SafeFallback));
    }

    private static ProviderActionResult RejectedProviderOperation(string operation, string code, string message, string details = "none")
    {
        AwakeLog.Write("provider_operation_rejected operation=" + NormalizeOperation(operation)
            + " code=" + code
            + " details=" + (string.IsNullOrWhiteSpace(details) ? "none" : details)
            + " message=" + (message ?? string.Empty));
        return ProviderActionResult.Failed(message);
    }

    private static string NormalizeOperation(string operation)
    {
        return string.IsNullOrWhiteSpace(operation) ? "unknown" : operation;
    }

    private static string DescribeErrorDetails(FrameworkError error)
    {
        if (error == null || error.Details == null || error.Details.Count == 0) return "none";
        StringBuilder builder = new StringBuilder();
        foreach (KeyValuePair<string, string> pair in error.Details)
        {
            if (string.IsNullOrWhiteSpace(pair.Value)) continue;
            if (builder.Length > 0) builder.Append(',');
            builder.Append(pair.Key).Append('=').Append(pair.Value);
        }
        return builder.Length == 0 ? "none" : builder.ToString();
    }

    private static void ShowActionResult(ProviderActionResult result)
    {
        if (result == null || string.IsNullOrWhiteSpace(result.Message)) return;
        if (result.Success) AwakeFeedback.ShowSuccess(result.Message);
        else AwakeFeedback.ShowError(result.Message);
    }

    private static void ShowModelList(IReadOnlyList<ProviderModelInfo> models)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("已读取的模型：");
        if (models == null || models.Count == 0)
        {
            builder.AppendLine("Provider 没有返回可用模型。");
        }
        else
        {
            int limit = Math.Min(models.Count, 40);
            for (int index = 0; index < limit; index++)
            {
                ProviderModelInfo model = models[index];
                builder.Append(index + 1).Append(". ").Append(model.Id);
                if (!string.IsNullOrWhiteSpace(model.DisplayName)
                    && !StringComparer.Ordinal.Equals(model.DisplayName, model.Id))
                {
                    builder.Append("（").Append(model.DisplayName).Append("）");
                }
                builder.AppendLine();
            }

            if (models.Count > limit) builder.AppendLine("……其余模型未在此窗口展开。");
            builder.AppendLine();
            builder.AppendLine("如需切换模型，请把模型 ID 填入 AWAKE MCM 的“模型名称”。");
        }

        InformationManager.ShowInquiry(
            new InquiryData(
                "可用模型",
                builder.ToString(),
                true,
                false,
                "关闭",
                string.Empty,
                null,
                null,
                string.Empty,
                0f,
                null,
                null,
                null),
            true,
            false);
    }

    private static void UpdateStatusOnUi(string value)
    {
        SetStatus(value);
        AwakeSettings.UpdateRuntimeStatus(AwakeMarcusLinkService.BuildStatusText());
    }

    private static void UpdateModelStatusOnUi(ProviderActionResult result)
    {
        string value = result == null || !result.Success
            ? "模型列表尚未获取。"
            : result.Message;
        lock (StateGate) latestModelStatus = value;
        AwakeSettings.NotifyProviderModelStatusChanged();
    }

    private static void SetStatus(string value)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? "尚未执行 AI 连接检查。" : value;
        lock (StateGate)
        {
            latestStatus = normalized;
        }
        AwakeRuntimeStatus.Update(normalized);
    }

    private sealed class ProviderConfigSnapshot
    {
        internal ProviderConfigSnapshot(string providerKind, string baseUrl, string defaultModel, bool isCloud)
        {
            ProviderKind = providerKind;
            BaseUrl = baseUrl;
            DefaultModel = defaultModel;
            IsCloud = isCloud;
        }

        internal string ProviderKind { get; }
        internal string BaseUrl { get; }
        internal string DefaultModel { get; }
        internal bool IsCloud { get; }
    }

    private sealed class ProviderActionResult
    {
        private ProviderActionResult(bool success, string message, IReadOnlyList<ProviderModelInfo> models)
        {
            Success = success;
            Message = message ?? string.Empty;
            Models = models ?? new ProviderModelInfo[0];
        }

        internal bool Success { get; }
        internal string Message { get; }
        internal IReadOnlyList<ProviderModelInfo> Models { get; }

        internal static ProviderActionResult Succeeded(string message, IReadOnlyList<ProviderModelInfo> models)
        {
            return new ProviderActionResult(true, message, models);
        }

        internal static ProviderActionResult Failed(string message)
        {
            return new ProviderActionResult(false, message, null);
        }
    }
}
