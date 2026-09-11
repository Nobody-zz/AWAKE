using MarcusAwakeProvider;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeProvider.Tests;

internal static partial class ProviderTests
{
    internal static async Task BoundedResponseRejectsContentLengthAndCompressionAsync()
    {
        var smallLimits = new ProviderLimits(maxResponseBytes: 32, maxStreamBytes: 64);
        var oversizedHandler = new FakeHttpMessageHandler(_ =>
        {
            var response = TestSupport.JsonResponse("{\"data\":[{\"id\":\"this-is-too-large\"}]}" );
            response.Content.Headers.ContentLength = 1024;
            return Task.FromResult(response);
        });
        using var oversizedInvoker = new HttpMessageInvoker(oversizedHandler);
        var oversizedProvider = new OpenAiCompatibleProvider(TestSupport.CreateProfile("limit-provider", ProviderKind.OpenAiCompatible), oversizedInvoker, limits: smallLimits);
        using var credential = new ApiKeyCredential("cred-main", "limit-secret");
        var oversized = await oversizedProvider.ListModelsAsync(credential, DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None).ConfigureAwait(false);
        TestSupport.AssertFailure(oversized, ProviderErrorCategory.ResourceExhausted);
        TestSupport.Assert(oversizedHandler.RequestCount == 1, "oversized response did not make the single expected request");

        var compressedHandler = new FakeHttpMessageHandler(_ =>
        {
            var response = TestSupport.JsonResponse("{\"data\":[]}");
            response.Content.Headers.ContentEncoding.Add("gzip");
            return Task.FromResult(response);
        });
        using var compressedInvoker = new HttpMessageInvoker(compressedHandler);
        var compressedProvider = new OpenAiCompatibleProvider(TestSupport.CreateProfile("compressed-provider", ProviderKind.OpenAiCompatible), compressedInvoker, limits: smallLimits);
        var compressed = await compressedProvider.ListModelsAsync(credential, DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None).ConfigureAwait(false);
        TestSupport.AssertFailure(compressed, ProviderErrorCategory.Unsupported);
    }

    internal static async Task OpenAiSseOrdersDeltasUsageAndCompletionAsync()
    {
        var payload = "event: ignored\ndata: {\"choices\":[{\"delta\":{\"content\":\"你\"}}]}\n\ndata: {\"choices\":[{\"delta\":{\"content\":\"好\"}}]}\n\ndata: {\"usage\":{\"prompt_tokens\":4,\"completion_tokens\":2}}\n\ndata: [DONE]\n\n";
        var handler = new FakeHttpMessageHandler(_ => TestSupport.SseResponse(payload, chunkSize: 2));
        using var invoker = new HttpMessageInvoker(handler);
        var provider = new OpenAiCompatibleProvider(TestSupport.CreateProfile("stream-provider", ProviderKind.OpenAiCompatible), invoker);
        using var credential = new ApiKeyCredential("cred-main", "stream-secret");
        var request = new ProviderChatRequest(new[] { new ProviderMessage("user", "hello") });

        var events = await TestSupport.CollectAsync(provider.StreamAsync(request, credential, DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None)).ConfigureAwait(false);

        TestSupport.Assert(events.Select(evt => evt.Kind).SequenceEqual(new[]
        {
            ProviderStreamEventKind.Started,
            ProviderStreamEventKind.TextDelta,
            ProviderStreamEventKind.TextDelta,
            ProviderStreamEventKind.UsageUpdate,
            ProviderStreamEventKind.Completed
        }), "SSE event ordering was wrong");
        TestSupport.Assert(events.Where(evt => evt.Kind == ProviderStreamEventKind.TextDelta).Select(evt => evt.Text).SequenceEqual(new[] { "你", "好" }), "SSE deltas were wrong");
        TestSupport.Assert(events.Last().Text == "你好", "completed stream text was not aggregated");
        TestSupport.Assert(events.Last().Usage!.OutputTokens == 2, "stream usage was not preserved");
        TestSupport.Assert(events.Select(evt => evt.Sequence).SequenceEqual(Enumerable.Range(1, events.Count).Select(value => (long)value)), "stream sequence was not monotonic");
    }

    internal static async Task SseRequiresDoneAndRejectsMalformedFramesAsync()
    {
        var noDoneHandler = new FakeHttpMessageHandler(_ => TestSupport.SseResponse("data: {\"choices\":[{\"delta\":{\"content\":\"partial\"}}]}\n\n"));
        using var noDoneInvoker = new HttpMessageInvoker(noDoneHandler);
        var noDoneProvider = new OpenAiCompatibleProvider(TestSupport.CreateProfile("no-done-provider", ProviderKind.OpenAiCompatible), noDoneInvoker);
        using var credential = new ApiKeyCredential("cred-main", "sse-secret");
        var request = new ProviderChatRequest(new[] { new ProviderMessage("user", "hello") });
        var noDoneEvents = await TestSupport.CollectAsync(noDoneProvider.StreamAsync(request, credential, DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None)).ConfigureAwait(false);
        TestSupport.Assert(noDoneEvents.Last().Kind == ProviderStreamEventKind.Failed, "EOF without DONE completed successfully");
        TestSupport.Assert(noDoneEvents.Last().Error!.Category == ProviderErrorCategory.IncompleteStream, "EOF without DONE had the wrong category");

        var malformedHandler = new FakeHttpMessageHandler(_ => TestSupport.SseResponse("data: {not-json}\n\ndata: [DONE]\n\n"));
        using var malformedInvoker = new HttpMessageInvoker(malformedHandler);
        var malformedProvider = new OpenAiCompatibleProvider(TestSupport.CreateProfile("malformed-provider", ProviderKind.OpenAiCompatible), malformedInvoker);
        var malformedEvents = await TestSupport.CollectAsync(malformedProvider.StreamAsync(request, credential, DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None)).ConfigureAwait(false);
        TestSupport.Assert(malformedEvents.Last().Kind == ProviderStreamEventKind.Failed, "malformed SSE frame did not fail");
        TestSupport.Assert(malformedEvents.Last().Error!.Category == ProviderErrorCategory.MalformedResponse, "malformed SSE frame had the wrong category");
    }

    internal static async Task CancellationAndDeadlineAreTypedAsync()
    {
        var handler = new FakeHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var invoker = new HttpMessageInvoker(handler);
        var provider = new OpenAiCompatibleProvider(TestSupport.CreateProfile("cancel-provider", ProviderKind.OpenAiCompatible), invoker);
        using var credential = new ApiKeyCredential("cred-main", "cancel-secret");
        using var callerCancellation = new CancellationTokenSource();
        var cancellationTask = provider.ListModelsAsync(credential, DateTimeOffset.UtcNow.AddMinutes(1), callerCancellation.Token);
        await handler.RequestStarted.Task.ConfigureAwait(false);
        callerCancellation.Cancel();
        var cancelled = await cancellationTask.ConfigureAwait(false);
        TestSupport.AssertFailure(cancelled, ProviderErrorCategory.Cancelled);

        var deadlineHandler = new FakeHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var deadlineInvoker = new HttpMessageInvoker(deadlineHandler);
        var deadlineProvider = new OpenAiCompatibleProvider(TestSupport.CreateProfile("deadline-provider", ProviderKind.OpenAiCompatible), deadlineInvoker);
        var deadline = await deadlineProvider.ListModelsAsync(credential, DateTimeOffset.UtcNow.AddMilliseconds(-1), CancellationToken.None).ConfigureAwait(false);
        TestSupport.AssertFailure(deadline, ProviderErrorCategory.Timeout);
        TestSupport.Assert(deadlineHandler.RequestCount == 0, "expired deadline still sent an HTTP request");
    }
}
