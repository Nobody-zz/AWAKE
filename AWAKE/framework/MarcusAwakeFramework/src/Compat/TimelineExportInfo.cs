using System;

namespace MarcusAwakeFramework.Api;

public sealed class TimelineExportInfo
{
	public string ExportId { get; }

	public string CampaignId { get; }

	public string TimelineId { get; }

	public string BannerlordApi { get; }

	public string PackageSha256 { get; }

	public long PackageByteLength { get; }

	public DateTimeOffset CreatedUtc { get; }

	public TimelineExportInfo(string exportId, string campaignId, string timelineId, string bannerlordApi, string packageSha256, long packageByteLength, DateTimeOffset createdUtc)
	{
		ExportId = exportId ?? string.Empty;
		CampaignId = campaignId ?? string.Empty;
		TimelineId = timelineId ?? string.Empty;
		BannerlordApi = bannerlordApi ?? string.Empty;
		PackageSha256 = packageSha256 ?? string.Empty;
		PackageByteLength = packageByteLength;
		CreatedUtc = createdUtc;
	}
}
