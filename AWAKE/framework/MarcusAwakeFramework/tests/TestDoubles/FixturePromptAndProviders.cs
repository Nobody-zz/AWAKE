using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;

namespace MarcusAwakeFramework.Tests.TestDoubles
{
    internal sealed class FixturePromptService : IPromptService
    {
        private readonly Dictionary<string, PromptDefinition> definitions = new Dictionary<string, PromptDefinition>(StringComparer.Ordinal);

        public Task<OperationResult<bool>> RegisterAsync(PromptDefinition definition, RequestContext context, CancellationToken cancellationToken)
        {
            if (definition == null || context == null) return Task.FromResult(OperationResult<bool>.Failed(FrameworkErrors.Create("prompt.invalid_definition", FrameworkErrorCategory.InvalidRequest, "A prompt definition and context are required.", context?.CorrelationId ?? "fixture-prompt")));
            if (cancellationToken == CancellationToken.None) return Task.FromResult(OperationResult<bool>.Failed(FrameworkErrors.Create("prompt.cancellation_token_missing", FrameworkErrorCategory.InvalidRequest, "A caller cancellation token is required.", context.CorrelationId)));
            var key = Key(definition.PromptId, definition.Version, definition.Revision);
            lock (definitions)
            {
                PromptDefinition existing;
                if (definitions.TryGetValue(key, out existing) && !StringComparer.Ordinal.Equals(existing.TemplateText, definition.TemplateText)) return Task.FromResult(OperationResult<bool>.Failed(FrameworkErrors.Create("prompt.conflict", FrameworkErrorCategory.Conflict, "The prompt revision is already registered with different content.", context.CorrelationId)));
                definitions[key] = definition;
            }
            return Task.FromResult(OperationResult<bool>.Succeeded(true));
        }

        public Task<OperationResult<PromptCompilation>> CompileAsync(PromptCompileRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            if (request == null || context == null) return Task.FromResult(OperationResult<PromptCompilation>.Failed(FrameworkErrors.Create("prompt.invalid_request", FrameworkErrorCategory.InvalidRequest, "A prompt compile request and context are required.", context?.CorrelationId ?? "fixture-prompt")));
            var definition = Find(request.PromptId, request.Version, request.Revision);
            if (definition == null) return Task.FromResult(OperationResult<PromptCompilation>.Failed(FrameworkErrors.Create("prompt.not_found", FrameworkErrorCategory.NotFound, "The prompt definition was not found.", context.CorrelationId)));
            var compiled = definition.TemplateText;
            for (var index = 0; index < definition.RequiredVariables.Count; index++)
            {
                var variable = definition.RequiredVariables[index];
                string value;
                if (!request.Variables.TryGetValue(variable, out value)) return Task.FromResult(OperationResult<PromptCompilation>.Failed(FrameworkErrors.Create("prompt.variable_missing", FrameworkErrorCategory.InvalidRequest, "A required prompt variable is missing.", context.CorrelationId)));
                compiled = compiled.Replace("{{" + variable + "}}", value ?? string.Empty);
            }
            return Task.FromResult(OperationResult<PromptCompilation>.Succeeded(new PromptCompilation(definition.PromptId, definition.Version, definition.Revision, compiled, definition.OutputContractId, definition.OutputSchemaJson)));
        }

        public Task<OperationResult<StructuredOutputResult>> ValidateOutputAsync(string outputContractId, string outputJson, RequestContext context, CancellationToken cancellationToken)
        {
            if (context == null) return Task.FromResult(OperationResult<StructuredOutputResult>.Failed(FrameworkErrors.Create("prompt.invalid_request", FrameworkErrorCategory.InvalidRequest, "A request context is required.", "fixture-output")));
            if (cancellationToken == CancellationToken.None) return Task.FromResult(OperationResult<StructuredOutputResult>.Failed(FrameworkErrors.Create("prompt.cancellation_token_missing", FrameworkErrorCategory.InvalidRequest, "A caller cancellation token is required.", context.CorrelationId)));
            var fingerprint = TaskRequestCanonicalizer.Hash("marcus-awake/raw-output/v1", outputJson ?? string.Empty);
            var parsed = TaskRequestCanonicalizer.CanonicalizeJson(outputJson ?? string.Empty, "marcus-awake/raw-output/v1", correlationId: context.CorrelationId);
            if (!parsed.IsSuccess)
            {
                var details = new Dictionary<string, string> { { "raw_output_present", string.IsNullOrEmpty(outputJson) ? "false" : "true" }, { "raw_output_bytes", Encoding.UTF8.GetByteCount(outputJson ?? string.Empty).ToString() }, { "raw_output_fingerprint", fingerprint } };
                return Task.FromResult(OperationResult<StructuredOutputResult>.Failed(FrameworkErrors.Create("prompt.output_invalid", FrameworkErrorCategory.InvalidRequest, "The structured output is invalid.", context.CorrelationId, details: details)));
            }
            return Task.FromResult(OperationResult<StructuredOutputResult>.Succeeded(new StructuredOutputResult(outputContractId, parsed.Value.CanonicalJson, fingerprint, true)));
        }

        private PromptDefinition Find(string promptId, string version, string revision)
        {
            lock (definitions)
            {
                PromptDefinition definition;
                definitions.TryGetValue(Key(promptId, version, revision), out definition);
                return definition;
            }
        }

        private static string Key(string promptId, string version, string revision) => promptId + "|" + version + "|" + revision;
    }

    internal sealed class FixtureProviderWireRequest
    {
        internal FixtureProviderWireRequest(string providerId, string bodyJson)
        {
            ProviderId = providerId;
            BodyJson = bodyJson;
        }

        internal string ProviderId { get; }
        internal string BodyJson { get; }
    }

    internal sealed class FixtureProviderTranslator
    {
        internal FixtureProviderWireRequest Translate(AiTaskRequest request, RouteProfile profile)
        {
            var input = request.InputJson;
            if (StringComparer.Ordinal.Equals(profile.ProviderId, "openai-compatible")) return new FixtureProviderWireRequest(profile.ProviderId, "{\"messages\":[{\"content\":" + input + ",\"role\":\"user\"}],\"model\":\"" + profile.ModelId + "\"}");
            if (StringComparer.Ordinal.Equals(profile.ProviderId, "anthropic")) return new FixtureProviderWireRequest(profile.ProviderId, "{\"max_tokens\":" + request.Budget.Tokens + ",\"messages\":[{\"content\":" + input + ",\"role\":\"user\"}],\"model\":\"" + profile.ModelId + "\"}");
            if (StringComparer.Ordinal.Equals(profile.ProviderId, "ollama")) return new FixtureProviderWireRequest(profile.ProviderId, "{\"model\":\"" + profile.ModelId + "\",\"prompt\":" + input + ",\"stream\":true}");
            throw new InvalidOperationException("Unsupported fixture provider.");
        }
    }

    internal sealed class FixtureProviderCapabilityCatalog
    {
        private readonly Dictionary<string, ProviderCapability> capabilities = new Dictionary<string, ProviderCapability>(StringComparer.Ordinal);

        internal FixtureProviderCapabilityCatalog()
        {
            Add(new ProviderCapability("openai-compatible", "text", true, string.Empty));
            Add(new ProviderCapability("anthropic", "text", true, string.Empty));
            Add(new ProviderCapability("ollama", "text", true, string.Empty));
            Add(new ProviderCapability("player2", "text", false, "fixture_unavailable"));
        }

        internal void Add(ProviderCapability capability) { capabilities[capability.ProviderId + "|" + capability.CapabilityId] = capability; }

        internal ProviderCapability Resolve(string providerId, string capabilityId)
        {
            ProviderCapability value;
            return capabilities.TryGetValue(providerId + "|" + capabilityId, out value) ? value : new ProviderCapability(providerId, capabilityId, false, "unknown_capability");
        }
    }
}
