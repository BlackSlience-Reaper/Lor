using System;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.monsters.TechnologyFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.TechnologyFloorLiberation;

public sealed class LimiterReleaseEgoCard : EgoCardBase
{
    protected override IEnumerable<string> ExtraRunAssetPaths =>
    [
        TechnologyFloorRegretBoss.AttackSfxPath
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(PreviewDamage, ValueProp.Move),
        new DynamicVar("Hits", TechnologyFloorEgoNumbers.LimiterReleaseHitCount),
        new PowerVar<LibraryBleedingPower>("Bleed", TechnologyFloorEgoNumbers.LimiterReleaseBleedPerHit)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBleedingPower>()
    ];

    public LimiterReleaseEgoCard()
        : base(1, previewDamage: TechnologyFloorEgoNumbers.LimiterReleaseHitDamage)
    {
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.BaseValue = TechnologyFloorEgoNumbers.LimiterReleaseHitUpgradedDamage;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        for (int i = 0; i < TechnologyFloorEgoNumbers.LimiterReleaseHitCount; i++)
        {
            LocalOggOneShotPlayer.Play(TechnologyFloorRegretBoss.AttackSfxPath, -2f);
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);

            if (cardPlay.Target.IsAlive)
            {
                await PowerCmdCompat.Apply<LibraryBleedingPower>(
                    choiceContext,
                    cardPlay.Target,
                    TechnologyFloorEgoNumbers.LimiterReleaseBleedPerHit,
                    Owner.Creature,
                    this);
            }
        }
    }
}
