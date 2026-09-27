using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.ArtFloorLiberation;

[CardPool(typeof(LibraryOfRuinaEgoCardPool))]
public sealed class PleasureEgoCard()
    : CardModel(3, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies, shouldShowInCardLibrary: false),
        IEnemyAttackPreviewCard
{
    public const int Damage = 6;
    public const int UpgradedDamage = 8;
    public const int HitCount = 3;
    public const int BleedAmount = 9;
    public const int FinalDamage = 30;
    public const int Strength = 5;

    private const string PortraitResourcePath = "packed/card_portraits/ego/pleasure_ego_card.png";

    public override int MaxUpgradeLevel => 1;

    public override CardPoolModel VisualCardPool => ModelDb.CardPool<LibraryOfRuinaEgoCardPool>();

    public override string PortraitPath => ImageHelper.GetImagePath(PortraitResourcePath);

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(Damage, ValueProp.Move),
        new DynamicVar("Hits", HitCount),
        new PowerVar<LibraryBleedingPower>("Bleed", BleedAmount),
        new DamageVar("FinalDamage", FinalDamage, ValueProp.Move),
        new PowerVar<StrengthPower>("Strength", Strength)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBleedingPower>(),
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    public void UpgradePreview()
    {
        UpgradeInternal();
        FinalizeUpgradeInternal();
    }

    public void SetEnemyAttackPreview(IReadOnlyList<int> damages, int hits)
    {
        IReadOnlyList<int> safeDamages = damages ?? [];
        if (safeDamages.Count > 0)
        {
            DynamicVars.Damage.BaseValue = safeDamages[0];
        }

        if (safeDamages.Count > 1 && DynamicVars.TryGetValue("FinalDamage", out DynamicVar? finalDamage))
        {
            finalDamage.BaseValue = safeDamages[1];
        }

        if (DynamicVars.TryGetValue("Hits", out DynamicVar? hitVar))
        {
            hitVar.BaseValue = Math.Max(1, hits);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(UpgradedDamage - Damage);
        EnergyCost.AddThisCombat(-1, reduceOnly: true);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        IReadOnlyList<Creature> targets = Owner.Creature.CombatState?.HittableEnemies.ToArray()
            ?? Array.Empty<Creature>();

        HashSet<Creature> hitTargets = [];
        for (int i = 0; i < HitCount; i++)
        {
            foreach (Creature target in targets.Where(static target => target.IsAlive))
            {
                AttackCommand attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                    .FromCard(this, cardPlay)
                    .Targeting(target)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(choiceContext);
                if (AttackCommandCompat.Results(attack).Any(static result => result.UnblockedDamage > 0))
                {
                    hitTargets.Add(target);
                }
            }
        }

        foreach (Creature target in hitTargets.Where(static target => target.IsAlive))
        {
            await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
                choiceContext,
                target,
                DynamicVars["Bleed"].BaseValue,
                Owner.Creature,
                this);
        }

        foreach (Creature target in targets.Where(static target => target.IsAlive))
        {
            await DamageCmd.Attack(DynamicVars["FinalDamage"].BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        await PowerCmdCompat.Apply<StrengthPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["Strength"].BaseValue,
            Owner.Creature,
            this);
    }

    public static string GetPortraitResourcePath() => PortraitResourcePath;
}
