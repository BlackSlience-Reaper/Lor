using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.cards;
using LibraryOfRuina.cards.ArtFloorLiberation;
using LibraryOfRuina.framework.cards;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.ArtFloorLiberation;

[CardPool(typeof(LibraryOfRuinaEgoCardPool))]
public sealed class OurLittleGalaxyEgoPreviewCard()
    : EgoCardBase(
        3,
        TargetType.AllEnemies,
        previewDamage: ArtFloorEgoNumbers.OurLittleGalaxyDamage,
        shouldShowInCardLibrary: false)
{
    public override CardPoolModel VisualCardPool => ModelDb.CardPool<LibraryOfRuinaEgoCardPool>();

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(PreviewDamage, ValueProp.Move),
        new DynamicVar("Hits", ArtFloorEgoNumbers.OurLittleGalaxyHitCount)
    ];

    public override void SetEnemyAttackPreview(IReadOnlyList<int> damages, int hits)
    {
        IReadOnlyList<int> safeDamages = damages ?? [];
        if (safeDamages.Count > 0)
        {
            DynamicVars.Damage.BaseValue = safeDamages[0];
        }

        if (DynamicVars.TryGetValue("Hits", out DynamicVar? hitVar))
        {
            hitVar.BaseValue = Math.Max(1, hits);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(ArtFloorEgoNumbers.OurLittleGalaxyUpgradedDamage - ArtFloorEgoNumbers.OurLittleGalaxyDamage);
        EnergyCost.AddThisCombat(-1, reduceOnly: true);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        IReadOnlyList<Creature> targets = Owner.Creature.CombatState?.HittableEnemies.ToArray()
            ?? Array.Empty<Creature>();

        for (int i = 0; i < ArtFloorEgoNumbers.OurLittleGalaxyHitCount; i++)
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
    }

    public static string GetPortraitResourcePath() => GetPortraitResourcePath<OurLittleGalaxyEgoPreviewCard>();
}
