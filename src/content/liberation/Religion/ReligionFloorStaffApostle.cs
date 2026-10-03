using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.content.guests.DawnOffice;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.content.liberation.Religion;

public sealed class ReligionFloorStaffApostle : ReligionFloorApostle
{
    private const int HpMinNormal = 61; // 权杖使徒：普通进阶生命下限。
    private const int HpMaxNormal = 64; // 权杖使徒：普通进阶生命上限。
    private const int HpMinHigh = 67; // 权杖使徒：ToughEnemies 进阶生命下限。
    private const int HpMaxHigh = 70; // 权杖使徒：ToughEnemies 进阶生命上限。
    private const int ChaoMaximum = 60; // 权杖使徒：混乱抗性上限。
    private const int AbandonStrengthNormal = 1; // 他没有撇下我：普通进阶下回合力量。
    private const int AbandonStrengthHigh = 2; // 他没有撇下我：DeadlyEnemies 进阶下回合力量。
    private const int AbandonWeight = 45; // 他没有撇下我：随机权重。
    private const int LampWeakNormal = 1; // 您必点着我的灯：普通进阶永久虚弱。
    private const int LampWeakHigh = 2; // 您必点着我的灯：DeadlyEnemies 进阶永久虚弱。
    private const int LampFlawNormal = 1; // 您必点着我的灯：普通进阶永久破绽。
    private const int LampFlawHigh = 2; // 您必点着我的灯：DeadlyEnemies 进阶永久破绽。
    private const int LampWeight = 25; // 您必点着我的灯：随机权重。
    internal const int WordDamageMin = 23; // 愿您的话语临到我身上：单次伤害下限。
    internal const int WordDamageMax = 25; // 愿您的话语临到我身上：单次伤害上限。
    internal const int WordHits = 1; // 愿您的话语临到我身上：攻击次数。
    private const int WordWeight = 30; // 愿您的话语临到我身上：随机权重。

    internal static int AbandonStrength => ReligionFloorRules.DamageValue(AbandonStrengthNormal, AbandonStrengthHigh);

    internal static int LampWeak => ReligionFloorRules.DamageValue(LampWeakNormal, LampWeakHigh);

    internal static int LampFlaw => ReligionFloorRules.DamageValue(LampFlawNormal, LampFlawHigh);

    internal override string AssetName => "staff_apostle";

    internal override int ApostleIndex => 2;

    public override int MinInitialHp => ReligionFloorRules.HpValue(HpMinNormal, HpMinHigh);

    public override int MaxInitialHp => ReligionFloorRules.HpValue(HpMaxNormal, HpMaxHigh);

    public override int DefaultChaoResistance => ChaoMaximum;

    public override LibraryCreatureResistanceData.Resistance DefaultPhysicalResistanceData => Resistance(LibraryDamageType.Blunt, false);

    public override LibraryCreatureResistanceData.Resistance DefaultChaoResistanceData => Resistance(LibraryDamageType.Blunt, true);

    internal override int MoveWeight(int move) => move switch { 0 => AbandonWeight, 1 => LampWeight, 2 => WordWeight, _ => 0 };

    internal override (int Min, int Max, int Hits) AttackValues(int move) =>
        move == 2 ? (WordDamageMin, WordDamageMax, WordHits) : (0, 0, 0);

    protected override IEnumerable<AbstractIntent> CreateIntents(int move) => move switch
    {
        0 => [new ReligionFloorEffectIntent("RELIGION_STAFF_STRENGTH", AbandonStrength, ReligionEffectKind.Buff)],
        1 => [new ReligionFloorEffectIntent("RELIGION_STAFF_LAMP", LampWeak, ReligionEffectKind.Debuff)],
        2 => [AttackIntent(move)],
        _ => [new HiddenIntent()]
    };

    protected override async Task PerformReligionMove(int move, IReadOnlyList<Creature> targets)
    {
        if (move == 2)
        {
            await AttackPlayers(move, targets, "Attack");
            return;
        }

        await CreatureCmd.TriggerAnim(Creature, "Cast", ReligionFloorRules.FrameSeconds);
        if (move == 0 && Encounter is { } encounter)
        {
            foreach (ReligionFloorApostle apostle in encounter.Apostles)
            {
                if (!apostle.Creature.IsAlive || apostle.IsFakeDead)
                {
                    continue;
                }

                await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(apostle.Creature, AbandonStrength, Creature, null);
            }
        }
        else if (move == 1)
        {
            var context = new ThrowingPlayerChoiceContext();
            await LibraryPowerCmd.Apply<LibraryWeakPower>(context, targets, LampWeak, 0, true, Creature, null);
            await LibraryPowerCmd.Apply<LibraryDisarmPower>(context, targets, LampFlaw, 0, true, Creature, null);
        }
    }
}
