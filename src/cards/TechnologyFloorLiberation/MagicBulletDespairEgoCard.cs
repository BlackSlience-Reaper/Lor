using System.Threading.Tasks;
using LibraryOfRuina.framework.cards;
using LibraryOfRuina.monsters.TechnologyFloorLiberation;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.TechnologyFloorLiberation;

public sealed class MagicBulletDespairEgoCard : EgoCardBase
{
    public const int BaseDamage = 150;
    public const int ChaosLoss = 50;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(PreviewDamage, ValueProp.Move),
        new DynamicVar("ChaosLoss", ChaosLoss)
    ];

    protected override IEnumerable<string> ExtraRunAssetPaths =>
    [
        TechnologyFloorMagicBulletBoss.AttackSfxPath
    ];

    public MagicBulletDespairEgoCard()
        : base(1, previewDamage: BaseDamage)
    {
    }

    protected override void OnUpgrade()
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        return Task.CompletedTask;
    }
}
    