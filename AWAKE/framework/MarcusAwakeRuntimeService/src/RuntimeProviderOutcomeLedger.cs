using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MarcusAwakeRuntimeService;

internal enum RuntimeProviderOutcomeStatus
{
    InProgress,
    Applied,
    Retryable,
    Unknown,
    Failed
}

internal enum RuntimeProviderLedgerReservation
{
    New,
    InProgress,
    Applied,
    Retryable,
    Unknown,
    Failed,
    Conflict,
    Unavailable
}

internal sealed class RuntimeProviderOutcomeRecord
{
    public string RequestPayloadHash { get; set; } = string.Empty;
    public RuntimeProviderOutcomeStatus Status { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}

internal sealed class RuntimeProviderOutcomeLedger
{
    private const int MaximumEntries = 512;
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);
    private readonly object gate = new object();
    private readonly string path;
    private readonly Dictionary<string, RuntimeProviderOutcomeRecord> records = new Dictionary<string, RuntimeProviderOutcomeRecord>(StringComparer.Ordinal);

    internal RuntimeProviderOutcomeLedger(string path)
    {
        this.path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
    }

    internal bool Load(out string error)
    {
        error = string.Empty;
        lock (gate)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    error = "path_is_directory";
                    return false;
                }
                if (!File.Exists(path)) return true;
                var loaded = JsonSerializer.Deserialize<Dictionary<string, RuntimeProviderOutcomeRecord>>(File.ReadAllText(path));
                if (loaded == null) return true;
                records.Clear();
                foreach (var pair in loaded)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null || string.IsNullOrWhiteSpace(pair.Value.RequestPayloadHash)) continue;
                    if (pair.Value.Status == RuntimeProviderOutcomeStatus.InProgress) pair.Value.Status = RuntimeProviderOutcomeStatus.Unknown;
                    records[pair.Key] = pair.Value;
                }
                PruneUnsafe(DateTimeOffset.UtcNow);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name;
                return false;
            }
        }
    }

    internal RuntimeProviderLedgerReservation Reserve(string key, string requestPayloadHash, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(requestPayloadHash)) return RuntimeProviderLedgerReservation.Unavailable;
        lock (gate)
        {
            PruneUnsafe(now);
            if (records.TryGetValue(key, out var existing))
            {
                if (!StringComparer.Ordinal.Equals(existing.RequestPayloadHash, requestPayloadHash)) return RuntimeProviderLedgerReservation.Conflict;
                return existing.Status switch
                {
                    RuntimeProviderOutcomeStatus.InProgress => RuntimeProviderLedgerReservation.InProgress,
                    RuntimeProviderOutcomeStatus.Applied => RuntimeProviderLedgerReservation.Applied,
                    RuntimeProviderOutcomeStatus.Retryable => ReplaceWithInProgressUnsafe(key, requestPayloadHash, now),
                    RuntimeProviderOutcomeStatus.Unknown => RuntimeProviderLedgerReservation.Unknown,
                    _ => RuntimeProviderLedgerReservation.Failed
                };
            }

            records[key] = new RuntimeProviderOutcomeRecord
            {
                RequestPayloadHash = requestPayloadHash,
                Status = RuntimeProviderOutcomeStatus.InProgress,
                UpdatedUtc = now
            };
            if (!PersistUnsafe())
            {
                records.Remove(key);
                return RuntimeProviderLedgerReservation.Unavailable;
            }
            return RuntimeProviderLedgerReservation.New;
        }
    }

    internal bool SetOutcome(string key, RuntimeProviderOutcomeStatus status, DateTimeOffset now)
    {
        lock (gate)
        {
            if (!records.TryGetValue(key, out var record)) return false;
            record.Status = status;
            record.UpdatedUtc = now;
            if (PersistUnsafe()) return true;
            record.Status = RuntimeProviderOutcomeStatus.Unknown;
            record.UpdatedUtc = now;
            PersistUnsafe();
            return false;
        }
    }

    private RuntimeProviderLedgerReservation ReplaceWithInProgressUnsafe(string key, string requestPayloadHash, DateTimeOffset now)
    {
        var previous = records[key];
        records[key] = new RuntimeProviderOutcomeRecord
        {
            RequestPayloadHash = requestPayloadHash,
            Status = RuntimeProviderOutcomeStatus.InProgress,
            UpdatedUtc = now
        };
        if (!PersistUnsafe())
        {
            records[key] = previous;
            return RuntimeProviderLedgerReservation.Unavailable;
        }
        return RuntimeProviderLedgerReservation.New;
    }

    private void PruneUnsafe(DateTimeOffset now)
    {
        foreach (var key in records.Where(pair => now - pair.Value.UpdatedUtc >= Retention).Select(pair => pair.Key).ToList()) records.Remove(key);
        while (records.Count > MaximumEntries)
        {
            var oldest = records.OrderBy(pair => pair.Value.UpdatedUtc).FirstOrDefault();
            if (oldest.Key == null) break;
            records.Remove(oldest.Key);
        }
    }

    private bool PersistUnsafe()
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            var temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(records));
            if (File.Exists(path)) File.Replace(temporaryPath, path, null);
            else File.Move(temporaryPath, path);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
