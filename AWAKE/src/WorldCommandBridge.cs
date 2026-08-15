using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarcusAIFramework.Api;

namespace Awake;

internal sealed class WorldCommandProposal
{
    internal string CommandId { get; }
    internal string ArgumentsJson { get; }
    internal string Reason { get; }

    internal WorldCommandProposal(string commandId, string argumentsJson, string reason)
    {
        CommandId = commandId ?? string.Empty;
        ArgumentsJson = argumentsJson ?? "{}";
        Reason = reason ?? string.Empty;
    }
}

internal sealed class WorldCommandBridge
{
    private readonly IMarcusAiFrameworkHost _host;

    internal WorldCommandBridge(IMarcusAiFrameworkHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    internal async Task<OperationResult<string>> ExecuteAsync(WorldCommandProposal proposal, string turnIntentId, CancellationToken cancellationToken)
    {
        if (proposal == null)
        {
            return OperationResult<string>.Failed(FrameworkErrors.Create(
                "awake.world_state.command_not_allowed",
                FrameworkErrorCategory.InvalidRequest,
                "The command is not allowed through the world bridge.",
                turnIntentId,
                owner: AwakeConstants.OwnerValue));
        }

        if (!CommandRiskPolicy.IsWorldBridgeAllowed(proposal.CommandId))
        {
            return OperationResult<string>.Failed(FrameworkErrors.Create(
                "awake.world_state.command_not_allowed",
                FrameworkErrorCategory.InvalidRequest,
                "The command is not allowed through the world bridge.",
                turnIntentId,
                owner: AwakeConstants.OwnerValue));
        }

        if (CommandRiskPolicy.TryGetRiskTier(proposal.CommandId, out CommandRiskTier riskTier))
        {
            AwakeLog.Write("command_risk_level=" + CommandRiskPolicy.RiskLabel(riskTier) + " command=" + proposal.CommandId);
        }
        else
        {
            AwakeLog.Write("command_risk_unknown command=" + proposal.CommandId);
        }

        string correlation = string.IsNullOrWhiteSpace(turnIntentId) ? Guid.NewGuid().ToString("N") : turnIntentId;
        RequestContext permissionContext = AwakeRuntime.CreateContext(_host, correlation);
        PermissionGate permissionGate = new PermissionGate(_host);
        PermissionGateResult permission = await permissionGate.EnsureAsync(
            PermissionCatalog.CommandPermission(proposal.CommandId),
            permissionContext,
            cancellationToken,
            "AWAKE 需要执行世界状态命令：" + proposal.CommandId + "。").ConfigureAwait(false);
        if (!permission.Granted)
        {
            if (permission.Error != null && permission.Error.Category == FrameworkErrorCategory.Denied)
            {
                return OperationResult<string>.Failed(FrameworkErrors.Create(
                    "awake.world_state.permission_denied",
                    FrameworkErrorCategory.Denied,
                    "The world state command was not authorized.",
                    correlation,
                    owner: AwakeConstants.OwnerValue));
            }
            FrameworkErrorCategory category = permission.Error != null && permission.Error.Category == FrameworkErrorCategory.InvalidRequest
                ? FrameworkErrorCategory.InvalidRequest
                : FrameworkErrorCategory.InternalFailure;
            return OperationResult<string>.Failed(FrameworkErrors.Create(
                "awake.world_state.permission_error",
                category,
                "Permission evaluation failed.",
                correlation,
                owner: AwakeConstants.OwnerValue));
        }

        if (AwakeRuntime.WorldStateStore == null)
        {
            return OperationResult<string>.Failed(FrameworkErrors.Create(
                "awake.world_state.store_unavailable",
                FrameworkErrorCategory.Unavailable,
                "The world state store is not ready.",
                correlation,
                retryable: true,
                owner: AwakeConstants.OwnerValue));
        }

        try
        {
            string idempotencyKey = DeriveIdempotencyKey(turnIntentId, proposal.CommandId, proposal.ArgumentsJson);
            RequestContext context = AwakeRuntime.CreateContext(_host, correlation);
            CommandRequest request = new CommandRequest(
                Guid.NewGuid().ToString("N"),
                proposal.CommandId,
                proposal.ArgumentsJson,
                idempotencyKey,
                DateTimeOffset.UtcNow + AwakeConstants.RequestTimeout);

            OperationResult<CommandPreflight> preflight = await _host.Commands.PreflightAsync(request, context, cancellationToken).ConfigureAwait(false);
            if (!preflight.IsSuccess || preflight.Value == null)
            {
                AwakeLog.Write("world_command_preflight_failed code=" + (preflight.Error?.Code ?? "unknown"));
                return OperationResult<string>.Failed(NormalizeError(preflight.Error, "awake.world_state.preflight_failed", "Command preflight failed.", context.CorrelationId));
            }

            OperationResult<CommandReceipt> submitted = await _host.Commands.SubmitAsync(request, context, cancellationToken).ConfigureAwait(false);
            if (!submitted.IsSuccess || submitted.Value == null)
            {
                AwakeLog.Write("world_command_submit_failed code=" + (submitted.Error?.Code ?? "unknown"));
                return OperationResult<string>.Failed(NormalizeError(submitted.Error, "awake.world_state.submit_failed", "Command submission failed.", context.CorrelationId));
            }

            CommandReceipt receipt = submitted.Value;
            if (receipt.State != CommandState.Succeeded)
            {
                return OperationResult<string>.Failed(FrameworkErrors.Create(
                    "awake.world_state.receipt_not_succeeded",
                    FrameworkErrorCategory.Conflict,
                    "Command receipt did not succeed: " + receipt.State,
                    context.CorrelationId,
                    owner: AwakeConstants.OwnerValue));
            }

            WorldStateStore store = AwakeRuntime.WorldStateStore;
            if (store != null)
            {
                WorldDrainSummary summary = await store.DrainAsync(proposal.CommandId, idempotencyKey, cancellationToken).ConfigureAwait(false);
                if (summary.DeferredRetry)
                {
                    return OperationResult<string>.Failed(FrameworkErrors.Create(
                        "awake.world_state.write_pending",
                        FrameworkErrorCategory.Unavailable,
                        "The world state write is still pending.",
                        context.CorrelationId,
                        true,
                        AwakeConstants.OwnerValue));
                }
                if (!string.IsNullOrWhiteSpace(summary.HardFailureCode))
                {
                    return OperationResult<string>.Failed(FrameworkErrors.Create(
                        summary.HardFailureCode,
                        CategoryForHardFailure(summary.HardFailureCode),
                        "The world state write failed.",
                        context.CorrelationId,
                        false,
                        AwakeConstants.OwnerValue));
                }
                if (!summary.OwnerCommandObserved)
                {
                    return OperationResult<string>.Failed(FrameworkErrors.Create(
                        "awake.world_state.write_pending",
                        FrameworkErrorCategory.Unavailable,
                        "The world state write was not observed.",
                        context.CorrelationId,
                        true,
                        AwakeConstants.OwnerValue));
                }
                string text = receipt.Summary ?? "世界状态已更新。";
                if (summary.EventPublishFailureCount > 0)
                {
                    text += " 事件记录未同步。";
                }
                return OperationResult<string>.Succeeded(text);
            }
            return OperationResult<string>.Succeeded(receipt.Summary ?? "世界状态已更新。");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("world_command_error error=" + ex.Message);
            return OperationResult<string>.Failed(FrameworkErrors.Create(
                "awake.world_state.execution_error",
                FrameworkErrorCategory.InternalFailure,
                "World command execution failed.",
                correlation,
                owner: AwakeConstants.OwnerValue));
        }
    }

    private static FrameworkError NormalizeError(FrameworkError error, string fallbackCode, string fallbackMessage, string correlationId)
    {
        FrameworkError source = error ?? FrameworkErrors.Create(fallbackCode, FrameworkErrorCategory.InternalFailure, fallbackMessage, correlationId, owner: AwakeConstants.OwnerValue);
        string owner = string.IsNullOrWhiteSpace(source.Owner) ? AwakeConstants.OwnerValue : source.Owner;
        string correlation = string.IsNullOrWhiteSpace(source.CorrelationId) ? correlationId : source.CorrelationId;
        if (StringComparer.Ordinal.Equals(owner, source.Owner) && StringComparer.Ordinal.Equals(correlation, source.CorrelationId))
        {
            return source;
        }
        return new FrameworkError(source.Code, source.Category, source.MessageTextId, source.SafeFallback, source.Retryable, owner, correlation, source.Details);
    }

    internal static string DeriveIdempotencyKey(string turnIntentId, string commandId, string argumentsJson)
    {
        string canonical = AwakeRuntime.CanonicalizeArguments(argumentsJson);
        string session = string.Empty;
        IMarcusAiFrameworkHost resolved = AwakeRuntime.ResolveHost();
        if (resolved != null && resolved.CurrentSession != null)
        {
            session = resolved.CurrentSession.CampaignId + "|" + resolved.CurrentSession.TimelineId + "|gen" + AwakeRuntime.SessionGeneration;
        }
        string input = session + "|" + (turnIntentId ?? string.Empty) + "|" + (commandId ?? string.Empty) + "|" + canonical;
        using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            StringBuilder builder = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) builder.Append(b.ToString("x2"));
            return builder.ToString();
        }
    }

    private static FrameworkErrorCategory CategoryForHardFailure(string code)
    {
        if (StringComparer.Ordinal.Equals(code, "awake.world_state.favor.insufficient_balance"))
        {
            return FrameworkErrorCategory.Conflict;
        }
        if (StringComparer.Ordinal.Equals(code, "awake.world_state.corrupt"))
        {
            return FrameworkErrorCategory.InvalidRequest;
        }
        return FrameworkErrorCategory.InternalFailure;
    }
}
