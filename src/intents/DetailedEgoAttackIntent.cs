using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.intents;

public sealed class PlayCardAttackIntent<TCard> :
    SingleAttackIntent,
    IEnemyCardIntent,
    IEnemyCardPreviewConfigurator,
    IBadgedIntent,
    IGroupAttackIntent
    where TCard : CardModel
{
    private readonly EnemyCardSpec _card;
    private readonly Func<int> _repeatCalc;
    private readonly Func<decimal>[] _additionalDamageCalcs;

    public PlayCardAttackIntent(
        string id,
        Func<decimal> damageCalc,
        Action<TCard, IReadOnlyList<int>>? configureCard = null,
        Func<int>? repeatCalc = null,
        params Func<decimal>[] additionalDamageCalcs)
        : this(id, damageCalc, configureCard, repeatCalc, badges: null, additionalDamageCalcs: additionalDamageCalcs)
    {
    }

    public PlayCardAttackIntent(
        string id,
        Func<decimal> damageCalc,
        Action<TCard, IReadOnlyList<int>>? configureCard,
        Func<int>? repeatCalc,
        IReadOnlyList<IntentBadge>? badges,
        params Func<decimal>[] additionalDamageCalcs)
        : base(damageCalc)
    {
        _repeatCalc = repeatCalc ?? (() => 1);
        _additionalDamageCalcs = additionalDamageCalcs ?? [];
        Badges = badges == null || badges.Count == 0
            ? Array.Empty<IntentBadge>()
            : badges;
        _card = new EnemyCardSpec(
            id,
            () => CreateCard(configureCard, damageCalc, _additionalDamageCalcs, Repeats),
            cost: 0,
            priority: 0,
            execute: static (_, _) => Task.CompletedTask,
            createIntent: _ => this);
    }

    public override int Repeats => Math.Max(1, _repeatCalc());

    public EnemyCardSpec EnemyCard => _card;

    public IntentBadge Badge =>
        Badges.Count > 0
            ? Badges[0]
            : throw new InvalidOperationException($"{nameof(PlayCardAttackIntent<TCard>)} has no badges.");

    public IReadOnlyList<IntentBadge> Badges { get; }

    public bool IsGroupAttack => IntentEffectCollection.HasGroupAttack(Badges);

    protected override LocString IntentLabelFormat =>
        Repeats > 1
            ? new LocString("intents", "FORMAT_DAMAGE_MULTI")
            : new LocString("intents", "FORMAT_DAMAGE_SINGLE");

    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths
            .Concat(_card.AssetPaths)
            .Concat(Badges.SelectMany(static badge => badge.AssetPaths));

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner)
    {
        return GetSingleDamage(targets, owner) * Repeats;
    }

    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        LocString fmt = IntentLabelFormat;
        fmt.Add("Damage", GetSingleDamage(targets, owner));
        if (Repeats > 1)
        {
            fmt.Add("Repeat", Repeats);
        }

        return fmt;
    }

    public void ConfigureEnemyCardPreview(
        CardModel card,
        IEnumerable<Creature> targets,
        Creature owner)
    {
        if (card is IEnemyAttackPreviewCard previewCard)
        {
            previewCard.SetEnemyAttackPreview(
                GetPreviewDamages(targets, owner),
                Repeats);
        }
    }

    private IReadOnlyList<int> GetPreviewDamages(IEnumerable<Creature> targets, Creature owner)
    {
        int[] damages = new int[1 + _additionalDamageCalcs.Length];
        damages[0] = GetSingleDamage(targets, owner);
        for (int i = 0; i < _additionalDamageCalcs.Length; i++)
        {
            damages[i + 1] = new SingleAttackIntent(_additionalDamageCalcs[i]).GetSingleDamage(targets, owner);
        }

        return damages;
    }

    private static CardModel CreateCard(
        Action<TCard, IReadOnlyList<int>>? configureCard,
        Func<decimal> damageCalc,
        Func<decimal>[] additionalDamageCalcs,
        int repeats)
    {
        TCard card = (TCard)ModelDb.Card<TCard>().ToMutable();
        int[] damages = new int[1 + additionalDamageCalcs.Length];
        damages[0] = Math.Max(0, (int)damageCalc());
        for (int i = 0; i < additionalDamageCalcs.Length; i++)
        {
            damages[i + 1] = Math.Max(0, (int)additionalDamageCalcs[i]());
        }

        configureCard?.Invoke(card, damages);
        if (card is IEnemyAttackPreviewCard previewCard)
        {
            previewCard.SetEnemyAttackPreview(damages, repeats);
        }

        return card;
    }
}
