using System;
using System.Collections.Generic;

namespace MarcusAwakeFramework.Api;

public sealed class TimelineImportInfo
{
	public string ExportId { get; }

	public string CampaignId { get; }

	public string SourceTimelineId { get; }

	public string ImportedTimelineId { get; }

	public IReadOnlyList<AssetIdRemap> AssetIdRemaps { get; }

	public TimelineImportInfo(string exportId, string campaignId, string sourceTimelineId, string importedTimelineId, IReadOnlyList<AssetIdRemap> assetIdRemaps)
	{
		ExportId = exportId ?? string.Empty;
		CampaignId = campaignId ?? string.Empty;
		SourceTimelineId = sourceTimelineId ?? string.Empty;
		ImportedTimelineId = importedTimelineId ?? string.Empty;
		AssetIdRemaps = assetIdRemaps ?? Array.Empty<AssetIdRemap>();
	}
}
