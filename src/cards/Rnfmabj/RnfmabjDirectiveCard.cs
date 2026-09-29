using System;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.specialguests.Rnfmabj;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.Rnfmabj;

[CardPool(typeof(QuestCardPool))]
public sealed class RnfmabjDirectiveCard() : CardModel(-1,
    CardType.Quest,
    CardRarity.Quest,
    TargetType.None,
    shouldShowInCardLibrary: false)
{
    public const string PortraitAssetPath =
        "res://images/packed/card_portraits/quest/rnfmabj_directive_card.png";

    private CardType[] _sequence = [];
    private int _progress;
    private int _taskNumber;
    private int _taskCount;
    private int _completedPlayers;
    private int _requiredPlayers;
    private bool _localPlayerCompleted;
    private bool _currentTaskCompleted;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _sequence = (CardType[])_sequence.Clone();
    }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public override string PortraitPath => PortraitAssetPath;

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    // public override IEnumerable<CardKeyword> CanonicalKeywords =>
    //     [CardKeyword.Unplayable];

    internal void SetPresentation(RnfmabjDirectiveSnapshot snapshot)
    {
        _sequence = (CardType[])snapshot.Sequence.Clone();
        _progress = Math.Clamp(snapshot.Progress, 0, _sequence.Length);
        _taskNumber = snapshot.TaskNumber;
        _taskCount = snapshot.TaskCount;
        _completedPlayers = snapshot.CompletedPlayers;
        _requiredPlayers = snapshot.RequiredPlayers;
        _localPlayerCompleted = snapshot.LocalPlayerCompleted;
        _currentTaskCompleted = snapshot.CurrentTaskCompleted;
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        description.Add("Sequence", BuildSequenceText());
        description.Add("TaskNumber", _taskNumber);
        description.Add("TaskCount", _taskCount);
        description.Add("CompletedPlayers", _completedPlayers);
        description.Add("RequiredPlayers", _requiredPlayers);
        description.Add("IsMultiplayer", _requiredPlayers > 1);
        description.Add(
            "State",
            _currentTaskCompleted
                ? 2
                : _localPlayerCompleted
                    ? 1
                    : 0);
    }

    private string BuildSequenceText()
    {
        var labels = new string[_sequence.Length];
        for (int index = 0; index < _sequence.Length; index++)
        {
            string label = _sequence[index].ToLocString().GetFormattedText();
            labels[index] = index < _progress
                ? "[green]" + label + "[/green]"
                : label;
        }

        return string.Join('-', labels);
    }
}
