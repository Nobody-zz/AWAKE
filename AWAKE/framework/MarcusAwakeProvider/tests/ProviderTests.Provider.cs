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
    internal static async Task OpenAiModelsAndCapabilityReportAsync()
    {
        var handler = new FakeHttpMessageHandler(_ => TestSupport.JsonResponse("{\"data\":[{\"id\":\"gpt-b\"},{\"id\":\"gpt-a\"},{\"id\":\"gpt-a\"}] }"));
        using var invoker = new HttpMessageInvoker(handler);
        var provider = new OpenAiCompatibleProvider(TestSupport.CreateProfile("openai-provider", ProviderKind.OpenAiCompatible), invoker);
        using var credential = new ApiKeyCredential("cred-main", "model-secret");

        var models = TestSupport.AssertSuccess(await provider.ListModelsAsync(credential, DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None).ConfigureAwait(false));
        var connection = TestSupport.AssertSuccess(await provider.TestConnectionAsync(credential, DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None).ConfigureAwait(false));

        TestSupport.Assert(models.Select(model => model.Id).SequenceEqual(new[] { "gpt-a", "gpt-b" }), "model IDs were not deduplicated and sorted");
        TestSupport.Assert(connection.Models.Count == 3, "connection test did not return discovered models");
        TestSupport.Assert(connection.Capabilities[ProviderCapabilityId.ModelDiscovery] == ProviderCapabilityState.Available, "model discovery was not marked available");
        TestSupport.Assert(connection.Capabilities[ProviderCapabilityId.Streaming] == ProviderCapabilityState.Unverified, "unprobed streaming was overstated");
        TestSupport.Assert(handler.Requests.All(request => request.Method == HttpMethod.Get && request.Path == "/v1/models"), "OpenAI model request path was wrong");
        TestSupport.Assert(handler.Requests.All(request => request.Authorization == "Bearer model-secret"), "OpenAI authorization header was wrong");
    }

    internal static async Task AnthropicModelsAndHeadersAsync()
    {
        var handler = new FakeHttpMessageHandler(_ => TestSupport.JsonResponse("{\"data\":[{\"id\":\"claude-test\"}]}"));
        using var invoker = new HttpMessageInvoker(handler);
        var provider = new AnthropicProvider(TestSupport.CreateProfile("anthropic-provider", ProviderKind.Anthropic), invoker);
        using var credential = new ApiKeyCredential("cred-main", "anthropic-secret");

        var models = TestSupport.AssertSuccess(await provider.ListModelsAsync(credential, DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None).ConfigureAwait(false));

        TestSupport.Assert(models.Single().Id == "claude-test", "Anthropic model ID was not parsed");
        TestSupport.Assert(handler.Requests.Single().Path == "/v1/models", "Anthropic model endpoint was wrong");
        TestSupport.Assert(handler.Requests.Single().ApiKey == "anthropic-secret", "Anthropic API key header was wrong");
        TestSupport.Assert(handler.Requests.Single().AnthropicVersion == "2023-06-01", "Anthropic version header was missing");
    }

    internal static async Task OllamaModelsWithoutAuthAsync()
    {
        var handler = new FakeHttpMessageHandler(_ => TestSupport.JsonResponse("{\"models\":[{\"name\":\"llama3:latest\"}]}"));
        using var invoker = new HttpMessageInvoker(handler);
        var profile = new ProviderConnectionProfile("ollama-provider", ProviderKind.Ollama, new Uri("http://127.0.0.1:11434/"), "llama3:latest");
        var provider = new OllamaProvider(profile, invoker);

        var models = TestSupport.AssertSuccess(await provider.ListModelsAsync(null, DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None).ConfigureAwait(false));

        TestSupport.Assert(models.Single().Id == "llama3:latest", "Ollama model ID was not parsed");
        TestSupport.Assert(handler.Requests.Single().Path == "/api/tags", "Ollama model endpoint was wrong");
        TestSupport.Assert(handler.Requests.Single().Authorization == null, "Ollama sent unexpected authorization");
    }

    internal static async Task TypedHttpFailuresAndRetryAfterAsync()
    {
        foreach (var status in new[] { HttpStatusCode.TooManyRequests, HttpStatusCode.InternalServerError, HttpStatusCode.BadGateway })
        {
            var handler = new FakeHttpMessageHandler(_ =>
            {
                var response = new HttpResponseMessage(status) { Content = new StringContent("secret response body") };
                if (status == HttpStatusCode.TooManyRequests) response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(7));
                return Task.FromResult(response);
            });
            using var invoker = new HttpMessageInvoker(handler);
            var provider = new OpenAiCompatibleProvider(TestSupport.CreateProfile("status-" + (int)status, ProviderKind.OpenAiCompatible), invoker);
            using var credential = new ApiKeyCredential("cred-main", "status-secret");

            var result = await provider.ListModelsAsync(credential, DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None).ConfigureAwait(false);
            if (status == HttpStatusCode.TooManyRequests)
            {
                TestSupport.AssertFailure(result, ProviderErrorCategory.RateLimited);
                TestSupport.Assert(result.Error!.Retryable, "429 was not retryable");
                TestSupport.Assert(result.Error.RetryAfter == TimeSpan.FromSeconds(7), "Retry-After was not preserved as bounded metadata");
            }
            else
            {
                TestSupport.AssertFailure(result, ProviderErrorCategory.ServerUnavailable);
                TestSupport.Assert(result.Error!.Retryable, "5xx was not retryable");
            }

            TestSupport.Assert(!result.Error!.ToString().Contains("secret response body", StringComparison.Ordinal), "HTTP body leaked into typed error");
        }
    }

    internal static async Task OpenAiCompletionAndStructuredOutputAsync()
    {
        var handler = new FakeHttpMessageHandler(async request =>
        {
            var body = await request.Content!.ReadAsStringAsync().ConfigureAwait(false);
            TestSupport.Assert(body.Contains("\"model\":\"test-model\"", StringComparison.Ordinal), "completion model missing");
            TestSupport.Assert(body.Contains("\"response_format\":{\"type\":\"json_object\"}", StringComparison.Ordinal), "structured schema was not sent unchanged");
            return TestSupport.JsonResponse("{\"id\":\"cmpl-1\",\"model\":\"test-model\",\"choices\":[{\"message\":{\"content\":\"{\\\"answer\\\":\\\"ok\\\"}\"}}],\"usage\":{\"prompt_tokens\":3,\"completion_tokens\":2}} ");
        });
        using var invoker = new HttpMessageInvoker(handler);
        var provider = new OpenAiCompatibleProvider(TestSupport.CreateProfile("completion-provider", ProviderKind.OpenAiCompatible), invoker);
        using var credential = new ApiKeyCredential("cred-main", "completion-secret");
        var request = new ProviderChatRequest(new[] { new ProviderMessage("user", "hello") }, responseSchemaJson: "{\"type\":\"json_object\"}");

        var result = TestSupport.AssertSuccess(await provider.CompleteAsync(request, credential, DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None).ConfigureAwait(false));

        TestSupport.Assert(result.Content == "{\"answer\":\"ok\"}", "completion text was not parsed");
        TestSupport.Assert(result.Usage!.InputTokens == 3 && result.Usage.OutputTokens == 2, "completion usage was not parsed");
        TestSupport.Assert(result.StructuredJson == result.Content, "structured JSON result was not exposed");
        TestSupport.Assert(handler.Requests.Single().Path == "/v1/chat/completions", "OpenAI completion endpoint was wrong");
    }

    internal static async Task StructuredResponseMustBeValidJsonAsync()
    {
        var handler = new FakeHttpMessageHandler(_ => TestSupport.JsonResponse("{\"choices\":[{\"message\":{\"content\":\"not-json\"}}]}"));
        using var invoker = new HttpMessageInvoker(handler);
        var provider = new OpenAiCompatibleProvider(TestSupport.CreateProfile("structured-provider", ProviderKind.OpenAiCompatible), invoker);
        using var credential = new ApiKeyCredential("cred-main", "structured-secret");
        var request = new ProviderChatRequest(new[] { new ProviderMessage("user", "hello") }, responseSchemaJson: "{\"type\":\"object\"}");

        var result = await provider.CompleteAsync(request, credential, DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None).ConfigureAwait(false);

        TestSupport.AssertFailure(result, ProviderErrorCategory.MalformedResponse);
        TestSupport.Assert(result.Error!.Code == "response.structured_json_invalid", "malformed structured response code was wrong");
    }
}
