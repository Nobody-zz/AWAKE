namespace MarcusAwakeFramework.Api;

public sealed class AssetCleanupSummary
{
	public int EligibleItems { get; }

	public int DeletedItems { get; }

	public int ReclaimedObjects { get; }

	public long ReclaimedBytes { get; }

	public AssetCleanupSummary(int eligibleItems, int deletedItems, int reclaimedObjects, long reclaimedBytes)
	{
		EligibleItems = eligibleItems;
		DeletedItems = deletedItems;
		ReclaimedObjects = reclaimedObjects;
		ReclaimedBytes = reclaimedBytes;
	}
}
