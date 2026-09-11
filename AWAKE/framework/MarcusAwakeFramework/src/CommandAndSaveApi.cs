using System;
using System.Collections.Generic;

namespace MarcusAwakeFramework.Api
{
    public enum CommandRisk
    {
        R0,
        R1,
        R2,
        R3
    }

    public enum CommandLedgerState
    {
        Prepared,
        Applied,
        Rejected,
        Unknown
    }

    public sealed class CommandDescriptor
    {
        public CommandDescriptor(string commandId, ApiVersion schemaVersion, ExtensionId owner, CommandRisk risk)
        {
            CommandId = ContractGuard.Id(commandId, nameof(commandId));
            SchemaVersion = schemaVersion ?? throw new ArgumentNullException(nameof(schemaVersion));
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Risk = risk;
            MinimumRisk = ToRiskTier(risk);
            InputSchema = new SchemaRef(CommandId + ".input", SchemaVersion);
            OutputSchema = new SchemaRef(CommandId + ".output", SchemaVersion);
            SupportedBannerlordApis = new string[0];
        }

        public CommandDescriptor(string commandId, ExtensionId owner, CommandRiskTier minimumRisk, SchemaRef inputSchema, SchemaRef outputSchema, IReadOnlyList<string> supportedBannerlordApis)
        {
            CommandId = ContractGuard.Id(commandId, nameof(commandId));
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            MinimumRisk = minimumRisk;
            SchemaVersion = inputSchema == null ? new ApiVersion(1, 0) : inputSchema.Version;
            Risk = ToCommandRisk(minimumRisk);
            InputSchema = inputSchema;
            OutputSchema = outputSchema;
            SupportedBannerlordApis = supportedBannerlordApis ?? new string[0];
        }

        public string CommandId { get; }
        public ApiVersion SchemaVersion { get; }
        public ExtensionId Owner { get; }
        public CommandRisk Risk { get; }
        public CommandRiskTier MinimumRisk { get; }
        public SchemaRef InputSchema { get; }
        public SchemaRef OutputSchema { get; }
        public IReadOnlyList<string> SupportedBannerlordApis { get; }

        private static CommandRiskTier ToRiskTier(CommandRisk value)
        {
            switch (value)
            {
                case CommandRisk.R0: return CommandRiskTier.R0Query;
                case CommandRisk.R1: return CommandRiskTier.R1Interface;
                case CommandRisk.R2: return CommandRiskTier.R2Gameplay;
                default: return CommandRiskTier.R3Strategic;
            }
        }

        private static CommandRisk ToCommandRisk(CommandRiskTier value)
        {
            switch (value)
            {
                case CommandRiskTier.R0Query: return CommandRisk.R0;
                case CommandRiskTier.R1Interface: return CommandRisk.R1;
                case CommandRiskTier.R2Gameplay: return CommandRisk.R2;
                default: return CommandRisk.R3;
            }
        }
    }

    public sealed class CommandRequest
    {
        public CommandRequest(CommandDescriptor descriptor, IdempotencyScope scope, string payloadJson)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            PayloadJson = payloadJson ?? string.Empty;
            RequestId = Scope.IdempotencyKey;
            CommandId = Descriptor.CommandId;
            ArgumentsJson = PayloadJson;
            IdempotencyKey = Scope.IdempotencyKey;
            ExpiresUtc = DateTimeOffset.MaxValue;
        }

        public CommandRequest(string requestId, string commandId, string argumentsJson, string idempotencyKey, DateTimeOffset expiresUtc)
        {
            RequestId = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("N") : requestId;
            CommandId = commandId ?? string.Empty;
            ArgumentsJson = argumentsJson ?? "{}";
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? RequestId : idempotencyKey;
            ExpiresUtc = expiresUtc;
            PayloadJson = ArgumentsJson;
        }

        public CommandDescriptor Descriptor { get; }
        public IdempotencyScope Scope { get; }
        public string PayloadJson { get; }
        public string RequestId { get; }
        public string CommandId { get; }
        public string ArgumentsJson { get; }
        public string IdempotencyKey { get; }
        public DateTimeOffset ExpiresUtc { get; }
    }

    public sealed class CommandLedgerEntry
    {
        internal CommandLedgerEntry(string ledgerEntryId, CommandRequest request)
        {
            LedgerEntryId = ledgerEntryId;
            Request = request;
            State = CommandLedgerState.Prepared;
        }
        public string LedgerEntryId { get; }
        public CommandRequest Request { get; }
        public CommandLedgerState State { get; internal set; }
        public long LedgerSequence { get; internal set; }
        public string EffectHash { get; internal set; }
    }

    public sealed class SettlementReceipt
    {
        internal SettlementReceipt(string settlementId, CommandLedgerEntry entry, bool applied, string effectHash)
        {
            SettlementId = settlementId;
            LedgerEntryId = entry.LedgerEntryId;
            LedgerSequence = entry.LedgerSequence;
            Applied = applied;
            EffectHash = effectHash ?? string.Empty;
        }
        public string SettlementId { get; }
        public string LedgerEntryId { get; }
        public long LedgerSequence { get; }
        public bool Applied { get; }
        public string EffectHash { get; }
    }

    public interface ICommandService
    {
        OperationResult<CommandLedgerEntry> Prepare(CommandRequest request, RequestContext context);
        OperationResult<SettlementReceipt> Settle(string ledgerEntryId, bool applied, string effectHash, RequestContext context);
        OperationResult<CommandLedgerEntry> GetLedgerEntry(string ledgerEntryId, RequestContext context);
    }

    public sealed class SaveAnchorSnapshot
    {
        public SaveAnchorSnapshot(string campaignGuid, string timelineId, long sessionGeneration, long committedAnchorSequence, string receiptHash, string storageSchemaEpoch, string migrationId, string worldbookRevision)
        {
            CampaignGuid = ContractGuard.Id(campaignGuid, nameof(campaignGuid));
            TimelineId = ContractGuard.Id(timelineId, nameof(timelineId));
            SessionGeneration = sessionGeneration;
            CommittedAnchorSequence = committedAnchorSequence;
            ReceiptHash = receiptHash ?? string.Empty;
            StorageSchemaEpoch = ContractGuard.Id(storageSchemaEpoch, nameof(storageSchemaEpoch));
            MigrationId = ContractGuard.Id(migrationId, nameof(migrationId));
            WorldbookRevision = ContractGuard.Id(worldbookRevision, nameof(worldbookRevision));
        }
        public string CampaignGuid { get; }
        public string TimelineId { get; }
        public long SessionGeneration { get; }
        public long CommittedAnchorSequence { get; }
        public string ReceiptHash { get; }
        public string StorageSchemaEpoch { get; }
        public string MigrationId { get; }
        public string WorldbookRevision { get; }
    }

    public enum SaveAttemptState
    {
        Prepared,
        Serialized,
        Committed,
        Failed
    }

    public sealed class SaveAttempt
    {
        internal SaveAttempt(string attemptId, SaveAnchorSnapshot snapshot) { AttemptId = attemptId; Snapshot = snapshot; State = SaveAttemptState.Prepared; }
        public string AttemptId { get; }
        public SaveAnchorSnapshot Snapshot { get; }
        public SaveAttemptState State { get; internal set; }
    }

    public interface ISaveAnchorStore
    {
        OperationResult<SaveAttempt> Prepare(SaveAnchorSnapshot snapshot);
        OperationResult<bool> MarkSerialized(string attemptId);
        OperationResult<bool> Commit(string attemptId);
        SaveAnchorSnapshot Current { get; }
    }

}
