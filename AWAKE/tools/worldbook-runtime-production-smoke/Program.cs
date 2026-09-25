using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Awake.WorldbookRuntimeProductionSmoke;

internal static partial class Program
{
    private static readonly object OutputGate = new object();
    private static readonly List<string> CapturedLogs = new List<string>();
    private static int _passed;
    private static int _failed;
    private static readonly List<Task> ObservedBackgroundTasks = new List<Task>();

    private static int Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "persona-prompt-render")
        {
            var variables = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(args[1]));
            if (variables == null) throw new InvalidDataException("Prompt variables must be a JSON object.");
            foreach (System.Text.RegularExpressions.Match placeholder in
                System.Text.RegularExpressions.Regex.Matches(NpcPromptTemplate.TemplateText, @"\{\{([A-Za-z0-9_]+)\}\}"))
            {
                if (!variables.ContainsKey(placeholder.Groups[1].Value))
                    throw new InvalidDataException("Missing prompt variable: " + placeholder.Groups[1].Value);
            }
            string prompt = NpcDialoguePromptPipeline.RenderTemplate(NpcPromptTemplate.TemplateText, variables);
            File.WriteAllText(args[2], prompt);
            Console.WriteLine("PROMPT_RENDERED=" + args[2]);
            return 0;
        }
        return MainAsync().GetAwaiter().GetResult();
    }

    private static async Task<int> MainAsync()
    {
        AwakeLog.Enabled = false;
        AwakeLog.Recorder = line =>
        {
            lock (OutputGate) CapturedLogs.Add(line ?? string.Empty);
        };
        try
        {
            await RunAsync("lifecycle-final-drain", TestLifecycleAndFinalDrainAsync).ConfigureAwait(false);
            await RunAsync("unknown-state-kind-rejected", TestUnknownStateKindRejectedAsync).ConfigureAwait(false);
            await RunAsync("storage-schema-contract", TestStorageSchemaContractAsync).ConfigureAwait(false);
            await RunAsync("npc-dialogue-structured-completion", TestNpcDialogueStructuredCompletionAsync).ConfigureAwait(false);
            await RunAsync("npc-dialogue-confirmed-settlement-observation", TestNpcDialogueConfirmedSettlementObservationAsync).ConfigureAwait(false);
            await RunAsync("npc-dialogue-context-simulation", TestNpcDialogueContextSimulationAsync).ConfigureAwait(false);
            await RunAsync("npc-dialogue-send-async-roundtrip", TestNpcDialogueSendAsyncRoundtripAsync).ConfigureAwait(false);
            await RunAsync("npc-dialogue-action-mode-gate", TestNpcDialogueActionModeGateAsync).ConfigureAwait(false);
            await RunAsync("developer-negotiation-entry", TestDeveloperNegotiationEntryAsync).ConfigureAwait(false);
            await RunAsync("npc-memory-structured-content", TestNpcMemoryStructuredContentAsync).ConfigureAwait(false);
            await RunAsync("letter-delivery-lifecycle", TestLetterDeliveryLifecycleAsync).ConfigureAwait(false);
            await RunAsync("npc-proactive-letter-initiative", TestNpcProactiveLetterInitiativeAsync).ConfigureAwait(false);
            await RunAsync("final-drain-single-flight", TestFinalDrainSingleFlightAsync).ConfigureAwait(false);
            await RunAsync("ensure-world-state-replacement", TestEnsureWorldStateReplacementAsync).ConfigureAwait(false);
            await RunAsync("record-before-boundary", TestRecordBeforeBoundaryAsync).ConfigureAwait(false);
            await RunAsync("record-after-boundary", TestRecordAfterBoundaryAsync).ConfigureAwait(false);
            await RunAsync("queued-record-generation", TestQueuedRecordGenerationAsync).ConfigureAwait(false);
            await RunAsync("load-cannot-clear-new-events", TestLoadCannotClearNewEventsAsync).ConfigureAwait(false);
            await RunAsync("store-switch-blocks-old-load", TestStoreSwitchBlocksOldLoadAsync).ConfigureAwait(false);
            await RunAsync("readiness-projection-monotonicity", TestReadinessProjectionMonotonicityAsync).ConfigureAwait(false);
            await RunAsync("weekly-report-snapshot-repair", TestWeeklyReportSnapshotRepairAsync).ConfigureAwait(false);
            await RunAsync("submodule-reset-boundary", TestSubModuleResetBoundaryAsync).ConfigureAwait(false);
            await RunAsync("probe-campaign-ready-boundary", TestProbeCampaignReadyBoundaryAsync).ConfigureAwait(false);
            await RunAsync("direct-write-replacement-boundary", TestDirectWriteReplacementBoundaryAsync).ConfigureAwait(false);
            await RunAsync("failed-drain-fail-closed", TestFailedDrainFailsClosedAsync).ConfigureAwait(false);
            await RunAsync("probe-session-ending-nonblocking", TestProbeSessionEndingNonBlockingAsync).ConfigureAwait(false);
            await RunAsync("pending-event-provenance-is-private", TestPendingEventProvenanceAsync).ConfigureAwait(false);
            await RunAsync("world-fact-event-caller", RunWorldFactEventCallerTests).ConfigureAwait(false);
            await RunAsync("world-fact-trigger-caller", RunWorldFactTriggerCallerTests).ConfigureAwait(false);
            await RunAsync("world-fact-trigger-content-api", RunWorldFactTriggerContentApiTests).ConfigureAwait(false);
            await RunAsync("world-fact-event-cancellation", RunWorldFactEventCancellationTests).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await AwakeRuntime.ResetSessionStateForTestingAsync().ConfigureAwait(false);
                CampaignResetLifecycle.Reset = null;
                AwakeBackgroundTask.ObserverForTesting = null;
                WorldEventServices.ResetForCampaign();
            }
            catch
            {
            }
            AwakeLog.Recorder = null;
            AwakeLog.Enabled = true;
        }

        Console.WriteLine("RESULT passed=" + _passed + " failed=" + _failed);
        return _failed == 0 ? 0 : 1;
    }

    private static async Task RunAsync(string name, Func<Task> test)
    {
        try
        {
            await ResetHarnessStateAsync().ConfigureAwait(false);
            await test().ConfigureAwait(false);
            _passed++;
            Console.WriteLine("PASS " + name);
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine("FAIL " + name + " :: " + FlattenException(ex));
        }
        finally
        {
            await CleanupHarnessStateAsync().ConfigureAwait(false);
        }
    }

    private static async Task ResetHarnessStateAsync()
    {
        await CleanupHarnessStateAsync().ConfigureAwait(false);
        WorldEventServices.ResetForCampaign();
        AwakeRuntime.CurrentGameDayProvider = () => 100;
        AwakeRuntime.NativeReadinessProbeForTesting = null;
        AwakeRuntime.SetHostOverrideForTesting(null);
        lock (OutputGate) CapturedLogs.Clear();
        AwakeBackgroundTask.ObserverForTesting = TrackBackgroundTask;
    }

    private static async Task CleanupHarnessStateAsync()
    {
        CampaignResetLifecycle.Reset = null;
        try
        {
            await AwakeRuntime.ResetSessionStateForTestingAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            lock (OutputGate) CapturedLogs.Add("harness_runtime_cleanup_error " + ex.Message);
        }

        while (true)
        {
            Task[] pending;
            lock (ObservedBackgroundTasks)
            {
                pending = ObservedBackgroundTasks.Where(task => task != null && !task.IsCompleted).ToArray();
            }
            if (pending.Length == 0) break;
            try
            {
                await Task.WhenAll(pending).ConfigureAwait(false);
            }
            catch
            {
            }
        }
        AwakeBackgroundTask.ObserverForTesting = null;
        lock (ObservedBackgroundTasks) ObservedBackgroundTasks.Clear();
        WorldEventServices.ResetForCampaign();
    }

    private static void TrackBackgroundTask(Task task, string label)
    {
        if (task == null) return;
        lock (ObservedBackgroundTasks) ObservedBackgroundTasks.Add(task);
    }

    private static ProductionSmokeHost CreateHost(string suffix, bool grantPermissions = true, bool enableWorldCommands = false)
    {
        return new ProductionSmokeHost(
            new MarcusAwakeFramework.Api.SessionRef(
                "campaign-production-smoke",
                "timeline-production-smoke",
                "session-" + suffix),
            grantPermissions,
            enableWorldCommands);
    }

    private static WorldStateStore CreateStore(ProductionSmokeHost host, ProductionSmokeKeyValueStore worldEvents, ProductionSmokeKeyValueStore memories = null)
    {
        WorldStateStore store = new WorldStateStore(host);
        store.InjectStoreForTesting(AiTaskConstants.WorldEventsNamespace, worldEvents);
        if (memories != null) store.InjectStoreForTesting(AiTaskConstants.NpcMemoriesNamespace, memories);
        return store;
    }

    private static async Task Install(WorldStateStore store)
    {
        bool installed = await AwakeRuntime.SetWorldStateStore(store).ConfigureAwait(false);
        Check(installed, "production smoke store installation must complete");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static async Task WaitForAsync(Task task, string message)
    {
        await WithTimeoutAsync(task, message).ConfigureAwait(false);
    }

    private static async Task WithTimeoutAsync(Task task, string message)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(5000)).ConfigureAwait(false);
        if (!ReferenceEquals(completed, task)) throw new TimeoutException(message);
        await task.ConfigureAwait(false);
    }

    private static async Task<T> WithTimeoutAsync<T>(Task<T> task, string message)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(5000)).ConfigureAwait(false);
        if (!ReferenceEquals(completed, task)) throw new TimeoutException(message);
        return await task.ConfigureAwait(false);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, string message, int timeoutMilliseconds)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (DateTime.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(20).ConfigureAwait(false);
        }
        throw new TimeoutException(message);
    }

    private static bool HasCapturedLog(string fragment)
    {
        lock (OutputGate)
        {
            foreach (string line in CapturedLogs)
            {
                if (line != null && line.IndexOf(fragment ?? string.Empty, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }
    }

    private static string RecentCapturedLogs()
    {
        lock (OutputGate)
        {
            int start = Math.Max(0, CapturedLogs.Count - 12);
            return string.Join(" || ", CapturedLogs.Skip(start));
        }
    }

    private static JObject ParseJsonObjectPreservingDateStrings(string json)
    {
        using (StringReader reader = new StringReader(json ?? string.Empty))
        using (JsonTextReader jsonReader = new JsonTextReader(reader) { DateParseHandling = DateParseHandling.None })
        {
            return JObject.Load(jsonReader);
        }
    }

    private static string FlattenException(Exception exception)
    {
        var messages = new List<string>();
        for (Exception current = exception; current != null; current = current.InnerException)
            messages.Add(current.GetType().Name + ":" + current.Message);
        return string.Join(" -> ", messages);
    }
}
