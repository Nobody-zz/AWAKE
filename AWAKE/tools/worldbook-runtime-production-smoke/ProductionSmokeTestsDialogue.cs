using System;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using Newtonsoft.Json.Linq;

namespace Awake.WorldbookRuntimeProductionSmoke;

internal static partial class Program
{
    private static async Task TestNpcDialogueSendAsyncRoundtripAsync()
    {
        ProductionSmokeHost host = CreateHost("dialogue-send");
        WorldStateStore store = CreateStore(host, new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace));
        await Install(store).ConfigureAwait(false);
        WorldKnowledgeQueryService knowledge = BindKnowledge();
        WorldbookRuntime.SetKnowledgeForTesting(knowledge);
        WorldKnowledgeQueryResult probe = knowledge.Query(new WorldbookQuery
        {
            HeroId = "hero:send",
            IdentityId = "profile.commoner",
            KnowledgeScope = "local",
            KnowledgeScopeAvailable = true,
            EffectiveDetail = "rumor",
            EffectiveDetailAvailable = true,
            ContentTier = "pure",
            PlayerText = "请谈谈近况。"
        });
        Check(string.Equals(probe.State, WorldKnowledgeDecisionPolicy.Known, System.StringComparison.Ordinal)
            || string.Equals(probe.State, WorldKnowledgeDecisionPolicy.Partial, System.StringComparison.Ordinal), "knowledge probe must allow AI state=" + probe.State
            + " blocked=" + probe.BlockedReason + " match=" + probe.MatchMode
            + " hits=" + string.Join(",", probe.HitIds));
        host.AiAdapter.NextStructuredJson = "{\"reply\":\"我听见了。\",\"mood\":\"平静\",\"effects\":[]}";
        using (NpcDialogueService service = new NpcDialogueService(host, "hero:send", "发送测试", string.Empty))
        {
            SetPrivateField(service, "_heroRole", "commoner");
            NpcDialogueTurnResult submitted = await service.SendAsync("请谈谈近况。", CancellationToken.None).ConfigureAwait(false);
            Check(submitted.Ok, "real SendAsync must accept a deterministic smoke route");
            NpcDialogueUiEvent completed = null;
            for (int attempt = 0; attempt < 50 && completed == null; attempt++)
            {
                NpcDialogueUiEvent evt;
                while (service.TryDrainUiEvent(out evt))
                {
                    if (evt.Kind == NpcDialogueUiEventKind.TurnCompleted) completed = evt;
                }
                if (completed == null) await Task.Delay(10).ConfigureAwait(false);
            }
            Check(completed != null && completed.Turn != null && completed.Turn.Reply == "我听见了。",
                "known knowledge must route real SendAsync completion to TurnCompleted");
            Check(host.AiAdapter.LastInput.IndexOf("测试领地近日平静", System.StringComparison.Ordinal) >= 0,
                "known knowledge must reach the submitted AI prompt");
        }
        await store.BeginFinalDrainAsync().ConfigureAwait(false);
    }

    private static Task TestNpcDialogueContextSimulationAsync()
    {
        var variables = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            ["npc_identity"] = "蒙楚格，可汗",
            ["persona_dsl"] = "[人格] 重视秩序，也会试探对方的代价。",
            ["retrieved_knowledge"] = "[事实] 东部山口近日有劫匪出没。",
            ["npc_memory"] = "[记忆] 玩家曾答应送来谷物。",
            ["npc_state"] = "信任 3；敌意 1。",
            ["npc_commitments"] = "待履行：送来十袋谷物。",
            ["player_known"] = "玩家是某部族首领。",
            ["scene"] = "奥尔泰西亚城外。",
            ["opening_hint"] = string.Empty,
            ["player_turn"] = "我愿送粮，换你派人清理山口。",
            ["npc_id"] = "hero:monchug",
            ["dialogue_action_mode"] = "negotiation"
        };
        NpcPromptBoundedResult prompt = NpcDialoguePromptPipeline.BuildBounded(
            variables, null, NpcPromptTemplate.TemplateText, 32768);
        string rendered = prompt.DirectText;
        Check(!prompt.IsDirectOnly && rendered.IndexOf("重视秩序", System.StringComparison.Ordinal) >= 0
            && rendered.IndexOf("东部山口", System.StringComparison.Ordinal) >= 0
            && rendered.IndexOf("曾答应送来谷物", System.StringComparison.Ordinal) >= 0
            && rendered.IndexOf("待履行：送来十袋谷物", System.StringComparison.Ordinal) >= 0
            && rendered.IndexOf("我愿送粮", System.StringComparison.Ordinal) >= 0,
            "context simulation must render persona, facts, memory, commitments, and player turn independently");

        const string proposed = "{\"reply\":\"先把谷物送到营地，再谈山口。\",\"mood\":\"审慎\",\"effects\":[],\"command\":{\"commandId\":\"awake.relationship.delta.v1\",\"arguments\":{\"heroId\":\"hero:monchug\",\"trustDelta\":1},\"reason\":\"接受条件\"}}";
        NpcDialogueValidatedOutput chat;
        string chatError;
        Check(NpcDialogueOutputValidator.TryValidate(proposed, NpcDialogueConstants.OutputContractId, false, out chat, out chatError)
            && chat.Command == null && chat.CommandSuppressed,
            "chat simulation must suppress a proposed command without treating it as executed");
        NpcDialogueValidatedOutput negotiation;
        string negotiationError;
        Check(NpcDialogueOutputValidator.TryValidate(proposed, NpcDialogueConstants.OutputContractId, true, out negotiation, out negotiationError)
            && negotiation.Command != null,
            "negotiation simulation must retain a valid proposal for later confirmation");
        NpcDialogueValidatedOutput malformed;
        string malformedError;
        Check(!NpcDialogueOutputValidator.TryValidate("{bad", NpcDialogueConstants.OutputContractId, true, out malformed, out malformedError),
            "malformed simulated output must not become a dialogue reply or command");
        return Task.CompletedTask;
    }

    private static async Task TestNpcDialogueConfirmedSettlementObservationAsync()
    {
        NpcDialogueCommandConfirmation confirmation = new NpcDialogueCommandConfirmation(
            42, "dialogue-original-correlation", null, "已确认。", "平静");
        Check(confirmation.CorrelationId == "dialogue-original-correlation",
            "command confirmation must retain the original AI correlation");

        TaskCompletionSource<NpcDialogueCommandSettlement> delayed =
            new TaskCompletionSource<NpcDialogueCommandSettlement>(TaskCreationOptions.RunContinuationsAsynchronously);
        NpcDialogueConfirmedSettlementRunner.Track("hero:smoke", 42, confirmation.CorrelationId, delayed.Task);
        delayed.SetResult(new NpcDialogueCommandSettlement(true, "ok"));
        for (int attempt = 0; attempt < 20 && !HasCapturedLog("completion_kind=command_confirmed"); attempt++)
        {
            await Task.Delay(10).ConfigureAwait(false);
        }
        Check(HasCapturedLog("hero=hero:smoke generation=42 correlation=dialogue-original-correlation completion_kind=command_confirmed"),
            "confirmed settlement must emit one attributable completion log outside the UI service");

        NpcDialogueService directService = new NpcDialogueService(CreateHost("dialogue-direct-sequence"), "hero:direct", "直答测试", string.Empty);
        WorldKnowledgeDecision directKnowledge = new WorldKnowledgeDecision
        {
            State = WorldKnowledgeDecisionPolicy.NotFound,
            DirectReply = "这是固定直答。",
            Mood = "平静"
        };
        NpcKnowledgePromptBuildResult direct = new NpcKnowledgePromptBuildResult(directKnowledge, string.Empty);
        InvokePrivateVoid(directService, "CompleteDirectKnowledgeTurn", "第一次", direct);
        InvokePrivateVoid(directService, "CompleteDirectKnowledgeTurn", "第二次", direct);
        Check(HasCapturedLog("correlation=dialogue:hero:direct:direct:1")
            && HasCapturedLog("correlation=dialogue:hero:direct:direct:2"),
            "consecutive direct replies must receive distinct local correlations");
        directService.Dispose();
    }

    private static async Task TestNpcDialogueActionModeGateAsync()
    {
        Newtonsoft.Json.Linq.JObject commitments = new Newtonsoft.Json.Linq.JObject
        {
            ["promises"] = new Newtonsoft.Json.Linq.JArray
            {
                new Newtonsoft.Json.Linq.JObject
                {
                    ["status"] = AwakePromiseStateMachine.Pending,
                    ["text"] = "下次会面前送来粮秣"
                },
                new Newtonsoft.Json.Linq.JObject
                {
                    ["status"] = AwakePromiseStateMachine.Accepted,
                    ["text"] = "派一队斥候查看山口"
                },
                new Newtonsoft.Json.Linq.JObject
                {
                    ["status"] = AwakePromiseStateMachine.Kept,
                    ["text"] = "不应再进入当前未决清单"
                }
            }
        };
        string commitmentText = NpcDialogueStateFormatter.FormatCommitments(commitments);
        Check(commitmentText.IndexOf("待确认：下次会面前送来粮秣", System.StringComparison.Ordinal) >= 0
            && commitmentText.IndexOf("已应允：派一队斥候查看山口", System.StringComparison.Ordinal) >= 0
            && commitmentText.IndexOf("不应再进入", System.StringComparison.Ordinal) < 0,
            "dialogue facts must include only persisted unresolved commitments");
        const string responseWithCommand =
            "{\"reply\":\"边境尚且安稳。\",\"mood\":\"谨慎\",\"command\":{"
            + "\"commandId\":\"awake.relationship.delta.v1\","
            + "\"arguments\":{\"trustDelta\":1,\"loveDelta\":0,\"hostilityDelta\":0}}}";

        NpcDialogueValidatedOutput chatOutput;
        string chatError;
        bool chatValid = NpcDialogueOutputValidator.TryValidate(
            responseWithCommand,
            NpcDialogueConstants.OutputContractId,
            false,
            out chatOutput,
            out chatError);
        Check(chatValid, "chat mode must keep a valid reply when the model adds a command");
        Check(chatOutput.Command == null, "chat mode must suppress command proposals");
        Check(chatOutput.CommandSuppressed, "chat mode suppression must be observable to the caller");

        NpcDialogueValidatedOutput negotiationOutput;
        string negotiationError;
        bool negotiationValid = NpcDialogueOutputValidator.TryValidate(
            responseWithCommand,
            NpcDialogueConstants.OutputContractId,
            true,
            out negotiationOutput,
            out negotiationError);
        Check(negotiationValid && negotiationOutput.Command != null, "negotiation mode must retain a valid command proposal");

        NpcDialogueValidatedOutput sceneOutput;
        string sceneError;
        bool sceneValid = NpcDialogueOutputValidator.TryValidate(
            responseWithCommand,
            NpcDialogueConstants.SceneShoutOutputContractId,
            false,
            out sceneOutput,
            out sceneError);
        Check(!sceneValid && sceneError == "command_not_allowed", "scene shout must reject commands");
        Check(NpcPromptTemplate.TemplateText.IndexOf("dialogue_action_mode", System.StringComparison.Ordinal) >= 0,
            "NPC prompt must expose the action mode input");
        Check(NpcPromptTemplate.TemplateText.IndexOf("人物说出的承诺", System.StringComparison.Ordinal) >= 0,
            "NPC prompt must distinguish speech from state changes");
        Check(NpcPromptTemplate.TemplateText.IndexOf("人格模板用于决定你如何看待和表达事情", System.StringComparison.Ordinal) >= 0,
            "NPC prompt must distinguish persona guidance from current facts");
        Check(NpcPromptTemplate.TemplateText.IndexOf("不要反复用同一句口号", System.StringComparison.Ordinal) >= 0,
            "NPC prompt must require a response to the current turn instead of slogan repetition");
        NpcDialoguePromptPipeline.RecordContextDiagnostics(
            "hero:context-diagnostic",
            false,
            new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal)
            {
                ["persona_dsl"] = "人格约束",
                ["retrieved_knowledge"] = "已验证知识",
                ["npc_memory"] = string.Empty,
                ["npc_state"] = "信任 0",
                ["npc_commitments"] = "待确认：下次会面前送来粮秣",
                ["player_known"] = "玩家",
                ["scene"] = "草原",
                ["dialogue_action_mode"] = "chat"
            });
        System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, string>> contextRows =
            NpcDialoguePromptPipeline.BuildContextDiagnosticRows();
        Check(FindContextRow(contextRows, "dialogue_context.persona") == "present"
            && FindContextRow(contextRows, "dialogue_context.knowledge") == "present"
            && FindContextRow(contextRows, "dialogue_context.memory") == "absent"
            && FindContextRow(contextRows, "dialogue_context.commitments") == "present"
            && FindContextRow(contextRows, "dialogue_context.mode") == "chat",
            "developer diagnostics must expose context provenance without dialogue content");
        ProductionSmokeHost host = CreateHost("dialogue-mode");
        Check(FindContextRow(AwakeDeveloperReport.BuildRows(host, null), "dialogue_context.target") == "hero:context-diagnostic",
            "developer report must include the latest dialogue context provenance");
        NpcDialogueService service = new NpcDialogueService(host, "hero:dialogue-mode", "测试角色", string.Empty);
        NpcDialogueVM vm = new NpcDialogueVM(service, () => { });
        Check(vm.IsChatMode && !vm.IsNegotiationMode,
            "dialogue must start in chat mode");
        vm.ExecuteSetNegotiationMode();
        Check(vm.IsNegotiationMode && !vm.IsChatMode,
            "dialogue UI must be able to switch into negotiation mode");
        vm.ExecuteSetChatMode();
        Check(vm.IsChatMode && !vm.IsNegotiationMode,
            "dialogue UI must be able to switch back to chat mode");

        SetPrivateField(service, "_generation", 7);
        SetPrivateField(service, "_sending", true);
        TaskCompletionSource<NpcDialogueCommandSettlement> settlementSource =
            new TaskCompletionSource<NpcDialogueCommandSettlement>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task completion = InvokePrivateTask(
            service,
            "CompleteTurnAfterCommandAsync",
            7,
            "谈判结果",
            "谨慎",
            settlementSource.Task);
        await Task.Delay(20).ConfigureAwait(false);
        NpcDialogueUiEvent beforeSettlement;
        Check(!service.TryDrainUiEvent(out beforeSettlement),
            "dialogue completion must wait for command settlement");
        settlementSource.SetResult(new NpcDialogueCommandSettlement(true, "关系变化已落账。"));
        await WithTimeoutAsync(completion, "dialogue completion did not follow settlement").ConfigureAwait(false);
        NpcDialogueUiEvent settlementStatus;
        NpcDialogueUiEvent completed;
        Check(service.TryDrainUiEvent(out settlementStatus)
            && settlementStatus.Kind == NpcDialogueUiEventKind.Status
            && settlementStatus.Text == "关系变化已落账。",
            "settlement status must reach the UI first");
        Check(service.TryDrainUiEvent(out completed)
            && completed.Kind == NpcDialogueUiEventKind.TurnCompleted
            && completed.Turn.Reply == "谈判结果",
            "dialogue completion must reach the UI after settlement");
        service.Dispose();

        ProductionSmokeHost relationHost = CreateHost("dialogue-positive", true, true);
        ProductionSmokeKeyValueStore worldEvents = new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace);
        ProductionSmokeKeyValueStore relationships = new ProductionSmokeKeyValueStore(AiTaskConstants.RelationshipsNamespace);
        ProductionSmokeKeyValueStore interactions = new ProductionSmokeKeyValueStore(AiTaskConstants.InteractionsNamespace);
        WorldStateStore relationStore = CreateStore(relationHost, worldEvents);
        relationStore.InjectStoreForTesting(AiTaskConstants.RelationshipsNamespace, relationships);
        relationStore.InjectStoreForTesting(AiTaskConstants.InteractionsNamespace, interactions);
        await Install(relationStore).ConfigureAwait(false);

        string relationHeroId = "hero:dialogue-positive";
        string relationArguments = new JObject
        {
            ["heroId"] = relationHeroId,
            ["trustDelta"] = 3,
            ["loveDelta"] = 0,
            ["hostilityDelta"] = 0,
            ["reason"] = "玩家明确提供援助并获得接受"
        }.ToString(Newtonsoft.Json.Formatting.None);
        WorldCommandBridge bridge = new WorldCommandBridge(relationHost);
        OperationResult<string> bridged = await bridge.ExecuteAsync(
            new WorldCommandProposal(
                AiTaskConstants.RelationshipDeltaCommandId,
                relationArguments,
                "玩家明确提供援助并获得接受"),
            "dialogue-positive-turn",
            CancellationToken.None).ConfigureAwait(false);
        Check(bridged.IsSuccess && bridged.Value.IndexOf("关系状态变化", System.StringComparison.Ordinal) >= 0,
            "dialogue command must pass bridge permission, preflight, submit and drain");
        RequestContext relationContext = AwakeRuntime.CreateContext(relationHost, "dialogue-positive-read");
        JObject relationship = await relationStore.GetRelationshipAsync(
            relationHeroId,
            relationContext,
            CancellationToken.None).ConfigureAwait(false);
        Check(relationship != null && (int)relationship["trust"] == 3,
            "successful relationship settlement must persist the trust delta");

        string promiseArguments = new JObject
        {
            ["playerHeroId"] = "hero:player",
            ["targetHeroId"] = relationHeroId,
            ["canonicalContactKey"] = relationHeroId,
            ["obligor"] = "player",
            ["text"] = "送来十袋谷物"
        }.ToString(Newtonsoft.Json.Formatting.None);
        OperationResult<string> promiseCreated = await bridge.ExecuteAsync(
            new WorldCommandProposal(
                AiTaskConstants.PromiseRequestCommandId,
                promiseArguments,
                "玩家提出粮食承诺"),
            "dialogue-promise-create",
            CancellationToken.None).ConfigureAwait(false);
        Check(promiseCreated.IsSuccess,
            "promise request must pass the same bridge and drain as a dialogue relationship command");
        JObject interactionState = await relationStore.GetInteractionsAsync(
            relationHeroId,
            relationContext,
            CancellationToken.None).ConfigureAwait(false);
        string promiseId = (string)((interactionState?["promises"] as JArray)?[0]?["promiseId"]);
        Check(!string.IsNullOrWhiteSpace(promiseId),
            "accepted promise request must be persisted with a stable promise id");
        int promiseCountBeforeInvalid = (interactionState?["promises"] as JArray)?.Count ?? 0;
        OperationResult<string> invalidPromise = await bridge.ExecuteAsync(
            new WorldCommandProposal(
                AiTaskConstants.PromiseRequestCommandId,
                new JObject
                {
                    ["playerHeroId"] = "hero:player",
                    ["targetHeroId"] = relationHeroId,
                    ["canonicalContactKey"] = relationHeroId,
                    ["obligor"] = "player"
                }.ToString(Newtonsoft.Json.Formatting.None),
                "缺少承诺内容"),
            "dialogue-promise-invalid",
            CancellationToken.None).ConfigureAwait(false);
        JObject afterInvalid = await relationStore.GetInteractionsAsync(
            relationHeroId,
            relationContext,
            CancellationToken.None).ConfigureAwait(false);
        Check(!invalidPromise.IsSuccess
            && ((afterInvalid?["promises"] as JArray)?.Count ?? 0) == promiseCountBeforeInvalid,
            "invalid promise proposals must fail before they change the interaction ledger");
        NpcDialogueService factService = new NpcDialogueService(
            relationHost,
            relationHeroId,
            "状态测试角色",
            string.Empty);
        await InvokePrivateTask(factService, "LoadNpcStateAsync", CancellationToken.None).ConfigureAwait(false);
        Check(((string)GetPrivateField(factService, "_npcCommitments")).IndexOf("送来十袋谷物", System.StringComparison.Ordinal) >= 0,
            "dialogue state refresh must load persisted unresolved commitments");
        string promiseUpdateArguments = new JObject
        {
            ["canonicalContactKey"] = relationHeroId,
            ["promiseId"] = promiseId,
            ["newStatus"] = AwakePromiseStateMachine.Kept,
            ["reason"] = "粮食已经送达"
        }.ToString(Newtonsoft.Json.Formatting.None);
        OperationResult<string> promiseUpdated = await bridge.ExecuteAsync(
            new WorldCommandProposal(
                AiTaskConstants.PromiseUpdateCommandId,
                promiseUpdateArguments,
                "承诺已经履行"),
            "dialogue-promise-update",
            CancellationToken.None).ConfigureAwait(false);
        Check(promiseUpdated.IsSuccess,
            "promise status update must settle before the next dialogue refresh");
        await InvokePrivateTask(factService, "LoadNpcStateAsync", CancellationToken.None).ConfigureAwait(false);
        Check(string.IsNullOrWhiteSpace((string)GetPrivateField(factService, "_npcCommitments")),
            "dialogue state refresh must discard commitments resolved between turns");
        NpcDialogueCommandProposal confirmationProposal = new NpcDialogueCommandProposal(
            AiTaskConstants.RelationshipDeltaCommandId,
            new JObject
            {
                ["heroId"] = relationHeroId,
                ["trustDelta"] = 2,
                ["loveDelta"] = 0,
                ["hostilityDelta"] = 0,
                ["reason"] = "确认后的变化"
            }.ToString(Newtonsoft.Json.Formatting.None),
            "确认后增加信任");
        SetPrivateField(factService, "_generation", 31);
        SetPrivateField(factService, "_sending", true);
        SetPrivateField(factService, "_actionMode", NpcDialogueActionMode.Negotiation);
        SetPrivateField(factService, "_pendingPlayerText", "我会先送来粮食。");
        InvokePrivateVoid(
            factService,
            "HandleCompleted",
            31,
            "dialogue-confirmation-fixture",
            new AiTaskEvent(
                "task-confirmation",
                "message-confirmation",
                AiTaskEventKind.Completed,
                1,
                string.Empty,
                null,
                string.Empty,
                0,
                0,
                new JObject
                {
                    ["reply"] = "先把粮食送来，再谈别的。",
                    ["mood"] = "审慎",
                    ["command"] = new JObject
                    {
                        ["commandId"] = confirmationProposal.CommandId,
                        ["arguments"] = JObject.Parse(confirmationProposal.ArgumentsJson),
                        ["reason"] = confirmationProposal.Reason
                    }
                }.ToString(Newtonsoft.Json.Formatting.None),
                string.Empty,
                string.Empty,
                string.Empty));
        bool confirmationRaised = false;
        NpcDialogueUiEvent fixtureEvent;
        while (factService.TryDrainUiEvent(out fixtureEvent))
        {
            if (fixtureEvent.Kind == NpcDialogueUiEventKind.CommandConfirmationRequired
                && fixtureEvent.Confirmation != null)
            {
                confirmationRaised = true;
            }
        }
        Check(confirmationRaised,
            "a model command must become a confirmation event before any settlement");
        factService.RejectPendingCommand();
        relationship = await relationStore.GetRelationshipAsync(relationHeroId, relationContext, CancellationToken.None).ConfigureAwait(false);
        Check((int)relationship["trust"] == 3,
            "rejecting a dialogue proposal must leave game state unchanged");
        factService.Dispose();
        await relationStore.BeginFinalDrainAsync().ConfigureAwait(false);

        ProductionSmokeHost deniedHost = CreateHost("dialogue-promise-denied", false, true);
        ProductionSmokeKeyValueStore deniedWorldEvents = new ProductionSmokeKeyValueStore(AiTaskConstants.WorldEventsNamespace);
        ProductionSmokeKeyValueStore deniedInteractions = new ProductionSmokeKeyValueStore(AiTaskConstants.InteractionsNamespace);
        WorldStateStore deniedStore = CreateStore(deniedHost, deniedWorldEvents);
        deniedStore.InjectStoreForTesting(AiTaskConstants.InteractionsNamespace, deniedInteractions);
        await Install(deniedStore).ConfigureAwait(false);
        OperationResult<string> deniedPromise = await new WorldCommandBridge(deniedHost).ExecuteAsync(
            new WorldCommandProposal(
                AiTaskConstants.PromiseRequestCommandId,
                promiseArguments,
                "权限拒绝时不得入账"),
            "dialogue-promise-denied",
            CancellationToken.None).ConfigureAwait(false);
        Check(!deniedPromise.IsSuccess && deniedInteractions.Read(WorldStateStore.BuildInteractionKey(relationHeroId)) == null,
            "denied promise proposals must not create an interaction ledger entry");
        await deniedStore.BeginFinalDrainAsync().ConfigureAwait(false);
    }

    private static Task InvokePrivateTask(object target, string name, params object[] arguments)
    {
        System.Reflection.MethodInfo method = target.GetType().GetMethod(
            name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (method == null) throw new System.InvalidOperationException("missing private method: " + name);
        return (Task)method.Invoke(target, arguments);
    }

    private static void InvokePrivateVoid(object target, string name, params object[] arguments)
    {
        System.Reflection.MethodInfo method = target.GetType().GetMethod(
            name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (method == null) throw new System.InvalidOperationException("missing private method: " + name);
        method.Invoke(target, arguments);
    }

    private static string FindContextRow(
        System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, string>> rows,
        string key)
    {
        foreach (System.Collections.Generic.KeyValuePair<string, string> row in rows)
        {
            if (System.StringComparer.Ordinal.Equals(row.Key, key)) return row.Value;
        }
        return string.Empty;
    }

    private static object GetPrivateField(object target, string name)
    {
        System.Reflection.FieldInfo field = target.GetType().GetField(
            name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (field == null) throw new System.InvalidOperationException("missing private field: " + name);
        return field.GetValue(target);
    }

}
