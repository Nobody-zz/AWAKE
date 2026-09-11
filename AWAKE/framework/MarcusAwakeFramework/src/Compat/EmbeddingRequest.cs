using System;
using System.Collections.Generic;

namespace MarcusAwakeFramework.Api;

public sealed class EmbeddingRequest : ProviderTaskRequest
{
	public IReadOnlyList<string> Inputs { get; }

	public int Dimensions { get; }

	public EmbeddingRequest(string routeId, IReadOnlyList<string> inputs, int dimensions, string cloudExportClassification, DateTimeOffset deadlineUtc, string idempotencyKey, bool pinModel = false)
		: base(routeId, cloudExportClassification, deadlineUtc, idempotencyKey, pinModel)
	{
		Inputs = inputs ?? Array.Empty<string>();
		Dimensions = dimensions;
	}
}
