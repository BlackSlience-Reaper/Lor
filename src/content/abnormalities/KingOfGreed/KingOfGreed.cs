using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.KingOfGreed;

public sealed class KingOfGreed : LorMonsterModel
{
    public const float LocalSfxVolumeScale = 0.85f;
    public static readonly float LocalSfxVolumeDb = Mathf.LinearToDb(LocalSfxVolumeScale);

    private const string GoldenPathComboMoveId = "GOLDEN_PATH_COMBO";
    private const string TyrantPathComboMoveId = "TYRANT_PATH_COMBO";
    private const string ForHappinessMoveId = "FOR_HAPPINESS";
    private const string ShiningStrikeMoveId = "SHINING_STRIKE";
    private const string VictoriousEuphoriaMoveId = "VICTORIOUS_EUPHORIA";
    private const string OverwhelmingGloryMoveId = "OVERWHELMING_GLORY";
    private const string HungerMoveId = "HUNGER";
    private const string GluttonyMoveId = "GLUTTONY";
    private const string CravingMoveId = "CRAVING";
    private const string ObsessionMoveId = "OBSESSION";
    private const string SummonShiningHappinessMoveId = "SUMMON_SHINING_HAPPINESS";
    private const string RouterStateId = "KING_OF_GREED_ROUTER";
    private const int SummonCycleInterval = 3;

    private static readonly string[] NormalBackgroundTextLineKeys =
    [
        "KING_OF_GREED.backgroundText.normal.0",
        "KING_OF_GREED.backgroundText.normal.1",
        "KING_OF_GREED.backgroundText.normal.2",
        "KING_OF_GREED.backgroundText.normal.3"
    ];

    private static readonly string[] MagicalGirlBackgroundTextLineKeys =
    [
        "KING_OF_GREED.backgroundText.magicalGirl.0",
        "KING_OF_GREED.backgroundText.magicalGirl.1",
        "KING_OF_GREED.backgroundText.magicalGirl.2"
    ];

    private static readonly string[] GoldenPathBackgroundTextLineKeys =
    [
        "KING_OF_GREED.backgroundText.goldenPath.0",
        "KING_OF_GREED.backgroundText.goldenPath.1"
    ];

    private static readonly string[] AttackFailureBackgroundTextLineKeys =
    [
        "KING_OF_GREED.backgroundText.attackFailure.0",
        "KING_OF_GREED.backgroundText.attackFailure.1",
        "KING_OF_GREED.backgroundText.attackFailure.2"
    ];

    private static readonly string[] AttackSuccessBackgroundTextLineKeys =
    [
        "KING_OF_GREED.backgroundText.attackSuccess.0",
        "KING_OF_GREED.backgroundText.attackSuccess.1",
        "KING_OF_GREED.backgroundText.attackSuccess.2"
    ];

    private static readonly string[] KingBackgroundTextLineKeys =
    [
        "KING_OF_GREED.backgroundText.king.0",
        "KING_OF_GREED.backgroundText.king.1",
        "KING_OF_GREED.backgroundText.king.2"
    ];

    private static readonly string[] TyrantPathBackgroundTextLineKeys =
    [
        "KING_OF_GREED.backgroundText.tyrantPath.0",
        "KING_OF_GREED.backgroundText.tyrantPath.1",
        "KING_OF_GREED.backgroundText.tyrantPath.2"
    ];

    private static readonly string[] MagicalGirlMoveIds =
    [
        ForHappinessMoveId,
        ShiningStrikeMoveId,
        VictoriousEuphoriaMoveId,
        OverwhelmingGloryMoveId,
        GoldenPathComboMoveId
    ];

    private static readonly string[] KingMoveIds =
    [
        HungerMoveId,
        GluttonyMoveId,
        CravingMoveId,
        ObsessionMoveId,
        TyrantPathComboMoveId
    ];

    private static readonly string KingOfGreedPageRelicTitleLocKey =
        $"{ModelDb.GetId<KingOfGreedPageRelic>().Entry}.title";

    public static readonly string[] AssetPathsStatic =
        new[]
        {
            KingOfGreedCreatureVisuals.ScenePath
        }
            .Concat(
            [
                GoldenAmber.SfxRoot + "awaken_king.ogg",
                GoldenAmber.SfxRoot + "awaken_magical_girl.ogg",
                GoldenAmber.SfxRoot + "gluttony_success.ogg",
                GoldenAmber.SfxRoot + "tyrant_path.ogg",
                GoldenAmber.SfxRoot + "king_stab.ogg",
                GoldenAmber.SfxRoot + "king_slash.ogg",
                GoldenAmber.SfxRoot + "transform_to_king.ogg",
                GoldenAmber.SfxRoot + "self_intoxication.ogg",
                GoldenAmber.SfxRoot + "golden_path.ogg",
                GoldenAmber.SfxRoot + "magical_girl_stab.ogg",
                GoldenAmber.SfxRoot + "magical_girl_slash.ogg",
                KingOfGreedAssets.GoldenAmberPowerIcon,
                KingOfGreedAssets.FlickeringDesirePowerIcon,
                KingOfGreedAssets.SelfIntoxicationPowerIcon,
                KingOfGreedAssets.MomentaryHappinessPowerIcon,
                KingOfGreedAssets.PassivePowerIcon,
                KingOfGreedAssets.GluttonyPowerIcon,
                KingOfGreedAssets.ShiningHappinessPowerIcon
            ])
            .ToArray();

    private const float BackgroundTextIntervalSeconds = 5f;
    private static readonly Rect2 BackgroundTextSpawnArea = new(150f, 200f, 900f, 450f);

    private bool _isMagicalGirl = true;
    private bool _transformPending;
    private bool _flickeringDesireTriggered;
    private int _cycleIndex;
    private bool _pendingSelfIntoxication;
    private bool _pendingMomentaryHappiness;
    private Dictionary<Creature, int> _pendingEndTurnVulnerableByTarget = [];
    private int _pendingEndTurnStrong;
    private bool _backgroundMoonTextLoopStarted;
    private int _attackAnimCursor;
    private int _immediateBackgroundTextCursor;
    private int _turnsSinceLastSummon;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _pendingEndTurnVulnerableByTarget = new(
            _pendingEndTurnVulnerableByTarget);
    }

    public bool IsMagicalGirl => _isMagicalGirl;

    public bool IsTransformPending => _transformPending;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 506, 482);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 510, 485);

    public override int DefaultChaoResistance => 210;

    public override float DeathAnimLengthOverride => LayeredBossSpine.DeathLength(this);

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override IEnumerable<string> AssetPaths =>
        AssetPathsStatic
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    // 混乱恢复统一回到路由：群攻失败触发的混乱发生在抽取下一招之前，而混乱锁生效后
    // 无法再改写恢复招式，只有恢复时重新经过路由才能按推进后的周期选招。
    public override string? StunRecoveryStateId => RouterStateId;

    internal void ConfigureInitialForm(bool magicalGirl)
    {
        AssertMutable();
        _isMagicalGirl = magicalGirl;
    }

    internal void MarkFlickeringDesireTriggered()
    {
        AssertMutable();
        _flickeringDesireTriggered = true;
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _cycleIndex = 0;
        _transformPending = false;
        _flickeringDesireTriggered = false;
        _pendingSelfIntoxication = false;
        _pendingMomentaryHappiness = false;
        ClearPendingEndTurnPowers();
        _backgroundMoonTextLoopStarted = false;
        _attackAnimCursor = 0;
        _immediateBackgroundTextCursor = 0;
        _turnsSinceLastSummon = SummonCycleInterval;
        await ApplyFormPowers();
        StartBackgroundMoonTextLoop(_isMagicalGirl ? MagicalGirlBackgroundTextLineKeys : KingBackgroundTextLineKeys);
    }

    public override void BeforeRemovedFromRoom()
    {
        base.BeforeRemovedFromRoom();
    }

    public override Task BeforeCombatStart()
    {
        if (!Creature.IsDead && !_backgroundMoonTextLoopStarted)
        {
            _backgroundMoonTextLoopStarted = true;
            StartBackgroundMoonTextLoop(_isMagicalGirl ? MagicalGirlBackgroundTextLineKeys : KingBackgroundTextLineKeys);
        }

        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (Creature.IsDead)
        {
            return;
        }

        if (side == CombatSide.Player)
        {
            if (_pendingMomentaryHappiness)
            {
                _pendingMomentaryHappiness = false;
                await TriggerMomentaryHappinessStagger();
            }

            return;
        }

        if (side != CombatSide.Enemy)
        {
            return;
        }

        if (_isMagicalGirl
            && !_transformPending
            && (_flickeringDesireTriggered || IsFlickeringDesireFloorReached))
        {
            await TransformToKingForm();
        }
    }

    // 触发标记只在生命变化事件里设置；若生命在没有事件的情况下已处于锁血下限
    // （例如生命上限被改动），仅靠标记会让锁血免伤一直持续。锁血生效就必须变身。
    private bool IsFlickeringDesireFloorReached =>
        Creature.CurrentHp <= LibraryOfRuinaFlickeringDesirePower.GetMinimumHp(Creature);

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented || creature != Creature)
        {
            return Task.CompletedTask;
        }

        AddPageRewardsFromDeathHook(creature);
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEndLate(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy)
        {
            return;
        }

        if (Creature.IsDead)
        {
            ClearPendingEndTurnPowers();
            _pendingSelfIntoxication = false;
            return;
        }

        await FlushPendingEndTurnPowers();
        await FlushPendingSelfIntoxication();
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var goldenCombo = new MoveState(
            GoldenPathComboMoveId,
            GoldenPathComboMove,
            new CombinedAttackDebuffIntent(
                () => GoldenPathDamage,
                () => 1,
                "KING_OF_GREED_GOLDEN_PATH.description",
                IndiscriminateAttackIntent.CreateGroupAttackBadge(),
                IntentBadge.Confusion(1)));
        var tyrantCombo = new MoveState(
            TyrantPathComboMoveId,
            TyrantPathComboMove,
            new CombinedAttackBuffIntent(
                () => TyrantPathDamage,
                () => 3,
                "KING_OF_GREED_TYRANT_PATH.description",
                IndiscriminateAttackIntent.CreateGroupAttackBadge(),
                IntentBadge.Heal(() => ScaledHealBadgeAmount(TyrantHeal))));

        var forHappiness = BuildNormalMove(ForHappinessMoveId, ForHappinessMove,
            new CombinedAttackDefendIntent(
                ForHappinessDamage,
                2,
                "KING_OF_GREED_FOR_HAPPINESS.description",
                ForHappinessBlock));
        var shiningStrike = BuildNormalMove(ShiningStrikeMoveId, ShiningStrikeMove,
            new CombinedAttackDebuffIntent(
                ShiningStrikeDamage,
                1,
                "KING_OF_GREED_SHINING_STRIKE.description",
                IntentBadge.Flaw(3, 1),
                IntentBadge.Bind(3)));
        var victorious = BuildNormalMove(VictoriousEuphoriaMoveId, VictoriousEuphoriaMove,
            new CombinedAttackDebuffIntent(
                VictoriousDamage,
                2,
                "KING_OF_GREED_VICTORIOUS_EUPHORIA.description",
                IntentBadge.FromPower<StrengthPower>(() => -VictoriousStrengthLoss),
                IntentBadge.FromPower<DexterityPower>(() => -VictoriousStrengthLoss),
                IntentBadge.Strength(1)));
        var overwhelming = BuildNormalMove(OverwhelmingGloryMoveId, OverwhelmingGloryMove,
            new SingleAttackIntent(OverwhelmingFirstDamage),
            new CombinedAttackDebuffIntent(
                OverwhelmingSecondDamage,
                1,
                "KING_OF_GREED_OVERWHELMING_GLORY.description",
                IntentBadge.FromPower<LibraryOfRuinaDrawCardsNextTurnPower>(2)));

        var hunger = BuildNormalMove(HungerMoveId, HungerMove,
            new CombinedAttackDebuffIntent(
                HungerDamage,
                2,
                "KING_OF_GREED_HUNGER.description",
                IntentBadge.Bleed(3)));
        var gluttony = BuildNormalMove(GluttonyMoveId, GluttonyMove,
            new CombinedAttackDebuffIntent(
                GluttonyFirstDamage,
                1,
                "KING_OF_GREED_GLUTTONY_FIRST.description",
                IntentBadge.RapidWear(5, 1)),
            new CombinedAttackDebuffIntent(
                GluttonySecondDamage,
                2,
                "KING_OF_GREED_GLUTTONY_SECOND.description",
                IntentBadge.Heal(() => ScaledHealBadgeAmount(GluttonyHealPerHit)),
                IntentBadge.Bleed(4)));
        var craving = BuildNormalMove(CravingMoveId, CravingMove,
            new CombinedAttackBuffIntent(
                CravingFirstDamage,
                2,
                "KING_OF_GREED_CRAVING_FIRST.description",
                IntentBadge.FromPower<LibraryStrongPower>(2, "2",  "2")),
            new CombinedAttackDebuffIntent(
                CravingSecondDamage,
                1,
                "KING_OF_GREED_CRAVING_SECOND.description",
                IntentBadge.Bleed(5)));
        var obsession = BuildNormalMove(ObsessionMoveId, ObsessionMove,
            new CombinedAttackDebuffIntent(
                ObsessionDamage,
                1,
                "KING_OF_GREED_OBSESSION.description",
                IntentBadge.Bleed(7),
                IntentBadge.Strength(1)));
        var summonShiningHappiness = BuildNormalMove(
            SummonShiningHappinessMoveId,
            SummonShiningHappinessMove,
            new SummonIntent(),
            new HealIntent());

        var router = new DelegatingMonsterRouterState(
            RouterStateId,
            (_, _) => ResolvePlannedMoveId());
        foreach (MoveState state in new[]
                 {
                      goldenCombo, tyrantCombo, forHappiness, shiningStrike, victorious, overwhelming,
                      hunger, gluttony, craving, obsession, summonShiningHappiness
                 })
        {
            state.FollowUpState = router;
        }

        return new MonsterMoveStateMachine(
            [
                goldenCombo, tyrantCombo, forHappiness, shiningStrike, victorious, overwhelming,
                hunger, gluttony, craving, obsession, summonShiningHappiness, router
            ],
            router);
    }

    private static MoveState BuildNormalMove(string id, Func<IReadOnlyList<Creature>, Task> move, params AbstractIntent[] intents) =>
        new(id, move, intents);

    private int ForHappinessDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 9, 8);

    private int ForHappinessBlock => 16;

    private int ShiningStrikeDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 19, 18);

    private int VictoriousDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 15, 13);

    private int VictoriousStrengthLoss =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);

    private int OverwhelmingFirstDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 12, 11);

    private int OverwhelmingSecondDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 14, 13);

    private int GoldenPathDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 14, 13);

    private int HungerDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 13, 11);

    private int GluttonyFirstDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 17, 15);

    private int GluttonySecondDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 14, 12);

    private int GluttonyHealPerHit => 12;

    private int CravingFirstDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 11, 10);

    private int CravingSecondDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 14, 13);

    private int ObsessionDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 30, 28);

    private int TyrantPathDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 12, 10);

    private int TyrantHeal => 25;

    private int ScaledHealBadgeAmount(int amount) =>
        (int)MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Creature, amount);

    private async Task GoldenPathComboMove(IReadOnlyList<Creature> targets)
    {
        await TriggerSpecialAnim();
        ShowImmediateBackgroundMoonText(GoldenPathBackgroundTextLineKeys);
        bool anyHit = await ExecuteGroupAttack(GoldenPathDamage, 1, GoldenAmber.SfxRoot + "golden_path.ogg");
        ShowImmediateBackgroundMoonText(anyHit ? AttackSuccessBackgroundTextLineKeys : AttackFailureBackgroundTextLineKeys);
        RegisterGroupAttackOutcome(anyHit);
        await ApplyConfusionToAllPlayers(1);
        AdvanceCycle();
    }

    private async Task TyrantPathComboMove(IReadOnlyList<Creature> targets)
    {
        await TriggerSpecialAnim();
        ShowImmediateBackgroundMoonText(TyrantPathBackgroundTextLineKeys);
        bool anyHit = false;
        bool allHitsLanded = true;
        for (int i = 0; i < 3; i++)
        {
            GroupAttackOutcome outcome = await ExecuteGroupAttackDetailed(TyrantPathDamage, 1, GoldenAmber.SfxRoot + "tyrant_path.ogg");
            anyHit |= outcome.AnyUnblocked;
            allHitsLanded &= outcome.AllUnblocked;
        }

        RegisterGroupAttackOutcome(anyHit);
        if (allHitsLanded)
        {
            await CreatureCmd.Heal(Creature, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Creature, TyrantHeal));
        }

        AdvanceCycle();
    }

    private async Task ForHappinessMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < 2; i++)
        {
            await ExecuteStandardGroupAttackDetailed(ForHappinessDamage);
        }

        await CreatureCmd.GainBlock(Creature, ForHappinessBlock, ValueProp.Move, null);
        AdvanceCycle();
    }

    private async Task ShiningStrikeMove(IReadOnlyList<Creature> targets)
    {
        GroupAttackOutcome attack = await ExecuteStandardGroupAttackDetailed(ShiningStrikeDamage);
        foreach (Creature target in attack.Targets)
        {
            await LibraryPowerCmd.Apply<LibraryDisarmPower>(target, 3, 1, Creature, null);
            await PowerCmdCompat.Apply<LibraryBindingPower>(target, 3m, Creature, null);
        }

        AdvanceCycle();
    }

    private async Task VictoriousEuphoriaMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < 2; i++)
        {
            await ExecuteStandardGroupAttackDetailed(VictoriousDamage);
        }
        decimal loss = VictoriousStrengthLoss;
        foreach (Creature target in GetLivingPlayers())
        {
            await PowerCmdCompat.Apply<StrengthPower>(target, -loss, Creature, null);
            await PowerCmdCompat.Apply<DexterityPower>(target, -loss, Creature, null);
        }

        await PowerCmdCompat.Apply<StrengthPower>(Creature, 1m, Creature, null);
        AdvanceCycle();
    }

    private async Task OverwhelmingGloryMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteStandardGroupAttackDetailed(OverwhelmingFirstDamage);
        await ExecuteStandardGroupAttackDetailed(OverwhelmingSecondDamage);
        await ApplyDrawReductionToAllPlayers(2);
        AdvanceCycle();
    }

    private async Task HungerMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < 2; i++)
        {
            GroupAttackOutcome attack = await ExecuteStandardGroupAttackDetailed(HungerDamage);
            foreach (Creature target in attack.UnblockedTargets)
            {
                await ApplyBleed(target, 3);
            }
        }

        AdvanceCycle();
    }

    private async Task GluttonyMove(IReadOnlyList<Creature> targets)
    {
        GroupAttackOutcome firstAttack = await ExecuteStandardGroupAttackDetailed(GluttonyFirstDamage);
        foreach (Creature target in firstAttack.Targets)
        {
            QueueEndTurnVulnerable(target, 5);
        }

        for (int i = 0; i < 2; i++)
        {
            GroupAttackOutcome attack = await ExecuteStandardGroupAttackDetailed(GluttonySecondDamage);
            foreach (DamageResult result in attack.UnblockedResults)
            {
                await CreatureCmd.Heal(Creature, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Creature, GluttonyHealPerHit));
                await ApplyBleed(result.Receiver, 4);
            }
        }

        AdvanceCycle();
    }

    private async Task CravingMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < 2; i++)
        {
            GroupAttackOutcome attack = await ExecuteStandardGroupAttackDetailed(CravingFirstDamage);
            if (attack.Results.Any(static result => result.BlockedDamage > 0 && !result.WasBlockBroken))
            {
                QueueEndTurnStrong(2);
            }
        }

        GroupAttackOutcome finalAttack = await ExecuteStandardGroupAttackDetailed(CravingSecondDamage);
        foreach (Creature target in finalAttack.UnblockedTargets)
        {
            await ApplyBleed(target, 5);
        }

        AdvanceCycle();
    }

    private async Task ObsessionMove(IReadOnlyList<Creature> targets)
    {
        GroupAttackOutcome attack = await ExecuteStandardGroupAttackDetailed(ObsessionDamage);
        foreach (Creature target in attack.UnblockedTargets)
        {
            await ApplyBleed(target, 7);
        }

        await PowerCmdCompat.Apply<StrengthPower>(Creature, 1m, Creature, null);
        AdvanceCycle();
    }

    private async Task SummonShiningHappinessMove(IReadOnlyList<Creature> targets)
    {
        bool hasSummonSlot = TryGetShiningHappinessSlot(out string slot);

        // Keep safety recovery from routing a failed summon attempt back into the same move forever.
        if (hasSummonSlot)
        {
            _turnsSinceLastSummon = 0;
        }
        AdvanceCycle();

        decimal healAmount = Creature.MaxHp * 0.10m;
        await CreatureCmd.Heal(Creature, healAmount);
        if (!hasSummonSlot)
        {
            return;
        }

        LocalOggOneShotPlayer.Play(ShiningHappiness.SummonSfxPath, LocalSfxVolumeDb);

        var summon = ModelDb.Monster<ShiningHappiness>().ToMutable();
        await CreatureCmd.Add(summon, CombatState, CombatSide.Enemy, slot);
    }

    internal string ResolvePlannedMoveId()
    {
        if (ShouldSummonShiningHappiness())
        {
            return SummonShiningHappinessMoveId;
        }

        string[] pool = _isMagicalGirl ? MagicalGirlMoveIds : KingMoveIds;
        return pool[Math.Clamp(_cycleIndex, 0, pool.Length - 1)];
    }

    private void AdvanceCycle()
    {
        string[] pool = _isMagicalGirl ? MagicalGirlMoveIds : KingMoveIds;
        _cycleIndex = (_cycleIndex + 1) % pool.Length;
        _turnsSinceLastSummon++;
    }

    private async Task ApplyFormPowers()
    {
        if (_isMagicalGirl)
        {
            await PowerCmdCompat.Apply<LibraryOfRuinaFlickeringDesirePower>(Creature, 1m, Creature, null, silent: true);
            await PowerCmdCompat.Apply<LibraryOfRuinaSelfIntoxicationPower>(Creature, 1m, Creature, null, silent: true);
        }
        else
        {
            await PowerCmdCompat.Apply<LibraryOfRuinaKingOfGreedPassivePower>(Creature, 1m, Creature, null, silent: true);
            await PowerCmdCompat.Apply<LibraryOfRuinaGluttonyPower>(Creature, 1m, Creature, null, silent: true);
        }

        await PowerCmdCompat.Apply<LibraryOfRuinaMomentaryHappinessPower>(Creature, 1m, Creature, null, silent: true);
    }

    private async Task TransformToKingForm()
    {
        if (!_isMagicalGirl || Creature.IsDead)
        {
            return;
        }

        _transformPending = true;
        _flickeringDesireTriggered = false;
        _isMagicalGirl = false;
        _cycleIndex = 0;

        PresentationGuard.Run(() =>
        {
            if (CombatQueries.CreatureNodeOf(this)?.Visuals is KingOfGreedCreatureVisuals visuals)
            {
                visuals.SetKingForm(true);
            }

            LocalOggOneShotPlayer.Play(GoldenAmber.SfxRoot + "transform_to_king.ogg", LocalSfxVolumeDb);
            StartBackgroundMoonTextLoop(KingBackgroundTextLineKeys);
        }, "KingOfGreed king form visuals");

        await PowerCmd.Remove(Creature.GetPower<LibraryOfRuinaFlickeringDesirePower>());
        await PowerCmd.Remove(Creature.GetPower<LibraryOfRuinaSelfIntoxicationPower>());
        await PowerCmdCompat.Apply<LibraryOfRuinaKingOfGreedPassivePower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<LibraryOfRuinaGluttonyPower>(Creature, 1m, Creature, null, silent: true);
        await LibraryOfRuinaShiningHappinessPower.ReapplyAllAuraContributions(CombatState);
        _transformPending = false;
    }

    private async Task<bool> ExecuteGroupAttack(int damage, int hits, string? sfxPath = null)
    {
        return (await ExecuteGroupAttackDetailed(damage, hits, sfxPath)).AnyUnblocked;
    }

    private async Task<GroupAttackOutcome> ExecuteGroupAttackDetailed(int damage, int hits, string? sfxPath = null)
    {
        IReadOnlyList<Creature> players = GetLivingPlayers();
        if (players.Count == 0)
        {
            return new GroupAttackOutcome(players, [], false, false);
        }

        List<DamageResult> allResults = [];
        for (int i = 0; i < hits; i++)
        {
            using (TargetedMonsterAttackHelper.ForceTargets(Creature, players))
            {
                await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(this, damage, players);
                if (!string.IsNullOrWhiteSpace(sfxPath))
                {
                    LocalOggOneShotPlayer.Play(sfxPath, LocalSfxVolumeDb);
                }

                AttackCommand attack = await DamageCmd.Attack(damage)
                    .FromMonster(this)
                    .WithAttackerAnim("SpecialAttack", 1f)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .SpawningHitVfxOnEachCreature()
                    .WithIndiscriminateBlockBreak(this, damage, players)
                    .Execute(null);
                IReadOnlyList<DamageResult> results = AttackCommandCompat.Results(attack).ToArray();
                allResults.AddRange(results);
            }
        }

        return GroupAttackOutcome.From(players, allResults);
    }

    private async Task<GroupAttackOutcome> ExecuteStandardGroupAttackDetailed(int damage)
    {
        IReadOnlyList<Creature> players = GetLivingPlayers();
        if (players.Count == 0 || damage <= 0)
        {
            return new GroupAttackOutcome(players, [], false, false);
        }

        using (TargetedMonsterAttackHelper.ForceTargets(Creature, players))
        {
            await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(this, damage, players);
            PlayAttackSfx();
            AttackCommand attack = await DamageCmd.Attack(damage)
                .FromMonster(this)
                .WithAttackerAnim("Attack", AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .SpawningHitVfxOnEachCreature()
                .WithIndiscriminateBlockBreak(this, damage, players)
                .Execute(null);
            IReadOnlyList<DamageResult> results = AttackCommandCompat.Results(attack).ToArray();
            return GroupAttackOutcome.From(players, results);
        }
    }

    private void RegisterGroupAttackOutcome(bool anyUnblockedHit)
    {
        if (anyUnblockedHit && _isMagicalGirl)
        {
            _pendingSelfIntoxication = true;
        }
        else if (!anyUnblockedHit)
        {
            _pendingMomentaryHappiness = true;
        }
    }

    private Task TriggerMomentaryHappinessStagger()
    {
        if (Creature is not LibraryCreature libraryCreature || !libraryCreature.HasChaoResistance)
        {
            return Task.CompletedTask;
        }

        return LibraryCreatureCmd.SetCurrentChaoValue(libraryCreature, 0m);
    }

    private void QueueEndTurnVulnerable(Creature target, int amount)
    {
        if (amount <= 0 || target.IsDead)
        {
            return;
        }

        _pendingEndTurnVulnerableByTarget.TryGetValue(target, out int currentAmount);
        _pendingEndTurnVulnerableByTarget[target] = Math.Max(currentAmount, amount);
    }

    private void QueueEndTurnStrong(int amount)
    {
        if (amount > 0)
        {
            _pendingEndTurnStrong = Math.Max(_pendingEndTurnStrong, amount);
        }
    }

    private async Task FlushPendingEndTurnPowers()
    {
        KeyValuePair<Creature, int>[] vulnerableByTarget = _pendingEndTurnVulnerableByTarget.ToArray();
        int strong = _pendingEndTurnStrong;
        ClearPendingEndTurnPowers();

        foreach ((Creature target, int amount) in vulnerableByTarget)
        {
            if (target.IsAlive)
            {
                LibraryVulnerablePower? vulnerable = await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                    target,
                    amount,
                    turns: 2,
                    applier: Creature,
                    cardSource: null);
                if (vulnerable != null)
                {
                    vulnerable.SkipNextDurationTick = false;
                }
            }
        }

        if (strong > 0)
        {
            await LibraryPowerCmd.Apply<LibraryStrongPower>(
                Creature,
                strong,
                1,
                Creature,
                null);
        }
    }

    private async Task FlushPendingSelfIntoxication()
    {
        if (!_pendingSelfIntoxication)
        {
            return;
        }

        _pendingSelfIntoxication = false;
        LocalOggOneShotPlayer.Play(GoldenAmber.SfxRoot + "self_intoxication.ogg", LocalSfxVolumeDb);
        await LibraryPowerCmd.Apply<LibraryStrongPower>(
            Creature,
            LibraryOfRuinaSelfIntoxicationPower.StrongStacks,
            1,
            Creature,
            null);
        await LibraryPowerCmd.Apply<LibraryProtectionPower>(
            Creature,
            LibraryOfRuinaSelfIntoxicationPower.SwiftStacks,
            LibraryOfRuinaSelfIntoxicationPower.ProtectionTurns,
            Creature,
            null);
    }

    private void ClearPendingEndTurnPowers()
    {
        _pendingEndTurnVulnerableByTarget.Clear();
        _pendingEndTurnStrong = 0;
    }

    private Task ApplyBleed(Creature target, int stacks)
    {
        return PowerCmdCompat.Apply<LibraryBleedingPower>(target, stacks, Creature, null);
    }

    private async Task ApplyConfusionToAllPlayers(int turns)
    {
        foreach (Creature player in GetLivingPlayers())
        {
            await PowerCmdCompat.Apply<LibraryOfRuinaConfusionPower>(player, turns, Creature, null);
        }
    }

    private async Task ApplyDrawReductionToAllPlayers(int amount)
    {
        foreach (Creature player in GetLivingPlayers())
        {
            await PowerCmdCompat.ApplyDebuff<LibraryOfRuinaDrawCardsNextTurnPower>(player, amount, Creature, null);
        }
    }

    private List<Creature> GetLivingPlayers()
    {
        CombatStateLike? combatState = Creature.CombatState;
        return combatState?.LivingPlayerCreatures().ToList() ?? [];
    }

    private bool ShouldSummonShiningHappiness() =>
        _turnsSinceLastSummon >= SummonCycleInterval
        && TryGetShiningHappinessSlot(out _);

    private bool TryGetShiningHappinessSlot(out string slot)
    {
        slot = string.Empty;
        CombatStateLike? combatState = Creature.CombatState;
        if (combatState?.Encounter is not KingOfGreedElite)
        {
            return false;
        }

        foreach (string candidate in KingOfGreedElite.HappinessSlots)
        {
            bool occupied = combatState.Enemies.Any(
                creature => creature.IsAlive && creature.SlotName == candidate);
            if (!occupied)
            {
                slot = candidate;
                return true;
            }
        }

        return false;
    }

    private void PlayAttackSfx()
    {
        bool stab = _attackAnimCursor % 2 == 0;
        _attackAnimCursor++;
        string path = _isMagicalGirl
            ? (stab ? GoldenAmber.SfxRoot + "magical_girl_stab.ogg" : GoldenAmber.SfxRoot + "magical_girl_slash.ogg")
            : (stab ? GoldenAmber.SfxRoot + "king_stab.ogg" : GoldenAmber.SfxRoot + "king_slash.ogg");
        LocalOggOneShotPlayer.Play(path, LocalSfxVolumeDb);
    }

    private Task TriggerSpecialAnim() => CreatureCmd.TriggerAnim(Creature, "SpecialIntro", 1f);

    private static void StartBackgroundMoonTextLoop(IReadOnlyList<string> lineKeys)
    {
        MonsterMoonTextLoop.Start(lineKeys, BackgroundTextIntervalSeconds, BackgroundTextSpawnArea);
    }

    private void ShowImmediateBackgroundMoonText(IReadOnlyList<string> lineKeys)
    {
        if (lineKeys.Count == 0)
        {
            return;
        }

        string key = lineKeys[_immediateBackgroundTextCursor % lineKeys.Count];
        _immediateBackgroundTextCursor++;
        MoonTextService.StartSequence(
            [new MoonTextSequenceEntry(L10NMonsterLookup(key), 0f)]);
    }

    private void AddPageRewardsFromDeathHook(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom is not CombatRoom room
            || room.Encounter is not KingOfGreedElite)
        {
            return;
        }

        foreach (Player player in room.CombatState.Players)
        {
            TryAddKingOfGreedPageReward(
                room,
                player,
                room.ExtraRewards.TryGetValue(player, out List<Reward>? rewards) ? rewards : null);
        }
    }

    internal static bool TryAddKingOfGreedPageReward(AbstractRoom? room, Player player, ICollection<Reward>? rewards)
    {
        if (room is not CombatRoom combatRoom || combatRoom.Encounter is not KingOfGreedElite)
        {
            return false;
        }

        if (!AbnormalityPageRewardHelper.ShouldAddPageReward<KingOfGreedPageRelic>(
                combatRoom,
                player,
                rewards ?? [],
                KingOfGreedPageRelicTitleLocKey))
        {
            return false;
        }

        RelicReward reward = new(ModelDb.Relic<KingOfGreedPageRelic>().ToMutable(), player);
        if (rewards != null)
        {
            rewards.Add(reward);
        }
        else
        {
            combatRoom.AddExtraReward(player, reward);
        }

        Log.Info("[KingOfGreedRewards] Added King of Greed page reward for player " + player.NetId + ".");
        return true;
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        foreach (MonsterState state in stateMachine.States.Values)
        {
            if (state is MoveState moveState)
            {
                foreach (AbstractIntent intent in moveState.Intents)
                {
                    yield return intent;
                }
            }
        }
    }

    private sealed record GroupAttackOutcome(
        IReadOnlyList<Creature> Targets,
        IReadOnlyList<DamageResult> Results,
        bool AnyUnblocked,
        bool AllUnblocked)
    {
        public IEnumerable<DamageResult> UnblockedResults =>
            Results.Where(static result => result.UnblockedDamage > 0);

        public IEnumerable<Creature> UnblockedTargets =>
            UnblockedResults.Select(static result => result.Receiver);

        public static GroupAttackOutcome From(
            IReadOnlyList<Creature> targets,
            IReadOnlyList<DamageResult> results)
        {
            bool anyUnblocked = results.Any(static result => result.UnblockedDamage > 0);
            bool allUnblocked = targets.Count > 0
                && targets.All(target => results.Any(result => result.Receiver == target && result.UnblockedDamage > 0));

            return new GroupAttackOutcome(targets, results, anyUnblocked, allUnblocked);
        }
    }
}
