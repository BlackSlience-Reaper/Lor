using System;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.framework.intents;

public enum EnemyCardDestination
{
    Discard,
    Exhaust,
    Retain
}

public sealed class EnemyCardSpec
{
    public EnemyCardSpec(
        string id,
        Func<CardModel> createDisplayCard,
        int cost,
        int priority,
        Func<EnemyCardRuntime, IReadOnlyList<Creature>, Task> execute,
        Func<EnemyCardSpec, AbstractIntent> createIntent,
        EnemyCardDestination destination = EnemyCardDestination.Discard)
    {
        Id = id;
        CreateDisplayCard = createDisplayCard ?? throw new ArgumentNullException(nameof(createDisplayCard));
        Cost = cost;
        Priority = priority;
        Execute = execute ?? throw new ArgumentNullException(nameof(execute));
        CreateIntent = createIntent ?? throw new ArgumentNullException(nameof(createIntent));
        Destination = destination;
    }

    public string Id { get; }

    public Func<CardModel> CreateDisplayCard { get; }

    public int Cost { get; }

    public int Priority { get; }

    public Func<EnemyCardRuntime, IReadOnlyList<Creature>, Task> Execute { get; }

    public Func<EnemyCardSpec, AbstractIntent> CreateIntent { get; }

    public EnemyCardDestination Destination { get; }

    public IEnumerable<string> AssetPaths => CreateDisplayCard().AllPortraitPaths;

    public AbstractIntent CreateIntentInstance() => CreateIntent(this);

    public CardModel CreateDisplayCardForIntent(
        AbstractIntent intent,
        IEnumerable<Creature> targets,
        Creature owner)
    {
        CardModel card = CreateDisplayCard();
        if (intent is IEnemyCardPreviewConfigurator configurator)
        {
            configurator.ConfigureEnemyCardPreview(card, targets, owner);
        }

        return card;
    }

    public static CardModel CreateDisplayCardModel<TCard>(Action<TCard>? configure = null)
        where TCard : CardModel
    {
        TCard card = (TCard)ModelDb.Card<TCard>().ToMutable();
        configure?.Invoke(card);
        return card;
    }

    public static IReadOnlyList<AbstractIntent> CreateIntentSequence(IEnumerable<EnemyCardSpec> specs)
    {
        return specs.Select(static spec => spec.CreateIntentInstance()).ToArray();
    }
}

internal interface IEnemyCardPreviewConfigurator
{
    void ConfigureEnemyCardPreview(
        CardModel card,
        IEnumerable<Creature> targets,
        Creature owner);
}
