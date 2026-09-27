using System;
using System.Linq;
using LibraryOfRuina.relics.LeopardPlush;
using LibraryOfRuina.relics.StandaloneRelics;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.monsters.Yanami;

public sealed class Yanami : AncientEventModel
{
    private enum OptionId
    {
        LeftoverSoda,
        FriesKnuckles,
        LoveBento,
        RestaurantReceipt,
        TeardropPendant,
        GhostClubMember,
        SchoolFestivalMagic,
        FiveHundredYenBill,
        LeopardPlush,
        FerrisWheelTicket
    }

    private static readonly OptionId[] GroupOne =
    [
        OptionId.LeftoverSoda,
        OptionId.FriesKnuckles,
        OptionId.LoveBento,
        OptionId.RestaurantReceipt
    ];

    private static readonly OptionId[] GroupTwo =
    [
        OptionId.TeardropPendant,
        OptionId.GhostClubMember,
        OptionId.SchoolFestivalMagic
    ];

    private static readonly OptionId[] GroupThree =
    [
        OptionId.FiveHundredYenBill,
        OptionId.LeopardPlush,
        OptionId.FerrisWheelTicket
    ];

    private static readonly OptionId[] AllOptions = GroupOne.Concat(GroupTwo).Concat(GroupThree).ToArray();

    public override IEnumerable<EventOption> AllPossibleOptions => AllOptions.Select(CreateOption);
   


    protected override AncientDialogueSet DefineDialogues()
    {
        return new AncientDialogueSet
        {
            FirstVisitEverDialogue = new AncientDialogue(""),
            CharacterDialogues = [],
            AgnosticDialogues =
            [
                new AncientDialogue(""),
                new AncientDialogue(""),
                new AncientDialogue(""),
                new AncientDialogue(""),
                new AncientDialogue("")
            ]
        };
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        OptionId[] groupTwoCandidates = BuildGroupTwoCandidates();
        return
        [
            PickOne(GroupOne),
            PickOne(groupTwoCandidates),
            PickOne(GroupThree)
        ];
    }

    private OptionId[] BuildGroupTwoCandidates()
    {
        if (TeardropPendantRelic.HasEnchantTargets(Owner))
        {
            return GroupTwo;
        }

        return GroupTwo.Where(id => id != OptionId.TeardropPendant).ToArray();
    }

    private EventOption PickOne(IReadOnlyList<OptionId> options)
    {
        return CreateOption(Rng.NextItem(options));
    }

    private EventOption CreateOption(OptionId optionId)
    {
        return optionId switch
        {
            OptionId.LeftoverSoda => RelicOption<LeftoverSodaRelic>(),
            OptionId.FriesKnuckles => RelicOption<FriesKnucklesRelic>(),
            OptionId.LoveBento => RelicOption<LoveBentoRelic>(),
            OptionId.RestaurantReceipt => RelicOption<RestaurantReceiptRelic>(),
            OptionId.TeardropPendant => RelicOption<TeardropPendantRelic>(),
            OptionId.GhostClubMember => RelicOption<GhostClubMemberRelic>(),
            OptionId.SchoolFestivalMagic => RelicOption<SchoolFestivalMagicRelic>(),
            OptionId.FiveHundredYenBill => RelicOption<FiveHundredYenBillRelic>(),
            OptionId.LeopardPlush => RelicOption<LeopardPlushRelic>(),
            OptionId.FerrisWheelTicket => RelicOption<FerrisWheelTicketRelic>(),
            _ => throw new ArgumentOutOfRangeException(nameof(optionId), optionId, null)
        };
    }
}
