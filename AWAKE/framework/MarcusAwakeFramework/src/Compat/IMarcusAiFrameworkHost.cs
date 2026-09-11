namespace MarcusAwakeFramework.Api;

/// <summary>Public service surface exposed to independently compiled extensions.</summary>
public interface IMarcusAiFrameworkHost
{
	FrameworkIdentity Identity { get; }

	SessionRef CurrentSession { get; }

	ICapabilityBroker Capabilities { get; }

	IToolCandidateService Tools { get; }

	IGameDataService GameData { get; }

	IContextService Context { get; }

	IRagService Rag { get; }

	IEventService Events { get; }

	ICommandService Commands { get; }

	IAiGateway Ai { get; }

	IAiModelService Models { get; }

	IMediaService Media { get; }

	IPromptRegistry Prompts { get; }

	IStorageService Storage { get; }

	IAssetService Assets { get; }

	IPermissionService Permissions { get; }

	IDiagnosticsService Diagnostics { get; }

	ILoggingService Log { get; }
}
