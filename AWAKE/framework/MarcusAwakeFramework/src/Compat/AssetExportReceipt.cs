namespace MarcusAwakeFramework.Api;

public sealed class AssetExportReceipt
{
	public string ExportId { get; }

	public string AssetId { get; }

	public string PackageSha256 { get; }

	public long PackageByteLength { get; }

	public AssetExportReceipt(string exportId, string assetId, string packageSha256, long packageByteLength)
	{
		ExportId = exportId ?? string.Empty;
		AssetId = assetId ?? string.Empty;
		PackageSha256 = packageSha256 ?? string.Empty;
		PackageByteLength = packageByteLength;
	}
}
