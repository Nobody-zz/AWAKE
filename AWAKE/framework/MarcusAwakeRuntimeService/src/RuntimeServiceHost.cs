using MarcusAwakeTransport;
using MarcusAwakeFramework.Api;
using MarcusAwakeStorage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Http;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeRuntimeService;

[SupportedOSPlatform("windows")]
internal sealed class RuntimeServiceHost
{
    private const int ParentPollMilliseconds = 150;
    private const int DrainTimeoutMilliseconds = 1500;
    private const int MaximumConcurrentFrames = 8;
    private const int MaximumPipeInstances = 8;
    private const int MaximumPendingCancellations = 256;
    private const int MaximumRequestLedgerEntries = 512;
    private const int MaximumProviderLedgerEntries = 256;
    private static readonly TimeSpan RequestLedgerTtl = TimeSpan.FromMinutes(15);
    private const int MaximumCommittedStreamLedgers = 64;
    private const long MaximumStreamReplayBytes = 16L * 1024L * 1024L;
    private static readonly TimeSpan StreamReplayTtl = TimeSpan.FromMinutes(15);
    private const string BootstrapAuthDomain = "marcus-awake-runtime";
    private const string FrameAuthDomain = "marcus-awake-runtime-frame";
    private const string ModelRootEnvironmentVariable = "MARCUS_AWAKE_MODEL_ROOT";
    private const string EmbeddingModelNameEnvironmentVariable = "MARCUS_AWAKE_EMBEDDING_MODEL";
    private const string DefaultEmbeddingModelName = "bge-small-zh-v1.5";
    /// <summary>
    /// Bump this whenever the passage text the game composes changes. It is part of the model
    /// identity stamped on every stored vector, so a change invalidates the whole cache instead of
    /// silently mixing vectors built from two different compositions
    /// (see docs/FEED-20260917-怎么喂与要不要自训.md §2).
    /// </summary>
    private const string EmbeddingPassageRevision = "feed-D-v1";
    private const string RuntimeTestModeEnvironmentVariable = "MARCUS_AWAKE_RUNTIME_TEST_MODE";
    private const string CrashAfterCommitMessageEnvironmentVariable = "MARCUS_AWAKE_TEST_CRASH_AFTER_COMMIT_BEFORE_RESPONSE";
    private const string RuntimeTestNowEnvironmentVariable = "MARCUS_AWAKE_RUNTIME_TEST_NOW_UNIX_MS";
    private const string RuntimeTestSuppressBeforeWriteMessageEnvironmentVariable = "MARCUS_AWAKE_TEST_SUPPRESS_BEFORE_FIRST_BYTE_MESSAGE_ID";
    private const string RuntimeTestLateStreamEventMessageEnvironmentVariable = "MARCUS_AWAKE_TEST_LATE_STREAM_EVENT_MESSAGE_ID";

    private static readonly string[] SupportedCapabilities =
    {
        "health",
        "echo",
        "cancel",
        "diagnostic",
        "shutdown",
        ProtocolConstants.CapabilityStorageRead,
        ProtocolConstants.CapabilityStorageWrite,
        ProtocolConstants.CapabilityRagRead,
        ProtocolConstants.CapabilityRagWrite,
        ProtocolConstants.CapabilityProviderConfigureV1,
        ProtocolConstants.CapabilityProviderCredentialsV1,
        ProtocolConstants.CapabilityProviderModelsV1,
        ProtocolConstants.CapabilityProviderCompleteV1,
        ProtocolConstants.CapabilityProviderStreamV1
    };

    private readonly object sync = new object();
    private readonly CancellationTokenSource lifecycle = new CancellationTokenSource();
    private readonly Dictionary<string, ActiveTask> activeTasks = new Dictionary<string, ActiveTask>(StringComparer.Ordinal);
    private readonly Dictionary<string, LedgerEntry> messageLedger = new Dictionary<string, LedgerEntry>(StringComparer.Ordinal);
    private readonly Dictionary<string, LedgerEntry> taskLedger = new Dictionary<string, LedgerEntry>(StringComparer.Ordinal);
    private readonly Dictionary<string, ProviderIdempotencyEntry> providerIdempotency = new Dictionary<string, ProviderIdempotencyEntry>(StringComparer.Ordinal);
    private readonly Dictionary<string, PendingCancellation> pendingCancellations = new Dictionary<string, PendingCancellation>(StringComparer.Ordinal);
    private readonly Dictionary<string, SuppressedMarker> suppressedMarkers = new Dictionary<string, SuppressedMarker>(StringComparer.Ordinal);
    private readonly LinkedList<LedgerEntry> committedStreamLedgers = new LinkedList<LedgerEntry>();
    private readonly HashSet<string> consumedChallenges = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> acceptedSessions = new HashSet<string>(StringComparer.Ordinal);

    private BootstrapDescriptor descriptor = null!;
    private byte[] bootstrapKeyMaterial = Array.Empty<byte>();
    private byte[] serviceKey = Array.Empty<byte>();
    private byte[] frameKey = Array.Empty<byte>();
    private string currentUserSid = string.Empty;
    private string serviceInstanceId = string.Empty;
    private string serviceArtifactSha256 = string.Empty;
    private string parentStartProof = string.Empty;
    private string serviceBootstrapProof = string.Empty;
    private long serviceStartUnixMilliseconds;
    private long nextConnectionEpoch;
    private long reservedStreamReplayBytes;
    private long committedStreamReplayBytes;
    private bool testSuppressionConsumed;
    private LifecycleState state = LifecycleState.Accepting;
    private Task? parentMonitor;
    private SqliteStorageAndRagBackend? storageBackend;
    private object? providerCredentialStore;
    private HttpMessageInvoker? providerInvoker;
    private ProviderRegistry? providerRegistry;
    private RuntimeProviderOutcomeLedger? providerOutcomeLedger;

    internal RuntimeServiceHost()
    {
    }

    internal async Task<int> RunAsync()
    {
        if (!OperatingSystem.IsWindows()) throw new ServiceStartupException("windows_required", 3);

        try
        {
            descriptor = await ReadBootstrapAsync(Console.OpenStandardInput(), lifecycle.Token).ConfigureAwait(false);
            InitializeBootstrap();
            storageBackend = new SqliteStorageAndRagBackend(ResolveStorageDatabasePath(), null, CreateEmbedder());
            providerOutcomeLedger = new RuntimeProviderOutcomeLedger(ResolveProviderOutcomeLedgerPath());
            if (!providerOutcomeLedger.Load(out var ledgerLoadError))
            {
                Console.Error.WriteLine("provider_outcome_ledger_unavailable:" + ledgerLoadError);
                Console.Error.Flush();
                providerOutcomeLedger = null;
            }
            InitializeProviderRuntime();
            await WriteServiceReadyAsync(Console.OpenStandardOutput(), lifecycle.Token).ConfigureAwait(false);
        }
        catch
        {
            DisposeProviderRuntime();
            ClearSecrets();
            throw;
        }

        parentMonitor = MonitorParentAsync(lifecycle.Token);
        try
        {
            await AcceptConnectionsAsync(lifecycle.Token).ConfigureAwait(false);
        }
        finally
        {
            BeginDrain("service_exit");
            if (parentMonitor != null)
            {
                try
                {
                    await parentMonitor.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            ClearSecrets();
            DisposeProviderRuntime();
            if (storageBackend != null)
            {
                try
                {
                    await storageBackend.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine("storage_dispose_failed:" + exception.GetType().Name);
                    Console.Error.Flush();
                }
                storageBackend = null;
            }
            state = LifecycleState.Stopped;
            lifecycle.Dispose();
        }

        return 0;
    }

    private async Task<BootstrapDescriptor> ReadBootstrapAsync(Stream descriptorPipe, CancellationToken cancellationToken)
    {
        string json;
        try
        {
            json = await PipeFrameIO.ReadFrameAsync(descriptorPipe, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is EndOfStreamException || exception is IOException || exception is InvalidDataException)
        {
            throw new ServiceStartupException("descriptor_read_failed", 4);
        }

        BootstrapDescriptor bootstrap;
        string error;
        if (!ProtocolCodec.TryParseBootstrap(json, out bootstrap, out error)) throw new ServiceStartupException("descriptor_" + error, 4);

        string keyMaterialBase64;
        try
        {
            keyMaterialBase64 = await PipeFrameIO.ReadFrameAsync(descriptorPipe, cancellationToken).ConfigureAwait(false);
            bootstrapKeyMaterial = Convert.FromBase64String(keyMaterialBase64);
        }
        catch (Exception exception) when (exception is FormatException || exception is EndOfStreamException || exception is IOException || exception is InvalidDataException)
        {
            throw new ServiceStartupException("descriptor_key_material_invalid", 4);
        }

        if (bootstrapKeyMaterial.Length != 32) throw new ServiceStartupException("descriptor_key_material_invalid", 4);

        return bootstrap;
    }

    private void InitializeBootstrap()
    {
        ValidateBootstrapShape();

        if (bootstrapKeyMaterial.Length != 32) throw new ServiceStartupException("descriptor_key_material_invalid", 4);

        try
        {
            serviceKey = TransportSecurity.DeriveAuthKey(bootstrapKeyMaterial, descriptor.LaunchNonce, BootstrapAuthDomain, descriptor.LaunchTransactionId, descriptor.AuthKeyId);
            frameKey = TransportSecurity.DeriveAuthKey(bootstrapKeyMaterial, descriptor.LaunchNonce, FrameAuthDomain, descriptor.LaunchTransactionId, descriptor.AuthKeyId);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bootstrapKeyMaterial);
            bootstrapKeyMaterial = Array.Empty<byte>();
        }

        currentUserSid = WindowsIdentityHelper.GetCurrentSid();
        var currentUserSidFingerprint = TransportSecurity.SidFingerprint(currentUserSid);
        if (!StringComparer.Ordinal.Equals(currentUserSidFingerprint, descriptor.UserSidFingerprint)) throw new ServiceStartupException("ipc_identity_mismatch", 5);

        var expectedPipeName = ProtocolConstants.PipePrefix + descriptor.UserSidFingerprint;
        if (!StringComparer.Ordinal.Equals(expectedPipeName, descriptor.PipeName)) throw new ServiceStartupException("pipe_identity_mismatch", 5);

        var actualParent = ProcessIdentity.ReadParent(Environment.ProcessId);
        if (!actualParent.IsValid || actualParent.ProcessId != descriptor.ParentProcessId) throw new ServiceStartupException("ipc_parent_invalid", 5);
        if (!ProcessIdentity.MatchesStartTime(descriptor.ParentProcessId, descriptor.ParentStartUnixMilliseconds)) throw new ServiceStartupException("ipc_parent_invalid", 5);

        serviceArtifactSha256 = ResolveServiceArtifactHash();
        if (!StringComparer.OrdinalIgnoreCase.Equals(serviceArtifactSha256, descriptor.ExpectedServiceArtifactSha256)) throw new ServiceStartupException("service_artifact_mismatch", 5);

        var expectedClientProof = TransportSecurity.ComputeClientBootstrapProof(descriptor, serviceKey);
        if (!TransportSecurity.FixedTimeEquals(expectedClientProof, descriptor.ClientBootstrapProof)) throw new ServiceStartupException("ipc_auth_failed", 5);

        var process = Process.GetCurrentProcess();
        serviceStartUnixMilliseconds = ProcessIdentity.ReadStartUnixMilliseconds(process);
        serviceInstanceId = TransportSecurity.RandomHex(16);
        parentStartProof = TransportSecurity.ComputeParentStartProof(descriptor, serviceKey, process.Id, serviceStartUnixMilliseconds, serviceArtifactSha256);
        serviceBootstrapProof = TransportSecurity.ComputeServiceBootstrapProof(descriptor, serviceKey, serviceInstanceId, process.Id, serviceStartUnixMilliseconds, serviceArtifactSha256, parentStartProof);
    }

    private void ValidateBootstrapShape()
    {
        if (!StringComparer.Ordinal.Equals(descriptor.ProtocolId, ProtocolConstants.ProtocolId) || descriptor.ProtocolMajor != ProtocolConstants.ProtocolMajor || descriptor.ProtocolMinor != ProtocolConstants.ProtocolMinor) throw new ServiceStartupException("protocol_incompatible", 5);
        if (descriptor.FrameworkApiMajor != ProtocolConstants.FrameworkApiMajor) throw new ServiceStartupException("framework_api_incompatible", 5);
        if (!StringComparer.Ordinal.Equals(descriptor.ServiceId, ProtocolConstants.ServiceId)) throw new ServiceStartupException("service_identity_mismatch", 5);
        if (string.IsNullOrWhiteSpace(descriptor.BannerlordApiVersion) || string.IsNullOrWhiteSpace(descriptor.ClientInstanceId) || string.IsNullOrWhiteSpace(descriptor.LaunchTransactionId) || string.IsNullOrWhiteSpace(descriptor.LaunchNonce) || string.IsNullOrWhiteSpace(descriptor.ChallengeId) || string.IsNullOrWhiteSpace(descriptor.SessionNonce) || string.IsNullOrWhiteSpace(descriptor.UserSidFingerprint) || string.IsNullOrWhiteSpace(descriptor.AuthKeyId) || string.IsNullOrWhiteSpace(descriptor.ExpectedServiceArtifactSha256) || string.IsNullOrWhiteSpace(descriptor.PipeName) || string.IsNullOrWhiteSpace(descriptor.ClientBootstrapProof)) throw new ServiceStartupException("descriptor_field_missing", 4);
        if (descriptor.ParentProcessId < 1 || descriptor.ParentStartUnixMilliseconds < 1 || descriptor.InstanceEpoch < 1) throw new ServiceStartupException("descriptor_identity_missing", 4);
        if (!StringComparer.Ordinal.Equals(descriptor.AuthKeyId, "bootstrap-v1")) throw new ServiceStartupException("auth_key_invalid", 5);
        if (descriptor.RequestedCapabilities == null || descriptor.RequestedCapabilities.Length == 0 || descriptor.RequestedCapabilities.Any(string.IsNullOrWhiteSpace) || descriptor.RequestedCapabilities.Distinct(StringComparer.Ordinal).Count() != descriptor.RequestedCapabilities.Length) throw new ServiceStartupException("capability_request_invalid", 4);
    }

    private async Task WriteServiceReadyAsync(Stream descriptorPipe, CancellationToken cancellationToken)
    {
        var ready = new ServiceReadyDescriptor
        {
            ProtocolId = ProtocolConstants.ProtocolId,
            ProtocolMajor = ProtocolConstants.ProtocolMajor,
            ProtocolMinor = ProtocolConstants.ProtocolMinor,
            FrameworkApiMajor = ProtocolConstants.FrameworkApiMajor,
            ServiceId = ProtocolConstants.ServiceId,
            ServiceInstanceId = serviceInstanceId,
            ServiceProcessId = Environment.ProcessId,
            ServiceStartUnixMilliseconds = serviceStartUnixMilliseconds,
            ServiceArtifactSha256 = serviceArtifactSha256,
            ParentStartProof = parentStartProof,
            ServiceBootstrapProof = serviceBootstrapProof,
            PipeName = descriptor.PipeName,
            InstanceEpoch = descriptor.InstanceEpoch,
            UserSidFingerprint = descriptor.UserSidFingerprint
        };

        await PipeFrameIO.WriteFrameAsync(descriptorPipe, ProtocolCodec.SerializeServiceReady(ready), cancellationToken).ConfigureAwait(false);
    }

    private async Task MonitorParentAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(ParentPollMilliseconds, cancellationToken).ConfigureAwait(false);
            if (!ProcessIdentity.MatchesStartTime(descriptor.ParentProcessId, descriptor.ParentStartUnixMilliseconds))
            {
                BeginDrain("parent_exit");
                return;
            }
        }
    }

    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        var connectionTasks = new List<Task>();
        while (!cancellationToken.IsCancellationRequested && !IsDraining())
        {
            var pipe = CreatePrivatePipe();
            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                pipe.Dispose();
                break;
            }
            catch (IOException)
            {
                pipe.Dispose();
                if (cancellationToken.IsCancellationRequested) break;
                await Task.Delay(25, cancellationToken).ConfigureAwait(false);
                continue;
            }

            connectionTasks.Add(HandleAcceptedConnectionAsync(pipe, cancellationToken));
            connectionTasks.RemoveAll(task => task.IsCompleted);
        }

        if (connectionTasks.Count > 0)
        {
            await Task.WhenAll(connectionTasks).ConfigureAwait(false);
        }
    }

    private async Task HandleAcceptedConnectionAsync(NamedPipeServerStream pipe, CancellationToken serviceCancellationToken)
    {
        using (pipe)
        {
            try
            {
                await HandleConnectionAsync(pipe, serviceCancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("connection_failed:" + exception.GetType().Name + ":correlation=unavailable");
                Console.Error.Flush();
            }
        }
    }

    private NamedPipeServerStream CreatePrivatePipe()
    {
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(true, false);
        var ownerSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
        ownerSid = new SecurityIdentifier(currentUserSid);
        security.AddAccessRule(new PipeAccessRule(ownerSid, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            descriptor.PipeName,
            PipeDirection.InOut,
            MaximumPipeInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            ProtocolConstants.MaxFrameBytes,
            ProtocolConstants.MaxFrameBytes,
            security,
            HandleInheritability.None,
            (PipeAccessRights)0);
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken serviceCancellationToken)
    {
        using (var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(serviceCancellationToken))
        {
            var handshake = await AuthenticateConnectionAsync(pipe, connectionCancellation.Token).ConfigureAwait(false);
            if (handshake == null) return;

            var connection = handshake;
            try
            {
                using (var frameCancellation = CancellationTokenSource.CreateLinkedTokenSource(connectionCancellation.Token, connection.CloseToken))
                {
                    var dispatches = new List<Task>();
                    try
                    {
                        while (!frameCancellation.IsCancellationRequested && pipe.IsConnected && !connection.CloseRequested && !IsDraining())
                        {
                            string rawJson;
                            try
                            {
                                rawJson = await PipeFrameIO.ReadFrameAsync(pipe, frameCancellation.Token).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }
                            catch (EndOfStreamException)
                            {
                                break;
                            }
                            catch (IOException)
                            {
                                break;
                            }
                            catch (InvalidDataException)
                            {
                                break;
                            }

                            HeaderOnlyEnvelope parsedHeader;
                            string error;
                            if (!HeaderOnlyParser.TryParse(rawJson, out parsedHeader, out error)) break;

                            var admission = ValidateFrameAdmission(connection, parsedHeader);
                            if (admission.ResponsePolicy == AdmissionResponsePolicy.CloseNoResponse)
                            {
                                connection.RequestClose();
                                break;
                            }

                            var dispatch = DispatchFrameAsync(connection, parsedHeader.Envelope, admission, frameCancellation.Token);
                            dispatches.Add(dispatch);
                            if (admission.CloseConnection) break;
                        }
                    }
                    finally
                    {
                        frameCancellation.Cancel();
                        try
                        {
                            DrainDispatchOutcome drainOutcome = await DrainDispatchesAsync(dispatches).ConfigureAwait(false);
                            if (drainOutcome != DrainDispatchOutcome.Completed)
                            {
                                Console.Error.WriteLine("dispatch_drain_incomplete:outcome=" + drainOutcome);
                                Console.Error.Flush();
                            }
                        }
                        finally
                        {
                            ReleaseAcceptedSession(connection.SessionId);
                        }
                    }
                }
            }
            finally
            {
                connection.DisposeCloseSignal();
            }
        }
    }

    private void ReleaseAcceptedSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;
        lock (sync) acceptedSessions.Remove(sessionId);
    }

    private async Task<ConnectionContext?> AuthenticateConnectionAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        string rawJson;
        try
        {
            rawJson = await PipeFrameIO.ReadFrameAsync(pipe, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception exception) when (exception is EndOfStreamException || exception is IOException || exception is InvalidDataException)
        {
            return null;
        }

        HandshakeRequest request;
        string error;
        if (!ProtocolCodec.TryParseHandshakeRequest(rawJson, out request, out error)) return null;

        var handshakeError = ValidateHandshake(request, pipe, out var connectionEpoch);
        if (!string.IsNullOrEmpty(handshakeError))
        {
            await WriteHandshakeResponseAsync(pipe, BuildHandshakeResponse(false, handshakeError, 0, Array.Empty<string>(), string.Empty, string.Empty, string.Empty), cancellationToken).ConfigureAwait(false);
            return null;
        }

        var clientDirectionNonce = TransportSecurity.RandomHex(16);
        var serviceDirectionNonce = TransportSecurity.RandomHex(16);
        var serviceAuthProof = TransportSecurity.ComputeServiceAuthResponse(request, serviceKey, serviceInstanceId, connectionEpoch, clientDirectionNonce, serviceDirectionNonce, parentStartProof);
        var capabilities = IntersectCapabilities(request.RequestedCapabilities);
        var acceptedResponse = BuildHandshakeResponse(true, string.Empty, connectionEpoch, capabilities, clientDirectionNonce, serviceDirectionNonce, serviceAuthProof);
        await WriteHandshakeResponseAsync(pipe, acceptedResponse, cancellationToken).ConfigureAwait(false);


        return new ConnectionContext(
            pipe,
            connectionEpoch,
            request.SessionId,
            clientDirectionNonce,
            serviceDirectionNonce,
            capabilities,
            frameKey);
    }

    private string ValidateHandshake(HandshakeRequest request, NamedPipeServerStream pipe, out long connectionEpoch)
    {
        connectionEpoch = 0;
        if (!StringComparer.Ordinal.Equals(request.ProtocolId, ProtocolConstants.ProtocolId) || request.ProtocolMajor != ProtocolConstants.ProtocolMajor || request.ProtocolMinor != ProtocolConstants.ProtocolMinor) return "protocol_incompatible";
        if (request.FrameworkApiMajor != ProtocolConstants.FrameworkApiMajor) return "framework_api_incompatible";
        if (!StringComparer.Ordinal.Equals(request.ServiceInstanceId, serviceInstanceId)) return "service_instance_mismatch";
        if (!StringComparer.Ordinal.Equals(request.ClientInstanceId, descriptor.ClientInstanceId)) return "client_instance_mismatch";
        if (!StringComparer.Ordinal.Equals(request.LaunchTransactionId, descriptor.LaunchTransactionId) || !StringComparer.Ordinal.Equals(request.LaunchNonce, descriptor.LaunchNonce) || !StringComparer.Ordinal.Equals(request.SessionNonce, descriptor.SessionNonce) || !StringComparer.Ordinal.Equals(request.AuthKeyId, descriptor.AuthKeyId) || !StringComparer.Ordinal.Equals(request.PipeName, descriptor.PipeName) || !StringComparer.Ordinal.Equals(request.ClientBootstrapProof, descriptor.ClientBootstrapProof) || !StringComparer.Ordinal.Equals(request.ServiceBootstrapProof, serviceBootstrapProof)) return "ipc_bootstrap_mismatch";
        if (!StringComparer.Ordinal.Equals(request.UserSidFingerprint, descriptor.UserSidFingerprint)) return "ipc_identity_mismatch";
        if (!ProcessIdentity.MatchesStartTime(request.ParentProcessId, request.ParentStartUnixMilliseconds) || request.ParentProcessId != descriptor.ParentProcessId || request.ParentStartUnixMilliseconds != descriptor.ParentStartUnixMilliseconds) return "ipc_parent_invalid";
        if (!IsConnectedUserCurrent(pipe)) return "ipc_identity_mismatch";
        var expectedChallengeResponse = TransportSecurity.ComputeChallengeResponse(request, serviceKey);
        if (!TransportSecurity.FixedTimeEquals(expectedChallengeResponse, request.ChallengeResponse)) return "ipc_auth_failed";
        if (request.RequestedCapabilities == null || request.RequestedCapabilities.Length == 0 || request.RequestedCapabilities.Any(capability => !SupportedCapabilities.Contains(capability, StringComparer.Ordinal))) return "capability_unsupported";
        if (string.IsNullOrWhiteSpace(request.SessionId)) return "session_missing";

        lock (sync)
        {
            if (!StringComparer.Ordinal.Equals(request.ChallengeId, ExpectedChallengeIdUnsafe())) return consumedChallenges.Contains(request.ChallengeId) ? "challenge_replay" : "challenge_invalid";
            if (acceptedSessions.Contains(request.SessionId)) return "session_replay";
            connectionEpoch = nextConnectionEpoch + 1;
            nextConnectionEpoch = connectionEpoch;
            consumedChallenges.Add(request.ChallengeId);
            acceptedSessions.Add(request.SessionId);
        }

        return string.Empty;
    }

    private bool IsConnectedUserCurrent(NamedPipeServerStream? pipe)
    {
        if (pipe == null) return true;

        try
        {
            var accountName = pipe.GetImpersonationUserName();
            var accountSid = new NTAccount(accountName).Translate(typeof(SecurityIdentifier)).Value;
            return StringComparer.Ordinal.Equals(accountSid, currentUserSid);
        }
        catch (IdentityNotMappedException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private string ExpectedChallengeIdUnsafe()
    {
        var expectedEpoch = nextConnectionEpoch + 1;
        return expectedEpoch == 1 ? descriptor.ChallengeId : descriptor.ChallengeId + ".connection." + expectedEpoch.ToString();
    }

    private HandshakeResponse BuildHandshakeResponse(bool accepted, string errorCode, long connectionEpoch, string[] capabilities, string clientDirectionNonce, string serviceDirectionNonce, string serviceAuthProof)
    {
        return new HandshakeResponse
        {
            Accepted = accepted,
            ErrorCode = errorCode,
            ProtocolId = ProtocolConstants.ProtocolId,
            ProtocolMajor = ProtocolConstants.ProtocolMajor,
            ProtocolMinor = ProtocolConstants.ProtocolMinor,
            FrameworkApiMajor = ProtocolConstants.FrameworkApiMajor,
            ServiceId = ProtocolConstants.ServiceId,
            ServiceInstanceId = serviceInstanceId,
            UserSidFingerprint = descriptor.UserSidFingerprint,
            ParentProcessId = descriptor.ParentProcessId,
            InstanceEpoch = descriptor.InstanceEpoch,
            ConnectionEpoch = connectionEpoch,
            ClientDirectionNonce = clientDirectionNonce,
            ServiceDirectionNonce = serviceDirectionNonce,
            ServiceBootstrapProof = serviceAuthProof,
            ParentStartProof = parentStartProof,
            Capabilities = capabilities,
            AckStatus = accepted ? "accepted" : string.Empty,
            OutcomeKind = accepted ? "accepted" : "rejected",
            NonDurable = true
        };
    }

    private async Task WriteHandshakeResponseAsync(Stream pipe, HandshakeResponse response, CancellationToken cancellationToken)
    {
        await PipeFrameIO.WriteFrameAsync(pipe, ProtocolCodec.SerializeHandshakeResponse(response), cancellationToken).ConfigureAwait(false);
    }

    private string[] IntersectCapabilities(string[] requestedCapabilities)
    {
        return SupportedCapabilities.Where(capability => requestedCapabilities.Contains(capability, StringComparer.Ordinal)).ToArray();
    }

    private async Task DispatchFrameAsync(ConnectionContext connection, PipeEnvelope envelope, AdmissionDecision admission, CancellationToken cancellationToken)
    {
        try
        {
            if (admission.SequenceResult != null)
            {
                await HandleSequenceDecisionAsync(connection, envelope, admission.SequenceResult, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!admission.AllowBusinessParse)
            {
                if (admission.ResponsePolicy == AdmissionResponsePolicy.GenericError || admission.ResponsePolicy == AdmissionResponsePolicy.SuppressedReplayError)
                {
                    var errorCode = admission.ResponsePolicy == AdmissionResponsePolicy.SuppressedReplayError ? "deadline_suppressed_replay" : admission.ErrorCode;
                    var rejected = BuildErrorResponse(connection, envelope, errorCode, admission.ErrorCode == "ipc_backpressure" || admission.ErrorCode == "sequence_gap" || admission.ErrorCode == "sequence_out_of_order" ? "retryable_reject" : "rejected");
                    await WriteResponseAsync(connection, rejected, cancellationToken).ConfigureAwait(false);
                }
                return;
            }

            var frameError = ValidateBusinessFrame(connection, envelope);
            if (!string.IsNullOrEmpty(frameError))
            {
                var response = ProviderProtocolContract.IsProviderRequest(envelope.MessageType)
                    ? BuildProviderErrorResponse(connection, envelope, "provider_schema_mismatch")
                    : BuildErrorResponse(connection, envelope, frameError, "rejected");
                await WriteResponseAsync(connection, response, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (ProviderProtocolContract.IsProviderRequest(envelope.MessageType))
            {
                if (!StringComparer.Ordinal.Equals(Environment.GetEnvironmentVariable(RuntimeTestModeEnvironmentVariable), "p3d-a0"))
                {
                    await HandleProviderBusinessAsync(connection, envelope, cancellationToken).ConfigureAwait(false);
                    return;
                }

                var errorCode = StringComparer.Ordinal.Equals(envelope.TaskScope?.SettlementRequirement, "required") ? "settlement_unavailable" : "provider_handler_deferred";
                var providerResponse = BuildProviderErrorResponse(connection, envelope, errorCode);
                var providerWrite = await WriteResponseAsync(connection, providerResponse, cancellationToken).ConfigureAwait(false);
                if (providerWrite.FrameComplete) StoreMessageTemplate(envelope.MessageId, ToTemplate(providerResponse));
                return;
            }

            switch (envelope.MessageType)
            {
                case "health":
                    await HandleHealthAsync(connection, envelope, cancellationToken).ConfigureAwait(false);
                    break;
                case "echo":
                    await HandleEchoAsync(connection, envelope, cancellationToken).ConfigureAwait(false);
                    break;
                case "cancel":
                    await HandleCancelAsync(connection, envelope, cancellationToken).ConfigureAwait(false);
                    break;
                case ProtocolConstants.MessageTypeStorageKvGet:
                case ProtocolConstants.MessageTypeStorageKvSet:
                case ProtocolConstants.MessageTypeStorageKvDelete:
                case ProtocolConstants.MessageTypeStorageTimelineAppend:
                case ProtocolConstants.MessageTypeStorageTimelineRead:
                case ProtocolConstants.MessageTypeRagIngest:
                case ProtocolConstants.MessageTypeRagSearch:
                    await HandleStorageBusinessAsync(connection, envelope, cancellationToken).ConfigureAwait(false);
                    break;
                case "diagnostic":
                    await HandleDiagnosticAsync(connection, envelope, cancellationToken).ConfigureAwait(false);
                    break;
                case "shutdown":
                    await HandleShutdownAsync(connection, envelope, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    await WriteResponseAsync(connection, BuildErrorResponse(connection, envelope, "schema_unsupported", "rejected"), cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("dispatch_failed:" + exception.GetType().Name + ":message=" + (envelope == null ? "unknown" : envelope.MessageType + ":" + envelope.MessageId + ":" + envelope.Sequence.ToString()));
            Console.Error.Flush();
        }
        finally
        {
            admission.SlotLease?.Release();
        }
    }

    private string ValidateFrameIdentity(ConnectionContext connection, PipeEnvelope envelope)
    {
        if (envelope.InstanceEpoch != descriptor.InstanceEpoch) return "stale_instance_epoch";
        if (envelope.ConnectionEpoch != connection.ConnectionEpoch) return "stale_connection_epoch";
        if (!StringComparer.Ordinal.Equals(envelope.DirectionNonce, connection.ClientDirectionNonce)) return "nonce_mismatch";
        if (!StringComparer.Ordinal.Equals(envelope.SessionId, connection.SessionId)) return "session_mismatch";
        if (string.IsNullOrWhiteSpace(envelope.MessageId) || string.IsNullOrWhiteSpace(envelope.CorrelationId)) return "frame_identity_missing";
        if (IsDraining() && !StringComparer.Ordinal.Equals(envelope.MessageType, "health") && !StringComparer.Ordinal.Equals(envelope.MessageType, "shutdown")) return "service_draining";
        return string.Empty;
    }

    private AdmissionDecision ValidateFrameAdmission(ConnectionContext connection, HeaderOnlyEnvelope header)
    {
        var envelope = header.Envelope;
        var identityError = ValidateHeaderIdentity(connection, header, envelope);
        if (!string.IsNullOrEmpty(identityError))
        {
            if (StringComparer.Ordinal.Equals(identityError, "stale_connection_epoch")) return AdmissionDecision.Generic("H1-I", identityError, false, null);
            return AdmissionDecision.Close("H1-I", identityError);
        }

        var integrityError = ValidateHeaderIntegrity(header, envelope);
        if (!string.IsNullOrEmpty(integrityError)) return AdmissionDecision.Close("H1-C", integrityError);

        var deadlineError = ValidateDeadline(header, envelope);
        if (!string.IsNullOrEmpty(deadlineError)) return AdmissionDecision.Close("H1-D", deadlineError);

        var slotLease = connection.TryEnterFrame();
        if (slotLease == null) return AdmissionDecision.Generic("S0", "ipc_backpressure", false, null);

        var sequenceDecision = connection.ReceiveWindow.Evaluate(envelope.SessionId, envelope.ConnectionEpoch, envelope.DirectionNonce, envelope.Sequence, envelope.MessageId, envelope.PayloadSha256);
            if (sequenceDecision.Kind != SequenceDecisionKind.Accepted)
            {
            var sequenceError = sequenceDecision.Kind == SequenceDecisionKind.Duplicate
                ? (TryGetMessageTemplate(envelope.MessageId, out _) ? "sequence_duplicate" : "duplicate_unknown")
                : sequenceDecision.ErrorCode == ProtocolErrorCode.SequenceGap ? "sequence_gap" : sequenceDecision.ErrorCode == ProtocolErrorCode.SequenceOutOfOrder ? "sequence_out_of_order" : sequenceDecision.ErrorCode == ProtocolErrorCode.NonceMismatch ? "nonce_mismatch" : "replay_rejected";
                return AdmissionDecision.Generic("S0", sequenceError, false, slotLease, sequenceDecision);
            }

            connection.RecordAcceptedRequest(envelope.MessageId, envelope.Sequence);

            var businessHeaderError = ValidateBusinessHeaders(connection, header, envelope);
            if (!string.IsNullOrEmpty(businessHeaderError)) return AdmissionDecision.Generic("B0", businessHeaderError, true, slotLease);
            if (TryGetSuppressedMarker(envelope.MessageId, envelope.Sequence, connection.ConnectionEpoch)) return AdmissionDecision.SuppressedReplay(slotLease);
            return AdmissionDecision.Allow(slotLease);
        }

    private string ValidateHeaderIdentity(ConnectionContext connection, HeaderOnlyEnvelope header, PipeEnvelope envelope)
    {
        var requiredIdentityFields = new[]
        {
            "protocol_id", "protocol_major", "protocol_minor", "message_type", "message_id", "correlation_id", "instance_epoch", "connection_epoch", "direction_nonce", "sequence"
        };
        for (var index = 0; index < requiredIdentityFields.Length; index++)
        {
            if (!header.HasField(requiredIdentityFields[index])) return "header_identity_missing";
        }

        var protocol = ProtocolValidation.ValidateProtocolIdentity(envelope.ProtocolId, envelope.ProtocolMajor, envelope.ProtocolMinor, ProtocolConstants.FrameworkApiMajor, ProtocolConstants.ServiceId);
        if (!protocol.IsAccepted) return protocol.ErrorCode;
        var connectionError = ValidateFrameIdentity(connection, envelope);
        if (!string.IsNullOrEmpty(connectionError)) return connectionError;
        if (!StringComparer.Ordinal.Equals(envelope.MessageType, ProtocolConstants.MessageTypeHealth))
        {
            if (!header.HasField("campaign_guid") || !header.HasField("timeline_id") || !header.HasField("session_generation") || string.IsNullOrWhiteSpace(envelope.CampaignGuid) || string.IsNullOrWhiteSpace(envelope.TimelineId) || envelope.SessionGeneration < 1) return "session_fence_incomplete";
            if (!connection.BindSessionFence(envelope.CampaignGuid, envelope.TimelineId, envelope.SessionGeneration)) return "session_fence_mismatch";
        }

        return string.Empty;
    }

    private string ValidateHeaderIntegrity(HeaderOnlyEnvelope header, PipeEnvelope envelope)
    {
        var requiredIntegrityFields = new[] { "checksum_algorithm", "fence_proof", "payload", "payload_length", "payload_sha256", "checksum" };
        for (var index = 0; index < requiredIntegrityFields.Length; index++)
        {
            if (!header.HasField(requiredIntegrityFields[index])) return "integrity_header_missing";
        }

        if (!StringComparer.Ordinal.Equals(envelope.ChecksumAlgorithm, ProtocolConstants.ChecksumAlgorithm)) return "checksum_algorithm_invalid";
        string canonicalPayload;
        try
        {
            canonicalPayload = CanonicalizePayload(envelope.PayloadJson);
        }
        catch (Exception)
        {
            return "payload_invalid";
        }

        var payloadBytes = Encoding.UTF8.GetBytes(canonicalPayload);
        if (payloadBytes.Length != envelope.PayloadLength) return "payload_length_mismatch";
        var payloadHash = TransportSecurity.Sha256Hex(payloadBytes);
        if (!TransportSecurity.FixedTimeEquals(payloadHash, envelope.PayloadSha256) || !TransportSecurity.FixedTimeEquals(payloadHash, envelope.Checksum)) return "checksum_mismatch";
        if (!StringComparer.Ordinal.Equals(envelope.FenceProof, TransportSecurity.ComputeFrameFenceProof(frameKey, envelope))) return "frame_fence_mismatch";
        return string.Empty;
    }

    private string ValidateDeadline(HeaderOnlyEnvelope header, PipeEnvelope envelope)
    {
        if (!header.HasField("deadline_unix_milliseconds")) return "deadline_missing";
        if (envelope.DeadlineUnixMilliseconds < 1) return "deadline_invalid";
        try
        {
            DateTimeOffset.FromUnixTimeMilliseconds(envelope.DeadlineUnixMilliseconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return "deadline_invalid";
        }

        return ResolveNowUnixMilliseconds() >= envelope.DeadlineUnixMilliseconds ? "deadline_expired" : string.Empty;
    }

    private string ValidateBusinessHeaders(ConnectionContext connection, HeaderOnlyEnvelope header, PipeEnvelope envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope.MessageType) || !IsKnownRequestMessage(envelope.MessageType)) return "message_type_missing";
        if (connection.IsControlOnly && !StringComparer.Ordinal.Equals(envelope.MessageType, ProtocolConstants.MessageTypeCancel)) return "control_connection_restricted";
        if (!header.HasField("payload_schema")) return "payload_schema_missing";
        if (ProviderProtocolContract.TryGetRequest(envelope.MessageType, out var providerContract))
        {
            if (!StringComparer.Ordinal.Equals(envelope.PayloadSchema, providerContract.RequestSchema)) return "schema_unsupported";
        }

        var requiredCapability = RequiredCapabilityForMessage(envelope.MessageType);
        if (string.IsNullOrWhiteSpace(requiredCapability) || !connection.Capabilities.Contains(requiredCapability, StringComparer.Ordinal)) return "capability_not_granted";
        if (IsTaskMessage(envelope.MessageType))
        {
            if (!header.HasField("task_scope") || envelope.TaskScope == null) return "task_scope_missing";
            if (!envelope.TaskScope.IsComplete || !StringComparer.Ordinal.Equals(envelope.TaskScope.MessageId, envelope.MessageId) || !StringComparer.Ordinal.Equals(envelope.TaskScope.OwnerId, envelope.OwnerId)) return "task_scope_invalid";
            if (ProviderProtocolContract.TryGetRequest(envelope.MessageType, out providerContract))
            {
                if (!StringComparer.Ordinal.Equals(envelope.TaskScope.OutputSchemaId, providerContract.OutputSchema) || envelope.TaskScope.OutputSchemaMajor != providerContract.OutputSchemaMajor || envelope.TaskScope.OutputSchemaMinor != providerContract.OutputSchemaMinor) return "output_schema_mismatch";
                if (string.IsNullOrWhiteSpace(envelope.CausationId)) return "provider_causation_missing";
            }
        }

        return string.Empty;
    }

    private string ValidateBusinessFrame(ConnectionContext connection, PipeEnvelope envelope)
    {
        var envelopeDecision = ProtocolValidation.ValidateEnvelope(envelope);
        if (!envelopeDecision.IsAccepted) return envelopeDecision.ErrorCode;
        if (IsTaskMessage(envelope.MessageType) && !IsSha256Hex(envelope.TaskScope?.RequestPayloadHash)) return "request_payload_hash_invalid";
        if (!TryValidatePayload(connection, envelope, out var payloadError)) return payloadError;
        return string.Empty;
    }

    private static bool IsKnownRequestMessage(string messageType)
    {
        return StringComparer.Ordinal.Equals(messageType, ProtocolConstants.MessageTypeHealth)
            || StringComparer.Ordinal.Equals(messageType, ProtocolConstants.MessageTypeEcho)
            || StringComparer.Ordinal.Equals(messageType, ProtocolConstants.MessageTypeCancel)
            || StringComparer.Ordinal.Equals(messageType, ProtocolConstants.MessageTypeDiagnostic)
            || StringComparer.Ordinal.Equals(messageType, ProtocolConstants.MessageTypeShutdown)
            || IsStorageBusinessMessage(messageType)
            || ProviderProtocolContract.IsProviderRequest(messageType);
    }

    private static bool IsSha256Hex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64) return false;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f') || (character >= 'A' && character <= 'F'))) return false;
        }

        return true;
    }

    private static long ResolveNowUnixMilliseconds()
    {
        var configured = Environment.GetEnvironmentVariable(RuntimeTestNowEnvironmentVariable);
        return long.TryParse(configured, out var value) && value > 0 ? value : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private static bool IsTaskMessage(string messageType)
    {
        return StringComparer.Ordinal.Equals(messageType, ProtocolConstants.MessageTypeEcho)
            || StringComparer.Ordinal.Equals(messageType, ProtocolConstants.MessageTypeCancel)
            || IsStorageBusinessMessage(messageType)
            || ProviderProtocolContract.IsProviderRequest(messageType);
    }

    private static bool IsStorageBusinessMessage(string messageType)
    {
        return StringComparer.Ordinal.Equals(messageType, ProtocolConstants.MessageTypeStorageKvGet)
            || StringComparer.Ordinal.Equals(messageType, ProtocolConstants.MessageTypeStorageKvSet)
            || StringComparer.Ordinal.Equals(messageType, ProtocolConstants.MessageTypeStorageKvDelete)
            || StringComparer.Ordinal.Equals(messageType, ProtocolConstants.MessageTypeStorageTimelineAppend)
            || StringComparer.Ordinal.Equals(messageType, ProtocolConstants.MessageTypeStorageTimelineRead)
            || StringComparer.Ordinal.Equals(messageType, ProtocolConstants.MessageTypeRagIngest)
            || StringComparer.Ordinal.Equals(messageType, ProtocolConstants.MessageTypeRagSearch);
    }

    private static string RequiredCapabilityForMessage(string messageType)
    {
        if (ProviderProtocolContract.TryGetRequest(messageType, out var providerContract)) return providerContract.RequiredCapability;
        switch (messageType)
        {
            case ProtocolConstants.MessageTypeStorageKvGet:
            case ProtocolConstants.MessageTypeStorageTimelineRead:
                return ProtocolConstants.CapabilityStorageRead;
            case ProtocolConstants.MessageTypeStorageKvSet:
            case ProtocolConstants.MessageTypeStorageKvDelete:
            case ProtocolConstants.MessageTypeStorageTimelineAppend:
                return ProtocolConstants.CapabilityStorageWrite;
            case ProtocolConstants.MessageTypeRagSearch:
                return ProtocolConstants.CapabilityRagRead;
            case ProtocolConstants.MessageTypeRagIngest:
                return ProtocolConstants.CapabilityRagWrite;
            default:
                return messageType;
        }
    }

    private bool TryValidatePayload(ConnectionContext connection, PipeEnvelope envelope, out string error)
    {
        error = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(envelope.PayloadJson, new JsonDocumentOptions { MaxDepth = ProtocolConstants.MaxJsonDepth, AllowTrailingCommas = false });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "payload_object_required";
                return false;
            }

            var properties = document.RootElement.EnumerateObject().ToArray();
            switch (envelope.MessageType)
            {
                case ProtocolConstants.MessageTypeProviderProfileUpsertV1:
                case ProtocolConstants.MessageTypeProviderCredentialUpsertV1:
                case ProtocolConstants.MessageTypeProviderProfileRemoveV1:
                case ProtocolConstants.MessageTypeProviderModelsV1:
                case ProtocolConstants.MessageTypeProviderCompleteV1:
                case ProtocolConstants.MessageTypeProviderStreamV1:
                    return TryValidateProviderPayload(envelope, document.RootElement, out error);
                case "health":
                    if (properties.Any(property => !StringComparer.Ordinal.Equals(property.Name, "probe")))
                    {
                        error = "payload_field_invalid";
                        return false;
                    }

                    if (properties.Length == 1 && (!properties[0].Value.TryGetPropertyValueAsString(out var probe) || !StringComparer.Ordinal.Equals(probe, "health")))
                    {
                        error = "payload_value_invalid";
                        return false;
                    }
                    return true;
                case "echo":
                    if (properties.Any(property => !StringComparer.Ordinal.Equals(property.Name, "value") && !StringComparer.Ordinal.Equals(property.Name, "delay_ms")))
                    {
                        error = "payload_field_invalid";
                        return false;
                    }

                    var valueProperty = properties.FirstOrDefault(property => StringComparer.Ordinal.Equals(property.Name, "value"));
                    if (valueProperty.Name == null || valueProperty.Value.ValueKind != JsonValueKind.String)
                    {
                        error = "payload_value_invalid";
                        return false;
                    }

                    if (document.RootElement.TryGetProperty("delay_ms", out var delayProperty) && (!delayProperty.TryGetInt32(out var delayMilliseconds) || delayMilliseconds < 0 || delayMilliseconds > 2000))
                    {
                        error = "payload_delay_invalid";
                        return false;
                    }
                    return true;
                case "cancel":
                    if (connection.IsControlOnly)
                    {
                        if (properties.Length != 3 || !document.RootElement.TryGetProperty("schema", out var schema) || schema.ValueKind != JsonValueKind.String || !StringComparer.Ordinal.Equals(schema.GetString(), "marcus-awake.cancel.v1") || !document.RootElement.TryGetProperty("task_id", out var controlTaskId) || controlTaskId.ValueKind != JsonValueKind.String || !StringComparer.Ordinal.Equals(controlTaskId.GetString(), envelope.TaskScope?.TaskId) || !document.RootElement.TryGetProperty("target_session_id", out var targetSessionId) || targetSessionId.ValueKind != JsonValueKind.String || !IsBoundedUtf8(targetSessionId.GetString(), 256))
                        {
                            error = "payload_task_invalid";
                            return false;
                        }
                    }
                    else if (properties.Length != 1 || !StringComparer.Ordinal.Equals(properties[0].Name, "task_id") || properties[0].Value.ValueKind != JsonValueKind.String || !StringComparer.Ordinal.Equals(properties[0].Value.GetString(), envelope.TaskScope?.TaskId))
                    {
                        error = "payload_task_invalid";
                        return false;
                    }
                    return true;
                case ProtocolConstants.MessageTypeStorageKvGet:
                case ProtocolConstants.MessageTypeStorageKvSet:
                case ProtocolConstants.MessageTypeStorageKvDelete:
                case ProtocolConstants.MessageTypeStorageTimelineAppend:
                case ProtocolConstants.MessageTypeStorageTimelineRead:
                case ProtocolConstants.MessageTypeRagIngest:
                case ProtocolConstants.MessageTypeRagSearch:
                    return StorageBusinessFrameAdapter.TryParse(envelope, out _, out error);
                case "diagnostic":
                case "shutdown":
                    if (properties.Length != 0)
                    {
                        error = "payload_field_invalid";
                        return false;
                    }
                    return true;
                default:
                    error = "schema_unsupported";
                    return false;
            }
        }
        catch (JsonException)
        {
            error = "payload_json_invalid";
            return false;
        }
    }

    private static bool TryValidateProviderPayload(PipeEnvelope envelope, JsonElement root, out string error)
    {
        error = string.Empty;
        if (!ProviderProtocolContract.TryGetRequest(envelope.MessageType, out var contract))
        {
            error = "provider_schema_mismatch";
            return false;
        }

        if (!root.TryGetProperty("schema", out var schema) || schema.ValueKind != JsonValueKind.String || !StringComparer.Ordinal.Equals(schema.GetString(), contract.RequestSchema))
        {
            error = "provider_schema_mismatch";
            return false;
        }

        var allowed = new HashSet<string>(StringComparer.Ordinal);
        if (envelope.MessageType == ProtocolConstants.MessageTypeProviderCredentialUpsertV1)
        {
            allowed.UnionWith(new[] { "schema", "profile_id", "provider_id", "route_id", "credential_reference", "secret" });
            if (!HasExactlyRequiredProperties(root, allowed, new[] { "schema", "profile_id", "provider_id", "route_id", "credential_reference", "secret" })) return ProviderPayloadFailure(out error, "provider_schema_mismatch");
            if (!TryReadProviderIds(root, envelope, out error)) return false;
            if (!root.TryGetProperty("credential_reference", out var credentialReference)
                || credentialReference.ValueKind != JsonValueKind.String
                || !IsBoundedIdentifier(credentialReference.GetString(), 160)
                || !root.TryGetProperty("secret", out var secret)
                || secret.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(secret.GetString())
                || !IsBoundedUtf8(secret.GetString(), 4096)) return ProviderPayloadFailure(out error, "provider_schema_mismatch");
            return true;
        }

        if (envelope.MessageType == ProtocolConstants.MessageTypeProviderProfileUpsertV1)
        {
            allowed.UnionWith(new[] { "schema", "profile_id", "provider_id", "route_id", "provider_kind", "base_url", "default_model", "credential_reference", "is_cloud" });
            if (!HasRequiredProperties(root, allowed, new[] { "schema", "profile_id", "provider_id", "route_id", "provider_kind", "base_url", "default_model", "is_cloud" })) return ProviderPayloadFailure(out error, "provider_schema_mismatch");
            if (!TryReadProviderIds(root, envelope, out error)) return false;
            if (!root.TryGetProperty("provider_kind", out var kind) || kind.ValueKind != JsonValueKind.String || (kind.GetString() != "openai_compatible" && kind.GetString() != "anthropic" && kind.GetString() != "ollama")) return ProviderPayloadFailure(out error, "provider_schema_mismatch");
            if (!root.TryGetProperty("base_url", out var baseUrl) || baseUrl.ValueKind != JsonValueKind.String || !IsValidProviderBaseUrl(baseUrl.GetString())) return ProviderPayloadFailure(out error, "provider_schema_mismatch");
            if (!root.TryGetProperty("default_model", out var model) || model.ValueKind != JsonValueKind.String || !IsBoundedUtf8(model.GetString(), 256)) return ProviderPayloadFailure(out error, "provider_schema_mismatch");
            if (root.TryGetProperty("credential_reference", out var credential) && (credential.ValueKind != JsonValueKind.String || !IsBoundedIdentifier(credential.GetString(), 160))) return ProviderPayloadFailure(out error, "provider_schema_mismatch");
            if (root.TryGetProperty("is_cloud", out var isCloud) && isCloud.ValueKind != JsonValueKind.True && isCloud.ValueKind != JsonValueKind.False) return ProviderPayloadFailure(out error, "provider_schema_mismatch");
            if (root.GetProperty("is_cloud").GetBoolean() && (!root.TryGetProperty("credential_reference", out credential) || credential.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(credential.GetString()))) return ProviderPayloadFailure(out error, "provider_schema_mismatch");
            return true;
        }

        allowed.UnionWith(new[] { "schema", "profile_id", "provider_id", "route_id" });
        if (envelope.MessageType == ProtocolConstants.MessageTypeProviderModelsV1 || envelope.MessageType == ProtocolConstants.MessageTypeProviderProfileRemoveV1)
        {
            if (!HasExactlyRequiredProperties(root, allowed, new[] { "schema", "profile_id", "provider_id", "route_id" })) return ProviderPayloadFailure(out error, "provider_schema_mismatch");
            return TryReadProviderIds(root, envelope, out error);
        }

        allowed.UnionWith(new[] { "model", "messages", "max_output_tokens", "temperature", "response_schema_json" });
        var required = new List<string> { "schema", "profile_id", "provider_id", "route_id", "messages" };
        if (envelope.MessageType == ProtocolConstants.MessageTypeProviderStreamV1)
        {
            allowed.Add("resource_budget");
            required.Add("resource_budget");
        }
        if (!HasRequiredProperties(root, allowed, required.ToArray())) return ProviderPayloadFailure(out error, "provider_schema_mismatch");
        if (!TryReadProviderIds(root, envelope, out error)) return false;
        if (root.TryGetProperty("model", out var modelProperty) && (modelProperty.ValueKind != JsonValueKind.String || !IsBoundedUtf8(modelProperty.GetString(), 256))) return ProviderPayloadFailure(out error, "provider_schema_mismatch");
        if (!TryValidateProviderMessages(root.GetProperty("messages"), out error)) return false;
        if (root.TryGetProperty("max_output_tokens", out var maxOutput) && (maxOutput.ValueKind != JsonValueKind.Number || !maxOutput.TryGetInt32(out var maxOutputValue) || maxOutputValue < 1 || maxOutputValue > 65536)) return ProviderPayloadFailure(out error, "provider_schema_mismatch");
        if (root.TryGetProperty("temperature", out var temperature) && (temperature.ValueKind != JsonValueKind.Number || !temperature.TryGetDouble(out var temperatureValue) || double.IsNaN(temperatureValue) || double.IsInfinity(temperatureValue) || temperatureValue < 0 || temperatureValue > 2)) return ProviderPayloadFailure(out error, "provider_schema_mismatch");
        if (root.TryGetProperty("response_schema_json", out var responseSchema))
        {
            if (responseSchema.ValueKind != JsonValueKind.String || !IsBoundedUtf8(responseSchema.GetString(), 65536) || !IsJsonObject(responseSchema.GetString())) return ProviderPayloadFailure(out error, "provider_schema_mismatch");
        }

        if (envelope.MessageType == ProtocolConstants.MessageTypeProviderStreamV1 && (!root.TryGetProperty("resource_budget", out var budget) || !TryValidateProviderResourceBudget(budget))) return ProviderPayloadFailure(out error, "stream_budget_invalid");

        if (ContainsSecretLikeProperty(root, envelope.MessageType == ProtocolConstants.MessageTypeProviderProfileUpsertV1)) return ProviderPayloadFailure(out error, "provider_schema_mismatch");
        return true;
    }

    private static bool TryValidateProviderResourceBudget(JsonElement budget)
    {
        if (budget.ValueKind != JsonValueKind.Object) return false;
        var properties = budget.EnumerateObject().ToArray();
        if (properties.Length != 4 || properties.Any(property => property.Name != "frame_count" && property.Name != "output_bytes" && property.Name != "tokens" && property.Name != "text_deltas")) return false;
        return TryReadPositiveBudgetValue(budget, "frame_count", 2, ProtocolConstants.MaxProviderStreamEvents)
            && TryReadPositiveBudgetValue(budget, "output_bytes", 1, 4194304)
            && TryReadPositiveBudgetValue(budget, "tokens", 1, 32768)
            && TryReadPositiveBudgetValue(budget, "text_deltas", 1, 4096);
    }

    private static bool TryReadPositiveBudgetValue(JsonElement root, string name, int minimum, int maximum)
    {
        return root.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out var value)
            && value >= minimum
            && value <= maximum;
    }

    private static bool TryReadProviderIds(JsonElement root, PipeEnvelope envelope, out string error)
    {
        error = string.Empty;
        var names = new[] { "profile_id", "provider_id", "route_id" };
        for (var index = 0; index < names.Length; index++)
        {
            if (!root.TryGetProperty(names[index], out var property) || property.ValueKind != JsonValueKind.String || !StringComparer.Ordinal.Equals(property.GetString(), ProviderScopeValue(envelope, names[index]))) return ProviderPayloadFailure(out error, "provider_schema_mismatch");
        }

        return true;
    }

    private static string ProviderScopeValue(PipeEnvelope envelope, string name)
    {
        if (name == "profile_id") return envelope.TaskScope?.ProfileId ?? string.Empty;
        if (name == "provider_id") return envelope.TaskScope?.ProviderId ?? string.Empty;
        return envelope.TaskScope?.RouteId ?? string.Empty;
    }

    private static bool TryValidateProviderMessages(JsonElement messages, out string error)
    {
        error = string.Empty;
        if (messages.ValueKind != JsonValueKind.Array || messages.GetArrayLength() < 1 || messages.GetArrayLength() > 64) return ProviderPayloadFailure(out error, "provider_schema_mismatch");
        foreach (var message in messages.EnumerateArray())
        {
            if (message.ValueKind != JsonValueKind.Object) return ProviderPayloadFailure(out error, "provider_schema_mismatch");
            var properties = message.EnumerateObject().ToArray();
            if (properties.Any(property => property.Name != "role" && property.Name != "content")) return ProviderPayloadFailure(out error, "provider_schema_mismatch");
            if (!message.TryGetProperty("role", out var role) || role.ValueKind != JsonValueKind.String || (role.GetString() != "system" && role.GetString() != "user" && role.GetString() != "assistant")) return ProviderPayloadFailure(out error, "provider_schema_mismatch");
            if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String || !IsBoundedUtf8(content.GetString(), 65536)) return ProviderPayloadFailure(out error, "provider_schema_mismatch");
        }

        return true;
    }

    private static bool HasRequiredProperties(JsonElement root, HashSet<string> allowed, string[] required)
    {
        var properties = root.EnumerateObject().ToArray();
        if (properties.Any(property => !allowed.Contains(property.Name))) return false;
        for (var index = 0; index < required.Length; index++) if (!root.TryGetProperty(required[index], out _)) return false;
        return true;
    }

    private static bool HasExactlyRequiredProperties(JsonElement root, HashSet<string> allowed, string[] required)
    {
        return HasRequiredProperties(root, allowed, required) && root.EnumerateObject().Count() == required.Length;
    }

    private static bool ProviderPayloadFailure(out string error, string code)
    {
        error = code;
        return false;
    }

    private static bool IsBoundedIdentifier(string? value, int maximumBytes)
    {
        return !string.IsNullOrWhiteSpace(value) && IsBoundedUtf8(value, maximumBytes) && value.All(character => char.IsLetterOrDigit(character) || character == '.' || character == '_' || character == '-' || character == ':' || character == '/');
    }

    private static bool IsBoundedUtf8(string? value, int maximumBytes)
    {
        return value != null && Encoding.UTF8.GetByteCount(value) <= maximumBytes && !value.Any(char.IsControl);
    }

    private static bool IsValidProviderBaseUrl(string? value)
    {
        if (!IsBoundedUtf8(value, 2048) || !Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
    }

    private static bool IsJsonObject(string? value)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            using var document = JsonDocument.Parse(value, new JsonDocumentOptions { MaxDepth = ProtocolConstants.MaxJsonDepth, AllowTrailingCommas = false });
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool ContainsSecretLikeProperty(JsonElement root, bool allowCredentialReference)
    {
        foreach (var property in root.EnumerateObject())
        {
            var name = property.Name.ToLowerInvariant();
            if (allowCredentialReference && name == "credential_reference") continue;
            if (name.Contains("api_key") || name.Contains("authorization") || name.Contains("password") || name.Contains("secret") || name.Contains("credential")) return true;
        }

        return false;
    }

    private async Task HandleSequenceDecisionAsync(ConnectionContext connection, PipeEnvelope envelope, SequenceDecision decision, CancellationToken cancellationToken)
    {
        switch (decision.Kind)
        {
            case SequenceDecisionKind.Duplicate:
                if (TryGetMessageTemplate(envelope.MessageId, out var duplicateTemplate))
                {
                    await WriteResponseAsync(connection, BuildResponseFromTemplate(connection, envelope, duplicateTemplate, "duplicate"), cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await WriteResponseAsync(connection, BuildErrorResponse(connection, envelope, "duplicate_unknown", "rejected"), cancellationToken).ConfigureAwait(false);
                }
                break;
            case SequenceDecisionKind.SequenceGap:
                await WriteResponseAsync(connection, BuildErrorResponse(connection, envelope, "sequence_gap", "retryable_reject"), cancellationToken).ConfigureAwait(false);
                break;
            case SequenceDecisionKind.SequenceOutOfOrder:
                await WriteResponseAsync(connection, BuildErrorResponse(connection, envelope, "sequence_out_of_order", "retryable_reject"), cancellationToken).ConfigureAwait(false);
                break;
            case SequenceDecisionKind.NonceMismatch:
                await WriteResponseAsync(connection, BuildErrorResponse(connection, envelope, "nonce_mismatch", "rejected"), cancellationToken).ConfigureAwait(false);
                break;
            default:
                await WriteResponseAsync(connection, BuildErrorResponse(connection, envelope, "replay_rejected", "rejected"), cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task HandleHealthAsync(ConnectionContext connection, PipeEnvelope envelope, CancellationToken cancellationToken)
    {
        var response = BuildResponse(connection, envelope, "health_ack", "marcus-awake.health.v1", "{\"state\":\"ready\",\"ledger\":\"non_durable\"}", null, "accepted");
        var writeResult = await WriteResponseAsync(connection, response, cancellationToken).ConfigureAwait(false);
        if (writeResult.FrameComplete) StoreMessageTemplate(envelope.MessageId, ToTemplate(response));
    }

    private async Task HandleDiagnosticAsync(ConnectionContext connection, PipeEnvelope envelope, CancellationToken cancellationToken)
    {
        var response = BuildResponse(connection, envelope, "diagnostic_ack", "marcus-awake.diagnostic.v1", "{\"state\":\"ready\",\"storage\":\"non_durable_memory\"}", null, "accepted");
        var writeResult = await WriteResponseAsync(connection, response, cancellationToken).ConfigureAwait(false);
        if (writeResult.FrameComplete) StoreMessageTemplate(envelope.MessageId, ToTemplate(response));
    }

    private async Task HandleEchoAsync(ConnectionContext connection, PipeEnvelope envelope, CancellationToken cancellationToken)
    {
        if (!TryReadEchoPayload(envelope.PayloadJson, out var value, out var delayMilliseconds))
        {
            await WriteResponseAsync(connection, BuildErrorResponse(connection, envelope, "payload_value_invalid", "rejected"), cancellationToken).ConfigureAwait(false);
            return;
        }

        var existing = GetOrCreateTaskLedger(envelope, out var ledgerCreated);
        if (!StringComparer.Ordinal.Equals(existing.MessageId, envelope.MessageId) && existing.Template == null)
        {
            await WriteResponseAsync(connection, BuildErrorResponse(connection, envelope, "task_in_progress", "retryable_reject"), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (existing.Template != null)
        {
            await WriteResponseAsync(connection, BuildResponseFromTemplate(connection, envelope, existing.Template, "terminal_replay"), cancellationToken).ConfigureAwait(false);
            return;
        }

        using (var taskCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            if (!RegisterActiveTask(envelope.TaskScope!.TaskId, taskCancellation))
            {
                if (ledgerCreated) RemoveTaskLedger(envelope);
                await WriteResponseAsync(connection, BuildErrorResponse(connection, envelope, "task_in_progress", "retryable_reject"), cancellationToken).ConfigureAwait(false);
                return;
            }

            try
            {
                if (delayMilliseconds > 0) await Task.Delay(delayMilliseconds, taskCancellation.Token).ConfigureAwait(false);
                var cancelled = taskCancellation.IsCancellationRequested;
                var payload = "{\"cancelled\":" + (cancelled ? "true" : "false") + ",\"value\":" + JsonSerializer.Serialize(value) + "}";
                var response = BuildResponse(connection, envelope, "echo_result", "marcus-awake.echo.v1", payload, envelope.TaskScope, "accepted");
                var writeResult = await WriteResponseAsync(connection, response, cancellationToken).ConfigureAwait(false);
                if (writeResult.FrameComplete) SetTaskTemplate(existing, ToTemplate(response));
                else RemoveTaskLedger(envelope);
            }
            catch (OperationCanceledException)
            {
                var payload = "{\"cancelled\":true,\"value\":" + JsonSerializer.Serialize(value) + "}";
                var response = BuildResponse(connection, envelope, "echo_result", "marcus-awake.echo.v1", payload, envelope.TaskScope, "accepted");
                if (!cancellationToken.IsCancellationRequested)
                {
                    var writeResult = await WriteResponseAsync(connection, response, cancellationToken).ConfigureAwait(false);
                    if (writeResult.FrameComplete) SetTaskTemplate(existing, ToTemplate(response));
                    else RemoveTaskLedger(envelope);
                }
            }
            finally
            {
                RemoveActiveTask(envelope.TaskScope!.TaskId);
            }
        }
    }

    private async Task HandleCancelAsync(ConnectionContext connection, PipeEnvelope envelope, CancellationToken cancellationToken)
    {
        if (!connection.IsControlOnly)
        {
            var localTaskId = envelope.TaskScope!.TaskId;
            ActiveTask? activeTask = null;
            ResponseTemplate? localTerminalTemplate = null;
            lock (sync)
            {
                if (activeTasks.TryGetValue(localTaskId, out activeTask))
                {
                    activeTask.MarkCancellationRequested();
                }
                else if (taskLedger.TryGetValue(localTaskId, out var ledgerEntry))
                {
                    localTerminalTemplate = ledgerEntry.Template;
                }
            }

            if (activeTask != null)
            {
                activeTask.ApplyCancellation();
                SetTaskLifecycle(activeTask.Ledger, ProviderTaskLifecycle.CancelledRequested);
                var response = BuildCancelAckResponse(connection, envelope, "cancel_requested");
                var writeResult = await WriteResponseAsync(connection, response, cancellationToken).ConfigureAwait(false);
                if (writeResult.FrameComplete) StoreMessageTemplate(envelope.MessageId, ToTemplate(response));
                return;
            }

            if (localTerminalTemplate != null)
            {
                await WriteResponseAsync(connection, BuildResponseFromTemplate(connection, envelope, localTerminalTemplate, "terminal_replay"), cancellationToken).ConfigureAwait(false);
                return;
            }

            var notFound = BuildCancelAckResponse(connection, envelope, "not_found");
            var notFoundWrite = await WriteResponseAsync(connection, notFound, cancellationToken).ConfigureAwait(false);
            if (notFoundWrite.FrameComplete) StoreMessageTemplate(envelope.MessageId, ToTemplate(notFound));
            return;
        }

        if (!TryReadControlCancelPayload(envelope.PayloadJson, out var controlTaskId, out var targetSessionId))
        {
            await WriteResponseAsync(connection, BuildErrorResponse(connection, envelope, "payload_task_invalid", "rejected"), cancellationToken).ConfigureAwait(false);
            return;
        }

        var targetScope = envelope.TaskScope!;
        CancelResolution resolution;
        ActiveTask? targetTask = null;
        lock (sync)
        {
            if (activeTasks.TryGetValue(controlTaskId, out targetTask))
            {
                resolution = targetTask.MatchesCancellationTarget(envelope, targetSessionId)
                    ? targetTask.MarkCancellationRequested()
                    : CancelResolution.NotFound;
            }
            else if (taskLedger.TryGetValue(controlTaskId, out var ledgerEntry))
            {
                if (!MatchesCancellationTarget(ledgerEntry, envelope, targetSessionId)) resolution = CancelResolution.NotFound;
                else if (ledgerEntry.Template != null || ledgerEntry.StreamTemplates != null) resolution = CancelResolution.AlreadyCompleted;
                else
                {
                    ledgerEntry.CancelRequested = true;
                    ledgerEntry.LifecycleState = ProviderTaskLifecycleName(ProviderTaskLifecycle.CancelledRequested);
                    AddPendingCancellationUnsafe(envelope, targetSessionId);
                    resolution = CancelResolution.Requested;
                }
            }
            else
            {
                AddPendingCancellationUnsafe(envelope, targetSessionId);
                resolution = CancelResolution.Requested;
            }
        }

        if (targetTask != null && resolution != CancelResolution.NotFound)
        {
            targetTask.ApplyCancellation();
            SetTaskLifecycle(targetTask.Ledger, resolution == CancelResolution.AlreadyCompleted ? ProviderTaskLifecycle.ResponsePublished : ProviderTaskLifecycle.CancelledRequested);
        }

        var status = resolution == CancelResolution.Requested || resolution == CancelResolution.AlreadyRequested
            ? "cancel_requested"
            : resolution == CancelResolution.AlreadyCompleted ? "already_completed" : "not_found";
        var cancelResponse = BuildCancelAckResponse(connection, envelope, status);
        var cancelWrite = await WriteResponseAsync(connection, cancelResponse, cancellationToken).ConfigureAwait(false);
        if (cancelWrite.FrameComplete) StoreMessageTemplate(envelope.MessageId, ToTemplate(cancelResponse));
    }

    private static bool TryReadControlCancelPayload(string payload, out string taskId, out string targetSessionId)
    {
        taskId = string.Empty;
        targetSessionId = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(payload ?? string.Empty);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("schema", out var schema)
                || schema.ValueKind != JsonValueKind.String
                || !StringComparer.Ordinal.Equals(schema.GetString(), "marcus-awake.cancel.v1")
                || !root.TryGetProperty("task_id", out var task)
                || task.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("target_session_id", out var targetSession)
                || targetSession.ValueKind != JsonValueKind.String) return false;

            taskId = task.GetString() ?? string.Empty;
            targetSessionId = targetSession.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(taskId) && !string.IsNullOrWhiteSpace(targetSessionId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private PipeEnvelope BuildCancelAckResponse(ConnectionContext connection, PipeEnvelope request, string status)
    {
        var payload = "{\"status\":" + JsonSerializer.Serialize(status) + ",\"task_id\":" + JsonSerializer.Serialize(request.TaskScope?.TaskId ?? string.Empty) + "}";
        return BuildResponse(connection, request, "cancel_ack", "marcus-awake.cancel.v1", payload, request.TaskScope, ProtocolConstants.OutcomeAccepted);
    }

    private bool MatchesCancellationTarget(LedgerEntry entry, PipeEnvelope envelope, string targetSessionId)
    {
        return entry.IsProviderTask
            && entry.TaskScope != null
            && StringComparer.Ordinal.Equals(entry.OwnerId, envelope.OwnerId)
            && StringComparer.Ordinal.Equals(entry.TaskScope.TaskId, envelope.TaskScope?.TaskId)
            && StringComparer.Ordinal.Equals(entry.TaskScope.OwnerId, envelope.OwnerId)
            && StringComparer.Ordinal.Equals(entry.TaskScope.OwnerId, envelope.TaskScope?.OwnerId)
            && StringComparer.Ordinal.Equals(entry.TaskScope.RouteId, envelope.TaskScope?.RouteId)
            && StringComparer.Ordinal.Equals(entry.TaskScope.ProviderId, envelope.TaskScope?.ProviderId)
            && StringComparer.Ordinal.Equals(entry.TaskScope.ProfileId, envelope.TaskScope?.ProfileId)
            && StringComparer.Ordinal.Equals(entry.TaskScope.IdempotencyKey, envelope.TaskScope?.IdempotencyKey)
            && StringComparer.Ordinal.Equals(entry.TaskScope.RequestPayloadHash, envelope.TaskScope?.RequestPayloadHash)
            && StringComparer.Ordinal.Equals(entry.TaskScope.OutputSchemaId, envelope.TaskScope?.OutputSchemaId)
            && entry.TaskScope.OutputSchemaMajor == envelope.TaskScope?.OutputSchemaMajor
            && entry.TaskScope.OutputSchemaMinor == envelope.TaskScope?.OutputSchemaMinor
            && StringComparer.Ordinal.Equals(entry.TaskScope.SettlementRequirement, envelope.TaskScope?.SettlementRequirement)
            && StringComparer.Ordinal.Equals(entry.CampaignGuid, envelope.CampaignGuid)
            && StringComparer.Ordinal.Equals(entry.TimelineId, envelope.TimelineId)
            && StringComparer.Ordinal.Equals(entry.SessionId, targetSessionId)
            && entry.SessionGeneration == envelope.SessionGeneration;
    }

    private static bool SameCancellationScope(TaskScopeEnvelope left, TaskScopeEnvelope? right)
    {
        return right != null
            && StringComparer.Ordinal.Equals(left.TaskId, right.TaskId)
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

    private void AddPendingCancellationUnsafe(PipeEnvelope envelope, string targetSessionId)
    {
        var key = BuildCancellationKey(envelope.OwnerId, envelope.CampaignGuid, envelope.TimelineId, targetSessionId, envelope.SessionGeneration, envelope.TaskScope!);
        PrunePendingCancellationsUnsafe();
        pendingCancellations[key] = new PendingCancellation(envelope.OwnerId, envelope.CampaignGuid, envelope.TimelineId, targetSessionId, envelope.SessionGeneration, CloneTaskScope(envelope.TaskScope!), DateTimeOffset.UtcNow.AddSeconds(30));
        while (pendingCancellations.Count > MaximumPendingCancellations)
        {
            var oldest = pendingCancellations.OrderBy(item => item.Value.ExpiresAt).First();
            pendingCancellations.Remove(oldest.Key);
        }
    }

    private bool ConsumePendingCancellationUnsafe(PipeEnvelope envelope)
    {
        PrunePendingCancellationsUnsafe();
        var key = BuildCancellationKey(envelope.OwnerId, envelope.CampaignGuid, envelope.TimelineId, envelope.SessionId, envelope.SessionGeneration, envelope.TaskScope!);
        return pendingCancellations.Remove(key);
    }

    private void PrunePendingCancellationsUnsafe()
    {
        var expired = pendingCancellations.Where(item => item.Value.ExpiresAt <= DateTimeOffset.UtcNow).Select(item => item.Key).ToList();
        for (var index = 0; index < expired.Count; index++) pendingCancellations.Remove(expired[index]);
    }

    private static string BuildCancellationKey(string ownerId, string campaignGuid, string timelineId, string sessionId, long sessionGeneration, TaskScopeEnvelope scope)
    {
        return string.Join("\u001f", new[]
        {
            ownerId,
            campaignGuid,
            timelineId,
            sessionId,
            sessionGeneration.ToString(),
            scope.TaskId,
            scope.RouteId,
            scope.ProviderId,
            scope.ProfileId,
            scope.IdempotencyKey,
            scope.RequestPayloadHash,
            scope.OutputSchemaId,
            scope.OutputSchemaMajor.ToString(),
            scope.OutputSchemaMinor.ToString(),
            scope.SettlementRequirement
        });
    }

    private async Task HandleStorageBusinessAsync(ConnectionContext connection, PipeEnvelope envelope, CancellationToken cancellationToken)
    {
        if (storageBackend == null)
        {
            await WriteResponseAsync(connection, BuildErrorResponse(connection, envelope, "storage.unavailable", "retryable_reject"), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!StorageBusinessFrameAdapter.TryParse(envelope, out var request, out var parseError) || request == null)
        {
            await WriteResponseAsync(connection, BuildErrorResponse(connection, envelope, parseError, "rejected"), cancellationToken).ConfigureAwait(false);
            return;
        }

        var ledgerEntry = GetOrCreateTaskLedger(envelope, out var ledgerCreated);
        if (!StringComparer.Ordinal.Equals(ledgerEntry.MessageId, envelope.MessageId) && ledgerEntry.Template == null)
        {
            await WriteResponseAsync(connection, BuildErrorResponse(connection, envelope, "task_in_progress", "retryable_reject"), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (ledgerEntry.Template != null)
        {
            await WriteResponseAsync(connection, BuildResponseFromTemplate(connection, envelope, ledgerEntry.Template, "terminal_replay"), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!RegisterActiveTask(envelope.TaskScope!.TaskId, cancellationToken, out var taskCancellation))
        {
            if (ledgerCreated) RemoveTaskLedger(envelope);
            await WriteResponseAsync(connection, BuildErrorResponse(connection, envelope, "task_in_progress", "retryable_reject"), cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            var context = CreateStorageRequestContext(connection, envelope);
            var result = await storageBackend.ExecuteBusinessAsync(request, context, taskCancellation.Token).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                var error = result.Error;
                var retryable = error != null && (error.Retryable || error.Category == FrameworkErrorCategory.Cancelled || error.Category == FrameworkErrorCategory.Timeout);
                var errorResponse = BuildErrorResponse(connection, envelope, error?.Code ?? "storage.operation_failed", retryable ? "retryable_reject" : "rejected", error);
                var errorWrite = await WriteResponseAsync(connection, errorResponse, cancellationToken).ConfigureAwait(false);
                if (!retryable && errorWrite.FrameComplete) SetTaskTemplate(ledgerEntry, ToTemplate(errorResponse));
                else RemoveTaskLedger(envelope);
                return;
            }

            var businessResult = result.Value;
            var responseType = IsRagBusinessMessage(envelope.MessageType) ? ProtocolConstants.MessageTypeRagResult : ProtocolConstants.MessageTypeStorageResult;
            var responseSchema = IsRagBusinessMessage(envelope.MessageType) ? "marcus-awake.rag.result.v1" : "marcus-awake.storage.result.v1";
            var response = BuildResponse(
                connection,
                envelope,
                responseType,
                responseSchema,
                businessResult.PayloadJson,
                envelope.TaskScope,
                string.IsNullOrWhiteSpace(businessResult.Outcome) ? ProtocolConstants.OutcomeAccepted : businessResult.Outcome,
                ackStatus: businessResult.Durable ? ProtocolConstants.AckDurablyRecorded : ProtocolConstants.AckAccepted,
                nonDurable: !businessResult.Durable,
                eventIndex: businessResult.EventIndex);
            MaybeCrashAfterCommitBeforeResponse(envelope, businessResult);
            var writeResult = await WriteResponseAsync(connection, response, cancellationToken).ConfigureAwait(false);
            if (writeResult.FrameComplete) SetTaskTemplate(ledgerEntry, ToTemplate(response));
            else RemoveTaskLedger(envelope);
        }
        catch (OperationCanceledException)
        {
            RemoveTaskLedger(envelope);
            if (!cancellationToken.IsCancellationRequested)
            {
                var response = BuildErrorResponse(connection, envelope, "awake.cancelled", "retryable_reject");
                await WriteResponseAsync(connection, response, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            RemoveTaskLedger(envelope);
            Console.Error.WriteLine("storage_dispatch_failed:" + exception.GetType().Name + ":correlation=" + envelope.CorrelationId + ":message=" + envelope.MessageType + ":" + envelope.MessageId);
            Console.Error.Flush();
            var response = BuildErrorResponse(connection, envelope, "storage.internal_failure", "retryable_reject");
            await WriteResponseAsync(connection, response, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CompleteActiveTask(envelope.TaskScope!.TaskId);
        }
    }

    private async Task HandleProviderBusinessAsync(ConnectionContext connection, PipeEnvelope envelope, CancellationToken cancellationToken)
    {
        if (providerRegistry == null)
        {
            var unavailable = BuildProviderErrorResponse(connection, envelope, ProviderWireAdapter.RuntimeUnavailable());
            await WriteResponseAsync(connection, unavailable, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!ProviderWireAdapter.TryParse(envelope, out var request, out var parseError) || request == null)
        {
            await WriteResponseAsync(connection, BuildProviderErrorResponse(connection, envelope, ProviderWireAdapter.SchemaError(parseError)), cancellationToken).ConfigureAwait(false);
            return;
        }

        var idempotencyDisposition = ReserveProviderIdempotency(envelope, request, out var ledgerEntry, out var replayTemplate);
        if (idempotencyDisposition == ProviderIdempotencyDisposition.Conflict)
        {
            await WriteResponseAsync(connection, BuildProviderErrorResponse(connection, envelope, request, ProviderWireAdapter.IdempotencyConflict()), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (idempotencyDisposition == ProviderIdempotencyDisposition.Capacity)
        {
            await WriteResponseAsync(connection, BuildProviderErrorResponse(connection, envelope, request, ProviderWireAdapter.LedgerCapacity()), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (idempotencyDisposition == ProviderIdempotencyDisposition.RestartApplied)
        {
            await WriteResponseAsync(connection, BuildProviderErrorResponse(connection, envelope, request, new ProviderWireError("provider.request_already_applied", "conflict", false, false, "The request was already applied before the runtime restarted.")), cancellationToken).ConfigureAwait(false);
            return;
        }
        if (idempotencyDisposition == ProviderIdempotencyDisposition.RestartUnknown)
        {
            await WriteResponseAsync(connection, BuildProviderErrorResponse(connection, envelope, request, new ProviderWireError("provider.request_outcome_unknown", "unavailable", true, false, "The request outcome is unknown after the runtime restarted; reconcile before retrying.")), cancellationToken).ConfigureAwait(false);
            return;
        }
        if (idempotencyDisposition == ProviderIdempotencyDisposition.RestartFailed)
        {
            await WriteResponseAsync(connection, BuildProviderErrorResponse(connection, envelope, request, new ProviderWireError("provider.request_failed_before_restart", "internal_failure", false, false, "The request failed before the runtime restarted.")), cancellationToken).ConfigureAwait(false);
            return;
        }
        if (idempotencyDisposition == ProviderIdempotencyDisposition.DurableUnavailable)
        {
            await WriteResponseAsync(connection, BuildProviderErrorResponse(connection, envelope, request, new ProviderWireError("provider.idempotency_unavailable", "unavailable", true, false, "Durable Provider idempotency state is unavailable.")), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (idempotencyDisposition == ProviderIdempotencyDisposition.Replay
            && request.Operation == ProviderWireOperation.Stream
            && ResolveNowUnixMilliseconds() >= envelope.DeadlineUnixMilliseconds)
        {
            await WriteResponseAsync(connection, BuildProviderErrorResponse(connection, envelope, request, ProviderWireAdapter.DeadlineExpired()), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (idempotencyDisposition == ProviderIdempotencyDisposition.Replay && request.Operation == ProviderWireOperation.Stream && ledgerEntry.StreamTemplates != null)
        {
            await ReplayStreamAsync(connection, envelope, ledgerEntry.StreamTemplates, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (idempotencyDisposition == ProviderIdempotencyDisposition.Replay && replayTemplate != null)
        {
            await WriteResponseAsync(connection, BuildResponseFromTemplate(connection, envelope, replayTemplate, "terminal_replay"), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (idempotencyDisposition == ProviderIdempotencyDisposition.InProgress)
        {
            await WriteResponseAsync(connection, BuildProviderErrorResponse(connection, envelope, request, ProviderWireAdapter.TaskInProgress()), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!StringComparer.Ordinal.Equals(ledgerEntry.MessageId, envelope.MessageId) && ledgerEntry.Template == null && ledgerEntry.StreamTemplates == null)
        {
            await WriteResponseAsync(connection, BuildProviderErrorResponse(connection, envelope, ProviderWireAdapter.TaskInProgress()), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (ledgerEntry.Template != null)
        {
            await WriteResponseAsync(connection, BuildResponseFromTemplate(connection, envelope, ledgerEntry.Template, "terminal_replay"), cancellationToken).ConfigureAwait(false);
            return;
        }

        var registration = RegisterProviderActiveTask(envelope, ledgerEntry, cancellationToken, out var taskCancellation, out var activeTask);
        if (registration == ProviderTaskRegistration.CancelledBeforeAdmission)
        {
            var cancelledBeforeAdmission = BuildProviderErrorResponse(connection, envelope, request, ProviderWireAdapter.Cancelled());
            var cancelledWrite = await WriteResponseAsync(connection, cancelledBeforeAdmission, cancellationToken).ConfigureAwait(false);
            if (cancelledWrite.FrameComplete)
            {
                SetTaskTemplate(ledgerEntry, ToTemplate(cancelledBeforeAdmission));
                SetProviderOutcome(envelope, RuntimeProviderOutcomeStatus.Retryable);
            }
            else SetProviderOutcome(envelope, RuntimeProviderOutcomeStatus.Unknown);
            return;
        }

        if (registration != ProviderTaskRegistration.Registered)
        {
            await WriteResponseAsync(connection, BuildProviderErrorResponse(connection, envelope, ProviderWireAdapter.TaskInProgress()), cancellationToken).ConfigureAwait(false);
            return;
        }

        var providerStarted = false;
        try
        {
            if (request.Operation == ProviderWireOperation.Stream)
            {
                await HandleProviderStreamAsync(connection, envelope, request, ledgerEntry, activeTask, taskCancellation, cancellationToken).ConfigureAwait(false);
                return;
            }

            ProviderWireResult result;
            if (!activeTask.TryBeginProvider())
            {
                result = ProviderWireResult.Failure(ProviderWireAdapter.Cancelled());
                SetTaskLifecycle(ledgerEntry, ProviderTaskLifecycle.Cancelled);
            }
            else
            {
                providerStarted = true;
                SetTaskLifecycle(ledgerEntry, ProviderTaskLifecycle.ProviderInFlight);
                var deadline = DateTimeOffset.FromUnixTimeMilliseconds(envelope.DeadlineUnixMilliseconds);
                result = request.Operation switch
                {
                    ProviderWireOperation.ProfileUpsert => providerRegistry.Upsert(envelope, request),
                    ProviderWireOperation.CredentialUpsert => await providerRegistry.UpsertCredentialAsync(envelope, request, taskCancellation.Token).ConfigureAwait(false),
                    ProviderWireOperation.ProfileRemove => providerRegistry.Remove(envelope, request),
                    ProviderWireOperation.Models => await providerRegistry.ListModelsAsync(envelope, request, deadline, taskCancellation.Token).ConfigureAwait(false),
                    ProviderWireOperation.Complete => await providerRegistry.CompleteAsync(envelope, request, deadline, taskCancellation.Token).ConfigureAwait(false),
                    _ => ProviderWireResult.Failure(ProviderWireAdapter.SchemaError())
                };
            }

            if (!activeTask.TryCaptureResult())
            {
                result = ProviderWireResult.Failure(ProviderWireAdapter.Cancelled());
                SetTaskLifecycle(ledgerEntry, ProviderTaskLifecycle.Cancelled);
            }
            else
            {
                SetTaskLifecycle(ledgerEntry, ProviderTaskLifecycle.ResultCaptured);
            }

            if (!activeTask.TryClaimResponse(result.IsSuccess))
            {
                result = ProviderWireResult.Failure(ProviderWireAdapter.Cancelled());
                SetTaskLifecycle(ledgerEntry, ProviderTaskLifecycle.Cancelled);
            }
            else
            {
                SetTaskLifecycle(ledgerEntry, result.IsSuccess ? ProviderTaskLifecycle.ResponsePublished : ProviderTaskLifecycle.Failed);
            }

            if (!result.IsSuccess)
            {
                var error = result.Error!;
                var errorResponse = BuildProviderErrorResponse(connection, envelope, request, error);
                if (!error.Retryable || StringComparer.Ordinal.Equals(error.Category, "cancelled")) SetTaskTemplate(ledgerEntry, ToTemplate(errorResponse));
                var writeResult = await WriteResponseAsync(connection, errorResponse, cancellationToken).ConfigureAwait(false);
                if (writeResult.FrameComplete)
                {
                    SetProviderOutcome(envelope, ResolveProviderOutcomeStatus(false, error, providerStarted, true));
                }
                else
                {
                    SetProviderOutcome(envelope, RuntimeProviderOutcomeStatus.Unknown);
                }
                return;
            }

            var responseMessageType = request.Operation switch
            {
                ProviderWireOperation.ProfileUpsert => "provider_profile_result",
                ProviderWireOperation.CredentialUpsert => "provider_credential_result",
                ProviderWireOperation.ProfileRemove => "provider_profile_result",
                ProviderWireOperation.Models => "provider_models_result",
                ProviderWireOperation.Complete => "provider_result",
                _ => "provider_result"
            };
            var responseSchema = request.Operation switch
            {
                ProviderWireOperation.ProfileUpsert => ProtocolConstants.ProviderProfileResultSchemaV1,
                ProviderWireOperation.CredentialUpsert => ProtocolConstants.ProviderCredentialResultSchemaV1,
                ProviderWireOperation.ProfileRemove => ProtocolConstants.ProviderProfileResultSchemaV1,
                ProviderWireOperation.Models => ProtocolConstants.ProviderModelsResultSchemaV1,
                ProviderWireOperation.Complete => ProtocolConstants.ProviderResultSchemaV1,
                _ => ProtocolConstants.ProviderResultSchemaV1
            };
            var response = BuildResponse(connection, envelope, responseMessageType, responseSchema, result.PayloadJson!, envelope.TaskScope, ProtocolConstants.OutcomeAccepted, ackStatus: ProtocolConstants.AckAccepted, nonDurable: true);
            SetTaskTemplate(ledgerEntry, ToTemplate(response));
            var successWrite = await WriteResponseAsync(connection, response, cancellationToken).ConfigureAwait(false);
            if (successWrite.FrameComplete)
            {
                SetProviderOutcome(envelope, RuntimeProviderOutcomeStatus.Applied);
            }
            else
            {
                SetProviderOutcome(envelope, RuntimeProviderOutcomeStatus.Unknown);
            }
        }
        catch (OperationCanceledException)
        {
            activeTask.MarkCancelled();
            SetTaskLifecycle(ledgerEntry, ProviderTaskLifecycle.Cancelled);
            if (!cancellationToken.IsCancellationRequested)
            {
                var response = BuildProviderErrorResponse(connection, envelope, request, ProviderWireAdapter.Cancelled());
                SetTaskTemplate(ledgerEntry, ToTemplate(response));
                var writeResult = await WriteResponseAsync(connection, response, cancellationToken).ConfigureAwait(false);
                if (writeResult.FrameComplete)
                {
                    SetProviderOutcome(envelope, RuntimeProviderOutcomeStatus.Unknown);
                }
                else SetProviderOutcome(envelope, RuntimeProviderOutcomeStatus.Unknown);
            }
            else SetProviderOutcome(envelope, RuntimeProviderOutcomeStatus.Unknown);
        }
        catch (Exception exception)
        {
            activeTask.MarkFailed();
            SetTaskLifecycle(ledgerEntry, ProviderTaskLifecycle.Failed);
            Console.Error.WriteLine("provider_dispatch_failed:" + exception.GetType().Name + ":correlation=" + envelope.CorrelationId + ":message=" + envelope.MessageType + ":" + envelope.MessageId);
            Console.Error.Flush();
            var response = BuildProviderErrorResponse(connection, envelope, request, new ProviderWireError("provider.runtime_failure", "internal_failure", false, false, "Provider runtime failed while executing the request."));
            SetTaskTemplate(ledgerEntry, ToTemplate(response));
            var writeResult = await WriteResponseAsync(connection, response, cancellationToken).ConfigureAwait(false);
            if (writeResult.FrameComplete)
            {
                SetProviderOutcome(envelope, ResolveProviderOutcomeStatus(false, new ProviderWireError("provider.runtime_failure", "internal_failure", false, false, "Provider runtime failed while executing the request."), providerStarted, writeResult.FrameComplete));
            }
            else SetProviderOutcome(envelope, RuntimeProviderOutcomeStatus.Unknown);
        }
        finally
        {
            CompleteActiveTask(envelope.TaskScope!.TaskId);
        }
    }

    private async Task HandleProviderStreamAsync(
        ConnectionContext connection,
        PipeEnvelope envelope,
        ProviderWireRequest request,
        LedgerEntry ledgerEntry,
        ActiveTask activeTask,
        CancellationTokenSource taskCancellation,
        CancellationToken connectionCancellationToken)
    {
        if (request.ResourceBudget == null)
        {
            await WriteResponseAsync(connection, BuildProviderErrorResponse(connection, envelope, ProviderWireAdapter.SchemaError("stream_budget_invalid")), connectionCancellationToken).ConfigureAwait(false);
            return;
        }

        var working = new StreamWorkingLedger(request.ResourceBudget);
        var stateMachine = new ProviderStreamStateMachine(envelope.TaskScope!.TaskId, envelope.TaskScope.ProviderId, envelope.TaskScope.ProfileId);
        var usage = new StreamUsageTracker();
        var deadline = DateTimeOffset.FromUnixTimeMilliseconds(envelope.DeadlineUnixMilliseconds);
        using var streamCancellation = CancellationTokenSource.CreateLinkedTokenSource(taskCancellation.Token, connectionCancellationToken);
        var providerCancellationToken = streamCancellation.Token;
        ProviderRegistry.ProviderStreamCandidateSnapshot? candidateSnapshot = null;
        ProviderWireError? admissionError = null;
        ProviderStreamSession? session = null;
        var providerStarted = false;
        var terminalWritten = false;
        var aborted = false;

        try
        {
            if (providerRegistry == null)
            {
                admissionError = ProviderWireAdapter.RuntimeUnavailable();
            }
            else
            {
                try
                {
                    candidateSnapshot = providerRegistry.DescribeCandidates(envelope, request);
                }
                catch (Exception)
                {
                    admissionError = ProviderWireAdapter.RuntimeUnavailable();
                }
            }

            var startedDecision = stateMachine.Accept("started", working.NextStreamSequence, request.ProviderId);
            if (!startedDecision.IsAccepted)
            {
                working.Abort();
                aborted = true;
                return;
            }

            var started = BuildProviderStreamEventResponse(connection, envelope, request, "started", working.NextStreamSequence, request.ProviderId, string.Empty, string.Empty, null, null, null, string.Empty, string.Empty, null);
            if (!await WriteStreamFrameAsync(connection, started, working, 0, false, 0, connectionCancellationToken).ConfigureAwait(false))
            {
                aborted = true;
                return;
            }

            providerStarted = activeTask.TryBeginProvider();
            if (!providerStarted)
            {
                var cancelled = await EmitSyntheticStreamTerminalAsync(connection, envelope, request, stateMachine, working, usage, activeTask, false, ProviderWireAdapter.Cancelled(), connectionCancellationToken).ConfigureAwait(false);
                terminalWritten = cancelled == StreamWriteResult.Written;
                aborted = !terminalWritten;
                return;
            }

            SetTaskLifecycle(ledgerEntry, ProviderTaskLifecycle.ProviderInFlight);
            if (admissionError != null)
            {
                var admission = await EmitSyntheticStreamTerminalAsync(connection, envelope, request, stateMachine, working, usage, activeTask, true, admissionError, connectionCancellationToken).ConfigureAwait(false);
                terminalWritten = admission == StreamWriteResult.Written;
                aborted = !terminalWritten;
                return;
            }

            if (candidateSnapshot == null || candidateSnapshot.Error != null)
            {
                var error = candidateSnapshot?.Error ?? ProviderWireAdapter.RouteNoCandidate();
                var candidate = await EmitSyntheticStreamTerminalAsync(connection, envelope, request, stateMachine, working, usage, activeTask, true, error, connectionCancellationToken).ConfigureAwait(false);
                terminalWritten = candidate == StreamWriteResult.Written;
                aborted = !terminalWritten;
                return;
            }

            if (providerCancellationToken.IsCancellationRequested)
            {
                var cancelled = await EmitSyntheticStreamTerminalAsync(connection, envelope, request, stateMachine, working, usage, activeTask, true, ProviderWireAdapter.Cancelled(), connectionCancellationToken).ConfigureAwait(false);
                terminalWritten = cancelled == StreamWriteResult.Written;
                aborted = !terminalWritten;
                return;
            }

            var begin = await providerRegistry!.BeginStreamAsync(envelope, request, candidateSnapshot, deadline, providerCancellationToken).ConfigureAwait(false);
            if (!begin.IsSuccess && begin.Error != null && StringComparer.Ordinal.Equals(begin.Error.ErrorCode, "provider_profile_changed"))
            {
                begin = await providerRegistry.BeginStreamAsync(envelope, request, deadline, providerCancellationToken).ConfigureAwait(false);
            }
            if (!begin.IsSuccess)
            {
                var beginError = begin.Error ?? ProviderWireAdapter.RuntimeUnavailable();
                var beginTerminal = await EmitSyntheticStreamTerminalAsync(connection, envelope, request, stateMachine, working, usage, activeTask, true, beginError, connectionCancellationToken).ConfigureAwait(false);
                terminalWritten = beginTerminal == StreamWriteResult.Written;
                aborted = !terminalWritten;
                return;
            }

            session = begin.Session!;
            while (!terminalWritten && !aborted)
            {
                if (providerCancellationToken.IsCancellationRequested)
                {
                    var cancelled = await EmitSyntheticStreamTerminalAsync(connection, envelope, request, stateMachine, working, usage, activeTask, true, ProviderWireAdapter.Cancelled(), connectionCancellationToken).ConfigureAwait(false);
                    terminalWritten = cancelled == StreamWriteResult.Written;
                    aborted = !terminalWritten;
                    break;
                }

                var step = await session.MoveNextAsync(providerCancellationToken).ConfigureAwait(false);
                if (step.Error != null)
                {
                    var stepTerminal = await EmitSyntheticStreamTerminalAsync(connection, envelope, request, stateMachine, working, usage, activeTask, true, step.Error, connectionCancellationToken).ConfigureAwait(false);
                    terminalWritten = stepTerminal == StreamWriteResult.Written;
                    aborted = !terminalWritten;
                    break;
                }

                if (step.End)
                {
                    var endError = providerCancellationToken.IsCancellationRequested ? ProviderWireAdapter.Cancelled() : ProviderWireAdapter.StreamIncomplete();
                    var endTerminal = await EmitSyntheticStreamTerminalAsync(connection, envelope, request, stateMachine, working, usage, activeTask, true, endError, connectionCancellationToken).ConfigureAwait(false);
                    terminalWritten = endTerminal == StreamWriteResult.Written;
                    aborted = !terminalWritten;
                    break;
                }

                var dispatch = await ForwardProviderStreamEventAsync(connection, envelope, request, stateMachine, working, usage, activeTask, providerStarted, step.Value!, connectionCancellationToken).ConfigureAwait(false);
                if (dispatch == StreamDispatchResult.TerminalWritten)
                {
                    terminalWritten = true;
                    await ExerciseLateStreamEventTestHookAsync(connection, envelope, request, stateMachine, working, usage, activeTask, providerStarted, connectionCancellationToken).ConfigureAwait(false);
                }
                else if (dispatch == StreamDispatchResult.Aborted)
                {
                    aborted = true;
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (!terminalWritten && !aborted)
            {
                var cancelled = await EmitSyntheticStreamTerminalAsync(connection, envelope, request, stateMachine, working, usage, activeTask, providerStarted, ProviderWireAdapter.Cancelled(), connectionCancellationToken).ConfigureAwait(false);
                terminalWritten = cancelled == StreamWriteResult.Written;
                aborted = !terminalWritten;
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("provider_stream_dispatch_failed:" + exception.GetType().Name + ":correlation=" + envelope.CorrelationId + ":message=" + envelope.MessageId);
            Console.Error.Flush();
            if (!terminalWritten && !aborted)
            {
                var failed = await EmitSyntheticStreamTerminalAsync(connection, envelope, request, stateMachine, working, usage, activeTask, providerStarted, StreamRuntimeFailure(), connectionCancellationToken).ConfigureAwait(false);
                terminalWritten = failed == StreamWriteResult.Written;
                aborted = !terminalWritten;
            }
        }
        finally
        {
            if (session != null)
            {
                ProviderWireError? disposeError = null;
                try
                {
                    disposeError = await session.DisposeSafeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    disposeError = new ProviderWireError("provider_stream_dispose_failed", "internal_failure", false, false, "Provider stream cleanup failed.");
                    Console.Error.WriteLine("provider_stream_dispose_failed:" + exception.GetType().Name + ":correlation=" + envelope.CorrelationId);
                    Console.Error.Flush();
                }

                if (disposeError != null)
                {
                    if (terminalWritten)
                    {
                        Console.Error.WriteLine("provider_stream_dispose_after_terminal:" + disposeError.ErrorCode + ":correlation=" + envelope.CorrelationId);
                        Console.Error.Flush();
                    }
                    else if (!aborted)
                    {
                        var disposeTerminal = await EmitSyntheticStreamTerminalAsync(connection, envelope, request, stateMachine, working, usage, activeTask, providerStarted, disposeError, connectionCancellationToken).ConfigureAwait(false);
                        terminalWritten = disposeTerminal == StreamWriteResult.Written;
                        aborted = !terminalWritten;
                    }
                }
            }

            if (terminalWritten) CommitStreamTemplates(envelope, ledgerEntry, working);
            else
            {
                working.Abort();
                SetProviderOutcome(envelope, RuntimeProviderOutcomeStatus.Unknown);
                RemoveTaskLedger(envelope);
            }
        }
    }

    private async Task ExerciseLateStreamEventTestHookAsync(
        ConnectionContext connection,
        PipeEnvelope envelope,
        ProviderWireRequest request,
        ProviderStreamStateMachine stateMachine,
        StreamWorkingLedger working,
        StreamUsageTracker usage,
        ActiveTask activeTask,
        bool providerStarted,
        CancellationToken cancellationToken)
    {
        if (!StringComparer.Ordinal.Equals(Environment.GetEnvironmentVariable(RuntimeTestModeEnvironmentVariable), "p3d-a2")
            || !StringComparer.Ordinal.Equals(Environment.GetEnvironmentVariable(RuntimeTestLateStreamEventMessageEnvironmentVariable), envelope.MessageId)) return;

        var lateDecision = stateMachine.Accept(
            "text_delta",
            working.NextStreamSequence,
            stateMachine.ActiveProviderId,
            hasText: true);
        if (lateDecision.IsAccepted || !StringComparer.Ordinal.Equals(lateDecision.ErrorCode, "stream_terminal_already_emitted"))
        {
            throw new InvalidOperationException("test_late_stream_event_not_rejected");
        }

        var lateEvent = new ProviderStreamEventProjection(
            "text_delta",
            working.NextStreamSequence,
            stateMachine.ActiveProviderId,
            string.Empty,
            "late",
            null,
            null,
            null,
            string.Empty,
            string.Empty,
            null);
        var dispatch = await ForwardProviderStreamEventAsync(
            connection,
            envelope,
            request,
            stateMachine,
            working,
            usage,
            activeTask,
            providerStarted,
            lateEvent,
            cancellationToken).ConfigureAwait(false);
        if (dispatch != StreamDispatchResult.Aborted)
        {
            throw new InvalidOperationException("test_late_stream_event_forwarded");
        }
    }

    private async Task<StreamDispatchResult> ForwardProviderStreamEventAsync(
        ConnectionContext connection,
        PipeEnvelope envelope,
        ProviderWireRequest request,
        ProviderStreamStateMachine stateMachine,
        StreamWorkingLedger working,
        StreamUsageTracker usage,
        ActiveTask activeTask,
        bool providerStarted,
        ProviderStreamEventProjection providerEvent,
        CancellationToken cancellationToken)
    {
        if (StringComparer.Ordinal.Equals(providerEvent.EventKind, "started"))
        {
            return StringComparer.Ordinal.Equals(providerEvent.ProviderId, stateMachine.ActiveProviderId)
                ? StreamDispatchResult.Continue
                : await EmitSyntheticStreamFailureAsync(connection, envelope, request, stateMachine, working, usage, activeTask, providerStarted, StreamProtocolFailure("stream_provider_mismatch"), cancellationToken).ConfigureAwait(false);
        }

        var eventKind = providerEvent.EventKind;
        var terminal = StringComparer.Ordinal.Equals(eventKind, "completed")
            || StringComparer.Ordinal.Equals(eventKind, "cancelled")
            || StringComparer.Ordinal.Equals(eventKind, "failed");
        var hasText = !string.IsNullOrEmpty(providerEvent.Text);
        var hasUsage = providerEvent.InputTokens.HasValue || providerEvent.OutputTokens.HasValue;
        var hasError = providerEvent.Error != null;
        var structuredJson = string.IsNullOrWhiteSpace(providerEvent.StructuredJson) ? null : providerEvent.StructuredJson;
        if (structuredJson != null && !StringComparer.Ordinal.Equals(eventKind, "completed"))
        {
            return await EmitSyntheticStreamFailureAsync(connection, envelope, request, stateMachine, working, usage, activeTask, providerStarted, StreamProtocolFailure("structured_output_invalid"), cancellationToken).ConfigureAwait(false);
        }

        if (structuredJson != null && !StructuredJsonCanonicalizer.TryCanonicalizeObject(structuredJson, out structuredJson, out var structuredError))
        {
            return await EmitSyntheticStreamFailureAsync(connection, envelope, request, stateMachine, working, usage, activeTask, providerStarted, StreamProtocolFailure(structuredError), cancellationToken).ConfigureAwait(false);
        }

        int outputBytes;
        try
        {
            outputBytes = Utf8ByteCount(providerEvent.Text);
            if (structuredJson != null) outputBytes += Utf8ByteCount(structuredJson);
        }
        catch (EncoderFallbackException)
        {
            return await EmitSyntheticStreamFailureAsync(connection, envelope, request, stateMachine, working, usage, activeTask, providerStarted, StreamProtocolFailure("stream_text_invalid_utf8"), cancellationToken).ConfigureAwait(false);
        }

        var knownTokens = usage.TotalKnownTokens;
        ProviderWireError? usageError = null;
        if (StringComparer.Ordinal.Equals(eventKind, "route_changed"))
        {
            if (!usage.TrySettleAttempt(request.ResourceBudget!.Tokens, out knownTokens, out usageError))
            {
                return await EmitSyntheticStreamFailureAsync(connection, envelope, request, stateMachine, working, usage, activeTask, providerStarted, usageError!, cancellationToken).ConfigureAwait(false);
            }
        }
        else if (StringComparer.Ordinal.Equals(eventKind, "usage_update") || StringComparer.Ordinal.Equals(eventKind, "completed"))
        {
            if (!usage.TryObserve(providerEvent, request.ResourceBudget!.Tokens, out knownTokens, out usageError))
            {
                return await EmitSyntheticStreamFailureAsync(connection, envelope, request, stateMachine, working, usage, activeTask, providerStarted, usageError!, cancellationToken).ConfigureAwait(false);
            }
        }

        if (terminal && !usage.TrySettleAttempt(request.ResourceBudget!.Tokens, out knownTokens, out usageError))
        {
            return await EmitSyntheticStreamFailureAsync(connection, envelope, request, stateMachine, working, usage, activeTask, providerStarted, usageError!, cancellationToken).ConfigureAwait(false);
        }

        if (!terminal && !working.CanWriteNonTerminal(outputBytes, StringComparer.Ordinal.Equals(eventKind, "text_delta") ? 1 : 0, knownTokens))
        {
            return await EmitSyntheticStreamFailureAsync(connection, envelope, request, stateMachine, working, usage, activeTask, providerStarted, ProviderWireAdapter.ResourceExhausted(), cancellationToken).ConfigureAwait(false);
        }

        if (terminal && !working.HasTerminalSlot()) return StreamDispatchResult.Aborted;

        var sequence = working.NextStreamSequence;
        var decision = stateMachine.Accept(
            eventKind,
            sequence,
            providerEvent.ProviderId,
            providerEvent.FromProviderId,
            providerEvent.ToProviderId,
            terminal,
            hasText,
            hasUsage,
            hasError,
            structuredJson != null);
        if (!decision.IsAccepted)
        {
            return await EmitSyntheticStreamFailureAsync(connection, envelope, request, stateMachine, working, usage, activeTask, providerStarted, StreamProtocolFailure(decision.ErrorCode), cancellationToken).ConfigureAwait(false);
        }

        if (terminal)
        {
            var error = StringComparer.Ordinal.Equals(eventKind, "completed") ? null : providerEvent.Error ?? (StringComparer.Ordinal.Equals(eventKind, "cancelled") ? ProviderWireAdapter.Cancelled() : StreamProtocolFailure("provider_terminal_error_missing"));
            var success = StringComparer.Ordinal.Equals(eventKind, "completed");
            var terminalResult = await EmitStreamTerminalAsync(connection, envelope, request, working, usage, activeTask, providerStarted, success, error, providerEvent.ProviderId, providerEvent.ModelId, structuredJson, outputBytes, knownTokens, cancellationToken).ConfigureAwait(false);
            return terminalResult == StreamWriteResult.Written ? StreamDispatchResult.TerminalWritten : StreamDispatchResult.Aborted;
        }

        var response = BuildProviderStreamEventResponse(connection, envelope, request, eventKind, sequence, providerEvent.ProviderId, providerEvent.ModelId, providerEvent.Text, providerEvent.InputTokens, providerEvent.OutputTokens, providerEvent.Error, providerEvent.FromProviderId, providerEvent.ToProviderId, structuredJson);
        return await WriteStreamFrameAsync(connection, response, working, outputBytes, StringComparer.Ordinal.Equals(eventKind, "text_delta"), knownTokens, cancellationToken).ConfigureAwait(false)
            ? StreamDispatchResult.Continue
            : StreamDispatchResult.Aborted;
    }

    private async Task<StreamDispatchResult> EmitSyntheticStreamFailureAsync(
        ConnectionContext connection,
        PipeEnvelope envelope,
        ProviderWireRequest request,
        ProviderStreamStateMachine stateMachine,
        StreamWorkingLedger working,
        StreamUsageTracker usage,
        ActiveTask activeTask,
        bool providerStarted,
        ProviderWireError error,
        CancellationToken cancellationToken)
    {
        var result = await EmitSyntheticStreamTerminalAsync(connection, envelope, request, stateMachine, working, usage, activeTask, providerStarted, error, cancellationToken).ConfigureAwait(false);
        return result == StreamWriteResult.Written ? StreamDispatchResult.TerminalWritten : StreamDispatchResult.Aborted;
    }

    private async Task<StreamWriteResult> EmitSyntheticStreamTerminalAsync(
        ConnectionContext connection,
        PipeEnvelope envelope,
        ProviderWireRequest request,
        ProviderStreamStateMachine stateMachine,
        StreamWorkingLedger working,
        StreamUsageTracker usage,
        ActiveTask activeTask,
        bool providerStarted,
        ProviderWireError error,
        CancellationToken cancellationToken)
    {
        var eventKind = IsCancellationError(error) ? "cancelled" : "failed";
        var decision = stateMachine.Accept(eventKind, working.NextStreamSequence, stateMachine.ActiveProviderId, terminal: true, hasError: true);
        if (!decision.IsAccepted) return StreamWriteResult.NotWritten;
        return await EmitStreamTerminalAsync(connection, envelope, request, working, usage, activeTask, providerStarted, false, error, stateMachine.ActiveProviderId, string.Empty, null, 0, usage.TotalKnownTokens, cancellationToken).ConfigureAwait(false);
    }

    private async Task<StreamWriteResult> EmitStreamTerminalAsync(
        ConnectionContext connection,
        PipeEnvelope envelope,
        ProviderWireRequest request,
        StreamWorkingLedger working,
        StreamUsageTracker usage,
        ActiveTask activeTask,
        bool providerStarted,
        bool success,
        ProviderWireError? error,
        string providerId,
        string modelId,
        string? structuredJson,
        int outputBytes,
        int knownTokens,
        CancellationToken cancellationToken)
    {
        if (!working.HasTerminalSlot()) return StreamWriteResult.NotWritten;

        var effectiveSuccess = success;
        var effectiveError = error;
        var effectiveStructuredJson = structuredJson;
        var effectiveOutputBytes = outputBytes;
        if (!working.CanWriteTerminal(effectiveOutputBytes, knownTokens))
        {
            if (!working.CanWriteTerminal(0, Math.Min(knownTokens, working.MaximumTokens))) return StreamWriteResult.NotWritten;
            effectiveSuccess = false;
            effectiveError = ProviderWireAdapter.ResourceExhausted();
            effectiveStructuredJson = null;
            effectiveOutputBytes = 0;
        }

        if (providerStarted)
        {
            if (!activeTask.TryCaptureResult() || !activeTask.TryClaimStreamTerminal(effectiveSuccess))
            {
                effectiveSuccess = false;
                effectiveError = ProviderWireAdapter.Cancelled();
                effectiveStructuredJson = null;
                effectiveOutputBytes = 0;
                activeTask.MarkCancelled();
            }
        }
        else
        {
            effectiveSuccess = false;
            effectiveError = ProviderWireAdapter.Cancelled();
            effectiveStructuredJson = null;
            effectiveOutputBytes = 0;
            activeTask.MarkCancelled();
        }

        if (!working.TryClaimTerminal()) return StreamWriteResult.NotWritten;
        var effectiveEventKind = effectiveSuccess ? "completed" : IsCancellationError(effectiveError) ? "cancelled" : "failed";
        var response = BuildProviderStreamEventResponse(connection, envelope, request, effectiveEventKind, working.NextStreamSequence, providerId, modelId, string.Empty, null, null, effectiveError, string.Empty, string.Empty, effectiveStructuredJson);
        if (!await WriteStreamFrameAsync(connection, response, working, effectiveOutputBytes, false, Math.Min(knownTokens, working.MaximumTokens), cancellationToken).ConfigureAwait(false)) return StreamWriteResult.NotWritten;

        working.MarkTerminalWritten(ResolveProviderOutcomeStatus(effectiveSuccess, effectiveError, providerStarted, true));
        SetTaskLifecycle(activeTask.Ledger, effectiveSuccess ? ProviderTaskLifecycle.ResponsePublished : IsCancellationError(effectiveError) ? ProviderTaskLifecycle.Cancelled : ProviderTaskLifecycle.Failed);
        return StreamWriteResult.Written;
    }

    private async Task<bool> WriteStreamFrameAsync(ConnectionContext connection, PipeEnvelope response, StreamWorkingLedger working, int outputBytes, bool textDelta, int knownTokens, CancellationToken cancellationToken)
    {
        try
        {
            var writeResult = await WriteResponseAsync(connection, response, cancellationToken).ConfigureAwait(false);
            if (!writeResult.FrameComplete)
            {
                working.Abort();
                return false;
            }

            working.RecordFrame(ToTemplate(response), outputBytes, textDelta, knownTokens);
            return true;
        }
        catch (OperationCanceledException)
        {
            working.Abort();
            return false;
        }
        catch (IOException)
        {
            connection.RequestClose();
            working.Abort();
            return false;
        }
    }

    private PipeEnvelope BuildProviderStreamEventResponse(
        ConnectionContext connection,
        PipeEnvelope request,
        ProviderWireRequest wireRequest,
        string eventKind,
        long streamSequence,
        string providerId,
        string modelId,
        string text,
        int? inputTokens,
        int? outputTokens,
        ProviderWireError? error,
        string fromProviderId,
        string toProviderId,
        string? structuredJson)
    {
        var projection = new ProviderStreamEventProjection(eventKind, streamSequence, providerId, modelId, text, inputTokens, outputTokens, error, fromProviderId, toProviderId, structuredJson);
        var payload = ProviderWireAdapter.BuildStreamEventPayload(wireRequest, projection);
        return BuildResponse(connection, request, ProtocolConstants.MessageTypeProviderStreamEvent, ProtocolConstants.ProviderStreamEventSchemaV1, payload, request.TaskScope, ProtocolConstants.OutcomeAccepted, error?.ErrorCode ?? string.Empty, ackStatus: ProtocolConstants.AckAccepted, nonDurable: true, eventIndex: streamSequence);
    }

    private void CommitStreamTemplates(PipeEnvelope envelope, LedgerEntry ledgerEntry, StreamWorkingLedger working)
    {
        var templates = working.SnapshotTemplates();
        var actualBytes = working.SerializedTemplateBytes;
        lock (sync)
        {
            EvictExpiredStreamLedgersUnsafe(ToUtcDateTime(ResolveNowUnixMilliseconds()));
            while (committedStreamLedgers.Count >= MaximumCommittedStreamLedgers
                || committedStreamReplayBytes + actualBytes > MaximumStreamReplayBytes)
            {
                if (!EvictOldestCommittedStreamLedgerUnsafe()) break;
            }

            if (ledgerEntry.StreamReplayReservationBytes > 0)
            {
                reservedStreamReplayBytes = Math.Max(0, reservedStreamReplayBytes - ledgerEntry.StreamReplayReservationBytes);
                ledgerEntry.StreamReplayReservationBytes = 0;
            }
            ledgerEntry.StreamTemplates = templates.ToList();
            ledgerEntry.Template = null;
            ledgerEntry.LifecycleState = ProviderTaskLifecycleName(ProviderTaskLifecycle.ResponsePublished);
            ledgerEntry.StreamReplayBytes = actualBytes;
            ledgerEntry.StreamReplayCommittedAtUtc = ToUtcDateTime(ResolveNowUnixMilliseconds());
            ledgerEntry.StreamReplayState = StreamLedgerState.Committed;
            ledgerEntry.StreamReplayNode = committedStreamLedgers.AddLast(ledgerEntry);
            committedStreamReplayBytes += actualBytes;
        }
        SetProviderOutcome(envelope, working.TerminalOutcomeStatus);
    }

    private static ProviderWireError StreamRuntimeFailure()
    {
        return new ProviderWireError("provider_stream_runtime_failure", "internal_failure", false, false, "Provider stream failed while executing the request.");
    }

    private static ProviderWireError StreamProtocolFailure(string errorCode)
    {
        return new ProviderWireError(errorCode, "malformed_response", false, false, "Provider stream event did not satisfy the negotiated contract.");
    }

    private static bool IsCancellationError(ProviderWireError? error)
    {
        return error != null && (StringComparer.Ordinal.Equals(error.Category, "cancelled") || StringComparer.Ordinal.Equals(error.ErrorCode, "request.cancelled"));
    }

    private static RuntimeProviderOutcomeStatus ResolveProviderOutcomeStatus(bool success, ProviderWireError? error, bool providerStarted, bool responseComplete)
    {
        if (!responseComplete) return RuntimeProviderOutcomeStatus.Unknown;
        if (success) return RuntimeProviderOutcomeStatus.Applied;
        if (error == null) return RuntimeProviderOutcomeStatus.Failed;
        if (IsCancellationError(error) || error.Retryable || StringComparer.Ordinal.Equals(error.Category, "unavailable"))
        {
            return providerStarted ? RuntimeProviderOutcomeStatus.Unknown : RuntimeProviderOutcomeStatus.Retryable;
        }
        return RuntimeProviderOutcomeStatus.Failed;
    }

    private void SetProviderOutcome(PipeEnvelope envelope, RuntimeProviderOutcomeStatus status)
    {
        if (providerOutcomeLedger == null || envelope.TaskScope == null) return;
        var key = BuildProviderIdempotencyKey(envelope);
        if (providerOutcomeLedger.SetOutcome(key, status, ToUtcDateTime(ResolveNowUnixMilliseconds()))) return;
        Console.Error.WriteLine("provider_outcome_ledger_write_failed:status=" + status + ":message=" + envelope.MessageId);
        Console.Error.Flush();
    }

    private static int Utf8ByteCount(string? value)
    {
        return value == null ? 0 : new UTF8Encoding(false, true).GetByteCount(value);
    }

    private static bool IsRagBusinessMessage(string messageType)
    {
        return StringComparer.Ordinal.Equals(messageType, ProtocolConstants.MessageTypeRagIngest)
            || StringComparer.Ordinal.Equals(messageType, ProtocolConstants.MessageTypeRagSearch);
    }

    private static void MaybeCrashAfterCommitBeforeResponse(PipeEnvelope envelope, StorageBusinessResult result)
    {
        if (!result.Durable
            || !StringComparer.Ordinal.Equals(Environment.GetEnvironmentVariable(RuntimeTestModeEnvironmentVariable), "p3c")
            || !StringComparer.Ordinal.Equals(Environment.GetEnvironmentVariable(CrashAfterCommitMessageEnvironmentVariable), envelope.MessageId)) return;

        Process.GetCurrentProcess().Kill(entireProcessTree: false);
    }

    private static RequestContext CreateStorageRequestContext(ConnectionContext connection, PipeEnvelope envelope)
    {
        var scope = envelope.TaskScope ?? throw new InvalidOperationException("task_scope_missing");
        var session = new SessionRef(envelope.CampaignGuid, envelope.TimelineId, connection.SessionId);
        var context = new RequestContext(
            new ExtensionId(envelope.OwnerId),
            session,
            envelope.SessionGeneration,
            envelope.CorrelationId,
            DateTimeOffset.FromUnixTimeMilliseconds(envelope.DeadlineUnixMilliseconds));
        var settlement = StringComparer.Ordinal.Equals(scope.SettlementRequirement, "required")
            ? SettlementRequirement.Required
            : SettlementRequirement.NotApplicable;
        var aiTaskScope = new AiTaskScope(
            scope.TaskId,
            scope.MessageId,
            scope.OwnerId,
            session,
            envelope.SessionGeneration,
            envelope.CorrelationId,
            string.IsNullOrWhiteSpace(envelope.CausationId) ? envelope.MessageId : envelope.CausationId,
            scope.RouteId,
            scope.ProviderId,
            scope.ProfileId,
            scope.IdempotencyKey,
            scope.RequestPayloadHash,
            new SchemaRef(scope.OutputSchemaId, scope.OutputSchemaMajor, scope.OutputSchemaMinor),
            settlement);
        return context.WithAiTaskScope(aiTaskScope);
    }

    private async Task HandleShutdownAsync(ConnectionContext connection, PipeEnvelope envelope, CancellationToken cancellationToken)
    {
        var response = BuildResponse(connection, envelope, "shutdown_ack", "marcus-awake.shutdown.v1", "{\"status\":\"draining\"}", null, "accepted");
        await WriteResponseAsync(connection, response, cancellationToken).ConfigureAwait(false);
        BeginDrain("client_shutdown");
    }

    private PipeEnvelope BuildErrorResponse(ConnectionContext connection, PipeEnvelope request, string errorCode, string outcomeKind, FrameworkError? typedError = null)
    {
        return BuildResponse(connection, request, ProtocolConstants.MessageTypeError, ProtocolConstants.GenericErrorSchemaV1, BuildGenericErrorPayload(errorCode, typedError), request.TaskScope != null && request.TaskScope.IsComplete ? request.TaskScope : null, outcomeKind, errorCode);
    }

    private PipeEnvelope BuildProviderErrorResponse(ConnectionContext connection, PipeEnvelope request, string errorCode)
    {
        var taskScope = request.TaskScope;
        var payload = "{\"schema\":\"" + ProtocolConstants.ProviderErrorSchemaV1 + "\",\"error_code\":\"" + errorCode + "\",\"category\":\"" + ProviderErrorCategory(errorCode) + "\",\"retryable\":false,\"fallback_allowed\":false,\"safe_message\":\"" + ProviderErrorMessage(errorCode) + "\",\"provider_id\":" + JsonSerializer.Serialize(taskScope?.ProviderId ?? string.Empty) + ",\"profile_id\":" + JsonSerializer.Serialize(taskScope?.ProfileId ?? string.Empty) + ",\"route_id\":" + JsonSerializer.Serialize(taskScope?.RouteId ?? string.Empty) + "}";
        return BuildResponse(connection, request, ProtocolConstants.MessageTypeError, ProtocolConstants.ProviderErrorSchemaV1, payload, taskScope, "rejected", errorCode);
    }

    private PipeEnvelope BuildProviderErrorResponse(ConnectionContext connection, PipeEnvelope request, ProviderWireError error)
    {
        return BuildResponse(connection, request, ProtocolConstants.MessageTypeError, ProtocolConstants.ProviderErrorSchemaV1, ProviderWireAdapter.BuildErrorPayload(null, error), request.TaskScope, error.Retryable ? ProtocolConstants.OutcomeRetryableReject : ProtocolConstants.OutcomeRejected, error.ErrorCode);
    }

    private PipeEnvelope BuildProviderErrorResponse(ConnectionContext connection, PipeEnvelope request, ProviderWireRequest wireRequest, ProviderWireError error)
    {
        return BuildResponse(connection, request, ProtocolConstants.MessageTypeError, ProtocolConstants.ProviderErrorSchemaV1, ProviderWireAdapter.BuildErrorPayload(wireRequest, error), request.TaskScope, error.Retryable ? ProtocolConstants.OutcomeRetryableReject : ProtocolConstants.OutcomeRejected, error.ErrorCode);
    }

    private static string BuildGenericErrorPayload(string errorCode, FrameworkError? typedError = null)
    {
        // Storage/RAG failures carry a real FrameworkErrorCategory set by the backend. Without this
        // the generic category resolver flattens every one of them to "invalid_request" and the
        // caller can no longer branch on conflict / resource_exhausted / denied.
        var category = typedError != null ? GenericCategoryName(typedError.Category) : GenericErrorCategory(errorCode);
        var retryable = typedError != null ? typedError.Retryable : IsRetryableGenericError(errorCode);
        return "{\"schema\":\"" + ProtocolConstants.GenericErrorSchemaV1 + "\",\"error_code\":\"" + (errorCode ?? "protocol_rejected") + "\",\"category\":\"" + category + "\",\"retryable\":" + (retryable ? "true" : "false") + ",\"fallback_allowed\":" + (IsFallbackAllowedGenericError(errorCode) ? "true" : "false") + ",\"safe_message\":\"" + GenericErrorMessage(errorCode) + "\"}";
    }

    private static string GenericCategoryName(FrameworkErrorCategory category)
    {
        switch (category)
        {
            case FrameworkErrorCategory.InvalidRequest: return "invalid_request";
            case FrameworkErrorCategory.Incompatible: return "incompatible";
            case FrameworkErrorCategory.Unsupported: return "unsupported";
            case FrameworkErrorCategory.Unavailable: return "unavailable";
            case FrameworkErrorCategory.Denied: return "denied";
            case FrameworkErrorCategory.NotFound: return "not_found";
            case FrameworkErrorCategory.Conflict: return "conflict";
            case FrameworkErrorCategory.Expired: return "expired";
            case FrameworkErrorCategory.RateLimited: return "rate_limited";
            case FrameworkErrorCategory.ProviderFailure: return "provider_failure";
            case FrameworkErrorCategory.Timeout: return "timeout";
            case FrameworkErrorCategory.Cancelled: return "cancelled";
            case FrameworkErrorCategory.ResourceExhausted: return "resource_exhausted";
            case FrameworkErrorCategory.RecoveryRequired: return "recovery_required";
            default: return "internal_failure";
        }
    }

    private static string GenericErrorCategory(string? errorCode)
    {
        if (StringComparer.Ordinal.Equals(errorCode, "schema_unsupported")) return "unsupported";
        if (StringComparer.Ordinal.Equals(errorCode, "capability_not_granted")) return "denied";
        if (StringComparer.Ordinal.Equals(errorCode, "deadline_suppressed_replay")) return "expired";
        if (StringComparer.Ordinal.Equals(errorCode, "ipc_backpressure") || StringComparer.Ordinal.Equals(errorCode, "sequence_gap") || StringComparer.Ordinal.Equals(errorCode, "sequence_out_of_order")) return "unavailable";
        return "invalid_request";
    }

    private static bool IsRetryableGenericError(string? errorCode)
    {
        return StringComparer.Ordinal.Equals(errorCode, "ipc_backpressure") || StringComparer.Ordinal.Equals(errorCode, "sequence_gap") || StringComparer.Ordinal.Equals(errorCode, "sequence_out_of_order");
    }

    private static bool IsFallbackAllowedGenericError(string? errorCode)
    {
        return false;
    }

    private static string GenericErrorMessage(string? errorCode)
    {
        switch (errorCode)
        {
            case "capability_not_granted": return "The requested runtime capability was not granted.";
            case "ipc_backpressure": return "The runtime service is temporarily busy.";
            case "schema_unsupported": return "The requested message schema is not supported.";
            case "deadline_suppressed_replay": return "The prior response expired before delivery.";
            case "task_scope_missing": return "The task scope is missing or incomplete.";
            case "task_scope_invalid": return "The task scope does not match the request.";
            case "output_schema_mismatch": return "The requested output schema is not supported for this message.";
            case "provider_causation_missing": return "The provider request causation identifier is missing.";
            case "sequence_gap": return "The request sequence has a gap and must be retried.";
            case "sequence_out_of_order": return "The request sequence is out of order and must be retried.";
            default: return "The runtime request was rejected.";
        }
    }

    private static string ProviderErrorCategory(string errorCode)
    {
        return StringComparer.Ordinal.Equals(errorCode, "provider_schema_mismatch") ? "invalid_request" : "unavailable";
    }

    private static string ProviderErrorMessage(string errorCode)
    {
        switch (errorCode)
        {
            case "settlement_unavailable": return "Provider result cannot satisfy the requested settlement.";
            case "provider_schema_mismatch": return "Provider request schema does not match the negotiated contract.";
            default: return "Provider handler is not enabled in this runtime build.";
        }
    }

    private PipeEnvelope BuildResponse(ConnectionContext connection, PipeEnvelope request, string messageType, string payloadSchema, string payloadJson, TaskScopeEnvelope? taskScope, string outcomeKind, string errorCode = "", string ackStatus = "", bool nonDurable = true, long eventIndex = 0)
    {
        var canonicalPayload = CanonicalizePayload(payloadJson);
        var payloadBytes = Encoding.UTF8.GetBytes(canonicalPayload);
        var response = new PipeEnvelope
        {
            MessageType = messageType,
            MessageId = request.MessageId,
            RequestId = string.IsNullOrWhiteSpace(request.RequestId) ? request.MessageId : request.RequestId,
            CorrelationId = request.CorrelationId,
            CausationId = request.MessageId,
            CampaignGuid = request.CampaignGuid,
            TimelineId = request.TimelineId,
            SessionId = connection.SessionId,
            SessionGeneration = request.SessionGeneration,
            OwnerId = request.OwnerId,
            DeadlineUnixMilliseconds = request.DeadlineUnixMilliseconds,
            InstanceEpoch = descriptor.InstanceEpoch,
            ConnectionEpoch = connection.ConnectionEpoch,
            DirectionNonce = connection.ServiceDirectionNonce,
            Sequence = 0,
            PayloadSchema = payloadSchema,
            PayloadJson = canonicalPayload,
            PayloadLength = payloadBytes.Length,
            PayloadSha256 = TransportSecurity.Sha256Hex(payloadBytes),
            Checksum = TransportSecurity.Sha256Hex(payloadBytes),
            AckStatus = string.IsNullOrEmpty(errorCode) ? (string.IsNullOrWhiteSpace(ackStatus) ? (nonDurable ? ProtocolConstants.AckAccepted : ProtocolConstants.AckDurablyRecorded) : ackStatus) : string.Empty,
            OutcomeKind = outcomeKind,
            NonDurable = nonDurable,
            EventIndex = eventIndex,
            ErrorCode = errorCode,
            TaskScope = taskScope == null ? null : CloneTaskScope(taskScope)
        };
        response.FenceProof = TransportSecurity.ComputeFrameFenceProof(frameKey, response);
        return response;
    }

    private PipeEnvelope BuildResponseFromTemplate(ConnectionContext connection, PipeEnvelope request, ResponseTemplate template, string outcomeKind)
    {
        var taskScope = template.TaskScope == null ? null : CloneTaskScopeForResponse(template.TaskScope, request.MessageId);
        return BuildResponse(connection, request, template.MessageType, template.PayloadSchema, template.PayloadJson, taskScope, outcomeKind, template.ErrorCode, template.AckStatus, template.NonDurable, template.EventIndex);
    }

    private async Task ReplayStreamAsync(ConnectionContext connection, PipeEnvelope request, IReadOnlyList<ResponseTemplate> templates, CancellationToken cancellationToken)
    {
        for (var index = 0; index < templates.Count; index++)
        {
            var outcomeKind = index == templates.Count - 1 ? ProtocolConstants.OutcomeTerminalReplay : ProtocolConstants.OutcomeAccepted;
            var response = BuildResponseFromTemplate(connection, request, templates[index], outcomeKind);
            var writeResult = await WriteResponseAsync(connection, response, cancellationToken).ConfigureAwait(false);
            if (!writeResult.FrameComplete) return;
        }
    }

    private static TaskScopeEnvelope CloneTaskScopeForResponse(TaskScopeEnvelope scope, string messageId)
    {
        var clone = CloneTaskScope(scope);
        clone.MessageId = messageId;
        return clone;
    }

    private async Task<FrameWriteResult> WriteResponseAsync(ConnectionContext connection, PipeEnvelope response, CancellationToken cancellationToken)
    {
        await connection.WriterGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            response.Sequence = connection.NextResponseSequence();
            response.FenceProof = TransportSecurity.ComputeFrameFenceProof(frameKey, response);
            var json = ProtocolCodec.SerializeEnvelope(response);
            var result = await PipeFrameIO.WriteFrameWithResultAsync(
                connection.Pipe,
                json,
                cancellationToken,
                () => BeforeFirstByteWrite(response),
                connection.RecordWrite).ConfigureAwait(false);
            if (!result.FrameComplete) connection.RequestClose();
            if (result.ResponseSuppressed && StringComparer.Ordinal.Equals(result.FailureKind, "gate_rejected")) MarkSuppressedResponse(connection, response);
            return result;
        }
        finally
        {
            connection.WriterGate.Release();
        }
    }

    private bool BeforeFirstByteWrite(PipeEnvelope response)
    {
        var suppressedMessageId = Environment.GetEnvironmentVariable(RuntimeTestSuppressBeforeWriteMessageEnvironmentVariable);
        var testMode = Environment.GetEnvironmentVariable(RuntimeTestModeEnvironmentVariable);
        if ((StringComparer.Ordinal.Equals(testMode, "p3d-a0") || StringComparer.Ordinal.Equals(testMode, "p3d-a1")) && StringComparer.Ordinal.Equals(suppressedMessageId, response.MessageId))
        {
            lock (sync)
            {
                if (!testSuppressionConsumed)
                {
                    testSuppressionConsumed = true;
                    return false;
                }
            }
        }

        return ResolveNowUnixMilliseconds() < response.DeadlineUnixMilliseconds;
    }

    private async Task<DrainDispatchOutcome> DrainDispatchesAsync(List<Task> dispatches)
    {
        if (dispatches.Count == 0) return DrainDispatchOutcome.Completed;
        var all = Task.WhenAll(dispatches);
        var timeout = Task.Delay(DrainTimeoutMilliseconds);
        Task completed = await Task.WhenAny(all, timeout).ConfigureAwait(false);
        if (ReferenceEquals(completed, timeout))
        {
            Console.Error.WriteLine("dispatch_drain_timeout:pending=" + dispatches.Count);
            Console.Error.Flush();
            _ = all.ContinueWith(
                late =>
                {
                    if (late.IsFaulted)
                    {
                        Console.Error.WriteLine("dispatch_drain_late_fault:" + (late.Exception?.GetBaseException()?.GetType().Name ?? "unknown"));
                    }
                    else
                    {
                        Console.Error.WriteLine("dispatch_drain_late_completion");
                    }
                    Console.Error.Flush();
                },
                TaskScheduler.Default);
            return DrainDispatchOutcome.TimedOut;
        }
        try
        {
            await all.ConfigureAwait(false);
            return DrainDispatchOutcome.Completed;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("dispatch_drain_fault:" + exception.GetType().Name);
            Console.Error.Flush();
            return DrainDispatchOutcome.Faulted;
        }
    }

    private enum DrainDispatchOutcome
    {
        Completed,
        TimedOut,
        Faulted
    }

    private void BeginDrain(string reason)
    {
        List<CancellationTokenSource> cancellations;
        lock (sync)
        {
            if (state == LifecycleState.Stopped) return;
            state = LifecycleState.Draining;
            cancellations = activeTasks.Values.Select(activeTask => activeTask.Cancellation).ToList();
        }

        foreach (var cancellation in cancellations)
        {
            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        lifecycle.Cancel();
    }

    private bool IsDraining()
    {
        lock (sync) return state == LifecycleState.Draining || state == LifecycleState.Stopped;
    }

    private bool RegisterActiveTask(string taskId, CancellationTokenSource cancellation)
    {
        lock (sync)
        {
            if (activeTasks.Count >= MaximumConcurrentFrames || activeTasks.ContainsKey(taskId)) return false;
            activeTasks.Add(taskId, new ActiveTask(cancellation));
            return true;
        }
    }

    private bool RegisterActiveTask(string taskId, CancellationToken parentToken, out CancellationTokenSource cancellation)
    {
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(parentToken, lifecycle.Token);
        if (RegisterActiveTask(taskId, cancellation)) return true;
        cancellation.Dispose();
        cancellation = null!;
        return false;
    }

    private ProviderTaskRegistration RegisterProviderActiveTask(PipeEnvelope envelope, LedgerEntry ledgerEntry, CancellationToken parentToken, out CancellationTokenSource cancellation, out ActiveTask activeTask)
    {
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(parentToken, lifecycle.Token);
        activeTask = null!;
        lock (sync)
        {
            if (activeTasks.Count >= MaximumConcurrentFrames || activeTasks.ContainsKey(envelope.TaskScope!.TaskId))
            {
                cancellation.Dispose();
                cancellation = null!;
                return ProviderTaskRegistration.Busy;
            }

            if (ConsumePendingCancellationUnsafe(envelope))
            {
                ledgerEntry.CancelRequested = true;
                ledgerEntry.LifecycleState = ProviderTaskLifecycleName(ProviderTaskLifecycle.Cancelled);
                cancellation.Dispose();
                cancellation = null!;
                return ProviderTaskRegistration.CancelledBeforeAdmission;
            }

            activeTask = new ActiveTask(cancellation, envelope, ledgerEntry);
            activeTasks.Add(envelope.TaskScope!.TaskId, activeTask);
            ledgerEntry.LifecycleState = ProviderTaskLifecycleName(ProviderTaskLifecycle.Admitted);
            return ProviderTaskRegistration.Registered;
        }
    }

    private void CompleteActiveTask(string taskId)
    {
        CancellationTokenSource? cancellation = null;
        lock (sync)
        {
            if (activeTasks.TryGetValue(taskId, out var activeTask)) cancellation = activeTask.Cancellation;
            activeTasks.Remove(taskId);
        }
        cancellation?.Dispose();
    }

    private void RemoveActiveTask(string taskId)
    {
        lock (sync) activeTasks.Remove(taskId);
    }

    private void RemoveTaskLedger(PipeEnvelope envelope)
    {
        lock (sync)
        {
            if (!messageLedger.TryGetValue(envelope.MessageId, out var entry)) return;
            RemoveLedgerUnsafe(entry);
        }
    }

    private bool TryGetActiveTask(string taskId, out ActiveTask activeTask)
    {
        lock (sync) return activeTasks.TryGetValue(taskId, out activeTask!);
    }

    private LedgerEntry GetOrCreateTaskLedger(PipeEnvelope envelope, out bool created)
    {
        lock (sync)
        {
            PruneRequestLedgersUnsafe(ToUtcDateTime(ResolveNowUnixMilliseconds()));
            return GetOrCreateTaskLedgerUnsafe(envelope, out created);
        }
    }

    private LedgerEntry GetOrCreateTaskLedgerUnsafe(PipeEnvelope envelope)
    {
        bool created;
        return GetOrCreateTaskLedgerUnsafe(envelope, out created);
    }

    private LedgerEntry GetOrCreateTaskLedgerUnsafe(PipeEnvelope envelope, out bool created)
    {
        if (messageLedger.TryGetValue(envelope.MessageId, out var messageEntry))
        {
            created = false;
            messageEntry.LastTouchedUtc = ToUtcDateTime(ResolveNowUnixMilliseconds());
            return messageEntry;
        }
        if (envelope.TaskScope != null && taskLedger.TryGetValue(envelope.TaskScope.TaskId, out var taskEntry))
        {
            created = false;
            taskEntry.LastTouchedUtc = ToUtcDateTime(ResolveNowUnixMilliseconds());
            return taskEntry;
        }
        LedgerEntry newEntry = new LedgerEntry(
            envelope.MessageId,
            envelope.TaskScope == null ? string.Empty : envelope.TaskScope.TaskId,
            envelope.TaskScope == null ? null : CloneTaskScope(envelope.TaskScope),
            envelope.OwnerId,
            envelope.CampaignGuid,
            envelope.TimelineId,
            envelope.SessionId,
            envelope.SessionGeneration);
        messageLedger[envelope.MessageId] = newEntry;
        if (!string.IsNullOrEmpty(newEntry.TaskId)) taskLedger[newEntry.TaskId] = newEntry;
        newEntry.LastTouchedUtc = ToUtcDateTime(ResolveNowUnixMilliseconds());
        created = true;
        return newEntry;
    }

    private ProviderIdempotencyDisposition ReserveProviderIdempotency(PipeEnvelope envelope, ProviderWireRequest request, out LedgerEntry ledgerEntry, out ResponseTemplate? replayTemplate)
    {
        if (envelope.TaskScope == null) throw new InvalidOperationException("task_scope_missing");

        lock (sync)
        {
            PruneRequestLedgersUnsafe(ToUtcDateTime(ResolveNowUnixMilliseconds()));
            EvictExpiredStreamLedgersUnsafe(ToUtcDateTime(ResolveNowUnixMilliseconds()));
            var key = BuildProviderIdempotencyKey(envelope);
            if (providerIdempotency.TryGetValue(key, out var existing))
            {
                ledgerEntry = existing.Ledger;
                replayTemplate = existing.Ledger.Template;
                if (!StringComparer.Ordinal.Equals(existing.RequestPayloadHash, envelope.TaskScope.RequestPayloadHash)) return ProviderIdempotencyDisposition.Conflict;
                TouchCommittedStreamLedgerUnsafe(existing.Ledger);
                return existing.Ledger.Template == null && existing.Ledger.StreamTemplates == null
                    ? ProviderIdempotencyDisposition.InProgress
                    : ProviderIdempotencyDisposition.Replay;
            }

            if (providerOutcomeLedger == null)
            {
                ledgerEntry = GetOrCreateTaskLedgerUnsafe(envelope);
                replayTemplate = null;
                return ProviderIdempotencyDisposition.DurableUnavailable;
            }

            var durableReservation = providerOutcomeLedger.Reserve(
                key,
                envelope.TaskScope.RequestPayloadHash,
                ToUtcDateTime(ResolveNowUnixMilliseconds()));
            if (durableReservation == RuntimeProviderLedgerReservation.Conflict)
            {
                ledgerEntry = GetOrCreateTaskLedgerUnsafe(envelope);
                replayTemplate = null;
                return ProviderIdempotencyDisposition.Conflict;
            }
            if (durableReservation == RuntimeProviderLedgerReservation.Applied)
            {
                ledgerEntry = GetOrCreateTaskLedgerUnsafe(envelope);
                replayTemplate = null;
                return ProviderIdempotencyDisposition.RestartApplied;
            }
            if (durableReservation == RuntimeProviderLedgerReservation.Unknown)
            {
                ledgerEntry = GetOrCreateTaskLedgerUnsafe(envelope);
                replayTemplate = null;
                return ProviderIdempotencyDisposition.RestartUnknown;
            }
            if (durableReservation == RuntimeProviderLedgerReservation.Failed)
            {
                ledgerEntry = GetOrCreateTaskLedgerUnsafe(envelope);
                replayTemplate = null;
                return ProviderIdempotencyDisposition.RestartFailed;
            }
            if (durableReservation == RuntimeProviderLedgerReservation.InProgress)
            {
                ledgerEntry = GetOrCreateTaskLedgerUnsafe(envelope);
                replayTemplate = null;
                return ProviderIdempotencyDisposition.InProgress;
            }
            if (durableReservation == RuntimeProviderLedgerReservation.Unavailable)
            {
                ledgerEntry = GetOrCreateTaskLedgerUnsafe(envelope);
                replayTemplate = null;
                return ProviderIdempotencyDisposition.DurableUnavailable;
            }

            var messageExists = messageLedger.TryGetValue(envelope.MessageId, out var messageEntry);
            var taskExists = !messageExists && !string.IsNullOrEmpty(envelope.TaskScope.TaskId) && taskLedger.TryGetValue(envelope.TaskScope.TaskId, out _);
            ledgerEntry = GetOrCreateTaskLedgerUnsafe(envelope);
            ledgerEntry.IsProviderTask = true;
            replayTemplate = null;
            if (request.Operation == ProviderWireOperation.Stream && !TryReserveStreamReplayUnsafe(ledgerEntry, request.ResourceBudget))
            {
                if (!messageExists && !taskExists) RemoveLedgerUnsafe(ledgerEntry);
                return ProviderIdempotencyDisposition.Capacity;
            }
            providerIdempotency.Add(key, new ProviderIdempotencyEntry(envelope.TaskScope.RequestPayloadHash, ledgerEntry));
            return ProviderIdempotencyDisposition.New;
        }
    }

    private void PruneRequestLedgersUnsafe(DateTimeOffset now)
    {
        var entries = new HashSet<LedgerEntry>(messageLedger.Values);
        foreach (var entry in entries)
        {
            if (now - entry.LastTouchedUtc < RequestLedgerTtl) continue;
            if (!string.IsNullOrEmpty(entry.TaskId) && activeTasks.TryGetValue(entry.TaskId, out var activeTask))
            {
                try { activeTask.Cancellation.Cancel(); } catch (ObjectDisposedException) { }
                entry.CancelRequested = true;
                entry.LifecycleState = "expired_unknown";
            }
            RemoveLedgerUnsafe(entry);
        }

        while (messageLedger.Count > MaximumRequestLedgerEntries)
        {
            var candidate = messageLedger.Values
                .Where(entry => string.IsNullOrEmpty(entry.TaskId) || !activeTasks.ContainsKey(entry.TaskId))
                .OrderBy(entry => entry.LastTouchedUtc)
                .FirstOrDefault();
            if (candidate == null) break;
            RemoveLedgerUnsafe(candidate);
        }

        while (providerIdempotency.Count > MaximumProviderLedgerEntries)
        {
            var candidate = providerIdempotency.Values
                .Select(value => value.Ledger)
                .Where(entry => string.IsNullOrEmpty(entry.TaskId) || !activeTasks.ContainsKey(entry.TaskId))
                .OrderBy(entry => entry.LastTouchedUtc)
                .FirstOrDefault();
            if (candidate == null) break;
            RemoveLedgerUnsafe(candidate);
        }
    }

    private bool TryReserveStreamReplayUnsafe(LedgerEntry ledgerEntry, ProviderStreamBudget? budget)
    {
        if (budget == null) return false;
        var reservationBytes = Math.Min(
            MaximumStreamReplayBytes,
            Math.Max(4096L, (long)budget.OutputBytes + ((long)budget.FrameCount * 2048L)));
        var now = ToUtcDateTime(ResolveNowUnixMilliseconds());
        EvictExpiredStreamLedgersUnsafe(now);
        while (committedStreamLedgers.Count >= MaximumCommittedStreamLedgers
            || reservedStreamReplayBytes + committedStreamReplayBytes + reservationBytes > MaximumStreamReplayBytes)
        {
            if (!EvictOldestCommittedStreamLedgerUnsafe()) return false;
        }

        ledgerEntry.StreamReplayState = StreamLedgerState.Working;
        ledgerEntry.StreamReplayReservationBytes = reservationBytes;
        reservedStreamReplayBytes += reservationBytes;
        return true;
    }

    private void EvictExpiredStreamLedgersUnsafe(DateTimeOffset now)
    {
        var node = committedStreamLedgers.First;
        while (node != null)
        {
            var next = node.Next;
            if (node.Value.StreamReplayCommittedAtUtc.HasValue && node.Value.StreamReplayCommittedAtUtc.Value + StreamReplayTtl <= now) RemoveCommittedStreamLedgerUnsafe(node.Value);
            node = next;
        }
    }

    private bool EvictOldestCommittedStreamLedgerUnsafe()
    {
        var node = committedStreamLedgers.First;
        if (node == null) return false;
        RemoveCommittedStreamLedgerUnsafe(node.Value);
        return true;
    }

    private void RemoveCommittedStreamLedgerUnsafe(LedgerEntry entry)
    {
        if (entry.StreamReplayNode != null)
        {
            committedStreamLedgers.Remove(entry.StreamReplayNode);
            entry.StreamReplayNode = null;
        }

        committedStreamReplayBytes = Math.Max(0, committedStreamReplayBytes - entry.StreamReplayBytes);
        entry.StreamReplayBytes = 0;
        entry.StreamReplayCommittedAtUtc = null;
        entry.StreamReplayState = StreamLedgerState.Evicted;
        RemoveLedgerUnsafe(entry, releaseReplay: false);
    }

    private void TouchCommittedStreamLedgerUnsafe(LedgerEntry entry)
    {
        if (entry.StreamReplayState != StreamLedgerState.Committed || entry.StreamReplayNode == null) return;
        committedStreamLedgers.Remove(entry.StreamReplayNode);
        entry.StreamReplayNode = committedStreamLedgers.AddLast(entry);
    }

    private void RemoveLedgerUnsafe(LedgerEntry entry, bool releaseReplay = true)
    {
        messageLedger.Remove(entry.MessageId);
        if (!string.IsNullOrEmpty(entry.TaskId) && taskLedger.TryGetValue(entry.TaskId, out var taskEntry) && ReferenceEquals(taskEntry, entry)) taskLedger.Remove(entry.TaskId);
        var providerKeys = providerIdempotency.Where(pair => ReferenceEquals(pair.Value.Ledger, entry)).Select(pair => pair.Key).ToList();
        for (var index = 0; index < providerKeys.Count; index++) providerIdempotency.Remove(providerKeys[index]);
        if (releaseReplay) ReleaseStreamReplayUnsafe(entry);
    }

    private void ReleaseStreamReplayUnsafe(LedgerEntry entry)
    {
        if (entry.StreamReplayReservationBytes > 0)
        {
            reservedStreamReplayBytes = Math.Max(0, reservedStreamReplayBytes - entry.StreamReplayReservationBytes);
            entry.StreamReplayReservationBytes = 0;
        }

        if (entry.StreamReplayNode != null)
        {
            committedStreamLedgers.Remove(entry.StreamReplayNode);
            entry.StreamReplayNode = null;
            committedStreamReplayBytes = Math.Max(0, committedStreamReplayBytes - entry.StreamReplayBytes);
            entry.StreamReplayBytes = 0;
        }

        entry.StreamReplayCommittedAtUtc = null;
        if (entry.StreamReplayState != StreamLedgerState.None) entry.StreamReplayState = StreamLedgerState.Aborted;
    }

    private static DateTimeOffset ToUtcDateTime(long unixMilliseconds)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);
    }

    private static string BuildProviderIdempotencyKey(PipeEnvelope envelope)
    {
        var scope = envelope.TaskScope ?? throw new InvalidOperationException("task_scope_missing");
        return string.Join("\u001f", new[]
        {
            envelope.OwnerId,
            envelope.CampaignGuid,
            envelope.TimelineId,
            envelope.SessionId,
            scope.RouteId,
            scope.ProviderId,
            scope.ProfileId,
            scope.IdempotencyKey
        });
    }

    private void RemoveProviderIdempotency(LedgerEntry ledgerEntry)
    {
        lock (sync)
        {
            var keys = providerIdempotency.Where(pair => ReferenceEquals(pair.Value.Ledger, ledgerEntry)).Select(pair => pair.Key).ToList();
            for (var index = 0; index < keys.Count; index++) providerIdempotency.Remove(keys[index]);
        }
    }

    private void StoreMessageTemplate(string messageId, ResponseTemplate template)
    {
        lock (sync)
        {
            PruneRequestLedgersUnsafe(ToUtcDateTime(ResolveNowUnixMilliseconds()));
            if (!messageLedger.ContainsKey(messageId)) messageLedger[messageId] = new LedgerEntry(messageId, template.TaskScope?.TaskId ?? string.Empty, template.TaskScope == null ? null : CloneTaskScope(template.TaskScope));
            messageLedger[messageId].Template = template;
            messageLedger[messageId].LastTouchedUtc = ToUtcDateTime(ResolveNowUnixMilliseconds());
        }
    }

    private bool TryGetSuppressedMarker(string messageId, long sequence, long connectionEpoch)
    {
        lock (sync)
        {
            if (!suppressedMarkers.TryGetValue(messageId, out var marker)) return false;
            return marker.Sequence == sequence && marker.ConnectionEpoch != connectionEpoch;
        }
    }

    private void MarkSuppressedResponse(ConnectionContext connection, PipeEnvelope response)
    {
        if (!connection.TryGetAcceptedRequest(response.MessageId, out var acceptedSequence)) return;

        lock (sync)
        {
            PruneRequestLedgersUnsafe(ToUtcDateTime(ResolveNowUnixMilliseconds()));
            if (messageLedger.TryGetValue(response.MessageId, out var existing))
            {
                if (!string.IsNullOrEmpty(existing.TaskId)) taskLedger.Remove(existing.TaskId);
            }

            messageLedger[response.MessageId] = new LedgerEntry(response.MessageId, string.Empty, null)
            {
                Suppressed = true
            };
            suppressedMarkers[response.MessageId] = new SuppressedMarker(response.MessageId, acceptedSequence, connection.ConnectionEpoch);
        }
    }

    private void SetTaskTemplate(LedgerEntry entry, ResponseTemplate template)
    {
        lock (sync)
        {
            if (entry.Template == null) entry.Template = template;
            entry.LastTouchedUtc = ToUtcDateTime(ResolveNowUnixMilliseconds());
        }
    }

    private void SetTaskLifecycle(LedgerEntry? entry, ProviderTaskLifecycle lifecycleState)
    {
        if (entry == null) return;
        lock (sync)
        {
            entry.LifecycleState = ProviderTaskLifecycleName(lifecycleState);
            entry.LastTouchedUtc = ToUtcDateTime(ResolveNowUnixMilliseconds());
            if (lifecycleState == ProviderTaskLifecycle.Cancelled || lifecycleState == ProviderTaskLifecycle.CancelledRequested) entry.CancelRequested = true;
        }
    }

    private bool TryGetMessageTemplate(string messageId, out ResponseTemplate template)
    {
        lock (sync)
        {
            PruneRequestLedgersUnsafe(ToUtcDateTime(ResolveNowUnixMilliseconds()));
            if (messageLedger.TryGetValue(messageId, out var entry) && entry.Template != null)
            {
                template = entry.Template;
                return true;
            }
        }

        template = null!;
        return false;
    }

    private bool TryGetTaskTemplate(string taskId, out ResponseTemplate template)
    {
        lock (sync)
        {
            PruneRequestLedgersUnsafe(ToUtcDateTime(ResolveNowUnixMilliseconds()));
            if (taskLedger.TryGetValue(taskId, out var entry) && entry.Template != null)
            {
                template = entry.Template;
                return true;
            }
        }

        template = null!;
        return false;
    }

    private static ResponseTemplate ToTemplate(PipeEnvelope response)
    {
        return new ResponseTemplate(response.MessageType, response.PayloadSchema, response.PayloadJson, response.ErrorCode, response.AckStatus, response.NonDurable, response.EventIndex, response.TaskScope == null ? null : CloneTaskScope(response.TaskScope));
    }

    private static long EstimateResponseTemplateBytes(ResponseTemplate template)
    {
        if (template == null) return 0;
        var bytes = 256L
            + Utf8ByteCount(template.MessageType)
            + Utf8ByteCount(template.PayloadSchema)
            + Utf8ByteCount(template.PayloadJson)
            + Utf8ByteCount(template.ErrorCode)
            + Utf8ByteCount(template.AckStatus);
        if (template.TaskScope != null)
        {
            bytes += Utf8ByteCount(template.TaskScope.TaskId)
                + Utf8ByteCount(template.TaskScope.MessageId)
                + Utf8ByteCount(template.TaskScope.OwnerId)
                + Utf8ByteCount(template.TaskScope.RouteId)
                + Utf8ByteCount(template.TaskScope.ProviderId)
                + Utf8ByteCount(template.TaskScope.ProfileId)
                + Utf8ByteCount(template.TaskScope.IdempotencyKey)
                + Utf8ByteCount(template.TaskScope.RequestPayloadHash)
                + Utf8ByteCount(template.TaskScope.OutputSchemaId)
                + Utf8ByteCount(template.TaskScope.SettlementRequirement);
        }

        return bytes;
    }

    private static TaskScopeEnvelope CloneTaskScope(TaskScopeEnvelope scope)
    {
        return new TaskScopeEnvelope
        {
            TaskId = scope.TaskId,
            MessageId = scope.MessageId,
            OwnerId = scope.OwnerId,
            RouteId = scope.RouteId,
            ProviderId = scope.ProviderId,
            ProfileId = scope.ProfileId,
            IdempotencyKey = scope.IdempotencyKey,
            RequestPayloadHash = scope.RequestPayloadHash,
            OutputSchemaId = scope.OutputSchemaId,
            OutputSchemaMajor = scope.OutputSchemaMajor,
            OutputSchemaMinor = scope.OutputSchemaMinor,
            SettlementRequirement = scope.SettlementRequirement
        };
    }

    private static bool ContainsSha256ChecksumAlgorithm(string rawJson)
    {
        try
        {
            using var document = JsonDocument.Parse(rawJson, new JsonDocumentOptions { MaxDepth = ProtocolConstants.MaxJsonDepth, AllowTrailingCommas = false });
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("checksum_algorithm", out var property)) return false;
            return property.ValueKind == JsonValueKind.String && StringComparer.Ordinal.Equals(property.GetString(), ProtocolConstants.ChecksumAlgorithm);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string CanonicalizePayload(string payloadJson)
    {
        var skeleton = new PipeEnvelope
        {
            MessageType = "health",
            MessageId = "canonicalize",
            CorrelationId = "canonicalize",
            SessionId = string.Empty,
            DeadlineUnixMilliseconds = 1,
            InstanceEpoch = 1,
            ConnectionEpoch = 1,
            DirectionNonce = "canonicalize",
            Sequence = 1,
            PayloadSchema = "canonicalize",
            PayloadJson = payloadJson,
            PayloadLength = 0,
            PayloadSha256 = string.Empty,
            Checksum = string.Empty
        };
        var serialized = ProtocolCodec.SerializeEnvelope(skeleton);
        if (!ProtocolCodec.TryParseEnvelope(serialized, out var normalized, out var error)) throw new InvalidDataException(error);
        return normalized.PayloadJson;
    }

    private static bool TryReadEchoPayload(string payloadJson, out string value, out int delayMilliseconds)
    {
        value = string.Empty;
        delayMilliseconds = 0;
        using var document = JsonDocument.Parse(payloadJson, new JsonDocumentOptions { MaxDepth = ProtocolConstants.MaxJsonDepth, AllowTrailingCommas = false });
        var root = document.RootElement;
        if (!root.TryGetProperty("value", out var valueProperty) || valueProperty.ValueKind != JsonValueKind.String) return false;
        value = valueProperty.GetString() ?? string.Empty;
        if (root.TryGetProperty("delay_ms", out var delayProperty) && (!delayProperty.TryGetInt32(out delayMilliseconds) || delayMilliseconds < 0 || delayMilliseconds > 2000)) return false;
        return true;
    }

    private static string ResolveServiceArtifactHash()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath)) executablePath = Process.GetCurrentProcess().MainModule?.FileName;
        var hash = TransportSecurity.Sha256FileHex(executablePath ?? string.Empty);
        if (string.IsNullOrWhiteSpace(hash)) throw new ServiceStartupException("service_artifact_unavailable", 5);
        return hash;
    }

    private static string ResolveStorageDatabasePath()
    {
        var root = Environment.GetEnvironmentVariable("MARCUS_AWAKE_RUNTIME_DATA_ROOT");
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AWAKE",
                "RuntimeData");
        }

        return Path.Combine(Path.GetFullPath(root), "awake-runtime.db");
    }

    private static string ResolveProviderOutcomeLedgerPath()
    {
        return Path.ChangeExtension(ResolveStorageDatabasePath(), ".provider-ledger.json");
    }

    private static string ResolveEmbeddingModelName()
    {
        var name = Environment.GetEnvironmentVariable(EmbeddingModelNameEnvironmentVariable);
        return string.IsNullOrWhiteSpace(name) ? DefaultEmbeddingModelName : name.Trim();
    }

    /// <summary>
    /// Model directory, resolved the same way this service already resolves its data root: an
    /// environment variable first, otherwise a directory next to the executable. The default points
    /// at `models/&lt;name&gt;/` beside the service binary, which is exactly where the packager drops
    /// the model, so the game side needs no configuration of its own
    /// (docs/CONFIG-20260916 §12).
    /// </summary>
    private static string ResolveEmbeddingModelDirectory()
    {
        var root = Environment.GetEnvironmentVariable(ModelRootEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(root)) root = Path.Combine(AppContext.BaseDirectory, "models");
        return Path.GetFullPath(Path.Combine(root, ResolveEmbeddingModelName()));
    }

    /// <summary>
    /// Builds the semantic-retrieval encoder, or returns null to leave the backend keyword-only.
    ///
    /// A model that is missing or corrupt must cost the player the semantic half of retrieval and
    /// nothing else, so nothing in here is allowed to be fatal. It must not be silent either: the
    /// trace goes to stderr, which <see cref="RuntimeServiceLog"/> mirrors into a durable file, so
    /// "retrieval quietly got worse" stays diagnosable (docs/CONFIG-20260916 §13).
    ///
    /// The session is warmed here on purpose. Building it reads the 90 MB of weights, and paying that
    /// on the first dialogue line would be a visible stall; at startup it is ~214 ms nobody sees.
    /// </summary>
    private static IRagEmbedder? CreateEmbedder()
    {
        try
        {
            var directory = ResolveEmbeddingModelDirectory();
            var embedder = new OnnxSentenceEmbedder(new OnnxEmbedderOptions
            {
                ModelDirectory = directory,
                VocabularyPath = Path.Combine(directory, "vocab.txt"),
                ModelId = ResolveEmbeddingModelName() + "|" + EmbeddingPassageRevision,
            });
            var dimension = embedder.Dimension;
            Console.Error.WriteLine("rag_embedder_ready model=" + embedder.ModelId + " dimension=" + dimension + " directory=" + directory);
            Console.Error.Flush();
            return embedder;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("rag_embedder_unavailable:" + exception.GetType().Name + ":" + exception.Message);
            Console.Error.Flush();
            return null;
        }
    }

    private void InitializeProviderRuntime()
    {
        var providerAssemblyPath = ProviderRegistry.ResolveProviderAssemblyPath();
        providerCredentialStore = ProviderRegistry.TryCreateCredentialStore(ResolveProviderCredentialDirectoryPath(), providerAssemblyPath);
        providerInvoker = new HttpMessageInvoker(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        }, true);
        providerRegistry = new ProviderRegistry(providerCredentialStore, providerInvoker, providerAssemblyPath);
    }

    private static string ResolveProviderCredentialDirectoryPath()
    {
        var root = Environment.GetEnvironmentVariable("MARCUS_AWAKE_RUNTIME_CREDENTIAL_ROOT");
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AWAKE",
                "RuntimeData",
                "credentials");
        }

        return Path.GetFullPath(root);
    }

    private void DisposeProviderRuntime()
    {
        if (providerRegistry != null)
        {
            try
            {
                providerRegistry.Dispose();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("provider_registry_dispose_failed:" + exception.GetType().Name);
                Console.Error.Flush();
            }
            providerRegistry = null;
        }

        if (providerInvoker != null)
        {
            try
            {
                providerInvoker.Dispose();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("provider_invoker_dispose_failed:" + exception.GetType().Name);
                Console.Error.Flush();
            }
            providerInvoker = null;
        }

        if (providerCredentialStore is IDisposable disposableStore)
        {
            try
            {
                disposableStore.Dispose();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("provider_credential_store_dispose_failed:" + exception.GetType().Name);
                Console.Error.Flush();
            }
        }
        providerCredentialStore = null;
    }

    private void ClearSecrets()
    {
        if (serviceKey.Length > 0) CryptographicOperations.ZeroMemory(serviceKey);
        if (frameKey.Length > 0) CryptographicOperations.ZeroMemory(frameKey);
        serviceKey = Array.Empty<byte>();
        frameKey = Array.Empty<byte>();
    }

    private enum AdmissionResponsePolicy
    {
        None,
        GenericError,
        CloseNoResponse,
        SuppressedReplayError
    }

    private sealed class AdmissionDecision
    {
        private AdmissionDecision(string phase, string errorCode, bool consumeSequence, bool allowBusinessParse, AdmissionResponsePolicy responsePolicy, FrameSlotLease? slotLease, SequenceDecision? sequenceResult)
        {
            Phase = phase;
            ErrorCode = errorCode ?? string.Empty;
            ConsumeSequence = consumeSequence;
            AllowBusinessParse = allowBusinessParse;
            ResponsePolicy = responsePolicy;
            SlotLease = slotLease;
            SequenceResult = sequenceResult;
        }

        internal string Phase { get; }
        internal string ErrorCode { get; }
        internal bool ConsumeSequence { get; }
        internal bool AllowBusinessParse { get; }
        internal AdmissionResponsePolicy ResponsePolicy { get; }
        internal FrameSlotLease? SlotLease { get; }
        internal SequenceDecision? SequenceResult { get; }
        internal bool CloseConnection => ResponsePolicy == AdmissionResponsePolicy.CloseNoResponse;

        internal static AdmissionDecision Close(string phase, string errorCode)
        {
            return new AdmissionDecision(phase, errorCode, false, false, AdmissionResponsePolicy.CloseNoResponse, null, null);
        }

        internal static AdmissionDecision Generic(string phase, string errorCode, bool consumeSequence, FrameSlotLease? slotLease, SequenceDecision? sequenceResult = null)
        {
            return new AdmissionDecision(phase, errorCode, consumeSequence, false, AdmissionResponsePolicy.GenericError, slotLease, sequenceResult);
        }

        internal static AdmissionDecision Allow(FrameSlotLease slotLease)
        {
            return new AdmissionDecision("B0", string.Empty, true, true, AdmissionResponsePolicy.None, slotLease, null);
        }

        internal static AdmissionDecision SuppressedReplay(FrameSlotLease slotLease)
        {
            return new AdmissionDecision("B0", "deadline_suppressed_replay", true, false, AdmissionResponsePolicy.SuppressedReplayError, slotLease, null);
        }
    }

    private enum LifecycleState
    {
        Accepting,
        Draining,
        Stopped
    }

    private enum CancelResolution
    {
        Requested,
        AlreadyRequested,
        AlreadyCompleted,
        NotFound
    }

    private enum ProviderTaskRegistration
    {
        Registered,
        Busy,
        CancelledBeforeAdmission
    }

    private enum ProviderTaskLifecycle
    {
        Admitted,
        ProviderInFlight,
        ResultCaptured,
        CancelledRequested,
        Cancelled,
        ResponsePublished,
        Failed
    }

    private static string ProviderTaskLifecycleName(ProviderTaskLifecycle lifecycleState)
    {
        return lifecycleState switch
        {
            ProviderTaskLifecycle.Admitted => "admitted",
            ProviderTaskLifecycle.ProviderInFlight => "provider_in_flight",
            ProviderTaskLifecycle.ResultCaptured => "result_captured",
            ProviderTaskLifecycle.CancelledRequested => "cancelled_requested",
            ProviderTaskLifecycle.Cancelled => "cancelled",
            ProviderTaskLifecycle.ResponsePublished => "response_published",
            _ => "failed"
        };
    }

    private sealed class ActiveTask
    {
        private readonly object sync = new object();
        private ProviderTaskLifecycle lifecycle = ProviderTaskLifecycle.Admitted;

        internal ActiveTask(CancellationTokenSource cancellation)
        {
            Cancellation = cancellation;
        }

        internal ActiveTask(CancellationTokenSource cancellation, PipeEnvelope envelope, LedgerEntry ledger)
        {
            Cancellation = cancellation;
            Ledger = ledger;
            OwnerId = envelope.OwnerId;
            CampaignGuid = envelope.CampaignGuid;
            TimelineId = envelope.TimelineId;
            SessionId = envelope.SessionId;
            SessionGeneration = envelope.SessionGeneration;
            TaskScope = CloneTaskScope(envelope.TaskScope!);
        }

        internal CancellationTokenSource Cancellation { get; }
        internal LedgerEntry? Ledger { get; }
        internal string OwnerId { get; } = string.Empty;
        internal string CampaignGuid { get; } = string.Empty;
        internal string TimelineId { get; } = string.Empty;
        internal string SessionId { get; } = string.Empty;
        internal long SessionGeneration { get; }
        internal TaskScopeEnvelope? TaskScope { get; }

        internal CancelResolution MarkCancellationRequested()
        {
            lock (sync)
            {
                if (lifecycle == ProviderTaskLifecycle.ResponsePublished || lifecycle == ProviderTaskLifecycle.Failed || lifecycle == ProviderTaskLifecycle.Cancelled) return CancelResolution.AlreadyCompleted;
                if (lifecycle == ProviderTaskLifecycle.CancelledRequested) return CancelResolution.AlreadyRequested;
                lifecycle = ProviderTaskLifecycle.CancelledRequested;
                return CancelResolution.Requested;
            }
        }

        internal void ApplyCancellation()
        {
            try
            {
                Cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        internal bool TryBeginProvider()
        {
            lock (sync)
            {
                if (lifecycle == ProviderTaskLifecycle.CancelledRequested || lifecycle == ProviderTaskLifecycle.Cancelled || Cancellation.IsCancellationRequested)
                {
                    lifecycle = ProviderTaskLifecycle.Cancelled;
                    return false;
                }

                if (lifecycle != ProviderTaskLifecycle.Admitted) return false;
                lifecycle = ProviderTaskLifecycle.ProviderInFlight;
                return true;
            }
        }

        internal bool TryCaptureResult()
        {
            lock (sync)
            {
                if (lifecycle == ProviderTaskLifecycle.CancelledRequested || lifecycle == ProviderTaskLifecycle.Cancelled || Cancellation.IsCancellationRequested)
                {
                    lifecycle = ProviderTaskLifecycle.Cancelled;
                    return false;
                }

                if (lifecycle != ProviderTaskLifecycle.ProviderInFlight) return false;
                lifecycle = ProviderTaskLifecycle.ResultCaptured;
                return true;
            }
        }

        internal bool TryClaimResponse(bool success)
        {
            lock (sync)
            {
                if (lifecycle == ProviderTaskLifecycle.CancelledRequested || lifecycle == ProviderTaskLifecycle.Cancelled || Cancellation.IsCancellationRequested)
                {
                    lifecycle = ProviderTaskLifecycle.Cancelled;
                    return false;
                }

                if (lifecycle != ProviderTaskLifecycle.ResultCaptured && lifecycle != ProviderTaskLifecycle.ProviderInFlight) return false;
                lifecycle = success ? ProviderTaskLifecycle.ResponsePublished : ProviderTaskLifecycle.Failed;
                return true;
            }
        }

        internal bool TryClaimStreamTerminal(bool success)
        {
            lock (sync)
            {
                if (lifecycle == ProviderTaskLifecycle.CancelledRequested || lifecycle == ProviderTaskLifecycle.Cancelled || Cancellation.IsCancellationRequested)
                {
                    lifecycle = ProviderTaskLifecycle.Cancelled;
                    return false;
                }

                if (lifecycle != ProviderTaskLifecycle.ResultCaptured && lifecycle != ProviderTaskLifecycle.ProviderInFlight) return false;
                lifecycle = success ? ProviderTaskLifecycle.ResponsePublished : ProviderTaskLifecycle.Failed;
                return true;
            }
        }

        internal void MarkCancelled()
        {
            lock (sync) lifecycle = ProviderTaskLifecycle.Cancelled;
        }

        internal void MarkFailed()
        {
            lock (sync) lifecycle = lifecycle == ProviderTaskLifecycle.CancelledRequested || lifecycle == ProviderTaskLifecycle.Cancelled ? ProviderTaskLifecycle.Cancelled : ProviderTaskLifecycle.Failed;
        }

        internal bool MatchesCancellationTarget(PipeEnvelope envelope, string targetSessionId)
        {
            return TaskScope != null
                && StringComparer.Ordinal.Equals(SessionId, targetSessionId)
                && StringComparer.Ordinal.Equals(OwnerId, envelope.OwnerId)
                && StringComparer.Ordinal.Equals(CampaignGuid, envelope.CampaignGuid)
                && StringComparer.Ordinal.Equals(TimelineId, envelope.TimelineId)
                && SessionGeneration == envelope.SessionGeneration
                && SameCancellationScope(TaskScope, envelope.TaskScope);
        }
    }

    private enum ProviderIdempotencyDisposition
    {
        New,
        InProgress,
        Replay,
        Conflict,
        Capacity,
        RestartApplied,
        RestartUnknown,
        RestartFailed,
        DurableUnavailable
    }

    private enum StreamLedgerState
    {
        None,
        Working,
        TerminalClaimed,
        Committed,
        Aborted,
        Evicted
    }

    private sealed class ProviderIdempotencyEntry
    {
        internal ProviderIdempotencyEntry(string requestPayloadHash, LedgerEntry ledger)
        {
            RequestPayloadHash = requestPayloadHash;
            Ledger = ledger;
        }

        internal string RequestPayloadHash { get; }
        internal LedgerEntry Ledger { get; }
    }

    private enum StreamWriteResult
    {
        NotWritten,
        Written
    }

    private enum StreamDispatchResult
    {
        Continue,
        TerminalWritten,
        Aborted
    }

    private sealed class StreamWorkingLedger
    {
        private readonly object sync = new object();
        private readonly int maximumFrames;
        private readonly int maximumOutputBytes;
        private readonly int maximumTokens;
        private readonly int maximumTextDeltas;
        private readonly List<ResponseTemplate> templates = new List<ResponseTemplate>();
        private int eventCount;
        private int outputBytes;
        private int knownTokens;
        private int textDeltas;
        private bool terminalClaimed;
        private bool aborted;
        private long serializedTemplateBytes;

        internal StreamWorkingLedger(ProviderStreamBudget budget)
        {
            maximumFrames = budget.FrameCount;
            maximumOutputBytes = budget.OutputBytes;
            maximumTokens = budget.Tokens;
            maximumTextDeltas = budget.TextDeltas;
        }

        internal int MaximumTokens => maximumTokens;

        internal long SerializedTemplateBytes
        {
            get
            {
                lock (sync) return serializedTemplateBytes;
            }
        }

        internal long NextStreamSequence
        {
            get
            {
                lock (sync) return eventCount + 1L;
            }
        }

        internal bool HasTerminalSlot()
        {
            lock (sync) return !terminalClaimed && !aborted && eventCount + 1 <= maximumFrames;
        }

        internal bool CanWriteNonTerminal(int additionalOutputBytes, int additionalTextDeltas, int projectedKnownTokens)
        {
            lock (sync)
            {
                return !terminalClaimed
                    && !aborted
                    && eventCount + 2 <= maximumFrames
                    && outputBytes + additionalOutputBytes <= maximumOutputBytes
                    && textDeltas + additionalTextDeltas <= maximumTextDeltas
                    && projectedKnownTokens <= maximumTokens;
            }
        }

        internal bool CanWriteTerminal(int additionalOutputBytes, int projectedKnownTokens)
        {
            lock (sync)
            {
                return !terminalClaimed
                    && !aborted
                    && eventCount + 1 <= maximumFrames
                    && outputBytes + additionalOutputBytes <= maximumOutputBytes
                    && projectedKnownTokens <= maximumTokens;
            }
        }

        internal bool TryClaimTerminal()
        {
            lock (sync)
            {
                if (terminalClaimed || aborted || eventCount + 1 > maximumFrames) return false;
                terminalClaimed = true;
                return true;
            }
        }

        internal void RecordFrame(ResponseTemplate template, int additionalOutputBytes, bool textDelta, int projectedKnownTokens)
        {
            lock (sync)
            {
                templates.Add(template);
                eventCount++;
                serializedTemplateBytes += EstimateResponseTemplateBytes(template);
                outputBytes += additionalOutputBytes;
                if (textDelta) textDeltas++;
                knownTokens = projectedKnownTokens;
            }
        }

        internal RuntimeProviderOutcomeStatus TerminalOutcomeStatus { get; private set; } = RuntimeProviderOutcomeStatus.Unknown;

        internal void MarkTerminalWritten(RuntimeProviderOutcomeStatus status)
        {
            lock (sync) TerminalOutcomeStatus = status;
        }

        internal IReadOnlyList<ResponseTemplate> SnapshotTemplates()
        {
            lock (sync) return templates.ToList().AsReadOnly();
        }

        internal void Abort()
        {
            lock (sync)
            {
                aborted = true;
                templates.Clear();
                serializedTemplateBytes = 0;
            }
        }
    }

    private sealed class StreamUsageTracker
    {
        private long settledTokens;
        private int? attemptInputTokens;
        private int? attemptOutputTokens;

        internal int TotalKnownTokens => ToInt(settledTokens + (attemptInputTokens ?? 0L) + (attemptOutputTokens ?? 0L));

        internal bool TryObserve(ProviderStreamEventProjection providerEvent, int maximumTokens, out int totalKnownTokens, out ProviderWireError? error)
        {
            error = null;
            if ((providerEvent.InputTokens.HasValue && providerEvent.InputTokens.Value < 0) || (providerEvent.OutputTokens.HasValue && providerEvent.OutputTokens.Value < 0))
            {
                totalKnownTokens = TotalKnownTokens;
                error = StreamProtocolFailure("usage_invalid");
                return false;
            }

            if (providerEvent.InputTokens.HasValue && attemptInputTokens.HasValue && providerEvent.InputTokens.Value < attemptInputTokens.Value)
            {
                totalKnownTokens = TotalKnownTokens;
                error = StreamProtocolFailure("usage_non_monotonic");
                return false;
            }

            if (providerEvent.OutputTokens.HasValue && attemptOutputTokens.HasValue && providerEvent.OutputTokens.Value < attemptOutputTokens.Value)
            {
                totalKnownTokens = TotalKnownTokens;
                error = StreamProtocolFailure("usage_non_monotonic");
                return false;
            }

            if (providerEvent.InputTokens.HasValue) attemptInputTokens = providerEvent.InputTokens.Value;
            if (providerEvent.OutputTokens.HasValue) attemptOutputTokens = providerEvent.OutputTokens.Value;
            var total = settledTokens + (attemptInputTokens ?? 0L) + (attemptOutputTokens ?? 0L);
            totalKnownTokens = ToInt(total);
            if (total > maximumTokens)
            {
                error = ProviderWireAdapter.ResourceExhausted();
                return false;
            }

            return true;
        }

        internal bool TrySettleAttempt(int maximumTokens, out int totalKnownTokens, out ProviderWireError? error)
        {
            error = null;
            settledTokens += (attemptInputTokens ?? 0L) + (attemptOutputTokens ?? 0L);
            attemptInputTokens = null;
            attemptOutputTokens = null;
            totalKnownTokens = ToInt(settledTokens);
            if (settledTokens > maximumTokens)
            {
                error = ProviderWireAdapter.ResourceExhausted();
                return false;
            }

            return true;
        }

        private static int ToInt(long value)
        {
            return value >= int.MaxValue ? int.MaxValue : value <= 0 ? 0 : (int)value;
        }
    }

    private sealed class LedgerEntry
    {
        internal LedgerEntry(string messageId, string taskId, TaskScopeEnvelope? taskScope, string ownerId = "", string campaignGuid = "", string timelineId = "", string sessionId = "", long sessionGeneration = 0)
        {
            MessageId = messageId;
            TaskId = taskId;
            TaskScope = taskScope;
            OwnerId = ownerId;
            CampaignGuid = campaignGuid;
            TimelineId = timelineId;
            SessionId = sessionId;
            SessionGeneration = sessionGeneration;
        }

        internal string MessageId { get; }
        internal string TaskId { get; }
        internal TaskScopeEnvelope? TaskScope { get; }
        internal string OwnerId { get; }
        internal string CampaignGuid { get; }
        internal string TimelineId { get; }
        internal string SessionId { get; }
        internal long SessionGeneration { get; }
        internal DateTimeOffset LastTouchedUtc { get; set; } = DateTimeOffset.UtcNow;
        internal bool IsProviderTask { get; set; }
        internal string LifecycleState { get; set; } = "admitted";
        internal bool CancelRequested { get; set; }
        internal ResponseTemplate? Template { get; set; }
        internal List<ResponseTemplate>? StreamTemplates { get; set; }
        internal bool Suppressed { get; set; }
        internal StreamLedgerState StreamReplayState { get; set; } = StreamLedgerState.None;
        internal long StreamReplayReservationBytes { get; set; }
        internal long StreamReplayBytes { get; set; }
        internal DateTimeOffset? StreamReplayCommittedAtUtc { get; set; }
        internal LinkedListNode<LedgerEntry>? StreamReplayNode { get; set; }
    }

    private sealed class PendingCancellation
    {
        internal PendingCancellation(string ownerId, string campaignGuid, string timelineId, string sessionId, long sessionGeneration, TaskScopeEnvelope taskScope, DateTimeOffset expiresAt)
        {
            OwnerId = ownerId;
            CampaignGuid = campaignGuid;
            TimelineId = timelineId;
            SessionId = sessionId;
            SessionGeneration = sessionGeneration;
            TaskScope = taskScope;
            ExpiresAt = expiresAt;
        }

        internal string OwnerId { get; }
        internal string CampaignGuid { get; }
        internal string TimelineId { get; }
        internal string SessionId { get; }
        internal long SessionGeneration { get; }
        internal TaskScopeEnvelope TaskScope { get; }
        internal DateTimeOffset ExpiresAt { get; }
    }

    private sealed class SuppressedMarker
    {
        internal SuppressedMarker(string messageId, long sequence, long connectionEpoch)
        {
            MessageId = messageId;
            Sequence = sequence;
            ConnectionEpoch = connectionEpoch;
        }

        internal string MessageId { get; }
        internal long Sequence { get; }
        internal long ConnectionEpoch { get; }
    }

    private sealed class ResponseTemplate
    {
        internal ResponseTemplate(string messageType, string payloadSchema, string payloadJson, string errorCode, string ackStatus, bool nonDurable, long eventIndex, TaskScopeEnvelope? taskScope)
        {
            MessageType = messageType;
            PayloadSchema = payloadSchema;
            PayloadJson = payloadJson;
            ErrorCode = errorCode;
            AckStatus = ackStatus;
            NonDurable = nonDurable;
            EventIndex = eventIndex;
            TaskScope = taskScope;
        }

        internal string MessageType { get; }
        internal string PayloadSchema { get; }
        internal string PayloadJson { get; }
        internal string ErrorCode { get; }
        internal string AckStatus { get; }
        internal bool NonDurable { get; }
        internal long EventIndex { get; }
        internal TaskScopeEnvelope? TaskScope { get; }
    }
}

internal sealed class FrameSlotLease
{
    private readonly ConnectionContext owner;
    private int releaseCount;

    internal FrameSlotLease(ConnectionContext owner)
    {
        this.owner = owner;
    }

    internal int ReleaseCount => Volatile.Read(ref releaseCount);

    internal bool Release()
    {
        if (Interlocked.Increment(ref releaseCount) != 1) return false;
        owner.ExitFrame();
        return true;
    }
}

internal sealed class ConnectionContext
{
    private readonly object sync = new object();
    private readonly CancellationTokenSource closeSignal = new CancellationTokenSource();
    private readonly HashSet<string> capabilities;
    private readonly Dictionary<string, long> acceptedRequests = new Dictionary<string, long>(StringComparer.Ordinal);
    private string campaignGuid = string.Empty;
    private string timelineId = string.Empty;
    private long sessionGeneration;
    private int activeFrames;
    private long responseSequence;
    private bool closeRequested;
    private FrameWriteResult? lastWriteResult;

    internal ConnectionContext(NamedPipeServerStream pipe, long connectionEpoch, string sessionId, string clientDirectionNonce, string serviceDirectionNonce, string[] capabilities, byte[] frameKey)
    {
        Pipe = pipe;
        ConnectionEpoch = connectionEpoch;
        SessionId = sessionId;
        ClientDirectionNonce = clientDirectionNonce;
        ServiceDirectionNonce = serviceDirectionNonce;
        this.capabilities = new HashSet<string>(capabilities, StringComparer.Ordinal);
        ReceiveWindow = new SequenceWindow(sessionId, connectionEpoch, clientDirectionNonce);
        WriterGate = new SemaphoreSlim(1, 1);
    }

    internal NamedPipeServerStream Pipe { get; }
    internal long ConnectionEpoch { get; }
    internal string SessionId { get; }
    internal string ClientDirectionNonce { get; }
    internal string ServiceDirectionNonce { get; }
    internal SequenceWindow ReceiveWindow { get; }
    internal SemaphoreSlim WriterGate { get; }
    internal CancellationToken CloseToken => closeSignal.Token;
    internal IReadOnlyCollection<string> Capabilities => capabilities;
    internal bool IsControlOnly => capabilities.Count == 2
        && capabilities.Contains("health")
        && capabilities.Contains("cancel");
    internal bool CloseRequested
    {
        get
        {
            lock (sync) return closeRequested;
        }
    }
    internal FrameWriteResult? LastWriteResult
    {
        get
        {
            lock (sync) return lastWriteResult;
        }
    }

    internal FrameSlotLease? TryEnterFrame()
    {
        lock (sync)
        {
            if (activeFrames >= 8) return null;
            activeFrames++;
            return new FrameSlotLease(this);
        }
    }

    internal void ExitFrame()
    {
        lock (sync)
        {
            if (activeFrames > 0) activeFrames--;
        }
    }

    internal void RequestClose()
    {
        var shouldCancel = false;
        lock (sync)
        {
            if (!closeRequested)
            {
                closeRequested = true;
                shouldCancel = true;
            }
        }

        if (shouldCancel) closeSignal.Cancel();
    }

    internal void DisposeCloseSignal()
    {
        closeSignal.Dispose();
    }

    internal void RecordWrite(FrameWriteResult result)
    {
        lock (sync) lastWriteResult = result;
    }

    internal long NextResponseSequence() => Interlocked.Increment(ref responseSequence);

    internal void RecordAcceptedRequest(string messageId, long sequence)
    {
        lock (sync)
        {
            acceptedRequests[messageId] = sequence;
        }
    }

    internal bool TryGetAcceptedRequest(string messageId, out long sequence)
    {
        lock (sync)
        {
            return acceptedRequests.TryGetValue(messageId, out sequence);
        }
    }

    internal bool BindSessionFence(string nextCampaignGuid, string nextTimelineId, long nextSessionGeneration)
    {
        lock (sync)
        {
            if (string.IsNullOrEmpty(campaignGuid))
            {
                campaignGuid = nextCampaignGuid;
                timelineId = nextTimelineId;
                sessionGeneration = nextSessionGeneration;
                return true;
            }

            return StringComparer.Ordinal.Equals(campaignGuid, nextCampaignGuid) && StringComparer.Ordinal.Equals(timelineId, nextTimelineId) && sessionGeneration == nextSessionGeneration;
        }
    }
}

[SupportedOSPlatform("windows")]
internal static class WindowsIdentityHelper
{
    internal static string GetCurrentSid()
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (identity.User == null) throw new ServiceStartupException("ipc_identity_unavailable", 5);
        return identity.User.Value;
    }
}

[SupportedOSPlatform("windows")]
internal static class ProcessIdentity
{
    private const uint SnapshotProcess = 0x00000002;
    private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

    internal static ParentIdentity ReadParent(int processId)
    {
        if (!OperatingSystem.IsWindows()) return new ParentIdentity(0);
        var snapshot = NativeMethods.CreateToolhelp32Snapshot(SnapshotProcess, 0);
        if (snapshot == IntPtr.Zero || snapshot == InvalidHandleValue) return new ParentIdentity(0);

        try
        {
            var entry = new NativeMethods.ProcessEntry32 { Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.ProcessEntry32>() };
            if (!NativeMethods.Process32First(snapshot, ref entry)) return new ParentIdentity(0);
            do
            {
                if (entry.ProcessId == (uint)processId) return new ParentIdentity((int)entry.ParentProcessId);
            }
            while (NativeMethods.Process32Next(snapshot, ref entry));
            return new ParentIdentity(0);
        }
        finally
        {
            NativeMethods.CloseHandle(snapshot);
        }
    }

    internal static bool MatchesStartTime(int processId, long expectedStartUnixMilliseconds)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return ReadStartUnixMilliseconds(process) == expectedStartUnixMilliseconds;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    internal static long ReadStartUnixMilliseconds(Process process)
    {
        return new DateTimeOffset(process.StartTime.ToUniversalTime()).ToUnixTimeMilliseconds();
    }

    internal readonly struct ParentIdentity
    {
        internal ParentIdentity(int processId)
        {
            ProcessId = processId;
        }

        internal int ProcessId { get; }
        internal bool IsValid => ProcessId > 0;
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        internal static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        internal static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        internal struct ProcessEntry32
        {
            internal uint Size;
            internal uint Usage;
            internal uint ProcessId;
            internal IntPtr DefaultHeapId;
            internal uint ModuleId;
            internal uint Threads;
            internal uint ParentProcessId;
            internal int BasePriority;
            internal uint Flags;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 260)]
            internal string ExecutableFile;
        }
    }
}

internal static class JsonElementExtensions
{
    internal static bool TryGetPropertyValueAsString(this JsonElement property, out string value)
    {
        if (property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
