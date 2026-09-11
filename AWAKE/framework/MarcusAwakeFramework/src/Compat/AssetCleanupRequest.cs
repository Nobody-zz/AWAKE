using System;
using System.Collections.Generic;

namespace MarcusAwakeFramework.Api;

public sealed class AssetCleanupRequest
{
	public DateTimeOffset OlderThanUtc { get; }

	public IReadOnlyList<string> RetentionClasses { get; }

	public int MaximumItems { get; }

	public bool DryRun { get; }

	public AssetCleanupRequest(DateTimeOffset olderThanUtc, IReadOnlyList<string> retentionClasses, int maximumItems, bool dryRun)
	{
		OlderThanUtc = olderThanUtc;
		RetentionClasses = retentionClasses ?? Array.Empty<string>();
		MaximumItems = maximumItems;
		DryRun = dryRun;
	}
}
