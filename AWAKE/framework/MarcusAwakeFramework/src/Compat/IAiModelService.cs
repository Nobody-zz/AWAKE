using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeFramework.Api;

public interface IAiModelService
{
	Task<OperationResult<RouteCapabilityReport>> GetCapabilitiesAsync(string routeId, string taskKind, string cloudExportClassification, RequestContext context, CancellationToken cancellationToken);

	Task<OperationResult<EmbeddingResult>> EmbedAsync(EmbeddingRequest request, RequestContext context, CancellationToken cancellationToken);

	Task<OperationResult<RerankResult>> RerankAsync(RerankRequest request, RequestContext context, CancellationToken cancellationToken);
}
