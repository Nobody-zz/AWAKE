using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarcusAIFramework.Api;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Awake;

internal static class NpcMemoryConstants
{
    internal const string RouteId = AiTaskConstants.RouteMemoryDaily;
    internal const string PromptId = "awake.npc.memory.summary.v1";
    internal const string OutputContractId = "awake.npc.memory.summary.output.v1";
    internal const int SummaryMaximumChars = AiTaskConstants.MemorySummaryMaximumChars;
    internal const int FactsMaximum = AiTaskConstants.MemoryFactsMaximum;
    internal const int EntryMaximumBytes = AiTaskConstants.MemoryEntryMaximumBytes;
    internal const int PinnedMaximum = AiTaskConstants.MemoryPinnedMaximum;
    internal const int EntriesMaximum = AiTaskConstants.MemoryEntriesMaximum;
    internal const int TopKMaximum = 8;
    internal const int MemoryBlockMaximumBytes = 2500;
}

internal sealed class NpcMemoryFact
{
    internal string Text { get; }

    internal NpcMemoryFact(string text)
    {
        Text = text ?? string.Empty;
    }
}

internal static class NpcMemoryFactsBuilder
{
    internal static JArray Build(IReadOnlyList<NpcMemoryFact> facts)
    {
        JArray result = new JArray();
        if (facts == null) return result;
        int count = 0;
        foreach (NpcMemoryFact fact in facts)
        {
            if (count >= NpcMemoryConstants.FactsMaximum) break;
            if (fact == null || string.IsNullOrWhiteSpace(fact.Text)) continue;
            result.Add(AwakeRuntime.TruncateTextElements(fact.Text, 120));
            count++;
        }
        return result;
    }
}

internal static class NpcMemorySelector
{
    internal static string FormatTopK(JObject doc, int maximumEntries, int maximumBytes)
    {
        if (doc == null || !(doc["memories"] is JArray memories)) return string.Empty;
        List<JObject> entries = new List<JObject>();
        foreach (JToken token in memories)
        {
            if (token is JObject entry) entries.Add(entry);
        }
        entries.Sort((a, b) =>
        {
            int weightCompare = IntValue(b["weight"]).CompareTo(IntValue(a["weight"]));
            if (weightCompare != 0) return weightCompare;
            int dayCompare = IntValue(b["day"]).CompareTo(IntValue(a["day"]));
            if (dayCompare != 0) return dayCompare;
            return string.CompareOrdinal((string)a["id"] ?? string.Empty, (string)b["id"] ?? string.Empty);
        });
        if (entries.Count > maximumEntries) entries.RemoveRange(maximumEntries, entries.Count - maximumEntries);

        StringBuilder builder = new StringBuilder();
        int budget = 0;
        foreach (JObject entry in entries)
        {
            string line = FormatLine(entry);
            if (string.IsNullOrWhiteSpace(line)) continue;
            int consumed;
            string clamped = ClampToBytes(line, maximumBytes - budget, out consumed);
            if (consumed <= 0) break;
            if (builder.Length > 0)
            {
                budget += Encoding.UTF8.GetByteCount("\n");
                if (budget > maximumBytes) break;
                builder.Append('\n');
            }
            builder.Append(clamped);
            budget += consumed;
        }
        return builder.ToString();
    }

    private static string FormatLine(JObject entry)
    {
        int day = IntValue(entry["day"]);
        string type = (string)entry["type"] ?? "shared_experience";
        string summary = (string)entry["summary"] ?? string.Empty;
        JArray facts = entry["facts"] as JArray;
        List<string> factTexts = new List<string>();
        if (facts != null)
        {
            foreach (JToken token in facts)
            {
                if (token is JValue value && !string.IsNullOrWhiteSpace(Convert.ToString(value)))
                {
                    factTexts.Add(Convert.ToString(value));
                }
            }
        }
        StringBuilder line = new StringBuilder();
        line.Append("· 第").Append(day).Append("天（").Append(type).Append("）：");
        if (!string.IsNullOrWhiteSpace(summary)) line.Append(summary);
        if (factTexts.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(summary)) line.Append('；');
            line.Append(string.Join("、", factTexts));
        }
        return line.ToString();
    }

    private static string ClampToBytes(string value, int budget, out int consumed)
    {
        consumed = 0;
        if (string.IsNullOrEmpty(value) || budget <= 0) return string.Empty;
        StringBuilder builder = new StringBuilder();
        System.Globalization.TextElementEnumerator enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(value);
        while (enumerator.MoveNext())
        {
            string element = enumerator.GetTextElement();
            int elementBytes = Encoding.UTF8.GetByteCount(element);
            if (consumed + elementBytes > budget) break;
            builder.Append(element);
            consumed += elementBytes;
        }
        return builder.ToString();
    }

    private static int IntValue(JToken token)
    {
        if (token == null || token.Type != JTokenType.Integer) return 0;
        try { return (int)token; } catch { return 0; }
    }
}

internal static class NpcMemorySummaryTemplate
{
    internal static string BuildInput(string heroId, JArray facts, string summaryHint)
    {
        string factText = facts == null ? string.Empty : string.Join("、", facts);
        string hint = AwakeRuntime.TruncateTextElements(summaryHint ?? string.Empty, 400);
        StringBuilder builder = new StringBuilder();
        builder.Append("你是卡拉迪亚的记忆书记官，任务是把玩家与 NPC（" + heroId + "）之间最近一次交谈压缩成一句跨会话记忆摘要。");
        builder.Append("只依据下面提供的事实与对话提示，不编造、不评价、不使用现代词汇。");
        builder.Append("输出必须是 JSON 对象，只含一个字段：{\"summary\": \"...\"}，summary 不超过 240 字。");
        builder.Append("\n事实：").Append(string.IsNullOrWhiteSpace(factText) ? "无" : factText);
        builder.Append("\n对话提示：").Append(string.IsNullOrWhiteSpace(hint) ? "无" : hint);
        return builder.ToString();
    }

    internal static string ParseSummary(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        try
        {
            JObject root = JObject.Parse(text);
            string summary = (string)root["summary"];
            return AwakeRuntime.TruncateTextElements(summary ?? string.Empty, NpcMemoryConstants.SummaryMaximumChars);
        }
        catch
        {
            return string.Empty;
        }
    }
}

internal sealed class NpcMemoryService : IDisposable
{
    private static NpcMemoryService _current;
    private readonly object _gate = new object();
    private readonly IMarcusAiFrameworkHost _host;
    private readonly AiTaskGateway _gateway;
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private readonly object _backgroundGate = new object();
    private readonly List<Task> _backgroundTasks = new List<Task>();
    private bool _disposed;

    internal static NpcMemoryService Current
    {
        get { lock (typeof(NpcMemoryService)) return _current; }
    }

    internal static void SetCurrent(NpcMemoryService service)
    {
        lock (typeof(NpcMemoryService))
        {
            _current = service;
        }
    }

    internal static void ShutdownCurrent()
    {
        NpcMemoryService service;
        lock (typeof(NpcMemoryService))
        {
            service = _current;
            _current = null;
        }
        if (service != null)
        {
            try
            {
                service.Dispose();
            }
            catch (Exception ex)
            {
                AwakeLog.Write("npc_memory_shutdown_error error=" + ex.Message);
            }
        }
    }

    internal void TrackBackground(Task task)
    {
        if (task == null) return;
        lock (_backgroundGate)
        {
            _backgroundTasks.Add(task);
        }
        _ = task.ContinueWith(
            _ =>
            {
                lock (_backgroundGate)
                {
                    _backgroundTasks.Remove(task);
                }
            },
            TaskScheduler.Default);
    }

    internal async Task DrainBackgroundAsync(int timeoutMilliseconds)
    {
        Task[] tasks;
        lock (_backgroundGate)
        {
            tasks = _backgroundTasks.ToArray();
        }
        if (tasks.Length == 0) return;
        try
        {
            Task all = Task.WhenAll(tasks);
            Task delay = Task.Delay(timeoutMilliseconds);
            await Task.WhenAny(all, delay).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    internal NpcMemoryService(IMarcusAiFrameworkHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _gateway = new AiTaskGateway(host);
    }

    internal bool Reserve(string heroId, string entrySource, int day, out string conversationId)
    {
        conversationId = string.Empty;
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null || _disposed) return false;
        int sequence;
        return store.ReserveMemory(heroId, entrySource, day, out conversationId, out sequence);
    }

    internal async Task<string> LoadMemoryBlockAsync(string heroId, CancellationToken cancellationToken)
    {
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null || string.IsNullOrWhiteSpace(heroId) || _disposed) return string.Empty;
        JObject doc;
        try
        {
            RequestContext context = AwakeRuntime.CreateContext(_host, Guid.NewGuid().ToString("N"));
            doc = await store.GetMemoriesAsync(heroId, context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_memory_load_error hero=" + heroId + " error=" + ex.Message);
            return string.Empty;
        }
        return NpcMemorySelector.FormatTopK(doc, NpcMemoryConstants.TopKMaximum, NpcMemoryConstants.MemoryBlockMaximumBytes);
    }

    internal async Task<bool> CloseConversationAsync(
        string heroId,
        string conversationId,
        int day,
        IReadOnlyList<NpcMemoryFact> facts,
        string summaryHint,
        string source,
        CancellationToken cancellationToken)
    {
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null || string.IsNullOrWhiteSpace(conversationId) || _disposed) return false;
        try
        {
            JArray factArray = NpcMemoryFactsBuilder.Build(facts);
            bool flushed = await store.FlushMemoryFactsAsync(
                heroId,
                conversationId,
                day,
                "shared_experience",
                factArray,
                string.Empty,
                2,
                string.IsNullOrWhiteSpace(source) ? "npc_dialogue" : source,
                cancellationToken).ConfigureAwait(false);
            if (!flushed)
            {
                AwakeLog.Write("npc_memory_facts_flush_failed hero=" + heroId + " conversation=" + conversationId);
                return false;
            }
            if (!string.IsNullOrWhiteSpace(summaryHint))
            {
                string summary = await SummarizeAsync(heroId, conversationId, factArray, summaryHint, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    await store.PatchMemorySummaryAsync(heroId, conversationId, summary, cancellationToken).ConfigureAwait(false);
                }
            }
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_memory_close_error hero=" + heroId + " error=" + ex.Message);
            return false;
        }
    }

    internal async Task<bool> RecordEventFactAsync(
        string heroId,
        int day,
        string type,
        IReadOnlyList<NpcMemoryFact> facts,
        int weight,
        string source,
        CancellationToken cancellationToken)
    {
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null || string.IsNullOrWhiteSpace(heroId) || _disposed) return false;
        try
        {
            return await store.AppendEventMemoryAsync(
                heroId,
                day,
                type,
                NpcMemoryFactsBuilder.Build(facts),
                weight,
                source,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_memory_event_fact_error hero=" + heroId + " error=" + ex.Message);
            return false;
        }
    }

    private async Task<string> SummarizeAsync(
        string heroId,
        string conversationId,
        JArray facts,
        string summaryHint,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_disposed) return string.Empty;
            RequestContext context = AwakeRuntime.CreateContext(_host, Guid.NewGuid().ToString("N"));
            TaskCompletionSource<string> completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action<AiTaskEvent> onEvent = evt =>
            {
                try
                {
                    if (evt == null) return;
                    if (evt.Kind == AiTaskEventKind.Completed)
                    {
                        completion.TrySetResult(NpcMemorySummaryTemplate.ParseSummary(evt.Text));
                    }
                    else if (evt.Kind == AiTaskEventKind.Failed || evt.Kind == AiTaskEventKind.Cancelled)
                    {
                        completion.TrySetResult(string.Empty);
                    }
                }
                catch
                {
                }
            };
            string input = NpcMemorySummaryTemplate.BuildInput(heroId, facts, summaryHint);
            AiTaskSubmitResult submitted = await _gateway.SubmitAsync(
                NpcMemoryConstants.RouteId,
                input,
                NpcMemoryConstants.OutputContractId,
                CloudExportPolicy.None,
                false,
                onEvent,
                context,
                cancellationToken).ConfigureAwait(false);
            if (!submitted.Ok)
            {
                AwakeLog.Write("npc_memory_summary_submit_failed code=" + submitted.ErrorCode + " conversation=" + conversationId);
                return string.Empty;
            }
            Task completed = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(30), cancellationToken)).ConfigureAwait(false);
            if (!ReferenceEquals(completed, completion.Task)) return string.Empty;
            return await completion.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return string.Empty;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_memory_summary_error hero=" + heroId + " error=" + ex.Message);
            return string.Empty;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        try
        {
            _cts.Cancel();
        }
        catch
        {
        }
        try
        {
            _cts.Dispose();
        }
        catch
        {
        }
        try
        {
            _gateway.Dispose();
        }
        catch (Exception ex)
        {
            AwakeLog.Write("npc_memory_gateway_dispose_error error=" + ex.Message);
        }
        AwakeLog.Write("npc_memory_service_disposed");
    }
}
