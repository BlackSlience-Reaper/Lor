using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.intents;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Natural;

public sealed class NaturalFloorGreenStemHermit : NaturalFloorWrathMonster
{
    private static readonly string[] Ids = ["GET_AWAY_AND_HUFF", "STAY_PUT_AND_HUFF", "MY_FRIEND", "RISE_UP"];

    protected override string[] MoveIds => Ids;

    public const int GetAwayBlock = 11; // 起开：自身格挡。

    public const int GroupBlock = 12; // 呼呼呼：群体格挡。

    public const int GroupStrong = 2; // 呼呼呼：群体永久强壮层数。

    public const int StaffHealPercent = 10; // 老实待着：隐士之杖体力及混乱恢复百分比。

    public const int DazedCount = 5; // 老实待着：向每名玩家弃牌堆加入的晕眩张数。

    [SavedProperty]
    public int StaffStep { get; private set; }

    [SavedProperty]
    public int NoStaffStep { get; private set; }

    [SavedProperty]
    public bool Staffless { get; private set; }

    public override int MinInitialHp =>
        HpValue(
            310, // 初始体力下限：普通。
            340); // 初始体力下限：ToughEnemies 进阶。

    public override int MaxInitialHp =>
        HpValue(
            320, // 初始体力上限：普通。
            350); // 初始体力上限：ToughEnemies 进阶。

    public override int DefaultChaoResistance => 150; // 初始混乱抗性。

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Endure,
        Blunt = LibraryResistanceLevel.Resist
    };

    protected override IEnumerable<string> VisualAssets => NaturalFloorHermitVisuals.AssetPaths;

    private static int GetAwayAndHuffDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            10, // 起开、呼呼呼……：DeadlyEnemies 进阶单次伤害。
            8); // 起开、呼呼呼……：普通单次伤害。

    private const int GetAwayAndHuffHits = 2; // 起开、呼呼呼……：攻击次数。

    private static int MyFriendDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            19, // 朋友啊……！：DeadlyEnemies 进阶单次伤害。
            18); // 朋友啊……！：普通单次伤害。

    private const int MyFriendHits = 1; // 朋友啊……！：攻击次数。

    private static int RiseUpDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            29, // 起来吧！：DeadlyEnemies 进阶单次伤害。
            28); // 起来吧！：普通单次伤害。

    private const int RiseUpHits = 1; // 起来吧！：攻击次数。

    private static int Damage(int move) =>
        move switch
        {
            0 => GetAwayAndHuffDamage,
            2 => MyFriendDamage,
            3 => RiseUpDamage,
            _ => RiseUpDamage
        };

    private static int Hits(int move) =>
        move switch
        {
            0 => GetAwayAndHuffHits,
            2 => MyFriendHits,
            3 => RiseUpHits,
            _ => RiseUpHits
        };

    protected override IEnumerable<AbstractIntent> CreateIntents(int move)
    {
        var huff = new CombinedDefendBuffIntent(GroupBlock, "NATURAL_HERMIT_HUFF.description", IntentBadge.FromPower<LibraryStrongPower>(GroupStrong));
        return move switch
        {
            0 => [new CombinedTargetedAttackDefendIntent(
                () => Damage(move),
                () => Hits(move),
                "NATURAL_HERMIT_GET_AWAY_TARGETED.description",
                "NATURAL_HERMIT_GET_AWAY.description",
                true,
                GetAwayBlock)
            {
                TargetLineResolver = GetTargetedAttackTargets
            }, huff],
            1 => [new NaturalFloorStaffRecoveryIntent(), new DetailedStatusCardIntent<Dazed>(DazedCount, PileType.Discard), huff],
            2 => [new CombinedTargetedAttackDebuffIntent(() => Damage(move), () => Hits(move), "NATURAL_HERMIT_MY_FRIEND.description",
                "NATURAL_HERMIT_MY_FRIEND.description", false, IntentBadge.Custom("atlases/intent_atlas.sprites/intent_stun.tres"))],
            _ => [new IndiscriminateAttackIntent(() => Damage(move), () => Hits(move), "NATURAL_HERMIT_RISE_UP.description",
                static owner => (owner.CombatState?.Encounter as NaturalFloorLiberationEncounter)?.HermitGroupTargets() ?? []),
            new SummonIntent()]
        };
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmdCompat.Ensure<NaturalFloorExploitedPower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Ensure<NaturalFloorDearFriendPower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Ensure<NaturalFloorTwoWorldsPower>(Creature, 1, Creature, null, silent: true);
    }

    internal void SynchronizeStaffMode(bool exists)
    {
        if (Staffless == !exists)
        {
            return;
        }

        Staffless = !exists;
        StaffStep = NoStaffStep = 0;
        // 手杖死亡时保留已展示的招式，仅通过目标契约刷新攻击目标。
        if (exists && !IsPerformingMove && NextMove.StateId != "STUNNED")
        {
            ForceMove(0);
        }
    }

    protected override string SelectMove(Rng rng) => Staffless ? Ids[2 + NoStaffStep] : Ids[StaffStep];

    protected override async Task PerformMove(int move)
    {
        if (!CanAct)
        {
            return;
        }

        if (move == 0)
        {
            await Attack(Damage(move), Hits(move), () => GetTargetedAttackTargets(Creature), ["AttackThrust"], ["hermit_attack"]);
            if (!CanAct)
            {
                return;
            }

            await CreatureCmd.GainBlock(Creature, GetAwayBlock, ValueProp.Move, null);
        }
        else if (move == 1)
        {
            Sound("meet");
            await CreatureCmd.TriggerAnim(Creature, "AttackReach", HitTime);
            foreach (Creature staff in Encounter!.Staffs())
            {
                await CreatureCmd.Heal(staff, Math.Ceiling(staff.MaxHp * StaffHealPercent / 100m));
                if (staff is LibraryCreature library)
                {
                    await LibraryCreatureCmd.HealChaoValue(library, Math.Ceiling(library.MaxChaoValue * StaffHealPercent / 100m));
                }
            }
            await CardPileCmdCompat.AddToCombatAndPreview<Dazed>(Encounter.LivingPlayers(), PileType.Discard, DazedCount, false);
        }
        else if (move == 2)
        {
            Creature? stagger = null;
            await Attack(Damage(move), Hits(move), () => GetTargetedAttackTargets(Creature), ["MentalAttack"], ["hermit_strong"],
                (target, loss) => { if (loss > 0 && target.Monster is NaturalFloorBlindRageBoss) { stagger = target; } });
            if (CanAct && stagger is LibraryCreature { IsAlive: true } library)
            {
                await LibraryCreatureCmd.SetCurrentChaoValue(library, 0);
            }

            NoStaffStep = 1;
        }
        else
        {
            await Attack(Damage(move), Hits(move), () => Encounter!.HermitGroupTargets(), ["AttackGround"], ["hermit_ground"]);
            if (CanAct)
            {
                await Encounter!.SummonStaffs();
            }

            NoStaffStep = 0;
        }
        if (move <= 1 && CanAct)
        {
            Sound("meet");
            await CreatureCmd.TriggerAnim(Creature, "AttackReach", HitTime);
            foreach (Creature monster in Encounter!.LivingMonsters())
            {
                if (monster.Monster is NaturalFloorBlindRageBoss)
                {
                    continue;
                }

                await CreatureCmd.GainBlock(monster, GroupBlock, ValueProp.Move, null);
                await LibraryPowerCmd.Apply<LibraryStrongPower>(monster, GroupStrong, -1, Creature, null);
            }
            if (!Staffless)
            {
                StaffStep = 1 - move;
            }
        }
    }

    public override IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner) =>
        Staffless && Encounter?.Rage is { IsAlive: true } rage ? [rage] : Encounter?.LivingPlayers() ?? [];

    internal override Dictionary<string, string> CaptureState()
    {
        var state = base.CaptureState();
        state["StaffStep"] = StaffStep.ToString();
        state["NoStaffStep"] = NoStaffStep.ToString();
        state["Staffless"] = Staffless.ToString();
        return state;
    }

    internal override void RestoreState(Dictionary<string, string> state)
    {
        base.RestoreState(state);
        StaffStep = ReadInt(state, "StaffStep");
        NoStaffStep = ReadInt(state, "NoStaffStep");
        Staffless = ReadBool(state, "Staffless");
    }
}
