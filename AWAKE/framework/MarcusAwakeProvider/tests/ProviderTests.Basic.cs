using MarcusAwakeProvider;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeProvider.Tests;

internal static partial class ProviderTests
{
    internal static Task ProfileValidationRejectsUnsafeInputsWithoutHttpAsync()
    {
        TestSupport.AssertThrows<ArgumentException>(() => new ProviderConnectionProfile("bad", ProviderKind.OpenAiCompatible, new Uri("ftp://example.test/v1/"), "model", "cred"));
        TestSupport.AssertThrows<ArgumentException>(() => new ProviderConnectionProfile("bad", ProviderKind.OpenAiCompatible, new Uri("https://example.test/v1/?key=secret"), "model", "cred"));
        TestSupport.AssertThrows<ArgumentException>(() => new ProviderConnectionProfile("bad", ProviderKind.OpenAiCompatible, new Uri("https://example.test/v1/"), "", "cred"));
        TestSupport.AssertThrows<ArgumentException>(() => new ProviderConnectionProfile("bad", ProviderKind.OpenAiCompatible, new Uri("https://example.test/v1/"), "model", ""));
        return Task.CompletedTask;
    }

    internal static async Task PolicyRejectionHappensBeforeHttpAsync()
    {
        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var invoker = new HttpMessageInvoker(handler);
        var profile = TestSupport.CreateProfile("policy-provider", ProviderKind.OpenAiCompatible);
        var policy = new DelegateProviderEndpointPolicy((_, _, _) => ProviderResult<bool>.Failed(new ProviderError(
            "endpoint.policy_denied",
            ProviderErrorCategory.PolicyDenied,
            "Endpoint denied by policy.",
            false,
            profile.ProviderId)));
        var provider = new OpenAiCompatibleProvider(profile, invoker, policy);
        using var credential = new ApiKeyCredential("cred-main", "sentinel-key");

        var result = await provider.ListModelsAsync(credential, DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None).ConfigureAwait(false);

        TestSupport.AssertFailure(result, ProviderErrorCategory.PolicyDenied);
        TestSupport.Assert(handler.RequestCount == 0, "policy rejection sent an HTTP request");
    }

    internal static async Task CredentialRedactionAndDisposalAsync()
    {
        const string secret = "sentinel-provider-key";
        using var credential = new ApiKeyCredential("cred-main", secret);
        TestSupport.Assert(!credential.ToString().Contains(secret, StringComparison.Ordinal), "credential ToString leaked the key");
        TestSupport.Assert(credential.Reference == "cred-main", "credential reference changed");

        credential.Dispose();
        TestSupport.AssertThrows<ObjectDisposedException>(() => credential.GetSecretForTest());

        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(secret)
        }));
        using var invoker = new HttpMessageInvoker(handler);
        var provider = new OpenAiCompatibleProvider(TestSupport.CreateProfile("redaction-provider", ProviderKind.OpenAiCompatible), invoker);
        using var liveCredential = new ApiKeyCredential("cred-main", secret);
        var result = await provider.ListModelsAsync(liveCredential, DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None).ConfigureAwait(false);
        var diagnostic = result.Error?.ToString() ?? string.Empty;

        TestSupport.AssertFailure(result, ProviderErrorCategory.Authentication);
        TestSupport.Assert(!diagnostic.Contains(secret, StringComparison.Ordinal), "provider error leaked the key");
        TestSupport.Assert(!diagnostic.Contains("Authorization", StringComparison.OrdinalIgnoreCase), "provider error leaked an authorization header");
        TestSupport.Assert(handler.Requests.Single().Authorization == "Bearer " + secret, "request did not authenticate with the key");
    }

    internal static async Task InMemoryCredentialStoreRoundTripAsync()
    {
        var store = new InMemoryCredentialStore();
        using var source = new ApiKeyCredential("cred-memory", "memory-secret");
        TestSupport.AssertSuccess(await store.SaveAsync(source, CancellationToken.None).ConfigureAwait(false));
        using var loaded = TestSupport.AssertSuccess(await store.GetAsync("cred-memory", CancellationToken.None).ConfigureAwait(false));
        TestSupport.Assert(loaded.Reference == source.Reference, "stored reference changed");
        TestSupport.Assert(loaded.GetSecretForTest() == "memory-secret", "stored key changed");
        source.Dispose();
        TestSupport.Assert(loaded.GetSecretForTest() == "memory-secret", "store returned an aliased credential");
    }

    internal static async Task ProtectedFileStoreRoundTripAndCorruptionAsync()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "credential-store-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new ProtectedFileCredentialStore(root, CredentialProtectionMode.AesGcm);
            using var source = new ApiKeyCredential("cred-file", "file-secret");
            TestSupport.AssertSuccess(await store.SaveAsync(source, CancellationToken.None).ConfigureAwait(false));
            var files = Directory.GetFiles(root, "*.credential");
            TestSupport.Assert(files.Length == 1, "credential store did not create one record");
            TestSupport.Assert(!File.ReadAllText(files[0]).Contains("file-secret", StringComparison.Ordinal), "credential file contains plaintext key");

            using var loaded = TestSupport.AssertSuccess(await store.GetAsync("cred-file", CancellationToken.None).ConfigureAwait(false));
            TestSupport.Assert(loaded.GetSecretForTest() == "file-secret", "file credential did not round-trip");
            TestSupport.Assert(store.ProtectionStatus == CredentialProtectionStatus.DegradedAesGcm, "AES-GCM fallback was not marked degraded");
            TestSupport.Assert(!string.IsNullOrWhiteSpace(store.Warning), "degraded credential warning is missing");

            File.WriteAllText(files[0], "corrupt");
            var corrupt = await store.GetAsync("cred-file", CancellationToken.None).ConfigureAwait(false);
            TestSupport.AssertFailure(corrupt, ProviderErrorCategory.CorruptCredential);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
