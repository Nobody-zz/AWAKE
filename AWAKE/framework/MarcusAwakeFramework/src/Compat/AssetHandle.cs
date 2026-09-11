using System;

namespace MarcusAwakeFramework.Api;

public sealed class AssetHandle
{
	public string AssetId { get; }

	public string ContentHash { get; }

	public string MediaType { get; }

	public long ByteLength { get; }

	public string LogicalKind { get; }

	public string CreatedByTask { get; }

	public ExtensionId OwnerExtensionId { get; }

	public string CampaignId { get; }

	public string TimelineId { get; }

	public string Provenance { get; }

	public string RetentionClass { get; }

	public AssetHandle(string assetId, string contentHash, string mediaType, long byteLength, string logicalKind, string createdByTask, ExtensionId ownerExtensionId, string campaignId, string timelineId, string provenance, string retentionClass)
	{
		if (string.IsNullOrWhiteSpace(assetId))
		{
			throw new ArgumentException("Asset ID is required.", "assetId");
		}
		if (string.IsNullOrWhiteSpace(contentHash))
		{
			throw new ArgumentException("Content hash is required.", "contentHash");
		}
		if (byteLength < 1)
		{
			throw new ArgumentOutOfRangeException("byteLength");
		}
		AssetId = assetId;
		ContentHash = contentHash;
		MediaType = mediaType ?? string.Empty;
		ByteLength = byteLength;
		LogicalKind = logicalKind ?? string.Empty;
		CreatedByTask = createdByTask ?? string.Empty;
		OwnerExtensionId = ownerExtensionId ?? throw new ArgumentNullException("ownerExtensionId");
		CampaignId = campaignId ?? string.Empty;
		TimelineId = timelineId ?? string.Empty;
		Provenance = provenance ?? string.Empty;
		RetentionClass = retentionClass ?? string.Empty;
	}
}
