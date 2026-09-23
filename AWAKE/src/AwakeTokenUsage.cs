using System;
using System.Collections.Generic;
using MarcusAwakeFramework.Api;

namespace Awake;

/// <summary>
/// AI 用量记账（2026-09-23 补）。
///
/// 背景：框架侧早已把 usage 采出来、发成 UsageUpdate 事件（framework 自测都写了），
/// 但 AWAKE 侧的事件消费从没接过这一类 ⇒ 日志里看不到任何 token 数，也无法回答
/// "某个路由今天花了多少"。
///
/// 本类零依赖：不碰存储、不碰游戏对象、不发事故 ⇒ 离线验台能直接压到它
/// （参照 P1-05 摘 AwakeLetterCommit 的做法）。
///
/// 为什么按「同一任务取各自最大值」而不是累加：
/// 一次流式请求会发多次 UsageUpdate，后一次是**从头累计**的值（Anthropic 的
/// message_delta 帧尤其明显），不是"这一段新增加的量"。累加会把同一笔算好几遍。
/// 终态事件里的值也一并并入最大值 —— 有些 provider 只在终态给数；
/// 反过来，框架兜底发出的取消/失败终态是硬编码 0（RuntimeServiceClient 的
/// PublishCancellation / PublishFailure），这时靠前面攒下来的 UsageUpdate 才救得回来。
/// </summary>
internal static class AwakeTokenUsage
{
    internal const string LineTag = "token_usage";

    /// <summary>未结算任务的保管上限。终态必然到达 ⇒ 正常只会剩几条；留上限只为防终态缺席时的无界增长。</summary>
    private const int PendingMaximum = 512;

    private static readonly object Gate = new object();
    private static readonly Dictionary<string, Usage> Pending = new Dictionary<string, Usage>(StringComparer.Ordinal);
    private static readonly Dictionary<string, Usage> RouteTotals = new Dictionary<string, Usage>(StringComparer.Ordinal);

    private sealed class Usage
    {
        internal long InputTokens;
        internal long OutputTokens;
    }

    /// <summary>
    /// 事件入口。用量更新 ⇒ 入账；终态 ⇒ 结算并落一行日志；其余 ⇒ 忽略。
    /// 两个调用点（对话、每日记忆归纳）共用这一份实现，计数口径不可能不一致。
    /// </summary>
    internal static void Track(AiTaskEvent evt, string routeId)
    {
        if (evt == null) return;
        string route = Normalize(routeId);
        if (evt.Kind == AiTaskEventKind.UsageUpdate)
        {
            Remember(evt, route);
            return;
        }
        if (evt.Kind == AiTaskEventKind.Completed
            || evt.Kind == AiTaskEventKind.Failed
            || evt.Kind == AiTaskEventKind.Cancelled)
        {
            Settle(evt, route);
        }
    }

    private static void Remember(AiTaskEvent evt, string route)
    {
        string taskId = evt.TaskId ?? string.Empty;
        lock (Gate)
        {
            if (Pending.Count >= PendingMaximum && !Pending.ContainsKey(taskId)) return;
            Usage usage;
            if (!Pending.TryGetValue(taskId, out usage))
            {
                usage = new Usage();
                Pending[taskId] = usage;
            }
            Raise(usage, evt.InputTokens, evt.OutputTokens);
        }
    }

    private static void Settle(AiTaskEvent evt, string route)
    {
        string taskId = evt.TaskId ?? string.Empty;
        long routeIn;
        long routeOut;
        lock (Gate)
        {
            Usage usage;
            Pending.TryGetValue(taskId, out usage);
            Pending.Remove(taskId);
            long inputTokens = evt.InputTokens;
            long outputTokens = evt.OutputTokens;
            if (usage != null)
            {
                if (usage.InputTokens > inputTokens) inputTokens = usage.InputTokens;
                if (usage.OutputTokens > outputTokens) outputTokens = usage.OutputTokens;
            }
            Usage totals;
            if (!RouteTotals.TryGetValue(route, out totals))
            {
                totals = new Usage();
                RouteTotals[route] = totals;
            }
            totals.InputTokens += inputTokens;
            totals.OutputTokens += outputTokens;
            routeIn = totals.InputTokens;
            routeOut = totals.OutputTokens;
            AwakeLog.Write(LineTag
                + " route=" + route
                + " task=" + taskId
                + " model=" + (evt.ResolvedModel ?? string.Empty)
                + " outcome=" + evt.Kind.ToString().ToLowerInvariant()
                + " in=" + inputTokens
                + " out=" + outputTokens
                + " route_total_in=" + routeIn
                + " route_total_out=" + routeOut);
        }
    }

    private static void Raise(Usage usage, long inputTokens, long outputTokens)
    {
        if (usage == null) return;
        // 每帧报的是累计值 ⇒ 取各自最大的那一次，不是加起来。
        if (inputTokens > usage.InputTokens) usage.InputTokens = inputTokens;
        if (outputTokens > usage.OutputTokens) usage.OutputTokens = outputTokens;
    }

    private static string Normalize(string routeId)
    {
        return string.IsNullOrWhiteSpace(routeId) ? "unknown" : routeId;
    }

    /// <summary>清空全部账目。只给离线验台用。</summary>
    internal static void Reset()
    {
        lock (Gate)
        {
            Pending.Clear();
            RouteTotals.Clear();
        }
    }

    /// <summary>某个路由到目前为止的累计。离线验台用它断言"按路由能分开"。</summary>
    internal static bool TryGetRouteTotal(string routeId, out long inputTokens, out long outputTokens)
    {
        inputTokens = 0;
        outputTokens = 0;
        lock (Gate)
        {
            Usage usage;
            if (!RouteTotals.TryGetValue(Normalize(routeId), out usage)) return false;
            inputTokens = usage.InputTokens;
            outputTokens = usage.OutputTokens;
            return true;
        }
    }
}
