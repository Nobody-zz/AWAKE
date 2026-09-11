using System;
using System.Collections.Generic;

namespace MarcusAwakeFramework.Api;

public sealed class RerankRequest : ProviderTaskRequest
{
	public string Query { get; }

	public IReadOnlyList<RerankDocument> Documents { get; }

	public int MaximumResults { get; }

	public int Dimensions { get; }

	public RerankRequest(string routeId, string query, IReadOnlyList<RerankDocument> documents, int maximumResults, int dimensions, string cloudExportClassification, DateTimeOffset deadlineUtc, string idempotencyKey, bool pinModel = false)
		: base(routeId, cloudExportClassification, deadlineUtc, idempotencyKey, pinModel)
	{
		Query = query ?? string.Empty;
		Documents = documents ?? Array.Empty<RerankDocument>();
		MaximumResults = maximumResults;
		Dimensions = dimensions;
	}
}
