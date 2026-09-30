using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.abnormalities.Leticia;

public sealed class LeticiaElite : EncounterModel
{
    public override RoomType RoomType => RoomType.Elite;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots =>
    [
        "gift_left",
        "gift_right",
        "leticia"
    ];

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<SurpriseGiftBox>(),
        ModelDb.Monster<Leticia>(),
        ModelDb.Monster<LittleWitchFriend>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        var leftGift = ModelDb.Monster<SurpriseGiftBox>().ToMutable();
        var rightGift = ModelDb.Monster<SurpriseGiftBox>().ToMutable();

        return
        [
            (leftGift, "gift_left"),
            (rightGift, "gift_right"),
            (ModelDb.Monster<Leticia>().ToMutable(), "leticia")
        ];
    }
}
