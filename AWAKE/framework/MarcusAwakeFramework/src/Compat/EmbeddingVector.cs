using System;
using System.Collections.Generic;

namespace MarcusAwakeFramework.Api;

public sealed class EmbeddingVector
{
	public int Index { get; }

	public IReadOnlyList<float> Values { get; }

	public EmbeddingVector(int index, IReadOnlyList<float> values)
	{
		Index = index;
		Values = values ?? Array.Empty<float>();
	}
}
