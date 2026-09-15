using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeProvider;

/// <summary>
/// OpenAI 兼容这条生图路（<c>POST {base}/images/generations</c>）。
///
/// <para>它和文字那侧共用一个 <see cref="OpenAiCompatibleProvider"/>，因为
/// base 地址、凭据、端点策略完全是同一套 —— 拆成两个类只会让 profile 有两份而互相漂移。</para>
/// </summary>
public sealed partial class OpenAiCompatibleProvider : IProviderImageAdapter
{
    private int maxImageAssetBytes = ProviderImageMedia.DefaultMaxAssetBytes;

    /// <summary>给生图用的一档大小限制。文字那三个方法的限制不受影响。</summary>
    public OpenAiCompatibleProvider(
        ProviderConnectionProfile profile,
        HttpMessageInvoker invoker,
        IProviderEndpointPolicy? endpointPolicy,
        ProviderLimits? limits,
        IProviderClock? clock,
        int maxImageAssetBytes)
        : base(profile, invoker, endpointPolicy, limits, clock)
    {
        if (profile.Kind != ProviderKind.OpenAiCompatible) throw new ArgumentException("Profile kind must be OpenAiCompatible.", nameof(profile));
        if (maxImageAssetBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxImageAssetBytes));
        this.maxImageAssetBytes = maxImageAssetBytes;
    }

    public int MaxImageAssetBytes => maxImageAssetBytes;

    private int MaxImageRequestBytes => Bounded(maxImageAssetBytes * 2L);

    private int MaxImageJsonBytes => Bounded(maxImageAssetBytes + maxImageAssetBytes / 2L + 4096L);

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
        var body = ImageWireShape.BuildOpenAiBody(request, model);
        if (body.Length > MaxImageRequestBytes)
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
                    ["max_request_bytes"] = MaxImageRequestBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)
                }));
        }

        var headers = request.IdempotencyKey.Length == 0
            ? null
            : new Dictionary<string, string>(StringComparer.Ordinal) { ["Idempotency-Key"] = request.IdempotencyKey };

        var responseResult = await SendWithBodyAsync(HttpMethod.Post, ImageWireShape.OpenAiImagesPath, body, credential, "image.generate", deadline, cancellationToken, "application/json", model, headers).ConfigureAwait(false);
        if (!responseResult.IsSuccess) return ProviderResult<ProviderBinaryResult>.Failed(responseResult.Error!);
        using var response = responseResult.Value!;

        var bodyResult = await ReadBoundedResponseAsync(response, "image.generate", model, cancellationToken, deadline, MaxImageJsonBytes).ConfigureAwait(false);
        if (!bodyResult.IsSuccess) return ProviderResult<ProviderBinaryResult>.Failed(bodyResult.Error!);

        var documentResult = ParseJson(bodyResult.Value!, "image.generate", model);
        if (!documentResult.IsSuccess) return ProviderResult<ProviderBinaryResult>.Failed(documentResult.Error!);
        using var document = documentResult.Value!;

        var payloadResult = ImageWireShape.ReadOpenAiPayload(document.RootElement, Profile.ProviderId, model);
        if (!payloadResult.IsSuccess) return ProviderResult<ProviderBinaryResult>.Failed(payloadResult.Error!);

        return ImageWireShape.DecodeAsset(payloadResult.Value, Profile.ProviderId, model, maxImageAssetBytes);
    }

    private static int Bounded(long value)
    {
        return value > int.MaxValue ? int.MaxValue : (int)value;
    }
}
