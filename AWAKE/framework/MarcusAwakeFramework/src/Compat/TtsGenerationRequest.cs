using System;

namespace MarcusAwakeFramework.Api;

public sealed class TtsGenerationRequest : ProviderTaskRequest
{
	public string Text { get; }

	public string Language { get; }

	public string VoiceId { get; }

	public string VoiceProfileId { get; }

	public float Speed { get; }

	public string Provenance { get; }

	public string RetentionClass { get; }

	public TtsGenerationRequest(string routeId, string text, string language, string voiceId, float speed, string provenance, string retentionClass, string cloudExportClassification, DateTimeOffset deadlineUtc, string idempotencyKey, bool pinModel = false, string voiceProfileId = "")
		: base(routeId, cloudExportClassification, deadlineUtc, idempotencyKey, pinModel)
	{
		Text = text ?? string.Empty;
		Language = language ?? string.Empty;
		VoiceId = voiceId ?? string.Empty;
		VoiceProfileId = voiceProfileId ?? string.Empty;
		Speed = speed;
		Provenance = provenance ?? string.Empty;
		RetentionClass = retentionClass ?? "campaign";
	}
}
