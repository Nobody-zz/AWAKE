namespace MarcusAwakeFramework.Api;

public sealed class ClanDto
{
	public EntityRef Id { get; }

	public string Name { get; }

	public EntityRef Leader { get; }

	public EntityRef Kingdom { get; }

	public int Tier { get; }

	public DataProvenance Provenance { get; }

	public ClanDto(EntityRef id, string name, EntityRef leader, EntityRef kingdom, int tier, DataProvenance provenance)
	{
		Id = id;
		Name = name ?? string.Empty;
		Leader = leader;
		Kingdom = kingdom;
		Tier = tier;
		Provenance = provenance;
	}
}
