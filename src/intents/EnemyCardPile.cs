using System;
using System.Linq;

namespace LibraryOfRuina.intents;

public sealed class EnemyCardPile
{
    private readonly List<EnemyCardSpec> _cards = [];

    public EnemyCardPile(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public int Count => _cards.Count;

    public IReadOnlyList<EnemyCardSpec> Cards => _cards;

    public void Clear()
    {
        _cards.Clear();
    }

    public void AddToTop(EnemyCardSpec card)
    {
        _cards.Insert(0, card ?? throw new ArgumentNullException(nameof(card)));
    }

    public void AddToBottom(EnemyCardSpec card)
    {
        _cards.Add(card ?? throw new ArgumentNullException(nameof(card)));
    }

    public void AddRangeToBottom(IEnumerable<EnemyCardSpec> cards)
    {
        _cards.AddRange(cards ?? throw new ArgumentNullException(nameof(cards)));
    }

    public bool Remove(EnemyCardSpec card)
    {
        return _cards.Remove(card);
    }

    public EnemyCardSpec? DrawTop()
    {
        if (_cards.Count == 0)
        {
            return null;
        }

        EnemyCardSpec card = _cards[0];
        _cards.RemoveAt(0);
        return card;
    }

    public void ReplaceWith(IEnumerable<EnemyCardSpec> cards)
    {
        _cards.Clear();
        _cards.AddRange(cards ?? throw new ArgumentNullException(nameof(cards)));
    }

    public IReadOnlyList<string> SnapshotIds()
    {
        return _cards.Select(static card => card.Id).ToArray();
    }
}
