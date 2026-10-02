#if STS2_0_107_1
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Modifiers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;
namespace LibraryOfRuinaVerification;
internal static partial class VerificationApi
{
    internal static IEnumerable<AbstractModel> AllModels => ModelDb.AllAbstractModelSubtypes.Where(ModelDb.Contains).Select(type => ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)));
    internal static int CounterCompat(this Rng rng) => rng.Counter;
    internal static CardPlay CreateCardPlay(CardModel card, Player player)
    {
        if (!ReferenceEquals(card.Owner, player)) throw new InvalidOperationException("旧版 CardPlay 的玩家必须等于 Card.Owner。");
        return new CardPlay
        {
            Card = card,
            Target = null,
            ResultPile = PileType.Discard,
            Resources = new ResourceInfo { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
            IsAutoPlay = false, PlayIndex = 0, PlayCount = 1
        };
    }
    internal static decimal ModifyDamageMultiplicativeCompat(this AbstractModel model, Creature? target, decimal damage, ValueProp props, Creature? dealer, CardModel? card, CardPlay? play) => model.ModifyDamageMultiplicative(target, damage, props, dealer, card);
    internal static decimal ModifyDamageAdditiveCompat(this AbstractModel model, Creature? target, decimal damage, ValueProp props, Creature? dealer, CardModel? card, CardPlay? play) => model.ModifyDamageAdditive(target, damage, props, dealer, card);
    internal static Task BeforeSideTurnEnd(CombatState state, CombatSide side, IEnumerable<Creature> creatures) => Hook.BeforeTurnEnd(state, side, creatures);
    // 对应 0.107.1 NDailyRunScreen.RollModifiers；这些夹具用空角色排除集。
    internal static IEnumerable<ModifierModel> DailyModifiers()
    {
        var rng = new Rng(7u);
        var available = ModelDb.GoodModifiers.ToList().StableShuffle(rng);
        var result = new List<ModifierModel>();
        for (int i = 0; i < 2; i++)
        {
            ModifierModel canonical = rng.NextItem(available) ?? throw new InvalidOperationException("没有足够的每日挑战词条。");
            ModifierModel mutable = canonical.ToMutable();
            if (mutable is CharacterCards cards) cards.CharacterModel = rng.NextItem(ModelDb.AllCharacters).Id;
            result.Add(mutable);
            available.Remove(canonical);
            var exclusive = ModelDb.MutuallyExclusiveModifiers.FirstOrDefault(set => set.Contains(canonical));
            if (exclusive != null) available.RemoveAll(exclusive.Contains);
        }
        result.Add(rng.NextItem(ModelDb.BadModifiers).ToMutable());
        return result;
    }
}
#endif
