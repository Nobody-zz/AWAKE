using System;
using System.Collections.Generic;

namespace MarcusAwakeFramework.Api;

public sealed class RerankResult
{
	public IReadOnlyList<RerankHit> Hits { get; }

	public string ResolvedModel { get; }

	public RerankResult(IReadOnlyList<RerankHit> hits, string resolvedModel)
	{
		Hits = hits ?? Array.Empty<RerankHit>();
		ResolvedModel = resolvedModel ?? string.Empty;
	}
}
