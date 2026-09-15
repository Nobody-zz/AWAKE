using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Awake;

// A confirmed command is no longer owned by a Gauntlet panel.  This runner only
// owns its final observation; the existing command bridge remains the sole writer.
internal static class NpcDialogueConfirmedSettlementRunner
{
    private static readonly object Gate = new object();
    private static readonly HashSet<string> ActiveCorrelations = new HashSet<string>(StringComparer.Ordinal);

    internal static void Track(string heroId, int generation, string correlationId, Task<NpcDialogueCommandSettlement> settlementTask)
    {
        if (settlementTask == null || string.IsNullOrWhiteSpace(correlationId)) return;
        lock (Gate)
        {
            if (!ActiveCorrelations.Add(correlationId)) return;
        }
        AwakeBackgroundTask.Run(async () =>
        {
            NpcDialogueCommandSettlement settlement;
            try
            {
                settlement = await settlementTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AwakeLog.Write("npc_dialogue_command_result_wait_error error=" + ex.Message);
                settlement = new NpcDialogueCommandSettlement(false, string.Empty);
            }
            finally
            {
                lock (Gate) ActiveCorrelations.Remove(correlationId);
            }
            AwakeLog.Write("npc_dialogue_turn_completed hero=" + (heroId ?? string.Empty)
                + " generation=" + generation
                + " correlation=" + correlationId
                + " completion_kind=" + (settlement != null && settlement.Succeeded ? "command_confirmed" : "command_settlement_failed"));
        }, "npc_dialogue_confirmed_settlement");
    }
}
