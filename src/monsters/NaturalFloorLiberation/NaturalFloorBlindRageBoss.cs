using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers.NaturalFloorLiberation;
using LibraryOfRuina.powers.WrathServant;
using LibraryOfRuina.visuals.NaturalFloorLiberation;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.monsters.NaturalFloorLiberation;

public sealed class NaturalFloorBlindRageBoss : NaturalFloorWrathMonster, ILiberationPrimaryPhaseBoss
{
    private static readonly string[] Ids = ["UUUGH", "AAAH", "AAAAH", "EVIL_INCARNATION", "REVIVE_AND_EMPOWER"];

    public int LiberationPhase => 2;

    public Task TriggerReviveAndEmpowerState()
    {
        ForceReviveAndEmpowerState();
        return Task.CompletedTask;
    }

    public void ForceReviveAndEmpowerState() => ForceMove(4);

    protected override string[] MoveIds => Ids;

    public const int DamageThreshold = 80; // 罪人：累计损失体力达到此值后预约特殊招式。

    public const int StrongAmount = 2; // 啊啊啊啊！！：永久强壮层数。

    [SavedProperty]
    public int CompletedMoveMask { get; private set; }

    [SavedProperty]
    public int LastNormalMove { get; private set; }

    [SavedProperty]
    public int LostHp { get; private set; }

    [SavedProperty]
    public bool DamageSpecialPending { get; private set; }

    [SavedProperty]
    public bool Staffless { get; private set; }

    [SavedProperty]
    public int NormalMovesUntilSpecial { get; private set; }

    private bool _performingSpecial;

    public override int MinInitialHp =>
        HpValue(
            267, // 初始体力下限：普通。
            245); // 初始体力下限：ToughEnemies 进阶。

    public override int MaxInitialHp =>
        HpValue(
            270, // 初始体力上限：普通。
            250); // 初始体力上限：ToughEnemies 进阶。

    public override int DefaultChaoResistance => 120; // 初始混乱抗性。

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Fatal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => DefaultPhysicalResistanceData;

    protected override IEnumerable<string> VisualAssets => NaturalFloorBlindRageVisuals.AssetPaths;

    private static int UuughDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            12, // 呃呃呃！：DeadlyEnemies 进阶单次伤害。
            14); // 呃呃呃！：普通单次伤害。

    private const int UuughHits = 2; // 呃呃呃！：攻击次数。

    private static int AaaahDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            11, // 啊啊啊啊！！：DeadlyEnemies 进阶单次伤害。
            12); // 啊啊啊啊！！：普通单次伤害。

    private const int AaaahHits = 3; // 啊啊啊啊！！：攻击次数。

    private static int EvilIncarnationDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            9, // 邪恶的化身！！！：DeadlyEnemies 进阶单次伤害。
            8); // 邪恶的化身！！！：普通单次伤害。

    private const int EvilIncarnationHits = 3; // 邪恶的化身！！！：攻击次数。

    private static int Damage(int move) =>
        move switch
        {
            0 => UuughDamage,
            2 => AaaahDamage,
            3 => EvilIncarnationDamage,
            _ => EvilIncarnationDamage
        };

    private static int Hits(int move) =>
        move switch
        {
            0 => UuughHits,
            2 => AaaahHits,
            3 => EvilIncarnationHits,
            _ => EvilIncarnationHits
        };

    private const int UuughCorrosion = 2; // 呃呃呃：下回合腐蚀层数。

    private const int AaahCorrosion = 4; // 啊啊啊：下回合腐蚀层数。

    private const int AaaahCorrosion = 6; // 啊啊啊啊：下回合腐蚀层数。

    private const int EvilIncarnationCorrosion = 8; // 邪恶的化身：下回合腐蚀层数。

    private static int Corrosion(int move) =>
        move switch
        {
            0 => UuughCorrosion,
            1 => AaahCorrosion,
            2 => AaaahCorrosion,
            _ => EvilIncarnationCorrosion
        };

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _performingSpecial = false;
    }

    protected override IEnumerable<AbstractIntent> CreateIntents(int move)
    {
        if (move == 4)
        {
            return [new HealIntent(), new BuffIntent()];
        }

        string key = "NATURAL_BLIND_RAGE_" + Ids[move] + ".description";
        var corrosion = IntentBadge.FromPower<WrathServantNextTurnCorrosionPower>(Corrosion(move));
        if (move == 1)
        {
            return [new BadgedDebuffIntent(corrosion, Corrosion(move), key)];
        }

        if (move == 3)
        {
            return
            [
                new IndiscriminateAttackIntent(
                    () => Damage(move),
                    () => Hits(move),
                    key,
                    static owner => (owner.CombatState?.Encounter as encounters.NaturalFloorLiberation.NaturalFloorLiberationEncounter)?.RageGroupTargets() ?? [],
                    corrosion)
                {
                    CombinedEffect = GroupAttackCombinedEffect.Debuff
                }
            ];
        }

        return [new CombinedTargetedAttackDebuffIntent(() => Damage(move), () => Hits(move),
            key, key, false, move == 2 ? [corrosion, IntentBadge.FromPower<LibraryStrongPower>(StrongAmount)] : [corrosion])];
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmdCompat.Ensure<NaturalFloorTodayPlayPower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Ensure<NaturalFloorBlindRagePower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Ensure<NaturalFloorSinnerPower>(Creature, LostHp + 1, Creature, null, silent: true);
        if (Encounter?.UsesRageTransitionCarrier == true)
        {
            ForceReviveAndEmpowerState();
        }
    }

    internal void SynchronizeStaffMode(bool exists)
    {
        if (Staffless == !exists)
        {
            return;
        }

        Staffless = !exists;
        LostHp = 0;
        DamageSpecialPending = false;
        NormalMovesUntilSpecial = 0;
        RefreshCounter();
    }

    internal void CountHpLoss(decimal delta)
    {
        if (Initializing || !CanAct || delta >= 0 || _performingSpecial || Staffless)
        {
            return;
        }

        LostHp = Math.Min(DamageThreshold, LostHp + (int)Math.Ceiling(-delta));
        if (LostHp >= DamageThreshold)
        {
            DamageSpecialPending = true;
        }

        RefreshCounter();
    }

    private void RefreshCounter() => Creature.GetPower<NaturalFloorSinnerPower>()?.SetAmount(LostHp + 1, silent: true);

    protected override string SelectMove(Rng rng)
    {
        if (Encounter?.UsesRageTransitionCarrier == true)
        {
            return Ids[4];
        }

        if (DamageSpecialPending || Staffless && NormalMovesUntilSpecial == 0)
        {
            return Ids[3];
        }

        return Ids[PickNormal(rng)];
    }

    private int PickNormal(Rng rng)
    {
        int[] choices = Staffless ? [0, 2] : [0, 1, 2];
        int[] eligible = choices.Where(i => (CompletedMoveMask & (1 << i)) == 0).ToArray();
        if (eligible.Length == 0)
        {
            CompletedMoveMask = 0;
            eligible = choices;
        }
        int[] different = eligible.Where(i => i + 1 != LastNormalMove).ToArray();
        return rng.NextItem(different.Length > 0 ? different : eligible);
    }

    internal void ReplaceIllegalCorrosionMove()
    {
        if (Staffless && !IsPerformingMove && NextMove.StateId == Ids[1])
        {
            ForceMove(PickNormal(RunRng.MonsterAi));
        }
    }

    protected override async Task PerformMove(int move)
    {
        if (move == 4 && Encounter is { CurrentPhase: 3, TransitionPending: true } encounter)
        {
            await CreatureCmd.SetCurrentHp(Creature, 1);
            await CreatureCmd.TriggerAnim(Creature, "Cast", HitTime);
            await encounter.CompletePhaseTransition();
            return;
        }
        if (!CanAct)
        {
            return;
        }

        if (move == 1 && Staffless)
        {
            move = PickNormal(RunRng.MonsterAi);
        }

        _performingSpecial = move == 3;
        try
        {
            if (move == 1)
            {
                Sound("decay");
                await MegaCrit.Sts2.Core.Commands.CreatureCmd.TriggerAnim(Creature, "Cast", HitTime);
                await Corrode(Encounter!.RageGroupTargets(), Corrosion(move));
            }
            else
            {
                Creature[] hit = await Attack(Damage(move), Hits(move),
                    () => move == 3 ? Encounter!.RageGroupTargets() : GetTargetedAttackTargets(Creature),
                    move == 3 ? ["SpecialS1", "SpecialS2", "SpecialS3"] : ["AttackStrike", "AttackThrust", "AttackSlash"],
                    move == 3 ? ["special_1", "special_2", "special_3"] : ["strike", "thrust", "slash"]);
                await Corrode(hit, Corrosion(move));
                if (move == 2 && CanAct)
                {
                    await LibraryPowerCmd.Apply<LibraryStrongPower>(Creature, StrongAmount, -1, Creature, null);
                }
            }
            if (move == 3)
            {
                LostHp = 0;
                DamageSpecialPending = false;
                NormalMovesUntilSpecial = 1;
                RefreshCounter();
            }
            else
            {
                CompletedMoveMask |= 1 << move;
                LastNormalMove = move + 1;
                if (Staffless && NormalMovesUntilSpecial > 0)
                {
                    NormalMovesUntilSpecial--;
                }
            }
        }
        finally
        {
            _performingSpecial = false;
        }
    }

    public override IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner) => Encounter?.RageTargets() ?? [];

    internal override Dictionary<string, string> CaptureState()
    {
        var state = base.CaptureState();
        state["Cycle"] = CompletedMoveMask.ToString();
        state["Last"] = LastNormalMove.ToString();
        state["Lost"] = LostHp.ToString();
        state["Pending"] = DamageSpecialPending.ToString();
        state["Staffless"] = Staffless.ToString();
        state["UntilSpecial"] = NormalMovesUntilSpecial.ToString();
        return state;
    }

    internal override void RestoreState(Dictionary<string, string> state)
    {
        base.RestoreState(state);
        CompletedMoveMask = ReadInt(state, "Cycle");
        LastNormalMove = ReadInt(state, "Last");
        LostHp = ReadInt(state, "Lost");
        DamageSpecialPending = ReadBool(state, "Pending");
        Staffless = ReadBool(state, "Staffless");
        NormalMovesUntilSpecial = ReadInt(state, "UntilSpecial");
    }
}
