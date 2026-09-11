using System;
using System.Threading;
using System.Threading.Tasks;
using MarcusAwakeFramework.Api;
using TaleWorlds.CampaignSystem;

namespace Awake;

/// <summary>
/// 玩家数据出口的 AWAKE 实现：读取主角快照，交给框架适配成兼容层服务。
/// 字段映射按 PLAN-AWAKE-DIALOGUE-CHAIN-010 的 D-5 写死：
/// Hero.Id = EntityRef("hero", Hero.MainHero.StringId)；Clan / Kingdom 可为空；
/// 正向断言 AwakeRuntime.CurrentHeroId == Hero.MainHero.StringId。
/// </summary>
internal sealed class AwakePlayerSnapshotProvider : IPlayerSnapshotProvider
{
    public Task<OperationResult<PlayerSnapshotDto>> GetCurrentPlayerAsync(
        RequestContext context,
        CancellationToken cancellationToken)
    {
        // 主角快照必须读游戏对象，统一回到游戏线程；测试/早期初始化未登记线程时直接内联执行。
        return AwakeUiDispatcher.RunOnGameThreadAsync(() => Task.FromResult(Read(context)), cancellationToken);
    }

    private static OperationResult<PlayerSnapshotDto> Read(RequestContext context)
    {
        string correlation = context?.CorrelationId ?? "player-snapshot";
        try
        {
            if (Campaign.Current == null)
            {
                AwakeLog.Write("player_snapshot_unavailable reason=campaign_missing");
                return Failure("game_data.campaign_unavailable", "There is no active campaign.", correlation);
            }

            Hero hero = Hero.MainHero;
            if (hero == null)
            {
                AwakeLog.Write("player_snapshot_unavailable reason=main_hero_missing");
                return Failure("game_data.player_unavailable", "The main hero is not available.", correlation);
            }

            string heroId = hero.StringId;
            if (string.IsNullOrWhiteSpace(heroId))
            {
                AwakeLog.Write("player_snapshot_unavailable reason=main_hero_id_missing");
                return Failure("game_data.player_unavailable", "The main hero has no stable identifier.", correlation);
            }

            DateTimeOffset observedAt = DateTimeOffset.UtcNow;
            string snapshotToken = BuildSnapshotToken(heroId);
            DataProvenance provenance = new DataProvenance(
                DataAccessScope.PlayerKnown,
                SourceClass.GameRuntime,
                EpistemicStatus.Fact,
                observedAt,
                snapshotToken);

            Clan clan = hero.Clan;
            Kingdom kingdom = clan?.Kingdom;
            EntityRef heroRef = new EntityRef("hero", heroId);
            EntityRef clanRef = clan != null ? new EntityRef("clan", clan.StringId) : null;
            EntityRef kingdomRef = kingdom != null ? new EntityRef("kingdom", kingdom.StringId) : null;

            HeroDto heroDto = new HeroDto(
                heroRef,
                hero.Name?.ToString() ?? string.Empty,
                clanRef,
                kingdomRef,
                hero.IsAlive,
                isPlayer: true,
                hero.CharacterObject != null ? hero.CharacterObject.Level : 0,
                provenance);
            ClanDto clanDto = clan == null
                ? null
                : new ClanDto(
                    clanRef,
                    clan.Name?.ToString() ?? string.Empty,
                    clan.Leader != null ? new EntityRef("hero", clan.Leader.StringId) : null,
                    kingdomRef,
                    clan.Tier,
                    provenance);
            KingdomDto kingdomDto = kingdom == null
                ? null
                : new KingdomDto(
                    kingdomRef,
                    kingdom.Name?.ToString() ?? string.Empty,
                    kingdom.Leader != null ? new EntityRef("hero", kingdom.Leader.StringId) : null,
                    provenance);

            AwakeLog.Write("player_snapshot_read hero=" + heroId
                + " clan=" + (clanRef?.StableId ?? "none")
                + " kingdom=" + (kingdomRef?.StableId ?? "none"));
            return OperationResult<PlayerSnapshotDto>.Succeeded(
                new PlayerSnapshotDto(heroDto, clanDto, kingdomDto, snapshotToken));
        }
        catch (Exception ex)
        {
            AwakeLog.Write("player_snapshot_error error=" + ex.Message);
            return Failure("game_data.player_snapshot_error", "The player snapshot could not be read.", correlation);
        }
    }

    private static string BuildSnapshotToken(string heroId)
    {
        string campaignId = Campaign.Current?.UniqueGameId ?? string.Empty;
        return "awake.player:" + campaignId + ":" + heroId + ":" + AwakeRuntime.SessionGeneration;
    }

    private static OperationResult<PlayerSnapshotDto> Failure(string code, string message, string correlation)
    {
        return OperationResult<PlayerSnapshotDto>.Failed(FrameworkErrors.Create(
            code,
            FrameworkErrorCategory.Unavailable,
            message,
            correlation,
            retryable: true,
            owner: AwakeConstants.OwnerValue));
    }
}
