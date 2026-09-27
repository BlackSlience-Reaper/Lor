using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.TechnologyFloorLiberation;

public sealed class ChordEgoCard : EgoCardBase
{
    public const int HitABaseDamage = 4;
    public const int HitAAscensionDamage = 5;
    public const int HitBBaseDamage = 3;
    public const int HitBAscensionDamage = 4;
    public const int HitCBaseDamage = 6;
    public const int HitCAscensionDamage = 7;

    private int _previewDamageA = HitABaseDamage;
    private int _previewDamageB = HitBBaseDamage;
    private int _previewDamageC = HitCBaseDamage;

    public override int MaxUpgradeLevel => 1;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(_previewDamageA, ValueProp.Move),
        new DynamicVar("DamageB", _previewDamageB),
        new DynamicVar("DamageC", _previewDamageC)
    ];

    public ChordEgoCard()
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
        EnergyCost.AddThisCombat(-1, reduceOnly: true);
    }

    public void SetPreviewDamage(int damageA, int damageB, int damageC)
    {
        _previewDamageA = damageA;
        _previewDamageB = damageB;
        _previewDamageC = damageC;
        DynamicVars.Damage.BaseValue = damageA;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        await DamageCmd.Attack(_previewDamageB)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        await DamageCmd.Attack(_previewDamageC)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }
}
