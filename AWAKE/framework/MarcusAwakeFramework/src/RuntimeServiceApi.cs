using System;
using System.Collections.Generic;

namespace MarcusAwakeFramework.Api
{
    public enum RuntimeServiceState
    {
        Created,
        Starting,
        Ready,
        Draining,
        Stopped,
        RecoveryRequired
    }

    public enum SettlementRequirement
    {
        NotApplicable,
        Required
    }

    public sealed class RuntimeServiceStartRequest
    {
        public RuntimeServiceStartRequest(string serviceId, ApiVersion protocolVersion, int maximumFrameBytes)
        {
            ServiceId = ContractGuard.Id(serviceId, nameof(serviceId));
            ProtocolVersion = protocolVersion ?? throw new ArgumentNullException(nameof(protocolVersion));
            if (maximumFrameBytes < 1024) throw new ArgumentOutOfRangeException(nameof(maximumFrameBytes));
            MaximumFrameBytes = maximumFrameBytes;
        }

        public string ServiceId { get; }
        public ApiVersion ProtocolVersion { get; }
        public int MaximumFrameBytes { get; }
    }

    public sealed class RuntimeServiceStatus
    {
        public RuntimeServiceStatus(RuntimeServiceState state, string serviceId, string instanceId, long connectionEpoch, ApiVersion protocolVersion, IReadOnlyList<string> capabilities)
        {
            State = state;
            ServiceId = ContractGuard.Id(serviceId, nameof(serviceId));
            InstanceId = instanceId ?? string.Empty;
            ConnectionEpoch = connectionEpoch;
            ProtocolVersion = protocolVersion ?? throw new ArgumentNullException(nameof(protocolVersion));
            Capabilities = capabilities ?? new string[0];
        }

        public RuntimeServiceState State { get; }
        public string ServiceId { get; }
        public string InstanceId { get; }
        public long ConnectionEpoch { get; }
        public ApiVersion ProtocolVersion { get; }
        public IReadOnlyList<string> Capabilities { get; }
        public bool IsReady => State == RuntimeServiceState.Ready;
    }

    public interface IRuntimeServicePort
    {
        RuntimeServiceStatus Status { get; }
        OperationResult<RuntimeServiceStatus> Start(RuntimeServiceStartRequest request, RequestContext context);
        OperationResult<RuntimeServiceStatus> BeginDrain(RequestContext context);
        OperationResult<RuntimeServiceStatus> CompleteDrain(RequestContext context);
    }

    public sealed class UnavailableRuntimeServicePort : IRuntimeServicePort
    {
        private readonly RuntimeServiceStatus status;

        public UnavailableRuntimeServicePort(string serviceId = "marcus-awake.runtime-service")
        {
            status = new RuntimeServiceStatus(RuntimeServiceState.Stopped, serviceId, string.Empty, 0, new ApiVersion(1, 0), new string[0]);
        }

        public RuntimeServiceStatus Status => status;

        public OperationResult<RuntimeServiceStatus> Start(RuntimeServiceStartRequest request, RequestContext context) => Failure("runtime_service_unavailable", "The Runtime Service is not connected.", context?.CorrelationId ?? "runtime-start");
        public OperationResult<RuntimeServiceStatus> BeginDrain(RequestContext context) => OperationResult<RuntimeServiceStatus>.Succeeded(status);
        public OperationResult<RuntimeServiceStatus> CompleteDrain(RequestContext context) => OperationResult<RuntimeServiceStatus>.Succeeded(status);

        private static OperationResult<RuntimeServiceStatus> Failure(string code, string fallback, string correlationId)
        {
            return OperationResult<RuntimeServiceStatus>.Failed(FrameworkErrors.Create(code, FrameworkErrorCategory.Unavailable, fallback, correlationId, retryable: true));
        }
    }

    public sealed class RuntimeResourceBudget
    {
        public const int MaximumStreamFrameCount = 125;

        public RuntimeResourceBudget(int inputBytes, int outputBytes, int tokens, int textDeltas)
            : this(MaximumStreamFrameCount, inputBytes, outputBytes, tokens, textDeltas)
        {
        }

        public RuntimeResourceBudget(int frameCount, int inputBytes, int outputBytes, int tokens, int textDeltas)
        {
            if (frameCount < 0) throw new ArgumentOutOfRangeException(nameof(frameCount));
            if (inputBytes < 0) throw new ArgumentOutOfRangeException(nameof(inputBytes));
            if (outputBytes < 0) throw new ArgumentOutOfRangeException(nameof(outputBytes));
            if (tokens < 0) throw new ArgumentOutOfRangeException(nameof(tokens));
            if (textDeltas < 0) throw new ArgumentOutOfRangeException(nameof(textDeltas));
            FrameCount = frameCount;
            InputBytes = inputBytes;
            OutputBytes = outputBytes;
            Tokens = tokens;
            TextDeltas = textDeltas;
        }

        public int FrameCount { get; }
        public int InputBytes { get; }
        public int OutputBytes { get; }
        public int Tokens { get; }
        public int TextDeltas { get; }
    }
}
