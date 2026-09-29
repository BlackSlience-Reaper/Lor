using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Natural;

public sealed class NaturalFloorForgottenSword : NaturalFloorDespairMonster
{
    private static readonly string[] Ids = ["HOLLOW_PRIDE", "FADED_TRUST", "SWORD_OF_GRIEF", "TEAR_EDGE_SWORD",
        "PIERCING_HEART_SWORD", "RENDING_HEART_SWORD", "RUINING_HEART_SWORD", "FALSE_DEATH_HIDDEN", "FALSE_DEATH"];

    protected override string[] MoveIds => Ids;

    public const int FalseDeathTurns = 2; // 暂息：假死持续回合数。

    public const int RecoveryPercent = 50; // 暂息：复活时体力及混乱恢复百分比。

    public const int SwordOfGriefBlock = 18; // 悲恸之剑：自身格挡。

    public const int TearEdgeSwordBlock = 18; // 泪锋之剑：自身格挡。

    public const int TearEdgeFrail = 2; // 泪锋之剑：脆弱层数。

    public const int TearEdgeWeak = 2; // 泪锋之剑：虚弱层数。

    public const int BleedAmount = 9; // 裂心之剑：整招施加的流血层数。

    public const int HealPercent = 10; // 穿心之剑：整招恢复最大体力的百分比。

    public const int PierceStabPercent = 10; // 穿心之剑：反刺扣除泪锋之剑最大体力的百分比。

    public const int SlashStabPercent = 15; // 裂心之剑：反刺扣除泪锋之剑最大体力的百分比。

    public const int BluntStabPercent = 20; // 毁心之剑：反刺扣除泪锋之剑最大体力的百分比。

    internal static int DazedAmount =>
        DamageValue(
            3, // 悲恸之剑：晕眩张数，普通。
            5); // 悲恸之剑：晕眩张数，DeadlyEnemies 进阶。

    internal static int StrengthAmount =>
        DamageValue(
            1, // 毁心之剑：力量层数，普通。
            2); // 毁心之剑：力量层数，DeadlyEnemies 进阶。

    [SavedProperty]
    public int SwordIndex { get; private set; }

    [SavedProperty]
    public bool HasTeardrop { get; private set; }

    [SavedProperty]
    public bool IsFakeDead { get; private set; }

    [SavedProperty]
    public int ReviveRound { get; private set; }

    [SavedProperty]
    public bool DespairAttack { get; private set; }

    [SavedProperty]
    public bool HasStabbed { get; private set; }

    [SavedProperty]
    public int NormalStep { get; private set; }

    [SavedProperty]
    public int TeardropStep { get; private set; }

    internal bool CanReceiveTeardrop => Creature.IsAlive && !IsFakeDead && !HasTeardrop;

    internal string VisualForm => IsFakeDead ? "dead" : DespairAttack ? "despair" : HasTeardrop ? "teardrop" : "normal";

    private NaturalFloorTearEdgeBoss? Boss => Creature.CombatState?.Enemies.Select(static c => c.Monster).OfType<NaturalFloorTearEdgeBoss>().FirstOrDefault();

    public override int MinInitialHp =>
        HpValue(
            91, // 初始体力下限：普通。
            96); // 初始体力下限：ToughEnemies 进阶。

    public override int MaxInitialHp =>
        HpValue(
            95, // 初始体力上限：普通。
            100); // 初始体力上限：ToughEnemies 进阶。

    public override int DefaultChaoResistance => 60; // 初始混乱抗性。

    public override LibraryCreatureResistanceData.Resistance DefaultPhysicalResistanceData => Resistance(HasTeardrop);

    public override LibraryCreatureResistanceData.Resistance DefaultChaoResistanceData => Resistance(HasTeardrop);

    // 混乱期间泪滴或绝望状态可能改变，恢复时须按当前状态重新选招，不能重放混乱前的招式。
    public override string? StunRecoveryStateId => RouterStateId;

    protected override IEnumerable<string> VisualAssets => NaturalFloorForgottenSwordVisuals.AssetPaths;

    internal void Configure(int index)
    {
        SwordIndex = Math.Clamp(index, 0, 2);
        NormalStep = TeardropStep = SwordIndex == 1 ? 1 : 0;
    }

    protected override void RebaseRounds(int offset)
    {
        if (IsFakeDead)
        {
            ReviveRound += offset;
        }
    }

    protected override int SelectMove() => IsFakeDead ? (ReviveRound <= (Creature.CombatState?.RoundNumber ?? 0) ? 8 : 7)
        : DespairAttack ? 4 + SwordIndex : HasTeardrop ? 2 + TeardropStep : NormalStep;

    private static int HollowPrideDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            14, // 空洞的自尊：DeadlyEnemies 进阶单次伤害。
            12); // 空洞的自尊：普通单次伤害。

    private const int HollowPrideHits = 1; // 空洞的自尊：攻击次数。

    private static int FadedTrustDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            6, // 褪色的信任：DeadlyEnemies 进阶单次伤害。
            5); // 褪色的信任：普通单次伤害。

    private const int FadedTrustHits = 3; // 褪色的信任：攻击次数。

    private static int PiercingHeartSwordDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            16, // 穿心之剑：DeadlyEnemies 进阶单次伤害。
            15); // 穿心之剑：普通单次伤害。

    private const int PiercingHeartSwordHits = 1; // 穿心之剑：攻击次数。

    private static int RendingHeartSwordDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            9, // 裂心之剑：DeadlyEnemies 进阶单次伤害。
            8); // 裂心之剑：普通单次伤害。

    private const int RendingHeartSwordHits = 2; // 裂心之剑：攻击次数。

    private static int RuiningHeartSwordDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            7, // 毁心之剑：DeadlyEnemies 进阶单次伤害。
            6); // 毁心之剑：普通单次伤害。

    private const int RuiningHeartSwordHits = 3; // 毁心之剑：攻击次数。

    private static int Damage(int move) =>
        move switch
        {
            0 => HollowPrideDamage,
            1 => FadedTrustDamage,
            4 => PiercingHeartSwordDamage,
            5 => RendingHeartSwordDamage,
            6 => RuiningHeartSwordDamage,
            _ => 0
        };

    private static int Hits(int move) =>
        move switch
        {
            0 => HollowPrideHits,
            1 => FadedTrustHits,
            4 => PiercingHeartSwordHits,
            5 => RendingHeartSwordHits,
            6 => RuiningHeartSwordHits,
            _ => RuiningHeartSwordHits
        };

    protected override IEnumerable<AbstractIntent> CreateIntents(int move)
    {
        string key = "NATURAL_SWORD_" + Ids[move] + ".description";
        return move switch
        {
            0 => [new SingleAttackIntent(HollowPrideDamage)],
            1 => [new MultiAttackIntent(FadedTrustDamage, FadedTrustHits)],
            2 => [new CombinedDefendDebuffIntent(SwordOfGriefBlock, key, IntentBadge.StatusCard<Dazed>(DazedAmount)),
                new DetailedStatusCardIntent<Dazed>(DazedAmount, PileType.Discard)],
            3 => [new CombinedDefendDebuffIntent(TearEdgeSwordBlock, key, IntentBadge.FromPower<FrailPower>(TearEdgeFrail), IntentBadge.FromPower<WeakPower>(TearEdgeWeak))],
            4 => [new NaturalFloorDespairSwordIntent(PiercingHeartSwordDamage, PiercingHeartSwordHits, key, PierceStabPercent, false, IntentBadge.Heal())],
            5 => [new NaturalFloorDespairSwordIntent(RendingHeartSwordDamage, RendingHeartSwordHits, key, SlashStabPercent, true, IntentBadge.FromPower<LibraryBleedingPower>(BleedAmount))],
            6 => [new NaturalFloorDespairSwordIntent(RuiningHeartSwordDamage, RuiningHeartSwordHits, key, BluntStabPercent, false, IntentBadge.FromPower<StrengthPower>(StrengthAmount))],
            8 => [new HealIntent(), new BuffIntent()],
            _ => [new HiddenIntent()]
        };
    }

    protected override async Task ApplyPassives()
    {
        await PowerCmdCompat.Ensure<MinionPower>(Creature, 1, Creature, null, silent: true); // 泪滴之剑：1 层爪牙标记，各剑形态共用。
        await PowerCmdCompat.Ensure<NaturalFloorSwordFalseDeathPower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Ensure<NaturalFloorPierceDespairPower>(Creature, 1, Creature, null, silent: true);
    }

    internal async Task ApplyTeardrop()
    {
        if (!CanReceiveTeardrop)
        {
            return;
        }

        Sound("grant");
        await PowerCmdCompat.Apply<NaturalFloorTeardropPower>(Creature, 1, Boss?.Creature ?? Creature, null);
    }

    internal async Task SetTeardrop(bool value)
    {
        HasTeardrop = value;
        if (Creature is LibraryCreature library)
        {
            var resistance = Resistance(value);
            var context = new ThrowingPlayerChoiceContext();
            foreach (LibraryDamageType type in new[] { LibraryDamageType.Slash, LibraryDamageType.Pierce, LibraryDamageType.Blunt })
            {
                LibraryResistanceLevel level = type == LibraryDamageType.Pierce ? resistance.Pierce : resistance.Slash;
                await LibraryCreatureCmd.SetPhysicalResistance(context, library, Creature, type, level);
                await LibraryCreatureCmd.SetChaoResistance(context, library, Creature, type, level);
            }
        }
        RefreshNormalMove();
    }

    internal async Task EnterFalseDeath()
    {
        if (IsFakeDead || Encounter is not { SettlementTriggered: false, TransitionPending: false })
        {
            return;
        }

        bool hadTeardrop = HasTeardrop;
        IsFakeDead = true;
        ReviveRound = (Creature.CombatState?.RoundNumber ?? 0) + FalseDeathTurns;
        DespairAttack = false;
        if (hadTeardrop)
        {
            Boss?.QueueDespair();
        }

        if (Creature.GetPower<NaturalFloorTeardropPower>() is { } tear)
        {
            await PowerCmd.Remove(tear);
        }

        await SetTeardrop(false);
        await FakeDeathDebuffHelper.ClearDebuffs(Creature);
        await TickFalseDeath(Creature.CombatState?.RoundNumber ?? 0);
    }

    internal Task TickFalseDeath(int round)
    {
        if (!IsFakeDead)
        {
            return Task.CompletedTask;
        }

        if (Creature is LibraryCreature library)
        {
            library.RestorePreStunResistance();
            library.SetCurrentChaoValueInternal(0);
        }
        ForceMove(round >= ReviveRound ? 8 : 7);
        return Task.CompletedTask;
    }

    private async Task RestoreLife(int percent)
    {
        IsFakeDead = false;
        ReviveRound = 0;
        await CreatureCmd.SetCurrentHp(Creature, Math.Max(1, Math.Floor(Creature.MaxHp * percent / 100m)));
        if (Creature is LibraryCreature library)
        {
            library.RestorePreStunResistance();
            library.SetCurrentChaoValueInternal(library.MaxChaoValue);
        }
    }

    internal async Task EnterDespair()
    {
        // The blessing includes fake-dead and staggered swords.
        await RestoreLife(100);
        DespairAttack = true;
        HasStabbed = false;
        ForceMove(4 + SwordIndex);
    }

    internal void EndDespair()
    {
        DespairAttack = false;
        RefreshNormalMove();
    }

    private void RefreshNormalMove()
    {
        if (IsFakeDead || DespairAttack)
        {
            return;
        }

        // 混乱锁定时不改写击晕，恢复时由 StunRecoveryStateId 经路由重新选招。
        if (Creature.IsStunned && Creature is LibraryCreature { IsChaoed: true })
        {
            return;
        }

        ForceMove(SelectMove());
    }

    protected override async Task PerformMove(int move, IReadOnlyList<Creature> targets)
    {
        if (move == 7)
        {
            return;
        }

        if (move == 8)
        {
            if (IsFakeDead && Encounter is { SettlementTriggered: false })
            {
                Sound("grant");
                await RestoreLife(RecoveryPercent);
                await CreatureCmd.TriggerAnim(Creature, "Cast", AttackTime);
            }
            return;
        }
        if (!CanAct || IsFakeDead)
        {
            return;
        }

        Creature[] players = targets.Where(static c => c.IsPlayer && c.IsAlive).ToArray();
        if (move is 2 or 3)
        {
            Sound("guard");
            await CreatureCmd.TriggerAnim(Creature, "Guard", AttackTime);
            await CreatureCmd.GainBlock(Creature, move == 2 ? SwordOfGriefBlock : TearEdgeSwordBlock, ValueProp.Move, null);
            foreach (Creature target in players)
            {
                if (move == 2)
                {
                    await CardPileCmdCompat.AddToCombatAndPreview<Dazed>(target, PileType.Discard, DazedAmount, addedByPlayer: false);
                }
                else
                {
                    await PowerCmdCompat.ApplyDebuff<FrailPower>(target, TearEdgeFrail, Creature, null);
                    await PowerCmdCompat.ApplyDebuff<WeakPower>(target, TearEdgeWeak, Creature, null);
                }
            }
            TeardropStep = 1 - TeardropStep;
            return;
        }
        int hits = Hits(move);
        LibraryDamageType type = move is 1 or 6 ? LibraryDamageType.Blunt : move == 4 ? LibraryDamageType.Pierce : LibraryDamageType.Slash;
        string motion = type == LibraryDamageType.Blunt ? "Blunt" : type == LibraryDamageType.Pierce ? "Pierce" : "Slash";
        bool damagedPlayer = false;
        for (int i = 0; i < hits && CanAct && !IsFakeDead; i++)
        {
            Sound((HasTeardrop || DespairAttack ? "tear_" : "normal_") + motion.ToLowerInvariant());
            var attack = await LibraryDamageCmd.Attack(Damage(move)).FromMonster(this).WithDamageType(type)
                .WithAttackerAnim(DespairAttack ? "Attack" : motion, AttackTime).WithHitFx("vfx/vfx_attack_slash").Execute(null);
            damagedPlayer |= attack.DamageResults.SelectMany(static r => r).Any(static r => r.Receiver.IsPlayer && r.UnblockedDamage > 0);
        }
        if (move < 2)
        {
            NormalStep = 1 - NormalStep;
            return;
        }
        // Resolve the move's secondary effect once, including fully blocked attacks.
        if (Creature.IsAlive && !IsFakeDead)
        {
            if (move == 4)
            {
                await CreatureCmd.Heal(Creature, Math.Ceiling(Creature.MaxHp * HealPercent / 100m));
            }

            if (move == 5)
            {
                foreach (Creature target in players.Where(static c => c.IsAlive))
                {
                    await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(target, BleedAmount, Creature, null);
                }
            }

            if (move == 6)
            {
                await PowerCmdCompat.Apply<StrengthPower>(Creature, StrengthAmount, Creature, null);
            }

            if (!damagedPlayer && DespairAttack && !HasStabbed && Boss is { IsInDespair: true } boss)
            {
                HasStabbed = true;
                await boss.ReceiveSwordStab(new[] { PierceStabPercent, SlashStabPercent, BluntStabPercent }[SwordIndex]);
            }
        }
    }

    private static LibraryCreatureResistanceData.Resistance Resistance(bool tear) => tear
        ? new()
        {
            Slash = LibraryResistanceLevel.Resist,
            Pierce = LibraryResistanceLevel.Fatal,
            Blunt = LibraryResistanceLevel.Resist
        }
        : new(LibraryResistanceLevel.Normal);
}
