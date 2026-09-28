using System;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.monsters.HistoryFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.HistoryFloorLiberation;

public sealed class EndLightEgoCard : EgoCardBase
{
    protected override IEnumerable<string> ExtraRunAssetPaths =>
    [
        HistoryFloorEndLightBoss.EndLightAttackSfxPath
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(PreviewDamage, ValueProp.Move),
        new PowerVar<LibraryBurnPower>("Burn", HistoryFloorEgoNumbers.EndLightBurnAmount)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBurnPower>()
    ];

    public EndLightEgoCard()
        : base(2, previewDamage: HistoryFloorEgoNumbers.EndLightBaseDamage)
    {
    }

    protected override void OnUpgrade()
    {
        EnergyCost.AddThisCombat(-1, reduceOnly: true);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        LocalOggOneShotPlayer.Play(HistoryFloorEndLightBoss.EndLightAttackSfxPath, -2f);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        await PowerCmdCompat.Apply<LibraryBurnPower>(
            choiceContext,
            cardPlay.Target,
            DynamicVars["Burn"].BaseValue,
            Owner.Creature,
            this);
    }
}
