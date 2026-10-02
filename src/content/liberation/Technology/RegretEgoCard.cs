using System;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.cards;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Technology;

public sealed class RegretEgoCard : EgoCardBase
{
    protected override IEnumerable<string> ExtraRunAssetPaths =>
    [
        TechnologyFloorRegretBoss.AttackSfxPath
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(PreviewDamage, ValueProp.Move),
        new DynamicVar("Hits", TechnologyFloorEgoNumbers.RegretMultiHitCount),
        new DamageVar("FinalDamage", TechnologyFloorEgoNumbers.RegretFinalDamage, ValueProp.Move),
        new PowerVar<LibraryOfRuinaConfusionPower>("Confusion", TechnologyFloorEgoNumbers.RegretConfusionAmount)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryOfRuinaConfusionPower>()
    ];

    public RegretEgoCard()
        : base(3, previewDamage: TechnologyFloorEgoNumbers.RegretMultiHitDamage)
    {
    }

    protected override void OnUpgrade()
    {
        EnergyCost.AddThisCombat(-1, reduceOnly: true);
    }

    public void SetPreviewDamage(int multiHitDamage, int finalDamage)
    {
        base.SetPreviewDamage(multiHitDamage);
        DynamicVars["FinalDamage"].BaseValue = finalDamage;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        for (int i = 0; i < TechnologyFloorEgoNumbers.RegretMultiHitCount; i++)
        {
            LocalOggOneShotPlayer.Play(TechnologyFloorRegretBoss.AttackSfxPath, -2f);
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCardCompat(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        LocalOggOneShotPlayer.Play(TechnologyFloorRegretBoss.AttackSfxPath, -2f);
        await DamageCmd.Attack(DynamicVars["FinalDamage"].BaseValue)
            .FromCardCompat(this, cardPlay)
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
