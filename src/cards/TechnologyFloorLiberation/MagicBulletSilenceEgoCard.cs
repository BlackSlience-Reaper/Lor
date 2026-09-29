using System;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.monsters.TechnologyFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.TechnologyFloorLiberation;

public sealed class MagicBulletSilenceEgoCard : EgoCardBase
{
    public const int BaseDamage = 12;
    public const int UpgradedDamage = 13;
    public const int BurnStacks = 10;
    public const int FrailStacks = 3;
    public const int WeakStacks = 3;
    public const int DebuffTurns = 1;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(PreviewDamage, ValueProp.Move),
        new DynamicVar("Burn", BurnStacks),
        new PowerVar<FrailPower>("Frail", FrailStacks),
        new PowerVar<WeakPower>("Weak", WeakStacks),
        new DynamicVar("Turns", DebuffTurns)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBurnPower>(),
        HoverTipFactory.FromPower<FrailPower>(),
        HoverTipFactory.FromPower<WeakPower>()
    ];

    protected override IEnumerable<string> ExtraRunAssetPaths =>
    [
        TechnologyFloorMagicBulletBoss.AttackSfxPath
    ];

    public MagicBulletSilenceEgoCard()
        : base(2, previewDamage: BaseDamage)
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
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        await PowerCmdCompat.Apply<LibraryBurnPower>(
            choiceContext, cardPlay.Target, BurnStacks, Owner.Creature, this);
        await PowerCmdCompat.Apply<FrailPower>(
            choiceContext, cardPlay.Target, FrailStacks, Owner.Creature, this);
        await PowerCmdCompat.Apply<WeakPower>(
            choiceContext, cardPlay.Target, WeakStacks, Owner.Creature, this);
    }
}
