using System;

namespace MarcusAwakeFramework.Api;

public sealed class ImageGenerationRequest : ProviderTaskRequest
{
	public string Prompt { get; }

	public string NegativePrompt { get; }

	public int Width { get; }

	public int Height { get; }

	public long Seed { get; }

	public string Provenance { get; }

	public string RetentionClass { get; }

	public ImageGenerationRequest(string routeId, string prompt, string negativePrompt, int width, int height, long seed, string provenance, string retentionClass, string cloudExportClassification, DateTimeOffset deadlineUtc, string idempotencyKey, bool pinModel = false)
		: base(routeId, cloudExportClassification, deadlineUtc, idempotencyKey, pinModel)
	{
		Prompt = prompt ?? string.Empty;
		NegativePrompt = negativePrompt ?? string.Empty;
		Width = width;
		Height = height;
		Seed = seed;
		Provenance = provenance ?? string.Empty;
		RetentionClass = retentionClass ?? "campaign";
	}
}
