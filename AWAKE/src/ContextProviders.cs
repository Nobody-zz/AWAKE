using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcusAIFramework.Api;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;

namespace Awake;

internal abstract class BaseAwakeContextProvider : IContextProvider
{
    public ExtensionId Owner { get; } = new ExtensionId(AwakeConstants.OwnerValue);
    public abstract string ProviderId { get; }

    public async Task<OperationResult<IReadOnlyList<ContextContribution>>> ContributeAsync(
        ContextPlanRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return OperationResult<IReadOnlyList<ContextContribution>>.Failed(FrameworkErrors.Create(
                "awake.cancelled",
                FrameworkErrorCategory.Cancelled,
                "The context provider request was cancelled.",
                context?.CorrelationId,
                owner: AwakeConstants.OwnerValue));
        }
        if (context == null || context.IsExpired)
        {
            return OperationResult<IReadOnlyList<ContextContribution>>.Failed(FrameworkErrors.Create(
                "awake.context_expired",
                FrameworkErrorCategory.Expired,
                "The context provider request expired.",
                context?.CorrelationId,
                owner: AwakeConstants.OwnerValue));
        }
        if (request == null || !ContainsScope(request.AllowedAccessScopes))
        {
            return OperationResult<IReadOnlyList<ContextContribution>>.Succeeded(Array.Empty<ContextContribution>());
        }
        return await ContributeCoreAsync(request, context, cancellationToken).ConfigureAwait(false);
    }

    protected abstract Task<OperationResult<IReadOnlyList<ContextContribution>>> ContributeCoreAsync(
        ContextPlanRequest request,
        RequestContext context,
        CancellationToken cancellationToken);

    private static bool ContainsScope(IReadOnlyList<string> scopes)
    {
        if (scopes == null) return false;
        foreach (string scope in scopes)
        {
            if (StringComparer.Ordinal.Equals(scope, AiTaskConstants.PlayerKnownScope)) return true;
        }
        return false;
    }
}

internal sealed class PlayerContextProvider : BaseAwakeContextProvider
{
    public override string ProviderId => AiTaskConstants.PlayerContextProviderId;

    protected override async Task<OperationResult<IReadOnlyList<ContextContribution>>> ContributeCoreAsync(
        ContextPlanRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            IMarcusAiFrameworkHost host = AwakeRuntime.ResolveHost();
            if (host == null || host.GameData == null)
            {
                return OperationResult<IReadOnlyList<ContextContribution>>.Succeeded(Array.Empty<ContextContribution>());
            }
            PermissionDefinition playerKnown;
            if (!PermissionCatalog.TryGet(AwakeConstants.PermissionPlayerKnownRead, out playerKnown))
            {
                AwakeLog.Write("player_context_provider_catalog_missing");
                return OperationResult<IReadOnlyList<ContextContribution>>.Succeeded(Array.Empty<ContextContribution>());
            }
            PermissionGateResult playerKnownGate = new PermissionGate(host).Evaluate(playerKnown, context);
            if (!playerKnownGate.Granted)
            {
                AwakeLog.Write("player_context_provider_permission_denied code=" + (playerKnownGate.Error?.Code ?? "none") + " correlation=" + (context?.CorrelationId ?? string.Empty));
                return OperationResult<IReadOnlyList<ContextContribution>>.Succeeded(Array.Empty<ContextContribution>());
            }
            OperationResult<PlayerSnapshotDto> result = await host.GameData.GetCurrentPlayerAsync(context, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess || result.Value == null || result.Value.Hero == null)
            {
                AwakeLog.Write("player_context_provider_degraded code=" + (result.Error?.Code ?? "empty"));
                return OperationResult<IReadOnlyList<ContextContribution>>.Succeeded(Array.Empty<ContextContribution>());
            }

            PlayerSnapshotDto snapshot = result.Value;
            string clanName = snapshot.Clan?.Name ?? string.Empty;
            string kingdomName = snapshot.Kingdom?.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(clanName) && Hero.MainHero?.Clan != null)
            {
                clanName = Hero.MainHero.Clan.Name?.ToString() ?? string.Empty;
            }
            if (string.IsNullOrWhiteSpace(kingdomName) && Hero.MainHero?.Clan?.Kingdom != null)
            {
                kingdomName = Hero.MainHero.Clan.Kingdom.Name?.ToString() ?? string.Empty;
            }
            JObject payload = new JObject
            {
                ["heroId"] = snapshot.Hero.Id?.StableId ?? string.Empty,
                ["playerName"] = snapshot.Hero.Name ?? string.Empty,
                ["clanName"] = clanName,
                ["kingdomName"] = kingdomName,
                ["snapshotToken"] = snapshot.SnapshotToken ?? string.Empty
            };
            ContextContribution contribution = new ContextContribution(
                ProviderId + ".1",
                ProviderId,
                ContextContentType.Structured,
                AiTaskConstants.PlayerKnownScope,
                SourceClass.GameRuntime.ToString(),
                EpistemicStatus.Fact.ToString(),
                new[] { snapshot.Hero.Id },
                DateTimeOffset.UtcNow,
                snapshot.SnapshotToken ?? string.Empty,
                1,
                16,
                null,
                CloudExportPolicy.PlayerState,
                payload.ToString(Formatting.None));
            return OperationResult<IReadOnlyList<ContextContribution>>.Succeeded(new[] { contribution });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AwakeLog.Write("player_context_provider_error error=" + ex.Message);
            return OperationResult<IReadOnlyList<ContextContribution>>.Succeeded(Array.Empty<ContextContribution>());
        }
    }
}

internal sealed class HeroContextProvider : BaseAwakeContextProvider
{
    public override string ProviderId => AiTaskConstants.HeroContextProviderId;

    protected override Task<OperationResult<IReadOnlyList<ContextContribution>>> ContributeCoreAsync(
        ContextPlanRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        string heroId = AwakeRuntime.CurrentHeroId;
        if (string.IsNullOrWhiteSpace(heroId))
        {
            return Task.FromResult(OperationResult<IReadOnlyList<ContextContribution>>.Succeeded(Array.Empty<ContextContribution>()));
        }

        JObject payload = new JObject
        {
            ["heroId"] = heroId,
            ["bound"] = true
        };
        EntityRef heroRef = new EntityRef("awake", "hero", heroId);
        ContextContribution contribution = new ContextContribution(
            ProviderId + ".1",
            ProviderId,
            ContextContentType.Structured,
            AiTaskConstants.PlayerKnownScope,
            SourceClass.GameRuntime.ToString(),
            EpistemicStatus.Fact.ToString(),
            new[] { heroRef },
            DateTimeOffset.UtcNow,
            "hero-bound",
            1,
            8,
            null,
            CloudExportPolicy.PlayerState,
            payload.ToString(Formatting.None));
        return Task.FromResult(OperationResult<IReadOnlyList<ContextContribution>>.Succeeded(new[] { contribution }));
    }
}
