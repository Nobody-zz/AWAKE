namespace MarcusAwakeFramework.Api;

public sealed class PlayerSnapshotDto
{
	public HeroDto Hero { get; }

	public ClanDto Clan { get; }

	public KingdomDto Kingdom { get; }

	public string SnapshotToken { get; }

	public PlayerSnapshotDto(HeroDto hero, ClanDto clan, KingdomDto kingdom, string snapshotToken)
	{
		Hero = hero;
		Clan = clan;
		Kingdom = kingdom;
		SnapshotToken = snapshotToken;
	}
}
