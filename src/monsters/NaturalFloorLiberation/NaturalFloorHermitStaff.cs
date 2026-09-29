using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers.NaturalFloorLiberation;
using LibraryOfRuina.visuals.NaturalFloorLiberation;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.NaturalFloorLiberation;

public sealed class NaturalFloorHermitStaff : NaturalFloorWrathMonster
{
    private static readonly string[] Ids = ["CREAK_CREAK", "CRACK_CRACK"];

    protected override string[] MoveIds => Ids;

    public const int BlockAmount = 15; // 喀嚓喀嚓：自身格挡。

    [SavedProperty]
    public int LastMove { get; private set; }

    [SavedProperty]
    public int SlotNumber { get; internal set; }

    public override int MinInitialHp =>
        HpValue(
            80, // 初始体力下限：普通。
            86); // 初始体力下限：ToughEnemies 进阶。

    public override int MaxInitialHp =>
        HpValue(
            84, // 初始体力上限：普通。
            90); // 初始体力上限：ToughEnemies 进阶。

    public override int DefaultChaoResistance => 80; // 初始混乱抗性。

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Endure,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Vulnerable
    };

    protected override IEnumerable<string> VisualAssets => NaturalFloorHermitStaffVisuals.AssetPaths;

    private static int CreakCreakDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            8, // 咯吱咯吱：DeadlyEnemies 进阶单次伤害。
            6); // 咯吱咯吱：普通单次伤害。

    private const int CreakCreakHits = 2; // 咯吱咯吱：攻击次数。

    private static int CrackCrackDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            19, // 喀嚓喀嚓：DeadlyEnemies 进阶单次伤害。
            18); // 喀嚓喀嚓：普通单次伤害。

    private const int CrackCrackHits = 1; // 喀嚓喀嚓：攻击次数。

    private static int Damage(int move) =>
        move switch
        {
            0 => CreakCreakDamage,
            1 => CrackCrackDamage,
            _ => CrackCrackDamage
        };

    private static int Hits(int move) =>
        move switch
        {
            0 => CreakCreakHits,
            1 => CrackCrackHits,
            _ => CrackCrackHits
        };

    protected override IEnumerable<AbstractIntent> CreateIntents(int move) => move == 0
        ? [new TargetedMonsterAttackIntent(() => Damage(move), () => Hits(move), "NATURAL_STAFF_CREAK.description")]
        : [new CombinedTargetedAttackDefendIntent(() => Damage(move), () => Hits(move), "NATURAL_STAFF_CRACK.description",
            "NATURAL_STAFF_CRACK.description", false, BlockAmount)];

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmdCompat.Ensure<MinionPower>(Creature, 1, Creature, null, silent: true); // 隐士之杖：1 层爪牙标记。
        await PowerCmdCompat.Ensure<NaturalFloorStaffPower>(Creature, 1, Creature, null, silent: true);
    }

    protected override string SelectMove(Rng rng) => Ids[LastMove == 0 ? rng.NextInt(0, 2) : 2 - LastMove];

    protected override async Task PerformMove(int move)
    {
        if (!CanAct)
        {
            return;
        }

        await Attack(Damage(move), Hits(move), () => GetTargetedAttackTargets(Creature), ["Attack"], ["hermit_attack"]);
        if (move == 1 && CanAct)
        {
            await CreatureCmd.GainBlock(Creature, BlockAmount, ValueProp.Move, null);
        }

        LastMove = move + 1;
    }

    public override IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner) =>
        Encounter?.Rage is { IsAlive: true } rage ? [rage] : [];

    public override Task AfterDeath(PlayerChoiceContext context, Creature creature, bool prevented, float animLength) =>
        creature == Creature && !prevented && Encounter is { } encounter ? encounter.RefreshStaffMode() : Task.CompletedTask;

    internal override Dictionary<string, string> CaptureState()
    {
        var state = base.CaptureState();
        state["Last"] = LastMove.ToString();
        state["Slot"] = SlotNumber.ToString();
        return state;
    }

    internal override void RestoreState(Dictionary<string, string> state)
    {
        base.RestoreState(state);
        LastMove = ReadInt(state, "Last");
        SlotNumber = ReadInt(state, "Slot");
    }
}
