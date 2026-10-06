using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.content.abnormalities.RedShoes;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Literature;

public sealed class LiteratureFloorEnhancedLeftShoe :
    LorMonsterModel
{
    public const string WhisperingDesireMoveId = "WHISPERING_DESIRE";
    public const string HiddenDesireMoveId = "HIDDEN_DESIRE";
    public const int WhisperingDesireHits = 2;
    public const int BleedApplied = 3;
    public const int HiddenDesireBlock = 11;

    private int WhisperingDesireDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            7,
            6);

    internal static (int Min, int Max) DebugHpRange(bool toughEnemies) =>
        toughEnemies ? (115, 120) : (100, 110);

    internal static int DebugWhisperingDesireDamage(
        bool deadlyEnemies) => deadlyEnemies ? 9 : 6;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            95,
            85);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            100,
            90);

    public override int DefaultChaoResistance => 50;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Endure,
            Pierce = LibraryResistanceLevel.Normal,
            Blunt = LibraryResistanceLevel.Vulnerable
        };

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Endure,
            Pierce = LibraryResistanceLevel.Endure,
            Blunt = LibraryResistanceLevel.Vulnerable
        };

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>(
                RedShoesLeftCreatureVisuals.Profile.AssetPaths);
            paths.Add(LiteratureFloorBloodlustBoss.HorizontalSfxPath);
            foreach (AbstractIntent intent in EnumerateIntentAssets())
            {
                paths.AddRange(intent.AssetPaths);
            }

            return paths.Distinct();
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<MinionPower>(
            Creature,
            1m,
            Creature,
            null,
            silent: true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var whisperingDesire = new MoveState(
            WhisperingDesireMoveId,
            WhisperingDesireMove,
            new CombinedAttackDebuffIntent(
                WhisperingDesireDamage,
                WhisperingDesireHits,
                "LITERATURE_FLOOR_ENHANCED_LEFT_SHOE_WHISPERING_DESIRE.description",
                IntentBadge.Bleed(BleedApplied)));
        var hiddenDesire = new MoveState(
            HiddenDesireMoveId,
            HiddenDesireMove,
            new CombinedDefendDebuffIntent(
                HiddenDesireBlock,
                "LITERATURE_FLOOR_ENHANCED_LEFT_SHOE_HIDDEN_DESIRE.description",
                IntentBadge.Bleed(BleedApplied)));

        whisperingDesire.FollowUpState = hiddenDesire;
        hiddenDesire.FollowUpState = whisperingDesire;
        return new MonsterMoveStateMachine(
            [whisperingDesire, hiddenDesire],
            whisperingDesire);
    }

    private async Task WhisperingDesireMove(
        IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(
            LiteratureFloorBloodlustBoss.HorizontalSfxPath,
            -1.5f);
        await DamageCmd.Attack(WhisperingDesireDamage)
            .WithHitCount(WhisperingDesireHits)
            .FromMonster(this)
            .WithAttackerAnim("Attack", RedShoesLeftCreatureVisuals.AttackImpactSeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
        await ApplyBleedToLivingPlayers();
    }

    private async Task HiddenDesireMove(
        IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.18f);
        await CreatureCmd.GainBlock(
            Creature,
            HiddenDesireBlock,
            ValueProp.Move,
            null);
        await ApplyBleedToLivingPlayers();
    }

    private async Task ApplyBleedToLivingPlayers()
    {
        Creature[] players = Creature.CombatState?.LivingPlayerCreatures()
            .OrderBy(static player => player.Player?.NetId ?? 0UL)
            .ToArray() ?? [];
        if (players.Length > 0)
        {
            await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
                players,
                BleedApplied,
                Creature,
                null);
        }
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new CombinedAttackDebuffIntent(
            WhisperingDesireDamage,
            WhisperingDesireHits,
            "LITERATURE_FLOOR_ENHANCED_LEFT_SHOE_WHISPERING_DESIRE.description",
            IntentBadge.Bleed(BleedApplied));
        yield return new CombinedDefendDebuffIntent(
            HiddenDesireBlock,
            "LITERATURE_FLOOR_ENHANCED_LEFT_SHOE_HIDDEN_DESIRE.description",
            IntentBadge.Bleed(BleedApplied));
    }
}
