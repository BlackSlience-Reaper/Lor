#if STS2_0_111_0
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
    internal static AttackCommand FromCardCompat(this AttackCommand command, CardModel card, CardPlay? cardPlay = null) => command.FromCard(card, cardPlay);
    internal static LibraryAttackCommand FromCardCompat(this LibraryAttackCommand command, CardModel card, CardPlay? cardPlay = null) => command.FromCard(card, cardPlay);
    internal static Player PlayerCompat(this CardPlay play) => play.Player;
    internal static CardPlay? CardPlayCompat(this AttackCommand command) => command.CardPlay;
    internal static Task LoseBlock(PlayerChoiceContext context, Creature target, decimal amount, Creature? remover) => CreatureCmd.LoseBlock(context, target, amount, remover);
    internal static bool IsDirectionalNavigation(NControllerManager? manager) => manager?.IsUsingDirectionalNavigation == true;
    internal static ulong FallbackSeed(string label) => MegaCrit.Sts2.Core.Helpers.StringHelper.GetDeterministicHashCode(label);
    internal static bool HasHibernate(Creature creature) => creature.HasPower<HibernatePower>();
    internal static Rng CreateRng(ulong seed, string label) => new Rng(seed, label);
    internal static decimal ModifyDamage(IRunState runState, ICombatState? combatState, Creature? target, Creature? dealer, decimal damage, ValueProp props, CardModel? cardSource, CardPlay? cardPlay, ModifyDamageHookType modifyDamageHookType, CardPreviewMode previewMode, out IEnumerable<AbstractModel> modifiers) => Hook.ModifyDamage(runState, combatState, target, dealer, damage, props, cardSource, cardPlay, modifyDamageHookType, previewMode, out modifiers);
    // 伤害预览按 Hook.ModifyDamage 的实际参数表解释执行。
    internal static object?[] ModifyDamageHookArguments(IRunState runState, ICombatState? combatState, Creature? target, Creature? dealer, decimal damage, ValueProp props, CardModel? cardSource, CardPlay? cardPlay, ModifyDamageHookType modifyDamageHookType, CardPreviewMode previewMode, object modifiers) => [runState, combatState, target, dealer, damage, props, cardSource, cardPlay, modifyDamageHookType, previewMode, modifiers];
    internal static StringName Confirm => MegaInput.confirm;
    internal static void ActivateEvoke(DarkOrb orb, Creature target) => orb.ActivateEvoke([target]);
    // 伤害预览按充能球被动的实际方法解释执行：新版回合钩子经 TriggerPassive 调各球的 Passive。
    internal const string OrbPassiveMethod = nameof(OrbModel.TriggerPassive);
    internal static void ResetEventCombat(EventSynchronizer synchronizer)
    {
        synchronizer.BeforeExitingRoom();
        synchronizer.GenerateInternalCombatStateIfNecessary(synchronizer.GetLocalEvent());
    }
    // 新版参数是 canonicalEncounter，交给 EventCombatSynchronizer.ReadyToEnterCombat：它按引用比对各玩家的遭遇，
    // 全员就绪后才 ToMutable 建战斗；泛型重载传的也是 ModelDb.Encounter<T>() 本身。
    internal static EncounterModel EventCombatEncounter(EncounterModel canonicalEncounter) => canonicalEncounter;
    internal static CardModel CreateDupe(CardModel card, Player owner) => card.CreateDupe(owner);
    internal static Rng CreateDetachedRng(Rng source, string label)
    {
        var state = source.ToSerializable();
        ulong seed = state.state0;
        seed = unchecked((seed * 1099511628211UL) ^ state.state1);
        seed = unchecked((seed * 1099511628211UL) ^ state.state2);
        seed = unchecked((seed * 1099511628211UL) ^ state.state3);
        seed = unchecked((seed * 1099511628211UL) ^ (uint)state.counter);
        return new Rng(seed, label);
    }
}
#endif
