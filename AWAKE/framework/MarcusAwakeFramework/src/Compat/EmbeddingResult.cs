using System;
using System.Collections.Generic;

namespace MarcusAwakeFramework.Api;

public sealed class EmbeddingResult
{
	public IReadOnlyList<EmbeddingVector> Vectors { get; }

	public string ResolvedModel { get; }

	public EmbeddingResult(IReadOnlyList<EmbeddingVector> vectors, string resolvedModel)
	{
		Vectors = vectors ?? Array.Empty<EmbeddingVector>();
		ResolvedModel = resolvedModel ?? string.Empty;
	}
}
