using System;

namespace Awake;

internal sealed class IdentitySnapshotInput
{
    internal string StableId { get; set; }
    internal string HeroId { get; set; }
    internal string CharacterId { get; set; }
    internal string CultureId { get; set; }
    internal string KingdomId { get; set; }
    internal string ClanId { get; set; }
    internal string ClanLeaderId { get; set; }
    internal string SettlementId { get; set; }
    internal string Occupation { get; set; }
    internal float? Age { get; set; }
    internal bool? IsFemale { get; set; }
    internal bool? IsNoble { get; set; }
    internal bool? IsClanLeader { get; set; }
    internal bool? IsKingdomLeader { get; set; }
    internal bool? IsFactionLeader { get; set; }
    internal bool? IsHeadman { get; set; }
    internal bool? IsRuralNotable { get; set; }
    internal bool? IsMerchant { get; set; }
    internal bool? IsGangLeader { get; set; }
    internal bool? IsWanderer { get; set; }
}

internal sealed class IdentitySnapshot
{
    internal const string CurrentSchemaVersion = "identity.v1";

    private IdentitySnapshot(IdentitySnapshotInput input)
    {
        SchemaVersion = CurrentSchemaVersion;
        StableId = input?.StableId;
        HeroId = input?.HeroId;
        CharacterId = input?.CharacterId;
        CultureId = input?.CultureId;
        KingdomId = input?.KingdomId;
        ClanId = input?.ClanId;
        ClanLeaderId = input?.ClanLeaderId;
        SettlementId = input?.SettlementId;
        Occupation = input?.Occupation;
        Age = input?.Age;
        IsFemale = input?.IsFemale;
        IsNoble = input?.IsNoble;
        IsClanLeader = input?.IsClanLeader;
        IsKingdomLeader = input?.IsKingdomLeader;
        IsFactionLeader = input?.IsFactionLeader;
        IsHeadman = input?.IsHeadman;
        IsRuralNotable = input?.IsRuralNotable;
        IsMerchant = input?.IsMerchant;
        IsGangLeader = input?.IsGangLeader;
        IsWanderer = input?.IsWanderer;
    }

    internal string SchemaVersion { get; }
    internal string StableId { get; }
    internal string HeroId { get; }
    internal string CharacterId { get; }
    internal string CultureId { get; }
    internal string KingdomId { get; }
    internal string ClanId { get; }
    internal string ClanLeaderId { get; }
    internal string SettlementId { get; }
    internal string Occupation { get; }
    internal float? Age { get; }
    internal bool? IsFemale { get; }
    internal bool? IsNoble { get; }
    internal bool? IsClanLeader { get; }
    internal bool? IsKingdomLeader { get; }
    internal bool? IsFactionLeader { get; }
    internal bool? IsHeadman { get; }
    internal bool? IsRuralNotable { get; }
    internal bool? IsMerchant { get; }
    internal bool? IsGangLeader { get; }
    internal bool? IsWanderer { get; }

    internal static IdentitySnapshot Capture(IdentitySnapshotInput input)
    {
        return new IdentitySnapshot(input);
    }
}
