using System;

namespace MarcusAwakeFramework.Api;

public sealed class AssetImportRequest
{
	private readonly byte[] _content;

	public int ByteLength => _content.Length;

	public string MediaType { get; }

	public string LogicalKind { get; }

	public string CreatedByTask { get; }

	public string Provenance { get; }

	public string RetentionClass { get; }

	public AssetImportRequest(byte[] content, string mediaType, string logicalKind, string createdByTask, string provenance, string retentionClass)
	{
		if (content == null)
		{
			throw new ArgumentNullException("content");
		}
		_content = (byte[])content.Clone();
		MediaType = mediaType ?? string.Empty;
		LogicalKind = logicalKind ?? string.Empty;
		CreatedByTask = createdByTask ?? string.Empty;
		Provenance = provenance ?? string.Empty;
		RetentionClass = retentionClass ?? string.Empty;
	}

	public byte[] GetContentCopy()
	{
		return (byte[])_content.Clone();
	}
}
