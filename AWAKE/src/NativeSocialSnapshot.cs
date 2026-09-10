using System;
using System.Collections.Generic;

namespace Awake;

internal enum NativeFlagState
{
    Unknown,
    False,
    True
}

internal enum FamilyLinkKind
{
    Father,
    Mother,
    Spouse,
    Child
}

internal sealed class FamilyLinkSnapshotInput
{
    internal FamilyLinkSnapshotInput(FamilyLinkKind kind, string heroId)
    {
        Kind = kind;
        HeroId = heroId;
    }

    internal FamilyLinkKind Kind { get; }
    internal string HeroId { get; }
}

internal sealed class FamilyLinkSnapshot
{
    private FamilyLinkSnapshot(FamilyLinkSnapshotInput input)
    {
        Kind = input.Kind;
        HeroId = input.HeroId;
    }

    internal FamilyLinkKind Kind { get; }
    internal string HeroId { get; }

    internal static FamilyLinkSnapshot Capture(FamilyLinkSnapshotInput input)
    {
        return input == null ? null : new FamilyLinkSnapshot(input);
    }
}

internal sealed class NativeSocialSnapshotInput
{
    internal int SessionGeneration { get; set; }
    internal string SnapshotToken { get; set; }
    internal double? CapturedAtGameTime { get; set; }
    internal IdentitySnapshotInput SourceIdentity { get; set; }
    internal IdentitySnapshotInput TargetIdentity { get; set; }
    internal int? BaseRelation { get; set; }
    internal int? EffectiveRelation { get; set; }
    internal NativeFlagState NativeFriendState { get; set; }
    internal NativeFlagState NativeEnemyState { get; set; }
    internal NativeFlagState NativeNeutralState { get; set; }
    internal int? EffectiveClanRelation { get; set; }
    internal int? WeightedClanRelation { get; set; }
    internal NativeFlagState SameClan { get; set; }
    internal NativeFlagState SameKingdom { get; set; }
    internal List<FamilyLinkSnapshotInput> FamilyLinks { get; set; }
}

internal sealed class NativeSocialSnapshot
{
    internal const string CurrentSchemaVersion = "NativeSocialSnapshot.v1";

    private NativeSocialSnapshot(
        NativeSocialSnapshotInput input,
        IdentitySnapshot sourceIdentity,
        IdentitySnapshot targetIdentity,
        IReadOnlyList<FamilyLinkSnapshot> familyLinks)
    {
        SchemaVersion = CurrentSchemaVersion;
        SessionGeneration = input.SessionGeneration;
        SnapshotToken = input.SnapshotToken;
        CapturedAtGameTime = input.CapturedAtGameTime;
        SourceIdentity = sourceIdentity;
        TargetIdentity = targetIdentity;
        BaseRelation = input.BaseRelation;
        EffectiveRelation = input.EffectiveRelation;
        NativeFriendState = input.NativeFriendState;
        NativeEnemyState = input.NativeEnemyState;
        NativeNeutralState = input.NativeNeutralState;
        EffectiveClanRelation = input.EffectiveClanRelation;
        WeightedClanRelation = input.WeightedClanRelation;
        SameClan = input.SameClan;
        SameKingdom = input.SameKingdom;
        FamilyLinks = familyLinks;
        SourceHeroId = sourceIdentity?.HeroId;
        TargetHeroId = targetIdentity?.HeroId;
        SourceClanId = sourceIdentity?.ClanId;
        TargetClanId = targetIdentity?.ClanId;
        SourceClanLeaderId = sourceIdentity?.ClanLeaderId;
        TargetClanLeaderId = targetIdentity?.ClanLeaderId;
    }

    internal string SchemaVersion { get; }
    internal int SessionGeneration { get; }
    internal string SnapshotToken { get; }
    internal double? CapturedAtGameTime { get; }
    internal IdentitySnapshot SourceIdentity { get; }
    internal IdentitySnapshot TargetIdentity { get; }
    internal string SourceHeroId { get; }
    internal string TargetHeroId { get; }
    internal string SourceClanId { get; }
    internal string TargetClanId { get; }
    internal string SourceClanLeaderId { get; }
    internal string TargetClanLeaderId { get; }
    internal int? BaseRelation { get; }
    internal int? EffectiveRelation { get; }
    internal NativeFlagState NativeFriendState { get; }
    internal NativeFlagState NativeEnemyState { get; }
    internal NativeFlagState NativeNeutralState { get; }
    internal int? EffectiveClanRelation { get; }
    internal int? WeightedClanRelation { get; }
    internal NativeFlagState SameClan { get; }
    internal NativeFlagState SameKingdom { get; }
    internal IReadOnlyList<FamilyLinkSnapshot> FamilyLinks { get; }

    internal static NativeSocialSnapshot Capture(NativeSocialSnapshotInput input)
    {
        NativeSocialSnapshotInput source = input ?? new NativeSocialSnapshotInput();
        IdentitySnapshot sourceIdentity = IdentitySnapshot.Capture(source.SourceIdentity);
        IdentitySnapshot targetIdentity = IdentitySnapshot.Capture(source.TargetIdentity);
        List<FamilyLinkSnapshot> links = new List<FamilyLinkSnapshot>();
        foreach (FamilyLinkSnapshotInput link in source.FamilyLinks ?? new List<FamilyLinkSnapshotInput>())
        {
            FamilyLinkSnapshot captured = FamilyLinkSnapshot.Capture(link);
            if (captured != null) links.Add(captured);
        }
        return new NativeSocialSnapshot(source, sourceIdentity, targetIdentity, links.AsReadOnly());
    }
}
