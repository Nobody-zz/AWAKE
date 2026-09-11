namespace MarcusAwakeFramework.Api;

public sealed class AssetIdRemap
{
	public string SourceAssetId { get; }

	public string ImportedAssetId { get; }

	public AssetIdRemap(string sourceAssetId, string importedAssetId)
	{
		SourceAssetId = sourceAssetId ?? string.Empty;
		ImportedAssetId = importedAssetId ?? string.Empty;
	}
}
