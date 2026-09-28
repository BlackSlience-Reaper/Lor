using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.TechnologyFloorLiberation;

public sealed class SolemnMourningEgoCard : EgoCardBase
{
    public const int HitDamage = 1;
    public const int BaseHitCount = 8;
    public const int UpgradedHitCount = 9;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(PreviewDamage, ValueProp.Move),
        new DynamicVar("Hits", BaseHitCount)
    ];

    protected override IEnumerable<string> ExtraRunAssetPaths =>
    [
        monsters.FuneralOfTheDeadButterflies.FuneralOfTheDeadButterflies.AttackWhiteSfxPath
    ];

    public SolemnMourningEgoCard()
        : base(2, TargetType.AllEnemies, previewDamage: HitDamage)
    {
    }

    protected override void OnUpgrade()
    {
        EnergyCost.AddThisCombat(-1, reduceOnly: true);
        DynamicVars["Hits"].BaseValue = UpgradedHitCount;
    }

    public override void SetPreviewDamage(int damage)
    {
        SetPreviewDamage(damage, IsUpgraded ? UpgradedHitCount : BaseHitCount);
    }

    public void SetPreviewDamage(int damage, int hits)
    {
        base.SetPreviewDamage(damage);
        DynamicVars["Hits"].BaseValue = hits;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int hits = IsUpgraded ? UpgradedHitCount : BaseHitCount;

        for (int i = 0; i < hits; i++)
        {
            LocalOggOneShotPlayer.Play(monsters.FuneralOfTheDeadButterflies.FuneralOfTheDeadButterflies.AttackWhiteSfxPath, -2f);
            IReadOnlyList<Creature> enemies = Owner.Creature.CombatState?.HittableEnemies.ToArray()
                ?? Array.Empty<Creature>();

            foreach (Creature enemy in enemies)
            {
                await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                    .FromCard(this, cardPlay)
                    .Targeting(enemy)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(choiceContext);
            }
        }
    }
}
