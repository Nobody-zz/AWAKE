namespace MarcusAwakeFramework.Api;

public sealed class KingdomDto
{
	public EntityRef Id { get; }

	public string Name { get; }

	public EntityRef Leader { get; }

	public DataProvenance Provenance { get; }

	public KingdomDto(EntityRef id, string name, EntityRef leader, DataProvenance provenance)
	{
		Id = id;
		Name = name ?? string.Empty;
		Leader = leader;
		Provenance = provenance;
	}
}
