using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.liberation.Technology;

[CardPool(typeof(TokenCardPool))]
public sealed class MagicBulletCommissionChoiceCard :
    MagicBulletShooterPageChoiceCardBase
{
    public override MagicBulletShooterPageMode PageMode => MagicBulletShooterPageMode.Commission;

    protected override string PortraitFileName =>
        "magic_bullet_commission_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class MagicBulletSeventhBulletChoiceCard :
    MagicBulletShooterPageChoiceCardBase
{
    public override MagicBulletShooterPageMode PageMode => MagicBulletShooterPageMode.SeventhBullet;

    protected override string PortraitFileName =>
        "magic_bullet_seventh_bullet_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class MagicBulletBlackFlameChoiceCard :
    MagicBulletShooterPageChoiceCardBase
{
    public override MagicBulletShooterPageMode PageMode => MagicBulletShooterPageMode.BlackFlame;

    protected override string PortraitFileName =>
        "magic_bullet_black_flame_choice_card.png";
}
