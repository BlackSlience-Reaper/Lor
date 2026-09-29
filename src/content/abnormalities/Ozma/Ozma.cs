using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers;
using LibraryOfRuina.relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.Ozma;

public enum OzmaMode
{
    None = 0,
    Forget = 1,
    Interference = 2,
    GroupAttackOne = 3,
    GroupAttackTwo = 4
}

public sealed class Ozma : LorMonsterModel
{
    // 遗忘：单人时全队需累计击中真杰克的基础次数；联机时先乘以人数，再乘以对应人数倍率。
    internal const int RequiredTrueJackHits = 8;

    // 遗忘：单人时真杰克击中次数不额外放大。
    private const decimal SoloTrueJackHitMultiplier = 1m;

    // 遗忘：2 名玩家时，按人数放大后的真杰克击中次数额外倍率。
    private const decimal TwoPlayerTrueJackHitMultiplier = 1.2m;

    // 遗忘：3 名玩家时，按人数放大后的真杰克击中次数额外倍率。
    private const decimal ThreePlayerTrueJackHitMultiplier = 1.5m;

    // 遗忘：4 名及以上玩家时，按人数放大后的真杰克击中次数额外倍率。
    private const decimal FourPlayerTrueJackHitMultiplier = 1.8m;

    // 遗忘：挑战可经历的玩家回合数，下一玩家回合开始时仍未完成则判定失败。
    internal const int AllowedPlayerTurns = 3;

    // 遗忘：挑战失败时，每名存活的遗忘玩家失去当前生命的百分比。
    internal const int ForgottenFailureHpLossPercent = 50;

    private const string ForgetMoveId = "FORGET";
    private const string InterferenceMoveId = "INTERFERENCE";
    private const string PressureMoveId = "PRESSURE";
    private const string PainMoveId = "PAIN_OF_THE_ROBBED";
    private const string SorrowMoveId = "SORROW_OF_THE_ROBBED";

    internal const string TextureRoot = "res://images/monsters/ozma/";
    public const string IdleTexturePath = TextureRoot + "ozma_idle.png";
    public const string AttackTexturePath = TextureRoot + "ozma_attack.png";
    public const string HitTexturePath = TextureRoot + "ozma_hit.png";
    public const string GuardTexturePath = TextureRoot + "ozma_guard.png";
    public const string PainTexturePath = TextureRoot + "ozma_pain.png";
    public const string SorrowTexturePath = TextureRoot + "ozma_sorrow.png";

    private const string SfxRoot = "res://audio/sfx/ozma/";
    public const string SummonJackSfxPath = SfxRoot + "summon_jacks.ogg";
    public const string TrueJackGetCardSfxPath = SfxRoot + "true_jack_get_card.ogg";
    public const string StrongAttackStartSfxPath = SfxRoot + "strong_attack_start.ogg";
    public const string StrongAttackEndSfxPath = SfxRoot + "strong_attack_end.ogg";
    public const string InterferenceHitSfxPath = SfxRoot + "interference_hit.ogg";

    private static readonly string[] AssetPathsStatic =
        OzmaCreatureVisuals.Profile.AssetPaths
        .Concat(
        [
        SummonJackSfxPath,
        TrueJackGetCardSfxPath,
        OzmaTrueJackFlashOverlay.TexturePath,
        StrongAttackStartSfxPath,
        StrongAttackEndSfxPath,
        InterferenceHitSfxPath,
        "res://images/powers/ozma_forgotten_power.png",
        "res://images/powers/ozma_lost_memory_power.png",
        "res://images/powers/ozma_pain_passive_power.png",
        "res://images/powers/ozma_sorrow_passive_power.png"
        ])
        .ToArray();

    private static readonly string PageRelicTitleLocKey =
        $"{ModelDb.GetId<OzmaPageRelic>().Entry}.title";

    private List<SerializableCard> _forgottenOriginalCards = [];

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 520, 440);

    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 550, 450);

    public override int DefaultChaoResistance => 320;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => ResistAll();

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => ResistAll();

    public override IEnumerable<string> AssetPaths =>
        AssetPathsStatic
            .Concat(OzmaJack.AssetPathsStatic)
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public OzmaMode Mode { get; private set; }

    public int ForgetCount { get; private set; }

    public bool NextInterferenceUsesPressure { get; private set; }

    public int[] ForgottenPlayerCombatIds { get; private set; } = [];

    // 全体玩家共享的真杰克剩余击中次数，各玩家的遗忘层数只同步显示此值。
    public int TrueJackHitsRemaining { get; private set; }

    public int ForgottenPlayerTurnsStarted { get; private set; }

    // 全体玩家共享的本回合真杰克方位，每个玩家回合开始时重新抽取。
    public JackDirection TrueJackDirection { get; private set; }

    public bool StunAfterForgottenRestorePending { get; private set; }

    public List<SerializableCard> ForgottenOriginalCards
    {
        get => _forgottenOriginalCards;
        private set
        {
            AssertMutable();
            _forgottenOriginalCards.Clear();
            _forgottenOriginalCards.AddRange(value);
        }
    }

    // 与 ForgottenOriginalCards 按下标对应，记录每张被遗忘书页所属玩家的 CombatId。
    public int[] ForgottenOriginalOwnerCombatIds { get; private set; } = [];

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<OzmaPainPassivePower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<OzmaSorrowPassivePower>(Creature, 1m, Creature, null, silent: true);
        if (Mode == OzmaMode.None)
        {
            Mode = OzmaMode.Forget;
        }
        ForceRefreshMoveState();
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _forgottenOriginalCards = [.. _forgottenOriginalCards];
        ForgottenPlayerCombatIds = [.. ForgottenPlayerCombatIds];
        ForgottenOriginalOwnerCombatIds = [.. ForgottenOriginalOwnerCombatIds];
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (Mode == OzmaMode.Interference)
        {
            await UpdateForgottenChallengeAtSideTurnStart(choiceContext, side);
        }

        if (side == CombatSide.Enemy && Mode is OzmaMode.GroupAttackOne or OzmaMode.GroupAttackTwo)
        {
            await SetChaoResistance(choiceContext, LibraryResistanceLevel.Fatal);
        }

        await base.BeforeSideTurnStart(choiceContext, side, participants, combatState);
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented || creature != Creature)
        {
            return;
        }

        await CleanupForgottenChallenge(restoreCards: true);
        AddPageRewardsFromDeathHook();
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState forget = new(
            ForgetMoveId,
            ForgetMove,
            new IndiscriminateAttackIntent(
                GetLifePowderDamage,
                () => 1,
                "OZMA_LIFE_POWDER.description"),
            new SummonIntent(),
            new CombinedDefendDebuffIntent(
                21,
                "OZMA_FADING_MEMORY.description",
                IntentBadge.FromPower<OzmaForgottenPower>(GetRequiredTrueJackHits)));
        MoveState interference = new(
            InterferenceMoveId,
            InterferenceMove,
            new CombinedAttackDefendIntent(
                () => GetInterferenceDamage(),
                () => 2,
                "OZMA_INTERFERENCE.description",
                9));
        MoveState pressure = new(
            PressureMoveId,
            PressureMove,
            new CombinedAttackDebuffIntent(
                () => GetPressureDamage(),
                () => 1,
                "OZMA_PRESSURE.description",
                IntentBadge.FromPower<LibraryOfRuinaParalysisPower>(3),
                IntentBadge.FromPower<LibraryBindingPower>(3)));
        MoveState pain = new(
            PainMoveId,
            PainMove,
            new IndiscriminateAttackIntent(
                GetPainDamage,
                () => 4,
                "OZMA_PAIN.description",
                IntentBadge.Weak(9)));
        MoveState sorrow = new(
            SorrowMoveId,
            SorrowMove,
            new IndiscriminateAttackIntent(
                GetSorrowDamage,
                () => 1,
                "OZMA_SORROW.description"));

        var router = new DelegatingMonsterRouterState(
            "OZMA_ROUTER",
            (_, _) => ResolvePlannedMoveId());
        forget.FollowUpState = router;
        interference.FollowUpState = router;
        pressure.FollowUpState = router;
        pain.FollowUpState = router;
        sorrow.FollowUpState = router;
        return new MonsterMoveStateMachine(
            [forget, interference, pressure, pain, sorrow, router],
            router);
    }

    internal string ResolvePlannedMoveId()
    {
        return Mode switch
        {
            OzmaMode.Interference => NextInterferenceUsesPressure ? PressureMoveId : InterferenceMoveId,
            OzmaMode.GroupAttackOne => PainMoveId,
            OzmaMode.GroupAttackTwo => SorrowMoveId,
            _ => ForgetMoveId
        };
    }

    // 遗忘：全队共享的真杰克累计击中次数，按战斗人数放大后再乘以对应人数倍率，向上取整。
    internal static int ResolveRequiredTrueJackHits(CombatStateLike? combatState)
    {
        int playerCount = MultiplayerScalingPatchHelper.ResolveRunPlayerCount(combatState);
        decimal scaledHits =
            RequiredTrueJackHits
            * playerCount
            * GetTrueJackHitMultiplier(playerCount);
        return (int)decimal.Ceiling(scaledHits);
    }

    // 任意玩家攻击命中当前真杰克时推进全队共享进度，并为攻击者随机夺回一张自己的书页。
    internal async Task RegisterTrueJackHit(
        PlayerChoiceContext choiceContext,
        Creature attacker,
        OzmaJack jack)
    {
        if (Mode != OzmaMode.Interference
            || TrueJackHitsRemaining <= 0
            || TrueJackDirection == JackDirection.None
            || jack.Direction != TrueJackDirection)
        {
            return;
        }

        OzmaTrueJackFlashOverlay.Play();
        await RestoreRandomForgottenCard(attacker);
        TrueJackHitsRemaining--;
        SyncForgottenPowerAmounts();
        if (TrueJackHitsRemaining <= 0)
        {
            await ResolveForgottenChallenge(choiceContext, succeeded: true, killJacks: true);
        }
    }

    internal async Task FinalizeForgottenRestoreStun()
    {
        if (!StunAfterForgottenRestorePending)
        {
            return;
        }

        StunAfterForgottenRestorePending = false;
        if (Creature.IsDead)
        {
            return;
        }

        await CreatureCmd.Stun(Creature, ResolvePlannedMoveId());
    }

    private async Task ForgetMove(IReadOnlyList<Creature> targets)
    {
        ForgetCount++;
        LocalOggOneShotPlayer.Play(StrongAttackStartSfxPath, -1f);
        await ExecuteGroupAttack(GetLifePowderDamage(), hits: 1, "LifePowder");
        LocalOggOneShotPlayer.Play(StrongAttackEndSfxPath, -1f);

        await WakeOrResummonJacks();
        await CreatureCmd.TriggerAnim(Creature, "Guard", 0f);
        await CreatureCmd.GainBlock(Creature, 21m, ValueProp.Move, null);
        LocalOggOneShotPlayer.Play(InterferenceHitSfxPath, -1f);
        bool challengeStarted = await BeginForgottenChallenge();

        Mode = challengeStarted
            ? OzmaMode.Interference
            : ForgetCount >= 2
                ? OzmaMode.GroupAttackTwo
                : OzmaMode.Forget;
        NextInterferenceUsesPressure = RunRng.MonsterAi.NextBool();
    }

    private async Task InterferenceMove(IReadOnlyList<Creature> targets)
    {
        AttackCommand attack = await DamageCmd.Attack(GetInterferenceDamage())
            .FromMonster(this)
            .WithHitCount(2)
            .OnlyPlayAnimOnce()
            .WithAttackerAnim("Attack", AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
        LocalOggOneShotPlayer.Play(InterferenceHitSfxPath, -1f);
        await CreatureCmd.TriggerAnim(Creature, "Guard", 0f);
        await CreatureCmd.GainBlock(Creature, 9m, ValueProp.Move, null);
        NextInterferenceUsesPressure = true;
    }

    private async Task PressureMove(IReadOnlyList<Creature> targets)
    {
        AttackCommand attack = await DamageCmd.Attack(GetPressureDamage())
            .FromMonster(this)
            .WithAttackerAnim("Attack", AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
        LocalOggOneShotPlayer.Play(InterferenceHitSfxPath, -1f);
        foreach (Creature target in AttackCommandCompat.Results(attack)
            .Select(static result => result.Receiver)
            .Distinct())
        {
            await PowerCmdCompat.ApplyDebuff<LibraryOfRuinaParalysisPower>(
                target,
                3m,
                Creature,
                null);
            await LibraryPowerCmd.Apply<LibraryBindingPower>(
                target,
                3m,
                1,
                Creature,
                null);
        }

        NextInterferenceUsesPressure = false;
    }

    private async Task PainMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(StrongAttackStartSfxPath, -1f);
        GroupAttackOutcome attack = await ExecuteGroupAttack(GetPainDamage(), hits: 4, "Pain");
        LocalOggOneShotPlayer.Play(StrongAttackEndSfxPath, -1f);
        foreach (Creature target in attack.Targets.Distinct())
        {
            await LibraryPowerCmd.Apply<LibraryWeakPower>(
                target,
                9m,
                1,
                Creature,
                null);
        }

        await SetChaoResistance(new ThrowingPlayerChoiceContext(), LibraryResistanceLevel.Resist);
        Mode = OzmaMode.Forget;
    }

    private async Task SorrowMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(StrongAttackStartSfxPath, -1f);
        await ExecuteGroupAttack(GetSorrowDamage(), hits: 1, "Sorrow");
        LocalOggOneShotPlayer.Play(StrongAttackEndSfxPath, -1f);
        Mode = OzmaMode.GroupAttackTwo;
    }

    // 遗忘同时施加给全体存活玩家；全队共享真杰克击中进度与挑战回合数。
    private async Task<bool> BeginForgottenChallenge()
    {
        IReadOnlyList<Creature> livingPlayers = OzmaEncounterHelper.LivingPlayers(Creature.CombatState);
        int requiredHits = ResolveRequiredTrueJackHits(Creature.CombatState);
        List<Creature> forgottenPlayers = [];
        foreach (Creature target in livingPlayers)
        {
            if (await ApplyForgottenPowers(target, requiredHits))
            {
                forgottenPlayers.Add(target);
            }
        }

        ForgottenPlayerCombatIds = forgottenPlayers
            .Select(ToSavedCombatId)
            .ToArray();
        TrueJackHitsRemaining = forgottenPlayers.Count > 0 ? requiredHits : 0;
        ForgottenPlayerTurnsStarted = 0;
        TrueJackDirection = JackDirection.None;
        foreach (Creature target in forgottenPlayers)
        {
            await TransformForgottenCards(target);
        }

        return forgottenPlayers.Count > 0;
    }

    private async Task<bool> ApplyForgottenPowers(Creature target, int requiredHits)
    {
        // 人工制品每次只抵消一次施加，按其层数重试，使遗忘不会被其永久抵消。
        int attempts = (int)Math.Ceiling(target.GetPower<ArtifactPower>()?.Amount ?? 0m) + 1;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            OzmaForgottenPower? forgottenPower =
                await PowerCmdCompat.ApplyDebuff<OzmaForgottenPower>(
                    target,
                    requiredHits,
                    Creature,
                    null);
            if (forgottenPower == null)
            {
                continue;
            }

            OzmaLostMemoryPower? lostMemoryPower =
                await PowerCmdCompat.ApplyDebuff<OzmaLostMemoryPower>(
                    target,
                    1m,
                    Creature,
                    null);
            if (lostMemoryPower == null)
            {
                await PowerCmd.Remove(forgottenPower);
                continue;
            }

            return true;
        }

        return false;
    }

    // 遗忘：手牌、抽牌堆与弃牌堆中所有可变化的卡牌全部变为“还给我”，不设数量上限。
    private async Task TransformForgottenCards(Creature target)
    {
        Player? player = target.Player;
        if (player?.PlayerCombatState == null)
        {
            return;
        }

        CardModel[] candidates =
        [
            .. player.PlayerCombatState.Hand.Cards,
            .. player.PlayerCombatState.DrawPile.Cards,
            .. player.PlayerCombatState.DiscardPile.Cards
        ];
        foreach (CardModel original in candidates)
        {
            // 前一次变化的 Hook 可能移走候选牌，变化前再次确认仍可变化。
            if (!CanBecomeForgottenCard(original))
            {
                continue;
            }

            OzmaReturnItToMeCard replacement = CombatState.CreateCard<OzmaReturnItToMeCard>(player);
            replacement.SetOriginal(original);
            await CardCmd.Transform(original, replacement);
            AddForgottenOriginal(target, replacement.OriginalCard!);
        }
    }

    private static bool CanBecomeForgottenCard(CardModel card) =>
        card.Pile != null
        && card.Type != CardType.Quest
        && card.IsTransformable
        && card is not OzmaReturnItToMeCard;

    private async Task UpdateForgottenChallengeAtSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side)
    {
        IReadOnlyList<Creature> forgottenPlayers = FindForgottenCreatures();
        if (!forgottenPlayers.Any(static creature => creature.IsAlive))
        {
            await ResolveForgottenChallenge(choiceContext, succeeded: true);
            return;
        }

        bool forgottenPowerMissing = forgottenPlayers.Any(static creature =>
            creature.IsAlive
            && (creature.GetPower<OzmaForgottenPower>() == null
                || creature.GetPower<OzmaLostMemoryPower>() == null));
        if (forgottenPowerMissing)
        {
            // 遗忘或丢失的记忆被外部移除时，归还全部书页并对全体存活玩家重新开始挑战。
            await CleanupForgottenChallenge(restoreCards: true);
            if (!await BeginForgottenChallenge())
            {
                Mode = ForgetCount >= 2
                    ? OzmaMode.GroupAttackTwo
                    : OzmaMode.Forget;
                ForceRefreshMoveState();
                return;
            }
        }

        if (side == CombatSide.Player)
        {
            await BeginForgottenPlayerTurn(choiceContext);
        }
    }

    private async Task BeginForgottenPlayerTurn(PlayerChoiceContext choiceContext)
    {
        if (ForgottenPlayerTurnsStarted >= AllowedPlayerTurns)
        {
            await FailForgottenChallenge(choiceContext);
            return;
        }

        ForgottenPlayerTurnsStarted++;
        IReadOnlyList<Creature> jacks = OzmaEncounterHelper.LivingAwakeJacks(Creature.CombatState);
        if (jacks.Count == 0)
        {
            TrueJackDirection = JackDirection.None;
            return;
        }

        OzmaJack trueJack = (OzmaJack)RunRng.CombatTargets.NextItem(jacks)!.Monster!;
        TrueJackDirection = trueJack.Direction;
    }

    private async Task FailForgottenChallenge(PlayerChoiceContext choiceContext)
    {
        Creature[] livingForgottenPlayers = FindForgottenCreatures()
            .Where(static creature => creature.IsAlive)
            .ToArray();
        foreach (Creature forgotten in livingForgottenPlayers)
        {
            decimal hpLoss = Math.Ceiling(forgotten.CurrentHp * ForgottenFailureHpLossPercent / 100m);
            await CreatureCmdCompat.Damage(
                choiceContext,
                forgotten,
                hpLoss,
                ValueProp.Unblockable | ValueProp.Unpowered,
                Creature,
                null);
        }

        await ResolveForgottenChallenge(choiceContext, succeeded: false);
    }

    // 各玩家遗忘能力的层数仅用于显示共享进度；直接同步数值，避免每次命中为每名玩家追加命令等待。
    private void SyncForgottenPowerAmounts()
    {
        foreach (Creature forgotten in FindForgottenCreatures())
        {
            forgotten.GetPower<OzmaForgottenPower>()?.SetAmount(TrueJackHitsRemaining, silent: true);
        }
    }

    private async Task RestoreRandomForgottenCard(Creature owner)
    {
        if (owner.Player == null)
        {
            return;
        }

        List<int> remainingIndices = FindForgottenOriginalIndices(owner);
        while (remainingIndices.Count > 0)
        {
            int candidateIndex = RunRng.CombatCardSelection.NextInt(remainingIndices.Count);
            int ledgerIndex = remainingIndices[candidateIndex];
            remainingIndices.RemoveAt(candidateIndex);

            SerializableCard original = ForgottenOriginalCards[ledgerIndex];
            if (!await RestoreForgottenCard(owner.Player, original))
            {
                continue;
            }

            RemoveForgottenOriginalAt(ledgerIndex);
            return;
        }
    }

    private async Task WakeOrResummonJacks()
    {
        LocalOggOneShotPlayer.Play(SummonJackSfxPath, -1f);
        foreach (string slot in OzmaElite.JackSlots)
        {
            Creature? jackCreature = Creature.CombatState?.Enemies
                .FirstOrDefault(creature => creature.IsAlive
                    && creature.SlotName == slot
                    && creature.Monster is OzmaJack);
            if (jackCreature == null && Creature.CombatState != null)
            {
                jackCreature = await CreatureCmd.Add(
                    ModelDb.Monster<OzmaJack>().ToMutable(),
                    Creature.CombatState,
                    CombatSide.Enemy,
                    slot);
            }

            if (jackCreature?.Monster is OzmaJack jack)
            {
                await jack.Wake();
            }
        }
    }

    private async Task ResolveForgottenChallenge(
        PlayerChoiceContext choiceContext,
        bool succeeded,
        bool killJacks = false)
    {
        if (Mode != OzmaMode.Interference)
        {
            return;
        }

        if (killJacks)
        {
            IReadOnlyList<Creature> jacks = Creature.CombatState?.Enemies
                .Where(static creature => creature.IsAlive && creature.Monster is OzmaJack)
                .ToArray()
                ?? [];
            if (jacks.Count > 0)
            {
                await CreatureCmd.Kill(jacks);
            }
        }

        bool deferredRestorePending =
            await CleanupForgottenChallenge(restoreCards: true);
        Mode = ForgetCount >= 2
            ? OzmaMode.GroupAttackTwo
            : succeeded
                ? OzmaMode.GroupAttackOne
                : OzmaMode.Forget;
        ForceRefreshMoveState();

        StunAfterForgottenRestorePending = killJacks;
        if (killJacks && !deferredRestorePending)
        {
            await FinalizeForgottenRestoreStun();
        }
    }

    private async Task<bool> CleanupForgottenChallenge(bool restoreCards)
    {
        bool deferredRestorePending = false;
        foreach (Creature forgotten in FindForgottenCreatures())
        {
            if (restoreCards && forgotten.Player != null)
            {
                deferredRestorePending |=
                    await RestoreAllForgottenCards(forgotten);
            }

            await PowerCmdCompat.RemoveIfPresent<OzmaForgottenPower>(
                forgotten);
            await PowerCmdCompat.RemoveIfPresent<OzmaLostMemoryPower>(
                forgotten);
        }

        ForgottenOriginalCards.Clear();
        ForgottenOriginalOwnerCombatIds = [];
        ForgottenPlayerCombatIds = [];
        TrueJackHitsRemaining = 0;
        ForgottenPlayerTurnsStarted = 0;
        TrueJackDirection = JackDirection.None;
        return deferredRestorePending;
    }

    private IReadOnlyList<Creature> FindForgottenCreatures()
    {
        IEnumerable<Creature> playerCreatures =
            Creature.CombatState?.PlayerCreatures
            ?? [];
        if (ForgottenPlayerCombatIds.Length == 0)
        {
            return playerCreatures
                .Where(static creature => creature.GetPower<OzmaForgottenPower>() != null)
                .ToArray();
        }

        return playerCreatures
            .Where(creature => ForgottenPlayerCombatIds.Contains(ToSavedCombatId(creature)))
            .ToArray();
    }

    private static int ToSavedCombatId(Creature creature) =>
        creature.CombatId is { } combatId && combatId <= int.MaxValue
            ? (int)combatId
            : -1;

    private static decimal GetTrueJackHitMultiplier(int playerCount)
    {
        if (playerCount <= 1)
        {
            return SoloTrueJackHitMultiplier;
        }

        if (playerCount == 2)
        {
            return TwoPlayerTrueJackHitMultiplier;
        }

        if (playerCount == 3)
        {
            return ThreePlayerTrueJackHitMultiplier;
        }

        return FourPlayerTrueJackHitMultiplier;
    }

    private int GetRequiredTrueJackHits()
    {
        if (!IsMutable)
        {
            return RequiredTrueJackHits;
        }

        return ResolveRequiredTrueJackHits(Creature.CombatState);
    }

    private void AddForgottenOriginal(Creature owner, SerializableCard original)
    {
        ForgottenOriginalCards.Add(original);
        ForgottenOriginalOwnerCombatIds = [.. ForgottenOriginalOwnerCombatIds, ToSavedCombatId(owner)];
    }

    private List<int> FindForgottenOriginalIndices(Creature owner)
    {
        int ownerCombatId = ToSavedCombatId(owner);
        return Enumerable.Range(0, ForgottenOriginalCards.Count)
            .Where(index => index < ForgottenOriginalOwnerCombatIds.Length
                && ForgottenOriginalOwnerCombatIds[index] == ownerCombatId)
            .ToList();
    }

    private void RemoveForgottenOriginalAt(int index)
    {
        ForgottenOriginalCards.RemoveAt(index);
        if (index < ForgottenOriginalOwnerCombatIds.Length)
        {
            ForgottenOriginalOwnerCombatIds = ForgottenOriginalOwnerCombatIds
                .Where((_, ownerIndex) => ownerIndex != index)
                .ToArray();
        }
    }

    private static async Task<bool> RestoreForgottenCard(Player player, SerializableCard original)
    {
        OzmaReturnItToMeCard[] matchingReplacements = player.PlayerCombatState?.AllCards
            .OfType<OzmaReturnItToMeCard>()
            .Where(card => original.Equals(card.OriginalCard))
            .ToArray()
            ?? [];
        OzmaReturnItToMeCard? replacement = matchingReplacements
            .FirstOrDefault(static card => !card.RestoreAfterCurrentPlay);
        if (replacement != null)
        {
            return await replacement.RestoreOriginal();
        }
        if (matchingReplacements.Length > 0)
        {
            return false;
        }

        CardModel? restored = OzmaReturnItToMeCard.CreateRestoredCombatCard(player, original);
        if (restored != null)
        {
            await CardPileCmd.Add(restored, PileType.Discard);
            return true;
        }

        return false;
    }

    private async Task<bool> RestoreAllForgottenCards(Creature forgotten)
    {
        Player player = forgotten.Player!;
        bool deferredRestorePending = false;
        OzmaReturnItToMeCard[] replacements = player.PlayerCombatState?.AllCards
            .OfType<OzmaReturnItToMeCard>()
            .Where(static card => card.OriginalCard != null)
            .ToArray()
            ?? [];

        foreach (OzmaReturnItToMeCard replacement in replacements)
        {
            SerializableCard original = replacement.OriginalCard!;
            if (replacement.RestoreAfterCurrentPlay)
            {
                deferredRestorePending = true;
                continue;
            }

            if (!await replacement.RestoreOriginal())
            {
                continue;
            }

            RemoveOneForgottenOriginal(forgotten, original);
            deferredRestorePending |= replacement.RestoreAfterCurrentPlay;
        }

        SerializableCard[] remainingOriginals = FindForgottenOriginalIndices(forgotten)
            .Select(index => ForgottenOriginalCards[index])
            .ToArray();
        foreach (SerializableCard original in remainingOriginals)
        {
            CardModel? restored =
                OzmaReturnItToMeCard.CreateRestoredCombatCard(player, original);
            if (restored == null)
            {
                continue;
            }

            await CardPileCmd.Add(restored, PileType.Discard);
            RemoveOneForgottenOriginal(forgotten, original);
        }

        return deferredRestorePending;
    }

    private void RemoveOneForgottenOriginal(Creature owner, SerializableCard original)
    {
        int index = FindForgottenOriginalIndices(owner)
            .FirstOrDefault(ownerIndex => original.Equals(ForgottenOriginalCards[ownerIndex]), -1);
        if (index >= 0)
        {
            RemoveForgottenOriginalAt(index);
        }
    }

    private async Task<GroupAttackOutcome> ExecuteGroupAttack(int damage, int hits, string anim)
    {
        IReadOnlyList<Creature> players = OzmaEncounterHelper.LivingPlayers(Creature.CombatState);
        List<DamageResult> results = [];
        for (int i = 0; i < hits; i++)
        {
            using (TargetedMonsterAttackHelper.ForceTargets(Creature, players))
            {
                await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(this, damage, players);
                AttackCommand attack = await DamageCmd.Attack(damage)
                    .FromMonster(this)
                    .WithAttackerAnim(anim, AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .SpawningHitVfxOnEachCreature()
                    .WithIndiscriminateBlockBreak(this, damage, players)
                    .Execute(null);
                results.AddRange(AttackCommandCompat.Results(attack));
            }
        }

        return new GroupAttackOutcome(results);
    }

    private async Task SetChaoResistance(
        PlayerChoiceContext choiceContext,
        LibraryResistanceLevel level)
    {
        if (Creature is not LibraryCreature libraryCreature)
        {
            return;
        }

        await LibraryCreatureCmd.SetChaoResistance(
            choiceContext,
            libraryCreature,
            Creature,
            LibraryDamageType.Slash,
            level);
        await LibraryCreatureCmd.SetChaoResistance(
            choiceContext,
            libraryCreature,
            Creature,
            LibraryDamageType.Pierce,
            level);
        await LibraryCreatureCmd.SetChaoResistance(
            choiceContext,
            libraryCreature,
            Creature,
            LibraryDamageType.Blunt,
            level);
    }

    private int GetLifePowderDamage() => GetAscensionDamage(lowAscension: 25, highAscension: 43);

    private int GetInterferenceDamage() => GetAscensionDamage(lowAscension: 7, highAscension: 10);

    private int GetPressureDamage() => GetAscensionDamage(lowAscension: 8, highAscension: 10);

    private int GetPainDamage() => GetAscensionDamage(lowAscension: 8, highAscension: 11);

    private int GetSorrowDamage() => GetAscensionDamage(lowAscension: 40, highAscension: 50);

    private static int GetAscensionDamage(int lowAscension, int highAscension) =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            highAscension,
            lowAscension);

    private void ForceRefreshMoveState()
    {
        if (!IsMutable || MoveStateMachine == null)
        {
            return;
        }

        string moveId = ResolvePlannedMoveId();
        if (MoveStateMachine.States.TryGetValue(moveId, out MonsterState? state)
            && state is MoveState moveState)
        {
            SetMoveImmediate(moveState, forceTransition: true);
        }
    }

    private void AddPageRewardsFromDeathHook()
    {
        if (Creature.CombatState?.RunState.CurrentRoom is not CombatRoom room
            || room.Encounter is not OzmaElite)
        {
            return;
        }

        foreach (Player player in room.CombatState.Players)
        {
            if (AbnormalityPageRewardHelper.ShouldAddPageReward<OzmaPageRelic>(
                    room,
                    player,
                    PageRelicTitleLocKey))
            {
                room.AddExtraReward(
                    player,
                    new RelicReward(ModelDb.Relic<OzmaPageRelic>().ToMutable(), player));
            }
        }
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

    private static LibraryCreatureResistanceData.Resistance ResistAll() => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    private sealed record GroupAttackOutcome(IReadOnlyList<DamageResult> Results)
    {
        public IEnumerable<Creature> Targets =>
            Results.Select(static result => result.Receiver);
    }
}
