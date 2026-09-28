using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.JudgementBird;
using LibraryOfRuina.intents;
using LibraryOfRuina.intents.JudgementBird;
using LibraryOfRuina.monsters.ScorchedGirl;
using LibraryOfRuina.powers.JudgementBird;
using LibraryOfRuina.relics;
using LibraryOfRuina.relics.JudgementBird;
using LibraryOfRuina.visuals.JudgementBird;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.JudgementBird;

public sealed class JudgementBird : LorMonsterModel
{
    internal const int LowMinHp = 450;
    internal const int LowMaxHp = 460;
    internal const int HighMinHp = 550;
    internal const int HighMaxHp = 600;

    internal const int UnjustScaleLowSin = 6;
    internal const int UnjustScaleHighSin = 7;
    internal const int GazeOneBlock = 26;
    internal const int GazeOneLowSin = 3;
    internal const int GazeOneHighSin = 5;
    internal const int GazeTwoLowDamage = 27;
    internal const int GazeTwoHighDamage = 33;
    internal const int GazeTwoLowSin = 2;
    internal const int GazeTwoHighSin = 4;
    internal const int GazeThreeHealPercent = 10;
    internal const int GazeThreeLowSin = 4;
    internal const int GazeThreeHighSin = 6;

    internal const string GazeOneMoveId =
        "JUDGEMENT_BIRD_GAZE_ONE";
    internal const string GazeTwoMoveId =
        "JUDGEMENT_BIRD_GAZE_TWO";
    internal const string GazeThreeMoveId =
        "JUDGEMENT_BIRD_GAZE_THREE";
    internal const string JudgementMoveId =
        "JUDGEMENT_BIRD_JUDGEMENT";
    internal const string GazeRouterId =
        "JUDGEMENT_BIRD_GAZE_ROUTER";
    internal const string GazeRandomRouterId =
        "JUDGEMENT_BIRD_GAZE_RANDOM_ROUTER";

    internal const string TextureRoot =
        "res://images/monsters/judgement_bird/";
    internal const string IdleTexturePath = TextureRoot + "idle.png";
    internal const string FireTexturePath = TextureRoot + "fire.png";
    internal const string GuardTexturePath = TextureRoot + "guard.png";
    internal const string HitTexturePath = TextureRoot + "hit.png";

    internal const string SfxRoot =
        "res://audio/sfx/judgement_bird/";
    internal const string OnSfxPath = SfxRoot + "on.ogg";
    internal const string DownSfxPath = SfxRoot + "down.ogg";
    internal const string HangSfxPath = SfxRoot + "hang.ogg";
    internal const string StunSfxPath = SfxRoot + "stun.ogg";

    private static readonly string PageRelicTitleLocKey =
        $"{ModelDb.GetId<JudgementBirdPageRelic>().Entry}.title";

    private List<MoveState> _gazeStates = [];
    private MoveState? _judgementState;
    private bool _hasPlayedBattleStartDialogue;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] PlannedJudgementTargetCombatIds { get; private set; } = [];

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool FullOfEvilPending { get; private set; }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _gazeStates = [];
        _judgementState = null;
        PlannedJudgementTargetCombatIds =
            [.. PlannedJudgementTargetCombatIds];
    }

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            HighMinHp,
            LowMinHp);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            HighMaxHp,
            LowMaxHp);

    public override int DefaultChaoResistance => 400;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Endure,
            Pierce = LibraryResistanceLevel.Endure,
            Blunt = LibraryResistanceLevel.Endure
        };

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Endure,
            Pierce = LibraryResistanceLevel.Endure,
            Blunt = LibraryResistanceLevel.Endure
        };

    internal static int UnjustScaleSin => DeadlyValue(
        UnjustScaleLowSin,
        UnjustScaleHighSin);

    internal static int GazeOneSin => DeadlyValue(
        GazeOneLowSin,
        GazeOneHighSin);

    internal static int GazeTwoDamage => DeadlyValue(
        GazeTwoLowDamage,
        GazeTwoHighDamage);

    internal static int GazeTwoSin => DeadlyValue(
        GazeTwoLowSin,
        GazeTwoHighSin);

    internal static int GazeThreeSin => DeadlyValue(
        GazeThreeLowSin,
        GazeThreeHighSin);

    public override IEnumerable<string> AssetPaths =>
        JudgementBirdCreatureVisuals.AssetPaths
            .Concat(
            [
                OnSfxPath,
                DownSfxPath,
                HangSfxPath,
                StunSfxPath,
                JudgementBirdJudgementVideoController.VideoPath
            ])
            .Concat(base.AssetPaths.Skip(1))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _hasPlayedBattleStartDialogue = false;
        EncounterBgmController.RegisterMonster(Creature);

        await PowerCmdCompat.Apply<JudgementBirdUnjustScalePower>(
            Creature,
            1,
            Creature,
            null,
            silent: true);
        await PowerCmdCompat.Apply<JudgementBirdWeightOfSinPower>(
            Creature,
            1,
            Creature,
            null,
            silent: true);
        await PowerCmdCompat.Apply<JudgementBirdJudgementPower>(
            Creature,
            1,
            Creature,
            null,
            silent: true);
        await PowerCmdCompat.Apply<JudgementBirdFullOfEvilPower>(
            Creature,
            1,
            Creature,
            null,
            silent: true);
    }

    public override Task BeforeCombatStart()
    {
        if (!Creature.IsDead && !_hasPlayedBattleStartDialogue)
        {
            _hasPlayedBattleStartDialogue = true;
            ScorchedGirlDialogueHelper.Speak(
                this,
                "JUDGEMENT_BIRD.dialogue.battleStart");
        }

        return Task.CompletedTask;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _gazeStates.Clear();

        MoveState gazeOne = new(
            GazeOneMoveId,
            GazeOneMove,
            new JudgementBirdDefendDebuffIntent(
                GazeOneBlock,
                "JUDGEMENT_BIRD_GAZE_ONE.description",
                IntentBadge.FromPower<JudgementBirdSinPower>(
                    () => GazeOneSin)));
        MoveState gazeTwo = new(
            GazeTwoMoveId,
            GazeTwoMove,
            new CombinedAttackDebuffIntent(
                () => GazeTwoDamage,
                () => 1,
                "JUDGEMENT_BIRD_GAZE_TWO.description",
                IntentBadge.FromPower<JudgementBirdSinPower>(
                    () => GazeTwoSin)));
        MoveState gazeThree = new(
            GazeThreeMoveId,
            GazeThreeMove,
            new JudgementBirdHealDebuffIntent(
                GazeThreeHealPercent,
                "JUDGEMENT_BIRD_GAZE_THREE.description",
                IntentBadge.FromPower<JudgementBirdSinPower>(
                    () => GazeThreeSin)));

        _judgementState = new MoveState(
            JudgementMoveId,
            JudgementMove,
            new JudgementBirdJudgementIntent(
                ResolveJudgementIntentTargets),
            new DebuffIntent(true));

        var randomRouter = new RandomBranchState(GazeRandomRouterId);
        randomRouter.AddBranch(gazeOne, MoveRepeatType.CannotRepeat);
        randomRouter.AddBranch(gazeTwo, MoveRepeatType.CannotRepeat);
        randomRouter.AddBranch(gazeThree, MoveRepeatType.CannotRepeat);
        var router = new DelegatingMonsterRouterState(
            GazeRouterId,
            (_, _) => ResolveNextMoveId());

        gazeOne.FollowUpState = router;
        gazeTwo.FollowUpState = router;
        gazeThree.FollowUpState = router;
        _judgementState.FollowUpState = router;
        _gazeStates.AddRange([gazeOne, gazeTwo, gazeThree]);

        return new MonsterMoveStateMachine(
            [
                gazeOne,
                gazeTwo,
                gazeThree,
                _judgementState,
                randomRouter,
                router
            ],
            router);
    }

    internal Task QueueJudgementIfRequired()
    {
        RefreshJudgementTargetPlan();
        return Task.CompletedTask;
    }

    private string ResolveNextMoveId()
    {
        RefreshJudgementTargetPlan();
        return PlannedJudgementTargetCombatIds.Length > 0
            ? JudgementMoveId
            : GazeRandomRouterId;
    }

    private void RefreshJudgementTargetPlan()
    {
        if (Creature.IsDead
            || Creature.CombatState == null
            || _judgementState == null
            || FullOfEvilPending
            || Creature is LibraryCreature { IsChaoed: true })
        {
            PlannedJudgementTargetCombatIds = [];
            return;
        }

        int[] targets = Creature.CombatState.Creatures
            .Where(target =>
                target.IsAlive
                && target != Creature
                && JudgementBirdSinService
                    .IsEncounterTransferableSinTarget(target)
                && JudgementBirdSinService.GetTotal(target)
                    > target.CurrentHp)
            .OrderBy(static target => target.CombatId ?? uint.MaxValue)
            .Select(static target => target.CombatId is { } id
                && id <= int.MaxValue
                    ? (int?)id
                    : null)
            .Where(static id => id.HasValue)
            .Select(static id => id!.Value)
            .ToArray();
        if (targets.Length == 0)
        {
            PlannedJudgementTargetCombatIds = [];
            return;
        }

        PlannedJudgementTargetCombatIds = targets;
    }

    private IReadOnlyList<Creature> ResolveJudgementIntentTargets(
        Creature owner)
    {
        if (owner.CombatState == null)
        {
            return [];
        }

        HashSet<int> planned = PlannedJudgementTargetCombatIds.ToHashSet();
        Creature[] resolved = owner.CombatState.Creatures
            .Where(target =>
                target.IsAlive
                && target.CombatId is { } id
                && id <= int.MaxValue
                && planned.Contains((int)id))
            .OrderBy(static target => target.CombatId ?? uint.MaxValue)
            .ToArray();
        if (resolved.Length > 0)
        {
            return resolved;
        }

        return [];
    }

    private async Task GazeOneMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(DownSfxPath, -2f);
        await CreatureCmd.TriggerAnim(Creature, "Guard", 0.36f);
        await CreatureCmd.GainBlock(
            Creature,
            GazeOneBlock,
            ValueProp.Move,
            null);
        await ApplySinToLivingPlayers(
            new ThrowingPlayerChoiceContext(),
            GazeOneSin,
            targets);
    }

    private async Task GazeTwoMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(OnSfxPath, -2f);
        await DamageCmd.Attack(GazeTwoDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.48f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
        await ApplySinToLivingPlayers(
            new ThrowingPlayerChoiceContext(),
            GazeTwoSin,
            targets);
    }

    private async Task GazeThreeMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(DownSfxPath, -2f);
        await CreatureCmd.TriggerAnim(Creature, "Heal", 0.48f);
        int heal = Math.Max(
            1,
            (int)Math.Ceiling(
                Creature.MaxHp * GazeThreeHealPercent / 100m));
        await CreatureCmd.Heal(Creature, heal);
        await ApplySinToLivingPlayers(
            new ThrowingPlayerChoiceContext(),
            GazeThreeSin,
            targets);
    }

    private async Task JudgementMove(IReadOnlyList<Creature> targets)
    {
        ScorchedGirlDialogueHelper.Speak(
            this,
            "JUDGEMENT_BIRD.dialogue.judgement");

        await JudgementBirdJudgementVideoController.PlayAsync(
            async () =>
            {
                IReadOnlyList<Creature> judgementTargets =
                    ResolveJudgementIntentTargets(Creature);
                var choiceContext = new ThrowingPlayerChoiceContext();
                foreach (Creature target in judgementTargets)
                {
                    await JudgementBirdSinService.Double(
                        choiceContext,
                        target,
                        Creature);
                }

                PlannedJudgementTargetCombatIds = [];
                ScheduleFullOfEvil();
            });
    }

    internal void ScheduleFullOfEvil()
    {
        if (!Creature.IsDead)
        {
            FullOfEvilPending = true;
        }
    }

    internal async Task ResolveFullOfEvilAtPlayerTurnStart()
    {
        if (!FullOfEvilPending)
        {
            return;
        }

        FullOfEvilPending = false;
        PlannedJudgementTargetCombatIds = [];
        if (Creature.IsDead
            || _gazeStates.Count == 0
            || Creature is LibraryCreature { IsChaoed: true })
        {
            return;
        }

        MoveState postStun = _gazeStates[
            RunRng.MonsterAi.NextInt(_gazeStates.Count)];
        SetMoveImmediate(postStun, forceTransition: true);
        LocalOggOneShotPlayer.Play(StunSfxPath, -2f);

        if (Creature is LibraryCreature libraryCreature)
        {
            await LibraryCreatureCmd.Stun(
                libraryCreature,
                postStun.Id);
            return;
        }

        await CreatureCmd.Stun(Creature, postStun.Id);
    }

    private async Task ApplySinToLivingPlayers(
        PlayerChoiceContext choiceContext,
        int amount,
        IEnumerable<Creature> fallbackTargets)
    {
        IEnumerable<Creature> players =
            Creature.CombatState?.PlayerCreatures
            ?? fallbackTargets;
        foreach (Creature target in players
            .Where(static target => target.IsAlive)
            .OrderBy(static target => target.CombatId ?? uint.MaxValue))
        {
            await JudgementBirdSinService.ApplyTransferable(
                choiceContext,
                target,
                amount,
                Creature,
                null);
        }
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (!wasRemovalPrevented && creature == Creature)
        {
            AddPageRewards(creature);
        }

        return Task.CompletedTask;
    }

    private static void AddPageRewards(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom
                is not CombatRoom room
            || room.Encounter is not JudgementBirdElite)
        {
            return;
        }

        foreach (Player player in room.CombatState.Players)
        {
            if (!AbnormalityPageRewardHelper
                    .ShouldAddPageReward<JudgementBirdPageRelic>(
                        room,
                        player,
                        PageRelicTitleLocKey))
            {
                continue;
            }

            room.AddExtraReward(
                player,
                new RelicReward(
                    ModelDb.Relic<JudgementBirdPageRelic>()
                        .ToMutable(),
                    player));
        }
    }

    private static int DeadlyValue(int lowValue, int highValue) =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            highValue,
            lowValue);
}

public sealed class EscapedBird : LorMonsterModel
{
    internal const int LowMinHp = 190;
    internal const int LowMaxHp = 193;
    internal const int HighMinHp = 197;
    internal const int HighMaxHp = 200;
    internal const int PanicLowDamage = 6;
    internal const int PanicHighDamage = 8;
    internal const int PanicHits = 3;
    internal const int ScreamLowDamage = 5;
    internal const int ScreamHighDamage = 7;
    internal const int ScreamHits = 2;
    internal const int ScreamWeak = 2;
    internal const int ExtraTransferLow = 3;
    internal const int ExtraTransferHigh = 4;

    internal const string PanicMoveId = "ESCAPED_BIRD_PANIC_FLAP";
    internal const string ScreamMoveId =
        "ESCAPED_BIRD_MISERABLE_SCREAM";
    internal const string RouterId = "ESCAPED_BIRD_ROUTER";

    internal const string TextureRoot =
        "res://images/monsters/escaped_bird/";
    internal const string IdleTexturePath = TextureRoot + "idle.png";
    internal const string AttackTexturePath = TextureRoot + "attack.png";
    internal const string AttackTwoTexturePath =
        TextureRoot + "attack_2.png";
    internal const string HitTexturePath = TextureRoot + "hit.png";
    internal const string AttackSfxPath =
        JudgementBird.SfxRoot + "escaped_attack.ogg";
    internal const string ScreamSfxPath =
        JudgementBird.SfxRoot + "escaped_scream.ogg";

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            HighMinHp,
            LowMinHp);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            HighMaxHp,
            LowMaxHp);

    public override int DefaultChaoResistance => 70;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => new(
            LibraryResistanceLevel.Normal);

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => new(
            LibraryResistanceLevel.Normal);

    internal static int PanicDamage => DeadlyValue(
        PanicLowDamage,
        PanicHighDamage);

    internal static int ScreamDamage => DeadlyValue(
        ScreamLowDamage,
        ScreamHighDamage);

    internal static int ExtraTransfer => DeadlyValue(
        ExtraTransferLow,
        ExtraTransferHigh);

    public override IEnumerable<string> AssetPaths =>
        EscapedBirdCreatureVisuals.AssetPaths
            .Concat([AttackSfxPath, ScreamSfxPath])
            .Concat(base.AssetPaths.Skip(1))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<MinionPower>(
            Creature,
            1,
            Creature,
            null,
            silent: true);
        await PowerCmdCompat.Apply<EscapedBirdScaryPower>(
            Creature,
            1,
            Creature,
            null,
            silent: true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState panic = new(
            PanicMoveId,
            PanicMove,
            new MultiAttackIntent(PanicDamage, PanicHits));
        MoveState scream = new(
            ScreamMoveId,
            ScreamMove,
            new CombinedAttackDebuffIntent(
                () => ScreamDamage,
                () => ScreamHits,
                "ESCAPED_BIRD_MISERABLE_SCREAM.description",
                IntentBadge.Weak(ScreamWeak)));
        var router = new DelegatingMonsterRouterState(
            RouterId,
            (owner, _) => owner.SlotName
                == JudgementBirdElite.RightEscapedBirdSlot
                    ? ScreamMoveId
                    : PanicMoveId);

        panic.FollowUpState = scream;
        scream.FollowUpState = panic;
        return new MonsterMoveStateMachine(
            [panic, scream, router],
            router);
    }

    private async Task PanicMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        await DamageCmd.Attack(PanicDamage)
            .FromMonster(this)
            .WithHitCount(PanicHits)
            .WithAttackerAnim("Attack", 0.36f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private async Task ScreamMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(ScreamSfxPath, -2f);
        await DamageCmd.Attack(ScreamDamage)
            .FromMonster(this)
            .WithHitCount(ScreamHits)
            .WithAttackerAnim("Scream", 0.42f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        if (Creature.CombatState != null)
        {
            await PowerCmdCompat.ApplyDebuff<WeakPower>(
                Creature.CombatState.PlayerCreatures
                    .Where(static target => target.IsAlive),
                ScreamWeak,
                Creature,
                null);
        }
    }

    private static int DeadlyValue(int lowValue, int highValue) =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            highValue,
            lowValue);
}
