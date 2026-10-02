#if STS2_0_111_0
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;
namespace LibraryOfRuinaVerification;
internal static partial class VerificationApi
{
    internal static IEnumerable<AbstractModel> AllModels => ModelDb.All;
    internal static int CounterCompat(this Rng rng) => rng.ToSerializable().counter;
    internal static CardPlay CreateCardPlay(CardModel card, Player player)
    {
        return new CardPlay
        {
            Card = card,
            Player = player,
            Target = null,
            ResultPile = PileType.Discard,
            Resources = new ResourceInfo { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
            IsAutoPlay = false, PlayIndex = 0, PlayCount = 1
        };
    }
    internal static decimal ModifyDamageMultiplicativeCompat(this AbstractModel model, Creature? target, decimal damage, ValueProp props, Creature? dealer, CardModel? card, CardPlay? play) => model.ModifyDamageMultiplicative(target, damage, props, dealer, card, play);
    internal static decimal ModifyDamageAdditiveCompat(this AbstractModel model, Creature? target, decimal damage, ValueProp props, Creature? dealer, CardModel? card, CardPlay? play) => model.ModifyDamageAdditive(target, damage, props, dealer, card, play);
    internal static Task BeforeSideTurnEnd(CombatState state, CombatSide side, IEnumerable<Creature> creatures) => Hook.BeforeSideTurnEnd(state, side, creatures);
    internal static IEnumerable<ModifierModel> DailyModifiers() => ModifierModel.Pick2Good1Bad(new Rng(7uL), []);
}
#endif
