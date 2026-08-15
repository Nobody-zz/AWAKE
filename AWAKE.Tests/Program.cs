using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcusAIFramework.Api;
using MarcusAIFramework.Sdk.FakeHost;
using MarcusAIFramework.Sdk.TestKit;
using TaleWorlds.CampaignSystem;

namespace Awake.SdkSmoke;

internal static class Program
{
	private static async Task<int> Main()
	{
		try
		{
			AwakeLog.Enabled = false;
			return await RunAsync();
		}
		catch (Exception ex)
		{
			Console.WriteLine("FAIL_TYPE " + ex.GetType().FullName);
			Console.WriteLine("FAIL_MSG " + ex.Message);
			if (ex.InnerException != null)
			{
				Console.WriteLine("INNER_MSG " + ex.InnerException.Message);
			}
			return 1;
		}
	}

	private static async Task<int> RunAsync()
	{
		await RunEchoAndProbeAsync();
		await RunUiDispatcherMainThreadSmokeAsync();
		RunNpcTargetStableIdSmoke();
		RunUnnamedProfileSmoke();
		RunSceneDialogueRangeSmoke();
		RunAwakeEventEngineCoreSmoke();
		RunRelationshipCommandSmoke();
		Console.WriteLine("PASS ALL Awake.SdkSmoke");
		return 0;
	}

	private static void RunRelationshipCommandSmoke()
	{
		if (!AwakeUnnamedProfileService.BuildStateConstraint(null).Contains("没有"))
		{
			throw new InvalidOperationException("unnamed profile null target should use the generic state block.");
		}

		string validJson = "{\"heroId\":\"hero-1\",\"trustDelta\":1,\"loveDelta\":0,\"hostilityDelta\":0,\"reason\":\"talk\"}";
		FakeClock clock = new FakeClock(DateTimeOffset.UtcNow);
		RequestContext context = clock.Context("awake.smoke", null, "relationship-pre");
		CommandRequest request = new CommandRequest(
			"rel-1",
			AiTaskConstants.RelationshipDeltaCommandId,
			validJson,
			"idem-1",
			DateTimeOffset.UtcNow.AddMinutes(1.0));
		OperationResult<CommandAdapterPreflight> preflight = new AwakeRelationshipDeltaAdapter().Preflight(request, context);
		MafAssertions.Succeeded(preflight, "relationship preflight expected");

		string zeroJson = "{\"heroId\":\"hero-1\",\"trustDelta\":0,\"loveDelta\":0,\"hostilityDelta\":0,\"reason\":\"zero\"}";
		CommandRequest zeroRequest = new CommandRequest(
			"rel-zero",
			AiTaskConstants.RelationshipDeltaCommandId,
			zeroJson,
			"idem-zero",
			DateTimeOffset.UtcNow.AddMinutes(1.0));
		MafAssertions.Failed(
			new AwakeRelationshipDeltaAdapter().Preflight(zeroRequest, context),
			FrameworkErrorCategory.InvalidRequest,
			"awake.world_state.relationship.invalid",
			"zero relationship delta should reject");

		if (!CommandRiskPolicy.IsWorldBridgeAllowed(AiTaskConstants.RelationshipDeltaCommandId))
		{
			throw new InvalidOperationException("relationship command should be world-bridge allowed.");
		}
		if (!CommandRiskPolicy.TryGetRiskTier(AiTaskConstants.RelationshipDeltaCommandId, out CommandRiskTier tier)
			|| tier != CommandRiskTier.R2Gameplay)
		{
			throw new InvalidOperationException("relationship command risk tier mismatch.");
		}
		if (Array.IndexOf(NpcDialogueConstants.AllowedCommandIds, AiTaskConstants.RelationshipDeltaCommandId) < 0)
		{
			throw new InvalidOperationException("relationship command should be allowlisted for NPC dialogue.");
		}
		if (NpcPromptTemplate.TemplateText.IndexOf("awake.relationship.delta.v1", StringComparison.Ordinal) < 0)
		{
			throw new InvalidOperationException("relationship command should be present in the NPC prompt template.");
		}
		if (!StringComparer.Ordinal.Equals(WorldStateStore.BuildHeroKey("hero-1"), "hero.hero-1.v1"))
		{
			throw new InvalidOperationException("relationship hero key mismatch.");
		}
		Newtonsoft.Json.Linq.JObject relationship = new Newtonsoft.Json.Linq.JObject
		{
			["trust"] = 5,
			["love"] = 3,
			["hostility"] = -2
		};
		string formatted = NpcDialogueStateFormatter.FormatState(relationship, null, null);
		if (formatted.IndexOf("信任 5", StringComparison.Ordinal) < 0
			|| formatted.IndexOf("爱意 3", StringComparison.Ordinal) < 0
			|| formatted.IndexOf("敌意 -2", StringComparison.Ordinal) < 0)
		{
			throw new InvalidOperationException("relationship state formatting mismatch.");
		}
		Console.WriteLine("PASS relationship command smoke");
	}

	private static void RunAwakeEventEngineCoreSmoke()
	{
		AwakeEventDefinition valid = TestEventDefinition(
			"awake.event.valid",
			new AwakeEventDialogueAction("a", "hero-1", "hint"));
		string error;
		if (!AwakeEventValidation.Validate(valid, out error))
		{
			throw new InvalidOperationException("valid event definition should pass.");
		}

		AwakeEventDefinition badChoice = TestEventDefinition(
			"awake.event.bad.choice",
			new AwakeEventDialogueAction("c", "hero-1", ""));
		if (AwakeEventValidation.Validate(badChoice, out error)
			|| !StringComparer.Ordinal.Equals(error, "dialogueAction.choice"))
		{
			throw new InvalidOperationException("invalid dialogue choice should be rejected.");
		}

		AwakeEventDefinition withDiscussion = TestEventDefinition(
			"awake.event.discussion",
			null,
			new AwakeEventDialogueAction("discuss", "hero-1", "topic hint"));
		if (!AwakeEventValidation.Validate(withDiscussion, out error))
		{
			throw new InvalidOperationException("valid discussion action should pass.");
		}

		AwakeEventDefinition badDiscussion = TestEventDefinition(
			"awake.event.bad.discussion",
			null,
			new AwakeEventDialogueAction("a", "hero-1", ""));
		if (AwakeEventValidation.Validate(badDiscussion, out error)
			|| !StringComparer.Ordinal.Equals(error, "discussionAction.choice"))
		{
			throw new InvalidOperationException("invalid discussion choice should be rejected.");
		}

		AwakeEventDefinition missingCategory = new AwakeEventDefinition(
			"awake.event.missing.category",
			"Title",
			"Body",
			"A",
			"B");
		if (AwakeEventValidation.Validate(missingCategory, out error)
			|| !StringComparer.Ordinal.Equals(error, "source"))
		{
			throw new InvalidOperationException("missing event category should be rejected.");
		}

		AwakeEventRule clamped = new AwakeEventRule(
			valid,
			-3,
			-1,
			AwakeEventCondition.Always,
			null,
			-2);
		if (clamped.Weight != 1 || clamped.CooldownHours != 0 || clamped.MaxPerDay != 0)
		{
			throw new InvalidOperationException("event rule clamping mismatch.");
		}

		if (AwakeEventEngineCore.SelectWeighted(new List<AwakeEventRule>(), new Random(1)) != null)
		{
			throw new InvalidOperationException("empty weighted selection should return null.");
		}
		if (!AwakeEventEngineCore.IsCooldownReady(-1d, 10d, 1)
			|| !AwakeEventEngineCore.IsCooldownReady(9d, 10d, 1)
			|| AwakeEventEngineCore.IsCooldownReady(9.5d, 10d, 1))
		{
			throw new InvalidOperationException("cooldown boundary mismatch.");
		}

		AwakeEventRule start = new AwakeEventRule(
			TestEventDefinition("start"),
			1,
			1,
			AwakeEventCondition.Always,
			"next");
		AwakeEventRule next = new AwakeEventRule(
			TestEventDefinition("next"),
			1,
			1,
			AwakeEventCondition.Always);
		Dictionary<string, AwakeEventRule> chain = new Dictionary<string, AwakeEventRule>(StringComparer.Ordinal)
		{
			["start"] = start,
			["next"] = next
		};
		if (!ReferenceEquals(AwakeEventChainCore.Resolve(chain, "start", "a"), next)
			|| AwakeEventChainCore.Resolve(chain, "start", "b") != null)
		{
			throw new InvalidOperationException("event chain resolution mismatch.");
		}

		Console.WriteLine("PASS awake event engine core smoke");
	}

	private static AwakeEventDefinition TestEventDefinition(
		string id,
		AwakeEventDialogueAction dialogueAction = null,
		AwakeEventDialogueAction discussionAction = null)
	{
		return new AwakeEventDefinition(
			id,
			"Title",
			"Body",
			"A",
			"B",
			dialogueAction,
			discussionAction,
			AwakeEventSource.PresetRule,
			AwakeEventContext.Camp,
			AwakeEventSubject.PlayerNpc,
			AwakeEventContent.Daily,
			AwakeEventResolution.DialogueEntry,
			AwakeEventChoiceShape.TwoChoice,
			AwakeEventPersistence.Repeatable);
	}

	private static void RunSceneDialogueRangeSmoke()
	{
		if (SceneDialogueSelection.CurrentRange(0f, 60f) != SceneDialogueSelection.MinRangeMeters)
		{
			throw new InvalidOperationException("scene dialogue initial range mismatch.");
		}
		if (SceneDialogueSelection.CurrentRange(SceneDialogueSelection.MaxHoldSeconds, 60f) != 60f)
		{
			throw new InvalidOperationException("scene dialogue max range mismatch.");
		}
		if (SceneDialogueSelection.CurrentRange(999f, 60f) != 60f)
		{
			throw new InvalidOperationException("scene dialogue hold overflow should clamp.");
		}
		float early = SceneDialogueSelection.CurrentRange(1f, 60f);
		float late = SceneDialogueSelection.CurrentRange(2f, 60f);
		if (early <= SceneDialogueSelection.MinRangeMeters || late <= early)
		{
			throw new InvalidOperationException("scene dialogue range curve must be monotonic.");
		}
		if (SceneDialogueSelection.ClampMax(500f) != SceneDialogueSelection.HardMaxRangeMeters
			|| SceneDialogueSelection.ClampMax(3f) != SceneDialogueSelection.MinRangeMeters
			|| SceneDialogueSelection.ClampMax(float.NaN) != SceneDialogueSelection.DefaultMaxRangeMeters)
		{
			throw new InvalidOperationException("scene dialogue range clamp mismatch.");
		}
		Console.WriteLine("PASS scene dialogue range curve");
	}

	private static void RunUnnamedProfileSmoke()
	{
		if (!StringComparer.Ordinal.Equals(AwakeUnnamedProfileService.RoleLabel(Occupation.Villager), "村民")
			|| !StringComparer.Ordinal.Equals(AwakeUnnamedProfileService.RoleLabel(Occupation.Tavernkeeper), "酒馆老板")
			|| !StringComparer.Ordinal.Equals(AwakeUnnamedProfileService.RoleLabel(Occupation.Soldier), "士兵"))
		{
			throw new InvalidOperationException("unnamed profile role label mismatch.");
		}
		if (!AwakeUnnamedProfileService.BuildStateConstraint(null).Contains("没有"))
		{
			throw new InvalidOperationException("unnamed profile null target should use the generic state block.");
		}
		Console.WriteLine("PASS unnamed profile role labels");
	}

	private static void RunNpcTargetStableIdSmoke()
	{
		string kind;
		string characterId;
		int agentIndex;

		if (!AwakeNpcTarget.TryParseStableId("hero:lord_swadian", out kind, out characterId, out agentIndex)
			|| !StringComparer.Ordinal.Equals(kind, "hero")
			|| !StringComparer.Ordinal.Equals(characterId, "lord_swadian")
			|| agentIndex != -1)
		{
			throw new InvalidOperationException("hero stable id parsing mismatch.");
		}

		if (!AwakeNpcTarget.TryParseStableId("npc:townsman_empire:a3", out kind, out characterId, out agentIndex)
			|| !StringComparer.Ordinal.Equals(kind, "npc")
			|| !StringComparer.Ordinal.Equals(characterId, "townsman_empire")
			|| agentIndex != 3)
		{
			throw new InvalidOperationException("agent stable id parsing mismatch.");
		}

		if (!AwakeNpcTarget.TryParseStableId("npc:townsman_empire:static", out kind, out characterId, out agentIndex)
			|| agentIndex != -1)
		{
			throw new InvalidOperationException("static npc stable id parsing mismatch.");
		}

		if (AwakeNpcTarget.TryParseStableId("invalid", out kind, out characterId, out agentIndex))
		{
			throw new InvalidOperationException("invalid stable id should be rejected.");
		}

		Console.WriteLine("PASS npc target stable id parsing");
	}

	private static async Task RunEchoAndProbeAsync()
	{
		FakeClock clock = new FakeClock(DateTimeOffset.UtcNow);
		RequestContext valid = clock.Context("awake.smoke", null, "smoke-correlation");
		OperationResult<string> obj = await AwakeExtension.HandleEchoAsync("{}", valid, CancellationToken.None);
		MafAssertions.Succeeded(obj, "echo success expected");
		if (obj.Value.IndexOf("\"echo\":true", StringComparison.Ordinal) < 0)
		{
			throw new InvalidOperationException("echo payload did not contain the expected marker.");
		}
		Console.WriteLine("PASS echo success path");

		MafAssertions.Failed(await AwakeExtension.HandleEchoAsync("[]", valid, CancellationToken.None), FrameworkErrorCategory.InvalidRequest, "awake.invalid_payload", "invalid payload expected");
		MafAssertions.Failed(await AwakeExtension.HandleEchoAsync("{not-json", valid, CancellationToken.None), FrameworkErrorCategory.InvalidRequest, "awake.invalid_payload", "malformed object payload expected");
		Console.WriteLine("PASS echo invalid payload path");

		RequestContext expired = new RequestContext(new ExtensionId("awake.smoke"), new SessionRef("smoke-campaign", "smoke-timeline", "smoke-session"), "smoke-expired", DateTimeOffset.UtcNow.AddSeconds(-1.0));
		MafAssertions.Failed(await AwakeExtension.HandleEchoAsync("{}", expired, CancellationToken.None), FrameworkErrorCategory.Expired, "awake.context_expired", "expired context expected");
		Console.WriteLine("PASS echo expired context path");

		RequestContext providerContext = clock.Context("awake.smoke", null, "smoke-provider");
		OperationResult<IReadOnlyList<ContextContribution>> contributed = await new ProbeContextProvider().ContributeAsync(
			new ContextPlanRequest(Array.Empty<string>(), Array.Empty<string>(), new[] { "PlayerKnown" }, Array.Empty<string>(), 512),
			providerContext,
			CancellationToken.None);
		MafAssertions.Succeeded(contributed, "context contribution expected");
		if (contributed.Value.Count != 1 || !StringComparer.Ordinal.Equals(contributed.Value[0].ProviderId, "awake.probe.context"))
		{
			throw new InvalidOperationException("unexpected context contribution shape.");
		}
		Console.WriteLine("PASS context provider contribution path");
	}

	private static async Task RunUiDispatcherMainThreadSmokeAsync()
	{
		AwakeUiDispatcher.ResetGameThreadForTesting();
		AwakeUiDispatcher.Drain();

		ManualResetEventSlim gameReady = new ManualResetEventSlim(false);
		ManualResetEventSlim drainSignal = new ManualResetEventSlim(false);
		Task gameThreadTask = Task.Factory.StartNew(
			() =>
			{
				try
				{
					AwakeUiDispatcher.InitializeGameThread();
					gameReady.Set();
					drainSignal.Wait();
					AwakeUiDispatcher.Drain();
				}
				catch (Exception ex)
				{
					AwakeLog.Write("ui_dispatcher_smoke_game_thread_error error=" + ex.Message);
				}
			},
			CancellationToken.None,
			TaskCreationOptions.LongRunning,
			TaskScheduler.Default);

		if (!gameReady.Wait(TimeSpan.FromSeconds(5)))
		{
			throw new InvalidOperationException("game thread smoke did not start.");
		}

		Task<int> enqueued = AwakeUiDispatcher.RunOnGameThreadAsync(
			() => Task.FromResult(42),
			CancellationToken.None);
		drainSignal.Set();
		await gameThreadTask;
		int value = await enqueued;
		if (value != 42)
		{
			throw new InvalidOperationException("ui dispatcher main thread result mismatch.");
		}
		Console.WriteLine("PASS ui dispatcher main thread smoke");
		AwakeUiDispatcher.ResetGameThreadForTesting();
	}
}
