using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeFramework.Api;

public interface IMediaService
{
	Task<OperationResult<GeneratedAssetResult>> GenerateImageAsync(ImageGenerationRequest request, RequestContext context, CancellationToken cancellationToken);

	Task<OperationResult<GeneratedAssetResult>> SynthesizeSpeechAsync(TtsGenerationRequest request, RequestContext context, CancellationToken cancellationToken);
}
