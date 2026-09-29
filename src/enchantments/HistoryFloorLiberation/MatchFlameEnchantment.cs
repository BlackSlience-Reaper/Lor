using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.enchantments.HistoryFloorLiberation;

public sealed class MatchFlameEnchantment : EnchantmentModel
{
    private const int DamageBonus = 2;
    private const int TargetBurnStacks = 3;
    private const int SelfBurnStacks = 5;
    private const int SelfBurnThresholdPlayCount = 4;

    public override bool HasExtraCardText => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(DamageBonus, ValueProp.Move),
        new DynamicVar("Burn", TargetBurnStacks),
        new DynamicVar("Threshold", SelfBurnThresholdPlayCount),
        new DynamicVar("SelfBurn", SelfBurnStacks)
    ];

    public override bool CanEnchantCardType(CardType cardType)
    {
        return cardType == CardType.Attack;
    }

    public override decimal EnchantDamageAdditive(decimal originalDamage, ValueProp props)
    {
        if (Status == EnchantmentStatus.Disabled || !ValuePropCompat.IsPoweredAttack(props))
        {
            return 0m;
        }

        return DynamicVars.Damage.BaseValue * GetCompletedPlayCountThisCombat();
    }

    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        if (cardPlay == null)
        {
            return;
        }

        foreach (Creature target in GetBurnTargets(cardPlay))
        {
            await PowerCmdCompat.Apply<LibraryBurnPower>(target, DynamicVars["Burn"].BaseValue, Card.Owner.Creature, cardPlay.Card);
        }

        int completedPlayCount = GetCompletedPlayCountThisCombat();
        if (completedPlayCount + 1 >= DynamicVars["Threshold"].IntValue)
        {
            await PowerCmdCompat.Apply<LibraryBurnPower>(Card.Owner.Creature, DynamicVars["SelfBurn"].BaseValue, Card.Owner.Creature, cardPlay.Card);
        }
    }

    private int GetCompletedPlayCountThisCombat()
    {
        if (!CombatManager.Instance.IsInProgress)
        {
            return 0;
        }

        return CombatManager.Instance.History.CardPlaysFinished.Count(entry => entry.CardPlay.Card == Card);
    }

    private IReadOnlyList<Creature> GetBurnTargets(CardPlay cardPlay)
    {
        List<Creature> targets = [];

        if (CombatManager.Instance.IsInProgress)
        {
            foreach (var entry in CombatManager.Instance.History.Entries.Reverse())
            {
                if (entry is CardPlayStartedEntry started && ReferenceEquals(started.CardPlay, cardPlay))
                {
                    break;
                }

                if (entry is DamageReceivedEntry damageEntry
                    && damageEntry.CardSource == cardPlay.Card
                    && damageEntry.Dealer == Card.Owner.Creature
                    && IsValidBurnTarget(damageEntry.Receiver))
                {
                    targets.Insert(0, damageEntry.Receiver);
                }
            }
        }

        if (cardPlay.Target is { } target && IsValidBurnTarget(target))
        {
            targets.Add(target);
        }

        return targets.Distinct().ToList();
    }

    private bool IsValidBurnTarget(Creature target)
    {
        return target.IsAlive && target.Side != Card.Owner.Creature.Side;
    }
}
