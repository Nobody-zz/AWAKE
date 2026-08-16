using System;
using System.Reflection;
using MarcusAIFramework.Api;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

[assembly: AssemblyTitle("Awake")]
[assembly: AssemblyProduct("Awake")]
[assembly: AssemblyVersion("0.2.0.0")]
[assembly: AssemblyFileVersion("0.2.0.0")]
[assembly: AssemblyInformationalVersion(Awake.AwakeVersion.InformationalVersion)]

namespace Awake;

public sealed class SubModule : MBSubModuleBase
{
    private AwakeExtension _extension;

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();
        DialogueOverlayLifecycle.CloseAll = CloseDialogueOverlays;
        CampaignResetLifecycle.Reset = ResetCampaignState;
        AwakeLog.Write("module_load id=Awake version=" + AwakeVersion.Version);
        _extension = new AwakeExtension();
        OperationResult<bool> registration = FrameworkHostLocator.Register(_extension);
        if (registration.IsSuccess && registration.Value)
        {
            AwakeLog.Write("register_ok");
        }
        else
        {
            AwakeLog.Write("register_failed code=" + (registration.Error?.Code ?? "unknown")
                + " category=" + (registration.Error?.Category.ToString() ?? "unknown")
                + " detail=" + (registration.Error?.SafeFallback ?? ""));
        }
    }

    protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
    {
        base.OnGameStart(game, gameStarterObject);
        ResetCampaignState();
        if (gameStarterObject is CampaignGameStarter campaignStarter)
        {
            try
            {
                campaignStarter.AddBehavior(new AwakeTerminalBehavior());
                AwakeLog.Write("awake_terminal_behavior_added");
                campaignStarter.AddBehavior(new AwakeEncounterBehavior());
                AwakeLog.Write("awake_encounter_behavior_added");
                campaignStarter.AddBehavior(new AwakeEventBehavior());
                AwakeLog.Write("awake_event_behavior_added");
            }
            catch (Exception ex)
            {
                AwakeLog.Write("awake_terminal_behavior_add_failed error=" + ex.Message);
            }
            NpcProactiveHooks.GetNearbyHeroes = limit => NpcDialogueLauncher.GetNearbyHeroes(limit);
            NpcProactiveHooks.FindHeroById = heroId => NpcDialogueLauncher.FindHeroById(heroId);
            NpcProactiveHooks.IsDialogueOpen = () => NpcDialogueOverlay.IsOpen;
            NpcProactiveHooks.IsMessengerOpen = () => AwakeMessengerOverlay.IsOpen
                || WorldEventInboxOverlay.IsOpen
                || WeeklyReportBrowserOverlay.IsOpen;
            NpcProactiveHooks.RecordDialogueContext = (heroId, hint) => NpcDialogueContext.Record(heroId, hint);
            NpcProactiveHooks.EnqueueDialogue = (heroId, hint) => EventDialogueQueue.Enqueue(heroId, hint);
            AwakeMcmActions.ShowDeveloperReport = AwakeTerminalBehavior.ShowDeveloperReportForMcm;
        }
        AwakeLog.Write("game_start version=" + AwakeVersion.Version);
    }

    protected override void OnApplicationTick(float dt)
    {
        base.OnApplicationTick(dt);
        AwakeTerminalBehavior.TickCurrent();
        NpcProactiveService.Current?.OnApplicationTick();
        AwakeUiDispatcher.InitializeGameThread();
        AwakeUiDispatcher.Drain();
        AwakeMessengerOverlay.OnApplicationTick();
        WorldEventInboxOverlay.OnApplicationTick();
        WeeklyReportBrowserOverlay.OnApplicationTick();
        NpcDialogueOverlay.OnApplicationTick();
        DrainEventDialogueQueue();
    }

    private static void CloseDialogueOverlays()
    {
        AwakeUiDispatcher.Enqueue(() =>
        {
            NpcDialogueOverlay.CloseActive();
        });
    }

    private static void ResetCampaignState()
    {
        try
        {
            DialogueOverlayLifecycle.CloseAll?.Invoke();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("campaign_reset_overlay_close_error error=" + ex.Message);
        }
        WorldEventLedger.ClearForTesting();
        AwakeMessengerHistory.ResetForCampaign();
        NpcDialogueContext.ClearForTesting();
        NpcDialogueLauncher.ClearCache();
        EventDialogueQueue.ClearForTesting();
        NpcProactiveService.ShutdownCurrent();
    }

    private static void DrainEventDialogueQueue()
    {
        try
        {
            if (AwakeRuntime.SessionEnded
                || NpcDialogueOverlay.IsOpen
                || AwakeMessengerOverlay.IsOpen
                )
            {
                return;
            }
            if (!(GameStateManager.Current?.ActiveState is MapState)
                && Campaign.Current?.CurrentMenuContext == null)
            {
                return;
            }
            try
            {
                if (Campaign.Current?.ConversationManager != null
                    && Campaign.Current.ConversationManager.IsConversationFlowActive)
                {
                    return;
                }
            }
            catch
            {
            }
            PendingDialogue pending;
            if (!EventDialogueQueue.TryDequeue(out pending)) return;
            AwakeNpcTarget target = NpcDialogueLauncher.FindTargetById(pending.HeroId);
            if (target == null || !NpcDialogueLauncher.IsEligibleNpcTarget(target))
            {
                WorldEventLedger.Record(AwakeRuntime.CurrentGameDay(), "npc_dialogue_open_failed", pending.HeroId + ":target_unavailable");
                return;
            }
            NpcDialogueContext.Record(pending.HeroId, pending.OpeningHint);
            NpcDialogueLaunchResult result = NpcDialogueLauncher.TryOpenDialogue(target, "event");
            if (result == NpcDialogueLaunchResult.None)
            {
                NpcDialogueContext.TryTake(out _, out _);
                WorldEventLedger.Record(AwakeRuntime.CurrentGameDay(), "npc_dialogue_open_failed", pending.HeroId + ":open_failed");
            }
            else if (result == NpcDialogueLaunchResult.Native)
            {
                NpcDialogueContext.TryTake(out _, out _);
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("event_dialogue_queue_drain_error error=" + ex.Message);
        }
    }
}
