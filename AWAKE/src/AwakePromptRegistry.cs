using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;

namespace Awake;

/// <summary>
/// 本地提示词登记与渲染，替代框架默认的 UnavailablePromptRegistry。
/// 登记 = 会话内记住定义；编译 = 用 NpcDialoguePromptPipeline.RenderTemplate 渲染变量。
/// 渲染结果仍由调用方套用 EnsureBudget 的 32KB 预算约束。
/// </summary>
internal sealed class AwakePromptRegistry : IPromptRegistry
{
    private readonly object _gate = new object();
    private readonly Dictionary<string, PromptDefinition> _definitions =
        new Dictionary<string, PromptDefinition>(StringComparer.Ordinal);

    public Task<OperationResult<bool>> RegisterAsync(
        PromptDefinition definition,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.PromptId))
        {
            return Task.FromResult(OperationResult<bool>.Failed(FrameworkErrors.Create(
                "prompts.invalid_definition",
                FrameworkErrorCategory.InvalidRequest,
                "A prompt definition with an id is required.",
                context?.CorrelationId ?? "prompts-register",
                owner: AwakeConstants.OwnerValue)));
        }

        lock (_gate)
        {
            _definitions[Key(definition.PromptId, definition.Version, definition.Revision)] = definition;
        }

        AwakeLog.Write("prompt_registered id=" + definition.PromptId
            + " version=" + definition.Version
            + " revision=" + definition.Revision);
        return Task.FromResult(OperationResult<bool>.Succeeded(true));
    }

    public Task<OperationResult<PromptCompilation>> CompileAsync(
        PromptCompileRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        string correlation = context?.CorrelationId ?? "prompts-compile";
        if (request == null || string.IsNullOrWhiteSpace(request.PromptId))
        {
            return Task.FromResult(OperationResult<PromptCompilation>.Failed(FrameworkErrors.Create(
                "prompts.invalid_request",
                FrameworkErrorCategory.InvalidRequest,
                "A prompt compile request with an id is required.",
                correlation,
                owner: AwakeConstants.OwnerValue)));
        }

        PromptDefinition definition;
        lock (_gate)
        {
            _definitions.TryGetValue(Key(request.PromptId, request.Version, request.Revision), out definition);
        }

        if (definition == null)
        {
            AwakeLog.Write("prompt_compile_unknown id=" + request.PromptId
                + " version=" + request.Version
                + " revision=" + request.Revision);
            return Task.FromResult(OperationResult<PromptCompilation>.Failed(FrameworkErrors.Create(
                "prompts.unknown_definition",
                FrameworkErrorCategory.Unavailable,
                "The prompt definition has not been registered.",
                correlation,
                retryable: true,
                owner: AwakeConstants.OwnerValue)));
        }

        string compiled = NpcDialoguePromptPipeline.RenderTemplate(definition.TemplateText, request.Variables);
        PromptCompilation compilation = new PromptCompilation(
            definition.PromptId,
            definition.Version,
            definition.Revision,
            compiled,
            definition.OutputContractId,
            definition.OutputSchemaJson);
        return Task.FromResult(OperationResult<PromptCompilation>.Succeeded(compilation));
    }

    private static string Key(string promptId, string version, string revision)
    {
        return (promptId ?? string.Empty) + "|" + (version ?? string.Empty) + "|" + (revision ?? string.Empty);
    }
}
