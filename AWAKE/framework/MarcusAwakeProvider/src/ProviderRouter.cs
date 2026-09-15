using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace MarcusAwakeProvider;

public sealed partial class ProviderRouter
{
    private readonly IReadOnlyList<ProviderRouteCandidate> candidates;

    public ProviderRouter(IEnumerable<ProviderRouteCandidate> candidates)
    {
        if (candidates == null) throw new ArgumentNullException(nameof(candidates));
        var copy = new List<ProviderRouteCandidate>();
        foreach (var candidate in candidates)
        {
            if (candidate == null) throw new ArgumentException("Route candidates cannot contain null values.", nameof(candidates));
            copy.Add(candidate);
        }

        this.candidates = copy.AsReadOnly();
    }

    public IReadOnlyList<ProviderRouteCandidate> Candidates => candidates;

    public Task<ProviderResult<IReadOnlyList<ProviderModel>>> ListModelsAsync(DateTimeOffset deadline, CancellationToken cancellationToken = default)
    {
        return ExecuteUnaryAsync(
            candidate => candidate.Adapter.ListModelsAsync(candidate.Credential, deadline, cancellationToken),
            null,
            cancellationToken);
    }

    public Task<ProviderResult<ProviderConnectivityResult>> TestConnectionAsync(DateTimeOffset deadline, CancellationToken cancellationToken = default)
    {
        return ExecuteUnaryAsync(
            candidate => candidate.Adapter.TestConnectionAsync(candidate.Credential, deadline, cancellationToken),
            null,
            cancellationToken);
    }

    public Task<ProviderResult<ProviderCompletion>> CompleteAsync(ProviderChatRequest request, DateTimeOffset deadline, CancellationToken cancellationToken = default)
    {
        return ExecuteUnaryAsync(
            candidate => candidate.Adapter.CompleteAsync(request, candidate.Credential, deadline, cancellationToken),
            request?.Model,
            cancellationToken);
    }

    public async IAsyncEnumerable<ProviderStreamEvent> StreamAsync(
        ProviderChatRequest request,
        DateTimeOffset deadline,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var uniqueCandidates = BuildUniqueCandidates(request?.Model);
        if (uniqueCandidates.Count == 0)
        {
            yield return new ProviderStreamEvent(ProviderStreamEventKind.Started, 1, "router", string.Empty, string.Empty, null, null, string.Empty, string.Empty);
            yield return ProviderStreamingSupport.Terminal("router", string.Empty, NoRouteError(), 1);
            yield break;
        }

        var sequence = 0L;
        var visibleOutput = false;
        var canonicalStarted = false;
        for (var candidateIndex = 0; candidateIndex < uniqueCandidates.Count; candidateIndex++)
        {
            var candidate = uniqueCandidates[candidateIndex];
            var model = ResolveCandidateModel(candidate, request?.Model);
            if (!canonicalStarted)
            {
                yield return new ProviderStreamEvent(ProviderStreamEventKind.Started, ++sequence, candidate.Adapter.Profile.ProviderId, model, string.Empty, null, null, string.Empty, string.Empty);
                canonicalStarted = true;
            }
            ProviderError? failure = null;
            var completed = false;
            using var preparedCandidate = PrepareCandidate(candidate);
            var effectiveCandidate = preparedCandidate.Candidate;

            await using var enumerator = effectiveCandidate.Adapter.StreamAsync(request!, effectiveCandidate.Credential, deadline, cancellationToken).GetAsyncEnumerator(cancellationToken);
            while (true)
            {
                var step = await ProviderStreamingSupport.MoveNextAsync(enumerator).ConfigureAwait(false);
                if (step.Exception != null)
                {
                    failure = ProviderStreamingSupport.MapReadException(
                        step.Exception,
                        candidate.Adapter.Profile.ProviderId,
                        model,
                        () => cancellationToken.IsCancellationRequested
                            ? new ProviderError("request.cancelled", ProviderErrorCategory.Cancelled, "Provider request was cancelled.", false, candidate.Adapter.Profile.ProviderId, model)
                            : new ProviderError("request.deadline_expired", ProviderErrorCategory.Timeout, "Provider request deadline expired.", false, candidate.Adapter.Profile.ProviderId, model));
                    break;
                }

                if (step.End)
                {
                    failure = ProviderResponseSupport.Incomplete(candidate.Adapter.Profile.ProviderId, model);
                    break;
                }

                var streamEvent = step.Item!;
                if (streamEvent.Kind == ProviderStreamEventKind.Started) continue;
                if (streamEvent.Kind == ProviderStreamEventKind.TextDelta && !string.IsNullOrEmpty(streamEvent.Text)) visibleOutput = true;
                if (streamEvent.Kind == ProviderStreamEventKind.Completed)
                {
                    if (!visibleOutput && !string.IsNullOrEmpty(streamEvent.Text))
                    {
                        visibleOutput = true;
                        yield return new ProviderStreamEvent(ProviderStreamEventKind.TextDelta, ++sequence, streamEvent.ProviderId, streamEvent.ModelId, streamEvent.Text, null, null, string.Empty, string.Empty);
                    }
                    yield return new ProviderStreamEvent(ProviderStreamEventKind.Completed, ++sequence, streamEvent.ProviderId, streamEvent.ModelId, string.Empty, streamEvent.Usage, null, string.Empty, string.Empty, streamEvent.StructuredJson);
                    completed = true;
                    break;
                }

                if (streamEvent.Kind == ProviderStreamEventKind.Cancelled)
                {
                    yield return streamEvent.WithSequence(++sequence);
                    yield break;
                }

                if (streamEvent.Kind == ProviderStreamEventKind.Failed)
                {
                    failure = streamEvent.Error ?? new ProviderError("router.provider_failure", ProviderErrorCategory.InternalFailure, "Provider stream failed without an error.", false, candidate.Adapter.Profile.ProviderId, model);
                    break;
                }

                yield return streamEvent.WithSequence(++sequence);
            }

            if (completed) yield break;
            failure ??= ProviderResponseSupport.Incomplete(candidate.Adapter.Profile.ProviderId, model);
            if (failure.Category == ProviderErrorCategory.Cancelled)
            {
                yield return ProviderStreamingSupport.Terminal(candidate.Adapter.Profile.ProviderId, model, failure, ++sequence);
                yield break;
            }

            var canFallback = !visibleOutput && failure.Retryable && candidateIndex + 1 < uniqueCandidates.Count;
            if (canFallback)
            {
                var nextCandidate = uniqueCandidates[candidateIndex + 1];
                var nextModel = ResolveCandidateModel(nextCandidate, request?.Model);
                yield return new ProviderStreamEvent(
                    ProviderStreamEventKind.RouteChanged,
                    ++sequence,
                    candidate.Adapter.Profile.ProviderId,
                    model,
                    string.Empty,
                    null,
                    null,
                    candidate.Adapter.Profile.ProviderId,
                    nextCandidate.Adapter.Profile.ProviderId);
                continue;
            }

            yield return ProviderStreamingSupport.Terminal(candidate.Adapter.Profile.ProviderId, model, failure, ++sequence);
            yield break;
        }
    }

    private async Task<ProviderResult<T>> ExecuteUnaryAsync<T>(
        Func<ProviderRouteCandidate, Task<ProviderResult<T>>> operation,
        string? requestedModel,
        CancellationToken cancellationToken)
    {
        ProviderError? lastError = null;
        foreach (var candidate in BuildUniqueCandidates(requestedModel))
        {
            ProviderResult<T> result;
            using (var preparedCandidate = PrepareCandidate(candidate))
            {
                try
                {
                    result = await operation(preparedCandidate.Candidate).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    var error = cancellationToken.IsCancellationRequested
                        ? new ProviderError("request.cancelled", ProviderErrorCategory.Cancelled, "Provider request was cancelled.", false, candidate.Adapter.Profile.ProviderId, ResolveCandidateModel(candidate, requestedModel))
                        : new ProviderError("request.deadline_expired", ProviderErrorCategory.Timeout, "Provider request ended during deadline cancellation.", false, candidate.Adapter.Profile.ProviderId, ResolveCandidateModel(candidate, requestedModel));
                    return ProviderResult<T>.Failed(error);
                }
                catch (HttpRequestException)
                {
                    result = ProviderResult<T>.Failed(new ProviderError("transport.unavailable", ProviderErrorCategory.TransportUnavailable, "Provider transport is unavailable.", true, candidate.Adapter.Profile.ProviderId, ResolveCandidateModel(candidate, requestedModel)));
                }
                catch (Exception)
                {
                    result = ProviderResult<T>.Failed(new ProviderError("router.adapter_failure", ProviderErrorCategory.InternalFailure, "Provider adapter failed unexpectedly.", false, candidate.Adapter.Profile.ProviderId, ResolveCandidateModel(candidate, requestedModel)));
                }
            }

            if (result.IsSuccess) return result;
            lastError = result.Error ?? new ProviderError("router.provider_failure", ProviderErrorCategory.InternalFailure, "Provider operation failed without an error.", false, candidate.Adapter.Profile.ProviderId, ResolveCandidateModel(candidate, requestedModel));
            if (!lastError.Retryable) return ProviderResult<T>.Failed(lastError);
        }

        return ProviderResult<T>.Failed(lastError ?? NoRouteError());
    }

    private List<ProviderRouteCandidate> BuildUniqueCandidates(string? requestedModel)
    {
        var unique = new List<ProviderRouteCandidate>();
        var providerIds = new HashSet<string>(StringComparer.Ordinal);
        var attemptIdentities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            var profile = candidate.Adapter.Profile;
            if (!providerIds.Add(profile.ProviderId)) continue;
            var model = ResolveCandidateModel(candidate, requestedModel);
            var credentialReference = candidate.Credential?.Reference ?? profile.CredentialReference ?? string.Empty;
            var identity = profile.EffectiveAttemptIdentity(model, credentialReference);
            if (!attemptIdentities.Add(identity)) continue;
            unique.Add(candidate);
        }

        return unique;
    }

    private static PreparedProviderRoute PrepareCandidate(ProviderRouteCandidate candidate)
    {
        var profileReference = candidate.Adapter.Profile.CredentialReference;
        if (profileReference == null || candidate.Credential == null || StringComparer.Ordinal.Equals(profileReference, candidate.Credential.Reference))
        {
            return new PreparedProviderRoute(candidate, null);
        }

        var secret = candidate.Credential.CopySecretChars();
        try
        {
            var boundCredential = new ApiKeyCredential(profileReference, secret);
            return new PreparedProviderRoute(new ProviderRouteCandidate(candidate.Adapter, boundCredential), boundCredential);
        }
        finally
        {
            Array.Clear(secret, 0, secret.Length);
        }
    }

    private static string ResolveCandidateModel(ProviderRouteCandidate candidate, string? requestedModel)
    {
        return string.IsNullOrWhiteSpace(requestedModel) ? candidate.Adapter.Profile.DefaultModel : requestedModel;
    }

    private static ProviderError NoRouteError()
    {
        return new ProviderError("router.no_route", ProviderErrorCategory.Unavailable, "No provider route is available.", false, "router");
    }

    private sealed class PreparedProviderRoute : IDisposable
    {
        internal PreparedProviderRoute(ProviderRouteCandidate candidate, ApiKeyCredential? ownedCredential)
        {
            Candidate = candidate;
            this.ownedCredential = ownedCredential;
        }

        internal ProviderRouteCandidate Candidate { get; }
        private readonly ApiKeyCredential? ownedCredential;

        public void Dispose()
        {
            ownedCredential?.Dispose();
        }
    }
}
