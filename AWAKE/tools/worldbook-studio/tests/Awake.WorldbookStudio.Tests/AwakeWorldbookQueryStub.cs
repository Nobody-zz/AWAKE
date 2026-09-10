using System;
using System.Collections.Generic;

namespace Awake;

internal sealed class WorldbookQuery
{
    internal string HeroId { get; set; } = string.Empty;
    internal string CharacterId { get; set; } = string.Empty;
    internal string IdentityId { get; set; } = string.Empty;
    internal string CultureId { get; set; } = string.Empty;
    internal string KingdomId { get; set; } = string.Empty;
    internal string SettlementId { get; set; } = string.Empty;
    internal string Role { get; set; } = string.Empty;
    internal bool? IsFemale { get; set; }
    internal int Age { get; set; }
    internal bool IsClanLeader { get; set; }
    internal Dictionary<string, int> Skills { get; set; } = new(StringComparer.Ordinal);
    internal string ContentTier { get; set; } = "pure";
    internal string KnowledgeScope { get; set; } = string.Empty;
    internal bool KnowledgeScopeAvailable { get; set; }
    internal string EffectiveDetail { get; set; } = string.Empty;
    internal bool EffectiveDetailAvailable { get; set; }
    internal string RequestedDetail { get; set; } = "secret";
    internal string PlayerText { get; set; } = string.Empty;
    internal int MaximumBytes { get; set; } = 4096;
}
