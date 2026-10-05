using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LibraryLib.Commands;
using LibraryLib.Entities.Creatures;
using LibraryLib.Hooks;
using LibraryLib.Models;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.ui.DamagePreview;

internal sealed partial class IncomingDamageSimulation
{
    private readonly Dictionary<PowerModel, int> _powerAmounts = [];
    private readonly HashSet<PowerModel> _removedPowers = [];
    private readonly Dictionary<Creature, List<PowerModel>> _addedPowers = [];
    private readonly HashSet<CardModel> _exhaustedCards = [];
    private static readonly HashSet<MethodInfo> ReportedUnsupportedHooks = [];
    private IncomingDamagePreviewHookReader? _hookReader;

    internal IncomingDamagePreviewHookReader HookReader => _hookReader ??= new(this);

    internal CombatSide PreviewSide { get; private set; } = CombatSide.Player;

    internal bool IsPartial { get; private set; }

    private static string SourceIcon(AbstractModel? source)
    {
        string icon = source == null ? "" : DamagePreviewTrace.Name(source);
        return icon is "◇" or "◆" ? "" : icon;
    }

    internal decimal EvaluateDecimalHook(Type hookType, string name, params object?[] args)
        => Convert.ToDecimal(EvaluateHook(hookType, name, args));

    internal object? EvaluateHook(Type hookType, string name, params object?[] args)
    {
        MethodInfo method = IncomingDamagePreviewHookReader.ResolveHookOverload(hookType, name, args);
        return HookReader.Invoke(method, null, args);
    }

    internal static IncomingDamagePreviewHookReader.Reference OutputReference(Action<object?> write) => new(() => null, write);

    internal IEnumerable<AbstractModel> ActiveListeners() =>
        Combat.IterateHookListeners()
            .Where(model => model is not PowerModel power || !_removedPowers.Contains(power))
            .Where(model => model is not LibraryPowerModeModel mode
                || mode.SourcePower != null && !_removedPowers.Contains(mode.SourcePower))
            .Where(model => model is not CardModel card || !_exhaustedCards.Contains(card))
            .Concat(_addedPowers.Values.SelectMany(static powers => powers));

    internal PowerModel[] ActivePowers(Creature creature) =>
        creature.Powers.Where(power => !_removedPowers.Contains(power))
            .Concat(_addedPowers.GetValueOrDefault(creature) ?? []).ToArray();

    internal void SimulateHooks(string name, params object?[] arguments)
    {
        foreach (AbstractModel listener in ActiveListeners().ToArray())
        {
            MethodInfo? method = AccessTools.Method(listener.GetType(), name,
                typeof(AbstractModel).GetMethods().First(candidate => candidate.Name == name).GetParameters()
                    .Select(static parameter => parameter.ParameterType).ToArray());
            if (method != null && method.DeclaringType != typeof(AbstractModel))
            {
                HookReader.TryInvoke(listener, method, arguments);
            }
        }
    }

    internal void SimulateTurnEnd(CombatSide side, IReadOnlyList<Creature> participants, bool before)
    {
        PreviewSide = side;
        string[] methods = before
            ? [nameof(AbstractModel.BeforeSideTurnEndVeryEarly), nameof(AbstractModel.BeforeSideTurnEndEarly), nameof(AbstractModel.BeforeSideTurnEnd)]
            : [nameof(AbstractModel.AfterSideTurnEnd), nameof(AbstractModel.AfterSideTurnEndLate)];
        foreach (string method in methods)
        {
            SimulateHooks(method, null, side, participants);
        }
    }

    internal void SimulateTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        PreviewSide = side;
        foreach (IncomingDamageTargetState state in States)
        {
            state.HpLostThisSide = 0;
        }

        SimulateHooks(nameof(AbstractModel.BeforeSideTurnStart), null, side, participants, Combat);
        foreach (Creature creature in participants)
        {
            if (!TryGetState(creature, out IncomingDamageTargetState? state))
            {
                continue;
            }

            AbstractModel? source = creature.Monster ?? (AbstractModel?)creature.Player?.Character;
            if (source != null)
            {
                MethodInfo method = AccessTools.Method(typeof(Hook), nameof(Hook.ShouldClearBlock));
                HookReader.TryExecute(source, method, () =>
                {
                    bool clear = (bool)EvaluateHook(typeof(Hook), nameof(Hook.ShouldClearBlock),
                        Combat, creature, OutputReference(_ => { }))!;
                    if (clear)
                    {
                        state!.Block = 0;
                    }
                });
            }
        }

        if (side == CombatSide.Player)
        {
            foreach (Creature creature in participants)
            {
                if (creature.Player is not { } player)
                {
                    continue;
                }

                SimulateHooks(nameof(AbstractModel.AfterPlayerTurnStartEarly), null, player);
                SimulateHooks(nameof(AbstractModel.AfterPlayerTurnStart), null, player);
                SimulateHooks(nameof(AbstractModel.AfterPlayerTurnStartLate), null, player);
            }
        }

        SimulateHooks(nameof(AbstractModel.AfterSideTurnStart), side, participants, Combat);
        SimulateHooks(nameof(AbstractModel.AfterSideTurnStartLate), side, participants, Combat);
    }

    internal void SimulateHand(Creature creature)
    {
        if (creature.Player?.PlayerCombatState is not { } combat)
        {
            return;
        }

        foreach (OrbModel orb in combat.OrbQueue.Orbs.ToArray())
        {
            MethodInfo method = AccessTools.Method(orb.GetType(), GameApi.OrbPassiveMethod);
            HookReader.TryInvoke(orb, method, null, null);
        }

        // 直接读每张卡的真实回合结束方法，条件和动态计算也由其 IL 解释。
        foreach (CardModel card in combat.Hand.Cards.ToArray())
        {
            if (card.HasTurnEndInHandEffect && !_exhaustedCards.Contains(card))
            {
                MethodInfo method = AccessTools.Method(card.GetType(), "OnTurnEndInHand");
                HookReader.TryInvoke(card, method, [null]);
            }
        }
    }

    internal void RecordUnsupportedHook(AbstractModel source, MethodInfo method, string reason)
    {
        IsPartial = true;
        if (ReportedUnsupportedHooks.Add(method))
        {
            Log.Info($"[LibraryOfRuina.IncomingPreview] Partial preview: unsupported hook {source.GetType().FullName}.{method.Name}: {reason}");
        }
    }

    internal bool TryReadPreviewField(object? owner, FieldInfo field, out object? value)
    {
        value = null;
        if (owner is Creature creature && TryGetState(creature, out IncomingDamageTargetState? state))
        {
            if (field.Name == "_currentHp")
            {
                value = state!.Hp;
                return true;
            }

            if (field.Name == "_block")
            {
                value = state!.Block;
                return true;
            }

            if (creature is LibraryCreature && field.Name == "_currentChaoValue")
            {
                value = state!.ChaoValue;
                return true;
            }

            if (creature is LibraryCreature && field.Name == "<IsChaoed>k__BackingField")
            {
                value = state!.IsChaoed;
                return true;
            }
        }

        if (owner is PowerModel power && field.Name == "_amount")
        {
            value = _powerAmounts.TryGetValue(power, out int amount) ? amount : field.GetValue(power);
            return true;
        }

        if (owner == Combat && field.Name.Contains("CurrentSide", StringComparison.Ordinal))
        {
            value = PreviewSide;
            return true;
        }

        return false;
    }

    internal bool TryWritePreviewField(object? owner, FieldInfo field, object? value)
    {
        if (owner is PowerModel power && field.Name == "_amount")
        {
            _powerAmounts[power] = Convert.ToInt32(value);
            return true;
        }

        if (owner is Creature creature && TryGetState(creature, out IncomingDamageTargetState? state))
        {
            if (field.Name == "_currentHp")
            {
                state!.Hp = Convert.ToInt32(value);
                return true;
            }

            if (field.Name == "_block")
            {
                state!.Block = Convert.ToInt32(value);
                return true;
            }

            if (creature is LibraryCreature && field.Name == "_currentChaoValue")
            {
                state!.ChaoValue = Convert.ToInt32(value);
                return true;
            }

            if (creature is LibraryCreature && field.Name == "<IsChaoed>k__BackingField")
            {
                state!.IsChaoed = Convert.ToBoolean(value);
                return true;
            }
        }

        return false;
    }

    internal Action CaptureState()
    {
        var states = _states.ToDictionary(static pair => pair.Key, static pair => pair.Value.Snapshot());
        var amounts = new Dictionary<PowerModel, int>(_powerAmounts);
        var removed = new HashSet<PowerModel>(_removedPowers);
        var added = _addedPowers.ToDictionary(static pair => pair.Key, static pair => pair.Value.ToList());
        var exhausted = new HashSet<CardModel>(_exhaustedCards);
        var redirects = new Dictionary<Creature, int>(_redirectHp);
        return () =>
        {
            foreach (var pair in states)
            {
                _states[pair.Key].Restore(pair.Value);
            }

            Replace(_powerAmounts, amounts);
            _removedPowers.Clear();
            _removedPowers.UnionWith(removed);
            Replace(_addedPowers, added);
            _exhaustedCards.Clear();
            _exhaustedCards.UnionWith(exhausted);
            Replace(_redirectHp, redirects);
        };
    }

    private static void Replace<TKey, TValue>(Dictionary<TKey, TValue> target, Dictionary<TKey, TValue> source)
        where TKey : notnull
    {
        target.Clear();
        foreach (var pair in source)
        {
            target.Add(pair.Key, pair.Value);
        }
    }

    internal bool TryInterpretCommand(MethodBase method, object? instance, object?[] args, AbstractModel? source, out object? result)
    {
        result = null;
        Type? type = method.DeclaringType;
        string name = method.Name;
        if (name is "IterateHookListeners" or "IterateCombatHookListeners"
            && (instance is IRunState or CombatState || type == typeof(Hook)))
        {
            result = ActiveListeners().ToArray();
            return true;
        }

        if (instance == Combat && name == "get_CurrentSide")
        {
            result = PreviewSide;
            return true;
        }

        if (instance is Creature owner && name == "get_Powers")
        {
            result = ActivePowers(owner);
            return true;
        }

        if (instance is PowerModel powerAmount && name == "get_Amount")
        {
            result = _powerAmounts.GetValueOrDefault(powerAmount, powerAmount.Amount);
            return true;
        }

        if (instance is Creature targetState && TryGetState(targetState, out IncomingDamageTargetState? preview))
        {
            switch (name)
            {
                case "get_CurrentHp": result = preview!.Hp; return true;
                case "get_Block": result = preview!.Block; return true;
                case "get_IsAlive": result = preview!.Hp > 0; return true;
                case "get_IsDead": result = preview!.Hp <= 0; return true;
                case "GetHpPercentRemaining": result = (double)preview!.Hp / targetState.MaxHp; return true;
            }
        }

        if ((type == typeof(CreatureCmd) || type == typeof(LibraryCreatureCmd)) && name == nameof(CreatureCmd.Damage))
        {
            ParameterInfo[] parameters = method.GetParameters();
            object? Get(string parameterName) => args[Array.FindIndex(parameters, parameter => parameter.Name == parameterName)];
            object? Optional(string parameterName)
            {
                int index = Array.FindIndex(parameters, parameter => parameter.Name == parameterName);
                return index < 0 ? null : args[index];
            }

            DamageVar? damageVar = Optional("damageVar") as DamageVar;
            decimal amount = damageVar?.BaseValue ?? Convert.ToDecimal(Optional("amount") ?? Get("damageAmount"));
            ValueProp props = damageVar?.Props ?? (ValueProp)Convert.ToInt32(Get("props"));
            CardModel? card = Optional("cardSource") as CardModel;
            Creature? dealer = Optional("dealer") as Creature ?? card?.Owner.Creature;
            LibraryDamageType damageType = Optional("type") is LibraryDamageType libraryType ? libraryType : LibraryDamageType.None;
            object? targets = Optional("targets") ?? Optional("target");
            Creature[] targetList = targets is Creature target ? [target] : ((IEnumerable?)targets)?.Cast<Creature>().ToArray() ?? [];
            var results = new List<DamageResult>();
            foreach (Creature creature in targetList)
            {
                if (!TryGetState(creature, out IncomingDamageTargetState? state) || state!.IsDown)
                {
                    continue;
                }

                bool libraryCommand = type == typeof(LibraryCreatureCmd);
                IncomingHitOutcome outcome = ResolveHit(state, amount, props, dealer, card, damageType,
                    SourceIcon(source), true, forceLibraryPipeline: libraryCommand, resolveChaos: !libraryCommand);
                results.AddRange(DispatchDamageCallbacks(state, outcome, props, dealer, card));
            }

            result = results;
            return true;
        }

        if (type == typeof(CreatureCmd) && name is "GainBlock" or "Heal" or "Kill")
        {
            Creature creature = (Creature)args[0]!;
            if (!TryGetState(creature, out IncomingDamageTargetState? state))
            {
                return true;
            }

            if (name == "GainBlock")
            {
                decimal amount = args[1] is BlockVar block ? block.BaseValue : Convert.ToDecimal(args[1]);
                ValueProp props = args[1] is BlockVar blockVar ? blockVar.Props : (ValueProp)Convert.ToInt32(args[2]);
                int before = state!.Block;
                GainBlock(state, amount, props, SourceIcon(source));
                result = (decimal)(state.Block - before);
            }
            else if (name == "Heal")
            {
                int before = state!.Hp;
                state!.Hp = Math.Min(creature.MaxHp, state.Hp + (int)Convert.ToDecimal(args[1]));
                SimulateHooks(nameof(AbstractModel.AfterCurrentHpChanged), creature, (decimal)(state.Hp - before));
            }
            else
            {
                state!.HasIncomingDamage = true;
                state.HpLoss += state.Hp;
                state.Hp = 0;
                SimulateDeath(creature, args.OfType<bool>().FirstOrDefault());
            }

            return true;
        }

        if (type == typeof(PowerCmd) && name == "Remove")
        {
            PowerModel? power = args[0] as PowerModel;
            if (method.IsGenericMethod && args[0] is Creature creature)
            {
                Type powerType = method.GetGenericArguments()[0];
                power = ActivePowers(creature).FirstOrDefault(candidate => powerType.IsInstanceOfType(candidate));
            }

            if (power != null)
            {
                RemovePreviewPower(power);
            }

            return true;
        }

        if (type == typeof(PowerCmd) && name == "ModifyAmount")
        {
            PowerModel power = args.OfType<PowerModel>().First();
            int before = _powerAmounts.GetValueOrDefault(power, power.Amount);
            decimal offset = Convert.ToDecimal(args[Array.FindIndex(method.GetParameters(), parameter => parameter.Name == "offset")]);
            Creature? applier = args.Length > 3 ? args[3] as Creature : null;
            CardModel? card = args.Length > 4 ? args[4] as CardModel : null;
            decimal modified = ModifyPowerOffset(power, power.Owner, offset, applier, card);
            int after = before + (int)modified;
            _powerAmounts[power] = after;
            if ((int)modified != 0)
            {
                SimulateHooks(nameof(AbstractModel.AfterPowerAmountChanged), null, power, modified, applier, card);
            }

            if ((bool)HookReader.Invoke(AccessTools.Method(typeof(PowerModel), nameof(PowerModel.ShouldRemoveDueToAmount)), power, [])!)
            {
                RemovePreviewPower(power);
            }

            result = after;
            return true;
        }

        if (type == typeof(PowerCmd) && name == "Apply" && method.IsGenericMethod)
        {
            Type powerType = method.GetGenericArguments()[0];
            ParameterInfo[] parameters = method.GetParameters();
            object? Read(string parameter) => args[Array.FindIndex(parameters, candidate => candidate.Name == parameter)];
            object? targets = parameters.Any(static parameter => parameter.Name == "target") ? Read("target") : Read("targets");
            IEnumerable<Creature> creatures = targets is Creature creature ? [creature] : ((IEnumerable?)targets)?.Cast<Creature>() ?? [];
            int amount = (int)Convert.ToDecimal(Read("amount"));
            var applied = new List<PowerModel>();
            foreach (Creature creatureTarget in creatures)
            {
                PowerModel? power = ActivePowers(creatureTarget).FirstOrDefault(candidate => candidate.GetType() == powerType);
                if (power == null)
                {
                    MethodInfo factory = typeof(ModelDb).GetMethods().Single(candidate => candidate.Name == "Power" && candidate.IsGenericMethodDefinition);
                    PowerModel canonical = (PowerModel)factory.MakeGenericMethod(powerType).Invoke(null, null)!;
                    // 只做字段复制，不运行第三方 AfterCloned 或模型构造逻辑。
                    power = (PowerModel)AccessTools.Method(typeof(object), "MemberwiseClone").Invoke(canonical, null)!;
                    VanillaPrivate.AbstractModelIsMutable.Set(power, true);
                    VanillaPrivate.PowerModelOwner.Set(power, creatureTarget);
                    if (!_addedPowers.TryGetValue(creatureTarget, out List<PowerModel>? powers))
                    {
                        _addedPowers[creatureTarget] = powers = [];
                    }

                    powers.Add(power);
                }

                _powerAmounts[power] = _powerAmounts.GetValueOrDefault(power, power.Amount) + amount;
                applied.Add(power);
            }

            result = targets is Creature ? applied.FirstOrDefault() : applied;
            return true;
        }

        if (type == typeof(CardCmd) && name == "Exhaust")
        {
            CardModel card = args.OfType<CardModel>().First();
            _exhaustedCards.Add(card);
            SimulateHooks(nameof(AbstractModel.AfterCardExhausted), null, card, args.OfType<bool>().FirstOrDefault());
            return true;
        }

        // 随机数、选择、存档和未识别的 Cmd 不进入真实执行；解释器将拒绝这些调用。
        return false;
    }

    internal IReadOnlyList<DamageResult> DispatchDamageCallbacks(IncomingDamageTargetState state,
        IncomingHitOutcome outcome, ValueProp props, Creature? dealer, CardModel? card)
    {
        var results = (outcome.RedirectedResults ?? []).ToList();
        results.Add(new DamageResult(state.Creature, props)
        {
            BlockedDamage = outcome.Blocked,
            UnblockedDamage = outcome.HpLoss,
            WasFullyBlocked = outcome.Blocked > 0 && outcome.HpLoss == 0,
            WasBlockBroken = outcome.Blocked > 0 && state.Block == 0,
            WasTargetKilled = state.IsDown
        });
        foreach (DamageResult result in results)
        {
            if (result.UnblockedDamage > 0)
            {
                SimulateHooks(nameof(AbstractModel.AfterCurrentHpChanged), result.Receiver, -(decimal)result.UnblockedDamage);
            }

            SimulateHooks(nameof(AbstractModel.AfterDamageGiven), null, dealer, result, props, result.Receiver, card);
            SimulateHooks(nameof(AbstractModel.AfterDamageReceived), null, result.Receiver, result, props, dealer, card);
            SimulateHooks(nameof(AbstractModel.AfterDamageReceivedLate), null, result.Receiver, result, props, dealer, card);
            if (TryGetState(result.Receiver, out IncomingDamageTargetState? receiverState) && receiverState!.IsDown)
            {
                SimulateDeath(result.Receiver, false);
            }
        }

        return results;
    }

    private void SimulateDeath(Creature creature, bool force)
    {
        SimulateHooks(nameof(AbstractModel.BeforeDeath), creature);
        AbstractModel? preventer = null;
        bool dies = force || (bool)EvaluateHook(typeof(Hook), nameof(Hook.ShouldDie), Run, Combat, creature,
            OutputReference(value => preventer = value as AbstractModel))!;
        SimulateHooks(nameof(AbstractModel.AfterDeath), null, creature, !dies, 0f);
        if (!dies && preventer != null)
        {
            HookReader.TryInvoke(preventer,
                AccessTools.Method(preventer.GetType(), nameof(AbstractModel.AfterPreventingDeath)), creature);
        }
    }

    private void RemovePreviewPower(PowerModel power)
    {
        if (_removedPowers.Add(power))
        {
            HookReader.Invoke(AccessTools.Method(power.GetType(), nameof(PowerModel.AfterRemoved)), power, [power.Owner]);
        }
    }

    private decimal ModifyPowerOffset(PowerModel power, Creature target, decimal amount, Creature? applier, CardModel? card)
    {
        SimulateHooks(nameof(AbstractModel.BeforePowerAmountChanged), power, amount, target, applier, card);
        IEnumerable<AbstractModel> given = [];
        IEnumerable<AbstractModel> received = [];
        if (applier != null)
        {
            amount = EvaluateDecimalHook(typeof(Hook), nameof(Hook.ModifyPowerAmountGiven), Combat, power, applier,
                amount, target, card, OutputReference(value => given = ((IEnumerable?)value)?.Cast<AbstractModel>() ?? []));
        }

        amount = EvaluateDecimalHook(typeof(Hook), nameof(Hook.ModifyPowerAmountReceived), Combat, power, target,
            amount, applier, OutputReference(value => received = ((IEnumerable?)value)?.Cast<AbstractModel>() ?? []));
        foreach (AbstractModel modifier in given)
        {
            HookReader.TryInvoke(modifier, AccessTools.Method(modifier.GetType(), nameof(AbstractModel.AfterModifyingPowerAmountGiven)), power);
        }

        foreach (AbstractModel modifier in received)
        {
            HookReader.TryInvoke(modifier, AccessTools.Method(modifier.GetType(), nameof(AbstractModel.AfterModifyingPowerAmountReceived)), power);
        }

        return amount;
    }
}
