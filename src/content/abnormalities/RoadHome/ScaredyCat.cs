using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.RoadHome;

public sealed class ScaredyCat : LorMonsterModel
{
    internal const string HowlMoveId = "SCAREDY_CAT_HOWL";
    internal const string PurrMoveId = "SCAREDY_CAT_PURR";
    internal const string GrowlMoveId = "SCAREDY_CAT_GROWL";
    internal const string WarningShotMoveId = "SCAREDY_CAT_WARNING_SHOT";
    internal const string StaggeredMoveId = "SCAREDY_CAT_STAGGERED";
    private const string RouterStateId = "SCAREDY_CAT_ROUTER";

    public const int CourageStrong = 1;
    public const int OneTurnDuration = 1;
    public const int WarningShotPermanentStrong = 3;
    public const int HowlLowDamage = 9;
    public const int HowlHighDamage = 12;
    public const int HowlHits = 2;
    public const int PurrLowDamage = 6;
    public const int PurrHighDamage = 8;
    public const int PurrHits = 3;
    public const int PurrBlock = 14;
    public const int GrowlLowDamage = 29;
    public const int GrowlHighDamage = 31;
    public const int GrowlWeak = 2;
    public const int WarningShotLowDamage = 6;
    public const int WarningShotHighDamage = 10;
    public const int WarningShotHits = 4;
    public const int WarningShotRapidWear = 2;

    public const string IdleTexturePath = RoadHomeEncounterHelper.CatTextureRoot + "idle.png";
    public const string HitTexturePath = RoadHomeEncounterHelper.CatTextureRoot + "hit.png";
    public const string AttackStrikeTexturePath = RoadHomeEncounterHelper.CatTextureRoot + "attack_strike.png";
    public const string AttackSlashTexturePath = RoadHomeEncounterHelper.CatTextureRoot + "attack_slash.png";
    public const string AttackRangedTexturePath = RoadHomeEncounterHelper.CatTextureRoot + "attack_ranged.png";
    public const string AfterRoadHomeDefeatedIdleTexturePath = RoadHomeEncounterHelper.CatTextureRoot + "companion_idle.png";
    public const string AfterRoadHomeDefeatedHitTexturePath = RoadHomeEncounterHelper.CatTextureRoot + "companion_hit.png";
    public const string LionChangeSfxPath = RoadHomeEncounterHelper.SfxRoot + "lion_change.ogg";
    public const string LionPotionSfxPath = RoadHomeEncounterHelper.SfxRoot + "lion_potion.ogg";
    public const string LionAttackSfxPath = RoadHomeEncounterHelper.SfxRoot + "lion_attack.ogg";

    public static readonly string[] StaticAssetPaths =
        ScaredyCatCreatureVisuals
            .Profile.AssetPaths.ToArray();

    private Dictionary<string, MoveState> _statesById = [];
    private bool _afterRoadHomeDefeated;
    private string? _lastModeOneMoveId;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _statesById = [];
    }

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 397, 390);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 400, 393);

    public override int DefaultChaoResistance => 400;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => ImmuneResistance();

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => NormalResistance();

    public override IEnumerable<string> AssetPaths =>
        StaticAssetPaths
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _afterRoadHomeDefeated = false;
        _lastModeOneMoveId = null;
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<ScaredyCatCouragePower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Apply<ScaredyCatCowardPower>(Creature, 1, Creature, null, silent: true);
        await SetPhysicalResistances(LibraryResistanceLevel.Immune);
        await ForceRefreshMoveState();
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (_afterRoadHomeDefeated && side == CombatSide.Enemy && Creature is LibraryCreature libraryCreature)
        {
            await CreatureCmd.TriggerAnim(Creature, "AfterRoadHomeDefeated", 0f);
            await LibraryCreatureCmd.SetCurrentChaoValue(libraryCreature, 0m);
            await RefreshIntents();
        }

        await base.BeforeSideTurnStart(choiceContext, side, participants, combatState);
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (!wasRemovalPrevented && creature == Creature)
        {
            return RoadHomeEncounterHelper.OnCatDefeated(choiceContext, creature);
        }

        return Task.CompletedTask;
    }

    public async Task ApplyCourage()
    {
        if (Creature.IsDead || _afterRoadHomeDefeated)
        {
            return;
        }

        var strongPower = Creature.GetPowerInstances<LibraryStrongPower>().FirstOrDefault(static
            p => p.TurnsRemaining > 0);
        if (strongPower == null)
        {
            await LibraryPowerCmd.Apply<LibraryStrongPower>(
                new ThrowingPlayerChoiceContext(),
                Creature,
                CourageStrong,
                0,
                IsPermanent: false,
                Creature,
                null);
            return;
        }
        // else
        // {
        //     strongPower.SetTurnsRemaining(1, false);
        //     await PowerCmdCompat.ModifyAmount(strongPower, 1m, Creature, null);
        // }
        await LibraryPowerCmd.ModifyAmount(
            new ThrowingPlayerChoiceContext(),
            strongPower,
            CourageStrong,
            0,
            IsPermanent: false,
            Creature,
            null);
    }

    public async Task EnterAfterRoadHomeDefeatedState(PlayerChoiceContext choiceContext)
    {
        if (_afterRoadHomeDefeated || Creature.IsDead)
        {
            return;
        }

        _afterRoadHomeDefeated = true;
        LocalOggOneShotPlayer.Play(LionChangeSfxPath, -1f);
        await CreatureCmd.TriggerAnim(Creature, "AfterRoadHomeDefeated", 0f);
        await CreatureCmd.SetMaxAndCurrentHp(Creature, 20m);
        await SetPhysicalResistances(LibraryResistanceLevel.Fatal);
        await SetChaoResistances(LibraryResistanceLevel.Fatal);
        await ResetMoveStateForAfterRoadHomeDefeated();
        if (Creature is LibraryCreature libraryCreature)
        {
            await LibraryCreatureCmd.SetCurrentChaoValue(libraryCreature, 0m);
        }

        await RefreshIntents();
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _statesById.Clear();

        MoveState howl = Register(new MoveState(
            HowlMoveId,
            HowlMove,
            new MultiAttackIntent(GetHowlDamage(), HowlHits)));

        MoveState purr = Register(new MoveState(
            PurrMoveId,
            PurrMove,
            new CombinedAttackDefendIntent(
                () => GetPurrDamage(),
                () => PurrHits,
                "SCAREDY_CAT_PURR.description",
                PurrBlock)));

        MoveState growl = Register(new MoveState(
            GrowlMoveId,
            GrowlMove,
            new CombinedAttackDebuffIntent(
                () => GetGrowlDamage(),
                null,
                "SCAREDY_CAT_GROWL.description",
                IntentBadge.Weak(GrowlWeak))));

        MoveState warningShot = Register(new MoveState(
            WarningShotMoveId,
            WarningShotMove,
            new CombinedAttackDebuffIntent(
                () => GetWarningShotDamage(),
                () => WarningShotHits,
                "SCAREDY_CAT_WARNING_SHOT.description",
                IntentBadge.RapidWear(WarningShotRapidWear))));

        MoveState staggered = Register(new MoveState(
            StaggeredMoveId,
            _ => Task.CompletedTask,
            new StunIntent()));

        var router = new DelegatingMonsterRouterState(
            RouterStateId,
            (_, rng) => ResolvePlannedMoveId(rng));
        howl.FollowUpState = router;
        purr.FollowUpState = router;
        growl.FollowUpState = router;
        warningShot.FollowUpState = router;
        staggered.FollowUpState = router;

        return new MonsterMoveStateMachine([howl, purr, growl, warningShot, staggered, router], router);
    }

    internal string ResolvePlannedMoveId(Rng rng)
    {
        if (_afterRoadHomeDefeated)
        {
            return StaggeredMoveId;
        }

        Creature? roadHome = RoadHomeEncounterHelper.FindRoadHome(Creature.CombatState);
        if (roadHome is LibraryCreature { IsChaoed: true })
        {
            return WarningShotMoveId;
        }

        if (roadHome?.Monster is RoadHome { CurrentActionPattern: RoadHomeActionPattern.ModeTwo })
        {
            return GrowlMoveId;
        }

        string selected = _lastModeOneMoveId == HowlMoveId ? PurrMoveId : HowlMoveId;
        if (_lastModeOneMoveId == null && rng.NextInt(2) == 1)
        {
            selected = PurrMoveId;
        }

        _lastModeOneMoveId = selected;
        return selected;
    }

    private async Task HowlMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(LionAttackSfxPath, -2f);
        await DamageCmd.Attack(GetHowlDamage())
            .FromMonster(this)
            .WithHitCount(HowlHits)
            .WithAttackerAnim("AttackStrike", 0.24f)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    private async Task PurrMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(LionAttackSfxPath, -2f);
        await DamageCmd.Attack(GetPurrDamage())
            .FromMonster(this)
            .WithHitCount(PurrHits)
            .WithAttackerAnim("AttackSlash", 0.24f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        await CreatureCmd.GainBlock(Creature, PurrBlock, ValueProp.Move, null);
    }

    private async Task GrowlMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(LionAttackSfxPath, -2f);
        IReadOnlyList<DamageResult> results = AttackCommandCompat.Results(
            await DamageCmd.Attack(GetGrowlDamage())
                .FromMonster(this)
                .WithAttackerAnim("AttackStrike", 0.3f)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(null));
        foreach (Creature receiver in HitPlayers(results))
        {
            await PowerCmdCompat.ApplyDebuff<WeakPower>(receiver, GrowlWeak, Creature, null);
        }
    }

    private async Task WarningShotMove(IReadOnlyList<Creature> targets)
    {
        await LibraryPowerCmd.Apply<LibraryStrongPower>(
            Creature,
            WarningShotPermanentStrong,
            turns: -1,
            Creature,
            null);

        await WarningShotSegment();
        //await WarningShotSegment();
    }

    private async Task WarningShotSegment()
    {
        LocalOggOneShotPlayer.Play(LionPotionSfxPath, -2f);
        IReadOnlyList<DamageResult> results = AttackCommandCompat.Results(
            await DamageCmd.Attack(GetWarningShotDamage())
                .FromMonster(this)
                .WithHitCount(WarningShotHits)
                .WithAttackerAnim("Ranged", 0.22f)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(null));
        foreach (Creature receiver in HitPlayers(results))
        {
            await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                receiver,
                WarningShotRapidWear,
                turns: -1,
                Creature,
                null);
        }
    }

    private static IReadOnlyList<Creature> HitPlayers(IEnumerable<DamageResult> results) =>
        results
            .Select(static result => result.Receiver)
            .Where(static creature => creature.IsPlayer)
            .Distinct()
            .ToArray();

    private async Task ResetMoveStateForAfterRoadHomeDefeated()
    {
        ResetStateMachine();
        SetUpForCombat();
        await ForceRefreshMoveState();
    }

    private async Task ForceRefreshMoveState()
    {
        if (!IsMutable || MoveStateMachine == null || _statesById.Count == 0)
        {
            return;
        }

        string moveId = ResolvePlannedMoveId(RunRng.MonsterAi);
        if (_statesById.TryGetValue(moveId, out MoveState? state))
        {
            SetMoveImmediate(state, forceTransition: true);
            await RefreshIntents();
        }
    }

    private Task RefreshIntents() =>
        CombatQueries.CreatureNodeOf(this)?.RefreshIntents() ?? Task.CompletedTask;

    private int GetHowlDamage() => AscDamage(HowlLowDamage, HowlHighDamage);

    private int GetPurrDamage() => AscDamage(PurrLowDamage, PurrHighDamage);

    private int GetGrowlDamage() => AscDamage(GrowlLowDamage, GrowlHighDamage);

    private int GetWarningShotDamage() => AscDamage(WarningShotLowDamage, WarningShotHighDamage);

    private static int AscDamage(int low, int high) =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, high, low);

    private async Task SetPhysicalResistances(LibraryResistanceLevel level)
    {
        if (Creature is not LibraryCreature libraryCreature)
        {
            return;
        }

        var context = new ThrowingPlayerChoiceContext();
        await LibraryCreatureCmd.SetPhysicalResistance(context, libraryCreature, Creature, LibraryDamageType.Slash, level);
        await LibraryCreatureCmd.SetPhysicalResistance(context, libraryCreature, Creature, LibraryDamageType.Pierce, level);
        await LibraryCreatureCmd.SetPhysicalResistance(context, libraryCreature, Creature, LibraryDamageType.Blunt, level);
    }

    private async Task SetChaoResistances(LibraryResistanceLevel level)
    {
        if (Creature is not LibraryCreature libraryCreature)
        {
            return;
        }

        var context = new ThrowingPlayerChoiceContext();
        await LibraryCreatureCmd.SetChaoResistance(context, libraryCreature, Creature, LibraryDamageType.Slash, level);
        await LibraryCreatureCmd.SetChaoResistance(context, libraryCreature, Creature, LibraryDamageType.Pierce, level);
        await LibraryCreatureCmd.SetChaoResistance(context, libraryCreature, Creature, LibraryDamageType.Blunt, level);
    }

    private MoveState Register(MoveState state)
    {
        _statesById[state.Id] = state;
        return state;
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        foreach (MonsterState state in stateMachine.States.Values)
        {
            if (state is MoveState move)
            {
                foreach (AbstractIntent intent in move.Intents)
                {
                    yield return intent;
                }
            }
        }
    }

    private static LibraryCreatureResistanceData.Resistance ImmuneResistance() => new()
    {
        Slash = LibraryResistanceLevel.Immune,
        Pierce = LibraryResistanceLevel.Immune,
        Blunt = LibraryResistanceLevel.Immune
    };

    private static LibraryCreatureResistanceData.Resistance NormalResistance() => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

}
