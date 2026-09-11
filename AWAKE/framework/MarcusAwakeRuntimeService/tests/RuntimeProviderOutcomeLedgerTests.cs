using System;
using System.IO;

namespace MarcusAwakeRuntimeService.Tests;

internal static class RuntimeProviderOutcomeLedgerTests
{
    internal static int Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "awake-provider-ledger-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "provider-ledger.json");
            var first = new RuntimeProviderOutcomeLedger(path);
            Require(first.Load(out _), "initial_load_failed");
            Require(first.Reserve("applied", "hash-a", DateTimeOffset.UtcNow) == RuntimeProviderLedgerReservation.New, "applied_reserve_failed");
            Require(first.SetOutcome("applied", RuntimeProviderOutcomeStatus.Applied, DateTimeOffset.UtcNow), "applied_write_failed");

            Require(first.Reserve("retryable", "hash-r", DateTimeOffset.UtcNow) == RuntimeProviderLedgerReservation.New, "retryable_reserve_failed");
            Require(first.SetOutcome("retryable", RuntimeProviderOutcomeStatus.Retryable, DateTimeOffset.UtcNow), "retryable_write_failed");

            Require(first.Reserve("unknown", "hash-u", DateTimeOffset.UtcNow) == RuntimeProviderLedgerReservation.New, "unknown_reserve_failed");

            var restarted = new RuntimeProviderOutcomeLedger(path);
            Require(restarted.Load(out _), "restart_load_failed");
            Require(restarted.Reserve("applied", "hash-a", DateTimeOffset.UtcNow) == RuntimeProviderLedgerReservation.Applied, "applied_was_not_replayed_as_applied");
            Require(restarted.Reserve("retryable", "hash-r", DateTimeOffset.UtcNow) == RuntimeProviderLedgerReservation.New, "retryable_was_not_reopened");
            Require(restarted.Reserve("unknown", "hash-u", DateTimeOffset.UtcNow) == RuntimeProviderLedgerReservation.Unknown, "inflight_was_not_reclassified_as_unknown");
            Require(restarted.Reserve("applied", "hash-b", DateTimeOffset.UtcNow) == RuntimeProviderLedgerReservation.Conflict, "hash_conflict_was_not_rejected");

            var corruptPath = Path.Combine(root, "corrupt.json");
            File.WriteAllText(corruptPath, "{");
            Require(!new RuntimeProviderOutcomeLedger(corruptPath).Load(out _), "corrupt_ledger_was_accepted");

            var unavailablePath = Path.Combine(root, "unavailable");
            Directory.CreateDirectory(unavailablePath);
            var unavailable = new RuntimeProviderOutcomeLedger(unavailablePath);
            Require(!unavailable.Load(out _), "directory_ledger_was_accepted");
            Require(unavailable.Reserve("unavailable", "hash-x", DateTimeOffset.UtcNow) == RuntimeProviderLedgerReservation.Unavailable, "unavailable_ledger_was_written");

            Console.WriteLine("PROVIDER_OUTCOME_LEDGER 6/6 PASS");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("PROVIDER_OUTCOME_LEDGER FAIL " + exception.GetType().Name + ":" + exception.Message);
            return 1;
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
