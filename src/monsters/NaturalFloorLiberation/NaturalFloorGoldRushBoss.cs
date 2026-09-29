using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.audio;
using LibraryOfRuina.combat;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers;
using LibraryOfRuina.powers.HistoryFloorLiberation;
using LibraryOfRuina.powers.NaturalFloorLiberation;
using LibraryOfRuina.visuals.NaturalFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.NaturalFloorLiberation;

public sealed class NaturalFloorGoldRushBoss : NaturalFloorPhaseMonster, ILiberationPrimaryPhaseBoss
{
    internal const int TransformThresholdPercent = 50; // 闪烁的欲望：变身前体力下限百分比。
    private const int NormalMoveCount = 4; // 每个形态：依次执行的普通招式数量，最后一招强化后补齐幸福。
    internal const int HappinessLimit = 2; // 周期召唤：场上幸福数量上限。
    internal const int GluttonyHealPercent = 25; // 暴食被动：整次暴君之路成功时恢复最大体力的百分比。
    internal const int ForHappinessBlock = 55; // 为了幸福：获得格挡。
    internal const int ShiningStrikeHits = 1; // 闪耀一击：攻击次数。
    internal const int ShiningStrikeBinding = 2; // 闪耀一击：永久束缚层数。
    internal const int ShiningStrikeFlaw = 2; // 闪耀一击：永久破绽层数。
    internal const int VictoriousHits = 3; // 胜利的陶醉：攻击次数。
    internal const int VictoriousParalysis = 2; // 胜利的陶醉：麻痹层数。
    internal const int VictoriousParalysisTurns = 1; // 胜利的陶醉：麻痹持续玩家回合数。
    internal const int OverwhelmingFrail = 3; // 压倒的光彩：脆弱层数。
    internal const int OverwhelmingVulnerable = 3; // 压倒的光彩：易伤层数。
    internal const int GoldenPathHits = 1; // 闪金之路：群体攻击次数。
    internal const int GoldenPathConfusion = 1; // 闪金之路：混乱层数。
    internal const int HungerWounds = 5; // 饥饿：每名玩家抽牌堆加入的伤口数。
    internal const int GluttonyHits = 1; // 暴食招式：攻击次数。
    internal const int GluttonyBlock = 30; // 暴食招式：获得格挡。
    internal const int CravingHits = 4; // 渴望：攻击次数。
    internal const int ObsessionFrail = 5; // 痴迷：脆弱层数。
    internal const int ObsessionVulnerable = 5; // 痴迷：易伤层数。
    internal const int TyrantPathHits = 3; // 暴君之路：群体攻击次数。

    private static readonly string[] Ids =
    [
        "FOR_HAPPINESS", "SHINING_STRIKE", "VICTORIOUS_EUPHORIA", "OVERWHELMING_GLORY", "GOLDEN_PATH",
        "HUNGER", "GLUTTONY", "CRAVING", "OBSESSION", "TYRANT_PATH", "PHASE_END"
    ];

    internal static int ShiningStrikeDamage => DamageValue(23, 25); // 闪耀一击：普通 / DeadlyEnemies 进阶单次伤害。

    internal static int VictoriousDamage => DamageValue(10, 12); // 胜利的陶醉：普通 / DeadlyEnemies 进阶单次伤害。

    internal static int VictoriousStrength => DamageValue(2, 3); // 胜利的陶醉：普通 / DeadlyEnemies 进阶力量层数。

    internal static int OverwhelmingVigor => DamageValue(13, 15); // 压倒的光彩：普通 / DeadlyEnemies 进阶活力层数。

    internal static int GoldenPathDamage => DamageValue(21, 24); // 闪金之路：普通 / DeadlyEnemies 进阶单次伤害。

    internal static int GluttonyDamage => DamageValue(25, 26); // 暴食招式：普通 / DeadlyEnemies 进阶单次伤害。

    internal static int CravingDamage => DamageValue(10, 11); // 渴望：普通 / DeadlyEnemies 进阶单次伤害。

    internal static int ObsessionVigor => DamageValue(20, 24); // 痴迷：普通 / DeadlyEnemies 进阶活力层数。

    internal static int TyrantPathDamage => DamageValue(12, 14); // 暴君之路：普通 / DeadlyEnemies 进阶单次伤害。

    internal static int SelfIntoxicationStrong => DamageValue(2, 3); // 自我陶醉：普通 / DeadlyEnemies 进阶永久强壮层数。

    internal static int KingBleeding => DamageValue(4, 6); // 贪婪国王：普通 / DeadlyEnemies 进阶每段流血层数。

    public override int MinInitialHp => HpValue(465, 475); // 闪金冲锋：普通 / ToughEnemies 进阶初始体力下限。

    public override int MaxInitialHp => HpValue(470, 480); // 闪金冲锋：普通 / ToughEnemies 进阶初始体力上限。

    public override int DefaultChaoResistance => 250; // 两种形态：混乱抗性上限。

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new(LibraryResistanceLevel.Endure);

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new(LibraryResistanceLevel.Resist);

    public int LiberationPhase => 4;

    public override LocString Title => L10NMonsterLookup(Id.Entry + (IsKingForm ? ".kingName" : ".name"));

    protected override string[] MoveIds => Ids;

    // 混乱恢复统一回到路由：群攻失败触发的混乱发生在抽取下一招之前，而混乱锁生效后
    // 无法再改写恢复招式，只有恢复时重新经过路由才能按推进后的周期选招。
    public override string? StunRecoveryStateId => RouterStateId;

    protected override IEnumerable<string> VisualAssets => NaturalFloorGoldRushVisuals.AssetPaths;

    [SavedProperty]
    public bool IsKingForm { get; private set; }

    [SavedProperty]
    public bool TransformPending { get; private set; }

    [SavedProperty]
    public int CompletedFormActions { get; private set; }

    [SavedProperty]
    public int NormalMoveIndex { get; private set; }

    [SavedProperty]
    public bool GroupAttackPending { get; private set; }

    [SavedProperty]
    public bool IsChargingSpecial { get; private set; }

    [SavedProperty]
    public bool SelfIntoxicationPending { get; private set; }

    [SavedProperty]
    public bool MomentaryHappinessPending { get; private set; }

    [SavedProperty]
    public int LastPlayerRound { get; private set; } = -1;

    [SavedProperty]
    public bool PhaseEndPending { get; private set; }

    internal int MinimumFormHp => (int)Math.Ceiling(Creature.MaxHp * TransformThresholdPercent / 100m);

    internal bool HasFormHpFloor => !IsKingForm && Encounter is { SettlementTriggered: false, TransitionPending: false };

    protected override void RebaseRounds(int offset)
    {
        if (LastPlayerRound >= 0)
        {
            LastPlayerRound += offset;
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        if (GroupAttackPending && NormalMoveIndex != 0)
        {
            // 旧存档可能在第三招后预约群攻；恢复尚未完成的普通招式循环。
            // 混乱中的恢复招式由路由在恢复时重新选取，此处只需要修正未混乱的当前招式。
            GroupAttackPending = false;
            IsChargingSpecial = false;
            if (!Creature.IsStunned)
            {
                ForceMove(SelectMove());
            }
        }

        if (GroupAttackPending && !IsChargingSpecial)
        {
            // 兼容已有蓄力回合存档：恢复场景时立即保持对应形态的蓄力姿态。
            IsChargingSpecial = true;
            await CreatureCmd.TriggerAnim(Creature, "Idle", 0f);
        }
    }

    protected override int SelectMove()
    {
        if (PhaseEndPending)
        {
            return 10;
        }

        int offset = IsKingForm ? 5 : 0;
        return offset + (GroupAttackPending ? 4 : NormalMoveIndex);
    }

    protected override IEnumerable<AbstractIntent> CreateIntents(int move)
    {
        string key = "NATURAL_FLOOR_GOLD_RUSH_" + Ids[move] + ".description";
        switch (move)
        {
            case 0:
                return [new NaturalFloorGoldRushDefendIntent(ForHappinessBlock, key)];
            case 1:
                return [new CombinedAttackDebuffIntent(() => ShiningStrikeDamage, () => ShiningStrikeHits, key,
                    IntentBadge.Bind(ShiningStrikeBinding), IntentBadge.Flaw(ShiningStrikeFlaw))];
            case 2:
                return [new CombinedAttackDebuffIntent(() => VictoriousDamage, () => VictoriousHits, key,
                    IntentBadge.FromPower<LibraryOfRuinaParalysisPower>(VictoriousParalysis, VictoriousParalysisTurns.ToString()),
                    IntentBadge.FromPower<StrengthPower>(() => VictoriousStrength))];
            case 3:
                return [new BadgedBuffIntent(IntentBadge.FromPower<VigorPower>(() => OverwhelmingVigor), OverwhelmingVigor, key),
                    new BadgedDebuffIntent([IntentBadge.Frail(OverwhelmingFrail), IntentBadge.Vulnerable(OverwhelmingVulnerable)],
                        OverwhelmingFrail, "NATURAL_FLOOR_GOLD_RUSH_GLORY_DEBUFF.description")];
            case 4:
                return [new IndiscriminateAttackIntent(() => GoldenPathDamage, () => GoldenPathHits, key,
                    IntentBadge.Confusion(GoldenPathConfusion)) { CombinedEffect = GroupAttackCombinedEffect.Debuff }];
            case 5:
                return [new DetailedStatusCardIntent<Wound>(HungerWounds, PileType.Draw, DetailedIntentScopeText.Target)];
            case 6:
                return [new CombinedAttackDefendIntent(() => GluttonyDamage, () => GluttonyHits, key, GluttonyBlock)];
            case 7:
                return [new MultiAttackIntent(CravingDamage, CravingHits)];
            case 8:
                return [new BadgedBuffIntent(IntentBadge.FromPower<VigorPower>(() => ObsessionVigor), ObsessionVigor, key),
                    new BadgedDebuffIntent([IntentBadge.Frail(ObsessionFrail), IntentBadge.Vulnerable(ObsessionVulnerable)],
                        ObsessionFrail, "NATURAL_FLOOR_GOLD_RUSH_OBSESSION_DEBUFF.description")];
            case 9:
                return [new IndiscriminateAttackIntent(() => TyrantPathDamage, () => TyrantPathHits, key)];
            default:
                return [new StunIntent()];
        }
    }

    protected override async Task ApplyPassives()
    {
        await PowerCmdCompat.Ensure<HistoryFloorCorrosionPower>(Creature, 1, Creature, null, silent: true);
        if (IsKingForm)
        {
            await PowerCmdCompat.Ensure<NaturalFloorKingOfGreedPower>(Creature, 1, Creature, null, silent: true);
            await PowerCmdCompat.Ensure<NaturalFloorGluttonyPower>(Creature, 1, Creature, null, silent: true);
            await PowerCmdCompat.Ensure<NaturalFloorKingMomentaryHappinessPower>(Creature, 1, Creature, null, silent: true);
        }
        else
        {
            await PowerCmdCompat.Ensure<NaturalFloorFlickeringDesirePower>(Creature, 1, Creature, null, silent: true);
            await PowerCmdCompat.Ensure<NaturalFloorSelfIntoxicationPower>(Creature, 1, Creature, null, silent: true);
            await PowerCmdCompat.Ensure<NaturalFloorMomentaryHappinessPower>(Creature, 1, Creature, null, silent: true);
        }

    }

    internal void QueueTransformation()
    {
        AssertMutable();
        if (HasFormHpFloor && Creature.CurrentHp <= MinimumFormHp)
        {
            TransformPending = true;
        }
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext context,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        await base.BeforeSideTurnStart(context, side, participants, combatState);
        if (!CanAct || side != CombatSide.Player || LastPlayerRound == combatState.RoundNumber)
        {
            return;
        }

        LastPlayerRound = combatState.RoundNumber;
        if (SelfIntoxicationPending)
        {
            SelfIntoxicationPending = false;
            await LibraryPowerCmd.Apply<LibraryStrongPower>(context, Creature, SelfIntoxicationStrong, 0, true, Creature, null);
            Sound("self_intoxication");
        }

        if (MomentaryHappinessPending)
        {
            MomentaryHappinessPending = false;
            if (Creature is LibraryCreature library)
            {
                await LibraryCreatureCmd.SetCurrentChaoValue(library, 0);
            }
        }

        if (TransformPending)
        {
            await TransformToKing();
        }
    }

    private async Task TransformToKing()
    {
        IsKingForm = true;
        TransformPending = false;
        CompletedFormActions = 0;
        NormalMoveIndex = 0;
        GroupAttackPending = false;
        IsChargingSpecial = false;
        SelfIntoxicationPending = false;
        MomentaryHappinessPending = false;
        await Encounter!.RemoveHappiness();
        if (Creature.GetPower<NaturalFloorFlickeringDesirePower>() is { } desire)
        {
            await PowerCmd.Remove(desire);
        }

        if (Creature.GetPower<NaturalFloorSelfIntoxicationPower>() is { } intoxication)
        {
            await PowerCmd.Remove(intoxication);
        }

        if (Creature.GetPower<NaturalFloorMomentaryHappinessPower>() is { } momentary)
        {
            await PowerCmd.Remove(momentary);
        }

        await CreatureCmd.SetCurrentHp(Creature, Creature.MaxHp);
        if (Creature is LibraryCreature library)
        {
            library.RestorePreStunResistance();
            await LibraryCreatureCmd.SetCurrentChaoValue(library, library.MaxChaoValue);
        }

        await ApplyPassives();
        ForceMove(SelectMove());
        Sound("transform");
        await CreatureCmd.TriggerAnim(Creature, "Transform", NaturalFloorGoldRushVisuals.TransformTime);
        if (Creature.GetCreatureNode() is { } node)
        {
            await node.RefreshIntents();
        }
    }

    protected override async Task PerformMove(int move, IReadOnlyList<Creature> targets)
    {
        if (move == 10)
        {
            if (Encounter is { } encounter)
            {
                await encounter.CompleteFourthPhase();
            }

            return;
        }

        if (!CanAct || Creature.IsStunned || Creature is LibraryCreature { IsChaoed: true })
        {
            return;
        }

        bool causedHpLoss = false;
        switch (move)
        {
            case 0:
                await CreatureCmd.TriggerAnim(Creature, "Guard", NaturalFloorGoldRushVisuals.GuardTime);
                Sound("guard");
                await CreatureCmd.GainBlock(Creature, ForHappinessBlock, ValueProp.Move, null);
                break;
            case 1:
                await Attack(ShiningStrikeDamage, ShiningStrikeHits, false);
                foreach (Creature player in LivingPlayers())
                {
                    await LibraryPowerCmd.Apply<LibraryBindingPower>(new ThrowingPlayerChoiceContext(), player, ShiningStrikeBinding, 0, true, Creature, null);
                    await LibraryPowerCmd.Apply<LibraryDisarmPower>(new ThrowingPlayerChoiceContext(), player, ShiningStrikeFlaw, 0, true, Creature, null);
                }
                break;
            case 2:
                await Attack(VictoriousDamage, VictoriousHits, false);
                foreach (Creature player in LivingPlayers())
                {
                    var paralysis = await PowerCmdCompat.ApplyDebuff<LibraryOfRuinaParalysisPower>(player, VictoriousParalysis, Creature, null);
                    paralysis?.SetTurnsRemaining(VictoriousParalysisTurns);
                }
                await PowerCmdCompat.Apply<StrengthPower>(Creature, VictoriousStrength, Creature, null);
                break;
            case 3:
                await Empower(OverwhelmingVigor, OverwhelmingFrail, OverwhelmingVulnerable);
                break;
            case 4:
                causedHpLoss = await Attack(GoldenPathDamage, GoldenPathHits, true);
                foreach (Creature player in LivingPlayers())
                {
                    await PowerCmdCompat.ApplyDebuff<LibraryOfRuinaConfusionPower>(player, GoldenPathConfusion, Creature, null);
                }
                break;
            case 5:
                await CreatureCmd.TriggerAnim(Creature, "Cast", NaturalFloorGoldRushVisuals.CastTime);
                await CardPileCmdCompat.AddToCombatAndPreview<Wound>(LivingPlayers(), PileType.Draw, HungerWounds, addedByPlayer: false);
                break;
            case 6:
                await Attack(GluttonyDamage, GluttonyHits, false);
                await CreatureCmd.GainBlock(Creature, GluttonyBlock, ValueProp.Move, null);
                break;
            case 7:
                await Attack(CravingDamage, CravingHits, false);
                break;
            case 8:
                await Empower(ObsessionVigor, ObsessionFrail, ObsessionVulnerable);
                break;
            case 9:
                causedHpLoss = await Attack(TyrantPathDamage, TyrantPathHits, true);
                break;
        }

        if (!CanAct)
        {
            return;
        }

        if (move is 4 or 9)
        {
            GroupAttackPending = false;
            if (!causedHpLoss)
            {
                MomentaryHappinessPending = true;
            }
            else if (move == 4)
            {
                SelfIntoxicationPending = true;
            }
            else
            {
                await CreatureCmd.Heal(Creature, Creature.MaxHp * GluttonyHealPercent / 100m);
                Sound("gluttony_success");
            }
        }
        else
        {
            NormalMoveIndex = (NormalMoveIndex + 1) % NormalMoveCount;
        }

        CompletedFormActions++;
        // 压倒的光彩 / 痴迷完成后召唤，下一次行动使用对应形态的群攻。
        if (move is 3 or 8)
        {
            GroupAttackPending = true;
            await Encounter!.SummonHappiness();
        }
    }

    private async Task Empower(int vigor, int frail, int vulnerable)
    {
        IsChargingSpecial = true;
        await CreatureCmd.TriggerAnim(Creature, "Cast", NaturalFloorGoldRushVisuals.CastTime);
        await PowerCmdCompat.Apply<VigorPower>(Creature, vigor, Creature, null);
        foreach (Creature player in LivingPlayers())
        {
            await PowerCmdCompat.ApplyDebuff<FrailPower>(player, frail, Creature, null);
            await PowerCmdCompat.ApplyDebuff<VulnerablePower>(player, vulnerable, Creature, null);
        }
    }

    private async Task<bool> Attack(int damage, int hits, bool group)
    {
        Creature[] players = LivingPlayers();
        if (players.Length == 0)
        {
            return false;
        }

        if (group)
        {
            await CreatureCmd.TriggerAnim(Creature, "SpecialIntro", NaturalFloorGoldRushVisuals.SpecialIntroTime);
            IsChargingSpecial = false;
        }

        string form = IsKingForm ? "king" : "human";
        Sound(form + (group ? "_special" : "_attack"));
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, players))
        {
            AttackCommand attack = DamageCmd.Attack(damage)
                .FromMonster(this)
                .WithHitCount(hits)
                .WithAttackerAnim(group ? "SpecialAttack" : "Attack", group
                    ? NaturalFloorGoldRushVisuals.SpecialHitTime
                    : NaturalFloorGoldRushVisuals.AttackHitTime);
            if (group)
            {
                attack = attack.WithIndiscriminateBlockBreak(this, damage, players);
            }

            await attack.Execute(null);
            return AttackCommandCompat.Results(attack).Any(result => result.Receiver.IsPlayer && result.UnblockedDamage > 0);
        }
    }

    public override Task AfterDamageReceivedLate(PlayerChoiceContext context, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Creature && result.TotalDamage > 0 && Creature.IsAlive)
        {
            Sound(result.WasFullyBlocked ? "guard" : "hit");
            return CreatureCmd.TriggerAnim(Creature, result.WasFullyBlocked ? "Guard" : "Hit", 0f);
        }

        return Task.CompletedTask;
    }

    public override Task AfterDeath(PlayerChoiceContext context, Creature creature, bool prevented, float animLength)
    {
        // 闪金冲锋与贪婪国王任一形态真正死亡，都立即解锁第四阶段结算。
        if (creature == Creature && !prevented && Encounter is { SettlementTriggered: false } encounter)
        {
            IsChargingSpecial = false;
            PhaseEndPending = true;
            return encounter.CompleteFourthPhase(fromDeathHook: true);
        }

        return Task.CompletedTask;
    }

    protected override Task AfterSideTurnEndInternal(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants) =>
        PhaseEndPending && Encounter is { } encounter ? encounter.CompleteFourthPhase() : Task.CompletedTask;

    public Task TriggerReviveAndEmpowerState()
    {
        ForceReviveAndEmpowerState();
        return Task.CompletedTask;
    }

    public void ForceReviveAndEmpowerState() => ForceMove(10);

    private Creature[] LivingPlayers() => Encounter?.LivingPlayers() ?? [];

    internal static string SoundPath(string file) => file switch
    {
        "human_attack" => KingOfGreed.GoldenAmber.SfxRoot + "magical_girl_stab.ogg",
        "king_attack" => KingOfGreed.GoldenAmber.SfxRoot + "king_stab.ogg",
        "human_special" => KingOfGreed.GoldenAmber.SfxRoot + "golden_path.ogg",
        "king_special" => KingOfGreed.GoldenAmber.SfxRoot + "tyrant_path.ogg",
        "transform" => KingOfGreed.GoldenAmber.SfxRoot + "transform_to_king.ogg",
        "summon" => KingOfGreed.ShiningHappiness.SummonSfxPath,
        "guard" or "hit" => NaturalFloorDespairMonster.SfxRoot + file + ".ogg",
        _ => KingOfGreed.GoldenAmber.SfxRoot + file + ".ogg"
    };

    internal static void Sound(string file) => LocalOggOneShotPlayer.Play(SoundPath(file));
}
