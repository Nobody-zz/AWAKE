using MarcusAwakeTransport;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeRuntimeService;

internal sealed class ProviderRegistry : IDisposable
{
    private readonly object sync = new object();
    private readonly object? credentialStore;
    private readonly HttpMessageInvoker invoker;
    private readonly string? explicitAssemblyPath;
    private readonly Dictionary<ProviderScopeKey, ProviderProfileEntry> profiles = new Dictionary<ProviderScopeKey, ProviderProfileEntry>();
    private Assembly? providerAssembly;
    private long mutationGeneration;
    private long registrationOrdinal;
    private bool disposed;

    internal ProviderRegistry(object? credentialStore, HttpMessageInvoker invoker, string? providerAssemblyPath = null)
    {
        this.credentialStore = credentialStore;
        this.invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
        explicitAssemblyPath = string.IsNullOrWhiteSpace(providerAssemblyPath) ? null : Path.GetFullPath(providerAssemblyPath);
    }

    internal ProviderWireResult Upsert(PipeEnvelope envelope, ProviderWireRequest request)
    {
        if (envelope == null) throw new ArgumentNullException(nameof(envelope));
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.Operation != ProviderWireOperation.ProfileUpsert) return ProviderWireResult.Failure(ProviderWireAdapter.SchemaError());

        var policyError = ValidateProfilePolicy(request);
        if (policyError != null) return ProviderWireResult.Failure(policyError);

        object adapter;
        try
        {
            adapter = CreateAdapter(request);
        }
        catch (ProviderRuntimeException exception)
        {
            return ProviderWireResult.Failure(exception.Error);
        }
        catch (TargetInvocationException exception)
        {
            return ProviderWireResult.Failure(ProfileConstructionError(exception.InnerException));
        }
        catch (Exception)
        {
            return ProviderWireResult.Failure(new ProviderWireError("provider.profile_invalid", "invalid_request", false, false, "Provider profile could not be constructed."));
        }

        var key = ProviderScopeKey.FromEnvelope(envelope);
        lock (sync)
        {
            ThrowIfDisposed();
            var entryGeneration = ++mutationGeneration;
            profiles[key] = new ProviderProfileEntry(key, request, adapter, entryGeneration, ++registrationOrdinal);
        }

        return ProviderWireResult.Success(ProviderWireAdapter.BuildProfileResult(request, "ready"));
    }

    internal async Task<ProviderWireResult> UpsertCredentialAsync(PipeEnvelope envelope, ProviderWireRequest request, CancellationToken cancellationToken)
    {
        if (envelope == null) throw new ArgumentNullException(nameof(envelope));
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.Operation != ProviderWireOperation.CredentialUpsert) return ProviderWireResult.Failure(ProviderWireAdapter.SchemaError());
        if (cancellationToken.IsCancellationRequested) return ProviderWireResult.Failure(ProviderWireAdapter.Cancelled());
        if (credentialStore == null) return ProviderWireResult.Failure(new ProviderWireError("credential.store_unavailable", "unavailable", true, false, "Provider credential storage is unavailable."));
        if (string.IsNullOrWhiteSpace(request.CredentialReference) || string.IsNullOrWhiteSpace(request.CredentialSecret)) return ProviderWireResult.Failure(new ProviderWireError("credential.invalid", "invalid_request", false, false, "Provider credential input is invalid."));

        object? credential = null;
        try
        {
            var assembly = GetProviderAssembly();
            if (assembly == null) return ProviderWireResult.Failure(ProviderWireAdapter.RuntimeUnavailable());
            var credentialType = RequireType(assembly, "MarcusAwakeProvider.ApiKeyCredential");
            var constructor = credentialType.GetConstructor(new[] { typeof(string), typeof(string) });
            if (constructor == null) return ProviderWireResult.Failure(ProviderWireAdapter.RuntimeUnavailable("Provider credential constructor is unavailable."));
            credential = constructor.Invoke(new object?[] { request.CredentialReference, request.CredentialSecret });

            var saveMethod = FindMethod(credentialStore.GetType(), "SaveAsync");
            var saveTask = saveMethod.Invoke(credentialStore, new object?[] { credential, cancellationToken });
            var saveResult = await AwaitTaskAsync(saveTask).ConfigureAwait(false);
            if (!TryReadProviderResult(saveResult, out _, out var providerError)) return ProviderWireResult.Failure(providerError!);
            return ProviderWireResult.Success(ProviderWireAdapter.BuildCredentialResult(request, "saved"));
        }
        catch (TargetInvocationException exception)
        {
            return ProviderWireResult.Failure(MapUnexpectedException(exception.InnerException, request.ProviderId));
        }
        catch (OperationCanceledException)
        {
            return ProviderWireResult.Failure(ProviderWireAdapter.Cancelled());
        }
        catch (Exception exception)
        {
            return ProviderWireResult.Failure(MapUnexpectedException(exception, request.ProviderId));
        }
        finally
        {
            if (credential is IDisposable disposable) disposable.Dispose();
        }
    }

    internal ProviderWireResult Remove(PipeEnvelope envelope, ProviderWireRequest request)
    {
        if (envelope == null) throw new ArgumentNullException(nameof(envelope));
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.Operation != ProviderWireOperation.ProfileRemove) return ProviderWireResult.Failure(ProviderWireAdapter.SchemaError());

        var key = ProviderScopeKey.FromEnvelope(envelope);
        lock (sync)
        {
            ThrowIfDisposed();
            mutationGeneration++;
            profiles.Remove(key);
        }

        return ProviderWireResult.Success(ProviderWireAdapter.BuildProfileResult(request, "removed"));
    }

    internal async Task<ProviderWireResult> ListModelsAsync(PipeEnvelope envelope, ProviderWireRequest request, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        if (envelope == null) throw new ArgumentNullException(nameof(envelope));
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.Operation != ProviderWireOperation.Models) return ProviderWireResult.Failure(ProviderWireAdapter.SchemaError());

        if (cancellationToken.IsCancellationRequested) return ProviderWireResult.Failure(ProviderWireAdapter.Cancelled());
        if (DateTimeOffset.UtcNow >= deadline) return ProviderWireResult.Failure(ProviderWireAdapter.DeadlineExpired());
        if (!TryGetProfile(envelope, out var entry)) return ProviderWireResult.Failure(ProviderWireAdapter.ProfileNotFound());

        return await ExecuteWithCredentialAsync(entry, async (adapter, credential) =>
        {
            var method = FindMethod(adapter.GetType(), "ListModelsAsync");
            var task = method.Invoke(adapter, new object?[] { credential, deadline, cancellationToken });
            var providerResult = await AwaitTaskAsync(task).ConfigureAwait(false);
            if (!TryReadProviderResult(providerResult, out var value, out var providerError)) return ProviderWireResult.Failure(providerError!);

            var models = ReadModels(value);
            if (models == null) return ProviderWireResult.Failure(new ProviderWireError("response.models_invalid", "malformed_response", false, false, "Provider returned an invalid model list."));
            return ProviderWireResult.Success(ProviderWireAdapter.BuildModelsResult(request, models));
        }, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<ProviderWireResult> CompleteAsync(PipeEnvelope envelope, ProviderWireRequest request, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        if (envelope == null) throw new ArgumentNullException(nameof(envelope));
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.Operation != ProviderWireOperation.Complete) return ProviderWireResult.Failure(ProviderWireAdapter.SchemaError());

        if (cancellationToken.IsCancellationRequested) return ProviderWireResult.Failure(ProviderWireAdapter.Cancelled());
        if (DateTimeOffset.UtcNow >= deadline) return ProviderWireResult.Failure(ProviderWireAdapter.DeadlineExpired());
        if (!TryGetProfile(envelope, out var entry)) return ProviderWireResult.Failure(ProviderWireAdapter.ProfileNotFound());

        object providerRequest;
        try
        {
            providerRequest = CreateChatRequest(request);
        }
        catch (TargetInvocationException exception)
        {
            return ProviderWireResult.Failure(ProfileConstructionError(exception.InnerException));
        }
        catch (Exception)
        {
            return ProviderWireResult.Failure(new ProviderWireError("request.invalid", "invalid_request", false, false, "Provider request could not be constructed."));
        }

        return await ExecuteWithCredentialAsync(entry, async (adapter, credential) =>
        {
            var method = FindMethod(adapter.GetType(), "CompleteAsync");
            var task = method.Invoke(adapter, new object?[] { providerRequest, credential, deadline, cancellationToken });
            var providerResult = await AwaitTaskAsync(task).ConfigureAwait(false);
            if (!TryReadProviderResult(providerResult, out var value, out var providerError)) return ProviderWireResult.Failure(providerError!);

            var completion = ReadCompletion(value);
            if (completion == null) return ProviderWireResult.Failure(new ProviderWireError("response.completion_invalid", "malformed_response", false, false, "Provider returned an invalid completion."));
            return ProviderWireResult.Success(ProviderWireAdapter.BuildCompletionResult(request, completion));
        }, cancellationToken).ConfigureAwait(false);
    }

    internal ProviderStreamCandidateSnapshot DescribeCandidates(PipeEnvelope envelope, ProviderWireRequest request)
    {
        if (envelope == null) throw new ArgumentNullException(nameof(envelope));
        if (request == null) throw new ArgumentNullException(nameof(request));
        lock (sync)
        {
            ThrowIfDisposed();
            var snapshot = profiles.Values
                .Where(entry => entry.Matches(envelope, request))
                .OrderBy(entry => entry.RegistrationOrdinal)
                .ThenBy(entry => entry.Request.ProviderId, StringComparer.Ordinal)
                .ThenBy(entry => entry.Request.DefaultModel, StringComparer.Ordinal)
                .ToList();
            if (snapshot.Count == 0) return ProviderStreamCandidateSnapshot.Failure(ProviderWireAdapter.RouteNoCandidate());

            var anchor = snapshot.FirstOrDefault(entry => StringComparer.Ordinal.Equals(entry.Request.ProviderId, request.ProviderId));
            if (anchor == null) return ProviderStreamCandidateSnapshot.Failure(ProviderWireAdapter.RouteAnchorUnavailable());

            snapshot.Remove(anchor);
            snapshot.Insert(0, anchor);
            return ProviderStreamCandidateSnapshot.Success(snapshot.AsReadOnly(), mutationGeneration);
        }
    }

    internal async Task<ProviderStreamBeginResult> BeginStreamAsync(PipeEnvelope envelope, ProviderWireRequest request, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var snapshot = DescribeCandidates(envelope, request);
            if (snapshot.Error != null) return ProviderStreamBeginResult.Failure(snapshot.Error);
            var result = await BeginStreamAsync(envelope, request, snapshot, deadline, cancellationToken).ConfigureAwait(false);
            if (result.Error == null || !StringComparer.Ordinal.Equals(result.Error.ErrorCode, "provider_profile_changed") || attempt == 1) return result;
        }

        return ProviderStreamBeginResult.Failure(ProviderWireAdapter.ProfileChanged());
    }

    internal async Task<ProviderStreamBeginResult> BeginStreamAsync(PipeEnvelope envelope, ProviderWireRequest request, ProviderStreamCandidateSnapshot candidateSnapshot, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        if (envelope == null) throw new ArgumentNullException(nameof(envelope));
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (candidateSnapshot == null) throw new ArgumentNullException(nameof(candidateSnapshot));
        if (request.Operation != ProviderWireOperation.Stream) return ProviderStreamBeginResult.Failure(ProviderWireAdapter.SchemaError());
        if (cancellationToken.IsCancellationRequested) return ProviderStreamBeginResult.Failure(ProviderWireAdapter.Cancelled());
        if (DateTimeOffset.UtcNow >= deadline) return ProviderStreamBeginResult.Failure(ProviderWireAdapter.DeadlineExpired());

        if (candidateSnapshot.Error != null) return ProviderStreamBeginResult.Failure(candidateSnapshot.Error);
        var snapshot = candidateSnapshot.Entries;
        if (snapshot == null) return ProviderStreamBeginResult.Failure(ProviderStreamErrors.BridgeInvalid());
            if (!SnapshotStillValid(snapshot, candidateSnapshot.MutationGeneration)) return ProviderStreamBeginResult.Failure(ProviderWireAdapter.ProfileChanged());

        var leases = new List<ProviderCredentialLease>(snapshot.Count);
        try
        {
            var providerRequest = CreateChatRequest(request);
            var candidates = new List<object>(snapshot.Count);
            for (var index = 0; index < snapshot.Count; index++)
            {
                if (cancellationToken.IsCancellationRequested) return await AbortStreamBeginAsync(leases, ProviderStreamBeginResult.Failure(ProviderWireAdapter.Cancelled())).ConfigureAwait(false);
                if (DateTimeOffset.UtcNow >= deadline) return await AbortStreamBeginAsync(leases, ProviderStreamBeginResult.Failure(ProviderWireAdapter.DeadlineExpired())).ConfigureAwait(false);
                var credentialResult = await LoadCredentialAsync(snapshot[index], cancellationToken).ConfigureAwait(false);
                if (credentialResult.Error != null) return await AbortStreamBeginAsync(leases, ProviderStreamBeginResult.Failure(credentialResult.Error)).ConfigureAwait(false);
                var lease = new ProviderCredentialLease(credentialResult.Credential);
                leases.Add(lease);
                candidates.Add(CreateRouteCandidate(snapshot[index].Adapter, lease.Credential));
            }

            if (!SnapshotStillValid(snapshot, candidateSnapshot.MutationGeneration))
            {
                return await AbortStreamBeginAsync(leases, ProviderStreamBeginResult.Failure(ProviderWireAdapter.ProfileChanged())).ConfigureAwait(false);
            }

            var router = CreateRouter(candidates);
            var stream = InvokeRouterStream(router, providerRequest, deadline, cancellationToken);
            var enumerator = CreateEnumerator(stream, cancellationToken, out var currentProperty, out var moveNextMethod, out var disposeMethod);
            if (!SnapshotStillValid(snapshot, candidateSnapshot.MutationGeneration))
            {
                var session = new ProviderStreamSession(enumerator, currentProperty, moveNextMethod, disposeMethod, leases, deadline, cancellationToken, () => ResolveCancellationError(cancellationToken, deadline));
                await session.DisposeSafeAsync().ConfigureAwait(false);
                return ProviderStreamBeginResult.Failure(ProviderWireAdapter.ProfileChanged());
            }

            return ProviderStreamBeginResult.Success(new ProviderStreamSession(enumerator, currentProperty, moveNextMethod, disposeMethod, leases, deadline, cancellationToken, () => ResolveCancellationError(cancellationToken, deadline)));
        }
        catch (ProviderRuntimeException exception)
        {
            return await AbortStreamBeginAsync(leases, ProviderStreamBeginResult.Failure(exception.Error)).ConfigureAwait(false);
        }
        catch (TargetInvocationException exception)
        {
            return await AbortStreamBeginAsync(leases, ProviderStreamBeginResult.Failure(MapUnexpectedException(exception.InnerException, request.ProviderId))).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return await AbortStreamBeginAsync(leases, ProviderStreamBeginResult.Failure(ResolveCancellationError(cancellationToken, deadline))).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return await AbortStreamBeginAsync(leases, ProviderStreamBeginResult.Failure(MapUnexpectedException(exception, request.ProviderId))).ConfigureAwait(false);
        }
    }

    private static async Task<ProviderStreamBeginResult> AbortStreamBeginAsync(IReadOnlyList<ProviderCredentialLease> leases, ProviderStreamBeginResult result)
    {
        await DisposeLeasesAsync(leases).ConfigureAwait(false);
        return result;
    }

    private static async Task DisposeLeasesAsync(IReadOnlyList<ProviderCredentialLease> leases)
    {
        for (var index = 0; index < leases.Count; index++) leases[index].DisposeSafe();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            profiles.Clear();
            disposed = true;
        }
    }

    internal static object? TryCreateCredentialStore(string directory, string? providerAssemblyPath = null)
    {
        var assembly = ProviderReflection.TryLoadAssembly(providerAssemblyPath);
        if (assembly == null) return null;

        try
        {
            var storeType = RequireType(assembly, "MarcusAwakeProvider.ProtectedFileCredentialStore");
            var modeType = RequireType(assembly, "MarcusAwakeProvider.CredentialProtectionMode");
            var mode = Enum.Parse(modeType, "PlatformPreferred", ignoreCase: false);
            return Activator.CreateInstance(storeType, new object?[] { directory, mode });
        }
        catch
        {
            return null;
        }
    }

    internal static string? ResolveProviderAssemblyPath(string? providerAssemblyPath = null)
    {
        return ProviderReflection.ResolveAssemblyPath(providerAssemblyPath);
    }

    private async Task<ProviderWireResult> ExecuteWithCredentialAsync(
        ProviderProfileEntry entry,
        Func<object, object?, Task<ProviderWireResult>> operation,
        CancellationToken cancellationToken)
    {
        object? credential = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(entry.CredentialReference))
            {
                if (credentialStore == null) return ProviderWireResult.Failure(new ProviderWireError("credential.store_unavailable", "unavailable", true, false, "Provider credential storage is unavailable."));
                var getMethod = FindMethod(credentialStore.GetType(), "GetAsync");
                var credentialTask = getMethod.Invoke(credentialStore, new object?[] { entry.CredentialReference, cancellationToken });
                var credentialResult = await AwaitTaskAsync(credentialTask).ConfigureAwait(false);
                if (!TryReadProviderResult(credentialResult, out credential, out var credentialError)) return ProviderWireResult.Failure(credentialError!);
            }

            return await operation(entry.Adapter, credential).ConfigureAwait(false);
        }
        catch (TargetInvocationException exception)
        {
            return ProviderWireResult.Failure(MapUnexpectedException(exception.InnerException, entry.Request.ProviderId));
        }
        catch (OperationCanceledException)
        {
            return ProviderWireResult.Failure(cancellationToken.IsCancellationRequested ? ProviderWireAdapter.Cancelled() : ProviderWireAdapter.DeadlineExpired());
        }
        catch (Exception exception)
        {
            return ProviderWireResult.Failure(MapUnexpectedException(exception, entry.Request.ProviderId));
        }
        finally
        {
            if (credential is IDisposable disposable) disposable.Dispose();
        }
    }

    private ProviderWireError? ValidateProfilePolicy(ProviderWireRequest request)
    {
        if (request.IsCloud && string.IsNullOrWhiteSpace(request.CredentialReference))
        {
            return new ProviderWireError("credential.reference_required", "invalid_request", false, false, "Cloud provider profiles require a credential reference.");
        }

        if (!Uri.TryCreate(request.BaseUrl, UriKind.Absolute, out var uri))
        {
            return new ProviderWireError("profile.base_url_invalid", "invalid_request", false, false, "Provider base URL is invalid.");
        }

        if (!request.IsCloud && !IsLoopbackHost(uri.Host))
        {
            return new ProviderWireError("endpoint.local_only", "policy_denied", false, false, "Local provider profiles must use a loopback endpoint.");
        }

        return null;
    }

    private object CreateAdapter(ProviderWireRequest request)
    {
        var assembly = GetProviderAssembly() ?? throw new ProviderRuntimeException(ProviderWireAdapter.RuntimeUnavailable());
        var kindType = RequireType(assembly, "MarcusAwakeProvider.ProviderKind");
        var profileType = RequireType(assembly, "MarcusAwakeProvider.ProviderConnectionProfile");
        var endpointPolicyType = RequireType(assembly, "MarcusAwakeProvider.ExactOriginEndpointPolicy");
        var limitsType = RequireType(assembly, "MarcusAwakeProvider.ProviderLimits");
        var clockType = RequireType(assembly, "MarcusAwakeProvider.SystemProviderClock");
        var adapterType = RequireType(assembly, request.ProviderKind switch
        {
            "openai_compatible" => "MarcusAwakeProvider.OpenAiCompatibleProvider",
            "anthropic" => "MarcusAwakeProvider.AnthropicProvider",
            "ollama" => "MarcusAwakeProvider.OllamaProvider",
            _ => throw new ProviderRuntimeException(ProviderWireAdapter.SchemaError())
        });
        var kindName = request.ProviderKind switch
        {
            "openai_compatible" => "OpenAiCompatible",
            "anthropic" => "Anthropic",
            "ollama" => "Ollama",
            _ => throw new ProviderRuntimeException(ProviderWireAdapter.SchemaError())
        };
        var kind = Enum.Parse(kindType, kindName, ignoreCase: false);
        var profile = Activator.CreateInstance(profileType, new object?[]
        {
            request.ProviderId,
            kind,
            new Uri(request.BaseUrl, UriKind.Absolute),
            request.DefaultModel,
            request.CredentialReference,
            (bool?)request.IsCloud
        });
        var endpointPolicy = Activator.CreateInstance(endpointPolicyType);
        var limits = Activator.CreateInstance(limitsType, new object[]
        {
            1_048_576,
            262_144,
            65_536,
            4_194_304,
            65_536,
            16_384,
            65_536,
            4_096,
            1_024
        });
        var clock = clockType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            ?? Activator.CreateInstance(clockType);
        var constructor = adapterType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(candidate =>
            {
                var parameters = candidate.GetParameters();
                return parameters.Length == 5
                    && parameters[0].ParameterType.IsAssignableFrom(profileType)
                    && parameters[1].ParameterType == typeof(HttpMessageInvoker)
                    && parameters[2].ParameterType.IsAssignableFrom(endpointPolicyType)
                    && parameters[3].ParameterType.IsAssignableFrom(limitsType)
                    && parameters[4].ParameterType.IsAssignableFrom(clockType);
            });
        if (constructor == null) throw new ProviderRuntimeException(ProviderWireAdapter.RuntimeUnavailable("Provider adapter constructor is unavailable."));
        return constructor.Invoke(new[] { profile, invoker, endpointPolicy, limits, clock });
    }

    private object CreateChatRequest(ProviderWireRequest request)
    {
        var assembly = GetProviderAssembly() ?? throw new ProviderRuntimeException(ProviderWireAdapter.RuntimeUnavailable());
        var messageType = RequireType(assembly, "MarcusAwakeProvider.ProviderMessage");
        var chatType = RequireType(assembly, "MarcusAwakeProvider.ProviderChatRequest");
        var messageListType = typeof(List<>).MakeGenericType(messageType);
        var messageList = (IList)Activator.CreateInstance(messageListType)!;
        foreach (var message in request.Messages)
        {
            messageList.Add(Activator.CreateInstance(messageType, new object?[] { message.Role, message.Content })!);
        }

        var readOnlyListType = typeof(IReadOnlyList<>).MakeGenericType(messageType);
        var constructor = chatType.GetConstructor(new[] { readOnlyListType, typeof(string), typeof(int?), typeof(double?), typeof(string) });
        if (constructor == null) throw new ProviderRuntimeException(ProviderWireAdapter.RuntimeUnavailable("Provider request constructor is unavailable."));
        return constructor.Invoke(new object?[] { messageList, request.Model, request.MaxOutputTokens, request.Temperature, request.ResponseSchemaJson });
    }

    private async Task<ProviderCredentialLoadResult> LoadCredentialAsync(ProviderProfileEntry entry, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entry.CredentialReference)) return ProviderCredentialLoadResult.Success(null);
        if (credentialStore == null) return ProviderCredentialLoadResult.Failure(new ProviderWireError("credential.store_unavailable", "unavailable", true, false, "Provider credential storage is unavailable."));
        try
        {
            var getMethod = FindMethod(credentialStore.GetType(), "GetAsync");
            var credentialTask = getMethod.Invoke(credentialStore, new object?[] { entry.CredentialReference, cancellationToken });
            var credentialResult = await AwaitTaskAsync(credentialTask).ConfigureAwait(false);
            if (!TryReadProviderResult(credentialResult, out var credential, out var credentialError)) return ProviderCredentialLoadResult.Failure(credentialError!);
            return ProviderCredentialLoadResult.Success(credential);
        }
        catch (TargetInvocationException exception)
        {
            return ProviderCredentialLoadResult.Failure(MapUnexpectedException(exception.InnerException, entry.Request.ProviderId));
        }
        catch (OperationCanceledException)
        {
            return ProviderCredentialLoadResult.Failure(ProviderWireAdapter.Cancelled());
        }
        catch (Exception exception)
        {
            return ProviderCredentialLoadResult.Failure(MapUnexpectedException(exception, entry.Request.ProviderId));
        }
    }

    private object CreateRouteCandidate(object adapter, object? credential)
    {
        var assembly = GetProviderAssembly() ?? throw new ProviderRuntimeException(ProviderWireAdapter.RuntimeUnavailable());
        var candidateType = RequireType(assembly, "MarcusAwakeProvider.ProviderRouteCandidate");
        var constructor = candidateType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(candidate => candidate.GetParameters().Length == 2);
        if (constructor == null)
        {
            throw new ProviderRuntimeException(ProviderStreamErrors.BridgeInvalid());
        }
        return constructor.Invoke(new[] { adapter, credential });
    }

    private object CreateRouter(IReadOnlyList<object> candidates)
    {
        var assembly = GetProviderAssembly() ?? throw new ProviderRuntimeException(ProviderWireAdapter.RuntimeUnavailable());
        var candidateType = RequireType(assembly, "MarcusAwakeProvider.ProviderRouteCandidate");
        var routerType = RequireType(assembly, "MarcusAwakeProvider.ProviderRouter");
        var listType = typeof(List<>).MakeGenericType(candidateType);
        var list = (IList)Activator.CreateInstance(listType)!;
        for (var index = 0; index < candidates.Count; index++) list.Add(candidates[index]);
        var constructor = routerType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(candidate => candidate.GetParameters().Length == 1);
        if (constructor == null)
        {
            throw new ProviderRuntimeException(ProviderStreamErrors.BridgeInvalid());
        }
        return constructor.Invoke(new object?[] { list });
    }

    private static object InvokeRouterStream(object router, object providerRequest, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        var method = FindMethod(router.GetType(), "StreamAsync");
        var stream = method.Invoke(router, new object?[] { providerRequest, deadline, cancellationToken });
        if (stream == null)
        {
            throw new ProviderRuntimeException(ProviderStreamErrors.BridgeInvalid());
        }
        return stream;
    }

    private static object CreateEnumerator(object stream, CancellationToken cancellationToken, out PropertyInfo currentProperty, out MethodInfo moveNextMethod, out MethodInfo disposeMethod)
    {
        var enumerableType = stream.GetType().GetInterfaces().FirstOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
        var getEnumerator = stream.GetType().GetMethod("GetAsyncEnumerator", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(CancellationToken) }, null)
            ?? enumerableType?.GetMethod("GetAsyncEnumerator", new[] { typeof(CancellationToken) });
        if (getEnumerator == null)
        {
            throw new ProviderRuntimeException(ProviderStreamErrors.BridgeInvalid());
        }
        var enumerator = getEnumerator.Invoke(stream, new object?[] { cancellationToken });
        if (enumerator == null)
        {
            throw new ProviderRuntimeException(ProviderStreamErrors.BridgeInvalid());
        }
        var enumeratorType = enumerator.GetType();
        var asyncEnumeratorType = enumeratorType.GetInterfaces().FirstOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAsyncEnumerator<>));
        var asyncDisposableType = enumeratorType.GetInterfaces().FirstOrDefault(type => type == typeof(IAsyncDisposable));
        currentProperty = enumeratorType.GetProperty("Current", BindingFlags.Public | BindingFlags.Instance)
            ?? asyncEnumeratorType?.GetProperty("Current")!;
        moveNextMethod = enumeratorType.GetMethod("MoveNextAsync", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null)
            ?? asyncEnumeratorType?.GetMethod("MoveNextAsync")!;
        disposeMethod = enumeratorType.GetMethod("DisposeAsync", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null)
            ?? asyncDisposableType?.GetMethod("DisposeAsync")!;
        if (currentProperty == null || moveNextMethod == null || disposeMethod == null)
        {
            throw new ProviderRuntimeException(ProviderStreamErrors.BridgeInvalid());
        }
        return enumerator;
    }

    private bool SnapshotStillValid(IReadOnlyList<ProviderProfileEntry> snapshot, long snapshotGeneration)
    {
        lock (sync)
        {
            if (mutationGeneration != snapshotGeneration) return false;
            for (var index = 0; index < snapshot.Count; index++)
            {
                if (!profiles.TryGetValue(snapshot[index].Key, out var current) || current.EntryGeneration != snapshot[index].EntryGeneration) return false;
            }

            return true;
        }
    }

    private static ProviderWireError ResolveCancellationError(CancellationToken cancellationToken, DateTimeOffset deadline)
    {
        return cancellationToken.IsCancellationRequested && DateTimeOffset.UtcNow < deadline
            ? ProviderWireAdapter.Cancelled()
            : ProviderWireAdapter.DeadlineExpired();
    }

    private bool TryGetProfile(PipeEnvelope envelope, out ProviderProfileEntry entry)
    {
        var key = ProviderScopeKey.FromEnvelope(envelope);
        lock (sync)
        {
            if (disposed)
            {
                entry = null!;
                return false;
            }

            return profiles.TryGetValue(key, out entry!);
        }
    }

    private Assembly? GetProviderAssembly()
    {
        lock (sync)
        {
            if (providerAssembly != null) return providerAssembly;
            providerAssembly = ProviderReflection.TryLoadAssembly(explicitAssemblyPath);
            return providerAssembly;
        }
    }

    private static MethodInfo FindMethod(Type type, string name)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => StringComparer.Ordinal.Equals(method.Name, name))
            .ToArray();
        if (methods.Length != 1) throw new InvalidOperationException("provider_method_unavailable:" + name);
        return methods[0];
    }

    private static async Task<object?> AwaitTaskAsync(object? taskObject)
    {
        if (taskObject is not Task task) throw new InvalidOperationException("provider_task_invalid");
        await task.ConfigureAwait(false);
        return task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance)?.GetValue(task);
    }

    private static bool TryReadProviderResult(object? providerResult, out object? value, out ProviderWireError? error)
    {
        value = null;
        error = null;
        if (providerResult == null)
        {
            error = new ProviderWireError("provider.result_missing", "internal_failure", false, false, "Provider returned no result.");
            return false;
        }

        var resultType = providerResult.GetType();
        var successValue = resultType.GetProperty("IsSuccess", BindingFlags.Public | BindingFlags.Instance)?.GetValue(providerResult);
        if (successValue is not bool success || !success)
        {
            var providerError = resultType.GetProperty("Error", BindingFlags.Public | BindingFlags.Instance)?.GetValue(providerResult);
            error = MapProviderError(providerError);
            return false;
        }

        value = resultType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)?.GetValue(providerResult);
        return true;
    }

    private static IReadOnlyList<ProviderModelProjection>? ReadModels(object? value)
    {
        if (value is not IEnumerable enumerable) return null;
        var models = new List<ProviderModelProjection>();
        foreach (var model in enumerable)
        {
            if (model == null) return null;
            var modelType = model.GetType();
            var id = modelType.GetProperty("Id")?.GetValue(model) as string;
            var displayName = modelType.GetProperty("DisplayName")?.GetValue(model) as string;
            if (string.IsNullOrWhiteSpace(id) || id.Length > 256 || (displayName != null && displayName.Length > 256)) return null;
            models.Add(new ProviderModelProjection(id, displayName ?? string.Empty));
            if (models.Count > 1_024) return null;
        }

        return models.AsReadOnly();
    }

    private static ProviderCompletionProjection? ReadCompletion(object? value)
    {
        if (value == null) return null;
        var type = value.GetType();
        var modelId = type.GetProperty("ModelId")?.GetValue(value) as string;
        var content = type.GetProperty("Content")?.GetValue(value) as string;
        if (string.IsNullOrWhiteSpace(modelId) || content == null || modelId.Length > 256 || content.Length > 4 * 1024 * 1024) return null;
        var structuredJson = type.GetProperty("StructuredJson")?.GetValue(value) as string;
        if (structuredJson != null && structuredJson.Length > 65_536) return null;

        int? inputTokens = null;
        int? outputTokens = null;
        var usage = type.GetProperty("Usage")?.GetValue(value);
        if (usage != null)
        {
            var usageType = usage.GetType();
            inputTokens = ReadNullableInt(usageType.GetProperty("InputTokens")?.GetValue(usage));
            outputTokens = ReadNullableInt(usageType.GetProperty("OutputTokens")?.GetValue(usage));
            if ((inputTokens.HasValue && inputTokens.Value < 0) || (outputTokens.HasValue && outputTokens.Value < 0)) return null;
        }

        return new ProviderCompletionProjection(modelId, content, structuredJson, inputTokens, outputTokens);
    }

    private static int? ReadNullableInt(object? value)
    {
        if (value == null) return null;
        if (value is int intValue) return intValue;
        var nullableValue = value.GetType().GetProperty("Value")?.GetValue(value);
        return nullableValue is int nullableInt ? nullableInt : null;
    }

    internal static ProviderWireError MapProviderError(object? providerError)
    {
        if (providerError == null) return new ProviderWireError("provider.error_missing", "internal_failure", false, false, "Provider returned no error details.");
        var type = providerError.GetType();
        var code = type.GetProperty("Code")?.GetValue(providerError) as string;
        var categoryValue = type.GetProperty("Category")?.GetValue(providerError)?.ToString();
        var safeMessage = type.GetProperty("SafeMessage")?.GetValue(providerError) as string;
        var retryable = type.GetProperty("Retryable")?.GetValue(providerError) as bool? ?? false;
        var statusCode = ReadNullableInt(type.GetProperty("StatusCode")?.GetValue(providerError));
        return new ProviderWireError(
            string.IsNullOrWhiteSpace(code) ? "provider.failure" : Clamp(code, 160),
            MapCategory(categoryValue),
            retryable,
            false,
            string.IsNullOrWhiteSpace(safeMessage) ? "Provider request failed." : Clamp(safeMessage, 512),
            statusCode);
    }

    private static ProviderWireError ProfileConstructionError(Exception? exception)
    {
        if (exception is ProviderRuntimeException providerRuntimeException) return providerRuntimeException.Error;
        return new ProviderWireError("provider.profile_invalid", "invalid_request", false, false, "Provider profile could not be constructed.");
    }

    private static ProviderWireError MapUnexpectedException(Exception? exception, string providerId)
    {
        if (exception is OperationCanceledException) return ProviderWireAdapter.Cancelled();
        return new ProviderWireError("provider.runtime_failure", "internal_failure", false, false, "Provider runtime failed while executing the request.");
    }

    private static string MapCategory(string? category)
    {
        return category switch
        {
            "InvalidRequest" => "invalid_request",
            "Authentication" => "authentication",
            "Forbidden" => "forbidden",
            "NotFound" => "not_found",
            "Conflict" => "conflict",
            "RateLimited" => "rate_limited",
            "Timeout" => "timeout",
            "Unavailable" => "unavailable",
            "ServerUnavailable" => "server_unavailable",
            "TransportUnavailable" => "transport_unavailable",
            "RedirectRejected" => "redirect_rejected",
            "PolicyDenied" => "policy_denied",
            "MalformedResponse" => "malformed_response",
            "IncompleteStream" => "incomplete_stream",
            "Cancelled" => "cancelled",
            "Unsupported" => "unsupported",
            "ResourceExhausted" => "resource_exhausted",
            "CorruptCredential" => "corrupt_credential",
            "InternalFailure" => "internal_failure",
            _ => "internal_failure"
        };
    }

    private static string Clamp(string value, int maximumCharacters)
    {
        return value.Length <= maximumCharacters ? value : value.Substring(0, maximumCharacters);
    }

    private static bool IsLoopbackHost(string host)
    {
        return StringComparer.OrdinalIgnoreCase.Equals(host, "localhost")
            || StringComparer.Ordinal.Equals(host, "127.0.0.1")
            || StringComparer.Ordinal.Equals(host, "::1");
    }

    private static Type RequireType(Assembly assembly, string fullName)
    {
        return assembly.GetType(fullName, throwOnError: true)!;
    }

    private void ThrowIfDisposed()
    {
        if (disposed) throw new ObjectDisposedException(nameof(ProviderRegistry));
    }

    internal sealed class ProviderProfileEntry
    {
        internal ProviderProfileEntry(ProviderScopeKey key, ProviderWireRequest request, object adapter, long entryGeneration, long registrationOrdinal)
        {
            Key = key;
            Request = request;
            Adapter = adapter;
            CredentialReference = request.CredentialReference;
            EntryGeneration = entryGeneration;
            RegistrationOrdinal = registrationOrdinal;
        }

        internal ProviderScopeKey Key { get; }
        internal ProviderWireRequest Request { get; }
        internal object Adapter { get; }
        internal string? CredentialReference { get; }
        internal long EntryGeneration { get; }
        internal long RegistrationOrdinal { get; }

        internal bool Matches(PipeEnvelope envelope, ProviderWireRequest request)
        {
            return StringComparer.Ordinal.Equals(Key.OwnerId, envelope.OwnerId)
                && StringComparer.Ordinal.Equals(Key.CampaignGuid, envelope.CampaignGuid)
                && StringComparer.Ordinal.Equals(Key.TimelineId, envelope.TimelineId)
                && StringComparer.Ordinal.Equals(Key.SessionId, envelope.SessionId)
                && StringComparer.Ordinal.Equals(Request.ProfileId, request.ProfileId)
                && StringComparer.Ordinal.Equals(Request.RouteId, request.RouteId);
        }
    }

    internal sealed class ProviderStreamCandidateSnapshot
    {
        private ProviderStreamCandidateSnapshot(IReadOnlyList<ProviderProfileEntry>? entries, long mutationGeneration, ProviderWireError? error)
        {
            Entries = entries;
            MutationGeneration = mutationGeneration;
            Error = error;
        }

        internal IReadOnlyList<ProviderProfileEntry>? Entries { get; }
        internal long MutationGeneration { get; }
        internal ProviderWireError? Error { get; }

        internal static ProviderStreamCandidateSnapshot Success(IReadOnlyList<ProviderProfileEntry> entries, long mutationGeneration)
        {
            return new ProviderStreamCandidateSnapshot(entries, mutationGeneration, null);
        }

        internal static ProviderStreamCandidateSnapshot Failure(ProviderWireError error)
        {
            return new ProviderStreamCandidateSnapshot(null, 0, error);
        }
    }

    internal readonly struct ProviderScopeKey : IEquatable<ProviderScopeKey>
    {
        private ProviderScopeKey(string ownerId, string campaignGuid, string timelineId, string sessionId, string profileId, string providerId, string routeId)
        {
            OwnerId = ownerId;
            CampaignGuid = campaignGuid;
            TimelineId = timelineId;
            SessionId = sessionId;
            ProfileId = profileId;
            ProviderId = providerId;
            RouteId = routeId;
        }

        internal string OwnerId { get; }
        internal string CampaignGuid { get; }
        internal string TimelineId { get; }
        internal string SessionId { get; }
        internal string ProfileId { get; }
        internal string ProviderId { get; }
        internal string RouteId { get; }

        internal static ProviderScopeKey FromEnvelope(PipeEnvelope envelope)
        {
            var taskScope = envelope.TaskScope ?? throw new InvalidOperationException("task_scope_missing");
            return new ProviderScopeKey(
                envelope.OwnerId,
                envelope.CampaignGuid,
                envelope.TimelineId,
                envelope.SessionId,
                taskScope.ProfileId,
                taskScope.ProviderId,
                taskScope.RouteId);
        }

        public bool Equals(ProviderScopeKey other)
        {
            return StringComparer.Ordinal.Equals(OwnerId, other.OwnerId)
                && StringComparer.Ordinal.Equals(CampaignGuid, other.CampaignGuid)
                && StringComparer.Ordinal.Equals(TimelineId, other.TimelineId)
                && StringComparer.Ordinal.Equals(SessionId, other.SessionId)
                && StringComparer.Ordinal.Equals(ProfileId, other.ProfileId)
                && StringComparer.Ordinal.Equals(ProviderId, other.ProviderId)
                && StringComparer.Ordinal.Equals(RouteId, other.RouteId);
        }

        public override bool Equals(object? obj) => obj is ProviderScopeKey other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(OwnerId, StringComparer.Ordinal);
            hash.Add(CampaignGuid, StringComparer.Ordinal);
            hash.Add(TimelineId, StringComparer.Ordinal);
            hash.Add(SessionId, StringComparer.Ordinal);
            hash.Add(ProfileId, StringComparer.Ordinal);
            hash.Add(ProviderId, StringComparer.Ordinal);
            hash.Add(RouteId, StringComparer.Ordinal);
            return hash.ToHashCode();
        }
    }

    private sealed class ProviderRuntimeException : Exception
    {
        internal ProviderRuntimeException(ProviderWireError error)
        {
            Error = error;
        }

        internal ProviderWireError Error { get; }
    }
}

internal static class ProviderReflection
{
    private const string ProviderAssemblyName = "MarcusAwakeProvider.dll";
    private const string ProviderAssemblyPathEnvironmentVariable = "MARCUS_AWAKE_PROVIDER_ASSEMBLY_PATH";

    internal static string? ResolveAssemblyPath(string? explicitPath = null)
    {
        var configured = string.IsNullOrWhiteSpace(explicitPath)
            ? Environment.GetEnvironmentVariable(ProviderAssemblyPathEnvironmentVariable)
            : explicitPath;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var fullConfigured = Path.GetFullPath(configured);
            return File.Exists(fullConfigured) ? fullConfigured : null;
        }

        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, ProviderAssemblyName),
            Path.Combine(baseDirectory, "..", "..", "MarcusAwakeProvider", "_build_out", "Release", ProviderAssemblyName),
            Path.Combine(baseDirectory, "..", "..", "..", "MarcusAwakeProvider", "_build_out", "Release", ProviderAssemblyName)
        };
        foreach (var candidate in candidates)
        {
            var fullCandidate = Path.GetFullPath(candidate);
            if (File.Exists(fullCandidate)) return fullCandidate;
        }

        return null;
    }

    internal static Assembly? TryLoadAssembly(string? explicitPath = null)
    {
        var path = ResolveAssemblyPath(explicitPath);
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            return Assembly.LoadFrom(path);
        }
        catch
        {
            return null;
        }
    }
}
