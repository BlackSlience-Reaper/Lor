using MegaCrit.Sts2.Core.Entities.Powers;

namespace LibraryOfRuina.powers.PunishingBird;

public sealed class PunishingBirdCageChainsPower : PunishingBirdBasePower
{
    public const int InitialChains = 3;

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override string? LegacyPowerId => "PUNISHING_BIRD_CAGE_CHAINS_POWER";

    protected override string IconFileName => "punishing_bird_cage_chains.png";
}
