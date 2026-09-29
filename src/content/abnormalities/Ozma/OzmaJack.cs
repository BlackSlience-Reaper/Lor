using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using LibraryOfRuina.powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.Ozma;

public enum JackDirection
{
    None = 0,
    East = 1,
    South = 2,
    West = 3,
    North = 4
}

public sealed class OzmaJack : LorMonsterModel
{
    private const string HiddenMoveId = "UNKNOWN";

    internal const string TextureRoot = "res://images/monsters/ozma/";
    public const string DormantTexturePath = TextureRoot + "jack_dormant.png";
    public const string AwakeTexturePath = TextureRoot + "jack_awake.png";
    public const string HitTexturePath = TextureRoot + "jack_hit.png";

    public static readonly string[] AssetPathsStatic =
        OzmaJackCreatureVisuals.Profile.AssetPaths
        .Concat(
        [
        "res://images/powers/ozma_east_jack_power.png",
        "res://images/powers/ozma_south_jack_power.png",
        "res://images/powers/ozma_west_jack_power.png",
        "res://images/powers/ozma_north_jack_power.png",
        "res://images/powers/ozma_which_is_real_power.png",
        "res://images/powers/ozma_take_or_be_taken_power.png"
        ])
        .ToArray();

    public override int MinInitialHp => 250;

    public override int MaxInitialHp => 250;

    public override int DefaultChaoResistance => 250;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => ResistAll();

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => ResistAll();

    public override IEnumerable<string> AssetPaths => AssetPathsStatic;

    public bool IsAwake { get; private set; }

    public JackDirection Direction => Creature?.SlotName switch
    {
        OzmaElite.EastJackSlot => JackDirection.East,
        OzmaElite.SouthJackSlot => JackDirection.South,
        OzmaElite.WestJackSlot => JackDirection.West,
        OzmaElite.NorthJackSlot => JackDirection.North,
        _ => JackDirection.None
    };

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmdCompat.Apply<MinionPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<OzmaWhichIsRealPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<OzmaTakeOrBeTakenPower>(Creature, 1m, Creature, null, silent: true);
        await ApplyDirectionPower();

        if (IsAwake)
        {
            await ApplyAwakeState();
        }
        else
        {
            await PowerCmdCompat.Ensure<UntargetablePower>(Creature);
            UpdateVisualState(false);
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState hidden = new(HiddenMoveId, _ => Task.CompletedTask, new HiddenIntent());
        hidden.FollowUpState = hidden;
        return new MonsterMoveStateMachine([hidden], hidden);
    }

    public async Task Wake()
    {
        if (Creature.IsDead || IsAwake)
        {
            return;
        }

        IsAwake = true;
        await PowerCmdCompat.RemoveIfPresent<
            UntargetablePower>(Creature);

        await ApplyAwakeState();
    }

    public override async Task BeforeDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        await base.BeforeDamageReceived(choiceContext, target, amount, props, dealer, cardSource);
        if (target != Creature
            || !IsAwake
            || dealer?.Player == null
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        await ApplyDirectionRetaliation(dealer);

        // 真杰克击中进度由全体玩家共享，任意玩家命中都交由奥兹玛结算。
        Ozma? boss = OzmaEncounterHelper.FindBoss(Creature.CombatState);
        if (boss != null)
        {
            await boss.RegisterTrueJackHit(choiceContext, dealer, this);
        }
    }

    private async Task ApplyDirectionPower()
    {
        switch (Direction)
        {
            case JackDirection.East:
                await PowerCmdCompat.Apply<OzmaEastJackPower>(Creature, 1m, Creature, null, silent: true);
                break;
            case JackDirection.South:
                await PowerCmdCompat.Apply<OzmaSouthJackPower>(Creature, 1m, Creature, null, silent: true);
                break;
            case JackDirection.West:
                await PowerCmdCompat.Apply<OzmaWestJackPower>(Creature, 1m, Creature, null, silent: true);
                break;
            case JackDirection.North:
                await PowerCmdCompat.Apply<OzmaNorthJackPower>(Creature, 1m, Creature, null, silent: true);
                break;
        }
    }

    private async Task ApplyAwakeState()
    {
        UpdateVisualState(true);
        await CreatureCmd.TriggerAnim(Creature, "Awake", 0f);
        if (Direction == JackDirection.East)
        {
            await PowerCmdCompat.Ensure<ThornsPower>(Creature, 6m);
        }
    }

    private async Task ApplyDirectionRetaliation(Creature dealer)
    {
        switch (Direction)
        {
            case JackDirection.South:
                await PowerCmdCompat.Apply<StrengthPower>(dealer, -1m, Creature, null);
                break;
            case JackDirection.West:
                await PowerCmdCompat.Apply<DexterityPower>(dealer, -1m, Creature, null);
                break;
            case JackDirection.North when dealer.Player != null:
                await PlayerCmd.LoseEnergy(1m, dealer.Player);
                break;
        }
    }

    private void UpdateVisualState(bool awake)
    {
        if (NCombatRoom.Instance?.GetCreatureNode(Creature)?.Visuals
            is OzmaJackCreatureVisuals visuals)
        {
            visuals.SetAwake(awake);
        }
    }

    private static LibraryCreatureResistanceData.Resistance ResistAll() => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };
}
