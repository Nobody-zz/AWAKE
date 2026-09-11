using System.Threading;
using System.Threading.Tasks;

namespace MarcusAwakeFramework.Api;

public interface IAssetService
{
	Task<OperationResult<AssetHandle>> ImportAsync(AssetImportRequest request, RequestContext context, CancellationToken cancellationToken);

	Task<OperationResult<AssetMetadata>> GetMetadataAsync(string assetId, RequestContext context, CancellationToken cancellationToken);

	Task<OperationResult<AssetContent>> ReadAsync(string assetId, RequestContext context, CancellationToken cancellationToken);

	Task<OperationResult<bool>> SetPinnedAsync(string assetId, bool pinned, RequestContext context, CancellationToken cancellationToken);

	Task<OperationResult<AssetListPage>> ListAsync(int maximumResults, string cursor, RequestContext context, CancellationToken cancellationToken);

	Task<OperationResult<AssetExportReceipt>> ExportAsync(string assetId, RequestContext context, CancellationToken cancellationToken);

	Task<OperationResult<bool>> DeleteAsync(string assetId, RequestContext context, CancellationToken cancellationToken);

	Task<OperationResult<AssetCleanupSummary>> CleanupAsync(AssetCleanupRequest request, RequestContext context, CancellationToken cancellationToken);
}
