using System;

namespace MarcusAwakeFramework.Api;

public sealed class CommandPreflight
{
	public string RequestId { get; }

	public CommandRiskTier Risk { get; }

	public string Summary { get; }

	public string SnapshotToken { get; }

	public DateTimeOffset ExpiresUtc { get; }

	public bool RequiresApproval { get; }

	public CommandPreflight(string requestId, CommandRiskTier risk, string summary, string snapshotToken, DateTimeOffset expiresUtc, bool requiresApproval)
	{
		RequestId = requestId;
		Risk = risk;
		Summary = summary ?? string.Empty;
		SnapshotToken = snapshotToken;
		ExpiresUtc = expiresUtc;
		RequiresApproval = requiresApproval;
	}
}
