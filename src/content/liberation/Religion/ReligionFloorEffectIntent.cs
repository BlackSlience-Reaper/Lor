using System;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using System.Linq;
using LibraryOfRuina.framework.intents;

namespace LibraryOfRuina.content.liberation.Religion;

internal enum ReligionEffectKind { Defend, Buff, Debuff, CardDebuff, StatusCard, Summon }

internal sealed class ReligionFloorEffectIntent(string key, Func<int> amount, ReligionEffectKind kind) : AbstractIntent, IIntentTargetLineProvider
{
    public ReligionFloorEffectIntent(string key, int amount, ReligionEffectKind kind)
        : this(key, () => amount, kind)
    {
    }

    public IReadOnlyList<IntentTargetLineTarget> GetIntentTargetLineTargets(Creature owner, IReadOnlyList<Creature>? fallbackTargets)
    {
        if (owner.CombatState is not { } combat)
        {
            return [];
        }

        if (kind is ReligionEffectKind.Debuff or ReligionEffectKind.CardDebuff or ReligionEffectKind.StatusCard || key == "RELIGION_SALVATION")
        {
            return combat.PlayerCreatures.Where(static creature => creature.IsAlive)
                .Select(static creature => new IntentTargetLineTarget(creature)).ToArray();
        }
        if (key is "RELIGION_SCYTHE_BLOCK" or "RELIGION_STAFF_STRENGTH")
        {
            return combat.Enemies.Where(static creature => creature.IsAlive && creature.Monster is ReligionFloorApostle { IsFakeDead: false })
                .Select(static creature => new IntentTargetLineTarget(creature)).ToArray();
        }
        if (key == "RELIGION_WELCOME")
        {
            return combat.Enemies.Where(static creature => creature.IsAlive && creature.Monster is not ReligionFloorApostle { IsFakeDead: true })
                .Select(static creature => new IntentTargetLineTarget(creature)).ToArray();
        }
        return [];
    }

    public override IntentType IntentType => kind switch
    {
        ReligionEffectKind.Defend => IntentType.Defend,
        ReligionEffectKind.Buff => IntentType.Buff,
        ReligionEffectKind.Summon => IntentType.Summon,
        ReligionEffectKind.CardDebuff => IntentType.CardDebuff,
        ReligionEffectKind.StatusCard => IntentType.StatusCard,
        _ => IntentType.Debuff
    };

    protected override string IntentPrefix => kind switch
    {
        ReligionEffectKind.CardDebuff => "CARD_DEBUFF",
        ReligionEffectKind.StatusCard => "STATUS",
        _ => kind.ToString().ToUpperInvariant()
    };

    protected override string SpritePath =>
        kind == ReligionEffectKind.StatusCard
            ? "atlases/intent_atlas.sprites/intent_status_card.tres"
            : $"atlases/intent_atlas.sprites/intent_{IntentPrefix.ToLowerInvariant()}.tres";

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        var text = new LocString("intents", key + ".description");
        text.Add("Amount", amount());
        text.Add("Strength", ReligionFloorRules.WelcomeStrength);
        text.Add("Flaw", ReligionFloorStaffApostle.LampFlaw);
        text.Add("Frail", ReligionFloorScytheApostle.SonFrail);
        return text;
    }
}
