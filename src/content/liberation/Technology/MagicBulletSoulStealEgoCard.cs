using System;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Technology;

public sealed class MagicBulletSoulStealEgoCard : EgoCardBase
{
    public const int BaseDamageA = 8;
    public const int UpgradedDamageA = 9;
    public const int BaseDamageB = 7;
    public const int UpgradedDamageB = 8;
    public const int BurnStacks = 5;
    public const int WeakStacks = 1;

    private int _previewDamageB = BaseDamageB;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(PreviewDamage, ValueProp.Move),
        new DynamicVar("DamageB", _previewDamageB),
        new DynamicVar("Burn", BurnStacks),
        new PowerVar<WeakPower>("Weak", WeakStacks)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBurnPower>(),
        HoverTipFactory.FromPower<WeakPower>()
    ];

    protected override IEnumerable<string> ExtraRunAssetPaths =>
    [
        TechnologyFloorMagicBulletBoss.AttackSfxPath
    ];

    public MagicBulletSoulStealEgoCard()
        : base(2, previewDamage: BaseDamageA)
    {
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.BaseValue = UpgradedDamageA;
        DynamicVars["DamageB"].BaseValue = UpgradedDamageB;
    }

    public void SetPreviewDamage(int damageA, int damageB)
    {
        _previewDamageB = damageB;
        base.SetPreviewDamage(damageA);
        DynamicVars["DamageB"].BaseValue = damageB;
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
        await PowerCmdCompat.Apply<WeakPower>(
            choiceContext, cardPlay.Target, WeakStacks, Owner.Creature, this);

        LocalOggOneShotPlayer.Play(TechnologyFloorMagicBulletBoss.AttackSfxPath, -2f);
        await DamageCmd.Attack(DynamicVars["DamageB"].BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        await PowerCmdCompat.Apply<LibraryBurnPower>(
            choiceContext, cardPlay.Target, BurnStacks, Owner.Creature, this);
        await PowerCmdCompat.Apply<WeakPower>(
            choiceContext, cardPlay.Target, WeakStacks, Owner.Creature, this);
    }
}
