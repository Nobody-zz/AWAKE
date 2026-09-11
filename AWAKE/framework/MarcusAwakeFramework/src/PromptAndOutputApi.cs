using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeFramework.Api
{
    public sealed class PromptDefinition
    {
        public PromptDefinition(string promptId, string version, string revision, string owner, string contentType, string templateText, IReadOnlyList<string> requiredVariables, string outputContractId, string outputSchemaJson, IReadOnlyList<string> allowedToolIds, string routeId, string languageMode, bool sensitive)
        {
            PromptId = ContractGuard.Id(promptId, nameof(promptId));
            Version = ContractGuard.Id(version, nameof(version));
            Revision = ContractGuard.Id(revision, nameof(revision));
            Owner = owner ?? string.Empty;
            ContentType = ContractGuard.Id(contentType, nameof(contentType));
            TemplateText = templateText ?? string.Empty;
            RequiredVariables = requiredVariables ?? new string[0];
            OutputContractId = ContractGuard.Id(outputContractId, nameof(outputContractId));
            OutputSchemaJson = outputSchemaJson ?? "{}";
            AllowedToolIds = allowedToolIds ?? new string[0];
            RouteId = ContractGuard.Id(routeId, nameof(routeId));
            LanguageMode = ContractGuard.Id(languageMode, nameof(languageMode));
            Sensitive = sensitive;
        }

        public string PromptId { get; }
        public string Version { get; }
        public string Revision { get; }
        public string Owner { get; }
        public string ContentType { get; }
        public string TemplateText { get; }
        public IReadOnlyList<string> RequiredVariables { get; }
        public string OutputContractId { get; }
        public string OutputSchemaJson { get; }
        public IReadOnlyList<string> AllowedToolIds { get; }
        public string RouteId { get; }
        public string LanguageMode { get; }
        public bool Sensitive { get; }
    }

    public sealed class PromptCompileRequest
    {
        public PromptCompileRequest(string promptId, string version, string revision, IReadOnlyDictionary<string, string> variables)
        {
            PromptId = ContractGuard.Id(promptId, nameof(promptId));
            Version = ContractGuard.Id(version, nameof(version));
            Revision = ContractGuard.Id(revision, nameof(revision));
            Variables = variables ?? new Dictionary<string, string>();
        }

        public string PromptId { get; }
        public string Version { get; }
        public string Revision { get; }
        public IReadOnlyDictionary<string, string> Variables { get; }
    }

    public sealed class PromptCompilation
    {
        public PromptCompilation(string promptId, string version, string revision, string compiledText, string outputContractId, string outputSchemaJson)
        {
            PromptId = ContractGuard.Id(promptId, nameof(promptId));
            Version = ContractGuard.Id(version, nameof(version));
            Revision = ContractGuard.Id(revision, nameof(revision));
            CompiledText = compiledText ?? string.Empty;
            OutputContractId = ContractGuard.Id(outputContractId, nameof(outputContractId));
            OutputSchemaJson = outputSchemaJson ?? "{}";
        }

        public string PromptId { get; }
        public string Version { get; }
        public string Revision { get; }
        public string CompiledText { get; }
        public string OutputContractId { get; }
        public string OutputSchemaJson { get; }
    }

    public sealed class StructuredOutputResult
    {
        public StructuredOutputResult(string outputContractId, string payloadJson, string rawOutputFingerprint, bool valid)
        {
            OutputContractId = ContractGuard.Id(outputContractId, nameof(outputContractId));
            PayloadJson = payloadJson ?? string.Empty;
            RawOutputFingerprint = rawOutputFingerprint ?? string.Empty;
            IsValid = valid;
        }

        public string OutputContractId { get; }
        public string PayloadJson { get; }
        public string RawOutputFingerprint { get; }
        public bool IsValid { get; }
    }

    public interface IPromptService
    {
        Task<OperationResult<bool>> RegisterAsync(PromptDefinition definition, RequestContext context, CancellationToken cancellationToken);
        Task<OperationResult<PromptCompilation>> CompileAsync(PromptCompileRequest request, RequestContext context, CancellationToken cancellationToken);
        Task<OperationResult<StructuredOutputResult>> ValidateOutputAsync(string outputContractId, string outputJson, RequestContext context, CancellationToken cancellationToken);
    }
}
