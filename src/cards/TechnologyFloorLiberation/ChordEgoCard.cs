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
    private int _previewDamageB = TechnologyFloorEgoNumbers.ChordHitBBaseDamage;
    private int _previewDamageC = TechnologyFloorEgoNumbers.ChordHitCBaseDamage;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(PreviewDamage, ValueProp.Move),
        new DynamicVar("DamageB", _previewDamageB),
        new DynamicVar("DamageC", _previewDamageC)
    ];

    public ChordEgoCard()
        : base(1, previewDamage: TechnologyFloorEgoNumbers.ChordHitABaseDamage)
    {
    }

    protected override void OnUpgrade()
    {
        EnergyCost.AddThisCombat(-1, reduceOnly: true);
    }

    public void SetPreviewDamage(int damageA, int damageB, int damageC)
    {
        _previewDamageB = damageB;
        _previewDamageC = damageC;
        base.SetPreviewDamage(damageA);
        DynamicVars["DamageB"].BaseValue = damageB;
        DynamicVars["DamageC"].BaseValue = damageC;
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
