using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarcusAIFramework.Api;
using MarcusAIFramework.Sdk.FakeHost;
using MarcusAIFramework.Sdk.TestKit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

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
		await RunStoragePipelineSmokeAsync();
		RunNpcMemorySmoke();
		RunWorldbookSmoke();
		RunRouteContractSmoke();
		RunTerminalHotkeySmoke();
		RunNpcProactiveSmoke();
		RunLongWaitSmoke();
		RunEventInboxSmoke();
		RunMemoryOverviewSmoke();
		RunGuardPerfSmoke();
		RunFeedbackSmoke();
		RunMcmPresetSmoke();
		RunMessengerHistorySmoke();
		Console.WriteLine("PASS ALL Awake.SdkSmoke");
		return 0;
	}

	private static void RunMessengerHistorySmoke()
	{
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

	private static void RunMcmPresetSmoke()
	{
		if (new AwakeConfig().NpcProactiveChance != 35)
		{
			throw new InvalidOperationException("mcm proactive chance default should be 35.");
		}
		bool sawStrict = false;
		foreach (MCM.Abstractions.ISettingsPreset preset in AwakePresetCatalog.Build())
		{
			AwakeConfig template = preset.LoadPreset() as AwakeConfig;
			if (template == null || template.NpcProactiveChance < 0 || template.NpcProactiveChance > 100)
			{
				throw new InvalidOperationException("mcm preset template invalid.");
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
			Affinity = 25,
			State = NpcProactiveState.Pending,
			Day = 10,
			ExpiresAtDay = 11,
			CooldownDay = 12,
			Fatigue = 1,
			OpeningHint = "hint"
		};
		Newtonsoft.Json.Linq.JObject json = candidate.ToJson();
		NpcProactiveCandidate parsed = NpcProactiveCandidate.FromJson(json);
		if (!StringComparer.Ordinal.Equals(parsed.HeroId, "hero-1")
			|| parsed.Motive != NpcProactiveMotive.Relationship
			|| parsed.State != NpcProactiveState.Pending
			|| parsed.Day != 10
			|| parsed.ExpiresAtDay != 11
			|| parsed.CooldownDay != 12
			|| parsed.Fatigue != 1
			|| !StringComparer.Ordinal.Equals(parsed.OpeningHint, "hint"))
		{
			throw new InvalidOperationException("npc proactive candidate roundtrip mismatch.");
		}
		if (NpcProactiveConstants.MaximumFatigue <= 0
			|| NpcProactiveConstants.CooldownDays <= 0
			|| NpcProactiveConstants.ExpiresAfterDays <= 0)
		{
			throw new InvalidOperationException("npc proactive constants should be positive.");
		}
		Console.WriteLine("PASS npc proactive smoke");
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
		store.InjectStoreForTesting(AiTaskConstants.NpcMemoriesNamespace, memoryStore);
		store.InjectStoreForTesting(AiTaskConstants.EventMetaNamespace, eventMetaStore);
		store.InjectStoreForTesting(AiTaskConstants.RelationshipsNamespace, relationshipStore);
		store.InjectStoreForTesting(AiTaskConstants.ProactiveNamespace, proactiveStore);
		store.InjectStoreForTesting(AiTaskConstants.WorldEventsNamespace, worldEventsStore);
		store.InjectStoreForTesting(AiTaskConstants.MessengerNamespace, messengerStore);

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

		bool worldAppended = await store.AppendWorldEventAsync(
			5,
			"event",
			"攻城战结束",
			"world-idem-1",
			CancellationToken.None).ConfigureAwait(false);
		if (!worldAppended) throw new InvalidOperationException("world event append should succeed.");
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

		Console.WriteLine("PASS storage pipeline smoke");
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
