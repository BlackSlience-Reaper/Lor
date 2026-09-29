using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.specialguests.Iori;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.cards.Iori;

public static class IoriPenetratingWoundChoices
{
    public static Task ChooseForPlayersAsync(
        IReadOnlyList<Creature> targets,
        int statLoss,
        int painDamage) =>
        Task.WhenAll(
            targets
                .Where(static target => target.IsAlive && target.IsPlayer)
                .Select(target => ChooseForPlayerAsync(
                    target.Player!,
                    statLoss,
                    painDamage)));

    public static async Task ChooseForPlayerAsync(
        Player player,
        int statLoss,
        int painDamage)
    {
        CombatStateLike? combatState = player.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        IoriLoseStrengthAndDexterityChoiceCard loseStats =
            combatState.CreateCard<IoriLoseStrengthAndDexterityChoiceCard>(player);
        loseStats.DynamicVars["StatLoss"].BaseValue = statLoss;

        IoriCardPlayPainChoiceCard pain = combatState
            .CreateCard<IoriCardPlayPainChoiceCard>(player);
        pain.DynamicVars["IoriCardPlayPainPower"].BaseValue = painDamage;

        CardModel? chosen = await CardSelectCmd.FromChooseACardScreen(
            new BlockingPlayerChoiceContext(),
            [loseStats, pain],
            player);
        if (chosen is KnowledgeDemon.IChoosable choosable)
        {
            await choosable.OnChosen();
        }
    }
}

[CardPool(typeof(TokenCardPool))]
public sealed class IoriLoseStrengthAndDexterityChoiceCard
    : CardModel, KnowledgeDemon.IChoosable
{
    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("StatLoss", 2m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>(),
    ];

    public override string PortraitPath =>
        ModelDb.Card<Disintegration>().PortraitPath;

    public override IEnumerable<string> AllPortraitPaths =>
    [
        PortraitPath,
    ];

    public IoriLoseStrengthAndDexterityChoiceCard()
        : base(
            -1,
            CardType.Status,
            CardRarity.Status,
            TargetType.None,
            shouldShowInCardLibrary: false)
    {
    }

    public async Task OnChosen()
    {
        int amount = DynamicVars["StatLoss"].IntValue;
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Owner.Creature,
            -amount,
            Owner.Creature,
            this);
        await PowerCmd.Apply<DexterityPower>(
            new ThrowingPlayerChoiceContext(),
            Owner.Creature,
            -amount,
            Owner.Creature,
            this);
    }
}

[CardPool(typeof(TokenCardPool))]
public sealed class IoriCardPlayPainChoiceCard
    : CardModel, KnowledgeDemon.IChoosable
{
    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<IoriCardPlayPainPower>(1m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<IoriCardPlayPainPower>(),
    ];

    public override string PortraitPath => ModelDb.Card<MindRot>().PortraitPath;

    public override IEnumerable<string> AllPortraitPaths =>
    [
        PortraitPath,
    ];

    public IoriCardPlayPainChoiceCard()
        : base(
            -1,
            CardType.Status,
            CardRarity.Status,
            TargetType.None,
            shouldShowInCardLibrary: false)
    {
    }

    public async Task OnChosen()
    {
        await PowerCmd.Apply<IoriCardPlayPainPower>(
            new ThrowingPlayerChoiceContext(),
            Owner.Creature,
            DynamicVars["IoriCardPlayPainPower"].IntValue,
            Owner.Creature,
            this);
    }
}
