using System.Threading.Tasks;
using LibraryOfRuina.content.abnormalities.Nosferatu;
using LibraryOfRuina.core.compat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace LibraryOfRuina.content.liberation.Language;

public sealed class LanguageFloorBloodBat : NosferatuBloodBatBase
{
    public const int MaxHp = 90;
    public const int MaxChaoResistance = 70;
    public const int HydrophobiaBloodThreshold = 1;
    public const int HydrophobiaStrong = 2;

    public int LastHydrophobiaRound { get; private set; } = -1;

    protected override string RightBatSlotName =>
        LanguageFloorLiberationEncounter.DipsiaRightBatSlot;

    public override int MinInitialHp => MaxHp;

    public override int MaxInitialHp => MaxHp;

    public override int DefaultChaoResistance => MaxChaoResistance;

    protected override Task ApplyHydrophobiaPassive() =>
        PowerCmdCompat.Apply<LanguageFloorDipsiaHydrophobiaPassivePower>(
            Creature,
            1m,
            Creature,
            null,
            silent: true);

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        int round = Creature.CombatState?.RoundNumber ?? int.MinValue;
        if (side == CombatSide.Player
            && Creature.IsAlive
            && LastHydrophobiaRound != round
            && NosferatuBloodPower.GetStacks(Creature)
                <= HydrophobiaBloodThreshold)
        {
            LastHydrophobiaRound = round;
            await LibraryPowerCmd.Apply<LibraryStrongPower>(
                new ThrowingPlayerChoiceContext(),
                Creature,
                HydrophobiaStrong,
                0,
                IsPermanent: false,
                Creature,
                null);
        }

        await base.BeforeSideTurnStart(
            choiceContext,
            side,
            participants,
            combatState);
    }
}
