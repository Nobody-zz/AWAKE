using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeProvider;

/// <summary>
/// 出图这条路的候选过滤与兜底。
///
/// <para><b>它和文字那条路共用同一批候选，但过滤条件不同</b>：文字路由看谁实现了
/// <see cref="IProviderAdapter"/>（人人都是），出图路由看谁实现了
/// <see cref="IProviderImageAdapter"/>（只有真会生图的）。所以「候选里一个能生图的都没有」
/// 是正常结局，不是异常 —— 报 <c>media.image_adapter_unsupported</c>。</para>
/// </summary>
public sealed partial class ProviderRouter
{
    public Task<ProviderResult<ProviderBinaryResult>> GenerateImageAsync(
        ProviderImageRequest request,
        DateTimeOffset deadline,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return Task.FromResult(ProviderResult<ProviderBinaryResult>.Failed(new ProviderError(
                "media.image_request_invalid",
                ProviderErrorCategory.InvalidRequest,
                "An image request is required.",
                false,
                "router")));
        }

        return ExecuteImageAsync(request, deadline, cancellationToken);
    }

    /// <summary>候选里有没有能生图的 —— 便于上层在发请求之前就决定要不要走这条路。</summary>
    public bool CanGenerateImages => FindImageCandidates(null).Count > 0;

    private async Task<ProviderResult<ProviderBinaryResult>> ExecuteImageAsync(
        ProviderImageRequest request,
        DateTimeOffset deadline,
        CancellationToken cancellationToken)
    {
        var candidatesForImage = FindImageCandidates(request.Model);
        if (candidatesForImage.Count == 0)
        {
            return ProviderResult<ProviderBinaryResult>.Failed(new ProviderError(
                "media.image_adapter_unsupported",
                ProviderErrorCategory.Unsupported,
                "No provider route can generate images.",
                false,
                "router",
                details: new Dictionary<string, string>(StringComparer.Ordinal) { ["operation"] = "image.generate" }));
        }

        ProviderError? lastError = null;
        foreach (var candidate in candidatesForImage)
        {
            var providerId = candidate.Adapter.Profile.ProviderId;
            var model = ResolveCandidateModel(candidate, request.Model);
            var imageAdapter = (IProviderImageAdapter)candidate.Adapter;
            ProviderResult<ProviderBinaryResult> result;
            using (var preparedCandidate = PrepareCandidate(candidate))
            {
                try
                {
                    result = await imageAdapter.GenerateImageAsync(request, preparedCandidate.Candidate.Credential, deadline, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    var error = cancellationToken.IsCancellationRequested
                        ? new ProviderError("request.cancelled", ProviderErrorCategory.Cancelled, "Provider request was cancelled.", false, providerId, model)
                        : new ProviderError("request.deadline_expired", ProviderErrorCategory.Timeout, "Provider request ended during deadline cancellation.", false, providerId, model);
                    return ProviderResult<ProviderBinaryResult>.Failed(error);
                }
                catch (HttpRequestException)
                {
                    result = ProviderResult<ProviderBinaryResult>.Failed(new ProviderError("transport.unavailable", ProviderErrorCategory.TransportUnavailable, "Provider transport is unavailable.", true, providerId, model));
                }
                catch (Exception)
                {
                    result = ProviderResult<ProviderBinaryResult>.Failed(new ProviderError("router.adapter_failure", ProviderErrorCategory.InternalFailure, "Provider adapter failed unexpectedly.", false, providerId, model));
                }
            }

            if (result.IsSuccess) return result;
            lastError = result.Error ?? new ProviderError("router.provider_failure", ProviderErrorCategory.InternalFailure, "Provider operation failed without an error.", false, providerId, model);
            if (!lastError.Retryable) return ProviderResult<ProviderBinaryResult>.Failed(lastError);
        }

        return ProviderResult<ProviderBinaryResult>.Failed(lastError ?? NoRouteError());
    }

    private List<ProviderRouteCandidate> FindImageCandidates(string? requestedModel)
    {
        var capable = new List<ProviderRouteCandidate>();
        foreach (var candidate in BuildUniqueCandidates(requestedModel))
        {
            if (candidate.Adapter is IProviderImageAdapter) capable.Add(candidate);
        }

        return capable;
    }
}
