using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.audio;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.BlueStar;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers;
using LibraryOfRuina.powers.BlueStar;
using LibraryOfRuina.relics;
using LibraryOfRuina.relics.BlueStar;
using LibraryOfRuina.visuals.BlueStar;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.monsters.BlueStar;

public sealed class BlueStarAltar : LorMonsterModel
{
    public const int MaxHp = 1000;
    public const int MaxChao = 1000;
    public const int NovaInterval = 3;
    public const int NovaLowAscensionDamage = 12;
    public const int NovaHighAscensionDamage = 15;
    public const int NovaConfusion = 1;
    public const int NovaFollowerChaoHealPercent = 10;
    public const int ReturnHpLossPercent = 20;

    public const string SilentMoveId = "BLUE_STAR_SILENT";
    public const string NovaVoiceMoveId = "NOVA_VOICE";
    private const string RouterStateId = "BLUE_STAR_ALTAR_ROUTER";

    public const string TextureRoot = "res://images/monsters/blue_star_altar/";
    public const string IdleTexturePath = TextureRoot + "idle.png";
    public const string NovaTexturePath = TextureRoot + "nova.png";

    public const string AudioRoot = "res://audio/sfx/blue_star/";
    public const string BgmPath = AudioRoot + "blue_star_bgm.ogg";
    public const string AttackSfxPath = AudioRoot + "blue_star_attack.ogg";
    public const string CastSfxPath = AudioRoot + "blue_star_cast.ogg";
    public const string InSfxPath = AudioRoot + "blue_star_in.ogg";
    public const string SubAttackSfxPath = AudioRoot + "blue_star_sub_attack.ogg";
    public const string SuicideSfxPath = AudioRoot + "blue_star_suicide.ogg";
    public const string DeathSceneSfxPath = AudioRoot + "blue_star_death_scene.ogg";

    private static readonly string[] PowerIconPaths =
    [
        "res://images/powers/blue_star_divine_power.png",
        "res://images/powers/blue_star_nova_voice_power.png",
        "res://images/powers/blue_star_return_to_stars_power.png",
        "res://images/powers/blue_star_martyr_power.png",
        "res://images/powers/blue_star_follower_voice_power.png",
        "res://images/powers/blue_star_martyrdom_power.png"
    ];

    private static readonly string[] AudioPaths =
    [
        BgmPath,
        AttackSfxPath,
        CastSfxPath,
        InSfxPath,
        SubAttackSfxPath,
        SuicideSfxPath,
        DeathSceneSfxPath
    ];

    public static readonly string[] AssetPathsStatic =
        BlueStarAltarCreatureVisuals.AssetPaths
            .Concat(PowerIconPaths)
            .Concat(AudioPaths)
            .Distinct()
            .ToArray();

    private static readonly string PageRelicTitleLocKey =
        $"{ModelDb.GetId<BlueStarPageRelic>().Entry}.title";

    private Dictionary<string, MoveState> _statesById = [];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _statesById = [];
    }

    internal static int NovaDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            NovaHighAscensionDamage,
            NovaLowAscensionDamage);

    public override int MinInitialHp => MaxHp;

    public override int MaxInitialHp => MaxHp;

    public override int DefaultChaoResistance => MaxChao;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => NormalResistance();

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => NormalResistance();

    public override IEnumerable<string> AssetPaths =>
        AssetPathsStatic
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<BlueStarDivinePower>(
            Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<BlueStarNovaVoicePower>(
            Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<BlueStarReturnToStarsPower>(
            Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<BlueStarMartyrPower>(
            Creature, 1m, Creature, null, silent: true);
        ForceRefreshMoveState();
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

        await base.BeforeSideTurnStart(
            choiceContext,
            side,
            participants,
            combatState);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _statesById.Clear();

        MoveState silent = Register(new MoveState(
            SilentMoveId,
            static _ => Task.CompletedTask,
            new UnknownIntent()));

        MoveState nova = Register(new MoveState(
            NovaVoiceMoveId,
            NovaVoiceMove,
            new CombinedAttackDebuffIntent(
                () => NovaDamage,
                () => 1,
                "BLUE_STAR_NOVA_VOICE.description",
                IndiscriminateAttackIntent.CreateGroupAttackBadge(),
                IntentBadge.Confusion(NovaConfusion)),
            new HealIntent()));

        var router = new DelegatingMonsterRouterState(
            RouterStateId,
            (_, _) => ResolvePlannedMoveId(),
            shouldAppearInLogs: true);
        silent.FollowUpState = router;
        nova.FollowUpState = router;
        return new MonsterMoveStateMachine([silent, nova, router], router);
    }

    internal string ResolvePlannedMoveId()
    {
        if (!BlueStarEncounterHelper.IsNovaRound(Creature?.CombatState))
        {
            return SilentMoveId;
        }

        return NovaVoiceMoveId;
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
            UpdateVisualForMove(moveId);
        }
    }

    private async Task NovaVoiceMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(CastSfxPath, -1f);
        await CreatureCmd.TriggerAnim(
            Creature,
            "Nova",
            BlueStarAltarAnimationContract.NovaDurationSeconds);
        LocalOggOneShotPlayer.Play(AttackSfxPath, -1f);
        await DamageCmd.Attack(NovaDamage)
            .FromMonster(this)
            .WithNoAttackerAnim()
            .WithHitFx("vfx/vfx_starry_impact")
            .SpawningHitVfxOnEachCreature()
            .Execute(null);

        Creature[] livingPlayers = targets
            .Where(static target => target.IsAlive)
            .ToArray();
        if (livingPlayers.Length > 0)
        {
            await PowerCmdCompat.ApplyDebuff<LibraryOfRuinaConfusionPower>(
                livingPlayers,
                NovaConfusion,
                Creature,
                null);
        }

        foreach (Creature follower in
                 BlueStarEncounterHelper.LivingFollowers(Creature.CombatState))
        {
            if (follower is not LibraryCreature libraryFollower)
            {
                continue;
            }

            int heal = Math.Max(
                1,
                (int)Math.Ceiling(
                    libraryFollower.MaxChaoValue
                    * NovaFollowerChaoHealPercent
                    / 100m));
            await LibraryCreatureCmd.HealChaoValue(libraryFollower, heal);
        }

        UpdateVisualForMove(SilentMoveId);
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

        AddPageRewardsFromDeathHook(creature);
        LocalOggOneShotPlayer.Play(DeathSceneSfxPath, -1f);
        foreach (Creature follower in
                 BlueStarEncounterHelper.LivingFollowers(creature.CombatState))
        {
            await CreatureCmd.Kill(follower);
        }
    }

    private void AddPageRewardsFromDeathHook(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom is not CombatRoom room
            || !BlueStarEncounterHelper.IsBlueStarEncounter(
                deadCreature.CombatState))
        {
            return;
        }

        foreach (Player player in room.CombatState.Players)
        {
            if (!AbnormalityPageRewardHelper
                    .ShouldAddPageReward<BlueStarPageRelic>(
                        room,
                        player,
                        PageRelicTitleLocKey))
            {
                continue;
            }

            room.AddExtraReward(
                player,
                new RelicReward(
                    ModelDb.Relic<BlueStarPageRelic>().ToMutable(),
                    player));
        }
    }

    private void UpdateVisualForMove(string moveId)
    {
        NCombatRoom? room = NCombatRoom.Instance;
        if (room?.GetCreatureNode(Creature)?.Visuals
            is BlueStarAltarCreatureVisuals visuals)
        {
            visuals.UpdateIdleForMove(moveId);
        }
    }

    private MoveState Register(MoveState state)
    {
        _statesById[state.Id] = state;
        return state;
    }

    private static IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new HiddenIntent();
        yield return new IndiscriminateAttackIntent(
            NovaHighAscensionDamage,
            1,
            "BLUE_STAR_NOVA_VOICE.description",
            IntentBadge.Confusion(NovaConfusion),
            IntentBadge.Heal(NovaFollowerChaoHealPercent));
    }

    private static LibraryCreatureResistanceData.Resistance NormalResistance() =>
        new()
        {
            Slash = LibraryResistanceLevel.Immune,
            Pierce = LibraryResistanceLevel.Immune,
            Blunt = LibraryResistanceLevel.Immune
        };
}
