namespace MarcusAwakeFramework.Api;

public sealed class HealthComponent
{
	public string Id { get; }

	public HealthLevel Level { get; }

	public string Code { get; }

	public string Summary { get; }

	public HealthComponent(string id, HealthLevel level, string code, string summary)
	{
		Id = id ?? string.Empty;
		Level = level;
		Code = code ?? string.Empty;
		Summary = summary ?? string.Empty;
	}
}
