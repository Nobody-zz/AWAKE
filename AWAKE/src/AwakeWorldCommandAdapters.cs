using System;
using System.Text;
using MarcusAIFramework.Api;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
