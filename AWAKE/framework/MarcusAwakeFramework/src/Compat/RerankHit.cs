namespace MarcusAwakeFramework.Api;

public sealed class RerankHit
{
	public string DocumentId { get; }

	public int OriginalIndex { get; }

	public double Score { get; }

	public RerankHit(string documentId, int originalIndex, double score)
	{
		DocumentId = documentId ?? string.Empty;
		OriginalIndex = originalIndex;
		Score = score;
	}
}
