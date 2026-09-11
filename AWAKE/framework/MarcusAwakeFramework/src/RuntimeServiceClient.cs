using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeTransport;

namespace MarcusAwakeFramework.Api
{
    internal enum RuntimeAiTransportMode
    {
        Unavailable,
        Stream,
        Unary
    }

    public sealed class RuntimeServiceClientOptions
    {
        private static readonly string[] DefaultCapabilities =
        {
            ProtocolConstants.MessageTypeHealth,
            ProtocolConstants.MessageTypeEcho,
            ProtocolConstants.MessageTypeCancel,
            ProtocolConstants.MessageTypeDiagnostic,
            ProtocolConstants.CapabilityProviderConfigureV1,
            ProtocolConstants.CapabilityProviderCredentialsV1,
            ProtocolConstants.CapabilityProviderModelsV1,
            ProtocolConstants.CapabilityProviderCompleteV1,
            ProtocolConstants.CapabilityProviderStreamV1
        };

        public RuntimeServiceClientOptions(
            string servicePath = null,
            string bannerlordApiVersion = "1.3.15",
            int maximumFrameBytes = ProtocolConstants.MaxFrameBytes,
            int bootstrapTimeoutMilliseconds = 5000,
            int connectTimeoutMilliseconds = 3000,
            int handshakeTimeoutMilliseconds = 3000,
            int healthTimeoutMilliseconds = 3000,
            int drainTimeoutMilliseconds = 3000,
            IReadOnlyList<string> requestedCapabilities = null)
        {
            ServicePath = string.IsNullOrWhiteSpace(servicePath)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MarcusAwakeRuntimeService.exe")
                : servicePath.Trim();
            BannerlordApiVersion = RequireText(bannerlordApiVersion, nameof(bannerlordApiVersion));
            if (maximumFrameBytes < 1024 || maximumFrameBytes > ProtocolConstants.MaxFrameBytes) throw new ArgumentOutOfRangeException(nameof(maximumFrameBytes));
            MaximumFrameBytes = maximumFrameBytes;
            BootstrapTimeoutMilliseconds = RequireTimeout(bootstrapTimeoutMilliseconds, nameof(bootstrapTimeoutMilliseconds));
            ConnectTimeoutMilliseconds = RequireTimeout(connectTimeoutMilliseconds, nameof(connectTimeoutMilliseconds));
            HandshakeTimeoutMilliseconds = RequireTimeout(handshakeTimeoutMilliseconds, nameof(handshakeTimeoutMilliseconds));
            HealthTimeoutMilliseconds = RequireTimeout(healthTimeoutMilliseconds, nameof(healthTimeoutMilliseconds));
            DrainTimeoutMilliseconds = RequireTimeout(drainTimeoutMilliseconds, nameof(drainTimeoutMilliseconds));

            var capabilities = requestedCapabilities ?? DefaultCapabilities;
            RequestedCapabilities = CopyAndValidateCapabilities(capabilities);
            if (!Contains(RequestedCapabilities, ProtocolConstants.MessageTypeHealth)) throw new ArgumentException("health_capability_required", nameof(requestedCapabilities));
        }

        public string ServicePath { get; }
        public string BannerlordApiVersion { get; }
        public int MaximumFrameBytes { get; }
        public int BootstrapTimeoutMilliseconds { get; }
        public int ConnectTimeoutMilliseconds { get; }
        public int HandshakeTimeoutMilliseconds { get; }
        public int HealthTimeoutMilliseconds { get; }
        public int DrainTimeoutMilliseconds { get; }
        public IReadOnlyList<string> RequestedCapabilities { get; }
        public string ServiceId => ProtocolConstants.ServiceId;
        public ApiVersion ProtocolVersion => new ApiVersion(ProtocolConstants.ProtocolMajor, ProtocolConstants.ProtocolMinor);

        private static string RequireText(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", name);
            return value.Trim();
        }

        private static int RequireTimeout(int value, string name)
        {
            if (value < 100 || value > 60000) throw new ArgumentOutOfRangeException(name);
            return value;
        }

        private static string[] CopyAndValidateCapabilities(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0) throw new ArgumentException("At least one capability is required.", nameof(values));
            var copy = new string[values.Count];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < values.Count; index++)
            {
                var value = RequireText(values[index], nameof(values));
                if (!seen.Add(value)) throw new ArgumentException("Duplicate capability.", nameof(values));
                copy[index] = value;
            }

            return copy;
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (var index = 0; index < values.Count; index++) if (StringComparer.Ordinal.Equals(values[index], value)) return true;
            return false;
        }
    }

    public sealed class RuntimeServiceClient : IRuntimeServicePort, IProviderRuntimePort, IAiGateway, IDisposable
    {
        private const int MaximumProviderStreamEvents = RuntimeResourceBudget.MaximumStreamFrameCount;
        private const string HealthPayloadSchema = "marcus-awake.health.v1";
        private const string HealthPayload = "{}";
        // The transport renders every payload through StrictJson, which sorts object keys by
        // StringComparer.Ordinal, so this acknowledged payload must be written in that exact order.
        // Any other key order can never match byte-for-byte and fails the health check on every call.
        private const string HealthAckPayload = "{\"ledger\":\"non_durable\",\"state\":\"ready\"}";
        private const string BootstrapAuthDomain = "marcus-awake-runtime";
        private const string FrameAuthDomain = "marcus-awake-runtime-frame";
        private const string AuthKeyId = "bootstrap-v1";

        private readonly object sync = new object();
        private readonly RuntimeServiceClientOptions options;
        private readonly string serviceId;
        private readonly ApiVersion protocolVersion;
        private RuntimeServiceState state;
        private string instanceId = string.Empty;
        private long connectionEpoch;
        private long launchEpoch;
        private IReadOnlyList<string> capabilities = new string[0];
        private Process process;
        private ClientConnection connection;
        private CancellationTokenSource lifecycleCancellation;
        private Task<OperationResult<RuntimeServiceStatus>> lastStartTask;
        private Task<OperationResult<RuntimeServiceStatus>> drainTask;
        private bool disposed;

        public RuntimeServiceClient(RuntimeServiceClientOptions options = null)
        {
            this.options = options ?? new RuntimeServiceClientOptions();
            serviceId = this.options.ServiceId;
            protocolVersion = this.options.ProtocolVersion;
            state = RuntimeServiceState.Created;
            lastStartTask = Task.FromResult(OperationResult<RuntimeServiceStatus>.Succeeded(CreateStatusLocked()));
        }

        public RuntimeServiceStatus Status
        {
            get
            {
                lock (sync) return CreateStatusLocked();
            }
        }

        public Task<OperationResult<RuntimeServiceStatus>> LastStartTask
        {
            get
            {
                lock (sync) return lastStartTask;
            }
        }

        public bool SupportsAiBusinessFrames
        {
            get
            {
                lock (sync) return state == RuntimeServiceState.Ready && (HasCapability(capabilities, ProtocolConstants.CapabilityProviderStreamV1) || HasCapability(capabilities, ProtocolConstants.CapabilityProviderCompleteV1));
            }
        }

        public bool SupportsAiStreamingFrames
        {
            get
            {
                lock (sync) return state == RuntimeServiceState.Ready && HasCapability(capabilities, ProtocolConstants.CapabilityProviderStreamV1);
            }
        }

        public OperationResult<RuntimeServiceStatus> Start(RuntimeServiceStartRequest request, RequestContext context)
        {
            return ScheduleStart(request, context, CancellationToken.None);
        }

        public async Task<OperationResult<RuntimeServiceStatus>> StartAsync(RuntimeServiceStartRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            var scheduled = ScheduleStart(request, context, cancellationToken);
            if (!scheduled.IsSuccess || scheduled.Value.State != RuntimeServiceState.Starting) return scheduled;

            Task<OperationResult<RuntimeServiceStatus>> task;
            lock (sync) task = lastStartTask;
            try
            {
                return await WaitWithCancellationAsync(task, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Failure<RuntimeServiceStatus>("runtime.cancelled", FrameworkErrorCategory.Cancelled, "The runtime service start was cancelled.", context == null ? "runtime-start" : context.CorrelationId);
            }
        }

        public async Task<OperationResult<RuntimeServiceStatus>> CheckHealthAsync(RequestContext context, CancellationToken cancellationToken)
        {
            var validation = ValidateContext(context, "runtime-health");
            if (!validation.IsSuccess) return OperationResult<RuntimeServiceStatus>.Failed(validation.Error);
            if (cancellationToken == CancellationToken.None) return Failure<RuntimeServiceStatus>("runtime.cancellation_token_missing", FrameworkErrorCategory.InvalidRequest, "A caller cancellation token is required.", context.CorrelationId);
            if (cancellationToken.IsCancellationRequested) return Failure<RuntimeServiceStatus>("runtime.cancelled", FrameworkErrorCategory.Cancelled, "The health check was cancelled.", context.CorrelationId);
            if (context.Session == null || !context.Session.IsCampaign || context.SessionGeneration < 1) return Failure<RuntimeServiceStatus>("runtime.session_fence_missing", FrameworkErrorCategory.InvalidRequest, "A complete campaign session fence is required for health checks.", context.CorrelationId);

            ClientConnection activeConnection;
            CancellationToken lifecycleToken;
            lock (sync)
            {
                if (state != RuntimeServiceState.Ready || connection == null)
                {
                    var category = state == RuntimeServiceState.RecoveryRequired ? FrameworkErrorCategory.RecoveryRequired : FrameworkErrorCategory.Unavailable;
                    var code = state == RuntimeServiceState.RecoveryRequired ? "runtime.recovery_required" : "runtime.not_ready";
                    return Failure<RuntimeServiceStatus>(code, category, "The runtime service is not ready.", context.CorrelationId);
                }

                activeConnection = connection;
                lifecycleToken = lifecycleCancellation == null ? CancellationToken.None : lifecycleCancellation.Token;
            }

            try
            {
                await activeConnection.SendHealthAsync(context, options.HealthTimeoutMilliseconds, lifecycleToken, cancellationToken).ConfigureAwait(false);
                lock (sync)
                {
                    if (!ReferenceEquals(connection, activeConnection) || state != RuntimeServiceState.Ready)
                    {
                        return Failure<RuntimeServiceStatus>("runtime.connection_changed", FrameworkErrorCategory.Expired, "The runtime service connection changed during the health check.", context.CorrelationId);
                    }

                    return OperationResult<RuntimeServiceStatus>.Succeeded(CreateStatusLocked());
                }
            }
            // A health probe is read-only. A failed probe must NOT tear the runtime down: tearing down is
            // reserved for business requests. A bad probe therefore costs one failed self-check only, and the
            // runtime keeps its process, connection and Ready state.
            catch (OperationCanceledException)
            {
                return Failure<RuntimeServiceStatus>("runtime.cancelled", FrameworkErrorCategory.Cancelled, "The health check was cancelled.", context.CorrelationId);
            }
            catch (TimeoutException)
            {
                return Failure<RuntimeServiceStatus>("runtime.health_timeout", FrameworkErrorCategory.Timeout, "The runtime service health check timed out.", context.CorrelationId);
            }
            catch (ClientProtocolException exception)
            {
                return Failure<RuntimeServiceStatus>("runtime.ipc_integrity_failed", FrameworkErrorCategory.Incompatible, "The runtime service returned an invalid response.", context.CorrelationId, exception.Code);
            }
            catch (Exception)
            {
                return Failure<RuntimeServiceStatus>("runtime.disconnected", FrameworkErrorCategory.Unavailable, "The runtime service disconnected.", context.CorrelationId);
            }
        }

        public OperationResult<RuntimeServiceStatus> BeginDrain(RequestContext context)
        {
            var validation = ValidateDrainContext(context, "runtime-drain");
            if (!validation.IsSuccess) return OperationResult<RuntimeServiceStatus>.Failed(validation.Error);

            ClientConnection activeConnection;
            Process activeProcess;
            CancellationTokenSource activeLifecycle;
            lock (sync)
            {
                if (state == RuntimeServiceState.Created || state == RuntimeServiceState.Stopped) return OperationResult<RuntimeServiceStatus>.Succeeded(CreateStatusLocked());
                if (state == RuntimeServiceState.RecoveryRequired) return Failure<RuntimeServiceStatus>("runtime.recovery_required", FrameworkErrorCategory.RecoveryRequired, "The runtime service requires recovery.", context.CorrelationId);
                if (state == RuntimeServiceState.Draining) return OperationResult<RuntimeServiceStatus>.Succeeded(CreateStatusLocked());
                if (state == RuntimeServiceState.Starting) return Failure<RuntimeServiceStatus>("runtime.starting", FrameworkErrorCategory.Conflict, "The runtime service is still starting.", context.CorrelationId);
                if (state != RuntimeServiceState.Ready || connection == null || process == null) return Failure<RuntimeServiceStatus>("runtime.invalid_drain", FrameworkErrorCategory.Conflict, "The runtime service is not ready to drain.", context.CorrelationId);

                state = RuntimeServiceState.Draining;
                activeConnection = connection;
                activeProcess = process;
                activeLifecycle = lifecycleCancellation;
                drainTask = Task.Run(() => DrainCoreAsync(activeConnection, activeProcess, activeLifecycle));
                return OperationResult<RuntimeServiceStatus>.Succeeded(CreateStatusLocked());
            }
        }

        public OperationResult<RuntimeServiceStatus> CompleteDrain(RequestContext context)
        {
            var validation = ValidateDrainContext(context, "runtime-complete-drain");
            if (!validation.IsSuccess) return OperationResult<RuntimeServiceStatus>.Failed(validation.Error);

            lock (sync)
            {
                if (state == RuntimeServiceState.RecoveryRequired) return Failure<RuntimeServiceStatus>("runtime.recovery_required", FrameworkErrorCategory.RecoveryRequired, "The runtime service requires recovery.", context.CorrelationId);
                return OperationResult<RuntimeServiceStatus>.Succeeded(CreateStatusLocked());
            }
        }

        public async Task<OperationResult<RuntimeServiceStatus>> BeginDrainAsync(RequestContext context, CancellationToken cancellationToken)
        {
            var begin = BeginDrain(context);
            if (!begin.IsSuccess && begin.Error != null && begin.Error.Code == "runtime.starting")
            {
                Task<OperationResult<RuntimeServiceStatus>> startTask;
                lock (sync) startTask = lastStartTask;
                try
                {
                    var started = await WaitWithCancellationAsync(startTask, cancellationToken).ConfigureAwait(false);
                    if (!started.IsSuccess) return started;
                }
                catch (OperationCanceledException)
                {
                    return Failure<RuntimeServiceStatus>("runtime.cancelled", FrameworkErrorCategory.Cancelled, "The runtime service drain was cancelled while waiting for startup.", context == null ? "runtime-drain" : context.CorrelationId);
                }
                begin = BeginDrain(context);
            }
            if (!begin.IsSuccess || begin.Value.State != RuntimeServiceState.Draining) return begin;
            return await AwaitDrainAsync(context, cancellationToken).ConfigureAwait(false);
        }

        public async Task<OperationResult<RuntimeServiceStatus>> CompleteDrainAsync(RequestContext context, CancellationToken cancellationToken)
        {
            var begin = await BeginDrainAsync(context, cancellationToken).ConfigureAwait(false);
            if (!begin.IsSuccess) return begin;
            if (begin.Value.State != RuntimeServiceState.Draining) return begin;
            return await AwaitDrainAsync(context, cancellationToken).ConfigureAwait(false);
        }

        public Task<OperationResult<RuntimeServiceStatus>> StopAsync(RequestContext context, CancellationToken cancellationToken)
        {
            return CompleteDrainAsync(context, cancellationToken);
        }

        public async Task<OperationResult<ProviderProfileResult>> UpsertProfileAsync(ProviderProfileRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            var correlationId = context == null ? "runtime-provider-upsert" : context.CorrelationId;
            if (request == null) return Failure<ProviderProfileResult>("runtime.invalid_request", FrameworkErrorCategory.InvalidRequest, "A Provider profile request is required.", correlationId);
            var validation = PrepareProviderCall(context, cancellationToken, ProtocolConstants.CapabilityProviderConfigureV1, "runtime-provider-upsert", out var activeConnection, out var lifecycleToken);
            if (!validation.IsSuccess) return OperationResult<ProviderProfileResult>.Failed(validation.Error);

            var payload = ProviderRuntimeWire.ProfileUpsertPayload(request);
            var scope = new ProviderScopeRequest(request.ProfileId, request.ProviderId, request.RouteId);
            var taskScope = CreateProviderTaskScope("provider-profile-upsert", scope, context, payload, ProviderRuntimeSchema.ProfileResult, "upsert");
            var envelope = activeConnection.CreateBusinessEnvelope(ProtocolConstants.MessageTypeProviderProfileUpsertV1, ProviderRuntimeSchema.ProfileUpsert, payload, taskScope, context, context.Deadline);
            return await SendProviderOperationAsync(activeConnection, envelope, context.Deadline, lifecycleToken, cancellationToken, response => ParseProfileResponse(response, scope, "upsert", correlationId)).ConfigureAwait(false);
        }

        public async Task<OperationResult<ProviderCredentialResult>> UpsertCredentialAsync(ProviderCredentialRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            var correlationId = context == null ? "runtime-provider-credential-upsert" : context.CorrelationId;
            if (request == null) return Failure<ProviderCredentialResult>("runtime.invalid_request", FrameworkErrorCategory.InvalidRequest, "A Provider credential request is required.", correlationId);
            var validation = PrepareProviderCall(context, cancellationToken, ProtocolConstants.CapabilityProviderCredentialsV1, "runtime-provider-credential-upsert", out var activeConnection, out var lifecycleToken);
            if (!validation.IsSuccess) return OperationResult<ProviderCredentialResult>.Failed(validation.Error);

            var payload = ProviderRuntimeWire.CredentialUpsertPayload(request);
            var scope = new ProviderScopeRequest(request.ProfileId, request.ProviderId, request.RouteId);
            var taskScope = CreateProviderTaskScope("provider-credential-upsert", scope, context, payload, ProviderRuntimeSchema.CredentialResult, "upsert");
            var envelope = activeConnection.CreateBusinessEnvelope(ProtocolConstants.MessageTypeProviderCredentialUpsertV1, ProviderRuntimeSchema.CredentialUpsert, payload, taskScope, context, context.Deadline);
            return await SendProviderOperationAsync(activeConnection, envelope, context.Deadline, lifecycleToken, cancellationToken, response => ParseCredentialResponse(response, request, scope, correlationId)).ConfigureAwait(false);
        }

        public async Task<OperationResult<ProviderProfileResult>> RemoveProfileAsync(ProviderScopeRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            var correlationId = context == null ? "runtime-provider-remove" : context.CorrelationId;
            if (request == null) return Failure<ProviderProfileResult>("runtime.invalid_request", FrameworkErrorCategory.InvalidRequest, "A Provider scope request is required.", correlationId);
            var validation = PrepareProviderCall(context, cancellationToken, ProtocolConstants.CapabilityProviderConfigureV1, "runtime-provider-remove", out var activeConnection, out var lifecycleToken);
            if (!validation.IsSuccess) return OperationResult<ProviderProfileResult>.Failed(validation.Error);

            var payload = ProviderRuntimeWire.ScopePayload(ProviderRuntimeSchema.ProfileRemove, request);
            var taskScope = CreateProviderTaskScope("provider-profile-remove", request, context, payload, ProviderRuntimeSchema.ProfileResult, "remove");
            var envelope = activeConnection.CreateBusinessEnvelope(ProtocolConstants.MessageTypeProviderProfileRemoveV1, ProviderRuntimeSchema.ProfileRemove, payload, taskScope, context, context.Deadline);
            return await SendProviderOperationAsync(activeConnection, envelope, context.Deadline, lifecycleToken, cancellationToken, response => ParseProfileResponse(response, request, "remove", correlationId)).ConfigureAwait(false);
        }

        public async Task<OperationResult<ProviderModelsResult>> ListModelsAsync(ProviderScopeRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            var correlationId = context == null ? "runtime-provider-models" : context.CorrelationId;
            if (request == null) return Failure<ProviderModelsResult>("runtime.invalid_request", FrameworkErrorCategory.InvalidRequest, "A Provider scope request is required.", correlationId);
            var validation = PrepareProviderCall(context, cancellationToken, ProtocolConstants.CapabilityProviderModelsV1, "runtime-provider-models", out var activeConnection, out var lifecycleToken);
            if (!validation.IsSuccess) return OperationResult<ProviderModelsResult>.Failed(validation.Error);

            var payload = ProviderRuntimeWire.ScopePayload(ProviderRuntimeSchema.Models, request);
            var taskScope = CreateProviderTaskScope("provider-models", request, context, payload, ProviderRuntimeSchema.ModelsResult, "models");
            var envelope = activeConnection.CreateBusinessEnvelope(ProtocolConstants.MessageTypeProviderModelsV1, ProviderRuntimeSchema.Models, payload, taskScope, context, context.Deadline);
            return await SendProviderOperationAsync(activeConnection, envelope, context.Deadline, lifecycleToken, cancellationToken, response => ParseModelsResponse(response, request, correlationId)).ConfigureAwait(false);
        }

        public Task<OperationResult<IAiTaskHandle>> SubmitAsync(AiTaskRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            var correlationId = context == null ? "runtime-ai-submit" : context.CorrelationId;
            if (request == null || context == null) return Task.FromResult(Failure<IAiTaskHandle>("runtime.invalid_request", FrameworkErrorCategory.InvalidRequest, "An AI task request and context are required.", correlationId));
            if (cancellationToken == CancellationToken.None) return Task.FromResult(Failure<IAiTaskHandle>("runtime.cancellation_token_missing", FrameworkErrorCategory.InvalidRequest, "A caller cancellation token is required.", correlationId));
            if (cancellationToken.IsCancellationRequested || context.CancellationToken.IsCancellationRequested) return Task.FromResult(Failure<IAiTaskHandle>("runtime.cancelled", FrameworkErrorCategory.Cancelled, "The AI task was cancelled.", correlationId));
            if (request.Deadline <= DateTimeOffset.UtcNow) return Task.FromResult(Failure<IAiTaskHandle>("runtime.deadline_expired", FrameworkErrorCategory.Expired, "The AI task deadline has expired.", correlationId));

            var validation = PrepareAiProviderCall(context, cancellationToken, out var activeConnection, out var lifecycleToken, out var useStream);
            if (!validation.IsSuccess) return Task.FromResult(OperationResult<IAiTaskHandle>.Failed(validation.Error));

            RuntimeResourceBudget streamBudget = null;
            OperationResult<string> payloadResult;
            if (useStream)
            {
                var budgetResult = ProjectStreamBudget(request.Budget, activeConnection.StreamBudget, correlationId);
                if (!budgetResult.IsSuccess) return Task.FromResult(OperationResult<IAiTaskHandle>.Failed(budgetResult.Error));
                streamBudget = budgetResult.Value;
                payloadResult = ProviderRuntimeWire.StreamPayload(request, streamBudget, correlationId);
            }
            else
            {
                payloadResult = ProviderRuntimeWire.CompletionPayload(request, correlationId);
            }
            if (!payloadResult.IsSuccess) return Task.FromResult(OperationResult<IAiTaskHandle>.Failed(payloadResult.Error));
            var payload = payloadResult.Value;
            var deadline = request.Deadline < context.Deadline ? request.Deadline : context.Deadline;
            var taskScope = CreateCompletionTaskScope(request, context, payload);
            var envelope = activeConnection.CreateBusinessEnvelope(useStream ? ProtocolConstants.MessageTypeProviderStreamV1 : ProtocolConstants.MessageTypeProviderCompleteV1, useStream ? ProviderRuntimeSchema.Stream : ProviderRuntimeSchema.Complete, payload, taskScope, context, deadline);
            var taskCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, context.CancellationToken);
            var cancelDelegate = new Func<CancellationToken, Task<OperationResult<bool>>>(cancelToken => CancelTaskAsync(activeConnection, taskScope, context, deadline, cancelToken));
            var handle = new AiTaskHandle(request.TaskId, request.MessageId, AiTaskHandle.MaximumSnapshotEvents, cancelDelegate, taskCancellation.Cancel);
            var accepted = handle.Publish(new AiTaskEvent(request.TaskId, request.MessageId, AiTaskEventKind.Accepted, 1, string.Empty, null, string.Empty, 0, 0));
            if (!accepted.IsSuccess)
            {
                taskCancellation.Dispose();
                return Task.FromResult(Failure<IAiTaskHandle>(accepted.Error.Code, accepted.Error.Category, accepted.Error.SafeFallback, correlationId));
            }
            var started = handle.Publish(new AiTaskEvent(request.TaskId, request.MessageId, AiTaskEventKind.Started, 2, string.Empty, null, string.Empty, 0, 0, string.Empty, request.ProviderId, string.Empty, string.Empty));
            if (!started.IsSuccess)
            {
                taskCancellation.Dispose();
                return Task.FromResult(Failure<IAiTaskHandle>(started.Error.Code, started.Error.Category, started.Error.SafeFallback, correlationId));
            }

            _ = RunCompletionAsync(handle, request, envelope, activeConnection, deadline, lifecycleToken, taskCancellation, correlationId, useStream, streamBudget);
            return Task.FromResult(OperationResult<IAiTaskHandle>.Succeeded(handle));
        }

        public Task<OperationResult<AiTaskReceipt>> GetReceiptAsync(AiTaskScope scope, RequestContext context, CancellationToken cancellationToken)
        {
            var correlationId = context == null ? "runtime-ai-receipt" : context.CorrelationId;
            if (scope == null || context == null) return Task.FromResult(Failure<AiTaskReceipt>("runtime.invalid_request", FrameworkErrorCategory.InvalidRequest, "An AI task scope and context are required.", correlationId));
            if (cancellationToken == CancellationToken.None) return Task.FromResult(Failure<AiTaskReceipt>("runtime.cancellation_token_missing", FrameworkErrorCategory.InvalidRequest, "A caller cancellation token is required.", correlationId));
            if (cancellationToken.IsCancellationRequested || context.CancellationToken.IsCancellationRequested) return Task.FromResult(Failure<AiTaskReceipt>("runtime.cancelled", FrameworkErrorCategory.Cancelled, "The AI receipt request was cancelled.", correlationId));
            return Task.FromResult(Failure<AiTaskReceipt>("runtime.receipt_unavailable", FrameworkErrorCategory.Unavailable, "Durable AI receipts are not enabled in the current unary Provider bridge.", correlationId));
        }

        public void Dispose()
        {
            Process activeProcess;
            ClientConnection activeConnection;
            CancellationTokenSource activeLifecycle;
            Task startTask;
            Task stopTask;
            lock (sync)
            {
                if (disposed) return;
                disposed = true;
                state = RuntimeServiceState.Draining;
                activeProcess = process;
                activeConnection = connection;
                activeLifecycle = lifecycleCancellation;
                startTask = lastStartTask;
                stopTask = drainTask;
                process = null;
                connection = null;
                lifecycleCancellation = null;
            }

            try { if (activeLifecycle != null) activeLifecycle.Cancel(); } catch (ObjectDisposedException) { }
            if (activeConnection != null) activeConnection.Abort();
            TryKill(activeProcess);
            WaitForLifecycleTask(startTask);
            WaitForLifecycleTask(stopTask);
            DisposeCancellation(activeLifecycle);
            lock (sync) state = RuntimeServiceState.Stopped;
        }

        private OperationResult<RuntimeServiceStatus> ScheduleStart(RuntimeServiceStartRequest request, RequestContext context, CancellationToken cancellationToken)
        {
            var validation = ValidateStartRequest(request, context);
            if (!validation.IsSuccess) return OperationResult<RuntimeServiceStatus>.Failed(validation.Error);
            if (cancellationToken.IsCancellationRequested) return Failure<RuntimeServiceStatus>("runtime.cancelled", FrameworkErrorCategory.Cancelled, "The runtime service start was cancelled.", context.CorrelationId);

            lock (sync)
            {
                if (disposed) return Failure<RuntimeServiceStatus>("runtime.disposed", FrameworkErrorCategory.Unavailable, "The runtime service client has been disposed.", context.CorrelationId);
                if (state == RuntimeServiceState.Ready) return OperationResult<RuntimeServiceStatus>.Succeeded(CreateStatusLocked());
                if (state == RuntimeServiceState.Starting) return OperationResult<RuntimeServiceStatus>.Succeeded(CreateStatusLocked());
                if (state == RuntimeServiceState.Draining) return Failure<RuntimeServiceStatus>("runtime.draining", FrameworkErrorCategory.Conflict, "The runtime service is draining.", context.CorrelationId);
                if (state == RuntimeServiceState.RecoveryRequired) return Failure<RuntimeServiceStatus>("runtime.recovery_required", FrameworkErrorCategory.RecoveryRequired, "The runtime service requires recovery.", context.CorrelationId);

                state = RuntimeServiceState.Starting;
                launchEpoch++;
                connectionEpoch = 0;
                instanceId = string.Empty;
                capabilities = new string[0];
                drainTask = null;

                var lifecycle = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, CancellationToken.None);
                var startup = CancellationTokenSource.CreateLinkedTokenSource(lifecycle.Token, cancellationToken);
                lifecycleCancellation = lifecycle;
                var currentLaunchEpoch = launchEpoch;
                lastStartTask = Task.Run(() => StartCoreAsync(request, context, currentLaunchEpoch, lifecycle, startup));
                return OperationResult<RuntimeServiceStatus>.Succeeded(CreateStatusLocked());
            }
        }

        private OperationResult<bool> ValidateStartRequest(RuntimeServiceStartRequest request, RequestContext context)
        {
            var validation = ValidateContext(context, "runtime-start");
            if (!validation.IsSuccess) return validation;
            if (request == null) return Failure<bool>("runtime.invalid_request", FrameworkErrorCategory.InvalidRequest, "A runtime service start request is required.", context.CorrelationId);
            if (!StringComparer.Ordinal.Equals(serviceId, request.ServiceId)) return Failure<bool>("runtime.service_mismatch", FrameworkErrorCategory.Incompatible, "The runtime service identifier does not match.", context.CorrelationId);
            if (!request.ProtocolVersion.Equals(protocolVersion)) return Failure<bool>("runtime.protocol_mismatch", FrameworkErrorCategory.Incompatible, "The runtime service protocol is incompatible.", context.CorrelationId);
            if (request.MaximumFrameBytes < 1024 || request.MaximumFrameBytes > ProtocolConstants.MaxFrameBytes) return Failure<bool>("runtime.frame_limit_invalid", FrameworkErrorCategory.InvalidRequest, "The requested frame limit is invalid.", context.CorrelationId);
            if (context.Session == null || string.IsNullOrWhiteSpace(context.Session.SessionId)) return Failure<bool>("runtime.session_missing", FrameworkErrorCategory.InvalidRequest, "A session identifier is required to start the runtime service.", context.CorrelationId);
            return OperationResult<bool>.Succeeded(true);
        }

        private OperationResult<bool> ValidateContext(RequestContext context, string operation)
        {
            if (context == null) return Failure<bool>("runtime.context_missing", FrameworkErrorCategory.InvalidRequest, "A request context is required.", operation);
            if (context.CancellationToken == CancellationToken.None) return Failure<bool>("runtime.session_token_missing", FrameworkErrorCategory.InvalidRequest, "A session-owned cancellation token is required.", context.CorrelationId);
            if (context.SessionLease == null || !context.SessionLease.MatchesReady(context)) return Failure<bool>("runtime.session_stale", FrameworkErrorCategory.Expired, "The request belongs to an inactive session.", context.CorrelationId);
            if (context.IsExpiredAt(DateTimeOffset.UtcNow)) return Failure<bool>("runtime.deadline_expired", FrameworkErrorCategory.Expired, "The request deadline has expired.", context.CorrelationId);
            return OperationResult<bool>.Succeeded(true);
        }

        private OperationResult<bool> ValidateDrainContext(RequestContext context, string operation)
        {
            if (context == null) return Failure<bool>("runtime.context_missing", FrameworkErrorCategory.InvalidRequest, "A request context is required.", operation);
            if (context.CancellationToken == CancellationToken.None) return Failure<bool>("runtime.session_token_missing", FrameworkErrorCategory.InvalidRequest, "A session-owned cancellation token is required.", context.CorrelationId);
            if (!context.SessionLease.MatchesClosing(context) && !context.SessionLease.MatchesReady(context)) return Failure<bool>("runtime.session_stale", FrameworkErrorCategory.Expired, "The request belongs to an inactive session.", context.CorrelationId);
            if (context.IsExpiredAt(DateTimeOffset.UtcNow)) return Failure<bool>("runtime.deadline_expired", FrameworkErrorCategory.Expired, "The runtime service request deadline has expired.", context.CorrelationId);
            return OperationResult<bool>.Succeeded(true);
        }

        private static void WaitForLifecycleTask(Task task)
        {
            if (task == null || task.IsCompleted) return;
            try { task.GetAwaiter().GetResult(); }
            catch { }
        }

        private OperationResult<bool> PrepareProviderCall(RequestContext context, CancellationToken cancellationToken, string requiredCapability, string operation, out ClientConnection activeConnection, out CancellationToken lifecycleToken)
        {
            activeConnection = null;
            lifecycleToken = CancellationToken.None;
            var validation = ValidateContext(context, operation);
            if (!validation.IsSuccess) return validation;
            if (cancellationToken == CancellationToken.None) return Failure<bool>("runtime.cancellation_token_missing", FrameworkErrorCategory.InvalidRequest, "A caller cancellation token is required.", context.CorrelationId);
            if (cancellationToken.IsCancellationRequested || context.CancellationToken.IsCancellationRequested) return Failure<bool>("runtime.cancelled", FrameworkErrorCategory.Cancelled, "The Provider request was cancelled.", context.CorrelationId);

            lock (sync)
            {
                if (state != RuntimeServiceState.Ready || connection == null)
                {
                    var category = state == RuntimeServiceState.RecoveryRequired ? FrameworkErrorCategory.RecoveryRequired : FrameworkErrorCategory.Unavailable;
                    var code = state == RuntimeServiceState.RecoveryRequired ? "runtime.recovery_required" : "runtime.not_ready";
                    return Failure<bool>(code, category, "The runtime service is not ready.", context.CorrelationId);
                }

                if (!HasCapability(capabilities, requiredCapability)) return Failure<bool>("runtime.capability_unavailable", FrameworkErrorCategory.Unsupported, "The runtime service did not negotiate the requested Provider capability.", context.CorrelationId);
                activeConnection = connection;
                lifecycleToken = lifecycleCancellation == null ? CancellationToken.None : lifecycleCancellation.Token;
            }

            return OperationResult<bool>.Succeeded(true);
        }

        private OperationResult<bool> PrepareAiProviderCall(RequestContext context, CancellationToken cancellationToken, out ClientConnection activeConnection, out CancellationToken lifecycleToken, out bool useStream)
        {
            activeConnection = null;
            lifecycleToken = CancellationToken.None;
            useStream = false;
            var validation = ValidateContext(context, "runtime-ai-submit");
            if (!validation.IsSuccess) return validation;
            if (cancellationToken == CancellationToken.None) return Failure<bool>("runtime.cancellation_token_missing", FrameworkErrorCategory.InvalidRequest, "A caller cancellation token is required.", context.CorrelationId);
            if (cancellationToken.IsCancellationRequested || context.CancellationToken.IsCancellationRequested) return Failure<bool>("runtime.cancelled", FrameworkErrorCategory.Cancelled, "The Provider request was cancelled.", context.CorrelationId);

            lock (sync)
            {
                if (state != RuntimeServiceState.Ready || connection == null)
                {
                    var category = state == RuntimeServiceState.RecoveryRequired ? FrameworkErrorCategory.RecoveryRequired : FrameworkErrorCategory.Unavailable;
                    var code = state == RuntimeServiceState.RecoveryRequired ? "runtime.recovery_required" : "runtime.not_ready";
                    return Failure<bool>(code, category, "The runtime service is not ready.", context.CorrelationId);
                }

                var transportMode = SelectAiTransport(capabilities);
                if (transportMode == RuntimeAiTransportMode.Unavailable) return Failure<bool>("stream_capability_unavailable", FrameworkErrorCategory.Unsupported, "The runtime service did not negotiate a stream or unary Provider capability.", context.CorrelationId, "complete_capability_unavailable");
                useStream = transportMode == RuntimeAiTransportMode.Stream;
                activeConnection = connection;
                lifecycleToken = lifecycleCancellation == null ? CancellationToken.None : lifecycleCancellation.Token;
            }

            return OperationResult<bool>.Succeeded(true);
        }

        internal static OperationResult<RuntimeResourceBudget> ProjectStreamBudget(RuntimeResourceBudget requested, ProviderStreamBudgetDescriptor negotiated, string correlationId = "runtime-ai-submit")
        {
            if (requested == null) return Failure<RuntimeResourceBudget>("stream_budget_invalid", FrameworkErrorCategory.InvalidRequest, "The stream resource budget is required.", correlationId);
            negotiated = negotiated ?? ProviderStreamBudgetDescriptor.Default();
            var frameCount = Math.Min(Math.Min(Math.Min(MaximumProviderStreamEvents, ProtocolConstants.MaxProviderStreamEvents), negotiated.FrameCount), requested.FrameCount);
            var outputBytes = Math.Min(Math.Min(Math.Min(RuntimeService.MaximumOutputBytes, ProtocolConstants.MaxProviderStreamOutputBytes), negotiated.OutputBytes), requested.OutputBytes);
            var tokens = Math.Min(Math.Min(Math.Min(RuntimeService.MaximumTokens, ProtocolConstants.MaxProviderStreamTokens), negotiated.Tokens), requested.Tokens);
            var textDeltas = Math.Min(Math.Min(Math.Min(RuntimeService.MaximumTextDeltas, ProtocolConstants.MaxProviderStreamTextDeltas), negotiated.TextDeltas), requested.TextDeltas);
            if (frameCount < 2 || outputBytes < 1 || tokens < 1 || textDeltas < 1) return Failure<RuntimeResourceBudget>("stream_budget_invalid", FrameworkErrorCategory.InvalidRequest, "The effective stream resource budget is invalid.", correlationId);
            return OperationResult<RuntimeResourceBudget>.Succeeded(new RuntimeResourceBudget(frameCount, requested.InputBytes, outputBytes, tokens, textDeltas));
        }

        private static bool HasCapability(IReadOnlyList<string> values, string expected)
        {
            if (values == null || string.IsNullOrWhiteSpace(expected)) return false;
            for (var index = 0; index < values.Count; index++) if (StringComparer.Ordinal.Equals(values[index], expected)) return true;
            return false;
        }

        internal static RuntimeAiTransportMode SelectAiTransport(IReadOnlyList<string> values)
        {
            if (HasCapability(values, ProtocolConstants.CapabilityProviderStreamV1)) return RuntimeAiTransportMode.Stream;
            if (HasCapability(values, ProtocolConstants.CapabilityProviderCompleteV1)) return RuntimeAiTransportMode.Unary;
            return RuntimeAiTransportMode.Unavailable;
        }

        private static TaskScopeEnvelope CreateProviderTaskScope(string operation, ProviderScopeRequest request, RequestContext context, string payload, string outputSchemaId, string idempotencySuffix)
        {
            var messageId = operation + "-" + Guid.NewGuid().ToString("N");
            return new TaskScopeEnvelope
            {
                TaskId = messageId + "-task",
                MessageId = messageId,
                OwnerId = context.Caller.Value,
                RouteId = request.RouteId,
                ProviderId = request.ProviderId,
                ProfileId = request.ProfileId,
                IdempotencyKey = operation + "-" + idempotencySuffix + "-" + Guid.NewGuid().ToString("N"),
                RequestPayloadHash = TransportSecurity.Sha256Hex(Encoding.UTF8.GetBytes(CanonicalizePayload(payload))),
                OutputSchemaId = outputSchemaId,
                OutputSchemaMajor = 1,
                OutputSchemaMinor = 0,
                SettlementRequirement = "not_applicable"
            };
        }

        private static TaskScopeEnvelope CreateCompletionTaskScope(AiTaskRequest request, RequestContext context, string payload)
        {
            return new TaskScopeEnvelope
            {
                TaskId = request.TaskId,
                MessageId = request.MessageId,
                OwnerId = context.Caller.Value,
                RouteId = request.RouteId,
                ProviderId = request.ProviderId,
                ProfileId = request.ProfileId,
                IdempotencyKey = request.IdempotencyKey,
                RequestPayloadHash = TransportSecurity.Sha256Hex(Encoding.UTF8.GetBytes(CanonicalizePayload(payload))),
                OutputSchemaId = request.OutputSchema.SchemaId,
                OutputSchemaMajor = request.OutputSchema.Major,
                OutputSchemaMinor = request.OutputSchema.Minor,
                SettlementRequirement = request.SettlementRequirement == SettlementRequirement.Required ? "required" : "not_applicable"
            };
        }

        private async Task<OperationResult<bool>> CancelTaskAsync(ClientConnection activeConnection, TaskScopeEnvelope taskScope, RequestContext context, DateTimeOffset deadline, CancellationToken cancellationToken)
        {
            try
            {
                return await activeConnection.SendControlCancelAsync(taskScope, context, deadline, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Failure<bool>("runtime.cancelled", FrameworkErrorCategory.Cancelled, "The cancellation was cancelled.", context.CorrelationId);
            }
            catch (Exception exception)
            {
                return Failure<bool>("runtime.cancel_unavailable", FrameworkErrorCategory.Unavailable, "The runtime service could not process the cancellation request.", context.CorrelationId, exception.GetType().Name);
            }
        }

        private async Task<OperationResult<T>> SendProviderOperationAsync<T>(ClientConnection activeConnection, PipeEnvelope envelope, DateTimeOffset deadline, CancellationToken lifecycleToken, CancellationToken callerToken, Func<PipeEnvelope, OperationResult<T>> parser)
        {
            try
            {
                var response = await activeConnection.SendBusinessAsync(envelope, deadline, lifecycleToken, callerToken).ConfigureAwait(false);
                return parser(response);
            }
            catch (OperationCanceledException)
            {
                await FailBusinessConnectionAsync(activeConnection).ConfigureAwait(false);
                return Failure<T>("runtime.cancelled", FrameworkErrorCategory.Cancelled, "The Provider request was cancelled.", envelope.CorrelationId);
            }
            catch (TimeoutException)
            {
                await FailBusinessConnectionAsync(activeConnection).ConfigureAwait(false);
                return Failure<T>("runtime.provider_timeout", FrameworkErrorCategory.Timeout, "The Provider request timed out.", envelope.CorrelationId);
            }
            catch (ClientProtocolException exception)
            {
                await FailBusinessConnectionAsync(activeConnection).ConfigureAwait(false);
                return Failure<T>("runtime.ipc_integrity_failed", FrameworkErrorCategory.Incompatible, "The runtime service returned an invalid Provider response.", envelope.CorrelationId, exception.Code);
            }
            catch (IOException)
            {
                await FailBusinessConnectionAsync(activeConnection).ConfigureAwait(false);
                return Failure<T>("runtime.disconnected", FrameworkErrorCategory.Unavailable, "The runtime service disconnected during the Provider request.", envelope.CorrelationId);
            }
            catch (Exception exception)
            {
                await FailBusinessConnectionAsync(activeConnection).ConfigureAwait(false);
                return Failure<T>("runtime.provider_call_failed", FrameworkErrorCategory.InternalFailure, "The Provider request could not be completed.", envelope.CorrelationId, exception.GetType().Name);
            }
        }

        private async Task FailBusinessConnectionAsync(ClientConnection activeConnection)
        {
            try { await FailConnectionAsync(activeConnection).ConfigureAwait(false); } catch (Exception) { }
        }

        private static OperationResult<ProviderProfileResult> ParseProfileResponse(PipeEnvelope response, ProviderScopeRequest scope, string operation, string correlationId)
        {
            if (response != null && StringComparer.Ordinal.Equals(response.MessageType, ProtocolConstants.MessageTypeError)) return ParseProviderError<ProviderProfileResult>(response, correlationId, scope);
            if (response == null || !StringComparer.Ordinal.Equals(response.MessageType, ProtocolConstants.MessageTypeProviderProfileResult) || !StringComparer.Ordinal.Equals(response.PayloadSchema, ProviderRuntimeSchema.ProfileResult)) throw new ClientProtocolException("provider_profile_response_identity_invalid");
            if (!ProviderRuntimeWire.TryReadProfileResult(response.PayloadJson, scope, operation, out var result, out var error)) throw new ClientProtocolException("provider_profile_response_" + error);
            return OperationResult<ProviderProfileResult>.Succeeded(result);
        }

        private static OperationResult<ProviderCredentialResult> ParseCredentialResponse(PipeEnvelope response, ProviderCredentialRequest request, ProviderScopeRequest scope, string correlationId)
        {
            if (response != null && StringComparer.Ordinal.Equals(response.MessageType, ProtocolConstants.MessageTypeError)) return ParseProviderError<ProviderCredentialResult>(response, correlationId, scope);
            if (response == null || !StringComparer.Ordinal.Equals(response.MessageType, ProtocolConstants.MessageTypeProviderCredentialResult) || !StringComparer.Ordinal.Equals(response.PayloadSchema, ProviderRuntimeSchema.CredentialResult)) throw new ClientProtocolException("provider_credential_response_identity_invalid");
            if (!ProviderRuntimeWire.TryReadCredentialResult(response.PayloadJson, request, out var result, out var error)) throw new ClientProtocolException("provider_credential_response_" + error);
            return OperationResult<ProviderCredentialResult>.Succeeded(result);
        }

        private static OperationResult<ProviderModelsResult> ParseModelsResponse(PipeEnvelope response, ProviderScopeRequest scope, string correlationId)
        {
            if (response != null && StringComparer.Ordinal.Equals(response.MessageType, ProtocolConstants.MessageTypeError)) return ParseProviderError<ProviderModelsResult>(response, correlationId, scope);
            if (response == null || !StringComparer.Ordinal.Equals(response.MessageType, ProtocolConstants.MessageTypeProviderModelsResult) || !StringComparer.Ordinal.Equals(response.PayloadSchema, ProviderRuntimeSchema.ModelsResult)) throw new ClientProtocolException("provider_models_response_identity_invalid");
            if (!ProviderRuntimeWire.TryReadModelsResult(response.PayloadJson, scope, out var result, out var error)) throw new ClientProtocolException("provider_models_response_" + error);
            return OperationResult<ProviderModelsResult>.Succeeded(result);
        }

        private static OperationResult<CompletionResultWire> ParseCompletionResponse(PipeEnvelope response, AiTaskRequest request, string correlationId)
        {
            var scope = new ProviderScopeRequest(request.ProfileId, request.ProviderId, request.RouteId);
            if (response != null && StringComparer.Ordinal.Equals(response.MessageType, ProtocolConstants.MessageTypeError)) return ParseProviderError<CompletionResultWire>(response, correlationId, scope);
            if (response == null || !StringComparer.Ordinal.Equals(response.MessageType, ProtocolConstants.MessageTypeProviderResult) || !StringComparer.Ordinal.Equals(response.PayloadSchema, ProviderRuntimeSchema.Result)) throw new ClientProtocolException("provider_completion_response_identity_invalid");
            if (!ProviderRuntimeWire.TryReadCompletionResult(response.PayloadJson, request, out var result, out var error)) throw new ClientProtocolException("provider_completion_response_" + error);
            return OperationResult<CompletionResultWire>.Succeeded(result);
        }

        private static OperationResult<T> ParseProviderError<T>(PipeEnvelope response, string correlationId, ProviderScopeRequest scope)
        {
            if (response == null || !ProviderRuntimeWire.TryReadError(response.PayloadJson, out var error)) throw new ClientProtocolException("provider_error_decode_failed");
            return OperationResult<T>.Failed(CreateProviderFrameworkError(error, correlationId, scope));
        }

        private static FrameworkError CreateProviderFrameworkError(ProviderWireError error, string correlationId, ProviderScopeRequest scope)
        {
            var providerId = string.IsNullOrWhiteSpace(error.ProviderId) ? scope.ProviderId : error.ProviderId;
            var profileId = string.IsNullOrWhiteSpace(error.ProfileId) ? scope.ProfileId : error.ProfileId;
            var routeId = string.IsNullOrWhiteSpace(error.RouteId) ? scope.RouteId : error.RouteId;
            var mapping = FrameworkErrors.MapProviderError(ToFrameworkProviderCategory(error.Category), providerId, correlationId);
            var details = new Dictionary<string, string>
            {
                { "provider_id", providerId },
                { "profile_id", profileId },
                { "route_id", routeId }
            };
            if (error.StatusCode.HasValue) details["status_code"] = error.StatusCode.Value.ToString();
            var fallback = string.IsNullOrWhiteSpace(error.SafeMessage) ? "The Provider request failed." : error.SafeMessage;
            return FrameworkErrors.Create("provider." + NormalizeProviderErrorCode(error.Code), mapping.CoreCategory, fallback, correlationId, error.Retryable || mapping.Retryable, "MarcusAwakeRuntimeService", details);
        }

        private static string ToFrameworkProviderCategory(string value)
        {
            switch (value)
            {
                case "invalid_request": return "InvalidRequest";
                case "authentication": return "Authentication";
                case "forbidden": return "Forbidden";
                case "not_found": return "NotFound";
                case "conflict": return "Conflict";
                case "rate_limited": return "RateLimited";
                case "timeout": return "Timeout";
                case "unavailable": return "Unavailable";
                case "server_unavailable": return "ServerUnavailable";
                case "transport_unavailable": return "TransportUnavailable";
                case "redirect_rejected": return "RedirectRejected";
                case "policy_denied": return "PolicyDenied";
                case "malformed_response": return "MalformedResponse";
                case "incomplete_stream": return "IncompleteStream";
                case "cancelled": return "Cancelled";
                case "unsupported": return "Unsupported";
                case "resource_exhausted": return "ResourceExhausted";
                case "corrupt_credential": return "CorruptCredential";
                case "internal_failure": return "InternalFailure";
                default: return "InternalFailure";
            }
        }

        private static string NormalizeProviderErrorCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "request_failed";
            var normalized = value.Trim();
            if (normalized.Length > 128) return "request_failed";
            for (var index = 0; index < normalized.Length; index++)
            {
                var character = normalized[index];
                if (!((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9') || character == '_' || character == '.' || character == '-')) return "request_failed";
            }
            return normalized;
        }

        private async Task RunCompletionAsync(AiTaskHandle handle, AiTaskRequest request, PipeEnvelope envelope, ClientConnection activeConnection, DateTimeOffset deadline, CancellationToken lifecycleToken, CancellationTokenSource taskCancellation, string correlationId, bool useStream, RuntimeResourceBudget streamBudget)
        {
            try
            {
                if (useStream)
                {
                    var streamEvents = await activeConnection.SendBusinessStreamAsync(envelope, deadline, lifecycleToken, taskCancellation.Token, streamBudget).ConfigureAwait(false);
                    var projectedStream = ProjectStreamEvents(handle, request, streamEvents, correlationId);
                    if (!projectedStream.IsSuccess)
                    {
                        await FailBusinessConnectionAsync(activeConnection).ConfigureAwait(false);
                        PublishFailure(handle, request, projectedStream.Error);
                    }
                    return;
                }

                var response = await activeConnection.SendBusinessAsync(envelope, deadline, lifecycleToken, taskCancellation.Token).ConfigureAwait(false);
                var parsed = ParseCompletionResponse(response, request, correlationId);
                if (!parsed.IsSuccess)
                {
                    if (parsed.Error != null && parsed.Error.Category == FrameworkErrorCategory.Cancelled) PublishCancellation(handle, request);
                    else PublishFailure(handle, request, parsed.Error);
                    return;
                }

                var projectedUnary = ProjectUnaryCompletion(handle, request, parsed.Value, correlationId);
                if (!projectedUnary.IsSuccess)
                {
                    PublishFailure(handle, request, projectedUnary.Error);
                }
            }
            catch (ProviderResponseException exception)
            {
                await FailBusinessConnectionAsync(activeConnection).ConfigureAwait(false);
                var providerError = exception.Error;
                var frameworkError = providerError == null
                    ? FrameworkErrors.Create("runtime.provider_call_failed", FrameworkErrorCategory.ProviderFailure, "The Provider request failed.", correlationId)
                    : CreateProviderFrameworkError(providerError, correlationId, new ProviderScopeRequest(request.ProfileId, request.ProviderId, request.RouteId));
                PublishFailure(handle, request, frameworkError);
            }
            catch (OperationCanceledException)
            {
                await FailBusinessConnectionAsync(activeConnection).ConfigureAwait(false);
                PublishCancellation(handle, request);
            }
            catch (TimeoutException)
            {
                await FailBusinessConnectionAsync(activeConnection).ConfigureAwait(false);
                PublishFailure(handle, request, FrameworkErrors.Create("runtime.provider_timeout", FrameworkErrorCategory.Timeout, "The Provider request timed out.", correlationId));
            }
            catch (ClientProtocolException exception)
            {
                await FailBusinessConnectionAsync(activeConnection).ConfigureAwait(false);
                PublishFailure(handle, request, FrameworkErrors.Create("runtime.ipc_integrity_failed", FrameworkErrorCategory.Incompatible, "The runtime service returned an invalid Provider response.", correlationId, details: new Dictionary<string, string> { { "protocol_detail", exception.Code } }));
            }
            catch (IOException)
            {
                await FailBusinessConnectionAsync(activeConnection).ConfigureAwait(false);
                PublishFailure(handle, request, FrameworkErrors.Create("runtime.disconnected", FrameworkErrorCategory.Unavailable, "The runtime service disconnected during the Provider request.", correlationId, retryable: true));
            }
            catch (Exception exception)
            {
                if (StringComparer.Ordinal.Equals(Environment.GetEnvironmentVariable("MARCUS_AWAKE_TEST_DIAGNOSTICS"), "1")) Console.Error.WriteLine("runtime_provider_call_exception:" + exception.GetType().Name);
                await FailBusinessConnectionAsync(activeConnection).ConfigureAwait(false);
                PublishFailure(handle, request, FrameworkErrors.Create("runtime.provider_call_failed", FrameworkErrorCategory.InternalFailure, "The Provider request could not be completed.", correlationId, details: new Dictionary<string, string> { { "failure_type", exception.GetType().Name } }));
            }
            finally
            {
                taskCancellation.Dispose();
            }
        }

        internal static OperationResult<bool> ProjectStreamEvents(AiTaskHandle handle, AiTaskRequest request, IReadOnlyList<ProviderStreamEventWire> streamEvents, string correlationId)
        {
            if (handle == null || request == null || streamEvents == null) return Failure<bool>("runtime.invalid_request", FrameworkErrorCategory.InvalidRequest, "A stream projection requires a task handle, request and events.", correlationId);

            var frameworkSequence = 3L;
            var inputTokens = 0;
            var outputTokens = 0;
            var expectsStructured = ProviderRuntimeWire.HasResponseSchemaJson(request);
            for (var index = 0; index < streamEvents.Count; index++)
            {
                var streamEvent = streamEvents[index];
                if (streamEvent == null) return Failure<bool>("runtime.stream_event_missing", FrameworkErrorCategory.Incompatible, "The Provider stream contained a missing event.", correlationId);
                if (streamEvent.EventKind == "started") continue;

                if (streamEvent.Usage != null)
                {
                    if (streamEvent.Usage.InputTokens.HasValue) inputTokens = streamEvent.Usage.InputTokens.Value;
                    if (streamEvent.Usage.OutputTokens.HasValue) outputTokens = streamEvent.Usage.OutputTokens.Value;
                }

                OperationResult<bool> published;
                switch (streamEvent.EventKind)
                {
                    case "text_delta":
                        published = handle.Publish(new AiTaskEvent(request.TaskId, request.MessageId, AiTaskEventKind.TextDelta, frameworkSequence++, streamEvent.Text, null, streamEvent.ModelId, 0, 0, string.Empty, streamEvent.ProviderId, string.Empty, string.Empty));
                        break;
                    case "usage_update":
                        published = handle.Publish(new AiTaskEvent(request.TaskId, request.MessageId, AiTaskEventKind.UsageUpdate, frameworkSequence++, string.Empty, null, streamEvent.ModelId, inputTokens, outputTokens, string.Empty, streamEvent.ProviderId, string.Empty, string.Empty));
                        break;
                    case "route_changed":
                        published = handle.Publish(new AiTaskEvent(request.TaskId, request.MessageId, AiTaskEventKind.RouteChanged, frameworkSequence++, string.Empty, null, streamEvent.ModelId, 0, 0, string.Empty, streamEvent.ToProviderId, streamEvent.FromProviderId, streamEvent.ToProviderId));
                        break;
                    case "completed":
                        if (expectsStructured && string.IsNullOrWhiteSpace(streamEvent.StructuredJson)) return Failure<bool>("structured_output_missing", FrameworkErrorCategory.ProviderFailure, "The Provider did not return the required structured result.", correlationId);
                        if (!expectsStructured && !string.IsNullOrWhiteSpace(streamEvent.StructuredJson)) return Failure<bool>("structured_json_unexpected", FrameworkErrorCategory.ProviderFailure, "The Provider returned an unexpected structured result.", correlationId);
                        return handle.PublishTerminal(AiTaskEventKind.Completed, string.Empty, null, streamEvent.ModelId, inputTokens, outputTokens, streamEvent.StructuredJson ?? string.Empty, streamEvent.ProviderId);
                    case "cancelled":
                        return handle.PublishTerminal(AiTaskEventKind.Cancelled, string.Empty, null, streamEvent.ModelId, inputTokens, outputTokens, string.Empty, streamEvent.ProviderId);
                    case "failed":
                        if (streamEvent.Error == null) return Failure<bool>("runtime.stream_error_missing", FrameworkErrorCategory.Incompatible, "The Provider stream failure did not contain an error.", correlationId);
                        var providerError = ProviderRuntimeWire.ToProviderWireError(streamEvent.Error);
                        var frameworkError = CreateProviderFrameworkError(providerError, correlationId, new ProviderScopeRequest(request.ProfileId, streamEvent.ProviderId, request.RouteId));
                        return handle.PublishTerminal(AiTaskEventKind.Failed, string.Empty, frameworkError, streamEvent.ModelId, inputTokens, outputTokens, string.Empty, streamEvent.ProviderId);
                    default:
                        return Failure<bool>("runtime.stream_event_kind_invalid", FrameworkErrorCategory.Incompatible, "The Provider stream contained an unsupported event.", correlationId);
                }

                if (!published.IsSuccess)
                {
                    if (published.Error != null && published.Error.Category == FrameworkErrorCategory.Conflict) return OperationResult<bool>.Succeeded(false);
                    return published;
                }
            }

            return Failure<bool>("runtime.stream_incomplete", FrameworkErrorCategory.ProviderFailure, "The Provider stream did not contain a terminal event.", correlationId);
        }

        internal static OperationResult<bool> ProjectUnaryCompletion(AiTaskHandle handle, AiTaskRequest request, CompletionResultWire result, string correlationId)
        {
            if (handle == null || request == null || result == null) return Failure<bool>("runtime.invalid_request", FrameworkErrorCategory.InvalidRequest, "A unary projection requires a task handle, request and result.", correlationId);

            var expectsStructured = ProviderRuntimeWire.HasResponseSchemaJson(request);
            if (expectsStructured && string.IsNullOrWhiteSpace(result.StructuredJson)) return Failure<bool>("structured_output_missing", FrameworkErrorCategory.ProviderFailure, "The Provider did not return the required structured result.", correlationId);
            if (!expectsStructured && !string.IsNullOrWhiteSpace(result.StructuredJson)) return Failure<bool>("structured_json_unexpected", FrameworkErrorCategory.ProviderFailure, "The Provider returned an unexpected structured result.", correlationId);

            var maximumOutputBytes = Math.Min(RuntimeService.MaximumOutputBytes, request.Budget.OutputBytes);
            var maximumTextDeltas = Math.Min(RuntimeService.MaximumTextDeltas, request.Budget.TextDeltas);
            var maximumTokens = Math.Min(RuntimeService.MaximumTokens, request.Budget.Tokens);
            var outputBytes = (long)Encoding.UTF8.GetByteCount(result.Content ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(result.StructuredJson)) outputBytes += Encoding.UTF8.GetByteCount(result.StructuredJson);
            if (outputBytes > maximumOutputBytes) return Failure<bool>("runtime.output_budget_exceeded", FrameworkErrorCategory.ResourceExhausted, "The Provider output budget was exceeded.", correlationId);

            var inputTokens = result.Usage == null ? 0 : result.Usage.InputTokens;
            var outputTokens = result.Usage == null ? 0 : result.Usage.OutputTokens;
            if (result.Usage != null && (!ProviderProtocolContract.IsValidUsage(inputTokens, outputTokens) || (long)inputTokens + outputTokens > maximumTokens)) return Failure<bool>("runtime.token_budget_exceeded", FrameworkErrorCategory.ResourceExhausted, "The Provider token budget was exceeded.", correlationId);
            if (!string.IsNullOrEmpty(result.Content) && maximumTextDeltas < 1) return Failure<bool>("runtime.delta_budget_exceeded", FrameworkErrorCategory.ResourceExhausted, "The Provider text delta budget was exceeded.", correlationId);

            var sequence = 3L;
            if (!string.IsNullOrEmpty(result.Content))
            {
                var publishedText = handle.Publish(new AiTaskEvent(request.TaskId, request.MessageId, AiTaskEventKind.TextDelta, sequence++, result.Content, null, result.ModelId, 0, 0, string.Empty, request.ProviderId, string.Empty, string.Empty));
                if (!publishedText.IsSuccess)
                {
                    if (publishedText.Error != null && publishedText.Error.Category == FrameworkErrorCategory.Conflict) return OperationResult<bool>.Succeeded(false);
                    return publishedText;
                }
            }

            if (result.Usage != null)
            {
                var publishedUsage = handle.Publish(new AiTaskEvent(request.TaskId, request.MessageId, AiTaskEventKind.UsageUpdate, sequence++, string.Empty, null, result.ModelId, inputTokens, outputTokens, string.Empty, request.ProviderId, string.Empty, string.Empty));
                if (!publishedUsage.IsSuccess)
                {
                    if (publishedUsage.Error != null && publishedUsage.Error.Category == FrameworkErrorCategory.Conflict) return OperationResult<bool>.Succeeded(false);
                    return publishedUsage;
                }
            }

            return handle.PublishTerminal(AiTaskEventKind.Completed, string.Empty, null, result.ModelId, inputTokens, outputTokens, result.StructuredJson ?? string.Empty, request.ProviderId);
        }

        private static void PublishCancellation(AiTaskHandle handle, AiTaskRequest request)
        {
            if (handle == null || request == null) return;
            handle.PublishTerminal(AiTaskEventKind.Cancelled, string.Empty, null, string.Empty, 0, 0);
        }

        private static void PublishFailure(AiTaskHandle handle, AiTaskRequest request, FrameworkError error)
        {
            if (handle == null || request == null || error == null) return;
            handle.PublishTerminal(AiTaskEventKind.Failed, string.Empty, error, string.Empty, 0, 0);
        }

        private async Task<OperationResult<RuntimeServiceStatus>> AwaitDrainAsync(RequestContext context, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return Failure<RuntimeServiceStatus>("runtime.cancelled", FrameworkErrorCategory.Cancelled, "The runtime service drain was cancelled.", context == null ? "runtime-drain" : context.CorrelationId);

            Task<OperationResult<RuntimeServiceStatus>> task;
            lock (sync)
            {
                if (state == RuntimeServiceState.RecoveryRequired) return Failure<RuntimeServiceStatus>("runtime.recovery_required", FrameworkErrorCategory.RecoveryRequired, "The runtime service requires recovery.", context.CorrelationId);
                if (state != RuntimeServiceState.Draining || drainTask == null) return OperationResult<RuntimeServiceStatus>.Succeeded(CreateStatusLocked());
                task = drainTask;
            }

            try
            {
                return await WaitWithCancellationAsync(task, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Failure<RuntimeServiceStatus>("runtime.cancelled", FrameworkErrorCategory.Cancelled, "The runtime service drain was cancelled.", context.CorrelationId);
            }
        }

        private RuntimeServiceStatus CreateStatusLocked()
        {
            return new RuntimeServiceStatus(state, serviceId, instanceId, connectionEpoch, protocolVersion, Copy(capabilities));
        }

        private static string ResolveServicePath(string configuredPath)
        {
            try
            {
                return Path.GetFullPath(configuredPath);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
            {
                throw new ClientUnavailableException("service_path_invalid");
            }
        }

        private static string[] Copy(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0) return new string[0];
            var copy = new string[values.Count];
            for (var index = 0; index < values.Count; index++) copy[index] = values[index];
            return copy;
        }

        private static OperationResult<T> Failure<T>(string code, FrameworkErrorCategory category, string fallback, string correlationId, string detail = null)
        {
            IReadOnlyDictionary<string, string> details = null;
            if (!string.IsNullOrWhiteSpace(detail)) details = new Dictionary<string, string> { { "protocol_detail", detail } };
            return OperationResult<T>.Failed(FrameworkErrors.Create(code, category, fallback, correlationId, retryable: category == FrameworkErrorCategory.Unavailable || category == FrameworkErrorCategory.Timeout, details: details));
        }

        private static string QuoteJson(string value)
        {
            var builder = new StringBuilder((value == null ? 0 : value.Length) + 2);
            builder.Append('"');
            if (value != null)
            {
                for (var index = 0; index < value.Length; index++)
                {
                    var character = value[index];
                    switch (character)
                    {
                        case '\\': builder.Append("\\\\"); break;
                        case '"': builder.Append("\\\""); break;
                        case '\b': builder.Append("\\b"); break;
                        case '\f': builder.Append("\\f"); break;
                        case '\n': builder.Append("\\n"); break;
                        case '\r': builder.Append("\\r"); break;
                        case '\t': builder.Append("\\t"); break;
                        default:
                            if (character < 0x20) builder.Append("\\u").Append(((int)character).ToString("x4"));
                            else builder.Append(character);
                            break;
                    }
                }
            }

            return builder.Append('"').ToString();
        }

        private static string CanonicalizePayload(string payloadJson)
        {
            var skeleton = new PipeEnvelope
            {
                MessageType = ProtocolConstants.MessageTypeHealth,
                MessageId = "canonicalize",
                CorrelationId = "canonicalize",
                SessionId = string.Empty,
                DeadlineUnixMilliseconds = 1,
                InstanceEpoch = 1,
                ConnectionEpoch = 1,
                DirectionNonce = "canonicalize",
                Sequence = 1,
                PayloadSchema = "marcus-awake.health.v1",
                PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
                PayloadLength = 0,
                PayloadSha256 = string.Empty,
                Checksum = string.Empty,
                ChecksumAlgorithm = ProtocolConstants.ChecksumAlgorithm,
                AckStatus = string.Empty,
                OutcomeKind = string.Empty,
                NonDurable = false
            };

            var serialized = ProtocolCodec.SerializeEnvelope(skeleton);
            if (!ProtocolCodec.TryParseEnvelope(serialized, out var normalized, out var error))
            {
                throw new ArgumentException(error, nameof(payloadJson));
            }
            return normalized.PayloadJson;
        }

        private static async Task<T> WaitWithCancellationAsync<T>(Task<T> task, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled) return await task.ConfigureAwait(false);
            var cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);
            var completed = await Task.WhenAny(task, cancellationTask).ConfigureAwait(false);
            if (completed != task) throw new OperationCanceledException(cancellationToken);
            return await task.ConfigureAwait(false);
        }

        private async Task<OperationResult<RuntimeServiceStatus>> StartCoreAsync(
            RuntimeServiceStartRequest request,
            RequestContext context,
            long currentLaunchEpoch,
            CancellationTokenSource lifecycle,
            CancellationTokenSource startup)
        {
            Process activeProcess = null;
            ClientConnection activeConnection = null;
            NamedPipeClientStream activePipe = null;
            byte[] bootstrapSecret = null;
            byte[] serviceKey = null;
            byte[] frameKey = null;
            var installed = false;
            var phase = "initializing";
            OperationResult<RuntimeServiceStatus> result;

            try
            {
                phase = "resolve_service";
                var servicePath = ResolveServicePath(options.ServicePath);
                if (!File.Exists(servicePath)) throw new ClientUnavailableException("service_binary_missing");
                phase = "hash_service";
                var serviceArtifactSha256 = TransportSecurity.Sha256FileHex(servicePath);
                if (string.IsNullOrWhiteSpace(serviceArtifactSha256)) throw new ClientUnavailableException("service_binary_hash_failed");

                phase = "create_bootstrap";
                var descriptor = CreateBootstrapDescriptor(serviceArtifactSha256, currentLaunchEpoch, out bootstrapSecret, out serviceKey, out frameKey);
                startup.Token.ThrowIfCancellationRequested();

                var startInfo = new ProcessStartInfo
                {
                    FileName = servicePath,
                    WorkingDirectory = Path.GetDirectoryName(servicePath) ?? AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                phase = "start_process";
                activeProcess = Process.Start(startInfo);
                if (activeProcess == null) throw new ClientUnavailableException("service_process_start_failed");
                RegisterStartingProcess(currentLaunchEpoch, activeProcess);
                ObserveStandardError(activeProcess);

                phase = "write_bootstrap";
                await WriteFrameWithTimeoutAsync(activeProcess.StandardInput.BaseStream, ProtocolCodec.SerializeBootstrap(descriptor), options.BootstrapTimeoutMilliseconds, startup.Token).ConfigureAwait(false);
                phase = "write_secret";
                await WriteFrameWithTimeoutAsync(activeProcess.StandardInput.BaseStream, Convert.ToBase64String(bootstrapSecret), options.BootstrapTimeoutMilliseconds, startup.Token).ConfigureAwait(false);
                try { activeProcess.StandardInput.Close(); } catch (IOException) { }

                phase = "read_ready";
                var readyJson = await ReadFrameWithTimeoutAsync(activeProcess.StandardOutput.BaseStream, options.BootstrapTimeoutMilliseconds, startup.Token).ConfigureAwait(false);
                ServiceReadyDescriptor ready;
                string readyError;
                if (!ProtocolCodec.TryParseServiceReady(readyJson, out ready, out readyError)) throw new ClientProtocolException("service_ready_" + readyError);
                phase = "validate_ready";
                ValidateServiceReady(descriptor, ready, serviceKey, servicePath, activeProcess);

                phase = "connect_pipe";
                activePipe = new NamedPipeClientStream(".", descriptor.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await ConnectWithTimeoutAsync(activePipe, options.ConnectTimeoutMilliseconds, startup.Token).ConfigureAwait(false);
                phase = "write_handshake";
                var handshakeRequest = CreateHandshakeRequest(descriptor, ready, context, request.MaximumFrameBytes, serviceKey);
                await WriteFrameWithTimeoutAsync(activePipe, ProtocolCodec.SerializeHandshakeRequest(handshakeRequest), options.HandshakeTimeoutMilliseconds, startup.Token).ConfigureAwait(false);
                phase = "read_handshake";
                var handshakeJson = await ReadFrameWithTimeoutAsync(activePipe, options.HandshakeTimeoutMilliseconds, startup.Token).ConfigureAwait(false);
                HandshakeResponse handshakeResponse;
                string handshakeError;
                if (!ProtocolCodec.TryParseHandshakeResponse(handshakeJson, out handshakeResponse, out handshakeError)) throw new ClientProtocolException("handshake_response_" + handshakeError);
                phase = "validate_handshake";
                var effectiveFrameBytes = ValidateHandshakeResponse(descriptor, ready, handshakeRequest, handshakeResponse, serviceKey, request.MaximumFrameBytes);
                activeConnection = new ClientConnection(this, activePipe, descriptor, ready, handshakeRequest, handshakeResponse, frameKey, serviceKey, effectiveFrameBytes);
                activePipe = null;

                lock (sync)
                {
                    if (disposed || state != RuntimeServiceState.Starting || launchEpoch != currentLaunchEpoch) throw new ClientLifecycleException("start_superseded");
                    connection = activeConnection;
                    process = activeProcess;
                    instanceId = ready.ServiceInstanceId;
                    connectionEpoch = handshakeResponse.ConnectionEpoch;
                    capabilities = Copy(handshakeResponse.Capabilities);
                    state = RuntimeServiceState.Ready;
                    installed = true;
                    activeConnection = null;
                    activeProcess = null;
                }

                result = OperationResult<RuntimeServiceStatus>.Succeeded(Status);
            }
            catch (OperationCanceledException)
            {
                result = Failure<RuntimeServiceStatus>("runtime.cancelled", FrameworkErrorCategory.Cancelled, "The runtime service start was cancelled.", context.CorrelationId);
            }
            catch (TimeoutException)
            {
                result = Failure<RuntimeServiceStatus>("runtime.start_timeout", FrameworkErrorCategory.Timeout, "The runtime service did not start within the configured time.", context.CorrelationId);
            }
            catch (ClientProtocolException exception)
            {
                result = Failure<RuntimeServiceStatus>("runtime.ipc_protocol_rejected", FrameworkErrorCategory.Incompatible, "The runtime service protocol was rejected.", context.CorrelationId, exception.Code);
            }
            catch (ClientUnavailableException exception)
            {
                result = Failure<RuntimeServiceStatus>("runtime.unavailable", FrameworkErrorCategory.Unavailable, "The runtime service could not be started.", context.CorrelationId, exception.Code);
            }
            catch (Exception exception)
            {
                result = Failure<RuntimeServiceStatus>("runtime.start_failed", FrameworkErrorCategory.Unavailable, "The runtime service could not be started.", context.CorrelationId, phase + ":" + exception.GetType().Name);
            }

            if (!installed)
            {
                var cleanup = await CleanupConnectionAndProcessAsync(activeConnection, activePipe, activeProcess).ConfigureAwait(false);
                if (!cleanup.Succeeded) result = Failure<RuntimeServiceStatus>("runtime.cleanup_required", FrameworkErrorCategory.RecoveryRequired, "The runtime service could not be closed safely and requires recovery.", context.CorrelationId);
                CompleteFailedStart(currentLaunchEpoch, lifecycle, activeConnection, activeProcess, cleanup.Succeeded);
            }
            else
            {
                startup.Dispose();
            }

            ClearBytes(bootstrapSecret);
            ClearBytes(serviceKey);
            ClearBytes(frameKey);
            return result;
        }

        private BootstrapDescriptor CreateBootstrapDescriptor(
            string serviceArtifactSha256,
            long currentLaunchEpoch,
            out byte[] bootstrapSecret,
            out byte[] serviceKey,
            out byte[] frameKey)
        {
            bootstrapSecret = TransportSecurity.RandomBytes(32);
            serviceKey = null;
            frameKey = null;
            try
            {
                if (Environment.OSVersion.Platform != PlatformID.Win32NT) throw new ClientUnavailableException("windows_required");

                string sid;
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    if (identity == null || identity.User == null) throw new ClientUnavailableException("current_sid_missing");
                    sid = identity.User.Value;
                }

                int parentProcessId;
                long parentStartUnixMilliseconds;
                using (var parent = Process.GetCurrentProcess())
                {
                    parentProcessId = parent.Id;
                    parentStartUnixMilliseconds = ReadProcessStartUnixMilliseconds(parent);
                }

                var sidFingerprint = TransportSecurity.SidFingerprint(sid);
                var descriptor = new BootstrapDescriptor
                {
                    ProtocolId = ProtocolConstants.ProtocolId,
                    ProtocolMajor = ProtocolConstants.ProtocolMajor,
                    ProtocolMinor = ProtocolConstants.ProtocolMinor,
                    FrameworkApiMajor = ProtocolConstants.FrameworkApiMajor,
                    BannerlordApiVersion = options.BannerlordApiVersion,
                    ServiceId = serviceId,
                    ClientInstanceId = "framework-client-" + TransportSecurity.RandomHex(12),
                    LaunchTransactionId = "launch-" + TransportSecurity.RandomHex(12),
                    LaunchNonce = TransportSecurity.RandomHex(32),
                    ChallengeId = "challenge-" + TransportSecurity.RandomHex(12),
                    SessionNonce = "session-" + TransportSecurity.RandomHex(16),
                    UserSidFingerprint = sidFingerprint,
                    ParentProcessId = parentProcessId,
                    ParentStartUnixMilliseconds = parentStartUnixMilliseconds,
                    InstanceEpoch = currentLaunchEpoch,
                    AuthKeyId = AuthKeyId,
                    ExpectedServiceArtifactSha256 = serviceArtifactSha256,
                    PipeName = ProtocolValidation.BuildPipeName(sidFingerprint),
                    RequestedCapabilities = Copy(options.RequestedCapabilities)
                };

                serviceKey = TransportSecurity.DeriveAuthKey(bootstrapSecret, descriptor.LaunchNonce, BootstrapAuthDomain, descriptor.LaunchTransactionId, descriptor.AuthKeyId);
                frameKey = TransportSecurity.DeriveAuthKey(bootstrapSecret, descriptor.LaunchNonce, FrameAuthDomain, descriptor.LaunchTransactionId, descriptor.AuthKeyId);
                descriptor.ClientBootstrapProof = TransportSecurity.ComputeClientBootstrapProof(descriptor, serviceKey);
                return descriptor;
            }
            catch
            {
                ClearBytes(bootstrapSecret);
                throw;
            }
        }

        private void RegisterStartingProcess(long currentLaunchEpoch, Process activeProcess)
        {
            lock (sync)
            {
                if (!disposed && state == RuntimeServiceState.Starting && launchEpoch == currentLaunchEpoch)
                {
                    process = activeProcess;
                    return;
                }
            }

            TryKill(activeProcess);
            throw new ClientLifecycleException("start_superseded");
        }

        private static void ObserveStandardError(Process activeProcess)
        {
            try
            {
                var errorTask = activeProcess.StandardError.ReadToEndAsync();
                errorTask.ContinueWith(task => { var ignored = task.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ObjectDisposedException)
            {
            }
        }

        private HandshakeRequest CreateHandshakeRequest(BootstrapDescriptor descriptor, ServiceReadyDescriptor ready, RequestContext context, int requestedFrameBytes, byte[] serviceKey, string sessionId = null, string challengeId = null, IReadOnlyList<string> requestedCapabilities = null)
        {
            var request = new HandshakeRequest
            {
                ProtocolId = ProtocolConstants.ProtocolId,
                ProtocolMajor = ProtocolConstants.ProtocolMajor,
                ProtocolMinor = ProtocolConstants.ProtocolMinor,
                ClientInstanceId = descriptor.ClientInstanceId,
                ServiceInstanceId = ready.ServiceInstanceId,
                ServiceId = serviceId,
                FrameworkApiMajor = ProtocolConstants.FrameworkApiMajor,
                BannerlordApiVersion = descriptor.BannerlordApiVersion,
                RequestedCapabilities = Copy(requestedCapabilities ?? descriptor.RequestedCapabilities),
                SessionNonce = descriptor.SessionNonce,
                UserSidFingerprint = descriptor.UserSidFingerprint,
                ParentProcessId = descriptor.ParentProcessId,
                ParentStartUnixMilliseconds = descriptor.ParentStartUnixMilliseconds,
                InstanceEpoch = descriptor.InstanceEpoch,
                LaunchTransactionId = descriptor.LaunchTransactionId,
                LaunchNonce = descriptor.LaunchNonce,
                ChallengeId = string.IsNullOrWhiteSpace(challengeId) ? descriptor.ChallengeId : challengeId,
                AuthKeyId = descriptor.AuthKeyId,
                PipeName = descriptor.PipeName,
                ClientBootstrapProof = descriptor.ClientBootstrapProof,
                ServiceBootstrapProof = ready.ServiceBootstrapProof,
                SessionId = string.IsNullOrWhiteSpace(sessionId) ? context.Session.SessionId : sessionId,
                MaxFrameBytes = Math.Min(Math.Min(requestedFrameBytes, options.MaximumFrameBytes), ProtocolConstants.MaxFrameBytes),
                ChecksumAlgorithm = ProtocolConstants.ChecksumAlgorithm,
                CorrelationId = descriptor.LaunchTransactionId
            };
            request.ChallengeResponse = TransportSecurity.ComputeChallengeResponse(request, serviceKey);
            return request;
        }

        private static long ReadProcessStartUnixMilliseconds(Process activeProcess)
        {
            try
            {
                return new DateTimeOffset(activeProcess.StartTime.ToUniversalTime()).ToUnixTimeMilliseconds();
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is System.ComponentModel.Win32Exception || exception is NotSupportedException)
            {
                throw new ClientUnavailableException("process_start_time_unavailable");
            }
        }

        private static void ValidateServiceReady(BootstrapDescriptor descriptor, ServiceReadyDescriptor ready, byte[] serviceKey, string servicePath, Process activeProcess)
        {
            if (!StringComparer.Ordinal.Equals(ready.ProtocolId, ProtocolConstants.ProtocolId) || ready.ProtocolMajor != ProtocolConstants.ProtocolMajor || ready.ProtocolMinor != ProtocolConstants.ProtocolMinor) throw new ClientProtocolException("service_ready_protocol_invalid");
            if (ready.FrameworkApiMajor != ProtocolConstants.FrameworkApiMajor) throw new ClientProtocolException("service_ready_api_invalid");
            if (!StringComparer.Ordinal.Equals(ready.ServiceId, ProtocolConstants.ServiceId)) throw new ClientProtocolException("service_ready_service_invalid");
            if (ready.ServiceProcessId != activeProcess.Id) throw new ClientProtocolException("service_ready_process_mismatch");
            var actualStart = ReadProcessStartUnixMilliseconds(activeProcess);
            if (ready.ServiceStartUnixMilliseconds != actualStart) throw new ClientProtocolException("service_ready_start_time_mismatch");
            if (!StringComparer.OrdinalIgnoreCase.Equals(ready.ServiceArtifactSha256, descriptor.ExpectedServiceArtifactSha256)) throw new ClientProtocolException("service_ready_hash_mismatch");
            if (!StringComparer.OrdinalIgnoreCase.Equals(ready.ServiceArtifactSha256, TransportSecurity.Sha256FileHex(servicePath))) throw new ClientProtocolException("service_ready_artifact_changed");
            if (!StringComparer.Ordinal.Equals(ready.PipeName, descriptor.PipeName)) throw new ClientProtocolException("service_ready_pipe_mismatch");
            if (!StringComparer.Ordinal.Equals(ready.UserSidFingerprint, descriptor.UserSidFingerprint)) throw new ClientProtocolException("service_ready_sid_mismatch");
            if (ready.InstanceEpoch != descriptor.InstanceEpoch) throw new ClientProtocolException("service_ready_epoch_mismatch");

            var expectedParentProof = TransportSecurity.ComputeParentStartProof(descriptor, serviceKey, ready.ServiceProcessId, ready.ServiceStartUnixMilliseconds, ready.ServiceArtifactSha256);
            if (!TransportSecurity.FixedTimeEquals(expectedParentProof, ready.ParentStartProof)) throw new ClientProtocolException("service_ready_parent_proof_invalid");
            var expectedServiceProof = TransportSecurity.ComputeServiceBootstrapProof(descriptor, serviceKey, ready.ServiceInstanceId, ready.ServiceProcessId, ready.ServiceStartUnixMilliseconds, ready.ServiceArtifactSha256, ready.ParentStartProof);
            if (!TransportSecurity.FixedTimeEquals(expectedServiceProof, ready.ServiceBootstrapProof)) throw new ClientProtocolException("service_ready_bootstrap_proof_invalid");
        }

        private int ValidateHandshakeResponse(BootstrapDescriptor descriptor, ServiceReadyDescriptor ready, HandshakeRequest request, HandshakeResponse response, byte[] serviceKey, int requestedFrameBytes)
        {
            if (!response.Accepted) throw new ClientProtocolException("handshake_rejected_" + (string.IsNullOrWhiteSpace(response.ErrorCode) ? "unknown" : response.ErrorCode));
            if (!StringComparer.Ordinal.Equals(response.ProtocolId, ProtocolConstants.ProtocolId) || response.ProtocolMajor != ProtocolConstants.ProtocolMajor || response.ProtocolMinor != ProtocolConstants.ProtocolMinor) throw new ClientProtocolException("handshake_protocol_invalid");
            if (response.FrameworkApiMajor != ProtocolConstants.FrameworkApiMajor) throw new ClientProtocolException("handshake_api_invalid");
            if (!StringComparer.Ordinal.Equals(response.ServiceId, serviceId)) throw new ClientProtocolException("handshake_service_invalid");
            if (!StringComparer.Ordinal.Equals(response.ServiceInstanceId, ready.ServiceInstanceId)) throw new ClientProtocolException("handshake_service_instance_invalid");
            if (!StringComparer.Ordinal.Equals(response.UserSidFingerprint, descriptor.UserSidFingerprint)) throw new ClientProtocolException("handshake_sid_invalid");
            if (response.ParentProcessId != descriptor.ParentProcessId) throw new ClientProtocolException("handshake_parent_invalid");
            if (!StringComparer.Ordinal.Equals(response.ParentStartProof, ready.ParentStartProof)) throw new ClientProtocolException("handshake_parent_proof_invalid");
            if (response.InstanceEpoch != descriptor.InstanceEpoch || response.ConnectionEpoch < 1) throw new ClientProtocolException("handshake_epoch_invalid");
            if (!StringComparer.Ordinal.Equals(response.AckStatus, ProtocolConstants.AckAccepted) || !StringComparer.Ordinal.Equals(response.OutcomeKind, ProtocolConstants.OutcomeAccepted) || !response.NonDurable) throw new ClientProtocolException("handshake_ack_invalid");
            if (!StringComparer.Ordinal.Equals(response.ChecksumAlgorithm, ProtocolConstants.ChecksumAlgorithm)) throw new ClientProtocolException("handshake_checksum_invalid");
            if (string.IsNullOrWhiteSpace(response.ClientDirectionNonce) || string.IsNullOrWhiteSpace(response.ServiceDirectionNonce)) throw new ClientProtocolException("handshake_nonce_missing");

            var expectedServiceAuth = TransportSecurity.ComputeServiceAuthResponse(request, serviceKey, ready.ServiceInstanceId, response.ConnectionEpoch, response.ClientDirectionNonce, response.ServiceDirectionNonce, ready.ParentStartProof);
            if (!TransportSecurity.FixedTimeEquals(expectedServiceAuth, response.ServiceBootstrapProof)) throw new ClientProtocolException("handshake_service_auth_invalid");
            if (!SameCapabilities(request.RequestedCapabilities, response.Capabilities)) throw new ClientProtocolException("handshake_capabilities_invalid");

            var effectiveFrameBytes = Math.Min(Math.Min(requestedFrameBytes, options.MaximumFrameBytes), response.MaxFrameBytes);
            if (effectiveFrameBytes < 1024 || effectiveFrameBytes > ProtocolConstants.MaxFrameBytes) throw new ClientProtocolException("handshake_frame_limit_invalid");
            return effectiveFrameBytes;
        }

        internal static bool SameCapabilities(IReadOnlyList<string> requested, IReadOnlyList<string> actual)
        {
            if (requested == null || actual == null || actual.Count == 0) return false;
            var requestedSet = new HashSet<string>(StringComparer.Ordinal);
            var actualSet = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < requested.Count; index++) if (!requestedSet.Add(requested[index])) return false;
            for (var index = 0; index < actual.Count; index++) if (!actualSet.Add(actual[index])) return false;
            if (!actualSet.Contains(ProtocolConstants.MessageTypeHealth)) return false;
            foreach (var value in actualSet) if (!requestedSet.Contains(value)) return false;
            return true;
        }

        private static async Task WriteFrameWithTimeoutAsync(Stream stream, string json, int timeoutMilliseconds, CancellationToken cancellationToken)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, CancellationToken.None))
            {
                timeout.CancelAfter(timeoutMilliseconds);
                try
                {
                    await PipeFrameIO.WriteFrameAsync(stream, json, timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested) throw;
                    throw new TimeoutException();
                }
            }
        }

        private static async Task<string> ReadFrameWithTimeoutAsync(Stream stream, int timeoutMilliseconds, CancellationToken cancellationToken)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, CancellationToken.None))
            {
                timeout.CancelAfter(timeoutMilliseconds);
                try
                {
                    return await PipeFrameIO.ReadFrameAsync(stream, timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested) throw;
                    throw new TimeoutException();
                }
            }
        }

        private static async Task ConnectWithTimeoutAsync(NamedPipeClientStream pipe, int timeoutMilliseconds, CancellationToken cancellationToken)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, CancellationToken.None))
            {
                timeout.CancelAfter(timeoutMilliseconds);
                try
                {
                    await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested) throw;
                    throw new TimeoutException();
                }
            }
        }

        private async Task<OperationResult<RuntimeServiceStatus>> DrainCoreAsync(ClientConnection activeConnection, Process activeProcess, CancellationTokenSource activeLifecycle)
        {
            CleanupResult cleanup;
            try
            {
                if (activeLifecycle != null) activeLifecycle.Cancel();
                cleanup = await CleanupConnectionAndProcessAsync(activeConnection, null, activeProcess).ConfigureAwait(false);
            }
            catch (Exception)
            {
                cleanup = new CleanupResult(false, false);
            }

            var succeeded = cleanup.Succeeded;
            lock (sync)
            {
                if (ReferenceEquals(connection, activeConnection)) connection = null;
                if (ReferenceEquals(process, activeProcess)) process = null;
                if (ReferenceEquals(lifecycleCancellation, activeLifecycle)) lifecycleCancellation = null;
                if (state == RuntimeServiceState.Draining) state = succeeded ? RuntimeServiceState.Stopped : RuntimeServiceState.RecoveryRequired;
            }

            DisposeCancellation(activeLifecycle);
            if (!succeeded) return Failure<RuntimeServiceStatus>("runtime.cleanup_required", FrameworkErrorCategory.RecoveryRequired, "The runtime service could not be closed safely and requires recovery.", "runtime-drain");
            return OperationResult<RuntimeServiceStatus>.Succeeded(Status);
        }

        private void CompleteFailedStart(long currentLaunchEpoch, CancellationTokenSource failedLifecycle, ClientConnection failedConnection, Process failedProcess, bool cleanupSucceeded)
        {
            lock (sync)
            {
                if (ReferenceEquals(connection, failedConnection)) connection = null;
                if (ReferenceEquals(process, failedProcess)) process = null;
                if (ReferenceEquals(lifecycleCancellation, failedLifecycle)) lifecycleCancellation = null;
                if (!disposed && launchEpoch == currentLaunchEpoch && state == RuntimeServiceState.Starting)
                {
                    state = cleanupSucceeded ? RuntimeServiceState.Stopped : RuntimeServiceState.RecoveryRequired;
                }
            }

            DisposeCancellation(failedLifecycle);
        }

        private async Task<CleanupResult> FailConnectionAsync(ClientConnection expectedConnection)
        {
            Process activeProcess;
            CancellationTokenSource activeLifecycle;
            lock (sync)
            {
                if (!ReferenceEquals(connection, expectedConnection)) return new CleanupResult(true, true);
                state = RuntimeServiceState.Draining;
                connection = null;
                activeProcess = process;
                process = null;
                activeLifecycle = lifecycleCancellation;
                lifecycleCancellation = null;
            }

            if (activeLifecycle != null) activeLifecycle.Cancel();
            var cleanup = await CleanupConnectionAndProcessAsync(expectedConnection, null, activeProcess).ConfigureAwait(false);
            lock (sync)
            {
                if (state == RuntimeServiceState.Draining) state = cleanup.Succeeded ? RuntimeServiceState.Stopped : RuntimeServiceState.RecoveryRequired;
            }

            DisposeCancellation(activeLifecycle);
            return cleanup;
        }

        private async Task<CleanupResult> CleanupConnectionAndProcessAsync(ClientConnection activeConnection, NamedPipeClientStream activePipe, Process activeProcess)
        {
            var connectionClosed = true;
            if (activeConnection != null) connectionClosed = await activeConnection.CloseAsync(options.DrainTimeoutMilliseconds).ConfigureAwait(false);
            if (activePipe != null)
            {
                try { activePipe.Dispose(); } catch (Exception exception) when (exception is IOException || exception is ObjectDisposedException) { connectionClosed = false; }
            }

            var processStopped = await TerminateProcessAsync(activeProcess, options.DrainTimeoutMilliseconds).ConfigureAwait(false);
            return new CleanupResult(connectionClosed, processStopped);
        }

        private static async Task<bool> TerminateProcessAsync(Process activeProcess, int timeoutMilliseconds)
        {
            if (activeProcess == null) return true;
            if (!HasExited(activeProcess))
            {
                if (!TryKill(activeProcess)) return false;
            }

            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            while (DateTime.UtcNow < deadline)
            {
                if (HasExited(activeProcess))
                {
                    try { activeProcess.Dispose(); } catch (ObjectDisposedException) { }
                    return true;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            var exited = HasExited(activeProcess);
            if (exited)
            {
                try { activeProcess.Dispose(); } catch (ObjectDisposedException) { }
            }
            return exited;
        }

        private static bool HasExited(Process activeProcess)
        {
            try { return activeProcess.HasExited; }
            catch (Exception exception) when (exception is InvalidOperationException || exception is NotSupportedException || exception is ObjectDisposedException) { return false; }
        }

        private static bool TryKill(Process activeProcess)
        {
            if (activeProcess == null || HasExited(activeProcess)) return true;
            try
            {
                activeProcess.Kill();
                return true;
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is System.ComponentModel.Win32Exception || exception is NotSupportedException)
            {
                return HasExited(activeProcess);
            }
        }

        private static void DisposeCancellation(CancellationTokenSource cancellation)
        {
            if (cancellation == null) return;
            try { cancellation.Dispose(); } catch (ObjectDisposedException) { }
        }

        private static void ClearBytes(byte[] bytes)
        {
            if (bytes == null) return;
            for (var index = 0; index < bytes.Length; index++) bytes[index] = 0;
        }

        private sealed class CleanupResult
        {
            internal CleanupResult(bool connectionClosed, bool processStopped)
            {
                ConnectionClosed = connectionClosed;
                ProcessStopped = processStopped;
            }

            internal bool ConnectionClosed { get; }
            internal bool ProcessStopped { get; }
            internal bool Succeeded => ConnectionClosed && ProcessStopped;
        }

        private sealed class ClientUnavailableException : Exception
        {
            internal ClientUnavailableException(string code) : base(code) { Code = code; }
            internal string Code { get; }
        }

        private sealed class ClientProtocolException : Exception
        {
            internal ClientProtocolException(string code) : base(code) { Code = code; }
            internal string Code { get; }
        }

        private sealed class ProviderResponseException : Exception
        {
            internal ProviderResponseException(ProviderWireError error) : base(error == null ? "provider_response_invalid" : error.Code)
            {
                Error = error;
            }

            internal ProviderWireError Error { get; }
        }

        private sealed class ClientLifecycleException : Exception
        {
            internal ClientLifecycleException(string code) : base(code) { Code = code; }
            internal string Code { get; }
        }

        private sealed class ClientConnection
        {
            private readonly RuntimeServiceClient owner;
            private readonly NamedPipeClientStream pipe;
            private readonly BootstrapDescriptor descriptor;
            private readonly ServiceReadyDescriptor ready;
            private readonly HandshakeRequest handshakeRequest;
            private readonly HandshakeResponse handshakeResponse;
            private readonly byte[] frameKey;
            private readonly byte[] serviceKey;
            private readonly int maximumFrameBytes;
            private readonly SemaphoreSlim requestGate = new SemaphoreSlim(1, 1);
            private readonly SemaphoreSlim controlConnectionGate = new SemaphoreSlim(1, 1);
            private readonly SequenceWindow responseWindow;
            private long nextSequence;
            private long lastConnectionEpoch;
            private int aborted;

            internal ClientConnection(
                RuntimeServiceClient owner,
                NamedPipeClientStream pipe,
                BootstrapDescriptor descriptor,
                ServiceReadyDescriptor ready,
                HandshakeRequest handshakeRequest,
                HandshakeResponse handshakeResponse,
                byte[] frameKey,
                byte[] serviceKey,
                int maximumFrameBytes)
            {
                this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
                this.pipe = pipe ?? throw new ArgumentNullException(nameof(pipe));
                this.descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
                this.ready = ready ?? throw new ArgumentNullException(nameof(ready));
                this.handshakeRequest = handshakeRequest ?? throw new ArgumentNullException(nameof(handshakeRequest));
                this.handshakeResponse = handshakeResponse ?? throw new ArgumentNullException(nameof(handshakeResponse));
                this.frameKey = CopyBytes(frameKey);
                this.serviceKey = CopyBytes(serviceKey);
                this.maximumFrameBytes = maximumFrameBytes;
                responseWindow = new SequenceWindow(handshakeRequest.SessionId, handshakeResponse.ConnectionEpoch, handshakeResponse.ServiceDirectionNonce);
                lastConnectionEpoch = handshakeResponse.ConnectionEpoch;
            }

            internal ProviderStreamBudgetDescriptor StreamBudget => handshakeResponse.StreamBudget ?? ProviderStreamBudgetDescriptor.Default();

            internal void Abort()
            {
                if (Interlocked.Exchange(ref aborted, 1) != 0) return;
                try { pipe.Dispose(); } catch (ObjectDisposedException) { }
            }

            internal async Task<bool> CloseAsync(int timeoutMilliseconds)
            {
                Abort();
                var wait = requestGate.WaitAsync();
                var completed = await Task.WhenAny(wait, Task.Delay(timeoutMilliseconds)).ConfigureAwait(false);
                if (completed != wait) return false;

                try
                {
                    ClearBytes(frameKey);
                    ClearBytes(serviceKey);
                    return true;
                }
                finally
                {
                    requestGate.Release();
                }
            }

            internal PipeEnvelope CreateBusinessEnvelope(string messageType, string payloadSchema, string payloadJson, TaskScopeEnvelope taskScope, RequestContext context, DateTimeOffset deadline, string sessionIdOverride = null)
            {
                if (string.IsNullOrWhiteSpace(messageType)) throw new ArgumentException("message_type_required", nameof(messageType));
                if (string.IsNullOrWhiteSpace(payloadSchema)) throw new ArgumentException("payload_schema_required", nameof(payloadSchema));
                if (taskScope == null) throw new ArgumentNullException(nameof(taskScope));
                if (context == null || context.Session == null || !context.Session.IsCampaign) throw new ArgumentException("campaign_session_required", nameof(context));
                var payload = RuntimeServiceClient.CanonicalizePayload(payloadJson);
                var payloadBytes = Encoding.UTF8.GetBytes(payload);
                var payloadHash = TransportSecurity.Sha256Hex(payloadBytes);
                var envelope = new PipeEnvelope
                {
                    MessageType = messageType,
                    MessageId = taskScope.MessageId,
                    RequestId = taskScope.MessageId,
                    CorrelationId = context.CorrelationId,
                    CausationId = taskScope.MessageId,
                    CampaignGuid = context.Session.CampaignGuid,
                    TimelineId = context.Session.TimelineId,
                    SessionId = string.IsNullOrWhiteSpace(sessionIdOverride) ? context.Session.SessionId : sessionIdOverride,
                    SessionGeneration = context.SessionGeneration,
                    OwnerId = context.Caller.Value,
                    DeadlineUnixMilliseconds = deadline.ToUnixTimeMilliseconds(),
                    InstanceEpoch = descriptor.InstanceEpoch,
                    ConnectionEpoch = handshakeResponse.ConnectionEpoch,
                    DirectionNonce = handshakeResponse.ClientDirectionNonce,
                    Sequence = Interlocked.Increment(ref nextSequence),
                    FenceProof = string.Empty,
                    PayloadSchema = payloadSchema,
                    PayloadJson = payload,
                    PayloadLength = payloadBytes.Length,
                    PayloadSha256 = payloadHash,
                    Checksum = payloadHash,
                    ChecksumAlgorithm = ProtocolConstants.ChecksumAlgorithm,
                    NonDurable = false,
                    TaskScope = taskScope
                };
                envelope.FenceProof = TransportSecurity.ComputeFrameFenceProof(frameKey, envelope);
                return envelope;
            }

            internal async Task<PipeEnvelope> SendBusinessAsync(PipeEnvelope request, DateTimeOffset deadline, CancellationToken lifecycleToken, CancellationToken callerToken)
            {
                if (request == null) throw new ArgumentNullException(nameof(request));
                var remaining = deadline - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero) throw new TimeoutException();
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifecycleToken, callerToken))
                {
                    timeout.CancelAfter(remaining);
                    var entered = false;
                    try
                    {
                        await requestGate.WaitAsync(timeout.Token).ConfigureAwait(false);
                        entered = true;
                        if (Volatile.Read(ref aborted) != 0) throw new IOException("runtime_pipe_closed");

                        await PipeFrameIO.WriteFrameAsync(pipe, ProtocolCodec.SerializeEnvelope(request), timeout.Token).ConfigureAwait(false);
                        var rawResponse = await PipeFrameIO.ReadFrameAsync(pipe, timeout.Token).ConfigureAwait(false);
                        if (Encoding.UTF8.GetByteCount(rawResponse) > maximumFrameBytes) throw new ClientProtocolException("response_frame_limit_exceeded");

                        if (!ProtocolCodec.TryParseEnvelope(rawResponse, out var response, out var error)) throw new ClientProtocolException("response_decode_" + error);
                        ValidateBusinessResponse(request, response);
                        return response;
                    }
                    catch (OperationCanceledException)
                    {
                        if (lifecycleToken.IsCancellationRequested || callerToken.IsCancellationRequested) throw;
                        throw new TimeoutException();
                    }
                    finally
                    {
                        if (entered) requestGate.Release();
                    }
                }
            }

            internal async Task<IReadOnlyList<ProviderStreamEventWire>> SendBusinessStreamAsync(PipeEnvelope request, DateTimeOffset deadline, CancellationToken lifecycleToken, CancellationToken callerToken, RuntimeResourceBudget streamBudget)
            {
                if (request == null) throw new ArgumentNullException(nameof(request));
                if (streamBudget == null || streamBudget.FrameCount < 2) throw new ClientProtocolException("stream_budget_invalid");
                var remaining = deadline - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero) throw new TimeoutException();
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifecycleToken, callerToken))
                {
                    timeout.CancelAfter(remaining);
                    var entered = false;
                    try
                    {
                        await requestGate.WaitAsync(timeout.Token).ConfigureAwait(false);
                        entered = true;
                        if (Volatile.Read(ref aborted) != 0) throw new IOException("runtime_pipe_closed");

                        await PipeFrameIO.WriteFrameAsync(pipe, ProtocolCodec.SerializeEnvelope(request), timeout.Token).ConfigureAwait(false);
                        var scope = request.TaskScope;
                        if (scope == null) throw new ClientProtocolException("stream_task_scope_missing");
                        var stateMachine = new ProviderStreamStateMachine(scope.TaskId, scope.ProviderId, scope.ProfileId);
                        var streamEvents = new List<ProviderStreamEventWire>();
                        var budgetTracker = new RuntimeStreamBudgetTracker(streamBudget);
                        while (true)
                        {
                            var rawResponse = await PipeFrameIO.ReadFrameAsync(pipe, timeout.Token).ConfigureAwait(false);
                            if (Encoding.UTF8.GetByteCount(rawResponse) > maximumFrameBytes) throw new ClientProtocolException("response_frame_limit_exceeded");
                            if (!ProtocolCodec.TryParseEnvelope(rawResponse, out var response, out var error)) throw new ClientProtocolException("response_decode_" + error);
                            ValidateBusinessResponse(request, response);
                            if (StringComparer.Ordinal.Equals(response.MessageType, ProtocolConstants.MessageTypeError))
                            {
                                if (!ProviderRuntimeWire.TryReadError(response.PayloadJson, out var providerError)) throw new ClientProtocolException("provider_error_decode_failed");
                                throw new ProviderResponseException(providerError);
                            }
                            if (!StringComparer.Ordinal.Equals(response.MessageType, ProtocolConstants.MessageTypeProviderStreamEvent) || !StringComparer.Ordinal.Equals(response.PayloadSchema, ProviderRuntimeSchema.StreamEvent)) throw new ClientProtocolException("provider_stream_response_identity_invalid");
                            if (!ProviderRuntimeWire.TryReadStreamEvent(response.PayloadJson, new ProviderScopeRequest(scope.ProfileId, scope.ProviderId, scope.RouteId), out var streamEvent, out var streamError)) throw new ClientProtocolException("provider_stream_response_" + streamError);
                            if (streamEvent.StreamSequence > streamBudget.FrameCount) throw new ClientProtocolException("stream_budget_exhausted");

                            var terminal = streamEvent.EventKind == "completed" || streamEvent.EventKind == "cancelled" || streamEvent.EventKind == "failed";
                            var decision = stateMachine.Accept(streamEvent.EventKind, streamEvent.StreamSequence, streamEvent.ProviderId, streamEvent.FromProviderId ?? string.Empty, streamEvent.ToProviderId ?? string.Empty, terminal, streamEvent.Text != null, streamEvent.Usage != null, streamEvent.Error != null, streamEvent.StructuredJson != null);
                            if (!decision.IsAccepted) throw new ClientProtocolException(decision.ErrorCode);
                            if (!budgetTracker.Accept(streamEvent, out var budgetError)) throw new ClientProtocolException(budgetError);

                            streamEvents.Add(streamEvent);
                            if (terminal) return streamEvents.AsReadOnly();
                        }
                    }
                    catch (EndOfStreamException)
                    {
                        throw new ClientProtocolException("stream_incomplete");
                    }
                    catch (OperationCanceledException)
                    {
                        if (lifecycleToken.IsCancellationRequested || callerToken.IsCancellationRequested) throw;
                        throw new TimeoutException();
                    }
                    finally
                    {
                        if (entered) requestGate.Release();
                    }
                }
            }

            internal async Task<OperationResult<bool>> SendControlCancelAsync(TaskScopeEnvelope targetScope, RequestContext context, DateTimeOffset deadline, CancellationToken callerToken)
            {
                if (targetScope == null || context == null || context.Session == null) return RuntimeServiceClient.Failure<bool>("runtime.invalid_request", FrameworkErrorCategory.InvalidRequest, "A cancellation target and context are required.", context == null ? "runtime-cancel" : context.CorrelationId);
                if (callerToken == CancellationToken.None) return RuntimeServiceClient.Failure<bool>("runtime.cancellation_token_missing", FrameworkErrorCategory.InvalidRequest, "A cancellation token is required.", context.CorrelationId);
                if (callerToken.IsCancellationRequested) return RuntimeServiceClient.Failure<bool>("runtime.cancelled", FrameworkErrorCategory.Cancelled, "The cancellation was cancelled.", context.CorrelationId);

                var remaining = deadline - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero) return RuntimeServiceClient.Failure<bool>("runtime.cancel_timeout", FrameworkErrorCategory.Timeout, "The cancellation request timed out.", context.CorrelationId);

                var entered = false;
                try
                {
                    await controlConnectionGate.WaitAsync(callerToken).ConfigureAwait(false);
                    entered = true;
                    if (Volatile.Read(ref aborted) != 0) return RuntimeServiceClient.Failure<bool>("runtime.cancel_unavailable", FrameworkErrorCategory.Unavailable, "The runtime service connection is unavailable.", context.CorrelationId);

                    var nextConnectionEpoch = lastConnectionEpoch + 1;
                    var controlNonce = TransportSecurity.RandomHex(8);
                    var controlSessionId = handshakeRequest.SessionId + ".cancel." + controlNonce;
                    var challengeId = descriptor.ChallengeId + ".connection." + nextConnectionEpoch.ToString();
                    var requestedCapabilities = new[] { ProtocolConstants.MessageTypeHealth, ProtocolConstants.MessageTypeCancel };
                    var controlHandshakeRequest = owner.CreateHandshakeRequest(descriptor, ready, context, maximumFrameBytes, serviceKey, controlSessionId, challengeId, requestedCapabilities);
                    NamedPipeClientStream controlPipe = null;
                    ClientConnection controlConnection = null;
                    try
                    {
                        controlPipe = new NamedPipeClientStream(".", descriptor.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                        await RuntimeServiceClient.ConnectWithTimeoutAsync(controlPipe, owner.options.ConnectTimeoutMilliseconds, callerToken).ConfigureAwait(false);
                        await RuntimeServiceClient.WriteFrameWithTimeoutAsync(controlPipe, ProtocolCodec.SerializeHandshakeRequest(controlHandshakeRequest), owner.options.HandshakeTimeoutMilliseconds, callerToken).ConfigureAwait(false);
                        var handshakeJson = await RuntimeServiceClient.ReadFrameWithTimeoutAsync(controlPipe, owner.options.HandshakeTimeoutMilliseconds, callerToken).ConfigureAwait(false);
                        if (!ProtocolCodec.TryParseHandshakeResponse(handshakeJson, out var controlHandshakeResponse, out var handshakeError)) throw new ClientProtocolException("control_handshake_response_" + handshakeError);
                        var effectiveFrameBytes = owner.ValidateHandshakeResponse(descriptor, ready, controlHandshakeRequest, controlHandshakeResponse, serviceKey, maximumFrameBytes);
                        if (controlHandshakeResponse.ConnectionEpoch != nextConnectionEpoch) throw new ClientProtocolException("control_connection_epoch_invalid");
                        lastConnectionEpoch = controlHandshakeResponse.ConnectionEpoch;
                        controlConnection = new ClientConnection(owner, controlPipe, descriptor, ready, controlHandshakeRequest, controlHandshakeResponse, frameKey, serviceKey, effectiveFrameBytes);
                        controlPipe = null;

                        var cancelMessageId = "cancel-" + Guid.NewGuid().ToString("N");
                        var cancelScope = new TaskScopeEnvelope
                        {
                            TaskId = targetScope.TaskId,
                            MessageId = cancelMessageId,
                            OwnerId = targetScope.OwnerId,
                            RouteId = targetScope.RouteId,
                            ProviderId = targetScope.ProviderId,
                            ProfileId = targetScope.ProfileId,
                            IdempotencyKey = targetScope.IdempotencyKey,
                            RequestPayloadHash = targetScope.RequestPayloadHash,
                            OutputSchemaId = targetScope.OutputSchemaId,
                            OutputSchemaMajor = targetScope.OutputSchemaMajor,
                            OutputSchemaMinor = targetScope.OutputSchemaMinor,
                            SettlementRequirement = targetScope.SettlementRequirement
                        };
                        var payload = "{\"schema\":\"marcus-awake.cancel.v1\",\"task_id\":" + RuntimeServiceClient.QuoteJson(targetScope.TaskId) + ",\"target_session_id\":" + RuntimeServiceClient.QuoteJson(handshakeRequest.SessionId) + "}";
                        var request = controlConnection.CreateBusinessEnvelope(ProtocolConstants.MessageTypeCancel, "marcus-awake.cancel.v1", payload, cancelScope, context, deadline, controlSessionId);
                        var response = await controlConnection.SendBusinessAsync(request, deadline, CancellationToken.None, callerToken).ConfigureAwait(false);
                        if (StringComparer.Ordinal.Equals(response.MessageType, ProtocolConstants.MessageTypeError)) return RuntimeServiceClient.Failure<bool>("runtime.cancel_unavailable", FrameworkErrorCategory.Unavailable, "The runtime service rejected the cancellation request.", context.CorrelationId);
                        if (!StringComparer.Ordinal.Equals(response.MessageType, ProtocolConstants.MessageTypeCancelAck) || !StringComparer.Ordinal.Equals(response.PayloadSchema, "marcus-awake.cancel.v1")) throw new ClientProtocolException("control_cancel_response_identity_invalid");
                        if (!TryReadCancelStatus(response.PayloadJson, out var status)) throw new ClientProtocolException("control_cancel_status_invalid");
                        if (StringComparer.Ordinal.Equals(status, "cancel_requested")) return OperationResult<bool>.Succeeded(true);
                        if (StringComparer.Ordinal.Equals(status, "already_completed")) return OperationResult<bool>.Succeeded(false);
                        if (StringComparer.Ordinal.Equals(status, "not_found")) return OperationResult<bool>.Succeeded(false);
                        throw new ClientProtocolException("control_cancel_status_unknown");
                    }
                    finally
                    {
                        if (controlConnection != null) await controlConnection.CloseAsync(owner.options.DrainTimeoutMilliseconds).ConfigureAwait(false);
                        if (controlPipe != null)
                        {
                            try { controlPipe.Dispose(); } catch (ObjectDisposedException) { }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return RuntimeServiceClient.Failure<bool>("runtime.cancelled", FrameworkErrorCategory.Cancelled, "The cancellation was cancelled.", context.CorrelationId);
                }
                catch (TimeoutException)
                {
                    return RuntimeServiceClient.Failure<bool>("runtime.cancel_timeout", FrameworkErrorCategory.Timeout, "The cancellation request timed out.", context.CorrelationId);
                }
                catch (ClientProtocolException exception)
                {
                    return RuntimeServiceClient.Failure<bool>("runtime.cancel_protocol", FrameworkErrorCategory.Incompatible, "The runtime service rejected the cancellation protocol.", context.CorrelationId, exception.Code);
                }
                catch (IOException)
                {
                    return RuntimeServiceClient.Failure<bool>("runtime.cancel_unavailable", FrameworkErrorCategory.Unavailable, "The runtime service connection is unavailable.", context.CorrelationId);
                }
                finally
                {
                    if (entered) controlConnectionGate.Release();
                }
            }

            internal async Task SendHealthAsync(RequestContext context, int timeoutMilliseconds, CancellationToken lifecycleToken, CancellationToken callerToken)
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifecycleToken, callerToken))
                {
                    timeout.CancelAfter(timeoutMilliseconds);
                    var entered = false;
                    try
                    {
                        await requestGate.WaitAsync(timeout.Token).ConfigureAwait(false);
                        entered = true;
                        if (Volatile.Read(ref aborted) != 0) throw new IOException("runtime_pipe_closed");

                        var request = CreateHealthEnvelope(context, timeoutMilliseconds);
                        await PipeFrameIO.WriteFrameAsync(pipe, ProtocolCodec.SerializeEnvelope(request), timeout.Token).ConfigureAwait(false);
                        var rawResponse = await PipeFrameIO.ReadFrameAsync(pipe, timeout.Token).ConfigureAwait(false);
                        if (Encoding.UTF8.GetByteCount(rawResponse) > maximumFrameBytes) throw new ClientProtocolException("response_frame_limit_exceeded");

                        PipeEnvelope response;
                        string error;
                        if (!ProtocolCodec.TryParseEnvelope(rawResponse, out response, out error)) throw new ClientProtocolException("response_decode_" + error);
                        ValidateHealthResponse(request, response);
                    }
                    catch (OperationCanceledException)
                    {
                        if (lifecycleToken.IsCancellationRequested || callerToken.IsCancellationRequested) throw;
                        throw new TimeoutException();
                    }
                    finally
                    {
                        if (entered) requestGate.Release();
                    }
                }
            }

            private PipeEnvelope CreateHealthEnvelope(RequestContext context, int timeoutMilliseconds)
            {
                var now = DateTimeOffset.UtcNow;
                var deadline = context.Deadline;
                var localDeadline = now.AddMilliseconds(timeoutMilliseconds);
                if (deadline > localDeadline) deadline = localDeadline;
                var payloadBytes = Encoding.UTF8.GetBytes(HealthPayload);
                var messageId = "health-" + Guid.NewGuid().ToString("N");
                var envelope = new PipeEnvelope
                {
                    MessageType = ProtocolConstants.MessageTypeHealth,
                    MessageId = messageId,
                    RequestId = messageId,
                    CorrelationId = context.CorrelationId + ".health." + Guid.NewGuid().ToString("N"),
                    CausationId = string.Empty,
                    CampaignGuid = context.Session.CampaignGuid,
                    TimelineId = context.Session.TimelineId,
                    SessionId = context.Session.SessionId,
                    SessionGeneration = context.SessionGeneration,
                    OwnerId = context.Caller.Value,
                    DeadlineUnixMilliseconds = deadline.ToUnixTimeMilliseconds(),
                    InstanceEpoch = descriptor.InstanceEpoch,
                    ConnectionEpoch = handshakeResponse.ConnectionEpoch,
                    DirectionNonce = handshakeResponse.ClientDirectionNonce,
                    Sequence = Interlocked.Increment(ref nextSequence),
                    FenceProof = string.Empty,
                    PayloadSchema = HealthPayloadSchema,
                    PayloadJson = HealthPayload,
                    PayloadLength = payloadBytes.Length,
                    PayloadSha256 = TransportSecurity.Sha256Hex(payloadBytes),
                    Checksum = TransportSecurity.Sha256Hex(payloadBytes),
                    ChecksumAlgorithm = ProtocolConstants.ChecksumAlgorithm,
                    NonDurable = false
                };
                envelope.FenceProof = TransportSecurity.ComputeFrameFenceProof(frameKey, envelope);
                return envelope;
            }

            private void ValidateHealthResponse(PipeEnvelope request, PipeEnvelope response)
            {
                if (!StringComparer.Ordinal.Equals(response.ProtocolId, ProtocolConstants.ProtocolId) || response.ProtocolMajor != ProtocolConstants.ProtocolMajor || response.ProtocolMinor != ProtocolConstants.ProtocolMinor) throw new ClientProtocolException("response_protocol_invalid");
                if (!StringComparer.Ordinal.Equals(response.MessageType, ProtocolConstants.MessageTypeHealthAck)) throw new ClientProtocolException("response_message_type_invalid");
                if (!StringComparer.Ordinal.Equals(response.MessageId, request.MessageId) || !StringComparer.Ordinal.Equals(response.RequestId, request.RequestId)) throw new ClientProtocolException("response_message_id_invalid");
                if (!StringComparer.Ordinal.Equals(response.CorrelationId, request.CorrelationId) || !StringComparer.Ordinal.Equals(response.CausationId, request.MessageId)) throw new ClientProtocolException("response_correlation_invalid");
                if (!StringComparer.Ordinal.Equals(response.CampaignGuid, request.CampaignGuid) || !StringComparer.Ordinal.Equals(response.TimelineId, request.TimelineId) || !StringComparer.Ordinal.Equals(response.SessionId, request.SessionId) || response.SessionGeneration != request.SessionGeneration) throw new ClientProtocolException("response_session_fence_invalid");
                if (!StringComparer.Ordinal.Equals(response.OwnerId, request.OwnerId)) throw new ClientProtocolException("response_owner_invalid");
                if (response.InstanceEpoch != descriptor.InstanceEpoch || response.ConnectionEpoch != handshakeResponse.ConnectionEpoch) throw new ClientProtocolException("response_epoch_invalid");
                if (!StringComparer.Ordinal.Equals(response.DirectionNonce, handshakeResponse.ServiceDirectionNonce)) throw new ClientProtocolException("response_nonce_invalid");
                if (!StringComparer.Ordinal.Equals(response.PayloadSchema, HealthPayloadSchema) || !StringComparer.Ordinal.Equals(response.PayloadJson, HealthAckPayload)) throw new ClientProtocolException("response_health_payload_invalid");
                if (!response.NonDurable || !StringComparer.Ordinal.Equals(response.AckStatus, ProtocolConstants.AckAccepted) || !StringComparer.Ordinal.Equals(response.OutcomeKind, ProtocolConstants.OutcomeAccepted) || !string.IsNullOrWhiteSpace(response.ErrorCode) || response.TaskScope != null) throw new ClientProtocolException("response_ack_invalid");

                var payloadBytes = Encoding.UTF8.GetBytes(response.PayloadJson);
                var payloadHash = TransportSecurity.Sha256Hex(payloadBytes);
                if (response.PayloadLength != payloadBytes.Length || !TransportSecurity.FixedTimeEquals(response.PayloadSha256, payloadHash) || !TransportSecurity.FixedTimeEquals(response.Checksum, payloadHash)) throw new ClientProtocolException("response_integrity_invalid");
                var expectedFence = TransportSecurity.ComputeFrameFenceProof(frameKey, response);
                if (!TransportSecurity.FixedTimeEquals(expectedFence, response.FenceProof)) throw new ClientProtocolException("response_fence_invalid");
                var sequence = responseWindow.Evaluate(response);
                if (sequence.Kind != SequenceDecisionKind.Accepted) throw new ClientProtocolException("response_sequence_" + sequence.ErrorCode);
            }

            private void ValidateBusinessResponse(PipeEnvelope request, PipeEnvelope response)
            {
                if (response == null) throw new ClientProtocolException("response_missing");
                if (!StringComparer.Ordinal.Equals(response.ProtocolId, ProtocolConstants.ProtocolId) || response.ProtocolMajor != ProtocolConstants.ProtocolMajor || response.ProtocolMinor != ProtocolConstants.ProtocolMinor) throw new ClientProtocolException("response_protocol_invalid");
                if (!StringComparer.Ordinal.Equals(response.MessageId, request.MessageId) || !StringComparer.Ordinal.Equals(response.RequestId, request.RequestId)) throw new ClientProtocolException("response_message_id_invalid");
                if (!StringComparer.Ordinal.Equals(response.CorrelationId, request.CorrelationId) || !StringComparer.Ordinal.Equals(response.CausationId, request.MessageId)) throw new ClientProtocolException("response_correlation_invalid");
                if (!StringComparer.Ordinal.Equals(response.CampaignGuid, request.CampaignGuid) || !StringComparer.Ordinal.Equals(response.TimelineId, request.TimelineId) || !StringComparer.Ordinal.Equals(response.SessionId, request.SessionId) || response.SessionGeneration != request.SessionGeneration) throw new ClientProtocolException("response_session_fence_invalid");
                if (!StringComparer.Ordinal.Equals(response.OwnerId, request.OwnerId)) throw new ClientProtocolException("response_owner_invalid");
                if (response.InstanceEpoch != descriptor.InstanceEpoch || response.ConnectionEpoch != handshakeResponse.ConnectionEpoch) throw new ClientProtocolException("response_epoch_invalid");
                if (!StringComparer.Ordinal.Equals(response.DirectionNonce, handshakeResponse.ServiceDirectionNonce)) throw new ClientProtocolException("response_nonce_invalid");
                if (request.TaskScope == null || response.TaskScope == null || !SameTaskScope(request.TaskScope, response.TaskScope)) throw new ClientProtocolException("response_task_scope_invalid");

                var payloadBytes = Encoding.UTF8.GetBytes(response.PayloadJson ?? string.Empty);
                var payloadHash = TransportSecurity.Sha256Hex(payloadBytes);
                if (response.PayloadLength != payloadBytes.Length || !TransportSecurity.FixedTimeEquals(response.PayloadSha256, payloadHash) || !TransportSecurity.FixedTimeEquals(response.Checksum, payloadHash)) throw new ClientProtocolException("response_integrity_invalid");
                var expectedFence = TransportSecurity.ComputeFrameFenceProof(frameKey, response);
                if (!TransportSecurity.FixedTimeEquals(expectedFence, response.FenceProof)) throw new ClientProtocolException("response_fence_invalid");
                var sequence = responseWindow.Evaluate(response);
                if (sequence.Kind != SequenceDecisionKind.Accepted) throw new ClientProtocolException("response_sequence_" + sequence.ErrorCode);
            }

            private static bool SameTaskScope(TaskScopeEnvelope left, TaskScopeEnvelope right)
            {
                return StringComparer.Ordinal.Equals(left.TaskId, right.TaskId)
                    && StringComparer.Ordinal.Equals(left.MessageId, right.MessageId)
                    && StringComparer.Ordinal.Equals(left.OwnerId, right.OwnerId)
                    && StringComparer.Ordinal.Equals(left.RouteId, right.RouteId)
                    && StringComparer.Ordinal.Equals(left.ProviderId, right.ProviderId)
                    && StringComparer.Ordinal.Equals(left.ProfileId, right.ProfileId)
                    && StringComparer.Ordinal.Equals(left.IdempotencyKey, right.IdempotencyKey)
                    && StringComparer.Ordinal.Equals(left.RequestPayloadHash, right.RequestPayloadHash)
                    && StringComparer.Ordinal.Equals(left.OutputSchemaId, right.OutputSchemaId)
                    && left.OutputSchemaMajor == right.OutputSchemaMajor
                    && left.OutputSchemaMinor == right.OutputSchemaMinor
                    && StringComparer.Ordinal.Equals(left.SettlementRequirement, right.SettlementRequirement);
            }

            private static bool TryReadCancelStatus(string payload, out string status)
            {
                status = string.Empty;
                if (string.IsNullOrWhiteSpace(payload)) return false;
                const string prefix = "\"status\":\"";
                var start = payload.IndexOf(prefix, StringComparison.Ordinal);
                if (start < 0) return false;
                start += prefix.Length;
                var end = payload.IndexOf('"', start);
                if (end <= start) return false;
                status = payload.Substring(start, end - start);
                return true;
            }

            private static byte[] CopyBytes(byte[] value)
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                var copy = new byte[value.Length];
                Buffer.BlockCopy(value, 0, copy, 0, value.Length);
                return copy;
            }
    }

    internal sealed class RuntimeStreamBudgetTracker
    {
        private readonly RuntimeResourceBudget budget;
        private long outputBytes;
        private int textDeltas;
        private int inputTokens;
        private int outputTokens;

        internal RuntimeStreamBudgetTracker(RuntimeResourceBudget budget)
        {
            this.budget = budget ?? throw new ArgumentNullException(nameof(budget));
        }

        internal long OutputBytes => outputBytes;
        internal int TextDeltas => textDeltas;
        internal int InputTokens => inputTokens;
        internal int OutputTokens => outputTokens;

        internal bool Accept(ProviderStreamEventWire streamEvent, out string error)
        {
            error = string.Empty;
            if (streamEvent == null)
            {
                error = "stream_event_missing";
                return false;
            }

            var nextOutputBytes = outputBytes;
            var nextTextDeltas = textDeltas;
            var nextInputTokens = inputTokens;
            var nextOutputTokens = outputTokens;
            if (streamEvent.Text != null)
            {
                nextOutputBytes += Encoding.UTF8.GetByteCount(streamEvent.Text);
                nextTextDeltas++;
            }
            if (streamEvent.StructuredJson != null) nextOutputBytes += Encoding.UTF8.GetByteCount(streamEvent.StructuredJson);
            if (streamEvent.Usage != null)
            {
                if (streamEvent.Usage.InputTokens.HasValue && streamEvent.Usage.InputTokens.Value < inputTokens || streamEvent.Usage.OutputTokens.HasValue && streamEvent.Usage.OutputTokens.Value < outputTokens)
                {
                    error = "usage_non_monotonic";
                    return false;
                }
                if (streamEvent.Usage.InputTokens.HasValue) nextInputTokens = streamEvent.Usage.InputTokens.Value;
                if (streamEvent.Usage.OutputTokens.HasValue) nextOutputTokens = streamEvent.Usage.OutputTokens.Value;
            }

            if (nextOutputBytes > budget.OutputBytes || nextTextDeltas > budget.TextDeltas || (long)nextInputTokens + nextOutputTokens > budget.Tokens)
            {
                error = "stream_budget_exhausted";
                return false;
            }

            outputBytes = nextOutputBytes;
            textDeltas = nextTextDeltas;
            inputTokens = nextInputTokens;
            outputTokens = nextOutputTokens;
            return true;
        }
    }

}
}
