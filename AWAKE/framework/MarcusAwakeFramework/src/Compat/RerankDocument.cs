namespace MarcusAwakeFramework.Api;

public sealed class RerankDocument
{
	public string DocumentId { get; }

	public string Text { get; }

	public RerankDocument(string documentId, string text)
	{
		DocumentId = documentId ?? string.Empty;
		Text = text ?? string.Empty;
	}
}
