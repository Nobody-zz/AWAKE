using System;
using System.Collections.Generic;

namespace MarcusAwakeFramework.Api
{
    internal static class ContractGuard
    {
        public static string Id(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A stable identifier is required.", name);
            return value.Trim();
        }
    }

    public sealed class ApiVersion : IComparable<ApiVersion>, IEquatable<ApiVersion>
    {
        public ApiVersion(int major, int minor)
        {
            if (major < 0 || minor < 0) throw new ArgumentOutOfRangeException();
            Major = major;
            Minor = minor;
        }

        public int Major { get; }
        public int Minor { get; }

        public int CompareTo(ApiVersion other)
        {
            if (other == null) return 1;
            int major = Major.CompareTo(other.Major);
            return major != 0 ? major : Minor.CompareTo(other.Minor);
        }

        public bool Equals(ApiVersion other) => other != null && Major == other.Major && Minor == other.Minor;
        public override bool Equals(object obj) => Equals(obj as ApiVersion);
        public override int GetHashCode() => (Major * 397) ^ Minor;
        public override string ToString() => Major + "." + Minor;
    }

    public sealed class ExtensionId : IEquatable<ExtensionId>
    {
        public ExtensionId(string value) { Value = ContractGuard.Id(value, nameof(value)); }
        public string Value { get; }
        public bool Equals(ExtensionId other) => other != null && StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object obj) => Equals(obj as ExtensionId);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public sealed class CapabilityId : IEquatable<CapabilityId>
    {
        public CapabilityId(string value) { Value = ContractGuard.Id(value, nameof(value)); }
        public string Value { get; }
        public bool Equals(CapabilityId other) => other != null && StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object obj) => Equals(obj as CapabilityId);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public sealed class SessionRef : IEquatable<SessionRef>
    {
        public SessionRef(string campaignGuid, string timelineId, string sessionId)
        {
            CampaignGuid = campaignGuid ?? string.Empty;
            TimelineId = timelineId ?? string.Empty;
            SessionId = sessionId ?? string.Empty;
        }

        public string CampaignGuid { get; }
        public string CampaignId => CampaignGuid;
        public string TimelineId { get; }
        public string SessionId { get; }
        public bool IsCampaign => CampaignGuid.Length > 0 && TimelineId.Length > 0 && SessionId.Length > 0;
        public bool Equals(SessionRef other) => other != null
            && StringComparer.Ordinal.Equals(CampaignGuid, other.CampaignGuid)
            && StringComparer.Ordinal.Equals(TimelineId, other.TimelineId)
            && StringComparer.Ordinal.Equals(SessionId, other.SessionId);
        public override bool Equals(object obj) => Equals(obj as SessionRef);
        public override int GetHashCode() => (((StringComparer.Ordinal.GetHashCode(CampaignGuid) * 397) ^ StringComparer.Ordinal.GetHashCode(TimelineId)) * 397) ^ StringComparer.Ordinal.GetHashCode(SessionId);
        public override string ToString() => CampaignGuid + "/" + TimelineId + "/" + SessionId;
    }

    public sealed class EntityRef : IEquatable<EntityRef>
    {
        public EntityRef(string entityType, string stableId)
            : this("awake", entityType, stableId, null, null)
        {
        }

        public EntityRef(string entityNamespace, string entityType, string stableId, string campaignId = null, string displayHint = null)
        {
            Namespace = ContractGuard.Id(entityNamespace, nameof(entityNamespace));
            EntityType = ContractGuard.Id(entityType, nameof(entityType));
            StableId = ContractGuard.Id(stableId, nameof(stableId));
            CampaignId = campaignId;
            DisplayHint = displayHint;
        }

        public string Namespace { get; }
        public string EntityType { get; }
        public string StableId { get; }
        public string CampaignId { get; }
        public string DisplayHint { get; }
        public bool Equals(EntityRef other) => other != null
            && StringComparer.Ordinal.Equals(Namespace, other.Namespace)
            && StringComparer.Ordinal.Equals(EntityType, other.EntityType)
            && StringComparer.Ordinal.Equals(StableId, other.StableId)
            && StringComparer.Ordinal.Equals(CampaignId, other.CampaignId);
        public override bool Equals(object obj) => Equals(obj as EntityRef);
        public override int GetHashCode() => (((StringComparer.Ordinal.GetHashCode(Namespace) * 397) ^ StringComparer.Ordinal.GetHashCode(EntityType)) * 397) ^ StringComparer.Ordinal.GetHashCode(StableId);
        public override string ToString() => Namespace + ":" + EntityType + ":" + StableId;
    }

    public sealed class SnapshotToken
    {
        public SnapshotToken(string value, DateTimeOffset expiresAt)
        {
            Value = ContractGuard.Id(value, nameof(value));
            ExpiresAt = expiresAt;
        }

        public string Value { get; }
        public DateTimeOffset ExpiresAt { get; }
        public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
    }

    public sealed class SchemaRef
    {
        public SchemaRef(string schemaId, ApiVersion version)
        {
            Id = ContractGuard.Id(schemaId, nameof(schemaId));
            Version = version ?? throw new ArgumentNullException(nameof(version));
        }

        public SchemaRef(string id, int major, int minor)
            : this(id, new ApiVersion(major, minor))
        {
        }

        public string Id { get; }
        public string SchemaId => Id;
        public int Major => Version.Major;
        public int Minor => Version.Minor;
        public ApiVersion Version { get; }
        public override string ToString() => Id + "/v" + Major + "." + Minor;
    }

    public sealed class PageRequest
    {
        public PageRequest(int limit, string cursor = null)
        {
            if (limit < 1 || limit > 256) throw new ArgumentOutOfRangeException(nameof(limit));
            Limit = limit;
            Cursor = cursor;
        }

        public int Limit { get; }
        public string Cursor { get; }
    }

    public sealed class Page<T>
    {
        public Page(IReadOnlyList<T> items, string nextCursor, SnapshotToken snapshot)
        {
            Items = items ?? throw new ArgumentNullException(nameof(items));
            NextCursor = nextCursor;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            SnapshotToken = snapshot.Value;
        }

        public Page(IReadOnlyList<T> items, string nextCursor, string snapshotToken)
        {
            Items = items ?? new T[0];
            NextCursor = nextCursor;
            SnapshotToken = snapshotToken ?? string.Empty;
            Snapshot = string.IsNullOrWhiteSpace(SnapshotToken)
                ? new SnapshotToken("snapshot-empty", DateTimeOffset.MaxValue)
                : new SnapshotToken(SnapshotToken, DateTimeOffset.MaxValue);
        }

        public IReadOnlyList<T> Items { get; }
        public string NextCursor { get; }
        public SnapshotToken Snapshot { get; }
        public string SnapshotToken { get; }
    }
}