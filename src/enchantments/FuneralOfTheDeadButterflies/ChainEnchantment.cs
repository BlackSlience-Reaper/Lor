using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.combat;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.enchantments.FuneralOfTheDeadButterflies;

public sealed class ChainEnchantment : EnchantmentModel
{
    private const decimal ChainDamageRatio = 0.2m;
    private const int ChainDamagePercent = 20;
    private const int RepeatCount = 1;

    private bool _isResolvingChain;

    public override bool HasExtraCardText => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("DamagePercent", ChainDamagePercent),
        new DynamicVar("Repeat", RepeatCount)
    ];

    public override bool CanEnchantCardType(CardType cardType)
    {
        return cardType == CardType.Attack;
    }

    public override async Task AfterAttack(
        PlayerChoiceContext choiceContext,
        AttackCommand command)
    {
        if (_isResolvingChain
            || Status == EnchantmentStatus.Disabled
            || command.ModelSource is not CardModel cardSource
            || !ReferenceEquals(cardSource, Card)
            || command.Attacker != Card.Owner.Creature
            || !ValuePropCompat.IsPoweredAttack(command.DamageProps))
        {
            return;
        }

        decimal chainDamage = AttackCommandCompat.Results(command)
            .Where(result => result.Receiver.Side != Card.Owner.Creature.Side && result.UnblockedDamage > 0)
            .Sum(result => result.UnblockedDamage);
        chainDamage = (int)(chainDamage * ChainDamageRatio);
        if (chainDamage <= 0)
        {
            return;
        }
        HashSet<Creature> hitTargets = AttackCommandCompat.Results(command).Select(result => result.Receiver).ToHashSet();

        IReadOnlyList<Creature> targets = AllyTurnRegistry
            .FilterPlayerEnemyTargets(Card.Owner.Creature.CombatState?.Enemies)
            .Where(enemy => enemy.IsAlive && !hitTargets.Contains(enemy))
            .ToArray();
        if (targets.Count == 0)
        {
            return;
        }

        _isResolvingChain = true;
        try
        {
            await CreatureCmdCompat.Damage(
                choiceContext,
                targets,
                chainDamage,
                ValueProp.Unblockable | ValueProp.Unpowered,
                Card.Owner.Creature,
                Card,
                command.CardPlay);
        }
        finally
        {
            _isResolvingChain = false;
        }
    }
}
