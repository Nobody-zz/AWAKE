using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace MarcusAwakeProvider;

/// <summary>
/// Player2 适配器。**它只会生图，不会聊天** —— 所以文字那三个方法如实报「不支持」，
/// 不给一个假实现去骗过类型系统。
///
/// <para><b>鉴权不在这里判。</b> 云端带 Bearer、本机不上头，这件事由
/// <see cref="ProviderConnectionProfile.IsCloud"/> 决定：本机 profile 的
/// <c>CredentialReference</c> 为空 ⇒ 压根没有凭据 ⇒ 自然不带 Authorization。
/// 本机服务带了反而可能被拒，所以不能无条件加上去。</para>
/// </summary>
public sealed class Player2Provider : ProviderAdapterBase, IProviderImageAdapter
{
    private readonly int maxImageAssetBytes;
    private readonly int maxImageRequestBytes;
    private readonly int maxImageJsonBytes;

    public Player2Provider(
        ProviderConnectionProfile profile,
        HttpMessageInvoker invoker,
        IProviderEndpointPolicy? endpointPolicy = null,
        ProviderLimits? limits = null,
        IProviderClock? clock = null,
        int maxImageAssetBytes = ProviderImageMedia.DefaultMaxAssetBytes)
        : base(profile, invoker, endpointPolicy, limits, clock)
    {
        if (profile.Kind != ProviderKind.Player2) throw new ArgumentException("Profile kind must be Player2.", nameof(profile));
        if (maxImageAssetBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxImageAssetBytes));
        this.maxImageAssetBytes = maxImageAssetBytes;

        // 参考图在请求体里是 base64（放大 4/3），响应体里装的是 base64 的图。两侧都要留够。
        maxImageRequestBytes = Bounded(maxImageAssetBytes * 2L);
        maxImageJsonBytes = Bounded(maxImageAssetBytes + maxImageAssetBytes / 2L + 4096L);
    }

    /// <summary>便于测试与上层判断单次能收多大的货。</summary>
    public int MaxImageAssetBytes => maxImageAssetBytes;

    public override Task<ProviderResult<IReadOnlyList<ProviderModel>>> ListModelsAsync(ApiKeyCredential? credential, DateTimeOffset deadline, CancellationToken cancellationToken = default)
    {
        var credentialError = ValidateCredential(credential);
        return Task.FromResult(credentialError != null
            ? ProviderResult<IReadOnlyList<ProviderModel>>.Failed(credentialError)
            : ProviderResult<IReadOnlyList<ProviderModel>>.Succeeded(SyntheticModels()));
    }

    /// <summary>Player2 没有 /models 端点，连通性检查不该去猜一个出来 ⇒ 直接用合成的模型名，不发请求。</summary>
    protected override Task<ProviderResult<IReadOnlyList<ProviderModel>>> ListModelsForConnectionAsync(ApiKeyCredential? credential, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        return ListModelsAsync(credential, deadline, cancellationToken);
    }

    public override Task<ProviderResult<ProviderCompletion>> CompleteAsync(ProviderChatRequest request, ApiKeyCredential? credential, DateTimeOffset deadline, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ProviderResult<ProviderCompletion>.Failed(Unsupported("completion", Profile.DefaultModel)));
    }

    public override async IAsyncEnumerable<ProviderStreamEvent> StreamAsync(
        ProviderChatRequest request,
        ApiKeyCredential? credential,
        DateTimeOffset deadline,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return ProviderStreamingSupport.Terminal(Profile.ProviderId, Profile.DefaultModel, Unsupported("stream", Profile.DefaultModel), 1);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task<ProviderResult<ProviderBinaryResult>> GenerateImageAsync(
        ProviderImageRequest request,
        ApiKeyCredential? credential,
        DateTimeOffset deadline,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return ProviderResult<ProviderBinaryResult>.Failed(new ProviderError("media.image_request_invalid", ProviderErrorCategory.InvalidRequest, "An image request is required.", false, Profile.ProviderId));
        }

        var credentialError = ValidateCredential(credential);
        if (credentialError != null) return ProviderResult<ProviderBinaryResult>.Failed(credentialError);

        var model = string.IsNullOrWhiteSpace(request.Model) ? Profile.DefaultModel : request.Model!;
        var body = ImageWireShape.BuildPlayer2Body(request);
        if (body.Length > maxImageRequestBytes)
        {
            return ProviderResult<ProviderBinaryResult>.Failed(new ProviderError(
                "media.image_request_too_large",
                ProviderErrorCategory.ResourceExhausted,
                "The image request exceeds the configured limit.",
                false,
                Profile.ProviderId,
                model,
                details: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["request_bytes"] = body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["max_request_bytes"] = maxImageRequestBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)
                }));
        }

        var path = request.HasReference ? ImageWireShape.Player2EditPath : ImageWireShape.Player2GeneratePath;
        var headers = request.IdempotencyKey.Length == 0
            ? null
            : new Dictionary<string, string>(StringComparer.Ordinal) { ["Idempotency-Key"] = request.IdempotencyKey };

        var responseResult = await SendWithBodyAsync(HttpMethod.Post, path, body, credential, "image.generate", deadline, cancellationToken, "application/json", model, headers).ConfigureAwait(false);
        if (!responseResult.IsSuccess) return ProviderResult<ProviderBinaryResult>.Failed(responseResult.Error!);
        using var response = responseResult.Value!;

        var bodyResult = await ReadBoundedResponseAsync(response, "image.generate", model, cancellationToken, deadline, maxImageJsonBytes).ConfigureAwait(false);
        if (!bodyResult.IsSuccess) return ProviderResult<ProviderBinaryResult>.Failed(bodyResult.Error!);

        var documentResult = ParseJson(bodyResult.Value!, "image.generate", model);
        if (!documentResult.IsSuccess) return ProviderResult<ProviderBinaryResult>.Failed(documentResult.Error!);
        using var document = documentResult.Value!;

        var payloadResult = ImageWireShape.ReadPlayer2Payload(document.RootElement, Profile.ProviderId, model);
        if (!payloadResult.IsSuccess) return ProviderResult<ProviderBinaryResult>.Failed(payloadResult.Error!);

        return ImageWireShape.DecodeAsset(payloadResult.Value, Profile.ProviderId, model, maxImageAssetBytes);
    }

    private static int Bounded(long value)
    {
        return value > int.MaxValue ? int.MaxValue : (int)value;
    }

    private ProviderModel[] SyntheticModels()
    {
        // Player2 不走「先发现模型、再选模型」那套，它一个固定名字。合成一条，让上游的
        // 候选/连接检查有个东西可用，同时**不假装发现了什么**。
        return new[] { new ProviderModel(Profile.DefaultModel, "Player2") };
    }

    private ProviderError Unsupported(string operation, string model)
    {
        return new ProviderError(
            "provider.operation_unsupported",
            ProviderErrorCategory.Unsupported,
            "The Player2 adapter does not provide chat completions.",
            false,
            Profile.ProviderId,
            model,
            details: new Dictionary<string, string>(StringComparer.Ordinal) { ["operation"] = operation });
    }
}
