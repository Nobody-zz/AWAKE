using MarcusAwakeProvider;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeProvider.Tests;

internal static partial class ProviderTests
{
    internal static async Task LocalAndCloudProfileContractAsync()
    {
        var localOpenAi = new ProviderConnectionProfile(
            "local-openai",
            ProviderKind.OpenAiCompatible,
            new Uri("http://127.0.0.1:43121/v1/"),
            "local-model",
            isCloud: false);
        TestSupport.Assert(!localOpenAi.IsCloud, "local profile was marked cloud");
        TestSupport.Assert(localOpenAi.CredentialReference == null, "local profile unexpectedly requires credentials");

        var localAnthropic = new ProviderConnectionProfile(
            "local-anthropic",
            ProviderKind.Anthropic,
            new Uri("https://localhost:43122/v1/"),
            "local-model",
            isCloud: false);
        TestSupport.Assert(!localAnthropic.IsCloud, "local Anthropic profile was marked cloud");

        var localIpv6 = new ProviderConnectionProfile(
            "local-ipv6",
            ProviderKind.OpenAiCompatible,
            new Uri("http://[::1]:43123/v1/"),
            "local-model",
            isCloud: false);
        TestSupport.Assert(!localIpv6.IsCloud, "IPv6 loopback profile was marked cloud");

        TestSupport.AssertThrows<ArgumentException>(() => new ProviderConnectionProfile(
            "local-external",
            ProviderKind.OpenAiCompatible,
            new Uri("https://example.test/v1/"),
            "model",
            isCloud: false));

        foreach (var kind in new[] { ProviderKind.OpenAiCompatible, ProviderKind.Anthropic, ProviderKind.Ollama })
        {
            TestSupport.AssertThrows<ArgumentException>(() => new ProviderConnectionProfile(
                "cloud-missing-" + kind,
                kind,
                new Uri("https://example.test/v1/"),
                "model",
                isCloud: true));
        }

        var cloudOllama = new ProviderConnectionProfile(
            "cloud-ollama",
            ProviderKind.Ollama,
            new Uri("https://ollama.example.test/"),
            "model",
            "credential-ollama",
            isCloud: true);
        TestSupport.Assert(cloudOllama.IsCloud, "explicit cloud Ollama profile was marked local");

        var handler = new FakeHttpMessageHandler(_ => TestSupport.JsonResponse("{\"data\":[]}"));
        using var invoker = new HttpMessageInvoker(handler);
        var localAdapter = ProviderAdapterFactory.Create(localOpenAi, invoker);
        var models = await localAdapter.ListModelsAsync(null, DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None).ConfigureAwait(false);
        TestSupport.AssertSuccess(models);
        TestSupport.Assert(handler.Requests.Count == 1, "local adapter did not perform the expected request");
        TestSupport.Assert(handler.Requests[0].Authorization == null, "local adapter sent an unexpected authorization header");
    }

    internal static Task ProviderKindCodecAndAdapterFactoryAsync()
    {
        TestSupport.Assert(ProviderKindCodec.TryParseWireName("openai_compatible", out var openAi) && openAi == ProviderKind.OpenAiCompatible, "OpenAI wire kind did not parse");
        TestSupport.Assert(ProviderKindCodec.TryParseWireName("anthropic", out var anthropic) && anthropic == ProviderKind.Anthropic, "Anthropic wire kind did not parse");
        TestSupport.Assert(ProviderKindCodec.TryParseWireName("ollama", out var ollama) && ollama == ProviderKind.Ollama, "Ollama wire kind did not parse");
        TestSupport.Assert(!ProviderKindCodec.TryParseWireName("OpenAiCompatible", out _), "non-contract wire kind was accepted");
        TestSupport.Assert(ProviderKindCodec.ToWireName(ProviderKind.OpenAiCompatible) == "openai_compatible", "OpenAI wire kind did not serialize");
        TestSupport.Assert(ProviderKindCodec.ToWireName(ProviderKind.Anthropic) == "anthropic", "Anthropic wire kind did not serialize");
        TestSupport.Assert(ProviderKindCodec.ToWireName(ProviderKind.Ollama) == "ollama", "Ollama wire kind did not serialize");

        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var invoker = new HttpMessageInvoker(handler);
        var profiles = new[]
        {
            new ProviderConnectionProfile("factory-openai", ProviderKind.OpenAiCompatible, new Uri("http://127.0.0.1:43124/v1/"), "model", isCloud: false),
            new ProviderConnectionProfile("factory-anthropic", ProviderKind.Anthropic, new Uri("http://localhost:43125/v1/"), "model", isCloud: false),
            new ProviderConnectionProfile("factory-ollama", ProviderKind.Ollama, new Uri("http://[::1]:43126/"), "model", isCloud: false)
        };

        TestSupport.Assert(ProviderAdapterFactory.Create(profiles[0], invoker) is OpenAiCompatibleProvider, "factory returned the wrong OpenAI adapter");
        TestSupport.Assert(ProviderAdapterFactory.Create(profiles[1], invoker) is AnthropicProvider, "factory returned the wrong Anthropic adapter");
        TestSupport.Assert(ProviderAdapterFactory.Create(profiles[2], invoker) is OllamaProvider, "factory returned the wrong Ollama adapter");
        return Task.CompletedTask;
    }
}
