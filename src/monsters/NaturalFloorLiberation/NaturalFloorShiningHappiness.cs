using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.intents;
using LibraryOfRuina.monsters.KingOfGreed;
using LibraryOfRuina.powers.NaturalFloorLiberation;
using LibraryOfRuina.visuals.NaturalFloorLiberation;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.monsters.NaturalFloorLiberation;

public sealed class NaturalFloorShiningHappiness : NaturalFloorPhaseMonster
{
    internal const int HpIncreasePercent = 20; // 自然层幸福：原遭遇基础体力额外提升百分比。
    internal const int StrongAura = ShiningHappiness.KingStrongStacks; // 幸福存活期间：Boss 永久强壮贡献。
    internal const int EnduranceAura = ShiningHappiness.KingEnduranceStacks; // 幸福存活期间：Boss 永久忍耐贡献。
    internal const int DeathCards = ShiningHappiness.DeathShardCount; // 幸福死亡：向每名存活玩家手牌加入的碎片数。
    private static readonly string[] Ids = ["HAPPINESS_FRAGMENT"];

    [SavedProperty]
    public int SlotNumber { get; set; }

    [SavedProperty]
    public bool SuppressDeathReward { get; internal set; }

    public override int MinInitialHp => ModelDb.Monster<ShiningHappiness>().MinInitialHp * (100 + HpIncreasePercent) / 100;

    public override int MaxInitialHp => ModelDb.Monster<ShiningHappiness>().MaxInitialHp * (100 + HpIncreasePercent) / 100;

    public override int DefaultChaoResistance => ModelDb.Monster<ShiningHappiness>().DefaultChaoResistance;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData =>
        ModelDb.Monster<ShiningHappiness>().DefaultPhysicalResistanceData;

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData =>
        ModelDb.Monster<ShiningHappiness>().DefaultChaoResistanceData;

    protected override string[] MoveIds => Ids;

    protected override IEnumerable<string> VisualAssets => new[] { NaturalFloorHappinessVisuals.ScenePath, ShiningHappiness.SummonSfxPath }
        .Concat(ModelDb.Card<NaturalFloorHappinessShard>().AllPortraitPaths)
        .Concat(ModelDb.Card<NaturalFloorShiningHappinessCard>().AllPortraitPaths);

    internal static int GiftCards => DamageValue(2, 1); // 幸福的碎片：普通 / DeadlyEnemies 进阶塞入抽牌堆的卡数，与原遭遇一致。

    protected override int SelectMove() => 0;

    protected override IEnumerable<AbstractIntent> CreateIntents(int move) =>
    [new DetailedStatusCardIntent<NaturalFloorShiningHappinessCard>(GiftCards, PileType.Draw, DetailedIntentScopeText.Target)];

    protected override async Task ApplyPassives()
    {
        await PowerCmdCompat.Ensure<MinionPower>(Creature, 1, Creature, null, silent: true);
        var power = await PowerCmdCompat.Ensure<NaturalFloorShiningHappinessPower>(Creature, 1, Creature, null, silent: true);
        if (power != null)
        {
            await power.ApplyAura();
        }
    }

    protected override Task PerformMove(int move, IReadOnlyList<Creature> targets) =>
        CanAct && !Creature.IsStunned
            ? CardPileCmdCompat.AddToCombatAndPreview<NaturalFloorShiningHappinessCard>(
                Encounter!.LivingPlayers(), PileType.Draw, GiftCards, addedByPlayer: false)
            : Task.CompletedTask;
}
