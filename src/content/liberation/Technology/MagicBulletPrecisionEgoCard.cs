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

public sealed class MagicBulletPrecisionEgoCard : EgoCardBase
{
    public const int BaseDamage = 12;
    public const int UpgradedDamage = 14;
    public const int ParalysisStacks = 3;
    public const int ParalysisTurns = 1;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(PreviewDamage, ValueProp.Move),
        new PowerVar<LibraryOfRuinaParalysisPower>("Paralysis", ParalysisStacks),
        new DynamicVar("Turns", ParalysisTurns)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryOfRuinaParalysisPower>()
    ];

    protected override IEnumerable<string> ExtraRunAssetPaths =>
    [
        TechnologyFloorMagicBulletBoss.AttackSfxPath
    ];

    public MagicBulletPrecisionEgoCard()
        : base(1, previewDamage: BaseDamage)
    {
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.BaseValue = UpgradedDamage;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        LocalOggOneShotPlayer.Play(TechnologyFloorMagicBulletBoss.AttackSfxPath, -2f);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        await PowerCmdCompat.Apply<LibraryOfRuinaParalysisPower>(
            choiceContext,
            cardPlay.Target,
            ParalysisStacks,
            Owner.Creature,
            this);
    }
}
