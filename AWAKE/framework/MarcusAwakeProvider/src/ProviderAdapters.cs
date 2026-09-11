using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace MarcusAwakeProvider;

public sealed class OpenAiCompatibleProvider : ProviderAdapterBase
{
    public OpenAiCompatibleProvider(
        ProviderConnectionProfile profile,
        HttpMessageInvoker invoker,
        IProviderEndpointPolicy? endpointPolicy = null,
        ProviderLimits? limits = null,
        IProviderClock? clock = null)
        : base(profile, invoker, endpointPolicy, limits, clock)
    {
        if (profile.Kind != ProviderKind.OpenAiCompatible) throw new ArgumentException("Profile kind must be OpenAiCompatible.", nameof(profile));
    }

    public override async Task<ProviderResult<IReadOnlyList<ProviderModel>>> ListModelsAsync(ApiKeyCredential? credential, DateTimeOffset deadline, CancellationToken cancellationToken = default)
    {
        var credentialError = ValidateCredential(credential);
        if (credentialError != null) return ProviderResult<IReadOnlyList<ProviderModel>>.Failed(credentialError);

        var responseResult = await SendAsync(HttpMethod.Get, "models", credential, "models", deadline, cancellationToken, "application/json", Profile.DefaultModel).ConfigureAwait(false);
        if (!responseResult.IsSuccess) return ProviderResult<IReadOnlyList<ProviderModel>>.Failed(responseResult.Error!);
        using var response = responseResult.Value!;

        var bodyResult = await ReadBoundedResponseAsync(response, "models", Profile.DefaultModel, cancellationToken, deadline, Limits.MaxResponseBytes).ConfigureAwait(false);
        if (!bodyResult.IsSuccess) return ProviderResult<IReadOnlyList<ProviderModel>>.Failed(bodyResult.Error!);
        var documentResult = ParseJson(bodyResult.Value!, "models", Profile.DefaultModel);
        if (!documentResult.IsSuccess) return ProviderResult<IReadOnlyList<ProviderModel>>.Failed(documentResult.Error!);
        using var document = documentResult.Value!;
        return ProviderResponseSupport.ParseModels(document, "data", Profile.ProviderId, Limits.MaxModels);
    }

    protected override Task<ProviderResult<IReadOnlyList<ProviderModel>>> ListModelsForConnectionAsync(ApiKeyCredential? credential, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        return ListModelsFromEndpointAsync("models", "data", credential, deadline, cancellationToken, false);
    }
    public override async Task<ProviderResult<ProviderCompletion>> CompleteAsync(ProviderChatRequest request, ApiKeyCredential? credential, DateTimeOffset deadline, CancellationToken cancellationToken = default)
    {
        var validationError = ValidateChatRequest(request, credential, out var model);
        if (validationError != null) return ProviderResult<ProviderCompletion>.Failed(validationError);

        var body = JsonPayload.BuildOpenAiChat(request, model, false);
        var bodyError = ValidateRequestBody(body, model);
        if (bodyError != null) return ProviderResult<ProviderCompletion>.Failed(bodyError);

        var responseResult = await SendWithBodyAsync(HttpMethod.Post, "chat/completions", body, credential, "completion", deadline, cancellationToken, "application/json", model).ConfigureAwait(false);
        if (!responseResult.IsSuccess) return ProviderResult<ProviderCompletion>.Failed(responseResult.Error!);
        using var response = responseResult.Value!;

        var bodyResult = await ReadBoundedResponseAsync(response, "completion", model, cancellationToken, deadline, Limits.MaxResponseBytes).ConfigureAwait(false);
        if (!bodyResult.IsSuccess) return ProviderResult<ProviderCompletion>.Failed(bodyResult.Error!);
        var documentResult = ParseJson(bodyResult.Value!, "completion", model);
        if (!documentResult.IsSuccess) return ProviderResult<ProviderCompletion>.Failed(documentResult.Error!);
        using var document = documentResult.Value!;

        var partsResult = ProviderResponseSupport.ParseOpenAiCompletion(document, Profile.ProviderId, model);
        if (!partsResult.IsSuccess) return ProviderResult<ProviderCompletion>.Failed(partsResult.Error!);
        var parts = partsResult.Value!;
        string? structuredJson = null;
        if (request.ResponseSchemaJson != null)
        {
            var structuredResult = ValidateStructuredContent(parts.Content, parts.ModelId);
            if (!structuredResult.IsSuccess) return ProviderResult<ProviderCompletion>.Failed(structuredResult.Error!);
            structuredJson = structuredResult.Value;
        }

        return ProviderResult<ProviderCompletion>.Succeeded(new ProviderCompletion(Profile.ProviderId, parts.ModelId, parts.Content, parts.Usage, structuredJson));
    }

    public override async IAsyncEnumerable<ProviderStreamEvent> StreamAsync(
        ProviderChatRequest request,
        ApiKeyCredential? credential,
        DateTimeOffset deadline,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var model = Profile.DefaultModel;
        var validationError = ValidateChatRequest(request, credential, out model);
        if (validationError != null)
        {
            yield return ProviderStreamingSupport.Terminal(Profile.ProviderId, model, validationError, 1);
            yield break;
        }

        var body = JsonPayload.BuildOpenAiChat(request, model, true);
        var bodyError = ValidateRequestBody(body, model);
        if (bodyError != null)
        {
            yield return ProviderStreamingSupport.Terminal(Profile.ProviderId, model, bodyError, 1);
            yield break;
        }

        var responseResult = await SendWithBodyAsync(HttpMethod.Post, "chat/completions", body, credential, "stream", deadline, cancellationToken, "text/event-stream", model).ConfigureAwait(false);
        if (!responseResult.IsSuccess)
        {
            yield return ProviderStreamingSupport.Terminal(Profile.ProviderId, model, responseResult.Error!, 1);
            yield break;
        }

        using var response = responseResult.Value!;
        using var readCancellation = CreateCancellationSource(cancellationToken, deadline);
        var sequence = 1L;
        var deltaCount = 0;
        var aggregateBytes = 0;
        var aggregate = new StringBuilder();
        ProviderUsage? usage = null;
        var done = false;
        yield return Started(model, sequence);

        var streamResult = await ProviderStreamingSupport.OpenStreamAsync(response.Content, readCancellation.Token, Profile.ProviderId, model, () => ClassifyCancellation(cancellationToken, deadline)).ConfigureAwait(false);
        if (!streamResult.IsSuccess)
        {
            yield return ProviderStreamingSupport.Terminal(Profile.ProviderId, model, streamResult.Error!, ++sequence);
            yield break;
        }

        await using var stream = streamResult.Value!;
        await using var enumerator = BoundedEventReader.ReadSseFramesAsync(stream, Limits, readCancellation.Token).GetAsyncEnumerator(readCancellation.Token);
        while (true)
        {
            var step = await ProviderStreamingSupport.MoveNextAsync(enumerator).ConfigureAwait(false);
            if (step.Exception != null)
            {
                yield return ProviderStreamingSupport.Terminal(Profile.ProviderId, model, ProviderStreamingSupport.MapReadException(step.Exception, Profile.ProviderId, model, () => ClassifyCancellation(cancellationToken, deadline)), ++sequence);
                yield break;
            }

            if (step.End) break;
            var frame = step.Item!;
            if (StringComparer.Ordinal.Equals(frame.Data.Trim(), "[DONE]"))
            {
                done = true;
                break;
            }

            if (frame.Data.Length == 0) continue;
            var documentResult = ParseJson(Encoding.UTF8.GetBytes(frame.Data), "stream", model);
            if (!documentResult.IsSuccess)
            {
                yield return Failed(model, documentResult.Error!, ++sequence);
                yield break;
            }

            using var document = documentResult.Value!;
            var partsResult = ProviderResponseSupport.ParseOpenAiStreamFrame(document, Profile.ProviderId, model);
            if (!partsResult.IsSuccess)
            {
                yield return Failed(model, partsResult.Error!, ++sequence);
                yield break;
            }

            var parts = partsResult.Value!;
            if (!string.IsNullOrEmpty(parts.Text))
            {
                if (++deltaCount > Limits.MaxDeltas)
                {
                    yield return Failed(model, ProviderResponseSupport.TooLarge(Profile.ProviderId, model, "delta_count_too_large"), ++sequence);
                    yield break;
                }

                if (!ProviderStreamingSupport.TryAppendText(aggregate, parts.Text, ref aggregateBytes, Limits.MaxStreamBytes))
                {
                    yield return Failed(model, ProviderResponseSupport.TooLarge(Profile.ProviderId, model, "text_too_large"), ++sequence);
                    yield break;
                }

                if (request.ResponseSchemaJson == null) yield return TextDelta(model, parts.Text, ++sequence);
            }

            if (parts.Usage != null)
            {
                usage = parts.Usage;
                yield return UsageUpdate(model, usage, ++sequence);
            }
        }

        if (!done)
        {
            yield return Failed(model, ProviderResponseSupport.Incomplete(Profile.ProviderId, model), ++sequence);
            yield break;
        }

        if (request.ResponseSchemaJson != null)
        {
            var structuredResult = ValidateStructuredContent(aggregate.ToString(), model);
            if (!structuredResult.IsSuccess)
            {
                yield return Failed(model, structuredResult.Error!, ++sequence);
                yield break;
            }

            yield return CompletedStructured(model, structuredResult.Value, usage, ++sequence);
        }
        else
        {
            yield return Completed(model, aggregate.ToString(), usage, ++sequence);
        }
    }
}

public sealed class AnthropicProvider : ProviderAdapterBase
{
    public AnthropicProvider(
        ProviderConnectionProfile profile,
        HttpMessageInvoker invoker,
        IProviderEndpointPolicy? endpointPolicy = null,
        ProviderLimits? limits = null,
        IProviderClock? clock = null)
        : base(profile, invoker, endpointPolicy, limits, clock)
    {
        if (profile.Kind != ProviderKind.Anthropic) throw new ArgumentException("Profile kind must be Anthropic.", nameof(profile));
    }

    public override async Task<ProviderResult<IReadOnlyList<ProviderModel>>> ListModelsAsync(ApiKeyCredential? credential, DateTimeOffset deadline, CancellationToken cancellationToken = default)
    {
        var credentialError = ValidateCredential(credential);
        if (credentialError != null) return ProviderResult<IReadOnlyList<ProviderModel>>.Failed(credentialError);

        var responseResult = await SendAsync(HttpMethod.Get, "models", credential, "models", deadline, cancellationToken, "application/json", Profile.DefaultModel).ConfigureAwait(false);
        if (!responseResult.IsSuccess) return ProviderResult<IReadOnlyList<ProviderModel>>.Failed(responseResult.Error!);
        using var response = responseResult.Value!;

        var bodyResult = await ReadBoundedResponseAsync(response, "models", Profile.DefaultModel, cancellationToken, deadline, Limits.MaxResponseBytes).ConfigureAwait(false);
        if (!bodyResult.IsSuccess) return ProviderResult<IReadOnlyList<ProviderModel>>.Failed(bodyResult.Error!);
        var documentResult = ParseJson(bodyResult.Value!, "models", Profile.DefaultModel);
        if (!documentResult.IsSuccess) return ProviderResult<IReadOnlyList<ProviderModel>>.Failed(documentResult.Error!);
        using var document = documentResult.Value!;
        return ProviderResponseSupport.ParseModels(document, "data", Profile.ProviderId, Limits.MaxModels);
    }

    protected override Task<ProviderResult<IReadOnlyList<ProviderModel>>> ListModelsForConnectionAsync(ApiKeyCredential? credential, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        return ListModelsFromEndpointAsync("models", "data", credential, deadline, cancellationToken, false);
    }
    public override async Task<ProviderResult<ProviderCompletion>> CompleteAsync(ProviderChatRequest request, ApiKeyCredential? credential, DateTimeOffset deadline, CancellationToken cancellationToken = default)
    {
        var validationError = ValidateChatRequest(request, credential, out var model);
        if (validationError != null) return ProviderResult<ProviderCompletion>.Failed(validationError);

        var body = JsonPayload.BuildAnthropicChat(request, model, false);
        var bodyError = ValidateRequestBody(body, model);
        if (bodyError != null) return ProviderResult<ProviderCompletion>.Failed(bodyError);

        var responseResult = await SendWithBodyAsync(HttpMethod.Post, "messages", body, credential, "completion", deadline, cancellationToken, "application/json", model).ConfigureAwait(false);
        if (!responseResult.IsSuccess) return ProviderResult<ProviderCompletion>.Failed(responseResult.Error!);
        using var response = responseResult.Value!;

        var bodyResult = await ReadBoundedResponseAsync(response, "completion", model, cancellationToken, deadline, Limits.MaxResponseBytes).ConfigureAwait(false);
        if (!bodyResult.IsSuccess) return ProviderResult<ProviderCompletion>.Failed(bodyResult.Error!);
        var documentResult = ParseJson(bodyResult.Value!, "completion", model);
        if (!documentResult.IsSuccess) return ProviderResult<ProviderCompletion>.Failed(documentResult.Error!);
        using var document = documentResult.Value!;

        var partsResult = ProviderResponseSupport.ParseAnthropicCompletion(document, Profile.ProviderId, model);
        if (!partsResult.IsSuccess) return ProviderResult<ProviderCompletion>.Failed(partsResult.Error!);
        var parts = partsResult.Value!;
        string? structuredJson = null;
        if (request.ResponseSchemaJson != null)
        {
            var structuredResult = ValidateStructuredContent(parts.Content, parts.ModelId);
            if (!structuredResult.IsSuccess) return ProviderResult<ProviderCompletion>.Failed(structuredResult.Error!);
            structuredJson = structuredResult.Value;
        }

        return ProviderResult<ProviderCompletion>.Succeeded(new ProviderCompletion(Profile.ProviderId, parts.ModelId, parts.Content, parts.Usage, structuredJson));
    }

    public override async IAsyncEnumerable<ProviderStreamEvent> StreamAsync(
        ProviderChatRequest request,
        ApiKeyCredential? credential,
        DateTimeOffset deadline,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var model = Profile.DefaultModel;
        var validationError = ValidateChatRequest(request, credential, out model);
        if (validationError != null)
        {
            yield return ProviderStreamingSupport.Terminal(Profile.ProviderId, model, validationError, 1);
            yield break;
        }

        var body = JsonPayload.BuildAnthropicChat(request, model, true);
        var bodyError = ValidateRequestBody(body, model);
        if (bodyError != null)
        {
            yield return ProviderStreamingSupport.Terminal(Profile.ProviderId, model, bodyError, 1);
            yield break;
        }

        var responseResult = await SendWithBodyAsync(HttpMethod.Post, "messages", body, credential, "stream", deadline, cancellationToken, "text/event-stream", model).ConfigureAwait(false);
        if (!responseResult.IsSuccess)
        {
            yield return ProviderStreamingSupport.Terminal(Profile.ProviderId, model, responseResult.Error!, 1);
            yield break;
        }

        using var response = responseResult.Value!;
        using var readCancellation = CreateCancellationSource(cancellationToken, deadline);
        var sequence = 1L;
        var deltaCount = 0;
        var aggregateBytes = 0;
        var aggregate = new StringBuilder();
        ProviderUsage? usage = null;
        var done = false;
        yield return Started(model, sequence);

        var streamResult = await ProviderStreamingSupport.OpenStreamAsync(response.Content, readCancellation.Token, Profile.ProviderId, model, () => ClassifyCancellation(cancellationToken, deadline)).ConfigureAwait(false);
        if (!streamResult.IsSuccess)
        {
            yield return ProviderStreamingSupport.Terminal(Profile.ProviderId, model, streamResult.Error!, ++sequence);
            yield break;
        }

        await using var stream = streamResult.Value!;
        await using var enumerator = BoundedEventReader.ReadSseFramesAsync(stream, Limits, readCancellation.Token).GetAsyncEnumerator(readCancellation.Token);
        while (true)
        {
            var step = await ProviderStreamingSupport.MoveNextAsync(enumerator).ConfigureAwait(false);
            if (step.Exception != null)
            {
                yield return ProviderStreamingSupport.Terminal(Profile.ProviderId, model, ProviderStreamingSupport.MapReadException(step.Exception, Profile.ProviderId, model, () => ClassifyCancellation(cancellationToken, deadline)), ++sequence);
                yield break;
            }

            if (step.End) break;
            var frame = step.Item!;
            if (StringComparer.Ordinal.Equals(frame.Data.Trim(), "[DONE]"))
            {
                done = true;
                break;
            }

            if (frame.Data.Length == 0) continue;
            var documentResult = ParseJson(Encoding.UTF8.GetBytes(frame.Data), "stream", model);
            if (!documentResult.IsSuccess)
            {
                yield return Failed(model, documentResult.Error!, ++sequence);
                yield break;
            }

            using var document = documentResult.Value!;
            var partsResult = ProviderResponseSupport.ParseAnthropicStreamFrame(document, frame.EventName, Profile.ProviderId, model);
            if (!partsResult.IsSuccess)
            {
                yield return Failed(model, partsResult.Error!, ++sequence);
                yield break;
            }

            var parts = partsResult.Value!;
            if (parts.IsTerminal) done = true;
            if (!string.IsNullOrEmpty(parts.Text))
            {
                if (++deltaCount > Limits.MaxDeltas)
                {
                    yield return Failed(model, ProviderResponseSupport.TooLarge(Profile.ProviderId, model, "delta_count_too_large"), ++sequence);
                    yield break;
                }

                if (!ProviderStreamingSupport.TryAppendText(aggregate, parts.Text, ref aggregateBytes, Limits.MaxStreamBytes))
                {
                    yield return Failed(model, ProviderResponseSupport.TooLarge(Profile.ProviderId, model, "text_too_large"), ++sequence);
                    yield break;
                }

                if (request.ResponseSchemaJson == null) yield return TextDelta(model, parts.Text, ++sequence);
            }

            if (parts.Usage != null)
            {
                usage = parts.Usage;
                yield return UsageUpdate(model, usage, ++sequence);
            }

            if (done) break;
        }

        if (!done)
        {
            yield return Failed(model, ProviderResponseSupport.Incomplete(Profile.ProviderId, model), ++sequence);
            yield break;
        }

        if (request.ResponseSchemaJson != null)
        {
            var structuredResult = ValidateStructuredContent(aggregate.ToString(), model);
            if (!structuredResult.IsSuccess)
            {
                yield return Failed(model, structuredResult.Error!, ++sequence);
                yield break;
            }

            yield return CompletedStructured(model, structuredResult.Value, usage, ++sequence);
        }
        else
        {
            yield return Completed(model, aggregate.ToString(), usage, ++sequence);
        }
    }
}

public sealed class OllamaProvider : ProviderAdapterBase
{
    public OllamaProvider(
        ProviderConnectionProfile profile,
        HttpMessageInvoker invoker,
        IProviderEndpointPolicy? endpointPolicy = null,
        ProviderLimits? limits = null,
        IProviderClock? clock = null)
        : base(profile, invoker, endpointPolicy, limits, clock)
    {
        if (profile.Kind != ProviderKind.Ollama) throw new ArgumentException("Profile kind must be Ollama.", nameof(profile));
    }

    public override async Task<ProviderResult<IReadOnlyList<ProviderModel>>> ListModelsAsync(ApiKeyCredential? credential, DateTimeOffset deadline, CancellationToken cancellationToken = default)
    {
        var credentialError = ValidateCredential(credential);
        if (credentialError != null) return ProviderResult<IReadOnlyList<ProviderModel>>.Failed(credentialError);

        var responseResult = await SendAsync(HttpMethod.Get, "api/tags", credential, "models", deadline, cancellationToken, "application/json", Profile.DefaultModel).ConfigureAwait(false);
        if (!responseResult.IsSuccess) return ProviderResult<IReadOnlyList<ProviderModel>>.Failed(responseResult.Error!);
        using var response = responseResult.Value!;

        var bodyResult = await ReadBoundedResponseAsync(response, "models", Profile.DefaultModel, cancellationToken, deadline, Limits.MaxResponseBytes).ConfigureAwait(false);
        if (!bodyResult.IsSuccess) return ProviderResult<IReadOnlyList<ProviderModel>>.Failed(bodyResult.Error!);
        var documentResult = ParseJson(bodyResult.Value!, "models", Profile.DefaultModel);
        if (!documentResult.IsSuccess) return ProviderResult<IReadOnlyList<ProviderModel>>.Failed(documentResult.Error!);
        using var document = documentResult.Value!;
        return ProviderResponseSupport.ParseModels(document, "models", Profile.ProviderId, Limits.MaxModels);
    }

    protected override Task<ProviderResult<IReadOnlyList<ProviderModel>>> ListModelsForConnectionAsync(ApiKeyCredential? credential, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        return ListModelsFromEndpointAsync("api/tags", "models", credential, deadline, cancellationToken, false);
    }
    public override async Task<ProviderResult<ProviderCompletion>> CompleteAsync(ProviderChatRequest request, ApiKeyCredential? credential, DateTimeOffset deadline, CancellationToken cancellationToken = default)
    {
        var validationError = ValidateChatRequest(request, credential, out var model);
        if (validationError != null) return ProviderResult<ProviderCompletion>.Failed(validationError);

        var body = JsonPayload.BuildOllamaChat(request, model, false);
        var bodyError = ValidateRequestBody(body, model);
        if (bodyError != null) return ProviderResult<ProviderCompletion>.Failed(bodyError);

        var responseResult = await SendWithBodyAsync(HttpMethod.Post, "api/chat", body, credential, "completion", deadline, cancellationToken, "application/json", model).ConfigureAwait(false);
        if (!responseResult.IsSuccess) return ProviderResult<ProviderCompletion>.Failed(responseResult.Error!);
        using var response = responseResult.Value!;

        var bodyResult = await ReadBoundedResponseAsync(response, "completion", model, cancellationToken, deadline, Limits.MaxResponseBytes).ConfigureAwait(false);
        if (!bodyResult.IsSuccess) return ProviderResult<ProviderCompletion>.Failed(bodyResult.Error!);
        var documentResult = ParseJson(bodyResult.Value!, "completion", model);
        if (!documentResult.IsSuccess) return ProviderResult<ProviderCompletion>.Failed(documentResult.Error!);
        using var document = documentResult.Value!;

        var partsResult = ProviderResponseSupport.ParseOllamaCompletion(document, Profile.ProviderId, model);
        if (!partsResult.IsSuccess) return ProviderResult<ProviderCompletion>.Failed(partsResult.Error!);
        var parts = partsResult.Value!;
        string? structuredJson = null;
        if (request.ResponseSchemaJson != null)
        {
            var structuredResult = ValidateStructuredContent(parts.Content, parts.ModelId);
            if (!structuredResult.IsSuccess) return ProviderResult<ProviderCompletion>.Failed(structuredResult.Error!);
            structuredJson = structuredResult.Value;
        }

        return ProviderResult<ProviderCompletion>.Succeeded(new ProviderCompletion(Profile.ProviderId, parts.ModelId, parts.Content, parts.Usage, structuredJson));
    }

    public override async IAsyncEnumerable<ProviderStreamEvent> StreamAsync(
        ProviderChatRequest request,
        ApiKeyCredential? credential,
        DateTimeOffset deadline,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var model = Profile.DefaultModel;
        var validationError = ValidateChatRequest(request, credential, out model);
        if (validationError != null)
        {
            yield return ProviderStreamingSupport.Terminal(Profile.ProviderId, model, validationError, 1);
            yield break;
        }

        var body = JsonPayload.BuildOllamaChat(request, model, true);
        var bodyError = ValidateRequestBody(body, model);
        if (bodyError != null)
        {
            yield return ProviderStreamingSupport.Terminal(Profile.ProviderId, model, bodyError, 1);
            yield break;
        }

        var responseResult = await SendWithBodyAsync(HttpMethod.Post, "api/chat", body, credential, "stream", deadline, cancellationToken, "application/x-ndjson", model).ConfigureAwait(false);
        if (!responseResult.IsSuccess)
        {
            yield return ProviderStreamingSupport.Terminal(Profile.ProviderId, model, responseResult.Error!, 1);
            yield break;
        }

        using var response = responseResult.Value!;
        using var readCancellation = CreateCancellationSource(cancellationToken, deadline);
        var sequence = 1L;
        var deltaCount = 0;
        var aggregateBytes = 0;
        var aggregate = new StringBuilder();
        ProviderUsage? usage = null;
        var done = false;
        yield return Started(model, sequence);

        var streamResult = await ProviderStreamingSupport.OpenStreamAsync(response.Content, readCancellation.Token, Profile.ProviderId, model, () => ClassifyCancellation(cancellationToken, deadline)).ConfigureAwait(false);
        if (!streamResult.IsSuccess)
        {
            yield return ProviderStreamingSupport.Terminal(Profile.ProviderId, model, streamResult.Error!, ++sequence);
            yield break;
        }

        await using var stream = streamResult.Value!;
        await using var enumerator = BoundedEventReader.ReadLinesAsync(stream, Limits, readCancellation.Token).GetAsyncEnumerator(readCancellation.Token);
        while (true)
        {
            var step = await ProviderStreamingSupport.MoveNextAsync(enumerator).ConfigureAwait(false);
            if (step.Exception != null)
            {
                yield return ProviderStreamingSupport.Terminal(Profile.ProviderId, model, ProviderStreamingSupport.MapReadException(step.Exception, Profile.ProviderId, model, () => ClassifyCancellation(cancellationToken, deadline)), ++sequence);
                yield break;
            }

            if (step.End) break;
            var line = step.Item!;
            if (line.Length == 0) continue;
            var documentResult = ParseJson(Encoding.UTF8.GetBytes(line), "stream", model);
            if (!documentResult.IsSuccess)
            {
                yield return Failed(model, documentResult.Error!, ++sequence);
                yield break;
            }

            using var document = documentResult.Value!;
            var partsResult = ProviderResponseSupport.ParseOllamaStreamLine(document, Profile.ProviderId, model);
            if (!partsResult.IsSuccess)
            {
                yield return Failed(model, partsResult.Error!, ++sequence);
                yield break;
            }

            var parts = partsResult.Value!;
            if (!string.IsNullOrEmpty(parts.Text))
            {
                if (++deltaCount > Limits.MaxDeltas)
                {
                    yield return Failed(model, ProviderResponseSupport.TooLarge(Profile.ProviderId, model, "delta_count_too_large"), ++sequence);
                    yield break;
                }

                if (!ProviderStreamingSupport.TryAppendText(aggregate, parts.Text, ref aggregateBytes, Limits.MaxStreamBytes))
                {
                    yield return Failed(model, ProviderResponseSupport.TooLarge(Profile.ProviderId, model, "text_too_large"), ++sequence);
                    yield break;
                }

                if (request.ResponseSchemaJson == null) yield return TextDelta(model, parts.Text, ++sequence);
            }

            if (parts.Usage != null)
            {
                usage = parts.Usage;
                yield return UsageUpdate(model, usage, ++sequence);
            }

            if (parts.IsTerminal)
            {
                done = true;
                break;
            }
        }

        if (!done)
        {
            yield return Failed(model, ProviderResponseSupport.Incomplete(Profile.ProviderId, model), ++sequence);
            yield break;
        }

        if (request.ResponseSchemaJson != null)
        {
            var structuredResult = ValidateStructuredContent(aggregate.ToString(), model);
            if (!structuredResult.IsSuccess)
            {
                yield return Failed(model, structuredResult.Error!, ++sequence);
                yield break;
            }

            yield return CompletedStructured(model, structuredResult.Value, usage, ++sequence);
        }
        else
        {
            yield return Completed(model, aggregate.ToString(), usage, ++sequence);
        }
    }
}

internal sealed class ProviderReadStep<T>
{
    internal ProviderReadStep(bool end, T? item, Exception? exception)
    {
        End = end;
        Item = item;
        Exception = exception;
    }

    internal bool End { get; }
    internal T? Item { get; }
    internal Exception? Exception { get; }
}

internal static class ProviderStreamingSupport
{
    private static readonly Encoding AggregateUtf8 = new UTF8Encoding(false, false);

    internal static ProviderStreamEvent Terminal(string providerId, string model, ProviderError error, long sequence)
    {
        var kind = error.Category == ProviderErrorCategory.Cancelled ? ProviderStreamEventKind.Cancelled : ProviderStreamEventKind.Failed;
        return new ProviderStreamEvent(kind, sequence, providerId, model, string.Empty, null, error, string.Empty, string.Empty);
    }

    internal static bool TryAppendText(StringBuilder aggregate, string text, ref int aggregateBytes, int maximumBytes)
    {
        var textBytes = AggregateUtf8.GetByteCount(text);
        if (textBytes > maximumBytes - aggregateBytes) return false;
        aggregate.Append(text);
        aggregateBytes += textBytes;
        return true;
    }

    internal static async Task<ProviderResult<Stream>> OpenStreamAsync(
        HttpContent content,
        CancellationToken cancellationToken,
        string providerId,
        string model,
        Func<ProviderError> cancellationError)
    {
        try
        {
            return ProviderResult<Stream>.Succeeded(await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            return ProviderResult<Stream>.Failed(cancellationError());
        }
        catch (HttpRequestException)
        {
            return ProviderResult<Stream>.Failed(ProviderResponseSupport.Transport(providerId, model));
        }
        catch (IOException)
        {
            return ProviderResult<Stream>.Failed(ProviderResponseSupport.Transport(providerId, model));
        }
        catch (ObjectDisposedException)
        {
            return ProviderResult<Stream>.Failed(ProviderResponseSupport.Transport(providerId, model));
        }
        catch (Exception)
        {
            return ProviderResult<Stream>.Failed(new ProviderError("stream.open_failed", ProviderErrorCategory.InternalFailure, "Provider stream could not be opened.", false, providerId, model));
        }
    }

    internal static async ValueTask<ProviderReadStep<T>> MoveNextAsync<T>(IAsyncEnumerator<T> enumerator)
    {
        try
        {
            if (!await enumerator.MoveNextAsync().ConfigureAwait(false)) return new ProviderReadStep<T>(true, default, null);
            return new ProviderReadStep<T>(false, enumerator.Current, null);
        }
        catch (Exception exception)
        {
            return new ProviderReadStep<T>(false, default, exception);
        }
    }

    internal static ProviderError MapReadException(Exception exception, string providerId, string model, Func<ProviderError> cancellationError)
    {
        return exception switch
        {
            OperationCanceledException => cancellationError(),
            BoundedInputException bounded => ProviderResponseSupport.TooLarge(providerId, model, bounded.Code),
            DecoderFallbackException => ProviderResponseSupport.InvalidUtf8(providerId, model),
            JsonException => ProviderResponseSupport.Malformed(providerId, "response.stream_frame_invalid", "Provider stream frame is malformed.", model),
            HttpRequestException => ProviderResponseSupport.Transport(providerId, model),
            IOException => ProviderResponseSupport.Transport(providerId, model),
            ObjectDisposedException => ProviderResponseSupport.Transport(providerId, model),
            _ => new ProviderError("stream.read_failed", ProviderErrorCategory.InternalFailure, "Provider stream could not be read.", false, providerId, model)
        };
    }
}
