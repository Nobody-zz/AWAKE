using MarcusAwakeProvider;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeProvider.Tests;

internal static class TestSupport
{
    internal static ProviderConnectionProfile CreateProfile(string providerId, ProviderKind kind, string model = "test-model")
    {
        var baseUri = kind == ProviderKind.Ollama ? new Uri("http://127.0.0.1:11434/") : new Uri("https://example.test/v1/");
        var credentialReference = kind == ProviderKind.Ollama ? null : "cred-main";
        return new ProviderConnectionProfile(providerId, kind, baseUri, model, credentialReference);
    }

    internal static HttpResponseMessage JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    internal static HttpResponseMessage SseResponse(string body, HttpStatusCode statusCode = HttpStatusCode.OK, int chunkSize = 4096)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StreamContent(new ChunkedReadStream(Encoding.UTF8.GetBytes(body), chunkSize))
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
        return response;
    }

    internal static async Task<List<ProviderStreamEvent>> CollectAsync(IAsyncEnumerable<ProviderStreamEvent> events)
    {
        var collected = new List<ProviderStreamEvent>();
        await foreach (var evt in events.ConfigureAwait(false)) collected.Add(evt);
        return collected;
    }

    internal static T AssertSuccess<T>(ProviderResult<T> result)
    {
        if (!result.IsSuccess) throw new InvalidOperationException("expected success, got " + result.Error);
        return result.Value!;
    }

    internal static void AssertFailure<T>(ProviderResult<T> result, ProviderErrorCategory category)
    {
        if (result.IsSuccess) throw new InvalidOperationException("expected failure");
        Assert(result.Error!.Category == category, "expected " + category + ", got " + result.Error.Category);
    }

    internal static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal static void AssertThrows<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException("expected " + typeof(TException).Name);
    }
}

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder;

    internal FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : this((request, _) => Task.FromResult(responder(request)))
    {
    }

    internal FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        : this((request, _) => responder(request))
    {
    }

    internal FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        this.responder = responder;
    }

    internal int RequestCount { get; private set; }
    internal List<RequestSnapshot> Requests { get; } = new List<RequestSnapshot>();
    internal TaskCompletionSource<bool> RequestStarted { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        var body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        Requests.Add(new RequestSnapshot(
            request.Method,
            request.RequestUri!.PathAndQuery,
            body,
            request.Headers.TryGetValues("Authorization", out var authorization) ? authorization.SingleOrDefault() : null,
            request.Headers.TryGetValues("x-api-key", out var apiKey) ? apiKey.SingleOrDefault() : null,
            request.Headers.TryGetValues("anthropic-version", out var version) ? version.SingleOrDefault() : null));
        RequestStarted.TrySetResult(true);
        return await responder(request, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed record RequestSnapshot(HttpMethod Method, string Path, string Body, string? Authorization, string? ApiKey, string? AnthropicVersion);

internal sealed class ChunkedReadStream : Stream
{
    private readonly byte[] data;
    private readonly int chunkSize;
    private int position;

    internal ChunkedReadStream(byte[] data, int chunkSize)
    {
        this.data = data;
        this.chunkSize = Math.Max(1, chunkSize);
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => data.Length;
    public override long Position { get => position; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => ReadCore(buffer.AsSpan(offset, count));
    public override int Read(Span<byte> buffer) => ReadCore(buffer);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromResult(ReadCore(buffer.Span));
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Task.FromResult(ReadCore(buffer.AsSpan(offset, count)));
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private int ReadCore(Span<byte> buffer)
    {
        if (position >= data.Length) return 0;
        var count = Math.Min(Math.Min(buffer.Length, chunkSize), data.Length - position);
        data.AsSpan(position, count).CopyTo(buffer);
        position += count;
        return count;
    }
}
