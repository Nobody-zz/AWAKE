using System;

namespace MarcusAwakeFramework.Api;

public abstract class ProviderTaskRequest
{
	public string RouteId { get; }

	public string CloudExportClassification { get; }

	public DateTimeOffset DeadlineUtc { get; }

	public string IdempotencyKey { get; }

	public bool PinModel { get; }

	protected ProviderTaskRequest(string routeId, string cloudExportClassification, DateTimeOffset deadlineUtc, string idempotencyKey, bool pinModel)
	{
		RouteId = routeId ?? string.Empty;
		CloudExportClassification = cloudExportClassification ?? "none";
		DeadlineUtc = deadlineUtc;
		IdempotencyKey = idempotencyKey ?? Guid.NewGuid().ToString("N");
		PinModel = pinModel;
	}
}
