using System;
using System.Collections.Generic;

namespace MarcusAwakeFramework.Api;

public sealed class HealthSnapshot
{
	public DateTimeOffset ObservedAtUtc { get; }

	public IReadOnlyList<HealthComponent> Components { get; }

	public HealthSnapshot(DateTimeOffset observedAtUtc, IReadOnlyList<HealthComponent> components)
	{
		ObservedAtUtc = observedAtUtc;
		Components = components ?? Array.Empty<HealthComponent>();
	}
}
