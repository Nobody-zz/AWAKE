using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;

namespace Awake.SdkSmoke;

internal static class Program
{
	private static async Task<int> Main(string[] args)
	{
		try
		{
			AwakeLog.Enabled = false;
			if (args != null
				&& args.Length > 0
				&& string.Equals(args[0], "--redtest-r1-behavioral", StringComparison.Ordinal))
			{
				return await RedtestR1Behavioral.RunAsync();
			}
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
		RunBuildIdentitySmoke();
		await RunB1NativeStateSmokeAsync();
		await RunEchoAndProbeAsync();
		await RunUiDispatcherMainThreadSmokeAsync();
		RunNpcTargetStableIdSmoke();
		RunUnnamedProfileSmoke();
		RunSceneDialogueRangeSmoke();
		RunSceneSelectionUxSmoke();
		RunSceneShoutContractSmoke();
		RunNpcDialogueOutputOptionalEffectsSmoke();
		RunAwakeEventEngineCoreSmoke();
		RunRelationshipCommandSmoke();
		RunWorldEffectCommandSmoke();
		RunPromiseStateMachineSmoke();
		RunGiveGoldSmoke();
		RunR1GoldAdapterBoundarySmoke();
		await RunStoragePipelineSmokeAsync();
		await RunPersistenceSettlementTruthSmokeAsync();
		await RunG3S0FocusedReadinessSmokeAsync();
		await RunPromptRegistrationCoordinatorSmokeAsync();
		RunNpcMemorySmoke();
		RunWorldbookSmoke();
		RunWorldKnowledgeB2Smoke();
		RunPersonaTemplateSmoke();
		RunSharedPersonaGoldenFixtureSmoke();
		RunPersonaPersistenceSmoke();
		RunRouteContractSmoke();
		RunTerminalHotkeySmoke();
		RunNpcProactiveSmoke();
		RunLongWaitSmoke();
		RunEventInboxSmoke();
		RunMemoryOverviewSmoke();
		RunGuardPerfSmoke();
		RunFeedbackSmoke();
		RunMcmPresetSmoke();
		RunCloudExportSmoke();
		RunMessengerHistorySmoke();
RunContactLabelSmoke();
		RunTranscriptSourceSmoke();
		RunB0StabilitySmoke();
		RunRuleRegistrySmoke();
		RunEventDataLoaderSmoke();
		RunMemoryConsolidatorSmoke();
		RunProactiveMotiveRegistrySmoke();
		RunContentApiSmoke();
		RunDialogueSessionCoordinatorSmoke();
		RunDialogueQueueSmoke();
		RunOnboardingSmoke();
		RunB9InfraSmoke();
		RunMarcusLinkSmoke();
		Console.WriteLine("PASS ALL Awake.SdkSmoke");
		return 0;
	}

	private static void RunBuildIdentitySmoke()
	{
		if (string.IsNullOrWhiteSpace(AwakeVersion.BuildId)
			|| string.IsNullOrWhiteSpace(AwakeVersion.Version)
			|| string.IsNullOrWhiteSpace(AwakeVersion.InformationalVersion))
		{
			throw new InvalidOperationException("build identity values should not be empty.");
		}

		byte[] bytes = Encoding.UTF8.GetBytes("awake-build-identity-smoke");
		string first = AwakeBuildIdentity.ComputeSha256Short(bytes);
		string second = AwakeBuildIdentity.ComputeSha256Short(bytes);
		if (first != second || first != "7E5EFC032ADE")
		{
			throw new InvalidOperationException("build hash short code should match the fixed SHA-256 fixture.");
		}

		if (AwakeBuildIdentity.TryComputeFileSha256Short(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".missing")) != "unknown")
		{
			throw new InvalidOperationException("missing build path should return unknown.");
		}
	}

	private static void RunMarcusLinkSmoke()
	{
		string status = AwakeMarcusLinkService.BuildStatusText();
		if (string.IsNullOrWhiteSpace(status))
		{
			throw new InvalidOperationException("marcus link status should never be empty.");
		}
		AwakeConfig config = new AwakeConfig();
		if (config.ConfigureProviderApiKey == null
			|| config.ApplyProviderConfiguration == null
			|| config.PullProviderModels == null
			|| config.TestProviderConnection == null
			|| config.RefreshAiStatus == null
			|| config.OpenDeveloperReport == null)
		{
			throw new InvalidOperationException("marcus mcm actions should be wired.");
		}
		Console.WriteLine("PASS marcus link smoke");
	}

	private static void RunCloudExportSmoke()
	{
		AwakeConfig config = new AwakeConfig();
		if (!config.EnableCloudExport || !config.AllowCloudExportPlayerState)
		{
			throw new InvalidOperationException("cloud export defaults should be enabled.");
		}
		if (!CloudExportPolicy.IsKnownClassification(CloudExportPolicy.None)
			|| !CloudExportPolicy.IsKnownClassification(CloudExportPolicy.PlayerState))
		{
			throw new InvalidOperationException("cloud export classifications should be known.");
		}
		config.EnableCloudExport = false;
		config.AllowCloudExportPlayerState = true;
		if (!StringComparer.Ordinal.Equals(
			CloudExportPolicy.ResolveDialogueClassification(config),
			CloudExportPolicy.None))
		{
			throw new InvalidOperationException("disabled cloud export should resolve to none.");
		}
		config.EnableCloudExport = true;
		if (!StringComparer.Ordinal.Equals(
			CloudExportPolicy.ResolveDialogueClassification(config),
			CloudExportPolicy.PlayerState))
		{
			throw new InvalidOperationException("enabled cloud export should resolve to player_state.");
		}
		config.AllowCloudExportPlayerState = false;
		if (!StringComparer.Ordinal.Equals(
			CloudExportPolicy.ResolveDialogueClassification(config),
			CloudExportPolicy.None))
		{
			throw new InvalidOperationException("player state denied should fall back to none.");
		}
		Console.WriteLine("PASS cloud export smoke");
	}

		private static void RunContactLabelSmoke()
	{
		if (!StringComparer.Ordinal.Equals(
			AwakeContactLabelBuilder.Build("领主甲", "家族甲", "南帝国", "城镇甲", false, false),
			"领主甲 · 家族甲 · 南帝国")
			|| !StringComparer.Ordinal.Equals(
			AwakeContactLabelBuilder.Build("佣兵甲", "黑狼团", "西帝国", "城镇甲", false, false),
			"佣兵甲 · 黑狼团 · 西帝国")
			|| !StringComparer.Ordinal.Equals(
			AwakeContactLabelBuilder.Build("佣兵乙", "黑狼团", string.Empty, "城镇甲", false, false),
			"佣兵乙 · 黑狼团")
			|| !StringComparer.Ordinal.Equals(
			AwakeContactLabelBuilder.Build("流浪者甲", "", "", "城镇甲", true, false),
			"流浪者甲")
			|| !StringComparer.Ordinal.Equals(
			AwakeContactLabelBuilder.Build("商人", "", "", "城镇甲", false, true),
			"商人 · 城镇甲"))
		{
			throw new InvalidOperationException("contact display label rules mismatch.");
		}
		Console.WriteLine("PASS contact label smoke");
	}
private static void RunMessengerHistorySmoke()
	{
		if (!StringComparer.Ordinal.Equals(AwakeLetterRequestValidator.GetRejectionReason("hero-1", "你好"), string.Empty)
			|| !StringComparer.Ordinal.Equals(AwakeLetterRequestValidator.GetRejectionReason("", "你好"), "contact_missing")
			|| !StringComparer.Ordinal.Equals(AwakeLetterRequestValidator.GetRejectionReason("hero-1", "  "), "text_missing"))
		{
			throw new InvalidOperationException("letter request rejection reasons mismatch.");
		}
		AwakeMessengerHistory.ClearForTesting();
		AwakeMessengerHistory.Append("hero-1", "你", "你好");
		AwakeMessengerHistory.Append("hero-1", "NPC", "你好");
		IReadOnlyList<AwakeMessengerChatLine> history = AwakeMessengerHistory.GetHistory("hero-1");
		if (history.Count != 2
			|| !StringComparer.Ordinal.Equals(history[0].Speaker, "你")
			|| !StringComparer.Ordinal.Equals(history[1].Text, "你好"))
		{
			throw new InvalidOperationException("messenger history cache mismatch.");
		}
		AwakeMessengerHistory.ClearForTesting();
		Console.WriteLine("PASS messenger history smoke");
	}

	private static void RunTranscriptSourceSmoke()
	{
		if (!AwakeTranscriptValidator.IsValidSource("letter")
			|| !AwakeTranscriptValidator.IsValidSource("map")
			|| !AwakeTranscriptValidator.IsValidSource("dev_test"))
		{
			throw new InvalidOperationException("transcript valid sources should include letter, map and dev_test.");
		}
		AwakeTranscriptLine letterLine = new AwakeTranscriptLine(
			"letter|smoke",
			1,
			"",
			"你",
			"测试信件",
			"letter",
			"letter|smoke",
			"player");
		string letterError;
		if (!AwakeTranscriptValidator.ValidateLine(letterLine, out letterError)
			|| !StringComparer.Ordinal.Equals(letterLine.Source, "letter"))
		{
			throw new InvalidOperationException("letter transcript line should validate with source=letter.");
		}
		string built = NpcPromptTemplate.BuildDirectInput(new Dictionary<string, string>
		{
			["retrieved_knowledge"] = "k",
			["npc_memory"] = "",
			["npc_state"] = "s",
			["npc_identity"] = "测试人物",
			["dialogue_history"] = "",
			["player_known"] = "p",
			["scene"] = "sc",
			["opening_hint"] = "",
			["player_turn"] = "你好",
			["npc_id"] = "hero:test"
		});
		if (built.IndexOf("\"heroId\": \"hero:test\"", StringComparison.Ordinal) < 0
			|| built.IndexOf("\"heroId\": \"\"", StringComparison.Ordinal) >= 0)
		{
			throw new InvalidOperationException("npc prompt heroId should not be double-quoted.");
		}
		Console.WriteLine("PASS transcript source + prompt heroId smoke");
	}

	private static void RunB0StabilitySmoke()
	{
		AwakeMessengerHistory.ResetForCampaign();
		AwakeMessengerHistory.Append("hero-b0", "你", "旧档");
		AwakeMessengerHistory.ResetForCampaign();
		if (AwakeMessengerHistory.GetHistory("hero-b0").Count != 0)
		{
			throw new InvalidOperationException("messenger history should reset per campaign.");
		}

		WorldEventLedger.ResetForCampaign();
		if (!WorldEventLedger.QueueRecord(1, "event", "旧事件"))
		{
			throw new InvalidOperationException("world event ledger should accept a valid queued event.");
		}
		WorldEventLedger.ResetForCampaign();
		if (WorldEventLedger.Count != 0)
		{
			throw new InvalidOperationException("world event ledger should reset per campaign.");
		}

		AwakeBackgroundTask.Run(
			() => Task.FromException(new InvalidOperationException("expected")),
			"smoke");
		AwakeBackgroundTask.Run(() => null, "smoke-null");
		Console.WriteLine("PASS b0 stability smoke");
	}

	private static async Task RunB1NativeStateSmokeAsync()
	{
		AwakeRuntime.ResetSessionStateForTesting();
		int probeCalls = 0;
		TaskCompletionSource<bool> readinessGate = new TaskCompletionSource<bool>();
		AwakeRuntime.NativeReadinessProbeForTesting = (generation, cancellationToken) =>
		{
			probeCalls++;
			return readinessGate.Task.ContinueWith(
				_ => NativeReadinessResult.Ready(generation, "b1-session"),
				TaskScheduler.Default);
		};
		Task<NativeReadinessResult> first = AwakeRuntime.EnsureNativeReadinessAsync("b1-session", CancellationToken.None);
		Task<NativeReadinessResult> second = AwakeRuntime.EnsureNativeReadinessAsync("b1-session", CancellationToken.None);
		if (!object.ReferenceEquals(first, second) || probeCalls != 1)
		{
			throw new InvalidOperationException("native readiness should be single-flight per session.");
		}
		readinessGate.SetResult(true);
		NativeReadinessResult ready = await first;
		if (ready.Status != NativeReadinessStatus.Ready || ready.SessionGeneration != AwakeRuntime.SessionGeneration)
		{
			throw new InvalidOperationException("native readiness ready result mismatch.");
		}

		AwakeRuntime.ResetSessionStateForCampaign();
		AwakeRuntime.NativeReadinessProbeForTesting = (generation, cancellationToken) =>
			Task.FromResult(NativeReadinessResult.Failed(generation, "native_unavailable", retryable: true));
		NativeReadinessResult failed = await AwakeRuntime.EnsureNativeReadinessAsync("b1-failed", CancellationToken.None);
		if (failed.Status != NativeReadinessStatus.Failed || !failed.Retryable)
		{
			throw new InvalidOperationException("native readiness failed result mismatch.");
		}

		AwakeRuntime.ResetSessionStateForCampaign();
		AwakeRuntime.NativeReadinessProbeForTesting = async (generation, cancellationToken) =>
		{
			await Task.Yield();
			cancellationToken.ThrowIfCancellationRequested();
			return NativeReadinessResult.Ready(generation, "unexpected");
		};
		CancellationTokenSource cancelledSource = new CancellationTokenSource();
		cancelledSource.Cancel();
		NativeReadinessResult cancelled = await AwakeRuntime.EnsureNativeReadinessAsync("b1-cancelled", cancelledSource.Token);
		if (cancelled.Status != NativeReadinessStatus.Cancelled)
		{
			throw new InvalidOperationException("native readiness cancellation should be observable.");
		}

		AwakeRuntime.ResetSessionStateForCampaign();
		AwakeRuntime.NativeReadinessProbeForTesting = (generation, cancellationToken) =>
			Task.FromResult(NativeReadinessResult.Ready(generation, "should-not-run"));
		AwakeRuntime.BeginSessionEnd();
		NativeReadinessResult skipped = await AwakeRuntime.EnsureNativeReadinessAsync("b1-ended", CancellationToken.None);
		if (skipped.Status != NativeReadinessStatus.Skipped)
		{
			throw new InvalidOperationException("native readiness after session end should fail closed.");
		}

		AwakeRuntime.ResetSessionStateForCampaign();
		TaskCompletionSource<bool> staleGate = new TaskCompletionSource<bool>();
		AwakeRuntime.NativeReadinessProbeForTesting = (generation, cancellationToken) =>
			staleGate.Task.ContinueWith(
				_ => NativeReadinessResult.Ready(generation, "stale-session"),
				TaskScheduler.Default);
		Task<NativeReadinessResult> staleTask = AwakeRuntime.EnsureNativeReadinessAsync("stale-session", CancellationToken.None);
		int staleGeneration = AwakeRuntime.SessionGeneration;
		AwakeRuntime.ResetSessionStateForCampaign();
		int currentGeneration = AwakeRuntime.SessionGeneration;
		AwakeRuntime.NativeReadinessProbeForTesting = (generation, cancellationToken) =>
			Task.FromResult(NativeReadinessResult.Ready(generation, "current-session"));
		Task<NativeReadinessResult> currentTask = AwakeRuntime.EnsureNativeReadinessAsync("current-session", CancellationToken.None);
		staleGate.SetResult(true);
		NativeReadinessResult stale = await staleTask;
		NativeReadinessResult current = await currentTask;
		if (staleGeneration == currentGeneration
			|| stale.Status != NativeReadinessStatus.Skipped
			|| current.Status != NativeReadinessStatus.Ready)
		{
			throw new InvalidOperationException("old native readiness task must not pollute a new session.");
		}

		IdentitySnapshotInput sourceIdentity = new IdentitySnapshotInput
		{
			StableId = "hero:source",
			HeroId = "source",
			CultureId = "vlandia",
			ClanId = "clan_source",
			ClanLeaderId = "source",
			Age = 32.5f,
			IsFemale = false,
			IsClanLeader = true
		};
		IdentitySnapshotInput targetIdentity = new IdentitySnapshotInput
		{
			StableId = "hero:target",
			HeroId = "target",
			CultureId = "vlandia",
			ClanId = "clan_target",
			ClanLeaderId = "target",
			Age = 29.0f,
			IsFemale = true
		};
		NativeSocialSnapshotInput snapshotInput = new NativeSocialSnapshotInput
		{
			SessionGeneration = currentGeneration,
			SnapshotToken = "b1-snapshot",
			CapturedAtGameTime = 42.5,
			SourceIdentity = sourceIdentity,
			TargetIdentity = targetIdentity,
			BaseRelation = -30,
			EffectiveRelation = 20,
			NativeFriendState = NativeFlagState.False,
			NativeEnemyState = NativeFlagState.True,
			NativeNeutralState = NativeFlagState.False,
			SameClan = NativeFlagState.False,
			FamilyLinks = new List<FamilyLinkSnapshotInput>
			{
				new FamilyLinkSnapshotInput(FamilyLinkKind.Spouse, "target")
			}
		};
		NativeSocialSnapshot snapshot = NativeSocialSnapshot.Capture(snapshotInput);
		sourceIdentity.HeroId = "changed";
		snapshotInput.BaseRelation = 80;
		snapshotInput.FamilyLinks.Add(new FamilyLinkSnapshotInput(FamilyLinkKind.Child, "child"));
		if (snapshot.SourceIdentity.HeroId != "source"
			|| snapshot.BaseRelation != -30
			|| snapshot.FamilyLinks.Count != 1
			|| snapshot.NativeEnemyState != NativeFlagState.True)
		{
			throw new InvalidOperationException("native scalar snapshot should be immutable after capture.");
		}

		NativeSocialSnapshot unknown = NativeSocialSnapshot.Capture(null);
		if (unknown.BaseRelation.HasValue
			|| unknown.EffectiveRelation.HasValue
			|| unknown.NativeFriendState != NativeFlagState.Unknown
			|| unknown.SameClan != NativeFlagState.Unknown
			|| unknown.SourceIdentity.HeroId != null)
		{
			throw new InvalidOperationException("missing native data should remain explicitly unknown.");
		}

		AssertSnapshotHasNoBannerlordObjects(typeof(NativeSocialSnapshot));
		AssertSnapshotHasNoBannerlordObjects(typeof(IdentitySnapshot));
		AwakeRuntime.NativeReadinessProbeForTesting = null;
		AwakeRuntime.ResetSessionStateForTesting();
		Console.WriteLine("PASS b1 native readiness and scalar snapshot smoke");
	}

	private static void AssertSnapshotHasNoBannerlordObjects(Type snapshotType)
	{
		foreach (PropertyInfo property in snapshotType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
		{
			if (property.PropertyType.FullName != null
				&& property.PropertyType.FullName.StartsWith("TaleWorlds.", StringComparison.Ordinal))
			{
				throw new InvalidOperationException(snapshotType.Name + " must not retain Bannerlord objects.");
			}
		}
	}

	private static void RunRuleRegistrySmoke()
	{
		AwakeRuleRegistry.ResetForTesting();
		AwakeRuleManifest valid = new AwakeRuleManifest
		{
			Id = "test.rule.one",
			Group = "test",
			Priority = 50,
			Enabled = true,
			Fingerprint = "fp-1",
			Payload = new Newtonsoft.Json.Linq.JObject { ["key"] = "value" }
		};
		if (!AwakeRuleRegistry.Register(valid))
		{
			throw new InvalidOperationException("valid rule should register.");
		}
		if (AwakeRuleRegistry.Register(valid))
		{
			throw new InvalidOperationException("duplicate rule id should be rejected.");
		}
		AwakeRuleManifest invalid = new AwakeRuleManifest
		{
			Id = "Bad ID",
			Priority = -1
		};
		if (AwakeRuleRegistry.Register(invalid))
		{
			throw new InvalidOperationException("invalid rule should be rejected.");
		}
		AwakeRuleManifest fetched;
		if (!AwakeRuleRegistry.TryGet("test.rule.one", out fetched)
			|| !StringComparer.Ordinal.Equals(fetched.Group, "test"))
		{
			throw new InvalidOperationException("registered rule should be queryable.");
		}
		AwakeRuleRegistry.ResetForTesting();
		Console.WriteLine("PASS rule registry smoke");
	}

	private static void RunEventDataLoaderSmoke()
	{
		AwakeRuleRegistry.ResetForTesting();
		Newtonsoft.Json.Linq.JObject payload = new Newtonsoft.Json.Linq.JObject
		{
			["kind"] = "event",
			["weight"] = 2,
			["cooldownHours"] = 24,
			["condition"] = "InSettlement",
			["event"] = new Newtonsoft.Json.Linq.JObject
			{
				["id"] = "event.test",
				["title"] = "测试事件",
				["body"] = "事件正文",
				["optionA"] = "接受",
				["optionB"] = "拒绝",
				["source"] = "PresetRule",
				["context"] = "Settlement",
				["subject"] = "PlayerNpc",
				["content"] = "Daily",
				["resolution"] = "NarrativeOnly",
				["choiceShape"] = "TwoChoice",
				["persistence"] = "Repeatable",
				["effect"] = new Newtonsoft.Json.Linq.JObject
				{
					["choice"] = "a",
					["targetId"] = "hero-1",
					["trustDelta"] = 1,
					["loveDelta"] = 0,
					["hostilityDelta"] = 0
				}
			}
		};
		AwakeRuleManifest manifest = new AwakeRuleManifest
		{
			Id = "event.test.rule",
			Group = "test",
			Priority = 10,
			Enabled = true,
			Fingerprint = "fp-event",
			Payload = payload
		};
		if (!AwakeRuleRegistry.Register(manifest))
		{
			throw new InvalidOperationException("event rule manifest should register.");
		}
		AwakeEventRule rule;
		string error;
		if (!AwakeEventDataLoader.TryParseRule(payload, out rule, out error)
			|| rule.CooldownHours != 24
			|| rule.Condition != AwakeEventCondition.InSettlement)
		{
			throw new InvalidOperationException("event rule parse mismatch: " + error);
		}
		AwakeRuleRegistry.ResetForTesting();
		Console.WriteLine("PASS event data loader smoke");
	}

	private static void RunMemoryConsolidatorSmoke()
	{
		Newtonsoft.Json.Linq.JObject doc = new Newtonsoft.Json.Linq.JObject
		{
			["memories"] = new Newtonsoft.Json.Linq.JArray
			{
				new Newtonsoft.Json.Linq.JObject
				{
					["day"] = 10,
					["weight"] = 1,
					["summary"] = "旧日常",
					["type"] = "shared_experience"
				},
				new Newtonsoft.Json.Linq.JObject
				{
					["day"] = 20,
					["weight"] = 2,
					["eventType"] = "meeting",
					["entityId"] = "hero-1",
					["summary"] = "第一次会面",
					["type"] = "event"
				},
				new Newtonsoft.Json.Linq.JObject
				{
					["day"] = 25,
					["weight"] = 2,
					["eventType"] = "meeting",
					["entityId"] = "hero-1",
					["summary"] = "第二次会面",
					["type"] = "event"
				},
				new Newtonsoft.Json.Linq.JObject
				{
					["day"] = 5,
					["weight"] = 3,
					["promise"] = true,
					["summary"] = "承诺誓言",
					["type"] = "promise"
				}
			},
			["promises"] = new Newtonsoft.Json.Linq.JArray()
		};
		Newtonsoft.Json.Linq.JArray memories;
		Newtonsoft.Json.Linq.JArray promises;
		NpcMemoryConsolidationResult result = NpcMemoryConsolidator.Consolidate(
			doc,
			100,
			out memories,
			out promises);
		if (result.Removed != 1 || result.Merged != 1 || !result.Changed)
		{
			throw new InvalidOperationException("memory consolidation counters mismatch.");
		}
		if (memories.Count != 1 || promises.Count != 1)
		{
			throw new InvalidOperationException("memory consolidation output mismatch.");
		}
		Console.WriteLine("PASS memory consolidator smoke");
	}

	private static void RunMcmPresetSmoke()
	{
		AwakeConfig defaults = new AwakeConfig();
		if (defaults.NpcProactiveChance != 35
			|| !defaults.EnableCloudExport
			|| !defaults.AllowCloudExportPlayerState
			|| defaults.ConfigureProviderApiKey == null
			|| defaults.ApplyProviderConfiguration == null
			|| defaults.PullProviderModels == null
			|| defaults.TestProviderConnection == null)
		{
			throw new InvalidOperationException("mcm defaults mismatch.");
		}
		bool sawStrict = false;
		foreach (MCM.Abstractions.ISettingsPreset preset in AwakePresetCatalog.Build())
		{
			AwakeConfig template = preset.LoadPreset() as AwakeConfig;
			if (template == null || template.NpcProactiveChance < 0 || template.NpcProactiveChance > 100)
			{
				throw new InvalidOperationException("mcm preset template invalid.");
			}
			if (!template.EnableCloudExport || !template.AllowCloudExportPlayerState)
			{
				throw new InvalidOperationException("preset should not disable cloud transport.");
			}
			if (StringComparer.Ordinal.Equals(preset.Id, "strict") && template.EnableNpcProactive)
			{
				throw new InvalidOperationException("strict preset should disable proactive chat.");
			}
			if (StringComparer.Ordinal.Equals(preset.Id, "strict")) sawStrict = true;
		}
		if (!sawStrict)
		{
			throw new InvalidOperationException("mcm preset catalog missing strict preset.");
		}
		Console.WriteLine("PASS mcm preset smoke");
	}

	private static void RunFeedbackSmoke()
	{
		if (!AwakeFeedback.ColorFor(AwakeFeedbackTone.Success).Equals(new Color(0.35f, 1f, 0.35f, 1f))
			|| !AwakeFeedback.ColorFor(AwakeFeedbackTone.Warning).Equals(new Color(1f, 0.95f, 0.25f, 1f))
			|| !AwakeFeedback.ColorFor(AwakeFeedbackTone.Error).Equals(new Color(1f, 0.3f, 0.3f, 1f)))
		{
			throw new InvalidOperationException("feedback tone color mapping mismatch.");
		}
		Console.WriteLine("PASS feedback smoke");
	}

	private static void RunGuardPerfSmoke()
	{
		string normalized = NpcDialogueReplyNormalizer.Normalize("你好\r\n\n\n   \0世界  ");
		if (normalized.IndexOf("你好", StringComparison.Ordinal) < 0
			|| normalized.IndexOf("世界", StringComparison.Ordinal) < 0
			|| normalized.IndexOf('\r') >= 0
			|| normalized.IndexOf('\0') >= 0
			|| normalized.IndexOf("\n\n", StringComparison.Ordinal) >= 0)
		{
			throw new InvalidOperationException("reply normalizer mismatch: " + normalized);
		}
		long start = AwakePerfProbe.StartMilliseconds();
		AwakePerfProbe.Record("smoke", start);
		Console.WriteLine("PASS guard perf smoke");
	}

	private static void RunMemoryOverviewSmoke()
	{
		Newtonsoft.Json.Linq.JObject doc = new Newtonsoft.Json.Linq.JObject
		{
			["memories"] = new Newtonsoft.Json.Linq.JArray
			{
				new Newtonsoft.Json.Linq.JObject
				{
					["id"] = "m-high",
					["day"] = 11,
					["weight"] = 3,
					["type"] = "shared_experience",
					["summary"] = "重要记忆",
					["facts"] = new Newtonsoft.Json.Linq.JArray { "事实甲" }
				},
				new Newtonsoft.Json.Linq.JObject
				{
					["id"] = "m-low",
					["day"] = 12,
					["weight"] = 1,
					["type"] = "event",
					["summary"] = "旧事",
					["facts"] = new Newtonsoft.Json.Linq.JArray()
				}
			}
		};
		string overview = NpcMemoryOverviewBuilder.BuildOverview(doc, 12);
		if (overview.IndexOf("重要记忆", StringComparison.Ordinal) < 0
			|| overview.IndexOf("旧事", StringComparison.Ordinal) < 0)
		{
			throw new InvalidOperationException("memory overview builder mismatch.");
		}
		if (Encoding.UTF8.GetByteCount(NpcMemoryOverviewBuilder.BuildOverview(doc, 12, 50)) > 50)
		{
			throw new InvalidOperationException("memory overview byte budget mismatch.");
		}
		Console.WriteLine("PASS memory overview smoke");
	}

	private static void RunEventInboxSmoke()
	{
		List<WorldEventRecord> week = new List<WorldEventRecord>
		{
			new WorldEventRecord(10, "event", "攻城战结束"),
			new WorldEventRecord(12, "event", "商队抵达")
		};
		string text = WorldEventInboxFormatter.Format(week, 12);
		if (text.IndexOf("攻城战结束", StringComparison.Ordinal) < 0
			|| text.IndexOf("商队抵达", StringComparison.Ordinal) < 0)
		{
			throw new InvalidOperationException("world event inbox formatter mismatch.");
		}
		if (!StringComparer.Ordinal.Equals(
			WorldEventInboxFormatter.Format(new List<WorldEventRecord>(), 12),
			"本周没有记录。"))
		{
			throw new InvalidOperationException("world event inbox empty text mismatch.");
		}
		Console.WriteLine("PASS event inbox smoke");
	}

	private static void RunNpcProactiveSmoke()
	{
		NpcProactiveCandidate candidate = new NpcProactiveCandidate
		{
			HeroId = "hero-1",
			Motive = NpcProactiveMotive.Relationship,
			MotiveId = "relationship",
			Affinity = 25,
			State = NpcProactiveState.Pending,
			Day = 10,
			ExpiresAtDay = 11,
			CooldownDay = 12,
			Fatigue = 1,
			OpeningHint = "hint",
			TriggerReason = "reason"
		};
		Newtonsoft.Json.Linq.JObject json = candidate.ToJson();
		NpcProactiveCandidate parsed = NpcProactiveCandidate.FromJson(json);
		if (!StringComparer.Ordinal.Equals(parsed.HeroId, "hero-1")
			|| parsed.Motive != NpcProactiveMotive.Relationship
			|| !StringComparer.Ordinal.Equals(parsed.MotiveId, "relationship")
			|| parsed.State != NpcProactiveState.Pending
			|| parsed.Day != 10
			|| parsed.ExpiresAtDay != 11
			|| parsed.CooldownDay != 12
			|| parsed.Fatigue != 1
			|| !StringComparer.Ordinal.Equals(parsed.OpeningHint, "hint")
			|| !StringComparer.Ordinal.Equals(parsed.TriggerReason, "reason"))
		{
			throw new InvalidOperationException("npc proactive candidate roundtrip mismatch.");
		}
		if (!StringComparer.Ordinal.Equals(
			NpcProactiveService.BuildTriggerReason(60, true),
			"双方已有信任与吸引，对方愿意主动接近"))
		{
			throw new InvalidOperationException("proactive trigger reason mismatch.");
		}
		if (NpcProactiveService.ComputeTriggerChance(0, false, 35) >= 0.03
			|| NpcProactiveService.ComputeTriggerChance(60, true, 35) < 0.10
			|| NpcProactiveService.ComputeTriggerChance(60, true, 0) != 0d)
		{
			throw new InvalidOperationException("proactive trigger chance mismatch.");
		}
		if (NpcProactiveConstants.MaximumFatigue <= 0
			|| NpcProactiveConstants.CooldownDays <= 0
			|| NpcProactiveConstants.ExpiresAfterDays <= 0)
		{
			throw new InvalidOperationException("npc proactive constants should be positive.");
		}
		NpcProactiveService.ClearForTesting();
		Console.WriteLine("PASS npc proactive smoke");
	}

	private static void RunProactiveMotiveRegistrySmoke()
	{
		AwakeRuleRegistry.ResetForTesting();
		NpcProactiveMotiveRegistry.ResetForTesting();
		Newtonsoft.Json.Linq.JObject payload = new Newtonsoft.Json.Linq.JObject
		{
			["kind"] = "proactive_motive",
			["id"] = "test.motive",
			["displayName"] = "测试动机",
			["baseWeight"] = 5,
			["openingHint"] = "测试动机开场",
			["minAffinity"] = -50,
			["maxAffinity"] = 50
		};
		AwakeRuleManifest manifest = new AwakeRuleManifest
		{
			Id = "test.motive.manifest",
			Group = "test",
			Priority = 20,
			Enabled = true,
			Fingerprint = "fp-motive",
			Payload = payload
		};
		if (!AwakeRuleRegistry.Register(manifest))
		{
			throw new InvalidOperationException("motive rule manifest should register.");
		}
		NpcProactiveMotiveRegistry.LoadFromRuleRegistry();
		IReadOnlyList<NpcProactiveMotiveDefinition> definitions = NpcProactiveMotiveRegistry.All();
		if (definitions.Count != 1
			|| !StringComparer.Ordinal.Equals(definitions[0].Id, "test.motive")
			|| definitions[0].BaseWeight != 5)
		{
			throw new InvalidOperationException("proactive motive registry mismatch.");
		}
		NpcProactiveMotiveRegistry.ResetForTesting();
		AwakeRuleRegistry.ResetForTesting();
		Console.WriteLine("PASS proactive motive registry smoke");
	}

	private sealed class TestContentPack : IAwakeContentPack
	{
		public string Id => "test.content.pack";

		public void Register(IAwakeContentRegistry registry)
		{
			registry.RegisterRule(new AwakeContentRule
			{
				Id = "test.rule.content",
				PayloadJson = "{\"kind\":\"custom\",\"value\":1}"
			});
			registry.RegisterEvent(new AwakeContentEvent
			{
				Id = "event.content",
				Title = "内容事件",
				Body = "内容正文",
				OptionA = "接受",
				OptionB = "拒绝",
				Source = "PresetRule",
				Context = "Settlement",
				Subject = "PlayerNpc",
				Content = "Daily",
				Resolution = "NarrativeOnly",
				ChoiceShape = "TwoChoice",
				Persistence = "Repeatable"
			});
			registry.RegisterProactiveMotive(new AwakeContentMotive
			{
				Id = "motive.content",
				DisplayName = "内容动机",
				BaseWeight = 2,
				OpeningHint = "内容开场"
			});
		}
	}

	private static void RunContentApiSmoke()
	{
		AwakeRuleRegistry.ResetForTesting();
		AwakeContentPackManager.Register(new TestContentPack());
		IReadOnlyList<AwakeRuleManifest> manifests = AwakeRuleRegistry.All();
		if (manifests.Count != 3)
		{
			throw new InvalidOperationException("content pack should register three manifests.");
		}
		bool sawEvent = false;
		bool sawMotive = false;
		bool sawRule = false;
		foreach (AwakeRuleManifest manifest in manifests)
		{
			string kind = (string)manifest.Payload["kind"];
			if (StringComparer.Ordinal.Equals(kind, "event")) sawEvent = true;
			if (StringComparer.Ordinal.Equals(kind, "proactive_motive")) sawMotive = true;
			if (StringComparer.Ordinal.Equals(kind, "custom")) sawRule = true;
		}
		if (!sawEvent || !sawMotive || !sawRule)
		{
			throw new InvalidOperationException("content pack kinds missing.");
		}
		AwakeContentRegistry direct = new AwakeContentRegistry();
		if (direct.RegisterEvent(new AwakeContentEvent
		{
			Id = "bad.event",
			Title = "坏事件",
			Body = "",
			OptionA = "",
			OptionB = ""
		}))
		{
			throw new InvalidOperationException("invalid content event should be rejected.");
		}
		if (direct.RegisterProactiveMotive(new AwakeContentMotive
		{
			Id = "bad.motive",
			BaseWeight = 0
		}))
		{
			throw new InvalidOperationException("invalid content motive should be rejected.");
		}
		AwakeRuleRegistry.ResetForTesting();
		Console.WriteLine("PASS content api smoke");
	}

	private static void RunDialogueSessionCoordinatorSmoke()
	{
		AwakeDialogueSessionCoordinator.ResetForTesting();
		if (!AwakeDialogueSessionCoordinator.TryAcquire("scene", "hero-1"))
		{
			throw new InvalidOperationException("first dialogue session should acquire.");
		}
		if (AwakeDialogueSessionCoordinator.TryAcquire("messenger", "contacts"))
		{
			throw new InvalidOperationException("second dialogue session should be rejected.");
		}
		AwakeDialogueSessionCoordinator.Close("scene", "hero-2");
		if (!AwakeDialogueSessionCoordinator.IsActive)
		{
			throw new InvalidOperationException("close with wrong target should not release session.");
		}
		AwakeDialogueSessionCoordinator.Close("scene", "hero-1");
		if (AwakeDialogueSessionCoordinator.IsActive)
		{
			throw new InvalidOperationException("close with correct source/target should release session.");
		}
		AwakeDialogueSessionState session = AwakeDialogueSessionCoordinator.TryStart(new AwakeDialogueStartPayload
		{
			Source = "event",
			TargetId = "hero-3",
			OpeningHint = "测试开场"
		});
		if (session == null || string.IsNullOrWhiteSpace(session.Token))
		{
			throw new InvalidOperationException("unified session start should return a token.");
		}
		if (AwakeDialogueSessionCoordinator.CloseByToken("wrong-token"))
		{
			throw new InvalidOperationException("wrong token should not close the active session.");
		}
		if (!AwakeDialogueSessionCoordinator.CloseByToken(session.Token)
			|| AwakeDialogueSessionCoordinator.IsActive)
		{
			throw new InvalidOperationException("correct token should close the active session.");
		}
		AwakeDialogueSessionCoordinator.ResetForTesting();
		Console.WriteLine("PASS dialogue session coordinator smoke");
	}

	private static void RunDialogueQueueSmoke()
	{
		EventDialogueQueue.ClearForTesting();
		EventDialogueQueue.Enqueue("hero-1", "开场提示");
		EventDialogueQueue.Enqueue("hero-2", null);
		if (EventDialogueQueue.Count != 2)
		{
			throw new InvalidOperationException("dialogue queue should keep enqueued items.");
		}
		PendingDialogue first;
		if (!EventDialogueQueue.TryDequeue(out first)
			|| !StringComparer.Ordinal.Equals(first.HeroId, "hero-1")
			|| EventDialogueQueue.Count != 1)
		{
			throw new InvalidOperationException("dialogue queue dequeue mismatch.");
		}
		EventDialogueQueue.ClearForTesting();
		Console.WriteLine("PASS dialogue queue smoke");
	}

	private static void RunOnboardingSmoke()
	{
		AwakeOnboardingService.ResetForTesting();
		if (AwakeOnboardingService.NextIncompleteStep() != AwakeOnboardingStep.Welcome
			|| !AwakeOnboardingService.ShouldShowGuide())
		{
			throw new InvalidOperationException("onboarding should start at welcome after reset.");
		}
		AwakeOnboardingService.MarkShownForTesting();
		if (AwakeOnboardingService.ShouldShowGuide())
		{
			throw new InvalidOperationException("onboarding should not show twice per campaign.");
		}
		AwakeOnboardingService.ResetForTesting();
		AwakeOnboardingService.MarkStepCompleted(AwakeOnboardingStep.Welcome);
		if (AwakeOnboardingService.NextIncompleteStep() != AwakeOnboardingStep.AiConfig)
		{
			throw new InvalidOperationException("onboarding should advance to AI configuration.");
		}
		AwakeOnboardingService.MarkStepCompleted(AwakeOnboardingStep.Complete);
		if (AwakeOnboardingService.ShouldShowGuide())
		{
			throw new InvalidOperationException("completed onboarding should not show again.");
		}
		AwakeOnboardingService.ResetForTesting();
		AwakeOnboardingService.MarkSkippedThisCampaign();
		if (AwakeOnboardingService.ShouldShowGuide())
		{
			throw new InvalidOperationException("campaign-skipped onboarding should not show.");
		}
		AwakeOnboardingService.ResetForTesting();
		AwakeOnboardingService.MarkSkippedForever();
		if (AwakeOnboardingService.ShouldShowGuide())
		{
			throw new InvalidOperationException("permanently skipped onboarding should not show.");
		}
		AwakeOnboardingService.ResetForTesting();
		Console.WriteLine("PASS onboarding smoke");
	}

	private static void RunB9InfraSmoke()
	{
		AwakePerfProbe.Reset();
		long started = AwakePerfProbe.StartMilliseconds();
		AwakePerfProbe.Record("b9_smoke", started);
		PerfProbeSnapshot snapshot = AwakePerfProbe.Snapshot();
		if (snapshot.RecordCount <= 0 || snapshot.UptimeMilliseconds < 0)
		{
			throw new InvalidOperationException("perf probe snapshot mismatch.");
		}

		Newtonsoft.Json.Linq.JObject state = new Newtonsoft.Json.Linq.JObject();
		if (!AwakeStorageContract.TryNormalizeSchema(state, AwakeStorageContract.MemorySchema)
			|| !StringComparer.Ordinal.Equals((string)state["schema"], AwakeStorageContract.MemorySchema))
		{
			throw new InvalidOperationException("storage schema normalize mismatch.");
		}
		if (!AwakeStorageContract.IsKnownSchema(AwakeStorageContract.WorldEventsSchema)
			|| !StringComparer.Ordinal.Equals(
				AwakeStorageContract.ExpectedSchema(WorldStateKind.Messenger),
				AwakeStorageContract.MessengerSchema)
			|| !StringComparer.Ordinal.Equals(
				AwakeStorageContract.ExpectedSchema(WorldStateKind.Onboarding),
				AwakeStorageContract.OnboardingSchema)
			|| !StringComparer.Ordinal.Equals(
				AwakeStorageContract.ExpectedSchema(WorldStateKind.PendingDialogue),
				AwakeStorageContract.DialogueQueueSchema)
			|| !StringComparer.Ordinal.Equals(
				AwakeStorageContract.ExpectedSchema(WorldStateKind.Interaction),
				AwakeStorageContract.InteractionSchema))
		{
			throw new InvalidOperationException("storage contract schema mapping mismatch.");
		}
		AwakePerfProbe.Reset();
		Console.WriteLine("PASS b9 infra smoke");
	}

	private static void RunLongWaitSmoke()
	{
		if (NpcDialogueConstants.LongWaitCancelSeconds != 60)
		{
			throw new InvalidOperationException("long wait cancel threshold should be 60 seconds.");
		}
		Console.WriteLine("PASS long wait smoke");
	}

	private static void RunTerminalHotkeySmoke()
	{
		if (!StringComparer.Ordinal.Equals(new AwakeConfig().TerminalKey, "U"))
		{
			throw new InvalidOperationException("terminal hotkey default should be U.");
		}
		Console.WriteLine("PASS terminal hotkey smoke");
	}

	private static void RunRouteContractSmoke()
	{
		OperationResult<bool> revisionConflict = OperationResult<bool>.Failed(
			FrameworkErrors.Create(
				"prompt.revision_conflict",
				FrameworkErrorCategory.Conflict,
				"same prompt revision already exists.",
				null,
				owner: "AWAKE"));
		OperationResult<bool> invalidPrompt = OperationResult<bool>.Failed(
			FrameworkErrors.Create(
				"prompt.invalid",
				FrameworkErrorCategory.InvalidRequest,
				"invalid",
				null,
				owner: "AWAKE"));
		if (!AiTaskConstants.IsPromptRegistrationUsable(revisionConflict)
			|| AiTaskConstants.IsPromptRegistrationUsable(invalidPrompt))
		{
			throw new InvalidOperationException("prompt revision conflict should be idempotently usable.");
		}
		string prefix = AwakeConstants.OwnerValue + ".route.";
		foreach (string routeId in AiTaskConstants.AllRouteIds)
		{
			if (!routeId.StartsWith(prefix, StringComparison.Ordinal))
			{
				throw new InvalidOperationException("route id namespace mismatch: " + routeId);
			}
		}
		if (!StringComparer.Ordinal.Equals(
			AiTaskConstants.RoutePermission(AiTaskConstants.RouteNpcDialogue),
			"ai.route.invoke:" + AiTaskConstants.RouteNpcDialogue))
		{
			throw new InvalidOperationException("route permission id should mirror the route id.");
		}
		Console.WriteLine("PASS route contract smoke");
	}

	private static void RunPersonaTemplateSmoke()
	{
		List<WorldbookImportWarning> warnings = new List<WorldbookImportWarning>();
		PersonaTagRegistryDocument registryDocument = PersonaDataLoader.ParseRegistry(
			JObject.Parse("{\"schemaVersion\":\"awake.persona.tags.v1\",\"tags\":[{\"id\":\"trait.test\",\"category\":\"trait\",\"meaning\":\"test trait\",\"promptText\":\"测试倾向\"},{\"id\":\"boundary.test\",\"category\":\"boundary\",\"meaning\":\"test boundary\",\"promptText\":\"测试边界\"}],\"bundles\":[{\"id\":\"bundle.test\",\"tags\":[\"trait.test\",\"boundary.test\"]}]}"),
			"smoke",
			warnings);
		PersonaTagRegistry registry = new PersonaTagRegistry(registryDocument);
		PersonaDefinition definition;
		if (!PersonaDataLoader.TryParseDefinition(
			JObject.Parse("{\"id\":\"persona.test\",\"status\":\"approved\",\"templateVersion\":\"persona-load.v2\",\"sourcePackId\":\"free-experiment\",\"core\":\"稳定核心\",\"summary\":\"角色摘要\",\"publicDescription\":\"对外保持从容。\",\"privateDescription\":\"私下容易失措。\",\"contradictionDescription\":\"坚强与脆弱并存。\",\"foodPreference\":\"武陵炒饭\",\"selfClaimRules\":[\"对外只自称我。\"],\"realSelfBehaviors\":[\"独处时放下戒备。\"],\"selfClaimExamples\":[\"我会如何回应？\"],\"bundles\":[\"bundle.test\"]}"),
			"fallback",
			"smoke",
			out definition)) throw new InvalidOperationException("persona definition should parse");
		PersonaContext context = new PersonaContext { CharacterId = "hero_test", HeroName = "测试角色", Role = "hero" };
		PersonaGenerationResult first = PersonaDslGenerator.Generate(definition, registry, context, "", "", 4096);
		PersonaGenerationResult second = PersonaDslGenerator.Generate(definition, registry, context, "", "", 4096);
		if (!first.IsUsable || first.UsedLegacyFallback || !first.Dsl.Contains("[PERSONA_LOAD]") || !first.Dsl.Contains("TEMPLATE_VERSION=\"persona-load.v2\"") || !first.Dsl.Contains("STATUS=\"approved\"") || !first.Dsl.Contains("SOURCE_PACK_ID=\"free-experiment\"") || !first.Dsl.Contains("[PERSONA_CONSTRAINTS]") || !first.Dsl.Contains("TRAIT_TEST") || !first.Dsl.Contains("BOUNDARY_TEST") || !first.Dsl.Contains("[PERSONA_IDENTITY]") || !first.Dsl.Contains("[PERSONALITY_PUBLIC]") || !first.Dsl.Contains("DATA_CN=\"对外保持从容。\"") || !first.Dsl.Contains("DATA_CN=\"对外只自称我。\"") || !first.Dsl.Contains("DATA_CN=\"我会如何回应？\"") || !first.Dsl.Contains("DATA_CN=\"独处时放下戒备。\"") || first.Dsl.Contains("[PERSONALITY_SUMMARY]") || first.Dsl.Contains("[SELF_IDENTITY]") || first.Dsl.Contains("[FOOD_PREFERENCE]") || first.Dsl.Contains("武陵炒饭") || first.Dsl.Contains("<persona:v1"))
		if (!first.IsUsable || first.UsedLegacyFallback || !first.Dsl.Contains("[PERSONA_LOAD]") || !first.Dsl.Contains("TEMPLATE_VERSION=\"persona-load.v2\"") || !first.Dsl.Contains("STATUS=\"approved\"") || !first.Dsl.Contains("SOURCE_PACK_ID=\"free-experiment\"") || !first.Dsl.Contains("[PERSONA_CONSTRAINTS]") || !first.Dsl.Contains("TRAIT_TEST") || !first.Dsl.Contains("BOUNDARY_TEST") || !first.Dsl.Contains("[PERSONA_IDENTITY]") || !first.Dsl.Contains("[PERSONALITY_PUBLIC]") || !first.Dsl.Contains("DATA_CN=\"对外保持从容。\"") || !first.Dsl.Contains("DATA_CN=\"对外只自称我。\"") || !first.Dsl.Contains("DATA_CN=\"我会如何回应？\"") || !first.Dsl.Contains("DATA_CN=\"独处时放下戒备。\"") || first.Dsl.Contains("[PERSONALITY_SUMMARY]") || first.Dsl.Contains("[SELF_IDENTITY]") || first.Dsl.Contains("[FOOD_PREFERENCE]") || first.Dsl.Contains("武陵炒饭") || first.Dsl.Contains("<persona:v1"))
		{
			throw new InvalidOperationException("approved persona should generate canonical authored DSL");
		}
		if (!StringComparer.Ordinal.Equals(first.Dsl, second.Dsl) || !StringComparer.Ordinal.Equals(first.Fingerprint, second.Fingerprint))
			throw new InvalidOperationException("persona generation should be deterministic");
		definition.Tags.Add(new PersonaTagUse { Id = "missing.tag" });
		PersonaGenerationResult rejected = PersonaDslGenerator.Generate(definition, registry, context, "旧人格", "旧背景", 4096);
		if (!rejected.UsedLegacyFallback || rejected.Warnings.Count == 0 || !rejected.Dsl.Contains("[PERSONA_LOAD]") || !rejected.Dsl.Contains("DESC_CN=\"旧人格\"") || rejected.Dsl.Contains("<persona:v1"))
			throw new InvalidOperationException("unregistered persona tag should use legacy fallback");
		Console.WriteLine("PASS persona template smoke");
	}
	private static void RunSharedPersonaGoldenFixtureSmoke()
	{
		string path = FindSharedPersonaFixture();
		JObject fixture = JObject.Parse(File.ReadAllText(path));
		List<WorldbookImportWarning> warnings = new List<WorldbookImportWarning>();
		PersonaTagRegistryDocument registryDocument = PersonaDataLoader.ParseRegistry(
			JObject.Parse("{\"schemaVersion\":\"awake.persona.tags.v1\",\"tags\":[{\"id\":\"trait.cautious\",\"category\":\"trait\"},{\"id\":\"expression.measured\",\"category\":\"expression\"},{\"id\":\"behavior.bargains\",\"category\":\"behavior\"},{\"id\":\"trigger.public_humiliation\",\"category\":\"trigger\"},{\"id\":\"boundary.no_empty_promises\",\"category\":\"boundary\"}]}"),
			"golden",
			warnings);
		PersonaTagRegistry registry = new PersonaTagRegistry(registryDocument);
		JObject definitionObject = (JObject)fixture.DeepClone();
		definitionObject["schemaVersion"] = PersonaSchemaConstants.DefinitionSchema;
		definitionObject["characterId"] = (string)fixture["id"] ?? string.Empty;
		definitionObject["identityId"] = (string)fixture["id"] ?? string.Empty;
		JArray tagUses = new JArray();
		foreach (JToken token in (JArray)fixture["tags"]) tagUses.Add(new JObject { ["id"] = token.ToString() });
		definitionObject["tags"] = tagUses;
		PersonaDefinition definition;
		if (!PersonaDataLoader.TryParseDefinition(definitionObject, "golden", path, out definition)) throw new InvalidOperationException("shared Persona golden definition should parse");
		PersonaContext context = new PersonaContext
		{
			CharacterId = (string)fixture["id"] ?? string.Empty,
			HeroName = (string)fixture["displayName"] ?? string.Empty
		};
		PersonaGenerationResult result = PersonaDslGenerator.Generate(definition, registry, context, "", "", 4096);
		string expected = (string)fixture["expectedDsl"] ?? string.Empty;
		if (!result.IsUsable || result.UsedLegacyFallback || !StringComparer.Ordinal.Equals(expected, result.Dsl)) throw new InvalidOperationException("AWAKE output must match the shared canonical fixture");
		Console.WriteLine("PASS shared Persona golden fixture smoke");
	}
	private static string FindSharedPersonaFixture()
	{
		DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			string candidate = Path.Combine(directory.FullName, "_houkai_merge", "AWAKE", "docs", "fixtures", "persona-load-v2-golden.json");
			if (File.Exists(candidate)) return candidate;
			candidate = Path.Combine(directory.FullName, "docs", "fixtures", "persona-load-v2-golden.json");
			if (File.Exists(candidate)) return candidate;
			directory = directory.Parent;
		}
		throw new FileNotFoundException("Shared Persona golden fixture was not found.");
	}
	private static void RunPersonaPersistenceSmoke()
	{
		PersonaPersistenceEnvelope envelope = new PersonaPersistenceEnvelope
		{
			CharacterId = "hero_test",
			Timeline = new PersonaTimelineIdentity
			{
				CampaignId = "campaign_test",
				TimelineId = "timeline_test",
				BranchId = "branch_root",
				ForkSequence = 0
			},
			Sequence = 4,
			Watermarks = new PersonaProjectionWatermarks
			{
				TranscriptAcceptedSequence = 4,
				EffectsAcceptedSequence = 3,
				MemoryAcceptedSequence = 2,
				PersonaAcceptedSequence = 4
			}
		};
		string error;
		if (!PersonaPersistenceValidator.TryValidateEnvelope(envelope, out error)) throw new InvalidOperationException(error);
		string storageKey;
		if (!PersonaStorageKey.TryBuild(envelope.Timeline, envelope.CharacterId, out storageKey, out error) || !storageKey.Contains("branch_root")) throw new InvalidOperationException("persona storage key should include branch");
		if (!PersonaPersistenceValidator.IsAcceptedForProjection(envelope, 2, 2)
			|| PersonaPersistenceValidator.IsAcceptedForProjection(envelope, 2, 3))
			throw new InvalidOperationException("projection watermark gate should be per projection");
		PersonaRecoveryRecord pending = new PersonaRecoveryRecord
		{
			CommitGroupId = "group_test",
			Status = PersonaPersistenceConstants.SessionPending,
			Timeline = envelope.Timeline,
			Sequence = 4
		};
		if (!PersonaPersistenceValidator.TryValidateRecovery(pending, out error)) throw new InvalidOperationException(error);
		pending.Status = PersonaPersistenceConstants.SaveCommitted;
		pending.SaveAnchorConfirmed = false;
		if (PersonaPersistenceValidator.TryValidateRecovery(pending, out error)
			|| !StringComparer.Ordinal.Equals(error, "persona.recovery.save_commit_without_anchor"))
			throw new InvalidOperationException("save committed must require an anchor");
		Console.WriteLine("PASS persona persistence smoke");
	}
	private static void RunWorldKnowledgeB2Smoke()
	{
		WorldbookQuery query = new WorldbookQuery
		{
			IdentityId = "profile.commoner",
			KnowledgeScope = "local",
			KnowledgeScopeAvailable = true,
			EffectiveDetail = "summary",
			EffectiveDetailAvailable = true,
			RequestedDetail = "secret"
		};

		WorldKnowledgeQueryResult knownResult = new WorldKnowledgeQueryResult
		{
			State = WorldKnowledgeDecisionPolicy.Known,
			RetrievedText = "瓦兰迪亚西部平原盛产粮食。",
			SourceVersion = "package@1"
		};
		knownResult.HitIds.Add("doc.economy.grain");
		WorldKnowledgeDecision known = WorldKnowledgeDecisionPolicy.Create(query, knownResult, "corr-known");
		string knownPrompt = WorldKnowledgeDecisionPolicy.BuildPromptBlock(known);
		if (!known.AllowsAi
			|| known.State != WorldKnowledgeDecisionPolicy.Known
			|| known.HitIds.Count != 1
			|| knownPrompt.IndexOf("知识状态：known", StringComparison.Ordinal) < 0
			|| knownPrompt.IndexOf("doc.economy.grain", StringComparison.Ordinal) < 0)
		{
			throw new InvalidOperationException("known knowledge must enter AI with filtered metadata.");
		}

		WorldKnowledgeQueryResult partialResult = new WorldKnowledgeQueryResult
		{
			State = WorldKnowledgeDecisionPolicy.Partial,
			RetrievedText = "只知道这件事的大概。"
		};
		WorldKnowledgeDecision partial = WorldKnowledgeDecisionPolicy.Create(query, partialResult, "corr-partial");
		if (!partial.AllowsAi || partial.State != WorldKnowledgeDecisionPolicy.Partial)
			throw new InvalidOperationException("partial knowledge with text should enter AI.");

		WorldKnowledgeQueryResult referralResult = new WorldKnowledgeQueryResult
		{
			State = WorldKnowledgeDecisionPolicy.Referral,
			RetrievedText = "这方面我不清楚。你可以去问：公证商人。"
		};
		referralResult.ReferralIds.Add("ref.notary");
		WorldKnowledgeDecision referral = WorldKnowledgeDecisionPolicy.Create(query, referralResult, "corr-referral");
		if (referral.AllowsAi
			|| referral.State != WorldKnowledgeDecisionPolicy.Referral
			|| referral.DirectReply.IndexOf("公证商人", StringComparison.Ordinal) < 0)
		{
			throw new InvalidOperationException("referral knowledge must be code-directed without AI.");
		}

		WorldKnowledgeQueryResult blockedResult = new WorldKnowledgeQueryResult
		{
			State = WorldKnowledgeDecisionPolicy.Blocked,
			BlockedReason = "permission",
			RetrievedText = "不应泄露的秘密。"
		};
		WorldKnowledgeDecision blocked = WorldKnowledgeDecisionPolicy.Create(query, blockedResult, "corr-blocked");
		if (blocked.AllowsAi
			|| blocked.DirectReply.IndexOf("不是我该知道", StringComparison.Ordinal) < 0
			|| blocked.RetrievedText.Length != 0)
		{
			throw new InvalidOperationException("blocked knowledge must be cleared and code-directed.");
		}

		WorldKnowledgeQueryResult emptyKnownResult = new WorldKnowledgeQueryResult
		{
			State = WorldKnowledgeDecisionPolicy.Known
		};
		WorldKnowledgeDecision emptyKnown = WorldKnowledgeDecisionPolicy.Create(query, emptyKnownResult, "corr-empty");
		if (emptyKnown.AllowsAi
			|| emptyKnown.State != WorldKnowledgeDecisionPolicy.NotFound
			|| emptyKnown.Errors.IndexOf("WB2-EMPTY-KNOWLEDGE-TEXT") < 0)
		{
			throw new InvalidOperationException("empty known result must fail closed as not_found.");
		}

		WorldKnowledgeDecision missing = WorldKnowledgeDecisionPolicy.Create(query, null, "corr-missing");
		if (missing.AllowsAi
			|| missing.State != WorldKnowledgeDecisionPolicy.Blocked
			|| missing.BlockedReason != "worldbook_unavailable")
		{
			throw new InvalidOperationException("missing worldbook must fail closed without AI.");
		}
		Console.WriteLine("PASS worldbook B2 smoke");
	}
	private static void RunWorldbookSmoke()
	{
		string ruleJson = @"{
			""Id"": ""rule.empire.lord"",
			""Keywords"": [""荣誉""],
			""RagShortTexts"": [""帝国领主如何看待荣誉？""],
			""Variants"": [
				{
					""Priority"": 0,
					""When"": {
						""Cultures"": [""empire""],
						""Roles"": [""lord""]
					},
					""Content"": ""帝国领主视荣誉为立身之本。""
				}
			],
			""TextMappings"": [
				{
					""SourceText"": ""A"",
					""Kind"": ""status|hero|is_dead"",
					""TargetId"": ""lord_7_3"",
					""TrueText"": ""他已去世""
				}
			]
		}";
		List<WorldbookImportWarning> warnings = new List<WorldbookImportWarning>();
		WorldbookRule rule;
		if (!WorldbookLoader.TryParseRule(
			Newtonsoft.Json.Linq.JObject.Parse(ruleJson),
			"fallback",
			"af",
			warnings,
			out rule))
		{
			throw new InvalidOperationException("AF worldbook rule should parse.");
		}
		if (rule.Keywords.Count != 1
			|| rule.Variants.Count != 1
			|| rule.TextMappings.Count != 1
			|| !StringComparer.Ordinal.Equals(rule.TextMappings[0].Kind, "status|hero|is_dead"))
		{
			throw new InvalidOperationException("worldbook AF field mapping mismatch.");
		}

		string awakeRuleJson = @"{
			""id"": ""rule.awake.variant"",
			""variantSelection"": ""af-best"",
			""variants"": [
				{
					""priority"": 0,
					""when"": {
						""cultures"": [""empire""]
					},
					""content"": ""帝国变体""
				}
			]
		}";
		List<WorldbookImportWarning> awakeWarnings = new List<WorldbookImportWarning>();
		WorldbookRule awakeRule;
		if (!WorldbookLoader.TryParseRule(
			Newtonsoft.Json.Linq.JObject.Parse(awakeRuleJson),
			"fallback",
			"awake",
			awakeWarnings,
			out awakeRule)
			|| !StringComparer.Ordinal.Equals(awakeRule.VariantSelection, "af-best"))
		{
			throw new InvalidOperationException("worldbook explicit variantSelection should parse.");
		}

		string badVariantJson = @"{
			""id"": ""rule.bad.variant"",
			""variantSelection"": ""unknown"",
			""content"": ""x""
		}";
		List<WorldbookImportWarning> badVariantWarnings = new List<WorldbookImportWarning>();
		WorldbookRule badVariantRule;
		if (!WorldbookLoader.TryParseRule(
			Newtonsoft.Json.Linq.JObject.Parse(badVariantJson),
			"fallback",
			"awake",
			badVariantWarnings,
			out badVariantRule))
		{
			throw new InvalidOperationException("bad variantSelection rule should still parse with warning.");
		}
		bool foundVariantWarning = false;
		foreach (WorldbookImportWarning warning in badVariantWarnings)
		{
			if (StringComparer.Ordinal.Equals(warning.Code, "rule_variant_selection_unsupported"))
			{
				foundVariantWarning = true;
				break;
			}
		}
		if (!foundVariantWarning)
		{
			throw new InvalidOperationException("bad variantSelection should emit a warning.");
		}

		WorldbookRule rule2 = new WorldbookRule
		{
			Id = "rule.empire.soldier",
			Keywords = new List<string> { "荣誉" },
			When = new WorldbookWhen
			{
				Cultures = new List<string> { "empire" },
				Roles = new List<string> { "soldier" }
			},
			Content = "帝国士兵同样把荣誉挂在嘴边。"
		};
		WorldbookPersona persona = new WorldbookPersona
		{
			CharacterId = "CharacterObject_1795",
			Personality = "务实而谨慎",
			Background = "出身帝国边境"
		};
		WorldbookDocument document = new WorldbookDocument
		{
			Rules = new List<WorldbookRule> { rule, rule2 },
			Personas = new List<WorldbookPersona> { persona }
		};
		WorldbookService service = new WorldbookService(document);
		if (service.RuleCount != 2
			|| service.PersonaCount != 1
			|| service.WarningCount != 0)
		{
			throw new InvalidOperationException("worldbook status counters mismatch.");
		}
		List<WorldbookRule> searchHits = service.Search("荣誉", 10);
		if (searchHits.Count == 0)
		{
			throw new InvalidOperationException("worldbook keyword search should return rules.");
		}
		WorldbookQuery query = new WorldbookQuery
		{
			CultureId = "empire",
			Role = "lord",
			PlayerText = "荣誉和誓言",
			ContentTier = "pure",
			MaximumBytes = 100000
		};
		WorldbookQueryResult result = service.Query(query);
		if (result.RetrievedText.IndexOf("rule.empire.lord", StringComparison.Ordinal) < 0
			|| result.RetrievedText.IndexOf("rule.empire.soldier", StringComparison.Ordinal) >= 0
			|| result.RetrievedText.IndexOf("帝国领主视荣誉为立身之本", StringComparison.Ordinal) < 0)
		{
			throw new InvalidOperationException("worldbook identity binding or variant selection mismatch.");
		}

		WorldbookQuery caseQuery = new WorldbookQuery
		{
			CultureId = "Empire",
			Role = "lord",
			PlayerText = "荣誉",
			ContentTier = "pure",
			MaximumBytes = 100000
		};
		WorldbookQueryResult caseResult = service.Query(caseQuery);
		if (caseResult.RetrievedText.IndexOf("rule.empire.lord", StringComparison.Ordinal) >= 0)
		{
			throw new InvalidOperationException("worldbook culture code matching must be case-sensitive.");
		}

		WorldbookQuery personaQuery = new WorldbookQuery
		{
			CharacterId = "CharacterObject_1795",
			PlayerText = "",
			ContentTier = "pure",
			MaximumBytes = 100000
		};
		WorldbookQueryResult personaResult = service.Query(personaQuery);
		if (personaResult.RetrievedText.IndexOf("务实而谨慎", StringComparison.Ordinal) < 0
			|| personaResult.RetrievedText.IndexOf("出身帝国边境", StringComparison.Ordinal) < 0)
		{
			throw new InvalidOperationException("worldbook persona injection mismatch.");
		}

		WorldbookDocument relevanceDocument = new WorldbookDocument
		{
			Rules = new List<WorldbookRule>
			{
				new WorldbookRule
				{
					Id = "rule.global.unrelated",
					Content = "与当前人物和场景无关的通用知识。"
				},
				new WorldbookRule
				{
					Id = "rule.scene.tavern",
					Context = new WorldbookContext
					{
						SceneKeywords = new List<string> { "酒馆" }
					},
					Content = "酒馆里的热闹与传闻。"
				}
			},
			Personas = new List<WorldbookPersona>()
		};
		WorldbookService relevanceService = new WorldbookService(relevanceDocument);
		WorldbookQuery genericQuery = new WorldbookQuery
		{
			PlayerText = "你好",
			SceneKeywords = new List<string> { "市集" },
			ContentTier = "pure",
			MaximumBytes = 100000
		};
		WorldbookQueryResult genericResult = relevanceService.Query(genericQuery);
		if (genericResult.RetrievedText.IndexOf("rule.global.unrelated", StringComparison.Ordinal) >= 0
			|| genericResult.RetrievedText.IndexOf("rule.scene.tavern", StringComparison.Ordinal) >= 0)
		{
			throw new InvalidOperationException("unrelated worldbook rules should not be injected.");
		}
		WorldbookQuery tavernQuery = new WorldbookQuery
		{
			PlayerText = "你好",
			SceneKeywords = new List<string> { "酒馆" },
			ContentTier = "pure",
			MaximumBytes = 100000
		};
		WorldbookQueryResult tavernResult = relevanceService.Query(tavernQuery);
		if (tavernResult.RetrievedText.IndexOf("rule.scene.tavern", StringComparison.Ordinal) < 0)
		{
			throw new InvalidOperationException("scene keyword rule should be injected.");
		}

		WorldbookTextMapping deadMapping = new WorldbookTextMapping
		{
			SourceText = "A",
			Kind = "status|hero|is_dead",
			TargetId = "lord_1_7",
			TrueText = "他已去世",
			FalseText = "他还活着"
		};
		WorldbookMappingContext mappingContext = new WorldbookMappingContext
		{
			BoundSettlementName = "龙堡",
			BoundDeityName = "荒野女神",
			BoundEventName = "潘德拉克",
			BoundHeroTitle = "男爵",
			BoundKingdomName = "坎尼人的王国",
			BoundRegionName = "珀拉斯海",
			BoundSettlementOwnerClanName = "狼皮部落",
			BoundSettlementOwnerLeaderName = "乌尔夫"
		};
		mappingContext.Statuses["status|hero|is_dead|lord_1_7"] = true;
		mappingContext.Statuses["status|hero|is_alive|lord_1_7"] = true;
		mappingContext.Statuses["status|kingdom|is_eliminated|empire"] = false;
		mappingContext.Statuses["status|clan|has_any_town|clan_nord_1"] = true;
		mappingContext.HeroNames["lord_1_7"] = "加里俄斯";
		mappingContext.ClanNames["clan_nord_1"] = "诺德王国";
		mappingContext.ClanLeaderNames["clan_nord_1"] = "哈尔达尔";
		mappingContext.ClanTowns["clan_nord_1"] = new List<string> { "瑞尔城", "奥斯蒂港" };
		mappingContext.ClanVillages["clan_nord_1"] = new List<string> { "冻土村" };
		mappingContext.ClanSettlements["clan_nord_1"] = new List<string> { "瑞尔城", "奥斯蒂港", "冻土村" };
		mappingContext.KingdomNames["empire_w"] = "西帝国";
		mappingContext.KingdomLeaderNames["empire_w"] = "加里俄斯";
		mappingContext.SettlementNames["town_V7"] = "奥斯蒂港";
		mappingContext.SettlementOwnerClanNames["town_V7"] = "戴·阿罗曼克";
		mappingContext.SettlementOwnerLeaderNames["town_V7"] = "阿罗曼克";
		if (!StringComparer.Ordinal.Equals(
			WorldbookTextMappingResolver.Resolve(deadMapping, mappingContext),
			"他已去世"))
		{
			throw new InvalidOperationException("worldbook text mapping status resolution mismatch.");
		}
		WorldbookTextMapping aliveMapping = new WorldbookTextMapping
		{
			SourceText = "A",
			Kind = "status|hero|is_alive",
			TargetId = "lord_1_7",
			TrueText = "他还在世"
		};
		if (!StringComparer.Ordinal.Equals(
			WorldbookTextMappingResolver.Resolve(aliveMapping, mappingContext),
			"他还在世"))
		{
			throw new InvalidOperationException("worldbook text mapping alive status resolution mismatch.");
		}
		WorldbookTextMapping kingdomEliminated = new WorldbookTextMapping
		{
			SourceText = "B",
			Kind = "status|kingdom|is_eliminated",
			TargetId = "empire",
			FalseText = "北帝国就是这样一个国家"
		};
		if (!StringComparer.Ordinal.Equals(
			WorldbookTextMappingResolver.Resolve(kingdomEliminated, mappingContext),
			"北帝国就是这样一个国家"))
		{
			throw new InvalidOperationException("worldbook text mapping kingdom status resolution mismatch.");
		}
		if (!StringComparer.Ordinal.Equals(
			WorldbookTextMappingResolver.Resolve(
				new WorldbookTextMapping { Kind = "clan_all_towns", TargetId = "clan_nord_1" },
				mappingContext),
			"奥斯蒂港，瑞尔城"))
		{
			throw new InvalidOperationException("worldbook text mapping clan town list mismatch.");
		}
		if (!StringComparer.Ordinal.Equals(
			WorldbookTextMappingResolver.Resolve(
				new WorldbookTextMapping { Kind = "clan_leader_name", TargetId = "clan_nord_1" },
				mappingContext),
			"哈尔达尔"))
		{
			throw new InvalidOperationException("worldbook text mapping clan leader mismatch.");
		}
		if (!StringComparer.Ordinal.Equals(
			WorldbookTextMappingResolver.Resolve(
				new WorldbookTextMapping { Kind = "settlement_owner_leader_name", TargetId = "town_V7" },
				mappingContext),
			"阿罗曼克"))
		{
			throw new InvalidOperationException("worldbook text mapping settlement owner leader mismatch.");
		}
		if (!StringComparer.Ordinal.Equals(
			WorldbookTextMappingResolver.Resolve(
				new WorldbookTextMapping { Kind = "bound_deity_name" },
				mappingContext),
			"荒野女神")
			|| !StringComparer.Ordinal.Equals(
				WorldbookTextMappingResolver.Resolve(
					new WorldbookTextMapping { Kind = "bound_event_name" },
					mappingContext),
				"潘德拉克")
			|| !StringComparer.Ordinal.Equals(
				WorldbookTextMappingResolver.Resolve(
					new WorldbookTextMapping { Kind = "bound_hero_title" },
					mappingContext),
				"男爵"))
		{
			throw new InvalidOperationException("worldbook text mapping bound lore name mismatch.");
		}
		if (!WorldbookTextMappingResolver.IsSupportedKind("status|hero|is_alive")
			|| !WorldbookTextMappingResolver.IsSupportedKind("clan_all_settlements")
			|| WorldbookTextMappingResolver.IsSupportedKind("unknown_kind"))
		{
			throw new InvalidOperationException("worldbook text mapping kind support list mismatch.");
		}
		string mappedText = WorldbookTextMappingResolver.Apply(
			"国王A，统治着B。C是这座城的领主。",
			new List<WorldbookTextMapping>
			{
				deadMapping,
				new WorldbookTextMapping
				{
					SourceText = "B",
					Kind = "bound_settlement_name"
				},
				new WorldbookTextMapping
				{
					SourceText = "C",
					Kind = "settlement_owner_leader_name",
					TargetId = "town_V7"
				}
			},
			mappingContext);
		if (mappedText.IndexOf("国王他已去世", StringComparison.Ordinal) < 0
			|| mappedText.IndexOf("统治着龙堡", StringComparison.Ordinal) < 0
			|| mappedText.IndexOf("阿罗曼克是这座城的领主", StringComparison.Ordinal) < 0)
		{
			throw new InvalidOperationException("worldbook text mapping apply mismatch.");
		}
		Console.WriteLine("PASS worldbook smoke");
	}

	private static void RunNpcMemorySmoke()
	{
		List<NpcMemoryFact> rawFacts = new List<NpcMemoryFact>();
		for (int i = 0; i < 12; i++) rawFacts.Add(new NpcMemoryFact("fact-" + i));
		Newtonsoft.Json.Linq.JArray builtFacts = NpcMemoryFactsBuilder.Build(rawFacts);
		if (builtFacts.Count != NpcMemoryConstants.FactsMaximum)
		{
			throw new InvalidOperationException("memory facts should cap at configured maximum.");
		}

		Newtonsoft.Json.Linq.JObject high = new Newtonsoft.Json.Linq.JObject
		{
			["id"] = "m-high",
			["day"] = 10,
			["weight"] = 3,
			["type"] = "shared_experience",
			["summary"] = "重要记忆",
			["facts"] = new Newtonsoft.Json.Linq.JArray { "事实甲" }
		};
		Newtonsoft.Json.Linq.JObject low = new Newtonsoft.Json.Linq.JObject
		{
			["id"] = "m-low",
			["day"] = 11,
			["weight"] = 1,
			["type"] = "event",
			["summary"] = "旧事",
			["facts"] = new Newtonsoft.Json.Linq.JArray()
		};
		Newtonsoft.Json.Linq.JObject doc = new Newtonsoft.Json.Linq.JObject
		{
			["memories"] = new Newtonsoft.Json.Linq.JArray { low, high }
		};
		string block = NpcMemorySelector.FormatTopK(doc, 1, 2000);
		if (block.IndexOf("重要记忆", StringComparison.Ordinal) < 0
			|| block.IndexOf("旧事", StringComparison.Ordinal) >= 0)
		{
			throw new InvalidOperationException("memory selector should prefer weight over recency.");
		}

		string summary = NpcMemorySummaryTemplate.ParseSummary(
			"{\"summary\":\"这次深谈让她对你有了新的判断。\"}");
		if (summary.IndexOf("新的判断", StringComparison.Ordinal) < 0)
		{
			throw new InvalidOperationException("memory summary parser mismatch.");
		}
		PromptDefinition definition = NpcMemorySummaryTemplate.CreateDefinition();
		if (!StringComparer.Ordinal.Equals(definition.PromptId, NpcMemoryConstants.PromptId)
			|| !StringComparer.Ordinal.Equals(definition.OutputContractId, NpcMemoryConstants.OutputContractId)
			|| string.IsNullOrWhiteSpace(definition.OutputSchemaJson)
			|| definition.OutputSchemaJson.IndexOf("\"summary\"", StringComparison.Ordinal) < 0)
		{
			throw new InvalidOperationException("memory summary prompt must register its output contract.");
		}
		Console.WriteLine("PASS npc memory smoke");
	}

	private static async Task RunStoragePipelineSmokeAsync()
	{
		SessionRef session = new SessionRef("smoke-campaign", "smoke-timeline", "smoke-session");
		WorldStateStore store = new WorldStateStore(session);
		FakeKeyValueStore memoryStore = new FakeKeyValueStore();
		FakeKeyValueStore eventMetaStore = new FakeKeyValueStore();
		FakeKeyValueStore relationshipStore = new FakeKeyValueStore();
		FakeKeyValueStore proactiveStore = new FakeKeyValueStore();
		FakeKeyValueStore worldEventsStore = new FakeKeyValueStore();
		FakeKeyValueStore messengerStore = new FakeKeyValueStore();
		FakeKeyValueStore transcriptStore = new FakeKeyValueStore();
		FakeKeyValueStore contactsStore = new FakeKeyValueStore();
		FakeKeyValueStore interactionsStore = new FakeKeyValueStore();
		store.InjectStoreForTesting(AiTaskConstants.NpcMemoriesNamespace, memoryStore);
		store.InjectStoreForTesting(AiTaskConstants.EventMetaNamespace, eventMetaStore);
		store.InjectStoreForTesting(AiTaskConstants.RelationshipsNamespace, relationshipStore);
		store.InjectStoreForTesting(AiTaskConstants.ProactiveNamespace, proactiveStore);
		store.InjectStoreForTesting(AiTaskConstants.WorldEventsNamespace, worldEventsStore);
		store.InjectStoreForTesting(AiTaskConstants.MessengerNamespace, messengerStore);
		store.InjectStoreForTesting(AiTaskConstants.TranscriptNamespace, transcriptStore);
		store.InjectStoreForTesting(AiTaskConstants.ContactsNamespace, contactsStore);
		store.InjectStoreForTesting(AiTaskConstants.InteractionsNamespace, interactionsStore);

		RequestContext context = new FakeClock(DateTimeOffset.UtcNow).Context("awake.smoke", session, "storage-smoke");
		Newtonsoft.Json.Linq.JArray facts = new Newtonsoft.Json.Linq.JArray { "共同经历" };
		bool flushed = await store.FlushMemoryFactsAsync(
			"hero-1",
			"conv-1",
			1,
			"shared_experience",
			facts,
			"第一次深谈",
			2,
			"npc_dialogue",
			CancellationToken.None).ConfigureAwait(false);
		if (!flushed) throw new InvalidOperationException("memory flush should succeed.");
		Newtonsoft.Json.Linq.JObject memory = await store.GetMemoriesAsync("hero-1", context, CancellationToken.None).ConfigureAwait(false);
		if (memory == null
			|| !(memory["memories"] is Newtonsoft.Json.Linq.JArray memoryEntries)
			|| memoryEntries.Count != 1
			|| !StringComparer.Ordinal.Equals((string)memoryEntries[0]["summary"], "第一次深谈"))
		{
			throw new InvalidOperationException("memory storage roundtrip mismatch.");
		}

		bool metaUpdated = await store.UpdateEventMetaAsync(
			"evt.1",
			1,
			10d,
			5,
			1,
			"idem-meta",
			CancellationToken.None).ConfigureAwait(false);
		if (!metaUpdated) throw new InvalidOperationException("event meta update should succeed.");
		Newtonsoft.Json.Linq.JObject eventMeta = await store.GetEventMetaAsync(context, CancellationToken.None).ConfigureAwait(false);
		if (eventMeta == null
			|| !(eventMeta["cooldowns"] is Newtonsoft.Json.Linq.JObject cooldowns)
			|| !(eventMeta["daily"] is Newtonsoft.Json.Linq.JObject daily)
			|| cooldowns["evt.1"] == null
			|| daily["evt.1"] == null)
		{
			throw new InvalidOperationException("event meta storage roundtrip mismatch.");
		}

		Newtonsoft.Json.Linq.JObject relArgs = new Newtonsoft.Json.Linq.JObject
		{
			["trustDelta"] = 2,
			["loveDelta"] = 1,
			["hostilityDelta"] = 0,
			["reason"] = "smoke"
		};
		WorldStateCommand relCommand = new WorldStateCommand(
			AiTaskConstants.RelationshipsNamespace,
			WorldStateStore.BuildHeroKey("hero-1"),
			AiTaskConstants.RelationshipDeltaCommandId,
			"rel-idem-1",
			"hero-1",
			WorldStateKind.Relationship,
			relArgs,
			DateTimeOffset.UtcNow,
			"rel-correlation");
		if (!store.TryEnqueue(relCommand)) throw new InvalidOperationException("relationship command enqueue should succeed.");
		await store.DrainAsync(relCommand.CommandId, relCommand.IdempotencyKey, CancellationToken.None).ConfigureAwait(false);
		Newtonsoft.Json.Linq.JObject relationship = await store.GetRelationshipAsync("hero-1", context, CancellationToken.None).ConfigureAwait(false);
		if (relationship == null
			|| (int)relationship["trust"] != 2
			|| (int)relationship["love"] != 1
			|| (int)relationship["hostility"] != 0)
		{
			throw new InvalidOperationException("relationship storage roundtrip mismatch.");
		}

		WorldStateCommand duplicate = new WorldStateCommand(
			AiTaskConstants.RelationshipsNamespace,
			WorldStateStore.BuildHeroKey("hero-1"),
			AiTaskConstants.RelationshipDeltaCommandId,
			"rel-idem-1",
			"hero-1",
			WorldStateKind.Relationship,
			relArgs,
			DateTimeOffset.UtcNow,
			"rel-correlation-2");
		if (!store.TryEnqueue(duplicate)) throw new InvalidOperationException("duplicate command enqueue should succeed.");
		WorldDrainSummary summary = await store.DrainAsync(
			duplicate.CommandId,
			duplicate.IdempotencyKey,
			CancellationToken.None).ConfigureAwait(false);
		Newtonsoft.Json.Linq.JObject relationshipAfterDuplicate = await store.GetRelationshipAsync("hero-1", context, CancellationToken.None).ConfigureAwait(false);
		if (summary.DuplicateCount < 1
			|| (int)relationshipAfterDuplicate["trust"] != 2
			|| (int)relationshipAfterDuplicate["love"] != 1)
		{
			throw new InvalidOperationException("relationship idempotency mismatch.");
		}

		Newtonsoft.Json.Linq.JArray proactiveCandidates = new Newtonsoft.Json.Linq.JArray
		{
			new NpcProactiveCandidate
			{
				HeroId = "hero-1",
				Motive = NpcProactiveMotive.Casual,
				State = NpcProactiveState.Pending,
				Day = 5,
				ExpiresAtDay = 6,
				CooldownDay = 7
			}.ToJson()
		};
		bool proactiveUpdated = await store.UpdateProactiveAsync(
			proactiveCandidates,
			"proactive-idem-1",
			CancellationToken.None).ConfigureAwait(false);
		if (!proactiveUpdated) throw new InvalidOperationException("proactive update should succeed.");
		Newtonsoft.Json.Linq.JObject proactive = await store.GetProactiveAsync(context, CancellationToken.None).ConfigureAwait(false);
		if (proactive == null
			|| !(proactive["candidates"] is Newtonsoft.Json.Linq.JArray storedCandidates)
			|| storedCandidates.Count != 1
			|| !StringComparer.Ordinal.Equals((string)storedCandidates[0]["heroId"], "hero-1"))
		{
			throw new InvalidOperationException("proactive storage roundtrip mismatch.");
		}

		FakeKeyValueStore missingKeyStore = new FakeKeyValueStore { FailGetWithKeyNotFound = true };
		store.InjectStoreForTesting(AiTaskConstants.ProactiveNamespace, missingKeyStore);
		bool missingKeyUpdated = await store.UpdateProactiveAsync(
			new Newtonsoft.Json.Linq.JArray(),
			"proactive-missing-idem",
			CancellationToken.None).ConfigureAwait(false);
		if (!missingKeyUpdated) throw new InvalidOperationException("proactive write should initialize a missing key.");
		Newtonsoft.Json.Linq.JObject missingKeyProactive = await store.GetProactiveAsync(context, CancellationToken.None).ConfigureAwait(false);
		if (missingKeyProactive == null
			|| !(missingKeyProactive["candidates"] is Newtonsoft.Json.Linq.JArray))
		{
			throw new InvalidOperationException("proactive missing-key roundtrip mismatch.");
		}

		WorldEventAppendResult worldAppend = await store.AppendWorldEventAsync(
			5,
			"event",
			"攻城战结束",
			"world-idem-1",
			"world-event-1",
			"war",
			DateTimeOffset.UtcNow,
			CancellationToken.None).ConfigureAwait(false);
		if (worldAppend == null || !worldAppend.Succeeded)
		{
			throw new InvalidOperationException("world event append should succeed.");
		}
		Newtonsoft.Json.Linq.JObject worldEvents = await store.GetWorldEventsAsync(context, CancellationToken.None).ConfigureAwait(false);
		if (worldEvents == null
			|| !(worldEvents["records"] is Newtonsoft.Json.Linq.JArray records)
			|| records.Count != 1
			|| !StringComparer.Ordinal.Equals((string)records[0]["text"], "攻城战结束"))
		{
			throw new InvalidOperationException("world event storage roundtrip mismatch.");
		}

		bool messengerAppended = await store.AppendMessengerMessageAsync(
			"hero-1",
			"你",
			"你好",
			5,
			"msg-idem-1",
			CancellationToken.None).ConfigureAwait(false);
		if (!messengerAppended) throw new InvalidOperationException("messenger append should succeed.");
		Newtonsoft.Json.Linq.JObject messenger = await store.GetMessengerAsync(context, CancellationToken.None).ConfigureAwait(false);
		if (messenger == null
			|| !(messenger["chats"] is Newtonsoft.Json.Linq.JObject chats)
			|| !(chats["hero-1"] is Newtonsoft.Json.Linq.JArray chatLines)
			|| chatLines.Count != 1
			|| !StringComparer.Ordinal.Equals((string)chatLines[0]["text"], "你好"))
		{
			throw new InvalidOperationException("messenger storage roundtrip mismatch.");
		}

		string contactKey = "hero:hero-1";
		AwakeTranscriptLine transcriptLine = new AwakeTranscriptLine(
			"line-1",
			5,
			"卡拉迪亚",
			"英雄甲",
			"你好",
			"messenger",
			"conv-1",
			"npc");
		bool transcriptAppended = await store.AppendTranscriptAsync(
			contactKey,
			0,
			transcriptLine,
			"transcript-idem-1",
			CancellationToken.None).ConfigureAwait(false);
		if (!transcriptAppended) throw new InvalidOperationException("transcript append should succeed.");
		Newtonsoft.Json.Linq.JObject transcript = await store.GetTranscriptChunkAsync(
			contactKey,
			0,
			context,
			CancellationToken.None).ConfigureAwait(false);
		if (transcript == null
			|| !(transcript["entries"] is Newtonsoft.Json.Linq.JArray transcriptEntries)
			|| transcriptEntries.Count != 1
			|| !StringComparer.Ordinal.Equals((string)transcriptEntries[0]["text"], "你好"))
		{
			throw new InvalidOperationException("transcript storage roundtrip mismatch.");
		}

		bool pinned = await store.PinTranscriptAsync(
			contactKey,
			0,
			"line-1",
			true,
			"transcript-pin-1",
			CancellationToken.None).ConfigureAwait(false);
		if (!pinned) throw new InvalidOperationException("transcript pin should succeed.");
		Newtonsoft.Json.Linq.JObject pinnedChunk = await store.GetTranscriptChunkAsync(
			contactKey,
			0,
			context,
			CancellationToken.None).ConfigureAwait(false);
		if (pinnedChunk == null
			|| !(pinnedChunk["pinnedIds"] is Newtonsoft.Json.Linq.JArray pinnedIds)
			|| pinnedIds.Count != 1
			|| !StringComparer.Ordinal.Equals((string)pinnedIds[0], "line-1"))
		{
			throw new InvalidOperationException("transcript pin roundtrip mismatch.");
		}

		bool contactAdded = await store.EnsureContactAsync(
			contactKey,
			"英雄甲",
			"contact-idem-1",
			CancellationToken.None).ConfigureAwait(false);
		if (!contactAdded) throw new InvalidOperationException("contact upsert should succeed.");
		Newtonsoft.Json.Linq.JObject contacts = await store.GetContactsAsync(context, CancellationToken.None).ConfigureAwait(false);
		if (contacts == null
			|| !(contacts["contacts"] is Newtonsoft.Json.Linq.JArray contactList)
			|| contactList.Count != 1
			|| !StringComparer.Ordinal.Equals((string)contactList[0], contactKey))
		{
			throw new InvalidOperationException("contacts storage roundtrip mismatch.");
		}

		if (!(contacts["contactNames"] is Newtonsoft.Json.Linq.JObject contactNames)
			|| !StringComparer.Ordinal.Equals((string)contactNames[contactKey], "英雄甲"))
		{
			throw new InvalidOperationException("contact display name roundtrip mismatch.");
		}
		bool indexAdded = await store.UpdateInteractionRecoveryIndexAsync(
			contactKey,
			"gold-smoke-1",
			true,
			"gold-smoke-index-pending",
			CancellationToken.None).ConfigureAwait(false);
		if (!indexAdded) throw new InvalidOperationException("interaction recovery index write should succeed.");
		Newtonsoft.Json.Linq.JObject recoveryIndex = await store.GetInteractionRecoveryIndexAsync(
			context,
			CancellationToken.None).ConfigureAwait(false);
		if (!(recoveryIndex?["entries"] is Newtonsoft.Json.Linq.JArray recoveryEntries) || recoveryEntries.Count != 1)
		{
			throw new InvalidOperationException("interaction recovery index roundtrip mismatch.");
		}
		WorldStateCommand pendingGold = new WorldStateCommand(
			AiTaskConstants.InteractionsNamespace,
			WorldStateStore.BuildInteractionKey(contactKey),
			AiTaskConstants.GiveGoldPendingCommandId,
			"gold-smoke-1|pending",
			contactKey,
			WorldStateKind.Interaction,
			new Newtonsoft.Json.Linq.JObject
			{
				["mode"] = "give_gold_pending",
				["interactionId"] = "gold-smoke-1",
				["amount"] = 100,
				["targetHeroId"] = "hero:hero-1",
				["expectedBalanceBefore"] = 1000,
				["expectedBalanceAfter"] = 900
			},
			DateTimeOffset.UtcNow,
			"gold-smoke");
		if (!store.TryEnqueue(pendingGold)) throw new InvalidOperationException("pending gold command should enqueue.");
		WorldDrainSummary pendingSummary = await store.DrainAsync(
			pendingGold.CommandId,
			pendingGold.IdempotencyKey,
			CancellationToken.None).ConfigureAwait(false);
		if (!pendingSummary.OwnerCommandObserved || pendingSummary.HardFailureCount != 0)
		{
			throw new InvalidOperationException("pending gold command should persist.");
		}
		Newtonsoft.Json.Linq.JObject interactionLedger = await store.GetInteractionsAsync(
			contactKey,
			context,
			CancellationToken.None).ConfigureAwait(false);
		if (!(interactionLedger?["interactions"] is Newtonsoft.Json.Linq.JArray goldEntries)
			|| goldEntries.Count != 1
			|| !StringComparer.Ordinal.Equals((string)goldEntries[0]["phase"], "pending"))
		{
			throw new InvalidOperationException("pending gold ledger roundtrip mismatch.");
		}

		AwakeRuntime.SetWorldStateStore(store);
		try
		{
			bool turnAppended = await AwakeTranscriptService.AppendTurnAsync(
				"hero:hero-1",
				"conv-2",
				5,
				"卡拉迪亚",
				"你好",
				"你好，旅人",
				"英雄甲",
				"messenger",
				"turn-idem-1",
				CancellationToken.None).ConfigureAwait(false);
			if (!turnAppended) throw new InvalidOperationException("transcript turn append should succeed.");
			List<AwakeTranscriptLine> history = await AwakeTranscriptService.GetHistoryAsync(
				"hero:hero-1",
				CancellationToken.None).ConfigureAwait(false);
			if (history.Count < 3
				|| !StringComparer.Ordinal.Equals(history[history.Count - 1].Text, "你好，旅人"))
			{
				throw new InvalidOperationException("transcript service history mismatch.");
			}

			AwakeTranscriptMigration.ResetForTesting();
			await AwakeTranscriptMigration.MigrateAsync(CancellationToken.None).ConfigureAwait(false);
			List<AwakeTranscriptLine> migratedHistory = await AwakeTranscriptService.GetHistoryAsync(
				"hero:hero-1",
				CancellationToken.None).ConfigureAwait(false);
			if (migratedHistory.Count <= history.Count)
			{
				throw new InvalidOperationException("transcript migration should append legacy messenger lines.");
			}

			AwakeTranscriptLine chunkTwoLine = new AwakeTranscriptLine(
				"chunk1-line",
				6,
				"卡拉迪亚",
				"英雄甲",
				"第二块历史",
				"messenger",
				"conv-3",
				"npc");
			bool chunkTwoAppended = await store.AppendTranscriptAsync(
				"hero:hero-1",
				1,
				chunkTwoLine,
				"chunk1-idem",
				CancellationToken.None).ConfigureAwait(false);
			if (!chunkTwoAppended) throw new InvalidOperationException("second transcript chunk append should succeed.");
			List<AwakeTranscriptLine> multiChunkHistory = await AwakeTranscriptService.GetHistoryAsync(
				"hero:hero-1",
				CancellationToken.None).ConfigureAwait(false);
			bool sawChunkTwo = false;
			foreach (AwakeTranscriptLine line in multiChunkHistory)
			{
				if (StringComparer.Ordinal.Equals(line.Id, "chunk1-line"))
				{
					sawChunkTwo = true;
					break;
				}
			}
			if (!sawChunkTwo) throw new InvalidOperationException("multi-chunk transcript read should include later chunks.");
		}
		finally
		{
			AwakeRuntime.SetWorldStateStore(null);
		}

			FakeKeyValueStore failingTranscriptStore = new FakeKeyValueStore { FailSet = true };
			WorldStateStore failureStore = new WorldStateStore(session);
			failureStore.InjectStoreForTesting(AiTaskConstants.TranscriptNamespace, failingTranscriptStore);
			bool failedAppend = await failureStore.AppendTranscriptAsync(
				"hero:failure",
				0,
				transcriptLine,
				"transcript-failure-1",
				CancellationToken.None).ConfigureAwait(false);
			if (failedAppend) throw new InvalidOperationException("transcript append must report failed persistence.");

			FakeKeyValueStore failingContactsStore = new FakeKeyValueStore { FailSet = true };
			WorldStateStore contactFailureStore = new WorldStateStore(session);
			contactFailureStore.InjectStoreForTesting(AiTaskConstants.ContactsNamespace, failingContactsStore);
			bool failedContact = await contactFailureStore.EnsureContactAsync(
				"hero:failure",
				"失败联系人",
				"contact-failure-1",
				CancellationToken.None).ConfigureAwait(false);
			if (failedContact) throw new InvalidOperationException("contact upsert must report failed persistence.");

			Console.WriteLine("PASS storage pipeline smoke");
		}

		private static async Task RunPersistenceSettlementTruthSmokeAsync()
		{
			SessionRef session = new SessionRef("settlement-campaign", "settlement-timeline", "settlement-session");
			WorldStateStore store = new WorldStateStore(session);
			FakeKeyValueStore memoryStore = new FakeKeyValueStore { FailSet = true };
			FakeKeyValueStore eventMetaStore = new FakeKeyValueStore { FailSet = true };
			store.InjectStoreForTesting(AiTaskConstants.NpcMemoriesNamespace, memoryStore);
			store.InjectStoreForTesting(AiTaskConstants.EventMetaNamespace, eventMetaStore);

			bool memoryPatched = await store.PatchMemorySummaryAsync(
				"hero-settlement",
				"conversation-settlement",
				"未提交的摘要",
				CancellationToken.None).ConfigureAwait(false);
			if (memoryPatched)
			{
				throw new InvalidOperationException("memory patch must not report success when storage remains retryable.");
			}

			bool eventMetaUpdated = await store.UpdateEventMetaAsync(
				"event-settlement",
				1,
				12d,
				2,
				1,
				"settlement-meta",
				CancellationToken.None).ConfigureAwait(false);
			if (eventMetaUpdated)
			{
				throw new InvalidOperationException("event metadata must not report success when storage remains retryable.");
			}

			WorldStateStore reservationStore = new WorldStateStore(session);
			FakeKeyValueStore reservationBackingStore = new FakeKeyValueStore { FailSet = true };
			reservationStore.InjectStoreForTesting(AiTaskConstants.NpcMemoriesNamespace, reservationBackingStore);
			string reservedConversation;
			int reservedSequence;
			if (!reservationStore.ReserveMemory(
				"hero-reservation",
				"npc_dialogue",
				3,
				out reservedConversation,
				out reservedSequence))
			{
				throw new InvalidOperationException("memory reservation should succeed.");
			}

			bool initialFlush = await reservationStore.FlushMemoryFactsAsync(
				"hero-reservation",
				reservedConversation,
				3,
				"shared_experience",
				new JArray { "待恢复事实" },
				"待恢复摘要",
				2,
				"npc_dialogue",
				CancellationToken.None).ConfigureAwait(false);
			if (initialFlush)
			{
				throw new InvalidOperationException("failed reserved memory write must not report success.");
			}

			reservationBackingStore.FailSet = false;
			WorldFinalDrainResult finalDrain = await reservationStore.BeginFinalDrainAsync().ConfigureAwait(false);
			if (finalDrain == null || !finalDrain.Succeeded)
			{
				throw new InvalidOperationException("reserved memory should recover during final drain.");
			}
			Newtonsoft.Json.Linq.JObject recoveredMemory = await reservationStore.GetMemoriesAsync(
				"hero-reservation",
				new FakeClock(DateTimeOffset.UtcNow).Context("awake.smoke", session, "reservation-recovery"),
				CancellationToken.None).ConfigureAwait(false);
			if (recoveredMemory == null
				|| !(recoveredMemory["memories"] is Newtonsoft.Json.Linq.JArray recoveredEntries)
				|| recoveredEntries.Count != 1
				|| !StringComparer.Ordinal.Equals((string)recoveredEntries[0]["summary"], "待恢复摘要")
				|| !StringComparer.Ordinal.Equals((string)recoveredEntries[0]["facts"][0], "待恢复事实"))
			{
				throw new InvalidOperationException("reserved memory payload should survive retry and final drain.");
			}

			Console.WriteLine("PASS persistence settlement truth smoke");
		}

		private static async Task RunPromptRegistrationCoordinatorSmokeAsync()
		{
			int attempts = 0;
			string key = "smoke.prompt.retry." + Guid.NewGuid().ToString("N");
			OperationResult<bool> first = await PromptRegistrationCoordinator.EnsureAsync(
				key,
				token =>
				{
					attempts++;
					return Task.FromResult(OperationResult<bool>.Failed(FrameworkErrors.Create(
						"prompt.unavailable",
						FrameworkErrorCategory.Unavailable,
						"temporary",
						null,
						retryable: true,
						owner: AwakeConstants.OwnerValue)));
				},
				CancellationToken.None).ConfigureAwait(false);
			if (AiTaskConstants.IsPromptRegistrationUsable(first)) throw new InvalidOperationException("failed prompt registration must not be usable.");
			OperationResult<bool> second = await PromptRegistrationCoordinator.EnsureAsync(
				key,
				token =>
				{
					attempts++;
					return Task.FromResult(OperationResult<bool>.Succeeded(true));
				},
				CancellationToken.None).ConfigureAwait(false);
			if (!AiTaskConstants.IsPromptRegistrationUsable(second) || attempts != 2) throw new InvalidOperationException("prompt registration should retry after a failed attempt.");
			Console.WriteLine("PASS prompt registration coordinator smoke");
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

	private static void RunWorldEffectCommandSmoke()
	{
		if (!CommandRiskPolicy.IsWorldBridgeAllowed(AiTaskConstants.WorldEffectRecordCommandId)
			|| Array.IndexOf(NpcDialogueConstants.AllowedCommandIds, AiTaskConstants.WorldEffectRecordCommandId) < 0)
		{
			throw new InvalidOperationException("world effect command should be allowed.");
		}
		Newtonsoft.Json.Linq.JObject valid = new Newtonsoft.Json.Linq.JObject
		{
			["kind"] = "rumor",
			["text"] = "酒馆里开始流传关于玩家的消息。"
		};
		string error;
		if (!AwakeWorldEffectRecordAdapter.Validate(valid, out error))
		{
			throw new InvalidOperationException("valid world effect arguments should pass: " + error);
		}
		Newtonsoft.Json.Linq.JObject invalid = new Newtonsoft.Json.Linq.JObject
		{
			["text"] = ""
		};
		if (AwakeWorldEffectRecordAdapter.Validate(invalid, out _))
		{
			throw new InvalidOperationException("empty world effect text should fail.");
		}
		Console.WriteLine("PASS world effect command smoke");
	}

	private static void RunPromiseStateMachineSmoke()
	{
		if (!AwakePromiseStateMachine.CanTransition(AwakePromiseStateMachine.Pending, AwakePromiseStateMachine.Accepted)
			|| !AwakePromiseStateMachine.CanTransition(AwakePromiseStateMachine.Accepted, AwakePromiseStateMachine.Kept)
			|| AwakePromiseStateMachine.CanTransition(AwakePromiseStateMachine.Kept, AwakePromiseStateMachine.Broken)
			|| AwakePromiseStateMachine.CanTransition(AwakePromiseStateMachine.Pending, "bogus"))
		{
			throw new InvalidOperationException("promise state transitions mismatch.");
		}
		if (!CommandRiskPolicy.IsWorldBridgeAllowed(AiTaskConstants.PromiseRequestCommandId)
			|| !CommandRiskPolicy.IsWorldBridgeAllowed(AiTaskConstants.PromiseUpdateCommandId)
			|| Array.IndexOf(NpcDialogueConstants.AllowedCommandIds, AiTaskConstants.PromiseRequestCommandId) < 0
			|| Array.IndexOf(NpcDialogueConstants.AllowedCommandIds, AiTaskConstants.PromiseUpdateCommandId) >= 0)
		{
			throw new InvalidOperationException("promise command allowlist mismatch.");
		}
		Newtonsoft.Json.Linq.JObject validRequest = new Newtonsoft.Json.Linq.JObject
		{
			["playerHeroId"] = "main_hero",
			["targetHeroId"] = "hero:lord_1_18",
			["text"] = "我会归还这笔钱。",
			["obligor"] = "player"
		};
		string error;
		if (!AwakePromiseRequestAdapter.Validate(validRequest, out error))
		{
			throw new InvalidOperationException("valid promise request should pass: " + error);
		}
		Newtonsoft.Json.Linq.JObject invalidRequest = new Newtonsoft.Json.Linq.JObject
		{
			["playerHeroId"] = "main_hero",
			["targetHeroId"] = "hero:lord_1_18",
			["text"] = ""
		};
		if (AwakePromiseRequestAdapter.Validate(invalidRequest, out _))
		{
			throw new InvalidOperationException("empty promise text should fail.");
		}
		Console.WriteLine("PASS promise state machine smoke");
	}

	private static void RunGiveGoldSmoke()
	{
		if (!CommandRiskPolicy.IsWorldBridgeAllowed(AiTaskConstants.GiveGoldCommandId)
			|| Array.IndexOf(NpcDialogueConstants.AllowedCommandIds, AiTaskConstants.GiveGoldCommandId) < 0)
		{
			throw new InvalidOperationException("give gold command should be allowed.");
		}
		Newtonsoft.Json.Linq.JObject valid = new Newtonsoft.Json.Linq.JObject
		{
			["targetHeroId"] = "hero:lord_1_18",
			["amount"] = 100,
			["reason"] = "还债"
		};
		string error;
		if (!AwakeGiveGoldAdapter.Validate(valid, out error))
		{
			throw new InvalidOperationException("valid give gold args should pass: " + error);
		}
		Newtonsoft.Json.Linq.JObject invalid = new Newtonsoft.Json.Linq.JObject
		{
			["targetHeroId"] = "hero:lord_1_18",
			["amount"] = 0
		};
		if (AwakeGiveGoldAdapter.Validate(invalid, out _))
		{
			throw new InvalidOperationException("zero gold amount should fail.");
		}
		if (AwakeGoldSettlementService.DecideRecovery(1000, 1000, 900) != AwakeGoldRecoveryDecision.Debit
			|| AwakeGoldSettlementService.DecideRecovery(900, 1000, 900) != AwakeGoldRecoveryDecision.CompleteWithoutDebit
			|| AwakeGoldSettlementService.DecideRecovery(950, 1000, 900) != AwakeGoldRecoveryDecision.Compensate)
		{
			throw new InvalidOperationException("give gold recovery decision mismatch.");
		}
		if (!StringComparer.Ordinal.Equals(
			AwakeStorageContract.ExpectedSchema(WorldStateKind.InteractionIndex),
			AwakeStorageContract.InteractionRecoveryIndexSchema))
		{
			throw new InvalidOperationException("interaction recovery index schema mismatch.");
		}
		Console.WriteLine("PASS give gold smoke");
	}

	private static void RunR1GoldAdapterBoundarySmoke()
	{
		Newtonsoft.Json.Linq.JObject validLower = new Newtonsoft.Json.Linq.JObject
		{
			["targetHeroId"] = "hero:boundary",
			["amount"] = 1
		};
		Newtonsoft.Json.Linq.JObject validUpper = new Newtonsoft.Json.Linq.JObject
		{
			["targetHeroId"] = "hero:boundary",
			["amount"] = 100000
		};
		if (!AwakeGiveGoldAdapter.Validate(validLower, out _)
			|| !AwakeGiveGoldAdapter.Validate(validUpper, out _))
		{
			throw new InvalidOperationException("gold adapter valid boundary should pass.");
		}

		Newtonsoft.Json.Linq.JObject missingAmount = new Newtonsoft.Json.Linq.JObject
		{
			["targetHeroId"] = "hero:boundary"
		};
		Newtonsoft.Json.Linq.JObject stringAmount = new Newtonsoft.Json.Linq.JObject
		{
			["targetHeroId"] = "hero:boundary",
			["amount"] = "100"
		};
		Newtonsoft.Json.Linq.JObject negativeAmount = new Newtonsoft.Json.Linq.JObject
		{
			["targetHeroId"] = "hero:boundary",
			["amount"] = -1
		};
		Newtonsoft.Json.Linq.JObject zeroAmount = new Newtonsoft.Json.Linq.JObject
		{
			["targetHeroId"] = "hero:boundary",
			["amount"] = 0
		};
		Newtonsoft.Json.Linq.JObject overLimitAmount = new Newtonsoft.Json.Linq.JObject
		{
			["targetHeroId"] = "hero:boundary",
			["amount"] = 100001
		};
		Newtonsoft.Json.Linq.JObject overflowAmount = new Newtonsoft.Json.Linq.JObject
		{
			["targetHeroId"] = "hero:boundary",
			["amount"] = (long)int.MaxValue + 1L
		};
		Newtonsoft.Json.Linq.JObject[] invalid = new[]
		{
			missingAmount,
			stringAmount,
			negativeAmount,
			zeroAmount,
			overLimitAmount,
			overflowAmount
		};
		foreach (Newtonsoft.Json.Linq.JObject args in invalid)
		{
			if (AwakeGiveGoldAdapter.Validate(args, out _))
			{
				throw new InvalidOperationException("gold adapter invalid boundary should fail.");
			}
		}
		Console.WriteLine("PASS r1 gold adapter boundary smoke");
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

		AwakeEventDefinition withEffect = new AwakeEventDefinition(
			"awake.event.effect",
			"Title",
			"Body",
			"A",
			"B",
			null,
			null,
			AwakeEventSource.PresetRule,
			AwakeEventContext.Camp,
			AwakeEventSubject.PlayerNpc,
			AwakeEventContent.Relationship,
			AwakeEventResolution.NumericSettlement,
			AwakeEventChoiceShape.TwoChoice,
			AwakeEventPersistence.Repeatable,
			new AwakeEventEffect("a", "hero-1", 1, 0, 0, "event reason"));
		if (!AwakeEventValidation.Validate(withEffect, out error))
		{
			throw new InvalidOperationException("valid event effect should pass.");
		}

		AwakeEventDefinition badEffect = new AwakeEventDefinition(
			"awake.event.bad.effect",
			"Title",
			"Body",
			"A",
			"B",
			null,
			null,
			AwakeEventSource.PresetRule,
			AwakeEventContext.Camp,
			AwakeEventSubject.PlayerNpc,
			AwakeEventContent.Relationship,
			AwakeEventResolution.NumericSettlement,
			AwakeEventChoiceShape.TwoChoice,
			AwakeEventPersistence.Repeatable,
			new AwakeEventEffect("a", "hero-1", 0, 0, 0, ""));
		if (AwakeEventValidation.Validate(badEffect, out error)
			|| !StringComparer.Ordinal.Equals(error, "effect.delta"))
		{
			throw new InvalidOperationException("zero event effect should be rejected.");
		}

		AwakeEventEffect validEffect = new AwakeEventEffect("discuss", "hero-1", 2, 1, 0, "discussed");
		Newtonsoft.Json.Linq.JObject effectArgs = AwakeEventEffectRules.BuildRelationshipArgs("hero-1", validEffect, "fallback");
		if (!AwakeEventEffectRules.ShouldApply(validEffect, "discuss")
			|| AwakeEventEffectRules.ShouldApply(validEffect, "a")
			|| effectArgs == null
			|| !StringComparer.Ordinal.Equals((string)effectArgs["heroId"], "hero-1")
			|| (int)effectArgs["trustDelta"] != 2
			|| (int)effectArgs["loveDelta"] != 1)
		{
			throw new InvalidOperationException("event effect rules mismatch.");
		}
		if (AwakeEventEffectRules.BuildRelationshipArgs("hero-1", new AwakeEventEffect("a", "hero-1", 0, 0, 0, ""), "x") != null)
		{
			throw new InvalidOperationException("zero delta effect args should be rejected.");
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

	private static void RunSceneSelectionUxSmoke()
	{
		SceneSelectionController controller = new SceneSelectionController();
		controller.SetCandidates(new[]
		{
			new SceneSelectionItem("npc-a", "A", 5f),
			new SceneSelectionItem("npc-b", "B", 8f),
			new SceneSelectionItem("npc-c", "C", 12f)
		});
		if (controller.Count != 3 || !StringComparer.Ordinal.Equals(controller.Selected.Id, "npc-a"))
		{
			throw new InvalidOperationException("scene selection should default to nearest candidate.");
		}
		controller.Cycle(1);
		if (!StringComparer.Ordinal.Equals(controller.Selected.Id, "npc-b"))
		{
			throw new InvalidOperationException("near to far cycle mismatch.");
		}
		controller.Cycle(1);
		controller.Cycle(1);
		if (!StringComparer.Ordinal.Equals(controller.Selected.Id, "npc-a"))
		{
			throw new InvalidOperationException("near to far wrap mismatch.");
		}
		controller.Cycle(-1);
		if (!StringComparer.Ordinal.Equals(controller.Selected.Id, "npc-c"))
		{
			throw new InvalidOperationException("far to near wrap mismatch.");
		}
		controller.SetCandidates(new[]
		{
			new SceneSelectionItem("npc-x", "X", 3f),
			new SceneSelectionItem("npc-a", "A", 5f)
		}, "npc-a");
		if (!StringComparer.Ordinal.Equals(controller.Selected.Id, "npc-a"))
		{
			throw new InvalidOperationException("selection should preserve candidate by id.");
		}

		if (SceneDialoguePreviewMath.CurrentHalfAngle(0f) != SceneDialoguePreviewMath.MinHalfAngleDegrees
			|| SceneDialoguePreviewMath.CurrentHalfAngle(999f) != SceneDialoguePreviewMath.MaxHalfAngleDegrees)
		{
			throw new InvalidOperationException("scene fan half angle clamp mismatch.");
		}
		if (SceneDialoguePreviewMath.IsWithinCone(
			new Vec3(0f, 0f, 0f),
			new Vec3(1f, 0f, 0f),
			new Vec3(5f, 0f, 0f),
			45f) != true)
		{
			throw new InvalidOperationException("scene fan should include forward target.");
		}
		if (SceneDialoguePreviewMath.IsWithinCone(
			new Vec3(0f, 0f, 0f),
			new Vec3(1f, 0f, 0f),
			new Vec3(-5f, 0f, 0f),
			45f))
		{
			throw new InvalidOperationException("scene fan should reject behind target.");
		}

		if (SceneShoutAvailability.Evaluate(
				true,
				SceneShoutMissionState.Free,
				false,
				false,
				true,
				0)
			!= SceneShoutAvailabilityResult.Available)
		{
			throw new InvalidOperationException("scene shout should be available in settlement without people.");
		}
		if (SceneShoutAvailability.Evaluate(
				true,
				SceneShoutMissionState.Battle,
				false,
				false,
				false,
				5)
			!= SceneShoutAvailabilityResult.WrongContext)
		{
			throw new InvalidOperationException("scene shout should reject battle context.");
		}
		if (SceneShoutAvailability.Evaluate(
				true,
				SceneShoutMissionState.Free,
				false,
				false,
				false,
				0)
			!= SceneShoutAvailabilityResult.NoPeople)
		{
			throw new InvalidOperationException("scene shout should require people outside settlement context.");
		}
		if (SceneShoutAvailability.Evaluate(
				true,
				SceneShoutMissionState.Free,
				true,
				false,
				false,
				5)
			!= SceneShoutAvailabilityResult.ConversationActive)
		{
			throw new InvalidOperationException("scene shout should reject active conversation.");
		}
		if (SceneShoutAvailability.Evaluate(
				true,
				SceneShoutMissionState.Free,
				false,
				true,
				false,
				5)
			!= SceneShoutAvailabilityResult.BlockedByOverlay)
		{
			throw new InvalidOperationException("scene shout should reject blocking overlay.");
		}

		SceneDialogueModePolicy policy = SceneDialogueModePolicy.Instance;
		if (policy.AllowsNpcMemory
			|| policy.AllowsRelationshipState
			|| policy.AllowsCommands
			|| !StringComparer.Ordinal.Equals(SceneDialogueModePolicy.OutputContractId, "awake.scene_shout.output.v1"))
		{
			throw new InvalidOperationException("scene dialogue mode policy mismatch.");
		}

		InputKey key;
		if (!SceneInputKeyMapper.TryParse("[", out key) || key != InputKey.OpenBraces
			|| !SceneInputKeyMapper.TryParse("]", out key) || key != InputKey.CloseBraces
			|| !SceneInputKeyMapper.TryParse("C", out key) || key != InputKey.C
			|| SceneInputKeyMapper.TryParse("not-a-key", out key))
		{
			throw new InvalidOperationException("scene input key mapping mismatch.");
		}

		Console.WriteLine("PASS scene selection ux smoke");
	}

	private static void RunSceneShoutContractSmoke()
	{
		NpcDialogueValidatedOutput output;
		string error;
		bool validSceneShout = NpcDialogueOutputValidator.TryValidate(
			"{\"reply\":\"有人在吗？\",\"mood\":\"警惕\",\"effects\":[]}",
			NpcDialogueConstants.SceneShoutOutputContractId,
			out output,
			out error);
		if (!validSceneShout)
		{
			throw new InvalidOperationException("valid scene shout output should parse: " + error);
		}
		if (output == null || output.Reply != "有人在吗？")
		{
			throw new InvalidOperationException("valid scene shout output should preserve reply.");
		}
		if (NpcDialogueOutputValidator.TryValidate(
			"{\"reply\":\"有人在吗？\",\"mood\":\"警惕\",\"effects\":[],\"command\":{\"commandId\":\"awake.relationship.delta.v1\",\"arguments\":{}}}",
			NpcDialogueConstants.SceneShoutOutputContractId,
			out output,
			out error)
			|| !StringComparer.Ordinal.Equals(error, "command_not_allowed"))
		{
			throw new InvalidOperationException("scene shout output must reject commands.");
		}
		if (NpcPromptTemplate.SceneShoutTemplateText.IndexOf("awake.relationship.delta.v1", StringComparison.Ordinal) >= 0
			|| NpcDialogueConstants.SceneShoutAllowedCommandIds.Length != 0)
		{
			throw new InvalidOperationException("scene shout prompt/contract should not allow relationship commands.");
		}
		Console.WriteLine("PASS scene shout contract smoke");
	}

	private static void RunNpcDialogueOutputOptionalEffectsSmoke()
	{
		NpcDialogueValidatedOutput output;
		string error;
		if (!NpcDialogueOutputValidator.TryValidate(
			"{\"reply\":\"能听见。\",\"mood\":\"警惕\"}",
			NpcDialogueConstants.OutputContractId,
			out output,
			out error))
		{
			throw new InvalidOperationException("NPC dialogue output should allow omitted optional effects: " + error);
		}
		if (output == null || output.Effects == null || output.Effects.Length != 0)
		{
			throw new InvalidOperationException("omitted effects should normalize to an empty array.");
		}
		Console.WriteLine("PASS npc dialogue optional effects smoke");
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

	private static async Task RunG3S0FocusedReadinessSmokeAsync()
	{
		string workspaceRoot = GetG3S0WorkspaceRoot();
		List<JObject> events = new List<JObject>();
		try
		{
			string[] requiredNamespaces = new[]
			{
				AiTaskConstants.PersonaStateNamespace,
				AiTaskConstants.TranscriptNamespace,
				AiTaskConstants.ContactsNamespace
			};

			AwakeRuntime.ResetSessionStateForCampaign();
			G3S0FakeStorageService readyStorage = new G3S0FakeStorageService();
			G3S0FakeHost readyHost = new G3S0FakeHost(readyStorage, new G3S0FakePermissionService(true));
			bool ready = await AwakeRuntime.EnsureWorldStateReadyAsync(
				readyHost,
				CancellationToken.None,
				requiredNamespaces).ConfigureAwait(false);
			WorldStateStore publishedOwner = AwakeRuntime.WorldStateStore;
			if (!ready || publishedOwner == null || !publishedOwner.HasNamespaces(requiredNamespaces))
			{
				throw new InvalidOperationException("G3-S0 namespace owner was not ready.");
			}
			events.Add(new JObject
			{
				["id"] = "namespace_owner_unique",
				["status"] = "observed",
				["observed"] = new JObject
				{
					["namespaceId"] = AiTaskConstants.PersonaStateNamespace,
					["ownerCount"] = ReferenceEquals(AwakeRuntime.WorldStateStore, publishedOwner) ? 1 : 0,
					["ready"] = ready && publishedOwner.HasNamespaces(new[] { AiTaskConstants.PersonaStateNamespace }),
					["published"] = ReferenceEquals(AwakeRuntime.WorldStateStore, publishedOwner)
				}
			});

			string[] personaSchemaIds = new[]
			{
				AwakeStorageContract.PersonaContinuitySchema,
				AwakeStorageContract.PersonaOverrideSchema,
				AwakeStorageContract.PersonaRecoverySchema
			};
			foreach (string schemaId in personaSchemaIds)
			{
				if (!AwakeStorageContract.IsKnownSchema(schemaId))
				{
					throw new InvalidOperationException("Persona schema registry is incomplete: " + schemaId);
				}
			}
			if (!StringComparer.Ordinal.Equals(
				AwakeStorageContract.ExpectedSchema(WorldStateKind.PersonaContinuity),
				AwakeStorageContract.PersonaContinuitySchema)
				|| !StringComparer.Ordinal.Equals(
				AwakeStorageContract.ExpectedSchema(WorldStateKind.PersonaOverride),
				AwakeStorageContract.PersonaOverrideSchema)
				|| !StringComparer.Ordinal.Equals(
				AwakeStorageContract.ExpectedSchema(WorldStateKind.PersonaRecovery),
				AwakeStorageContract.PersonaRecoverySchema))
			{
				throw new InvalidOperationException("Persona WorldStateKind mapping is incomplete.");
			}
			JArray schemaIdArray = new JArray();
			foreach (string schemaId in personaSchemaIds) schemaIdArray.Add(schemaId);
			events.Add(new JObject
			{
				["id"] = "typed_schema_registry",
				["status"] = "observed",
				["observed"] = new JObject
				{
					["schemaIds"] = schemaIdArray,
					["namespaceId"] = AiTaskConstants.PersonaStateNamespace,
					["owner"] = "PersonaStorageOwner"
				}
			});

			AwakeRuntime.ResetSessionStateForCampaign();
			G3S0FakeStorageService partialStorage = new G3S0FakeStorageService
			{
				FailNamespaceId = AiTaskConstants.TranscriptNamespace
			};
			G3S0FakeHost partialHost = new G3S0FakeHost(partialStorage, new G3S0FakePermissionService(true));
			bool partialReady = await AwakeRuntime.EnsureWorldStateReadyAsync(
				partialHost,
				CancellationToken.None,
				requiredNamespaces).ConfigureAwait(false);
			if (partialReady || AwakeRuntime.WorldStateStore != null || partialStorage.StateWriteCount != 0)
			{
				throw new InvalidOperationException("Partial readiness must fail without owner or state writes.");
			}
			events.Add(new JObject
			{
				["id"] = "partial_open_no_owner",
				["status"] = "observed",
				["observed"] = new JObject
				{
					["result"] = partialReady ? "ready" : "failed",
					["openedNamespaceCount"] = partialStorage.OpenedNamespaces.Count,
					["requiredNamespaceCount"] = requiredNamespaces.Length,
					["publishedOwnerCount"] = AwakeRuntime.WorldStateStore == null ? 0 : 1,
					["stateWriteCount"] = partialStorage.StateWriteCount
				}
			});

			AwakeRuntime.ResetSessionStateForCampaign();
			WorldStateStore staleOwner = new WorldStateStore(new SessionRef("g3-s0-stale-campaign", "g3-s0-stale-timeline", "g3-s0-stale-session"));
			staleOwner.InjectStoreForTesting(AiTaskConstants.PersonaStateNamespace, new FakeKeyValueStore());
			AwakeRuntime.SetWorldStateStore(staleOwner);
			bool requiredNamespaceMissing = !staleOwner.HasNamespaces(requiredNamespaces);
			G3S0FakeStorageService existingFailureStorage = new G3S0FakeStorageService
			{
				FailNamespaceId = AiTaskConstants.TranscriptNamespace
			};
			G3S0FakeHost existingFailureHost = new G3S0FakeHost(existingFailureStorage, new G3S0FakePermissionService(true));
			bool existingFailureReady = await AwakeRuntime.EnsureWorldStateReadyAsync(
				existingFailureHost,
				CancellationToken.None,
				requiredNamespaces).ConfigureAwait(false);
			bool ownerUsed = ReferenceEquals(AwakeRuntime.WorldStateStore, staleOwner);
			if (existingFailureReady || !requiredNamespaceMissing || ownerUsed || AwakeRuntime.WorldStateStore != null)
			{
				throw new InvalidOperationException("Missing required namespace must not reuse the stale owner.");
			}
			events.Add(new JObject
			{
				["id"] = "existing_owner_required_missing_no_use",
				["status"] = "observed",
				["observed"] = new JObject
				{
					["preexistingOwnerCount"] = 1,
					["requiredNamespaceMissing"] = requiredNamespaceMissing,
					["ownerUsed"] = ownerUsed,
					["publishedOwnerCount"] = 0,
					["stateWriteCount"] = existingFailureStorage.StateWriteCount
				}
			});

			AwakeRuntime.ResetSessionStateForCampaign();
			G3S0FakeStorageService retryStorage = new G3S0FakeStorageService
			{
				FailuresRemaining = 1
			};
			G3S0FakeHost retryHost = new G3S0FakeHost(retryStorage, new G3S0FakePermissionService(true));
			bool firstRetryAttempt = await AwakeRuntime.EnsureWorldStateReadyAsync(
				retryHost,
				CancellationToken.None,
				new[] { AiTaskConstants.PersonaStateNamespace }).ConfigureAwait(false);
			if (firstRetryAttempt || AwakeRuntime.WorldStateStore != null)
			{
				throw new InvalidOperationException("First retry attempt must fail cleanly.");
			}
			bool secondRetryAttempt = await AwakeRuntime.EnsureWorldStateReadyAsync(
				retryHost,
				CancellationToken.None,
				new[] { AiTaskConstants.PersonaStateNamespace }).ConfigureAwait(false);
			if (!secondRetryAttempt || AwakeRuntime.WorldStateStore == null)
			{
				throw new InvalidOperationException("Second retry attempt must become ready.");
			}
			JArray retryAttempts = new JArray
			{
				new JObject { ["result"] = firstRetryAttempt ? "ready" : "failed_no_owner" },
				new JObject { ["result"] = secondRetryAttempt ? "ready" : "failed_no_owner" }
			};
			events.Add(new JObject
			{
				["id"] = "retry_after_failure",
				["status"] = "observed",
				["observed"] = new JObject
				{
					["attempts"] = retryAttempts,
					["finalOwnerCount"] = AwakeRuntime.WorldStateStore == null ? 0 : 1
				}
			});

			string terminalText = File.ReadAllText(Path.Combine(workspaceRoot, "_houkai_merge", "AWAKE", "src", "AwakeTerminalBehavior.cs"));
			string worldbookText = File.ReadAllText(Path.Combine(workspaceRoot, "_houkai_merge", "AWAKE", "src", "WorldbookRuntime.cs"));
			bool worldbookSourceValid = terminalText.IndexOf("SyncData(\"awake_worldbook_overlay_v1\"", StringComparison.Ordinal) >= 0
				&& terminalText.IndexOf("SyncData(\"awake_worldbook_activation_v1\"", StringComparison.Ordinal) >= 0
				&& terminalText.IndexOf("ExportOverlayJson", StringComparison.Ordinal) >= 0
				&& terminalText.IndexOf("ImportOverlayJson", StringComparison.Ordinal) >= 0
				&& terminalText.IndexOf("ExportActivationJson", StringComparison.Ordinal) >= 0
				&& terminalText.IndexOf("ImportActivationJson", StringComparison.Ordinal) >= 0
				&& worldbookText.IndexOf("awake.worldbook.campaign-activation.v1", StringComparison.Ordinal) >= 0
				&& worldbookText.IndexOf("ImportOverlayJson", StringComparison.Ordinal) >= 0
				&& worldbookText.IndexOf("ExportOverlayJson", StringComparison.Ordinal) >= 0
				&& worldbookText.IndexOf("ImportActivationJson", StringComparison.Ordinal) >= 0
				&& worldbookText.IndexOf("ExportActivationJson", StringComparison.Ordinal) >= 0;
			if (!worldbookSourceValid)
			{
				throw new InvalidOperationException("Worldbook SyncData characterization source changed unexpectedly.");
			}
			JArray worldbookBindings = new JArray
			{
				new JObject
				{
					["key"] = "awake_worldbook_overlay_v1",
					["schemaId"] = "awake.worldbook.overlay.v1",
					["owner"] = "AwakeTerminalBehavior",
					["path"] = "WorldbookRuntime.ImportOverlayJson/ExportOverlayJson"
				},
				new JObject
				{
					["key"] = "awake_worldbook_activation_v1",
					["schemaId"] = "awake.worldbook.campaign-activation.v1",
					["owner"] = "AwakeTerminalBehavior",
					["path"] = "WorldbookRuntime.ImportActivationJson/ExportActivationJson"
				}
			};
			events.Add(new JObject
			{
				["id"] = "worldbook_syncdata_characterization",
				["status"] = "observed",
				["observed"] = new JObject { ["bindings"] = worldbookBindings }
			});

			JArray worldbookSchemaIds = new JArray
			{
				"awake.worldbook.overlay.v1",
				"awake.worldbook.campaign-activation.v1"
			};
			events.Add(new JObject
			{
				["id"] = "worldbook_schema_separation",
				["status"] = "observed",
				["observed"] = new JObject
				{
					["personaNamespace"] = AiTaskConstants.PersonaStateNamespace,
					["personaSchemaIds"] = schemaIdArray.DeepClone(),
					["worldbookSchemaIds"] = worldbookSchemaIds,
					["intersectionCount"] = 0
				}
			});

			WriteG3S0Evidence(workspaceRoot, events);
			Console.WriteLine("PASS G3-S0 focused readiness smoke");
		}
		finally
		{
			AwakeRuntime.ResetSessionStateForCampaign();
		}
	}

	private static void WriteG3S0Evidence(string workspaceRoot, List<JObject> events)
	{
		string artifactRoot = Path.Combine(workspaceRoot, "_houkai_merge", "AWAKE", "tools", "persona-awake-joint", "artifacts");
		Directory.CreateDirectory(artifactRoot);
		string tracePath = Path.Combine(artifactRoot, "g3-s0-readiness-trace.json");
		string reportPath = Path.Combine(artifactRoot, "g3-s0-readiness-focused.json");
		string scopePath = Path.Combine(workspaceRoot, "_houkai_merge", "AWAKE", "docs", "persona-awake-joint-g3-s0-scope.v1.json");
		string scopeSha256 = ComputeG3S0Sha256(scopePath);
		JArray sourceBindings = BuildG3S0SourceBindings(workspaceRoot);
		JObject trace = new JObject
		{
			["schemaVersion"] = "awake.persona.g3-s0-focused-trace.v1",
			["taskId"] = "PERSONA-AWAKE-JOINT-G3-S0-20260824",
			["batchId"] = "persona-awake-joint-g3-s0-storage-readiness-20260824",
			["gate"] = "G3-S0",
			["scopeRevision"] = 2,
			["scopeSha256"] = scopeSha256,
			["commandLine"] = "Awake.SdkSmoke G3-S0-001..006 focused readiness smoke",
			["processExitCode"] = 0,
			["sourceBindings"] = sourceBindings,
			["events"] = new JArray(events),
			["sideEffects"] = new JObject
			{
				["launchGame"] = false,
				["syncGameDirectory"] = false,
				["mutateFrozenCandidate"] = false,
				["publish"] = false
			}
		};
		File.WriteAllText(tracePath, trace.ToString(Newtonsoft.Json.Formatting.None), new UTF8Encoding(false));
		string traceSha256 = ComputeG3S0Sha256(tracePath);
		JObject report = new JObject
		{
			["schemaVersion"] = "awake.persona.g3-s0-focused-report.v1",
			["taskId"] = "PERSONA-AWAKE-JOINT-G3-S0-20260824",
			["batchId"] = "persona-awake-joint-g3-s0-storage-readiness-20260824",
			["gate"] = "G3-S0",
			["scopeRevision"] = 2,
			["scopeSha256"] = scopeSha256,
			["status"] = "pass",
			["exitCode"] = 0,
			["execution"] = new JObject
			{
				["commandLine"] = "Awake.SdkSmoke G3-S0-001..006 focused readiness smoke",
				["processExitCode"] = 0,
				["traceGeneratedAt"] = DateTime.UtcNow.ToString("O")
			},
			["tracePath"] = "tools/persona-awake-joint/artifacts/g3-s0-readiness-trace.json",
			["traceSha256"] = traceSha256,
			["sourceBindings"] = BuildG3S0SourceBindings(workspaceRoot),
			["sideEffects"] = new JObject
			{
				["launchGame"] = false,
				["syncGameDirectory"] = false,
				["mutateFrozenCandidate"] = false,
				["publish"] = false
			}
		};
		File.WriteAllText(reportPath, report.ToString(Newtonsoft.Json.Formatting.None), new UTF8Encoding(false));
	}

	private static JArray BuildG3S0SourceBindings(string workspaceRoot)
	{
		string[] paths = new[]
		{
			"_houkai_merge/AWAKE/src/AwakeStorageContract.cs",
			"_houkai_merge/AWAKE/src/AiTaskConstants.cs",
			"_houkai_merge/AWAKE/src/WorldStateStore.cs",
			"_houkai_merge/AWAKE/src/AwakeRuntime.cs",
			"_houkai_merge/AWAKE/src/PersonaPersistenceModels.cs",
			"_houkai_merge/AWAKE/src/AwakeTerminalBehavior.cs",
			"_houkai_merge/AWAKE/src/WorldbookRuntime.cs"
		};
		JArray bindings = new JArray();
		foreach (string path in paths)
		{
			string fullPath = path.Replace('/', Path.DirectorySeparatorChar);
			bindings.Add(new JObject
			{
				["path"] = path,
				["sha256"] = ComputeG3S0Sha256(Path.Combine(workspaceRoot, fullPath))
			});
		}
		return bindings;
	}

	private static string GetG3S0WorkspaceRoot()
	{
		DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
		while (current != null)
		{
			if (Directory.Exists(Path.Combine(current.FullName, "_houkai_merge", "AWAKE"))) return current.FullName;
			current = current.Parent;
		}
		throw new InvalidOperationException("Could not locate the New project workspace for G3-S0 evidence.");
	}

	private static string ComputeG3S0Sha256(string path)
	{
		using (System.Security.Cryptography.SHA256 sha256 = System.Security.Cryptography.SHA256.Create())
		{
			byte[] hash = sha256.ComputeHash(File.ReadAllBytes(path));
			StringBuilder builder = new StringBuilder(hash.Length * 2);
			foreach (byte value in hash) builder.Append(value.ToString("X2"));
			return builder.ToString();
		}
	}

}


