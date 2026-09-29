using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Language;

public sealed class LanguageFloorSmilingFaceFindCorpsesPower
    : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "LANGUAGE_FLOOR_SMILING_FACE_FIND_CORPSES_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HealPercentFirstSecond", 40),
        new DynamicVar("HealPercentThird", 30)
    ];

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        return dealer == Owner
            && result.WasTargetKilled
            && target.Side == Owner.Side
            && target != Owner
            && Owner.Monster is LanguageFloorSmilingFace boss
                ? boss.OnAllyKilled()
                : Task.CompletedTask;
    }
}

public abstract class LanguageFloorSmilingFaceFakeDeathPower
    : LibraryFakeDeathPowerModel
{
    protected override bool IsOwnerFakeDead =>
        Owner.Monster is LanguageFloorSmilingFace { IsFakeDead: true };

    protected override bool CanEnterFakeDeath(Creature creature) =>
        Owner.Monster is LanguageFloorSmilingFace boss
        && boss.CanEnterFakeDeath(creature);

    protected override Task EnterFakeDeath(Creature creature) =>
        Owner.Monster is LanguageFloorSmilingFace boss
            ? boss.EnterFakeDeathFromDeath()
            : Task.CompletedTask;

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        return side == CombatSide.Player
            && Owner.Monster is LanguageFloorSmilingFace boss
            && (boss.CorpseTrialPending || boss.CorpseTrialActive)
                ? boss.TickCorpseTrialOnPlayerTurnStart(combatState)
                : Task.CompletedTask;
    }
}

public sealed class LanguageFloorSmilingFaceFormOneSplitPower
    : LanguageFloorSmilingFaceFakeDeathPower
{
    protected override string LegacyPowerId =>
        "LANGUAGE_FLOOR_SMILING_FACE_FORM_ONE_SPLIT_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("CorpseCount", LanguageFloorSmilingFace.TrialCorpseCount),
        new DynamicVar("PlayerTurns", LanguageFloorSmilingFace.TrialPlayerTurns)
    ];
}

public sealed class LanguageFloorSmilingFaceFusionPower
    : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "LANGUAGE_FLOOR_SMILING_FACE_FUSION_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class LanguageFloorSmilingFaceSplitAndFusionPower
    : LanguageFloorSmilingFaceFakeDeathPower
{
    protected override string LegacyPowerId =>
        "LANGUAGE_FLOOR_SMILING_FACE_SPLIT_AND_FUSION_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class LanguageFloorSmilingFaceScreamPower
    : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "LANGUAGE_FLOOR_SMILING_FACE_SCREAM_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<LibraryStrongPower>()];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<LibraryStrongPower>(
            "Strong",
            LanguageFloorSmilingFace.PermanentStrongPerPlayerTurn)];
}

public sealed class LanguageFloorSmilingFaceFormThreeSplitPower
    : LanguageFloorSmilingFaceFakeDeathPower
{
    protected override string LegacyPowerId =>
        "LANGUAGE_FLOOR_SMILING_FACE_FORM_THREE_SPLIT_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class LanguageFloorSmilingFaceVomitPower
    : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "LANGUAGE_FLOOR_SMILING_FACE_VOMIT_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<LibraryStrongPower>()];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<LibraryStrongPower>(
            "Strong",
            LanguageFloorSmilingFace.PermanentStrongPerPlayerTurn)];
}

public sealed class LanguageFloorMeltingCorpseRotPower
    : LibraryOfRuinaPowerModel
{
    public const int RetaliationDamage = 8;
    public const int VulnerableAmount = 1;
    public const int VulnerableTurns = 1;

    protected override string LegacyPowerId =>
        "LANGUAGE_FLOOR_MELTING_CORPSE_ROT_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<LibraryVulnerablePower>()];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Damage", RetaliationDamage),
        new PowerVar<LibraryVulnerablePower>("Vulnerable", VulnerableAmount),
        new DynamicVar("Turns", VulnerableTurns)
    ];

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        {
            if (target != Owner
                || dealer is not { IsPlayer: true, IsAlive: true }
                || !ValuePropCompat.IsPoweredAttack(props))
            {
                return;
            }

            Flash();
            await CreatureCmd.Damage(
                choiceContext,
                dealer,
                RetaliationDamage,
                ValueProp.Unpowered | ValueProp.SkipHurtAnim,
                Owner,
                null,
                null);
            if (dealer.IsAlive)
            {
                LibraryVulnerablePower? existing = dealer
                    .GetPowerInstances<LibraryVulnerablePower>()
                    .FirstOrDefault(static power => power.TurnsRemaining > 0);
                if (existing == null)
                {
                    await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                        new ThrowingPlayerChoiceContext(),
                        dealer,
                        VulnerableAmount,
                        VulnerableTurns - 1,
                        IsPermanent: false,
                        Owner,
                        null);
                }
                else
                {
                    await LibraryPowerCmd.ModifyAmount(
                        choiceContext,
                        existing,
                        VulnerableAmount,
                        VulnerableTurns - 1,
                        IsPermanent: false,
                        Owner,
                        null);
                }
            }
        }
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (!wasRemovalPrevented
            && creature == Owner
            && Owner.CombatState?.Enemies
                .Select(static enemy => enemy.Monster)
                .OfType<LanguageFloorSmilingFace>()
                .FirstOrDefault() is { } boss)
        {
            await boss.OnCorpseDeath();
        }
    }
}
