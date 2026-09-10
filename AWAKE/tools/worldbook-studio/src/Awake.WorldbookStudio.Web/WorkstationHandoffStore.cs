using System.Text;
using System.Text.Json.Nodes;
using System.Diagnostics.CodeAnalysis;
using Awake.WorldbookStudio.Core;

namespace Awake.WorldbookStudio.Web;

internal sealed record WorkstationHandoffOperation(
    string HandoffId,
    JsonObject Envelope,
    JsonObject Receipt,
    string? DraftId = null,
    string? DraftStatus = null,
    bool ReviewOnly = true);

internal sealed class WorkstationHandoffStore
{
    private readonly object _gate = new();
    private readonly string _ledgerPath;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<WorkstationHandoffEnvelope, string, string> _draftWriter;
    private readonly Func<WorkstationHandoffEnvelope, string, bool> _draftExists;
    private readonly Dictionary<string, WorkstationHandoffOperation> _entries = new(StringComparer.Ordinal);
    private static readonly HashSet<string> ReceiptFields = new(StringComparer.Ordinal)
    {
        "schema_version", "receipt_id", "handoff_id", "consumer", "lifecycle_status",
        "consumer_review_status", "recovery_required", "accepted_at_utc",
        "consumed_at_utc", "draft_id", "recovery_reason", "request_fingerprint"
    };

    public WorkstationHandoffStore(WorkspaceService workspace, AuthoringDraftStore drafts)
        : this(
            workspace.Policy.RequireAllowed(
                Path.Combine(workspace.Root, "authoring", "handoff-inbox"),
                "初始化 workstation handoff inbox"),
            drafts)
    {
    }

    public WorkstationHandoffStore(
        string inboxRoot,
        AuthoringDraftStore drafts,
        Func<WorkstationHandoffEnvelope, string, string>? draftWriter = null,
        Func<WorkstationHandoffEnvelope, string, bool>? draftExists = null,
        Func<DateTimeOffset>? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inboxRoot);
        ArgumentNullException.ThrowIfNull(drafts);
        Directory.CreateDirectory(inboxRoot);
        _ledgerPath = Path.Combine(inboxRoot, "handoff-inbox.v1.ndjson");
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _draftWriter = draftWriter ?? ((envelope, _) =>
        {
            var draft = drafts.CreateImportedReviewDraft(
                envelope.HandoffId,
                envelope.DocumentId,
                envelope.Payload,
                envelope.ContentSha256);
            return draft.DraftId;
        });
        _draftExists = draftExists ?? ((envelope, _) =>
            drafts.ImportedReviewDraftExists(envelope.HandoffId, envelope.ContentSha256));
        Load();
    }

    public WorkstationHandoffOperation Import(JsonObject envelopeJson)
    {
        var envelope = WorkstationHandoffValidator.Validate(envelopeJson, _clock());
        lock (_gate)
        {
            if (_entries.TryGetValue(envelope.HandoffId, out var existing))
            {
                if (!string.Equals(
                        existing.Receipt["request_fingerprint"]?.GetValue<string>(),
                        envelope.RequestFingerprint,
                        StringComparison.Ordinal))
                    Conflict("同一 handoff_id 已对应不同请求，请重新生成新的 handoff。");
                return Clone(existing);
            }

            var receipt = NewReceipt(envelope, "issued");
            var operation = new WorkstationHandoffOperation(envelope.HandoffId, envelope.ToJson(), receipt);
            Append("imported", operation);
            _entries[envelope.HandoffId] = operation;
            return Clone(operation);
        }
    }

    public WorkstationHandoffOperation Accept(string handoffId, string receiptId, string expectedStatus)
    {
        lock (_gate)
        {
            var current = RequireEntry(handoffId, receiptId);
            current = ExpireIfNeeded(current);
            var status = Status(current);
            if (status is "accepted" or "consumed") return Clone(current);
            if (status == "expired") Expired();
            if (status is "in_doubt" or "unknown") Unknown();
            if (status == "rejected") Conflict("handoff 已被拒绝，请修复来源后重新发起。");
            if (expectedStatus != "issued" || status != "issued")
                Conflict("accept 的 expected_status 与当前 receipt 不一致。");

            var receipt = CloneReceipt(current.Receipt);
            receipt["lifecycle_status"] = "accepted";
            receipt["accepted_at_utc"] = Timestamp(_clock());
            ValidateReceipt(receipt);
            var updated = current with { Receipt = receipt };
            Append("accepted", updated);
            _entries[handoffId] = updated;
            return Clone(updated);
        }
    }

    public WorkstationHandoffOperation Consume(
        string handoffId,
        string receiptId,
        string expectedStatus,
        bool injectInDoubtFailure = false)
    {
        lock (_gate)
        {
            var current = RequireEntry(handoffId, receiptId);
            current = ExpireIfNeeded(current);
            var status = Status(current);
            if (status == "consumed") return Clone(current);
            if (status == "expired") Expired();
            if (status is "in_doubt" or "unknown") Unknown();
            if (status != "accepted" || expectedStatus != "accepted")
                Conflict("consume 只能处理 expected_status=accepted 的 receipt。");
            return ConsumeAccepted(current, injectInDoubtFailure);
        }
    }

    public WorkstationHandoffOperation Recover(string handoffId, string receiptId, string action, string? reason)
    {
        lock (_gate)
        {
            var current = RequireEntry(handoffId, receiptId);
            if (Status(current) == "unknown") Unknown();
            if (Status(current) != "in_doubt") Conflict("recover 只能处理 in_doubt receipt。");

            if (action == "mark_unknown")
            {
                if (string.IsNullOrWhiteSpace(reason))
                    throw new WorkstationHandoffException("WB-HANDOFF-400", "mark_unknown 必须提供非空 reason。", 400, "reason");
                if (reason.Trim().Length > 512)
                    throw new WorkstationHandoffException("WB-HANDOFF-400", "recovery reason 不能超过 512 个字符。", 400, "reason");
                var receipt = CloneReceipt(current.Receipt);
                receipt["lifecycle_status"] = "unknown";
                receipt["recovery_required"] = true;
                receipt["recovery_reason"] = reason.Trim();
                ValidateReceipt(receipt);
                var unknown = current with { Receipt = receipt };
                Append("marked_unknown", unknown);
                _entries[handoffId] = unknown;
                return Clone(unknown);
            }

            if (action != "retry_consume")
                throw new WorkstationHandoffException("WB-HANDOFF-400", "recover action 无效。", 400, "action");

            var envelope = ToEnvelope(current.Envelope);
            var draftId = DeterministicDraftId(envelope.HandoffId);
            try
            {
                if (!_draftExists(envelope, draftId))
                    draftId = _draftWriter(envelope, draftId);
                return SetConsumed(current, draftId, "recovered");
            }
            catch (Exception ex) when (ex is not WorkstationHandoffException)
            {
                throw new WorkstationHandoffException(
                    "WB-HANDOFF-503",
                    "恢复后仍无法确认草稿结果；请读取 receipt 后决定重试或标记 unknown。",
                    503,
                    ex.GetType().Name);
            }
        }
    }

    public WorkstationHandoffOperation Get(string handoffId)
    {
        lock (_gate)
        {
            var current = RequireEntry(handoffId, null);
            current = ExpireIfNeeded(current);
            return Clone(current);
        }
    }

    private WorkstationHandoffOperation ConsumeAccepted(
        WorkstationHandoffOperation current,
        bool injectInDoubtFailure)
    {
        var inDoubtReceipt = CloneReceipt(current.Receipt);
        inDoubtReceipt["lifecycle_status"] = "in_doubt";
        inDoubtReceipt["recovery_required"] = true;
        ValidateReceipt(inDoubtReceipt);
        var inDoubt = current with { Receipt = inDoubtReceipt };
        Append("consume_claimed", inDoubt);
        _entries[current.HandoffId] = inDoubt;

        if (injectInDoubtFailure)
            throw new WorkstationHandoffException(
                "WB-HANDOFF-503",
                "测试故障注入：草稿写入结果未知；请读取 receipt 并使用 recover 决定重试或标记 unknown。",
                503,
                "fault_injection");

        var envelope = ToEnvelope(current.Envelope);
        var draftId = DeterministicDraftId(envelope.HandoffId);
        try
        {
            draftId = _draftWriter(envelope, draftId);
            return SetConsumed(inDoubt, draftId, "consumed");
        }
        catch (Exception ex) when (ex is not WorkstationHandoffException)
        {
            throw new WorkstationHandoffException(
                "WB-HANDOFF-503",
                "草稿写入结果未知；请读取 receipt 并使用 recover 决定重试或标记 unknown。",
                503,
                ex.GetType().Name);
        }
    }

    private WorkstationHandoffOperation SetConsumed(WorkstationHandoffOperation current, string draftId, string eventType)
    {
        var receipt = CloneReceipt(current.Receipt);
        receipt["lifecycle_status"] = "consumed";
        receipt["recovery_required"] = false;
        receipt["consumed_at_utc"] = Timestamp(_clock());
        receipt["draft_id"] = draftId;
        receipt["recovery_reason"] = null;
        ValidateReceipt(receipt);
        var consumed = current with
        {
            Receipt = receipt,
            DraftId = draftId,
            DraftStatus = "needs_review",
            ReviewOnly = true
        };
        Append(eventType, consumed);
        _entries[current.HandoffId] = consumed;
        return Clone(consumed);
    }

    private WorkstationHandoffOperation ExpireIfNeeded(WorkstationHandoffOperation current)
    {
        var status = Status(current);
        if (status is not ("issued" or "accepted")) return current;
        var expiresAt = DateTimeOffset.Parse(current.Envelope["expires_at_utc"]!.GetValue<string>());
        if (_clock() < expiresAt) return current;
        var receipt = CloneReceipt(current.Receipt);
        receipt["lifecycle_status"] = "expired";
        receipt["recovery_required"] = false;
        receipt["draft_id"] = null;
        receipt["consumed_at_utc"] = null;
        receipt["recovery_reason"] = null;
        ValidateReceipt(receipt);
        var expired = current with { Receipt = receipt };
        Append("expired", expired);
        _entries[current.HandoffId] = expired;
        return expired;
    }

    private WorkstationHandoffOperation RequireEntry(string handoffId, string? receiptId)
    {
        if (!_entries.TryGetValue(handoffId, out var current))
            Conflict("handoff 不存在，请先导入。");
        if (receiptId is not null
            && !string.Equals(receiptId, current.Receipt["receipt_id"]?.GetValue<string>(), StringComparison.Ordinal))
            Conflict("receipt_id 与 handoff 不匹配。");
        return current;
    }

    private static JsonObject NewReceipt(WorkstationHandoffEnvelope envelope, string status)
    {
        var receipt = new JsonObject
        {
            ["schema_version"] = WorkstationHandoffValidator.ReceiptSchemaVersion,
            ["receipt_id"] = "wbs-receipt-" + Guid.NewGuid().ToString("N"),
            ["handoff_id"] = envelope.HandoffId,
            ["consumer"] = "worldbook_studio",
            ["lifecycle_status"] = status,
            ["consumer_review_status"] = "needs_review",
            ["recovery_required"] = false,
            ["accepted_at_utc"] = null,
            ["consumed_at_utc"] = null,
            ["draft_id"] = null,
            ["recovery_reason"] = null,
            ["request_fingerprint"] = envelope.RequestFingerprint
        };
        ValidateReceipt(receipt);
        return receipt;
    }

    private static void ValidateReceipt(JsonObject receipt)
    {
        foreach (var property in receipt)
            if (!ReceiptFields.Contains(property.Key))
                throw new InvalidDataException("WB-HANDOFF-STATE: receipt 包含未冻结字段。");
        var receiptId = receipt["receipt_id"]?.GetValue<string>();
        var handoffId = receipt["handoff_id"]?.GetValue<string>();
        var fingerprint = receipt["request_fingerprint"]?.GetValue<string>();
        if (receipt["schema_version"]?.GetValue<string>() != WorkstationHandoffValidator.ReceiptSchemaVersion
            || receipt["consumer"]?.GetValue<string>() != "worldbook_studio"
            || receiptId is null
            || !System.Text.RegularExpressions.Regex.IsMatch(receiptId, "^wbs-receipt-[a-f0-9]{32}$")
            || handoffId is null
            || !System.Text.RegularExpressions.Regex.IsMatch(handoffId, "^(pwb|wbs|ui)-handoff-[a-f0-9]{32}$")
            || fingerprint is null
            || fingerprint.Length != 64
            || fingerprint.Any(character => !Uri.IsHexDigit(character) || char.IsUpper(character)))
            throw new InvalidDataException("WB-HANDOFF-STATE: receipt 基础字段不符合冻结 schema。");
        var status = receipt["lifecycle_status"]?.GetValue<string>();
        var accepted = receipt["accepted_at_utc"] is not null;
        var consumed = receipt["consumed_at_utc"] is not null;
        var draft = receipt["draft_id"] is not null;
        var recovery = receipt["recovery_required"]?.GetValue<bool>() == true;
        var reason = receipt["recovery_reason"]?.GetValue<string>();
        if (reason is not null && (reason.Length is < 1 or > 512))
            throw new InvalidDataException("WB-HANDOFF-STATE: recovery_reason 长度无效。");
        var valid = status switch
        {
            "issued" => !accepted && !consumed && !draft && !recovery && reason is null,
            "accepted" => accepted && !consumed && !draft && !recovery && reason is null,
            "consumed" => accepted && consumed && draft && !recovery && reason is null,
            "expired" => !consumed && !draft && !recovery && reason is null,
            "rejected" => !consumed && !draft && !recovery && reason is null,
            "in_doubt" => accepted && !consumed && recovery && reason is null,
            "unknown" => accepted && recovery && !string.IsNullOrWhiteSpace(reason),
            _ => false
        };
        if (!valid)
            throw new InvalidOperationException("WB-HANDOFF-STATE: receipt 状态字段组合无效。");
    }

    private void Append(string eventType, WorkstationHandoffOperation operation)
    {
        var entry = new JsonObject
        {
            ["event_type"] = eventType,
            ["recorded_at_utc"] = Timestamp(_clock()),
            ["handoff_id"] = operation.HandoffId,
            ["envelope"] = operation.Envelope.DeepClone(),
            ["receipt"] = operation.Receipt.DeepClone(),
            ["draft_status"] = operation.DraftStatus,
            ["review_only"] = operation.ReviewOnly
        };
        using var stream = new FileStream(_ledgerPath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(CanonicalJson.Serialize(entry));
        writer.Write('\n');
        writer.Flush();
        stream.Flush(true);
    }

    private void Load()
    {
        if (!File.Exists(_ledgerPath)) return;
        foreach (var line in File.ReadLines(_ledgerPath, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var entry = JsonNode.Parse(line)?.AsObject()
                ?? throw new InvalidDataException("WB-HANDOFF-STATE: handoff ledger 记录为空。");
            var envelope = entry["envelope"]?.AsObject()
                ?? throw new InvalidDataException("WB-HANDOFF-STATE: handoff ledger 缺少 envelope。");
            var receipt = entry["receipt"]?.AsObject()
                ?? throw new InvalidDataException("WB-HANDOFF-STATE: handoff ledger 缺少 receipt。");
            _ = WorkstationHandoffValidator.Validate(envelope, DateTimeOffset.MinValue);
            ValidateReceipt(receipt);
            var handoffId = entry["handoff_id"]?.GetValue<string>()
                ?? throw new InvalidDataException("WB-HANDOFF-STATE: handoff ledger 缺少 handoff_id。");
            _entries[handoffId] = new WorkstationHandoffOperation(
                handoffId,
                (JsonObject)envelope.DeepClone(),
                (JsonObject)receipt.DeepClone(),
                receipt["draft_id"]?.GetValue<string>(),
                entry["draft_status"]?.GetValue<string>(),
                entry["review_only"]?.GetValue<bool>() != false);
        }
    }

    private static WorkstationHandoffEnvelope ToEnvelope(JsonObject envelope)
        => WorkstationHandoffValidator.Validate(envelope, DateTimeOffset.MinValue);

    private static string DeterministicDraftId(string handoffId)
        => "draft-handoff-" + Hashing.Sha256Text(handoffId).ToLowerInvariant()[..32];

    private static string Status(WorkstationHandoffOperation operation)
        => operation.Receipt["lifecycle_status"]?.GetValue<string>() ?? string.Empty;

    private static string Timestamp(DateTimeOffset value)
        => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");

    private static JsonObject CloneReceipt(JsonObject receipt)
        => (JsonObject)receipt.DeepClone();

    private static WorkstationHandoffOperation Clone(WorkstationHandoffOperation operation)
        => operation with
        {
            Envelope = (JsonObject)operation.Envelope.DeepClone(),
            Receipt = (JsonObject)operation.Receipt.DeepClone()
        };

    [DoesNotReturn]
    private static void Conflict(string message)
        => throw new WorkstationHandoffException("WB-HANDOFF-409", message, 409);

    [DoesNotReturn]
    private static void Expired()
        => throw new WorkstationHandoffException("WB-HANDOFF-410", "handoff 已过期且不可恢复，请重新发起。", 410);

    [DoesNotReturn]
    private static void Unknown()
        => throw new WorkstationHandoffException("WB-HANDOFF-503", "handoff 结果未知，请先读取 receipt 并执行人工恢复决策。", 503);
}
