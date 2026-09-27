using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.TechnologyFloorLiberation;

[CardPool(typeof(TokenCardPool))]
public sealed class MagicBulletCommissionChoiceCard :
    MagicBulletShooterPageChoiceCardBase
{
    protected override string PortraitFileName =>
        "magic_bullet_commission_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class MagicBulletSeventhBulletChoiceCard :
    MagicBulletShooterPageChoiceCardBase
{
    protected override string PortraitFileName =>
        "magic_bullet_seventh_bullet_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class MagicBulletBlackFlameChoiceCard :
    MagicBulletShooterPageChoiceCardBase
{
    protected override string PortraitFileName =>
        "magic_bullet_black_flame_choice_card.png";
}
