using System;
using System.Collections.Generic;

namespace MarcusAwakeFramework.Api;

public sealed class ProviderCapabilityInfo
{
	public string ModelId { get; }

	public string Adapter { get; }

	public ProviderCapabilityStatus Status { get; }

	public IReadOnlyList<string> Modalities { get; }

	public bool Streaming { get; }

	public bool StructuredOutput { get; }

	public IReadOnlyList<int> EmbeddingDimensions { get; }

	public bool Rerank { get; }

	public IReadOnlyList<string> ImageFormats { get; }

	public IReadOnlyList<string> TtsFormats { get; }

	public string CancellationSemantics { get; }

	public string SafeDetail { get; }

	public ProviderCapabilityInfo(string modelId, string adapter, ProviderCapabilityStatus status, IReadOnlyList<string> modalities, bool streaming, bool structuredOutput, IReadOnlyList<int> embeddingDimensions, bool rerank, IReadOnlyList<string> imageFormats, IReadOnlyList<string> ttsFormats, string cancellationSemantics, string safeDetail)
	{
		ModelId = modelId ?? string.Empty;
		Adapter = adapter ?? string.Empty;
		Status = status;
		Modalities = modalities ?? Array.Empty<string>();
		Streaming = streaming;
		StructuredOutput = structuredOutput;
		EmbeddingDimensions = embeddingDimensions ?? Array.Empty<int>();
		Rerank = rerank;
		ImageFormats = imageFormats ?? Array.Empty<string>();
		TtsFormats = ttsFormats ?? Array.Empty<string>();
		CancellationSemantics = cancellationSemantics ?? string.Empty;
		SafeDetail = safeDetail ?? string.Empty;
	}
}
