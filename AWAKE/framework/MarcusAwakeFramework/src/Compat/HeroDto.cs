namespace MarcusAwakeFramework.Api;

public sealed class HeroDto
{
	public EntityRef Id { get; }

	public string Name { get; }

	public EntityRef Clan { get; }

	public EntityRef Kingdom { get; }

	public bool IsAlive { get; }

	public bool IsPlayer { get; }

	public int Level { get; }

	public DataProvenance Provenance { get; }

	public HeroDto(EntityRef id, string name, EntityRef clan, EntityRef kingdom, bool isAlive, bool isPlayer, int level, DataProvenance provenance)
	{
		Id = id;
		Name = name ?? string.Empty;
		Clan = clan;
		Kingdom = kingdom;
		IsAlive = isAlive;
		IsPlayer = isPlayer;
		Level = level;
		Provenance = provenance;
	}
}
