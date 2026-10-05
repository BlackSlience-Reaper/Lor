using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.content.liberation.Religion;

public sealed class ReligionFloorSpearApostle : ReligionFloorApostle
{
    private const int HpMinNormal = 132; // 长枪使徒：普通进阶生命下限。
    private const int HpMaxNormal = 135; // 长枪使徒：普通进阶生命上限。
    private const int HpMinHigh = 136; // 长枪使徒：ToughEnemies 进阶生命下限。
    private const int HpMaxHigh = 140; // 长枪使徒：ToughEnemies 进阶生命上限。
    private const int ChaoMaximum = 100; // 长枪使徒：混乱抗性上限。
    internal const int TruthDamageMin = 11; // 求您以真理引导我：单次伤害下限。
    internal const int TruthDamageMax = 12; // 求您以真理引导我：单次伤害上限。
    internal const int TruthHits = 2; // 求您以真理引导我：攻击次数。
    private const int TruthWeight = 40; // 求您以真理引导我：随机权重。
    internal const int DevotionDamageMin = 16; // 我将永献于您：单次伤害下限。
    internal const int DevotionDamageMax = 17; // 我将永献于您：单次伤害上限。
    internal const int DevotionHits = 1; // 我将永献于您：攻击次数。
    internal const int DevotionNormalityCards = 1; // 我将永献于您：加入每名存活玩家手牌的凡庸数量。
    private const int DevotionWeight = 40; // 我将永献于您：随机权重。
    internal const int HolyConfusionTurns = 1; // 因为他是圣洁的：混乱持续回合数。
    private const int HolyWeight = 20; // 因为他是圣洁的：随机权重。

    internal override string AssetName => "spear_apostle";

    internal override int ApostleIndex => 1;

    public override int MinInitialHp => ReligionFloorRules.HpValue(HpMinNormal, HpMinHigh);

    public override int MaxInitialHp => ReligionFloorRules.HpValue(HpMaxNormal, HpMaxHigh);

    public override int DefaultChaoResistance => ChaoMaximum;

    public override LibraryCreatureResistanceData.Resistance DefaultPhysicalResistanceData => Resistance(LibraryDamageType.Pierce, false);

    public override LibraryCreatureResistanceData.Resistance DefaultChaoResistanceData => Resistance(LibraryDamageType.Pierce, true);

    internal override int MoveWeight(int move) => move switch { 0 => TruthWeight, 1 => DevotionWeight, 2 => HolyWeight, _ => 0 };

    internal override (int Min, int Max, int Hits) AttackValues(int move) => move switch
    {
        0 => (TruthDamageMin, TruthDamageMax, TruthHits),
        1 => (DevotionDamageMin, DevotionDamageMax, DevotionHits),
        _ => (0, 0, 0)
    };

    protected override IEnumerable<AbstractIntent> CreateIntents(int move) => move switch
    {
        0 => [AttackIntent(move)],
        1 => [AttackIntent(move), new ReligionFloorEffectIntent("RELIGION_SPEAR_NORMALITY", DevotionNormalityCards, ReligionEffectKind.StatusCard)],
        2 => [new ReligionFloorEffectIntent("RELIGION_SPEAR_CONFUSION", HolyConfusionTurns, ReligionEffectKind.Debuff)],
        _ => [new HiddenIntent()]
    };

    protected override async Task PerformReligionMove(int move, IReadOnlyList<Creature> targets)
    {
        if (move == 2)
        {
            await CreatureCmd.TriggerAnim(Creature, "Cast", ReligionFloorRules.FrameSeconds);
            await PowerCmdCompat.Apply<LibraryOfRuinaConfusionPower>(targets, HolyConfusionTurns, Creature, null);
            return;
        }

        await AttackPlayers(move, targets, "Pierce");
        if (move == 1)
        {
            await CardPileCmdCompat.AddToCombatAndPreview<Normality>(
                targets.Where(static target => target.IsAlive),
                PileType.Hand,
                DevotionNormalityCards,
                addedByPlayer: false);
        }
    }
}
