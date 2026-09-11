using MarcusAwakeProvider;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeProvider.Tests;

internal static partial class ProviderTests
{
    internal static async Task RouterFallsBackOnceAndSkipsDuplicatesAsync()
    {
        var firstHandler = new FakeHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var secondHandler = new FakeHttpMessageHandler(_ => TestSupport.JsonResponse("{\"choices\":[{\"message\":{\"content\":\"fallback\"}}]}"));
        using var firstInvoker = new HttpMessageInvoker(firstHandler);
        using var secondInvoker = new HttpMessageInvoker(secondHandler);
        var first = new OpenAiCompatibleProvider(TestSupport.CreateProfile("first-provider", ProviderKind.OpenAiCompatible, "first-model"), firstInvoker);
        var second = new OpenAiCompatibleProvider(TestSupport.CreateProfile("second-provider", ProviderKind.OpenAiCompatible, "second-model"), secondInvoker);
        using var firstCredential = new ApiKeyCredential("cred-first", "first-secret");
        using var secondCredential = new ApiKeyCredential("cred-second", "second-secret");
        var router = new ProviderRouter(new[]
        {
            new ProviderRouteCandidate(first, firstCredential),
            new ProviderRouteCandidate(first, firstCredential),
            new ProviderRouteCandidate(second, secondCredential),
            new ProviderRouteCandidate(second, secondCredential)
        });

        var result = await router.CompleteAsync(new ProviderChatRequest(new[] { new ProviderMessage("user", "hello") }), DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None).ConfigureAwait(false);

        TestSupport.AssertSuccess(result);
        TestSupport.Assert(result.Value!.Content == "fallback", "router did not return fallback result");
        TestSupport.Assert(firstHandler.RequestCount == 1 && secondHandler.RequestCount == 1, "router retried or duplicated a candidate");
    }

    internal static async Task RouterStreamFallbackIsOnlyBeforeVisibleOutputAsync()
    {
        var preOutputFirstHandler = new FakeHttpMessageHandler(_ => TestSupport.SseResponse("data: {\"error\":\"upstream\"}\n\n", HttpStatusCode.ServiceUnavailable));
        var preOutputSecondHandler = new FakeHttpMessageHandler(_ => TestSupport.SseResponse("data: {\"choices\":[{\"delta\":{\"content\":\"fallback\"}}]}\n\ndata: [DONE]\n\n"));
        using var firstInvoker = new HttpMessageInvoker(preOutputFirstHandler);
        using var secondInvoker = new HttpMessageInvoker(preOutputSecondHandler);
        var first = new OpenAiCompatibleProvider(TestSupport.CreateProfile("stream-first", ProviderKind.OpenAiCompatible), firstInvoker);
        var second = new OpenAiCompatibleProvider(TestSupport.CreateProfile("stream-second", ProviderKind.OpenAiCompatible), secondInvoker);
        using var firstCredential = new ApiKeyCredential("cred-first", "first-secret");
        using var secondCredential = new ApiKeyCredential("cred-second", "second-secret");
        var router = new ProviderRouter(new[]
        {
            new ProviderRouteCandidate(first, firstCredential),
            new ProviderRouteCandidate(second, secondCredential)
        });
        var request = new ProviderChatRequest(new[] { new ProviderMessage("user", "hello") });

        var preOutputEvents = await TestSupport.CollectAsync(router.StreamAsync(request, DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None)).ConfigureAwait(false);

        TestSupport.Assert(preOutputEvents.Any(evt => evt.Kind == ProviderStreamEventKind.RouteChanged), "pre-output fallback did not emit RouteChanged");
        var routeChanged = preOutputEvents.Single(evt => evt.Kind == ProviderStreamEventKind.RouteChanged);
        TestSupport.Assert(routeChanged.FromProviderId == "stream-first" && routeChanged.ToProviderId == "stream-second", "route change metadata was wrong");
        TestSupport.Assert(preOutputEvents.Any(evt => evt.Kind == ProviderStreamEventKind.TextDelta && evt.Text == "fallback"), "pre-output fallback lost visible text");
        TestSupport.Assert(preOutputEvents.Last().Kind == ProviderStreamEventKind.Completed && preOutputEvents.Last().Text.Length == 0, "pre-output fallback did not use the canonical terminal shape");

        var postOutputFirstHandler = new FakeHttpMessageHandler(_ => TestSupport.SseResponse("data: {\"choices\":[{\"delta\":{\"content\":\"partial\"}}]}\n\ndata: {bad}\n\n"));
        var postOutputSecondHandler = new FakeHttpMessageHandler(_ => TestSupport.SseResponse("data: {\"choices\":[{\"delta\":{\"content\":\"wrong\"}}]}\n\ndata: [DONE]\n\n"));
        using var postFirstInvoker = new HttpMessageInvoker(postOutputFirstHandler);
        using var postSecondInvoker = new HttpMessageInvoker(postOutputSecondHandler);
        var postFirst = new OpenAiCompatibleProvider(TestSupport.CreateProfile("post-first", ProviderKind.OpenAiCompatible), postFirstInvoker);
        var postSecond = new OpenAiCompatibleProvider(TestSupport.CreateProfile("post-second", ProviderKind.OpenAiCompatible), postSecondInvoker);
        using var postFirstCredential = new ApiKeyCredential("cred-post-first", "post-first-secret");
        using var postSecondCredential = new ApiKeyCredential("cred-post-second", "post-second-secret");
        var postRouter = new ProviderRouter(new[]
        {
            new ProviderRouteCandidate(postFirst, postFirstCredential),
            new ProviderRouteCandidate(postSecond, postSecondCredential)
        });

        var postOutputEvents = await TestSupport.CollectAsync(postRouter.StreamAsync(request, DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None)).ConfigureAwait(false);

        TestSupport.Assert(postOutputEvents.Last().Kind == ProviderStreamEventKind.Failed, "post-output failure was not terminal");
        TestSupport.Assert(!postOutputEvents.Any(evt => evt.Kind == ProviderStreamEventKind.RouteChanged), "post-output failure emitted fallback route change");
        TestSupport.Assert(postOutputSecondHandler.RequestCount == 0, "post-output failure started a second provider");
    }

    internal static async Task RedirectIsRejectedAndNeverFollowedAsync()
    {
        var handler = new FakeHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TemporaryRedirect);
            response.Headers.Location = new Uri("https://attacker.example.test/steal");
            return Task.FromResult(response);
        });
        using var invoker = new HttpMessageInvoker(handler);
        var provider = new OpenAiCompatibleProvider(TestSupport.CreateProfile("redirect-provider", ProviderKind.OpenAiCompatible), invoker);
        using var credential = new ApiKeyCredential("cred-main", "redirect-secret");

        var result = await provider.ListModelsAsync(credential, DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None).ConfigureAwait(false);

        TestSupport.AssertFailure(result, ProviderErrorCategory.RedirectRejected);
        TestSupport.Assert(handler.RequestCount == 1, "redirect response caused an unexpected follow-up request");
        TestSupport.Assert(!result.Error!.ToString().Contains("attacker.example.test", StringComparison.Ordinal), "redirect target leaked into error");
    }
}
