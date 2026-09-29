using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Art;

public sealed class BeyondFragmentEgoCard : EgoCardBase
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(PreviewDamage, ValueProp.Move),
        new DynamicVar("Hits", ArtFloorEgoNumbers.BeyondFragmentHitCount),
        new PowerVar<StrengthPower>("StrengthLoss", ArtFloorEgoNumbers.BeyondFragmentStrengthLoss),
        new PowerVar<DexterityPower>("DexterityLoss", ArtFloorEgoNumbers.BeyondFragmentDexterityLoss)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>()
    ];

    public BeyondFragmentEgoCard()
        : base(3, TargetType.AllEnemies, previewDamage: ArtFloorEgoNumbers.BeyondFragmentDamage)
    {
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(ArtFloorEgoNumbers.BeyondFragmentUpgradedDamage - ArtFloorEgoNumbers.BeyondFragmentDamage);
        EnergyCost.AddThisCombat(-1, reduceOnly: true);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        IReadOnlyList<Creature> targets = Owner.Creature.CombatState?.HittableEnemies.ToArray()
            ?? Array.Empty<Creature>();

        for (int i = 0; i < ArtFloorEgoNumbers.BeyondFragmentHitCount; i++)
        {
            foreach (Creature target in targets.Where(static target => target.IsAlive))
            {
                await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                    .FromCard(this, cardPlay)
                    .Targeting(target)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(choiceContext);
            }
        }

        foreach (Creature target in targets.Where(static target => target.IsAlive))
        {
            await PowerCmdCompat.Apply<StrengthPower>(
                choiceContext,
                target,
                -DynamicVars["StrengthLoss"].BaseValue,
                Owner.Creature,
                this);
            await PowerCmdCompat.Apply<DexterityPower>(
                choiceContext,
                target,
                -DynamicVars["DexterityLoss"].BaseValue,
                Owner.Creature,
                this);
        }
    }
}
