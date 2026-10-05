using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Religion;

public sealed class ReligionFloorScytheApostle : ReligionFloorApostle
{
    private const int HpMinNormal = 140; // 镰刀使徒：普通进阶生命下限。
    private const int HpMaxNormal = 144; // 镰刀使徒：普通进阶生命上限。
    private const int HpMinHigh = 147; // 镰刀使徒：ToughEnemies 进阶生命下限。
    private const int HpMaxHigh = 150; // 镰刀使徒：ToughEnemies 进阶生命上限。
    private const int ChaoMaximum = 110; // 镰刀使徒：混乱抗性上限。
    internal const int FollowBlock = 66; // 起来跟从您：所有使徒格挡。
    private const int FollowWeight = 1; // 起来跟从您：三招等权。
    internal const int SonDamageMin = 11; // 您是神的儿子：单次伤害下限。
    internal const int SonDamageMax = 12; // 您是神的儿子：单次伤害上限。
    internal const int SonHits = 1; // 您是神的儿子：攻击次数。
    internal const int SonWeak = 2; // 您是神的儿子：原版虚弱层数。
    internal const int SonFrail = 2; // 您是神的儿子：原版脆弱层数。
    private const int SonWeight = 1; // 您是神的儿子：三招等权。
    internal const int RevealDamageMin = 8; // 求您显给我们看：单次伤害下限。
    internal const int RevealDamageMax = 9; // 求您显给我们看：单次伤害上限。
    internal const int RevealHits = 3; // 求您显给我们看：攻击次数。
    private const int RevealWeight = 1; // 求您显给我们看：三招等权。

    internal override string AssetName => "scythe_apostle";

    internal override int ApostleIndex => 0;

    public override int MinInitialHp => ReligionFloorRules.HpValue(HpMinNormal, HpMinHigh);

    public override int MaxInitialHp => ReligionFloorRules.HpValue(HpMaxNormal, HpMaxHigh);

    public override int DefaultChaoResistance => ChaoMaximum;

    public override LibraryCreatureResistanceData.Resistance DefaultPhysicalResistanceData => Resistance(LibraryDamageType.Slash, false);

    public override LibraryCreatureResistanceData.Resistance DefaultChaoResistanceData => Resistance(LibraryDamageType.Slash, true);

    internal override int MoveWeight(int move) => move switch { 0 => FollowWeight, 1 => SonWeight, 2 => RevealWeight, _ => 0 };

    internal override (int Min, int Max, int Hits) AttackValues(int move) => move switch
    {
        1 => (SonDamageMin, SonDamageMax, SonHits),
        2 => (RevealDamageMin, RevealDamageMax, RevealHits),
        _ => (0, 0, 0)
    };

    protected override IEnumerable<AbstractIntent> CreateIntents(int move) => move switch
    {
        0 => [new ReligionFloorEffectIntent("RELIGION_SCYTHE_BLOCK", FollowBlock, ReligionEffectKind.Defend)],
        1 => [AttackIntent(move), new ReligionFloorEffectIntent("RELIGION_SCYTHE_WEAK", SonWeak, ReligionEffectKind.Debuff)],
        2 => [AttackIntent(move)],
        _ => [new HiddenIntent()]
    };

    protected override async Task PerformReligionMove(int move, IReadOnlyList<Creature> targets)
    {
        if (move == 0 && Encounter is { } encounter)
        {
            await CreatureCmd.TriggerAnim(Creature, "Guard", ReligionFloorRules.FrameSeconds);
            foreach (ReligionFloorApostle apostle in encounter.Apostles)
            {
                if (!apostle.Creature.IsAlive || apostle.IsFakeDead)
                {
                    continue;
                }

                await CreatureCmd.GainBlock(apostle.Creature, FollowBlock, ValueProp.Move, null);
            }
            return;
        }

        await AttackPlayers(move, targets, move == 1 ? "Slash" : "Strike");
        if (move == 1)
        {
            await PowerCmdCompat.Apply<WeakPower>(targets.Where(static target => target.IsAlive), SonWeak, Creature, null);
            await PowerCmdCompat.Apply<FrailPower>(targets.Where(static target => target.IsAlive), SonFrail, Creature, null);
        }
    }
}
