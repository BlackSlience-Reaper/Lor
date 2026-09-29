using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.powers.KingOfGreed;
using LibraryOfRuina.visuals.KingOfGreed;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.monsters.KingOfGreed;

public sealed class GoldenAmber : LorMonsterModel
{
    public const string Root = "res://images/monsters/king_of_greed/";
    public const string IdleTexturePath = Root + "golden_amber.png";
    public const string SfxRoot = "res://audio/sfx/king_of_greed/";
    public const string AwakenMagicalGirlSfxPath = SfxRoot + "awaken_magical_girl.ogg";
    public const string AwakenKingSfxPath = SfxRoot + "awaken_king.ogg";

    private const string WaitMoveId = "AMBER_WAIT";

    private bool _wasBroken;
    private bool _isAwakening;
    private bool _hasAwakened;

    public bool WasBroken => _wasBroken;

    public bool IsAwakening => _isAwakening;

    public bool HasAwakened => _hasAwakened;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 85, 70);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 90, 75);

    public override int DefaultChaoResistance => 50;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
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
    [
        ..GoldenAmberCreatureVisuals
            .Profile.AssetPaths,
        AwakenMagicalGirlSfxPath,
        AwakenKingSfxPath,
        ..KingOfGreed.AssetPathsStatic
    ];

    internal void MarkBroken() => _wasBroken = true;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _wasBroken = false;
        _isAwakening = false;
        _hasAwakened = false;
        await PowerCmdCompat.Apply<LibraryOfRuinaGoldenAmberPower>(Creature, 1m, Creature, null, silent: true);
    }

    public override void BeforeRemovedFromRoom()
    {
        base.BeforeRemovedFromRoom();
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        // 苏醒属于转阶段行动：开战前被打出混乱（如白雪公主书页“恶意”）时，
        // 混乱击晕不能覆盖苏醒，琥珀仍须在第一次敌方行动时苏醒。
        var wait = new LibraryPhaseTransitionMoveState(
            WaitMoveId,
            WaitMove,
            new SummonIntent(),
            new UnknownIntent());
        wait.FollowUpState = wait;

        return new MonsterMoveStateMachine([wait], wait);
    }

    private async Task WaitMove(IReadOnlyList<Creature> targets)
    {
        await AwakenInPlace();
    }

    private async Task AwakenInPlace()
    {
        CombatStateLike? combatState = Creature.CombatState;
        string? slot = Creature.SlotName;
        if (combatState == null || string.IsNullOrWhiteSpace(slot))
        {
            return;
        }

        bool magicalGirl = _wasBroken;
        LocalOggOneShotPlayer.Play(
            magicalGirl ? AwakenMagicalGirlSfxPath : AwakenKingSfxPath,
            KingOfGreed.LocalSfxVolumeDb);

        var king = (KingOfGreed)ModelDb.Monster<KingOfGreed>().ToMutable();
        king.ConfigureInitialForm(magicalGirl);

        _isAwakening = true;
        try
        {
            await CreatureCmd.Kill(Creature, force: true);
            Creature spawned = await CreatureCmd.Add(king, combatState, CombatSide.Enemy, slot);
            _hasAwakened = true;

            NCreature? spawnedNode = NCombatRoom.Instance?.GetCreatureNode(spawned);
            if (spawnedNode?.Visuals is KingOfGreedCreatureVisuals visuals)
            {
                visuals.SetKingForm(!magicalGirl);
            }

            if (!CombatManager.Instance.IsInProgress || spawned.IsDead)
            {
                return;
            }

            spawned.Monster!.RollMove(combatState.PlayerCreatures);
            if (spawnedNode != null)
            {
                await spawnedNode.PerformIntent();
            }

            await spawned.Monster.PerformMove();
        }
        finally
        {
            _isAwakening = false;
        }
    }
}
