using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.monsters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using PileType = MegaCrit.Sts2.Core.Entities.Cards.PileType;

namespace LibraryOfRuina.content.liberation.Art;

public enum ArtFloorDaCapoPerformerVariant
{
    First = 1,
    Second = 2,
    Third = 3,
    Fourth = 4
}

public sealed class ArtFloorDaCapoPerformer : LorMonsterModel
{
    private const string LogTag = "LibraryOfRuina.ArtFloorDaCapoPerformer";
    private const string PerformMoveId = "PERFORM";
    private const string HiddenMoveId = "SILENT_CLIMAX";

    public const string Root = ArtFloorAssets.DacapoPerformersMonsterRoot;

    private ArtFloorDaCapoPerformerVariant _variant = ArtFloorDaCapoPerformerVariant.First;
    private bool _showUnknownIntent;
    private MoveState? _performState;
    private MoveState? _hiddenState;

    public override int DefaultChaoResistance => 100;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 150, 90);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 160, 93);

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Immune,
        Blunt = LibraryResistanceLevel.Immune
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LocString Title
    {
        get
        {
            return _variant switch
            {
                ArtFloorDaCapoPerformerVariant.First => L10NMonsterLookup(
                    "ART_FLOOR_DA_CAPO_PERFORMER.variant1.name"),
                ArtFloorDaCapoPerformerVariant.Second => L10NMonsterLookup(
                    "ART_FLOOR_DA_CAPO_PERFORMER.variant2.name"),
                ArtFloorDaCapoPerformerVariant.Third => L10NMonsterLookup(
                    "ART_FLOOR_DA_CAPO_PERFORMER.variant3.name"),
                ArtFloorDaCapoPerformerVariant.Fourth => L10NMonsterLookup(
                    "ART_FLOOR_DA_CAPO_PERFORMER.variant4.name"),
                _ => L10NMonsterLookup("ART_FLOOR_DA_CAPO_PERFORMER.variant.name")
            };
        }
    }

    public string IdleTexturePath => Root + $"performer_{(int)_variant}_idle.png";

    public string AttackTexturePath => Root + $"performer_{(int)_variant}_attack.png";

    public string HitTexturePath => Root + $"performer_{(int)_variant}_hit.png";

    public string GuardTexturePath => Root + $"performer_{(int)_variant}_guard.png";

    public override IEnumerable<string> AssetPaths =>
        ArtFloorDaCapoPerformerCreatureVisuals.Profile.AssetPaths.Concat(
        [
            ImageHelper.GetImagePath(
                "powers/art_floor_final_da_capo_performer_passive_power.png"),
            ImageHelper.GetImagePath(
                "powers/art_floor_da_capo_soul_binding_power.png")
        ]);

    internal void ConfigureVariant(ArtFloorDaCapoPerformerVariant variant)
    {
        AssertMutable();
        _variant = variant;
    }

    internal bool ConfigureHiddenIntent(bool showUnknownIntent)
    {
        AssertMutable();
        _showUnknownIntent = showUnknownIntent;
        return ApplyHiddenIntentState();
    }

    protected override string VisualsPath => SceneHelper.GetScenePath("creature_visuals/" + Id.Entry.ToLowerInvariant());

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<MinionPower>(Creature, 1m, Creature, null, silent: true);
        await EnsurePerformerPassivePower(Creature);
    }

    internal static async Task EnsurePerformerPassivePower(Creature creature)
    {
        if (creature.Monster is not ArtFloorDaCapoPerformer
            || creature.HasPower<ArtFloorFinalDaCapoPerformerPassivePower>())
        {
            return;
        }

        try
        {
            await PowerCmdCompat.Apply<ArtFloorFinalDaCapoPerformerPassivePower>(
                creature,
                1m,
                creature,
                null,
                silent: true);
        }
        catch (Exception exception)
        {
            string context = DescribePassiveContext(creature);
            Log.Warn(
                "[" + LogTag + "] failed to apply performer passive context="
                + context
                + " exception="
                + exception.GetType().Name
                + ": "
                + exception.Message);
            throw new InvalidOperationException("Failed to apply Da Capo performer passive. context=" + context, exception);
        }
    }

    private static string DescribePassiveContext(Creature creature)
    {
        string combatState = creature.CombatState == null
            ? "none"
            : "encounter=" + (creature.CombatState.Encounter?.Id.Entry ?? "none")
              + ",round=" + creature.CombatState.RoundNumber
              + ",side=" + creature.CombatState.CurrentSide;
        return "creature=" + (creature.Name ?? "none")
            + ",monster=" + (creature.Monster?.Id.Entry ?? "none")
            + ",isAlive=" + creature.IsAlive
            + ",isDead=" + creature.IsDead
            + ",hasPassive=" + creature.HasPower<ArtFloorFinalDaCapoPerformerPassivePower>()
            + "," + combatState;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _performState = new MoveState(
            PerformMoveId,
            PerformMove,
            CreateVisibleIntent());

        _hiddenState = new MoveState(
            HiddenMoveId,
            PerformMove,
            new UnknownIntent());

        ConditionalBranchState chooser = new("DACAPO_PERFORMER_ROUTER");
        chooser.AddState(_hiddenState, () => _showUnknownIntent);
        chooser.AddState(_performState, () => true);

        _performState.FollowUpState = chooser;
        _hiddenState.FollowUpState = chooser;

        return new MonsterMoveStateMachine([_performState, _hiddenState, chooser], chooser);
    }

    private bool ApplyHiddenIntentState()
    {
        if (MoveStateMachine == null
            || Creature.IsStunned
            || Creature is LibraryCreature { IsChaoed: true })
        {
            return false;
        }

        MoveState? targetState = _showUnknownIntent ? _hiddenState : _performState;
        if (targetState != null)
        {
            SetMoveImmediate(targetState, forceTransition: true);
            return true;
        }

        return false;
    }

    private AbstractIntent CreateVisibleIntent()
    {
        return _variant switch
        {
            ArtFloorDaCapoPerformerVariant.First => new DefendIntent(),
            ArtFloorDaCapoPerformerVariant.Second => new DebuffIntent(),
            ArtFloorDaCapoPerformerVariant.Third => new StatusIntent(4),
            _ => new CardDebuffIntent()
        };
    }

    private async Task PerformMove(IReadOnlyList<Creature> targets)
    {
        if (CombatState?.Encounter is not ArtFloorLiberationEncounter encounter)
        {
            return;
        }

        if (encounter.GetCurrentArtFloorDaCapo() is not ArtFloorFinalDaCapoBoss daCapo || daCapo.Creature.IsDead)
        {
            return;
        }

        switch (_variant)
        {
            case ArtFloorDaCapoPerformerVariant.First:
                await CreatureCmd.TriggerAnim(Creature, "Guard", 0.2f);
                await CreatureCmd.GainBlock(daCapo.Creature, 15m, ValueProp.Move, null);
                break;

            case ArtFloorDaCapoPerformerVariant.Second:
                await CreatureCmd.TriggerAnim(Creature, "Attack", 0.2f);
                await PowerCmdCompat.ApplyDebuff<RingingPower>(
                    targets.Where(static target => target.IsAlive),
                    1m,
                    Creature,
                    null);
                break;

            case ArtFloorDaCapoPerformerVariant.Third:
                await CreatureCmd.TriggerAnim(Creature, "Attack", 0.2f);
                foreach (Creature target in targets.Where(static target => target.IsAlive))
                {
                    await CardPileCmdCompat.AddToCombatAndPreview<Dazed>(
                        target,
                        PileType.Discard,
                        4,
                        addedByPlayer: false);
                }
                break;

            case ArtFloorDaCapoPerformerVariant.Fourth:
                await CreatureCmd.TriggerAnim(Creature, "Attack", 0.2f);
                await PowerCmdCompat.ApplyDebuff<ChainsOfBindingPower>(
                    targets.Where(static target => target.IsAlive),
                    1m,
                    Creature,
                    null);
                break;
        }
    }
}
