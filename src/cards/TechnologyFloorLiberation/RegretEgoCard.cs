using System;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.monsters.TechnologyFloorLiberation;
using LibraryOfRuina.powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.TechnologyFloorLiberation;

public sealed class RegretEgoCard : EgoCardBase
{
    public const int MultiHitDamage = 5;
    public const int MultiHitUpgradedDamage = 6;
    public const int MultiHitCount = 2;
    public const int FinalDamage = 12;
    public const int FinalUpgradedDamage = 14;
    public const int ConfusionAmount = 1;

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
        new DynamicVar("Hits", MultiHitCount),
        new DamageVar("FinalDamage", FinalDamage, ValueProp.Move),
        new PowerVar<LibraryOfRuinaConfusionPower>("Confusion", ConfusionAmount)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryOfRuinaConfusionPower>()
    ];

    public RegretEgoCard()
        : base(3, previewDamage: MultiHitDamage)
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

        for (int i = 0; i < MultiHitCount; i++)
        {
            LocalOggOneShotPlayer.Play(TechnologyFloorRegretBoss.AttackSfxPath, -2f);
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        LocalOggOneShotPlayer.Play(TechnologyFloorRegretBoss.AttackSfxPath, -2f);
        await DamageCmd.Attack(DynamicVars["FinalDamage"].BaseValue)
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
