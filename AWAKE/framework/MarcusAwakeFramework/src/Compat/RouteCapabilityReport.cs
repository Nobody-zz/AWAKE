using System;
using System.Collections.Generic;

namespace MarcusAwakeFramework.Api;

public sealed class RouteCapabilityReport
{
	public string RouteId { get; }

	public IReadOnlyList<ProviderCapabilityInfo> Candidates { get; }

	public RouteCapabilityReport(string routeId, IReadOnlyList<ProviderCapabilityInfo> candidates)
	{
		RouteId = routeId ?? string.Empty;
		Candidates = candidates ?? Array.Empty<ProviderCapabilityInfo>();
	}
}
