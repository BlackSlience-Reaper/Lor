using System;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.monsters.HistoryFloorLiberation;
using LibraryOfRuina.powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.HistoryFloorLiberation;

public sealed class ForgottenLongingEmbraceEgoCard : EgoCardBase
{
    protected override IEnumerable<string> ExtraRunAssetPaths =>
    [
        HistoryFloorForgottenBoss.LongingEmbraceSfxPath
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(PreviewDamage, ValueProp.Move),
        new PowerVar<LibraryOfRuinaConfusionPower>("Confusion", HistoryFloorEgoNumbers.LongingEmbraceConfusion)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryOfRuinaConfusionPower>()
    ];

    protected override void OnUpgrade()
    {
        EnergyCost.AddThisCombat(-1, reduceOnly: true);
    }

    public ForgottenLongingEmbraceEgoCard()
        : base(2, previewDamage: HistoryFloorEgoNumbers.LongingEmbraceBaseDamage)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        LocalOggOneShotPlayer.Play(HistoryFloorForgottenBoss.LongingEmbraceSfxPath, -2f);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        await PowerCmdCompat.Apply<LibraryOfRuinaConfusionPower>(
            choiceContext,
            cardPlay.Target,
            DynamicVars["Confusion"].BaseValue,
            Owner.Creature,
            this);
    }
}
