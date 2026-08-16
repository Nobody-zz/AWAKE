using System;
using System.Text;
using MarcusAIFramework.Api;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;

namespace Awake;

internal abstract class BaseAwakeCommandAdapter : ICommandAdapter
{
    public abstract OperationResult<CommandAdapterPreflight> Preflight(CommandRequest request, RequestContext context);

    public abstract OperationResult<CommandAdapterResult> Execute(
        CommandRequest request,
        RequestContext context,
        string expectedSnapshotToken);

    protected static JObject ParseArguments(CommandRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ArgumentsJson)) return null;
        try
        {
            JToken token = JToken.Parse(request.ArgumentsJson);
            return token as JObject;
        }
        catch
        {
            return null;
        }
    }

    protected static string SnapshotFromArguments(CommandRequest request)
    {
        if (request == null) return string.Empty;
        using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(
                request.CommandId + "|" + AwakeRuntime.CanonicalizeArguments(request.ArgumentsJson)));
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) builder.Append(b.ToString("x2"));
            return builder.ToString();
        }
    }

    protected static bool TokenMatches(string expected, string actual)
    {
        return string.Equals(expected, actual, StringComparison.Ordinal);
    }

    protected static bool IsValidIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        foreach (char c in value)
        {
            bool ok = char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-';
            if (!ok) return false;
        }
        return true;
    }
}

internal sealed class AwakeRelationshipDeltaAdapter : BaseAwakeCommandAdapter
{
    public override OperationResult<CommandAdapterPreflight> Preflight(CommandRequest request, RequestContext context)
    {
        if (context == null || context.IsExpired) return Denied("awake.world_state.context_expired", context?.CorrelationId);
        JObject args = ParseArguments(request);
        if (args == null || !Validate(args, out string _))
        {
            return Denied("awake.world_state.relationship.invalid", context.CorrelationId);
        }
        return OperationResult<CommandAdapterPreflight>.Succeeded(new CommandAdapterPreflight(
            "关系状态变化：" + ((string)args["heroId"] ?? string.Empty),
            SnapshotFromArguments(request)));
    }

    public override OperationResult<CommandAdapterResult> Execute(
        CommandRequest request,
        RequestContext context,
        string expectedSnapshotToken)
    {
        if (context == null || context.IsExpired) return ResultFailed("awake.world_state.context_expired", context?.CorrelationId);
        if (!TokenMatches(expectedSnapshotToken, SnapshotFromArguments(request)))
        {
            return ResultFailed("awake.world_state.relationship.snapshot_mismatch", context.CorrelationId);
        }
        JObject args = ParseArguments(request);
        if (args == null || !Validate(args, out string _))
        {
            return ResultFailed("awake.world_state.relationship.invalid", context.CorrelationId);
        }

        string heroId = (string)args["heroId"];
        if (AwakeRuntime.SessionEnded) return ResultFailed("awake.world_state.session_ended", context.CorrelationId);
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null) return ResultFailed("awake.world_state.store_unavailable", context.CorrelationId);

        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.RelationshipsNamespace,
            WorldStateStore.BuildHeroKey(heroId),
            AiTaskConstants.RelationshipDeltaCommandId,
            request.IdempotencyKey,
            heroId,
            WorldStateKind.Relationship,
            args,
            DateTimeOffset.UtcNow,
            context.CorrelationId);
        if (!store.TryEnqueue(command))
        {
            return ResultFailed("awake.world_state.session_ended", context.CorrelationId);
        }
        return OperationResult<CommandAdapterResult>.Succeeded(
            new CommandAdapterResult(CommandState.Succeeded, "关系状态变化已入队。"));
    }

    internal static bool Validate(JObject args, out string error)
    {
        error = string.Empty;
        if (args == null)
        {
            error = "args";
            return false;
        }
        string heroId = (string)args["heroId"];
        if (string.IsNullOrWhiteSpace(heroId) || heroId.Length > 80 || !IsValidIdentifier(heroId))
        {
            error = "heroId";
            return false;
        }
        int trust;
        int love;
        int hostility;
        if (!TryDelta(args, "trustDelta", -100, 100, out trust)
            || !TryDelta(args, "loveDelta", -100, 100, out love)
            || !TryDelta(args, "hostilityDelta", -100, 100, out hostility))
        {
            error = "delta";
            return false;
        }
        if (trust == 0 && love == 0 && hostility == 0)
        {
            error = "delta";
            return false;
        }
        string reason = (string)args["reason"];
        if (reason != null && reason.Length > 240)
        {
            error = "reason";
            return false;
        }
        return true;
    }

    private static bool TryDelta(JObject args, string name, int minimum, int maximum, out int value)
    {
        value = 0;
        JToken token = args[name];
        if (token == null || token.Type != JTokenType.Integer) return false;
        int parsed;
        try { parsed = (int)token; }
        catch { return false; }
        if (parsed < minimum || parsed > maximum) return false;
        value = parsed;
        return true;
    }

    private static OperationResult<CommandAdapterPreflight> Denied(string code, string correlationId)
    {
        return OperationResult<CommandAdapterPreflight>.Failed(FrameworkErrors.Create(
            code,
            FrameworkErrorCategory.InvalidRequest,
            "Relationship command rejected.",
            correlationId,
            owner: AwakeConstants.OwnerValue));
    }

    private static OperationResult<CommandAdapterResult> ResultFailed(string code, string correlationId)
    {
        return OperationResult<CommandAdapterResult>.Failed(FrameworkErrors.Create(
            code,
            FrameworkErrorCategory.InvalidRequest,
            "Relationship command failed.",
            correlationId,
            owner: AwakeConstants.OwnerValue));
    }
}

internal sealed class AwakeWorldEffectRecordAdapter : BaseAwakeCommandAdapter
{
    public override OperationResult<CommandAdapterPreflight> Preflight(CommandRequest request, RequestContext context)
    {
        if (context == null || context.IsExpired) return Denied("awake.world_state.context_expired", context?.CorrelationId);
        JObject args = ParseArguments(request);
        if (args == null || !Validate(args, out string _))
        {
            return Denied("awake.world_state.world_effect.invalid", context.CorrelationId);
        }
        return OperationResult<CommandAdapterPreflight>.Succeeded(new CommandAdapterPreflight(
            "记录世界事件：" + ((string)args["text"] ?? string.Empty),
            SnapshotFromArguments(request)));
    }

    public override OperationResult<CommandAdapterResult> Execute(
        CommandRequest request,
        RequestContext context,
        string expectedSnapshotToken)
    {
        if (context == null || context.IsExpired) return ResultFailed("awake.world_state.context_expired", context?.CorrelationId);
        if (!TokenMatches(expectedSnapshotToken, SnapshotFromArguments(request)))
        {
            return ResultFailed("awake.world_state.world_effect.snapshot_mismatch", context.CorrelationId);
        }
        JObject args = ParseArguments(request);
        if (args == null || !Validate(args, out string _))
        {
            return ResultFailed("awake.world_state.world_effect.invalid", context.CorrelationId);
        }
        if (AwakeRuntime.SessionEnded) return ResultFailed("awake.world_state.session_ended", context.CorrelationId);
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null) return ResultFailed("awake.world_state.store_unavailable", context.CorrelationId);

        int day = TryInt(args["day"]) ?? AwakeRuntime.CurrentGameDay();
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.WorldEventsNamespace,
            AiTaskConstants.WorldEventsKey,
            "awake.world.events.append",
            request.IdempotencyKey,
            string.Empty,
            WorldStateKind.WorldEvents,
            new JObject
            {
                ["day"] = day,
                ["kind"] = (string)args["kind"] ?? "event",
                ["text"] = (string)args["text"] ?? string.Empty
            },
            DateTimeOffset.UtcNow,
            context.CorrelationId);
        if (!store.TryEnqueue(command))
        {
            return ResultFailed("awake.world_state.session_ended", context.CorrelationId);
        }
        return OperationResult<CommandAdapterResult>.Succeeded(
            new CommandAdapterResult(CommandState.Succeeded, "世界事件已记录。"));
    }

    internal static bool Validate(JObject args, out string error)
    {
        error = string.Empty;
        if (args == null)
        {
            error = "args";
            return false;
        }
        string text = (string)args["text"];
        if (string.IsNullOrWhiteSpace(text) || text.Length > 2000)
        {
            error = "text";
            return false;
        }
        string kind = (string)args["kind"];
        if (!string.IsNullOrWhiteSpace(kind) && kind.Length > 80)
        {
            error = "kind";
            return false;
        }
        if (args["day"] != null && args["day"].Type != JTokenType.Integer)
        {
            error = "day";
            return false;
        }
        return true;
    }

    private static int? TryInt(JToken token)
    {
        if (token == null || token.Type != JTokenType.Integer) return null;
        try
        {
            return (int)token;
        }
        catch
        {
            return null;
        }
    }

    private static OperationResult<CommandAdapterPreflight> Denied(string code, string correlationId)
    {
        return OperationResult<CommandAdapterPreflight>.Failed(FrameworkErrors.Create(
            code,
            FrameworkErrorCategory.InvalidRequest,
            "World effect command rejected.",
            correlationId,
            owner: AwakeConstants.OwnerValue));
    }

    private static OperationResult<CommandAdapterResult> ResultFailed(string code, string correlationId)
    {
        return OperationResult<CommandAdapterResult>.Failed(FrameworkErrors.Create(
            code,
            FrameworkErrorCategory.InvalidRequest,
            "World effect command failed.",
            correlationId,
            owner: AwakeConstants.OwnerValue));
    }
}

internal sealed class AwakePromiseRequestAdapter : BaseAwakeCommandAdapter
{
    public override OperationResult<CommandAdapterPreflight> Preflight(CommandRequest request, RequestContext context)
    {
        if (context == null || context.IsExpired) return Denied("awake.world_state.context_expired", context?.CorrelationId);
        JObject args = ParseArguments(request);
        if (args == null || !Validate(args, out string _))
        {
            return Denied("awake.action.promise_request.invalid", context.CorrelationId);
        }
        return OperationResult<CommandAdapterPreflight>.Succeeded(new CommandAdapterPreflight(
            "承诺请求：" + ((string)args["text"] ?? string.Empty),
            SnapshotFromArguments(request)));
    }

    public override OperationResult<CommandAdapterResult> Execute(
        CommandRequest request,
        RequestContext context,
        string expectedSnapshotToken)
    {
        if (context == null || context.IsExpired) return ResultFailed("awake.world_state.context_expired", context?.CorrelationId);
        if (!TokenMatches(expectedSnapshotToken, SnapshotFromArguments(request)))
        {
            return ResultFailed("awake.action.promise_request.snapshot_mismatch", context.CorrelationId);
        }
        JObject args = ParseArguments(request);
        if (args == null || !Validate(args, out string _))
        {
            return ResultFailed("awake.action.promise_request.invalid", context.CorrelationId);
        }
        if (AwakeRuntime.SessionEnded) return ResultFailed("awake.world_state.session_ended", context.CorrelationId);
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null) return ResultFailed("awake.world_state.store_unavailable", context.CorrelationId);

        string playerHeroId = (string)args["playerHeroId"] ?? string.Empty;
        string targetHeroId = (string)args["targetHeroId"] ?? string.Empty;
        string contactKey = (string)args["canonicalContactKey"] ?? targetHeroId;
        bool playerObliged = StringComparer.OrdinalIgnoreCase.Equals((string)args["obligor"], "player");
        JObject promise = new JObject
        {
            ["promiseId"] = AwakePromiseStateMachine.NewPromiseId(),
            ["status"] = AwakePromiseStateMachine.Pending,
            ["text"] = ClampText((string)args["text"] ?? string.Empty, 240),
            ["day"] = AwakeRuntime.CurrentGameDay(),
            ["playerHeroId"] = playerHeroId,
            ["targetHeroId"] = targetHeroId,
            ["obligor"] = playerObliged ? playerHeroId : targetHeroId,
            ["obligee"] = playerObliged ? targetHeroId : playerHeroId
        };
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.InteractionsNamespace,
            WorldStateStore.BuildInteractionKey(contactKey),
            AiTaskConstants.PromiseRequestCommandId,
            request.IdempotencyKey,
            contactKey,
            WorldStateKind.Interaction,
            new JObject
            {
                ["mode"] = "promise_upsert",
                ["promise"] = promise
            },
            DateTimeOffset.UtcNow,
            context.CorrelationId);
        if (!store.TryEnqueue(command))
        {
            return ResultFailed("awake.world_state.session_ended", context.CorrelationId);
        }
        return OperationResult<CommandAdapterResult>.Succeeded(
            new CommandAdapterResult(CommandState.Succeeded, "承诺已进入账本。"));
    }

    internal static bool Validate(JObject args, out string error)
    {
        error = string.Empty;
        if (args == null)
        {
            error = "args";
            return false;
        }
        string playerHeroId = (string)args["playerHeroId"] ?? string.Empty;
        string targetHeroId = (string)args["targetHeroId"] ?? string.Empty;
        string text = (string)args["text"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(playerHeroId) || string.IsNullOrWhiteSpace(targetHeroId))
        {
            error = "hero";
            return false;
        }
        if (string.IsNullOrWhiteSpace(text) || text.Length > 240)
        {
            error = "text";
            return false;
        }
        string obligor = (string)args["obligor"] ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(obligor)
            && !StringComparer.OrdinalIgnoreCase.Equals(obligor, "player")
            && !StringComparer.OrdinalIgnoreCase.Equals(obligor, "npc"))
        {
            error = "obligor";
            return false;
        }
        return true;
    }

    private static string ClampText(string text, int maximum)
    {
        return AwakeRuntime.TruncateTextElements(text ?? string.Empty, maximum);
    }

    private static OperationResult<CommandAdapterPreflight> Denied(string code, string correlationId)
    {
        return OperationResult<CommandAdapterPreflight>.Failed(FrameworkErrors.Create(
            code,
            FrameworkErrorCategory.InvalidRequest,
            "Promise request rejected.",
            correlationId,
            owner: AwakeConstants.OwnerValue));
    }

    private static OperationResult<CommandAdapterResult> ResultFailed(string code, string correlationId)
    {
        return OperationResult<CommandAdapterResult>.Failed(FrameworkErrors.Create(
            code,
            FrameworkErrorCategory.InvalidRequest,
            "Promise request failed.",
            correlationId,
            owner: AwakeConstants.OwnerValue));
    }
}

internal sealed class AwakePromiseUpdateAdapter : BaseAwakeCommandAdapter
{
    public override OperationResult<CommandAdapterPreflight> Preflight(CommandRequest request, RequestContext context)
    {
        if (context == null || context.IsExpired) return Denied("awake.world_state.context_expired", context?.CorrelationId);
        JObject args = ParseArguments(request);
        if (args == null || !Validate(args, out string _))
        {
            return Denied("awake.action.promise_update.invalid", context.CorrelationId);
        }
        return OperationResult<CommandAdapterPreflight>.Succeeded(new CommandAdapterPreflight(
            "承诺状态：" + ((string)args["newStatus"] ?? string.Empty),
            SnapshotFromArguments(request)));
    }

    public override OperationResult<CommandAdapterResult> Execute(
        CommandRequest request,
        RequestContext context,
        string expectedSnapshotToken)
    {
        if (context == null || context.IsExpired) return ResultFailed("awake.world_state.context_expired", context?.CorrelationId);
        if (!TokenMatches(expectedSnapshotToken, SnapshotFromArguments(request)))
        {
            return ResultFailed("awake.action.promise_update.snapshot_mismatch", context.CorrelationId);
        }
        JObject args = ParseArguments(request);
        if (args == null || !Validate(args, out string _))
        {
            return ResultFailed("awake.action.promise_update.invalid", context.CorrelationId);
        }
        if (AwakeRuntime.SessionEnded) return ResultFailed("awake.world_state.session_ended", context.CorrelationId);
        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null) return ResultFailed("awake.world_state.store_unavailable", context.CorrelationId);

        string contactKey = (string)args["canonicalContactKey"] ?? string.Empty;
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.InteractionsNamespace,
            WorldStateStore.BuildInteractionKey(contactKey),
            AiTaskConstants.PromiseUpdateCommandId,
            request.IdempotencyKey,
            contactKey,
            WorldStateKind.Interaction,
            new JObject
            {
                ["mode"] = "promise_update",
                ["promiseId"] = (string)args["promiseId"] ?? string.Empty,
                ["newStatus"] = (string)args["newStatus"] ?? string.Empty,
                ["reason"] = (string)args["reason"] ?? string.Empty
            },
            DateTimeOffset.UtcNow,
            context.CorrelationId);
        if (!store.TryEnqueue(command))
        {
            return ResultFailed("awake.world_state.session_ended", context.CorrelationId);
        }
        return OperationResult<CommandAdapterResult>.Succeeded(
            new CommandAdapterResult(CommandState.Succeeded, "承诺状态已更新。"));
    }

    internal static bool Validate(JObject args, out string error)
    {
        error = string.Empty;
        if (args == null)
        {
            error = "args";
            return false;
        }
        string promiseId = (string)args["promiseId"] ?? string.Empty;
        string status = (string)args["newStatus"] ?? string.Empty;
        string contactKey = (string)args["canonicalContactKey"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(promiseId) || string.IsNullOrWhiteSpace(contactKey))
        {
            error = "id";
            return false;
        }
        if (!AwakePromiseStateMachine.IsValidStatus(status))
        {
            error = "status";
            return false;
        }
        return true;
    }

    private static OperationResult<CommandAdapterPreflight> Denied(string code, string correlationId)
    {
        return OperationResult<CommandAdapterPreflight>.Failed(FrameworkErrors.Create(
            code,
            FrameworkErrorCategory.InvalidRequest,
            "Promise update rejected.",
            correlationId,
            owner: AwakeConstants.OwnerValue));
    }

    private static OperationResult<CommandAdapterResult> ResultFailed(string code, string correlationId)
    {
        return OperationResult<CommandAdapterResult>.Failed(FrameworkErrors.Create(
            code,
            FrameworkErrorCategory.InvalidRequest,
            "Promise update failed.",
            correlationId,
            owner: AwakeConstants.OwnerValue));
    }
}

internal sealed class AwakeGiveGoldAdapter : BaseAwakeCommandAdapter
{
    public override OperationResult<CommandAdapterPreflight> Preflight(CommandRequest request, RequestContext context)
    {
        if (context == null || context.IsExpired) return Denied("awake.world_state.context_expired", context?.CorrelationId);
        JObject args = ParseArguments(request);
        if (args == null || !Validate(args, out string _))
        {
            return Denied("awake.action.give_gold.invalid", context.CorrelationId);
        }
        return OperationResult<CommandAdapterPreflight>.Succeeded(new CommandAdapterPreflight(
            "给金币：" + ((string)args["amount"] ?? "0"),
            SnapshotFromArguments(request)));
    }

    public override OperationResult<CommandAdapterResult> Execute(
        CommandRequest request,
        RequestContext context,
        string expectedSnapshotToken)
    {
        if (context == null || context.IsExpired) return ResultFailed("awake.world_state.context_expired", context?.CorrelationId);
        if (!TokenMatches(expectedSnapshotToken, SnapshotFromArguments(request)))
        {
            return ResultFailed("awake.action.give_gold.snapshot_mismatch", context.CorrelationId);
        }
        JObject args = ParseArguments(request);
        if (args == null || !Validate(args, out string _))
        {
            return ResultFailed("awake.action.give_gold.invalid", context.CorrelationId);
        }
        if (AwakeRuntime.SessionEnded) return ResultFailed("awake.world_state.session_ended", context.CorrelationId);
        if (Hero.MainHero == null || Hero.MainHero.Gold < 0)
        {
            return ResultFailed("awake.action.give_gold.player_unavailable", context.CorrelationId);
        }
        int amount = (int)args["amount"];
        if (Hero.MainHero.Gold < amount)
        {
            return OperationResult<CommandAdapterResult>.Failed(FrameworkErrors.Create(
                "awake.action.insufficient_gold",
                FrameworkErrorCategory.Conflict,
                "Player gold is insufficient.",
                context.CorrelationId,
                owner: AwakeConstants.OwnerValue));
        }
        Hero.MainHero.Gold -= amount;

        WorldStateStore store = AwakeRuntime.WorldStateStore;
        if (store == null) return ResultFailed("awake.world_state.store_unavailable", context.CorrelationId);
        string contactKey = (string)args["canonicalContactKey"] ?? (string)args["targetHeroId"];
        WorldStateCommand command = new WorldStateCommand(
            AiTaskConstants.InteractionsNamespace,
            WorldStateStore.BuildInteractionKey(contactKey),
            AiTaskConstants.GiveGoldCommandId,
            request.IdempotencyKey,
            contactKey,
            WorldStateKind.Interaction,
            new JObject
            {
                ["mode"] = "give_gold",
                ["amount"] = amount,
                ["targetHeroId"] = (string)args["targetHeroId"] ?? string.Empty,
                ["day"] = AwakeRuntime.CurrentGameDay(),
                ["reason"] = (string)args["reason"] ?? string.Empty
            },
            DateTimeOffset.UtcNow,
            context.CorrelationId);
        if (!store.TryEnqueue(command))
        {
            return ResultFailed("awake.world_state.session_ended", context.CorrelationId);
        }
        return OperationResult<CommandAdapterResult>.Succeeded(
            new CommandAdapterResult(CommandState.Succeeded, "金币已扣除并记录。"));
    }

    internal static bool Validate(JObject args, out string error)
    {
        error = string.Empty;
        if (args == null)
        {
            error = "args";
            return false;
        }
        string targetHeroId = (string)args["targetHeroId"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(targetHeroId) || targetHeroId.Length > 120)
        {
            error = "target";
            return false;
        }
        JToken amountToken = args["amount"];
        if (amountToken == null || amountToken.Type != JTokenType.Integer)
        {
            error = "amount";
            return false;
        }
        int amount;
        try
        {
            amount = (int)amountToken;
        }
        catch
        {
            error = "amount";
            return false;
        }
        if (amount < 1 || amount > 100000)
        {
            error = "amount";
            return false;
        }
        string reason = (string)args["reason"];
        if (reason != null && reason.Length > 240)
        {
            error = "reason";
            return false;
        }
        return true;
    }

    private static OperationResult<CommandAdapterPreflight> Denied(string code, string correlationId)
    {
        return OperationResult<CommandAdapterPreflight>.Failed(FrameworkErrors.Create(
            code,
            FrameworkErrorCategory.InvalidRequest,
            "Give gold command rejected.",
            correlationId,
            owner: AwakeConstants.OwnerValue));
    }

    private static OperationResult<CommandAdapterResult> ResultFailed(string code, string correlationId)
    {
        return OperationResult<CommandAdapterResult>.Failed(FrameworkErrors.Create(
            code,
            FrameworkErrorCategory.InvalidRequest,
            "Give gold command failed.",
            correlationId,
            owner: AwakeConstants.OwnerValue));
    }
}
