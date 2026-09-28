using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.BlueStar;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers;
using LibraryOfRuina.powers.BlueStar;
using LibraryOfRuina.visuals.BlueStar;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.BlueStar;

public sealed class BlueStarFollower : LorMonsterModel
{
    public const int RequiredFollowerCount = 3;
    public const int MaxHp = 500;
    public const int MaxChao = 120;
    public const int SelfDestructThresholdPercent = 10;
    public const int BasicAttackLowAscensionDamage = 10;
    public const int BasicAttackHighAscensionDamage = 12;
    public const int BasicBlock = 6;
    public const int ParalysisStacks = 3;
    public const int ParalysisTurns = 1;
    public const int ChaoHealPercent = 5;
    public const int ChaoHeal = 6;
    public const int StrengthGain = 3;
    public const int VoiceAttackLowAscensionDamage = 7;
    public const int VoiceAttackHighAscensionDamage = 10;
    public const int VoiceAttackHits = 2;
    public const int SelfDestructLowAscensionDamage = 24;
    public const int SelfDestructHighAscensionDamage = 30;
    public const int SelfDestructVulnerable = 1;

    public const string ForTheStarMoveId = "FOR_THE_BLUE_STAR";
    public const string FaithMoveId = "FAITH_WAS_NOT_WRONG";
    public const string SinnersMoveId = "WE_ARE_ALL_SINNERS";
    public const string HearVoiceMoveId = "I_HEAR_THE_STAR_VOICE";
    public const string SelfDestructMoveId = "BECOME_STARS_AND_MEET_AGAIN";
    private const string RouterStateId = "BLUE_STAR_FOLLOWER_ROUTER";

    public const string TextureRoot =
        "res://images/monsters/blue_star_follower/";
    public const string IdleTexturePath = TextureRoot + "idle.png";
    public const string BasicAttackTexturePath =
        TextureRoot + "basic_attack.png";
    public const string VoiceAttackOneTexturePath =
        TextureRoot + "voice_attack_1.png";
    public const string VoiceAttackTwoTexturePath =
        TextureRoot + "voice_attack_2.png";
    public const string HitTexturePath = TextureRoot + "hit.png";
    public const string GuardTexturePath = TextureRoot + "guard.png";

    public static readonly string[] AssetPathsStatic =
        BlueStarFollowerCreatureVisuals.AssetPaths
            .Concat(
            [
                BlueStarAltar.SubAttackSfxPath,
                BlueStarAltar.SuicideSfxPath,
                "res://images/powers/blue_star_follower_voice_power.png"
            ])
            .Distinct()
            .ToArray();

    private static readonly string[][] NormalCycles =
    [
        [ForTheStarMoveId, FaithMoveId, SinnersMoveId, HearVoiceMoveId],
        [FaithMoveId, ForTheStarMoveId, HearVoiceMoveId, SinnersMoveId],
        [HearVoiceMoveId, SinnersMoveId, ForTheStarMoveId, FaithMoveId]
    ];

    private Dictionary<string, MoveState> _statesById = [];
    private bool _startDialoguePlayed;
    private LibraryCreature? _chaoValueSource;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _statesById = [];
    }

    public override int MinInitialHp => MaxHp;

    public override int MaxInitialHp => MaxHp;

    public override int DefaultChaoResistance => MaxChao;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => ImmuneResistance();

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => ResistResistance();

    internal static int BasicAttackDamage => AscensionDamage(
        BasicAttackLowAscensionDamage,
        BasicAttackHighAscensionDamage);

    internal static int VoiceAttackDamage => AscensionDamage(
        VoiceAttackLowAscensionDamage,
        VoiceAttackHighAscensionDamage);

    internal static int SelfDestructDamage => AscensionDamage(
        SelfDestructLowAscensionDamage,
        SelfDestructHighAscensionDamage);

    public override IEnumerable<string> AssetPaths =>
        AssetPathsStatic
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _startDialoguePlayed = false;
        SubscribeToChaoValueChanges();
        EncounterBgmController.RegisterMonster(Creature);
        await ApplyRoleResistance();
        await PowerCmdCompat.Apply<MinionPower>(
            Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<BlueStarFollowerVoicePower>(
            Creature, 1m, Creature, null, silent: true);
        ForceRefreshMoveState();
    }

    public override void BeforeRemovedFromRoom()
    {
        UnsubscribeFromChaoValueChanges();
        base.BeforeRemovedFromRoom();
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == CombatSide.Player)
        {
            ForceRefreshMoveState();
        }
        else if (side == CombatSide.Enemy
                 && combatState.RoundNumber == 1
                 && !_startDialoguePlayed)
        {
            _startDialoguePlayed = true;
            PlayDialogue("START");
        }

        await base.BeforeSideTurnStart(
            choiceContext,
            side,
            participants,
            combatState);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _statesById.Clear();

        MoveState forTheStar = Register(new MoveState(
            ForTheStarMoveId,
            ForTheStarMove,
            new CombinedAttackDefendIntent(
                () => BasicAttackDamage,
                () => 1,
                "BLUE_STAR_FOR_THE_STAR.description",
                BasicBlock)));

        MoveState faith = Register(new MoveState(
            FaithMoveId,
            FaithMove,
            new DebuffIntent()));

        MoveState sinners = Register(new MoveState(
            SinnersMoveId,
            SinnersMove,
            new HealIntent(),
            new BuffIntent()));

        MoveState hearVoice = Register(new MoveState(
            HearVoiceMoveId,
            HearVoiceMove,
            new BlueStarDynamicMultiAttackIntent(
                () => VoiceAttackDamage,
                VoiceAttackHits)));

        MoveState selfDestruct = Register(new MoveState(
            SelfDestructMoveId,
            SelfDestructMove,
            new DeathBlowIntent(() => SelfDestructDamage),
            new DetailedDebuffIntent<FrailPower>(
                SelfDestructVulnerable)));

        var router = new DelegatingMonsterRouterState(
            RouterStateId,
            (_, _) => ResolvePlannedMoveId(),
            shouldAppearInLogs: true);
        foreach (MoveState state in _statesById.Values)
        {
            state.FollowUpState = router;
        }

        return new MonsterMoveStateMachine(
            [forTheStar, faith, sinners, hearVoice, selfDestruct, router],
            router);
    }

    internal string ResolvePlannedMoveId()
    {
        CombatStateLike? combatState = Creature?.CombatState;
        string moveId;
        if (IsAtOrBelowSelfDestructThreshold())
        {
            moveId = SelfDestructMoveId;
        }
        else if (BlueStarEncounterHelper.IsNovaRound(combatState))
        {
            moveId = HearVoiceMoveId;
        }
        else
        {
            int round = Math.Max(1, combatState?.RoundNumber ?? 1);
            moveId = ResolveNormalMoveId(Role, round);
        }

        return moveId;
    }

    internal void ForceRefreshMoveState()
    {
        if (!IsMutable || MoveStateMachine == null || _statesById.Count == 0)
        {
            return;
        }

        string moveId = ResolvePlannedMoveId();
        if (_statesById.TryGetValue(moveId, out MoveState? state))
        {
            SetMoveImmediate(state, forceTransition: true);
        }
    }

    internal bool IsAtOrBelowSelfDestructThreshold()
    {
        return Creature is LibraryCreature
        {
            MaxChaoValue: > 0
        } libraryCreature
        && ShouldSelfDestruct(
            libraryCreature.CurrentChaoValue,
            libraryCreature.MaxChaoValue);
    }

    private void SubscribeToChaoValueChanges()
    {
        UnsubscribeFromChaoValueChanges();
        if (Creature is not LibraryCreature libraryCreature)
        {
            return;
        }

        _chaoValueSource = libraryCreature;
        _chaoValueSource.CurrentChaoValueChanged += OnCurrentChaoValueChanged;
    }

    private void UnsubscribeFromChaoValueChanges()
    {
        if (_chaoValueSource == null)
        {
            return;
        }

        _chaoValueSource.CurrentChaoValueChanged -= OnCurrentChaoValueChanged;
        _chaoValueSource = null;
    }

    private void OnCurrentChaoValueChanged(int previousValue, int currentValue)
    {
        if (_chaoValueSource == null
            || Creature.IsDead
            || ShouldSelfDestruct(
                previousValue,
                _chaoValueSource.MaxChaoValue)
                == ShouldSelfDestruct(
                    currentValue,
                    _chaoValueSource.MaxChaoValue))
        {
            return;
        }

        ForceRefreshMoveState();
    }

    internal static bool ShouldSelfDestruct(
        int currentChao,
        int maxChao) =>
        maxChao > 0
        && currentChao * 100
            <= maxChao * SelfDestructThresholdPercent;

    internal static string ResolveNormalMoveId(
        BlueStarFollowerRole role,
        int roundNumber)
    {
        int round = Math.Max(1, roundNumber);
        int normalTurnIndex = Math.Max(
            0,
            round - 1 - round / BlueStarAltar.NovaInterval) % 4;
        return NormalCycles[(int)role][normalTurnIndex];
    }

    internal BlueStarFollowerRole Role =>
        BlueStarEncounterHelper.ResolveFollowerRole(Creature?.SlotName);

    private async Task ForTheStarMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(BlueStarAltar.SubAttackSfxPath, -4f);
        await DamageCmd.Attack(BasicAttackDamage)
            .FromMonster(this)
            .WithAttackerAnim(
                "BasicAttack",
                BlueStarFollowerAnimationContract.BasicAttackDurationSeconds)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
        await CreatureCmd.TriggerAnim(
            Creature,
            "Guard",
            BlueStarFollowerAnimationContract.GuardDurationSeconds);
        await CreatureCmd.GainBlock(
            Creature,
            BasicBlock,
            ValueProp.Move,
            null);
    }

    private async Task FaithMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(
            Creature,
            "Guard",
            BlueStarFollowerAnimationContract.GuardDurationSeconds);
        foreach (Creature target in targets.Where(static target => target.IsAlive))
        {
            await PowerCmdCompat.ApplyDebuff<LibraryOfRuinaParalysisPower>(
                target,
                ParalysisStacks,
                Creature,
                null);
            target.GetPower<LibraryOfRuinaParalysisPower>()
                ?.SetTurnsRemaining(ParalysisTurns);
        }
    }

    private async Task SinnersMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(
            Creature,
            "Guard",
            BlueStarFollowerAnimationContract.GuardDurationSeconds);
        if (Creature is LibraryCreature libraryCreature)
        {
            await LibraryCreatureCmd.HealChaoValue(
                libraryCreature,
                ChaoHeal);
        }

        await PowerCmdCompat.Apply<StrengthPower>(
            Creature,
            StrengthGain,
            Creature,
            null);
    }

    private async Task HearVoiceMove(IReadOnlyList<Creature> targets)
    {
        if (BlueStarEncounterHelper.IsNovaRound(Creature.CombatState))
        {
            PlayDialogue("NORMAL");
        }

        LocalOggOneShotPlayer.Play(BlueStarAltar.SubAttackSfxPath, -3f);
        await DamageCmd.Attack(VoiceAttackDamage)
            .FromMonster(this)
            .WithHitCount(VoiceAttackHits)
            .WithAttackerAnim(
                "VoiceAttack",
                BlueStarFollowerAnimationContract.VoiceAttackDurationSeconds)
            .WithHitFx("vfx/vfx_starry_impact")
            .Execute(null);
    }

    private async Task SelfDestructMove(IReadOnlyList<Creature> targets)
    {
        PlayDialogue("NORMAL");
        LocalOggOneShotPlayer.Play(BlueStarAltar.SuicideSfxPath, -1f);
        await CreatureCmd.TriggerAnim(
            Creature,
            "SelfDestruct",
            BlueStarFollowerAnimationContract.SelfDestructDurationSeconds);
        await DamageCmd.Attack(SelfDestructDamage)
            .FromMonster(this)
            .WithNoAttackerAnim()
            .WithHitFx("vfx/vfx_starry_impact")
            .Execute(null);

        Creature[] livingTargets = targets
            .Where(static target => target.IsAlive)
            .ToArray();
        if (livingTargets.Length > 0)
        {
            await PowerCmdCompat.ApplyDebuff<FrailPower>(
                livingTargets,
                SelfDestructVulnerable,
                Creature,
                null);
        }

        if (!Creature.IsDead)
        {
            await CreatureCmd.Kill(Creature);
        }
    }

    private async Task ApplyRoleResistance()
    {
        if (Creature is not LibraryCreature libraryCreature)
        {
            return;
        }

        LibraryDamageType fatalType = Role switch
        {
            BlueStarFollowerRole.Left => LibraryDamageType.Slash,
            BlueStarFollowerRole.Middle => LibraryDamageType.Pierce,
            _ => LibraryDamageType.Blunt
        };
        await LibraryCreatureCmd.SetChaoResistance(
            new ThrowingPlayerChoiceContext(),
            libraryCreature,
            Creature,
            fatalType,
            LibraryResistanceLevel.Fatal);
    }

    private void PlayDialogue(string phase)
    {
        int index = (int)Role + 1;
        TalkCmd.Play(
            new LocString(
                "monsters",
                $"BLUE_STAR_FOLLOWER.banter.{phase}_{index}"),
            Creature,
            VfxColor.Cyan,
            VfxDuration.Standard);
    }

    private static int AscensionDamage(
        int lowAscension,
        int highAscension) =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            highAscension,
            lowAscension);

    private MoveState Register(MoveState state)
    {
        _statesById[state.Id] = state;
        return state;
    }

    private static IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new CombinedAttackDefendIntent(
            BasicAttackHighAscensionDamage,
            1,
            "BLUE_STAR_FOR_THE_STAR.description",
            BasicBlock);
        yield return new DebuffIntent();
        yield return new HealIntent();
        yield return new BuffIntent();
        yield return new MultiAttackIntent(
            VoiceAttackHighAscensionDamage,
            VoiceAttackHits);
        yield return new DeathBlowIntent(
            () => SelfDestructHighAscensionDamage);
        yield return new DetailedDebuffIntent<VulnerablePower>(
            SelfDestructVulnerable,
            DetailedIntentScopeText.Target);
    }

    private static LibraryCreatureResistanceData.Resistance ImmuneResistance() =>
        new()
        {
            Slash = LibraryResistanceLevel.Immune,
            Pierce = LibraryResistanceLevel.Immune,
            Blunt = LibraryResistanceLevel.Immune
        };

    private static LibraryCreatureResistanceData.Resistance ResistResistance() => 
    new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Endure,
        Blunt = LibraryResistanceLevel.Endure
    };
}

internal sealed class BlueStarDynamicMultiAttackIntent : MultiAttackIntent
{
    internal BlueStarDynamicMultiAttackIntent(
        Func<int> damageCalc,
        int repeat)
        : base(0, repeat)
    {
        DamageCalc = () => damageCalc();
    }
}
