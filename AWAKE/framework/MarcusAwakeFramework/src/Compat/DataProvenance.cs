using System;

namespace MarcusAwakeFramework.Api;

public sealed class DataProvenance
{
	public DataAccessScope AccessScope { get; }

	public SourceClass SourceClass { get; }

	public EpistemicStatus EpistemicStatus { get; }

	public DateTimeOffset ObservedAtUtc { get; }

	public string SnapshotToken { get; }

	public DataProvenance(DataAccessScope accessScope, SourceClass sourceClass, EpistemicStatus epistemicStatus, DateTimeOffset observedAtUtc, string snapshotToken)
	{
		AccessScope = accessScope;
		SourceClass = sourceClass;
		EpistemicStatus = epistemicStatus;
		ObservedAtUtc = observedAtUtc;
		SnapshotToken = snapshotToken;
	}
}
