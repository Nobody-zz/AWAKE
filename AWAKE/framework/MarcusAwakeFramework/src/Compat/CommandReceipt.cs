namespace MarcusAwakeFramework.Api;

public sealed class CommandReceipt
{
	public string RequestId { get; }

	public CommandState State { get; }

	public string Summary { get; }

	public string ResultEventId { get; }

	public CommandReceipt(string requestId, CommandState state, string summary, string resultEventId)
	{
		RequestId = requestId;
		State = state;
		Summary = summary ?? string.Empty;
		ResultEventId = resultEventId;
	}
}
