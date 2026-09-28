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

public sealed class MagicBulletPrecisionEgoCard : EgoCardBase
{
    public const int BaseDamage = 12;
    public const int UpgradedDamage = 14;
    public const int ParalysisStacks = 3;
    public const int ParalysisTurns = 1;

    private int _previewDamage = BaseDamage;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(_previewDamage, ValueProp.Move),
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
        : base(1)
    {
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.BaseValue = UpgradedDamage;
    }

    public void SetPreviewDamage(int damage)
    {
        _previewDamage = damage;
        DynamicVars.Damage.BaseValue = damage;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        LocalOggOneShotPlayer.Play(TechnologyFloorMagicBulletBoss.AttackSfxPath, -2f);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
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
