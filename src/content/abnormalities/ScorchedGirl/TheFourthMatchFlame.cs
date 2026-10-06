using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.content.liberation.History;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.content.abnormalities.ScorchedGirl;

public sealed class TheFourthMatchFlame : LorMonsterModel
{
    public override float DeathAnimLengthOverride => LayeredBossSpine.DeathLength(this);

    private int EmberDamage => 
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 8, 5);

    private const int EmberBurnAmount = 6;
    private const int BrokenHopeGuardAmount = 3;

    private bool _startsWithBrokenHope;

    public bool StartsWithBrokenHope
    {
        get => _startsWithBrokenHope;
        set
        {
            AssertMutable();
            _startsWithBrokenHope = value;
        }
    }

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 35, 33);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 38, 36);

    public override IEnumerable<string> AssetPaths =>
        TheFourthMatchFlameCreatureVisuals.Profile.AssetPaths
            .Append(ScorchedGirlAssets.FourthMatchFlameAttackSfx)
            .Concat(base.AssetPaths.Skip(1))
            .Distinct();

    public override int DefaultChaoResistance => 25;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Fatal
    };

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmdCompat.Apply<MinionPower>(Creature, 1m, Creature, null);
    }
    
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        var brokenHope = new MoveState(
            "BROKEN_HOPE",
            BrokenHopeMove,
            new BuffIntent());

        var ember = new MoveState(
            "EMBER",
            EmberMove,
            new SingleAttackIntent(EmberDamage),
            new DebuffIntent());

        var initialChooser = new ConditionalBranchState("INITIAL_CHOOSER");
        initialChooser.AddState(brokenHope, () => StartsWithBrokenHope);
        initialChooser.AddState(ember, () => true);

        brokenHope.FollowUpState = ember;
        ember.FollowUpState = brokenHope;

        states.Add(brokenHope);
        states.Add(ember);
        states.Add(initialChooser);

        return new MonsterMoveStateMachine(states, initialChooser);
    }

    private async Task BrokenHopeMove(IReadOnlyList<Creature> targets)
    {
        await AbnormalityAnimHelper.TriggerCast(Creature);
        await LibraryPowerCmd.Apply<LibraryEndurancePower>(Creature, BrokenHopeGuardAmount, 1, Creature, null);
        await PowerCmdCompat.Apply<StrengthPower>(Creature, 1, Creature, null);
    }

    private async Task EmberMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(ScorchedGirlAssets.FourthMatchFlameAttackSfx, -3f);

        await AbnormalityAnimHelper.ExecuteAttackSegment(this, EmberDamage);

        await PowerCmdCompat.Apply<LibraryBurnPower>(targets, EmberBurnAmount, Creature, null);
    }
}
