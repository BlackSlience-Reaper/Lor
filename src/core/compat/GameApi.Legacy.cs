#if STS2_0_107_1
using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
namespace LibraryOfRuina.core.compat;
internal static partial class GameApi
{
    internal static AttackCommand FromCardCompat(this AttackCommand command, CardModel card, CardPlay? cardPlay = null) => command.FromCard(card);
    internal static LibraryAttackCommand FromCardCompat(this LibraryAttackCommand command, CardModel card, CardPlay? cardPlay = null) => command.FromCard(card, cardPlay);
    internal static Player PlayerCompat(this CardPlay play) => play.Card.Owner;
    internal static CardPlay? CardPlayCompat(this AttackCommand command) => null;
    internal static Task LoseBlock(PlayerChoiceContext context, Creature target, decimal amount, Creature? remover) => CreatureCmd.LoseBlock(target, amount);
    internal static bool IsDirectionalNavigation(NControllerManager? manager) => manager?.IsUsingController == true;
    internal static ulong FallbackSeed(string label) => unchecked((uint)MegaCrit.Sts2.Core.Helpers.StringHelper.GetDeterministicHashCode(label));
    internal static bool HasHibernate(Creature creature) => false;
    internal static Rng CreateRng(ulong seed, string label) => new Rng(unchecked((uint)seed), label);
    internal static decimal ModifyDamage(IRunState runState, ICombatState? combatState, Creature? target, Creature? dealer, decimal damage, ValueProp props, CardModel? cardSource, CardPlay? cardPlay, ModifyDamageHookType modifyDamageHookType, CardPreviewMode previewMode, out IEnumerable<AbstractModel> modifiers) => Hook.ModifyDamage(runState, combatState, target, dealer, damage, props, cardSource, modifyDamageHookType, previewMode, out modifiers);
    internal static StringName Confirm => MegaInput.accept;
    internal static void ActivateEvoke(DarkOrb orb, Creature target) { } // 旧版激发没有此通知。
    internal static void ResetEventCombat(EventSynchronizer synchronizer) { } // 旧版没有事件战斗同步器。
    // 旧版 EventModel.EnterCombatWithoutExitingEvent 的参数是 mutableEncounter，直接 new CombatState，
    // 后者对遭遇调用 AssertMutable；泛型重载自己传 ModelDb.Encounter<T>().ToMutable()。
    internal static EncounterModel EventCombatEncounter(EncounterModel canonicalEncounter) => canonicalEncounter.ToMutable();
    // 旧版 CreateDupe 经 CreateClone → CombatState.CloneCard（MemberwiseClone）保留原主人，原版 HistoryCourse
    // 直接打出该复制品；Owner 的 setter 在已有主人时抛 "already has an owner"，旧版也没有转交主人的入口。
    // 因此主人不一致时在造牌前明确失败，不改写复制品的主人。
    internal static CardModel CreateDupe(CardModel card, Player owner)
    {
        if (!ReferenceEquals(card.Owner, owner))
        {
            throw new InvalidOperationException(
                $"Cannot duplicate card {card.Id.Entry} for another player on 0.107.1; the duplicate keeps its original owner.");
        }

        return card.CreateDupe();
    }
    internal static Rng CreateDetachedRng(Rng source, string label) =>
        new Rng(unchecked(source.Seed + (uint)source.Counter), label);
}
#endif
