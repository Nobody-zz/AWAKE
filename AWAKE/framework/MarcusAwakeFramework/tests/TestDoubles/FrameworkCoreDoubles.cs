using System;
using System.Collections.Generic;
using MarcusAwakeFramework.Api;

namespace MarcusAwakeFramework.Tests.TestDoubles
{
    internal sealed class InMemoryCapabilityBroker : ICapabilityBroker
    {
        private readonly Dictionary<string, CapabilityDescriptor> descriptors = new Dictionary<string, CapabilityDescriptor>(StringComparer.Ordinal);

        public OperationResult<CapabilityDescriptor> Register(ExtensionManifest manifest, CapabilityDescriptor descriptor)
        {
            var correlation = manifest?.ExtensionId?.Value ?? "capability-registration";
            if (manifest == null || descriptor == null) return OperationResult<CapabilityDescriptor>.Failed(FrameworkErrors.Create("capability.invalid_manifest", FrameworkErrorCategory.InvalidRequest, "Capability registration is invalid.", correlation));
            if (!StringComparer.Ordinal.Equals(manifest.ExtensionId.Value, descriptor.Owner.Value)) return OperationResult<CapabilityDescriptor>.Failed(FrameworkErrors.Create("capability.owner_mismatch", FrameworkErrorCategory.Denied, "Capability owner does not match the manifest.", correlation));
            if (!Contains(manifest.Capabilities, descriptor.Id)) return OperationResult<CapabilityDescriptor>.Failed(FrameworkErrors.Create("capability.not_declared", FrameworkErrorCategory.Denied, "Capability is not declared by the manifest.", correlation));
            CapabilityDescriptor existing;
            if (descriptors.TryGetValue(descriptor.Id.Value, out existing))
            {
                if (StringComparer.Ordinal.Equals(existing.Owner.Value, descriptor.Owner.Value) && existing.Version.Equals(descriptor.Version)) return OperationResult<CapabilityDescriptor>.Succeeded(existing);
                return OperationResult<CapabilityDescriptor>.Failed(FrameworkErrors.Create("capability.conflict", FrameworkErrorCategory.Conflict, "Capability is already owned by another version or extension.", correlation));
            }
            descriptors.Add(descriptor.Id.Value, descriptor);
            return OperationResult<CapabilityDescriptor>.Succeeded(descriptor);
        }

        public OperationResult<CapabilityDescriptor> Resolve(CapabilityId id)
        {
            if (id == null || !descriptors.ContainsKey(id.Value)) return OperationResult<CapabilityDescriptor>.Failed(FrameworkErrors.Create("capability.unavailable", FrameworkErrorCategory.Unavailable, "Capability is unavailable.", "capability-resolve", true));
            return OperationResult<CapabilityDescriptor>.Succeeded(descriptors[id.Value]);
        }

        public IReadOnlyList<CapabilityDescriptor> Discover() => new List<CapabilityDescriptor>(descriptors.Values).AsReadOnly();

        private static bool Contains(IReadOnlyList<CapabilityId> values, CapabilityId value)
        {
            for (var index = 0; index < values.Count; index++) if (values[index].Equals(value)) return true;
            return false;
        }
    }

    internal sealed class InMemoryGameDataService : IGameDataService
    {
        private readonly List<DynamicEntityDto> entities = new List<DynamicEntityDto>();
        private readonly DateTimeOffset snapshotExpiry;

        internal InMemoryGameDataService(DateTimeOffset snapshotExpiry) { this.snapshotExpiry = snapshotExpiry; }

        internal void Add(DynamicEntityDto entity) { entities.Add(entity ?? throw new ArgumentNullException(nameof(entity))); }

        public OperationResult<Page<DynamicEntityDto>> Query(GameDataQuery query, RequestContext context)
        {
            if (query == null || context == null) return OperationResult<Page<DynamicEntityDto>>.Failed(FrameworkErrors.Create("gamedata.invalid_request", FrameworkErrorCategory.InvalidRequest, "The game data query is invalid.", context?.CorrelationId ?? "gamedata-query"));
            if (context.IsExpiredAt(DateTimeOffset.UtcNow)) return OperationResult<Page<DynamicEntityDto>>.Failed(FrameworkErrors.Create("gamedata.deadline_expired", FrameworkErrorCategory.Expired, "The query deadline has expired.", context.CorrelationId));
            if (query.Snapshot != null && query.Snapshot.IsExpired(DateTimeOffset.UtcNow)) return OperationResult<Page<DynamicEntityDto>>.Failed(FrameworkErrors.Create("gamedata.snapshot_expired", FrameworkErrorCategory.Expired, "The snapshot has expired.", context.CorrelationId));
            var filtered = new List<DynamicEntityDto>();
            for (var index = 0; index < entities.Count; index++) if (StringComparer.Ordinal.Equals(entities[index].Identity.EntityType, query.EntityType)) filtered.Add(entities[index]);
            var offset = ParseCursor(query.Page.Cursor);
            var pageItems = new List<DynamicEntityDto>();
            for (var index = offset; index < filtered.Count && pageItems.Count < query.Page.Limit; index++) pageItems.Add(filtered[index]);
            var next = offset + pageItems.Count < filtered.Count ? (offset + pageItems.Count).ToString() : null;
            return OperationResult<Page<DynamicEntityDto>>.Succeeded(new Page<DynamicEntityDto>(pageItems.AsReadOnly(), next, new SnapshotToken("snapshot-" + filtered.Count, snapshotExpiry)));
        }

        private static int ParseCursor(string cursor)
        {
            int value;
            return string.IsNullOrWhiteSpace(cursor) || !int.TryParse(cursor, out value) || value < 0 ? 0 : value;
        }
    }

    internal sealed class InMemoryCommandService : ICommandService
    {
        private readonly Dictionary<string, CommandLedgerEntry> entries = new Dictionary<string, CommandLedgerEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> dedupeToFullKey = new Dictionary<string, string>(StringComparer.Ordinal);
        private long sequence;

        public OperationResult<CommandLedgerEntry> Prepare(CommandRequest request, RequestContext context)
        {
            if (request == null || context == null) return OperationResult<CommandLedgerEntry>.Failed(FrameworkErrors.Create("command.invalid_request", FrameworkErrorCategory.InvalidRequest, "The command request is invalid.", context?.CorrelationId ?? "command-prepare"));
            if (!request.Scope.Session.Equals(context.Session)) return OperationResult<CommandLedgerEntry>.Failed(FrameworkErrors.Create("command.session_mismatch", FrameworkErrorCategory.Denied, "The command session does not match the request context.", context.CorrelationId));
            string existingFullKey;
            if (dedupeToFullKey.TryGetValue(request.Scope.DeduplicationKey, out existingFullKey) && !StringComparer.Ordinal.Equals(existingFullKey, request.Scope.FullKey)) return OperationResult<CommandLedgerEntry>.Failed(FrameworkErrors.Create("command.idempotency_conflict", FrameworkErrorCategory.Conflict, "The idempotency key was reused with a different payload.", context.CorrelationId));
            CommandLedgerEntry existing;
            if (entries.TryGetValue(request.Scope.FullKey, out existing)) return OperationResult<CommandLedgerEntry>.Succeeded(existing);
            var entry = new CommandLedgerEntry("ledger-" + (entries.Count + 1), request) { LedgerSequence = ++sequence };
            entries.Add(request.Scope.FullKey, entry);
            dedupeToFullKey[request.Scope.DeduplicationKey] = request.Scope.FullKey;
            return OperationResult<CommandLedgerEntry>.Succeeded(entry);
        }

        public OperationResult<SettlementReceipt> Settle(string ledgerEntryId, bool applied, string effectHash, RequestContext context)
        {
            if (string.IsNullOrWhiteSpace(ledgerEntryId) || context == null) return OperationResult<SettlementReceipt>.Failed(FrameworkErrors.Create("command.settlement_invalid", FrameworkErrorCategory.InvalidRequest, "The settlement request is invalid.", context?.CorrelationId ?? "command-settle"));
            var entry = Find(ledgerEntryId);
            if (entry == null) return OperationResult<SettlementReceipt>.Failed(FrameworkErrors.Create("command.ledger_not_found", FrameworkErrorCategory.NotFound, "The command ledger entry was not found.", context.CorrelationId));
            if (!entry.Request.Scope.Session.Equals(context.Session)) return OperationResult<SettlementReceipt>.Failed(FrameworkErrors.Create("command.session_mismatch", FrameworkErrorCategory.Denied, "The command session does not match the request context.", context.CorrelationId));
            if (entry.State == CommandLedgerState.Applied || entry.State == CommandLedgerState.Rejected) return OperationResult<SettlementReceipt>.Failed(FrameworkErrors.Create("command.already_settled", FrameworkErrorCategory.Conflict, "The command is already settled.", context.CorrelationId));
            entry.State = applied ? CommandLedgerState.Applied : CommandLedgerState.Rejected;
            entry.EffectHash = effectHash ?? string.Empty;
            return OperationResult<SettlementReceipt>.Succeeded(new SettlementReceipt("settlement-" + entry.LedgerEntryId, entry, applied, entry.EffectHash));
        }

        public OperationResult<CommandLedgerEntry> GetLedgerEntry(string ledgerEntryId, RequestContext context)
        {
            var entry = Find(ledgerEntryId);
            if (entry == null) return OperationResult<CommandLedgerEntry>.Failed(FrameworkErrors.Create("command.ledger_not_found", FrameworkErrorCategory.NotFound, "The command ledger entry was not found.", context?.CorrelationId ?? "command-get"));
            return OperationResult<CommandLedgerEntry>.Succeeded(entry);
        }

        private CommandLedgerEntry Find(string ledgerEntryId)
        {
            foreach (var pair in entries) if (StringComparer.Ordinal.Equals(pair.Value.LedgerEntryId, ledgerEntryId)) return pair.Value;
            return null;
        }
    }

    internal sealed class InMemorySaveAnchorStore : ISaveAnchorStore
    {
        private readonly Dictionary<string, SaveAttempt> attempts = new Dictionary<string, SaveAttempt>(StringComparer.Ordinal);
        public SaveAnchorSnapshot Current { get; private set; }

        public OperationResult<SaveAttempt> Prepare(SaveAnchorSnapshot snapshot)
        {
            if (snapshot == null) return OperationResult<SaveAttempt>.Failed(FrameworkErrors.Create("save.anchor_missing", FrameworkErrorCategory.InvalidRequest, "A save anchor snapshot is required.", "save-prepare"));
            var attempt = new SaveAttempt("save-attempt-" + (attempts.Count + 1), snapshot);
            attempts.Add(attempt.AttemptId, attempt);
            return OperationResult<SaveAttempt>.Succeeded(attempt);
        }

        public OperationResult<bool> MarkSerialized(string attemptId)
        {
            SaveAttempt attempt;
            if (!attempts.TryGetValue(attemptId ?? string.Empty, out attempt)) return OperationResult<bool>.Failed(FrameworkErrors.Create("save.attempt_not_found", FrameworkErrorCategory.NotFound, "The save attempt was not found.", "save-serialize"));
            if (attempt.State != SaveAttemptState.Prepared) return OperationResult<bool>.Failed(FrameworkErrors.Create("save.invalid_transition", FrameworkErrorCategory.Conflict, "The save attempt cannot be serialized in its current state.", "save-serialize"));
            attempt.State = SaveAttemptState.Serialized;
            return OperationResult<bool>.Succeeded(true);
        }

        public OperationResult<bool> Commit(string attemptId)
        {
            SaveAttempt attempt;
            if (!attempts.TryGetValue(attemptId ?? string.Empty, out attempt)) return OperationResult<bool>.Failed(FrameworkErrors.Create("save.attempt_not_found", FrameworkErrorCategory.NotFound, "The save attempt was not found.", "save-commit"));
            if (attempt.State != SaveAttemptState.Serialized) return OperationResult<bool>.Failed(FrameworkErrors.Create("save.invalid_transition", FrameworkErrorCategory.Conflict, "The save attempt is not serialized.", "save-commit"));
            attempt.State = SaveAttemptState.Committed;
            Current = attempt.Snapshot;
            return OperationResult<bool>.Succeeded(true);
        }
    }
}
