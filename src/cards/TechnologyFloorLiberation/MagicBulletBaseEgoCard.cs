using System;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.monsters.TechnologyFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.TechnologyFloorLiberation;

public sealed class MagicBulletBaseEgoCard : EgoCardBase
{
    public const int BaseDamage = 7;
    public const int UpgradedDamage = 8;

    private int _previewDamage = BaseDamage;

    public override int MaxUpgradeLevel => 1;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(_previewDamage, ValueProp.Move)
    ];

    protected override IEnumerable<string> ExtraRunAssetPaths =>
    [
        TechnologyFloorMagicBulletBoss.AttackSfxPath
    ];

    public MagicBulletBaseEgoCard()
        : base(1)
    {
    }

    public void UpgradePreview()
    {
        UpgradeInternal();
        FinalizeUpgradeInternal();
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
    }
}
