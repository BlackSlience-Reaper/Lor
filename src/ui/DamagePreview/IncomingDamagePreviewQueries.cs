using System;
using System.Collections.Generic;
using System.Reflection;
using LibraryLib.Combat;
using LibraryLib.Hooks;
using LibraryLib.Models;
using LibraryOfRuina.content.liberation.Philosophy;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.core.compat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.ui.DamagePreview;

internal sealed partial class IncomingDamageSimulation
{
    private static readonly Dictionary<(Type Type, string Name, int Count, Type? Contract), MethodInfo?> QueryMethods = [];

    internal Creature? AffectedCreature { get; set; }

    /// <summary>直接按生产管线的顺序聚合查询；只有有效覆写进入隔离解释器，单个失败不会取消整次攻击。</summary>
    internal bool TryEvaluateQuery(MethodBase method, object?[] args, out object? result)
    {
        result = null;
        bool library = method.DeclaringType == typeof(LibraryHooks);
        if (!library && method.DeclaringType != typeof(Hook))
        {
            return false;
        }

        string name = method.Name;
        bool includeRun = name is "ModifyDamage" or "ModifyChaoDamage" or "ModifyHpLost"
            or "ModifyHpLostBeforeOsty" or "ModifyHpLostAfterOsty" or "ShouldDie";
        if (name == "ModifyDamageTarget" && method is MethodInfo targetMethod && targetMethod.ReturnType != typeof(Creature))
        {
            return false;
        }

        var parameters = method.GetParameters();
        object? Read(string parameter)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].Name == parameter)
                {
                    return args[i];
                }
            }

            return null;
        }

        Creature? target = (Read("target") ?? Read("originalTarget") ?? Read("creature")) as Creature;
        Creature? previousTarget = AffectedCreature;
        AffectedCreature = target ?? previousTarget;
        try
        {
            var modifiers = new List<AbstractModel>();
            decimal amount = Convert.ToDecimal(Read("amount") ?? Read("damage") ?? Read("chaoDamage") ?? Read("block") ?? 0m);
            ValueProp props = (ValueProp)(Read("props") ?? (ValueProp)0);
            Creature? dealer = Read("dealer") as Creature;
            CardModel? card = Read("cardSource") as CardModel;
            object? play = Read("cardPlay");
            object? damageType = Read("type");
            void ApplyFinalClamps()
            {
                foreach (AbstractModel listener in ActiveListeners(includeRun: true))
                {
                    if (listener is IFinalHpLossClamp)
                    {
                        decimal clamped = EvaluateQueryModel(listener, nameof(IFinalHpLossClamp.ClampFinalHpLoss),
                            [target, amount, props, dealer, card], amount, typeof(IFinalHpLossClamp));
                        if (clamped != amount && !modifiers.Contains(listener))
                        {
                            modifiers.Add(listener);
                        }

                        amount = clamped;
                    }
                }
            }

            if (name is "ModifyDamage" or "ModifyChaoDamage" or "ModifyHpLostBeforeOsty" or "ModifyHpLostAfterOsty" or "ModifyHpLost" or "ModifyBlock")
            {
                LibraryCombatValueKind kind = name switch
                {
                    "ModifyDamage" => LibraryCombatValueKind.PhysicalDamage,
                    "ModifyChaoDamage" => LibraryCombatValueKind.ChaoDamage,
                    "ModifyBlock" => LibraryCombatValueKind.Block,
                    _ => LibraryCombatValueKind.HpLoss
                };
                var context = new LibraryCombatValueContext(Combat, kind, amount, target, dealer, props, card,
                    play as MegaCrit.Sts2.Core.Entities.Cards.CardPlay,
                    damageType is LibraryDamageType type ? type : LibraryDamageType.None,
                    Read("previewMode") is MegaCrit.Sts2.Core.Entities.Cards.CardPreviewMode mode
                        ? mode : MegaCrit.Sts2.Core.Entities.Cards.CardPreviewMode.None);
                LibraryCombatValueResolution resolution = LibraryCombatValueResolution.Default;
                foreach (AbstractModel listener in ActiveListeners())
                {
                    if (listener is ILibraryCombatValueResolutionPolicy)
                    {
                        var candidate = EvaluateQueryModel(listener, nameof(ILibraryCombatValueResolutionPolicy.GetCombatValueResolution),
                            [new IncomingDamagePreviewHookReader.Reference(() => context, _ => { })],
                            LibraryCombatValueResolution.Default, typeof(ILibraryCombatValueResolutionPolicy));
                        resolution = LibraryCombatValueResolver.MostRestrictive(resolution, candidate);
                    }
                }

                if (resolution != LibraryCombatValueResolution.Default)
                {
                    amount = LibraryCombatValueResolver.ResolveBaseValue(resolution, amount);
                    // 基础库前缀提前返回后，生产管线的最终锁血后缀仍会执行。
                    if (name == "ModifyHpLostAfterOsty" || name == "ModifyHpLost"
                        && Read("phases") is HpLossHookPhase phases && phases.HasFlag(HpLossHookPhase.AfterOsty))
                    {
                        ApplyFinalClamps();
                    }

                    result = amount;
                    WriteModifiers();
                    return true;
                }
            }

            object?[] WithType(object?[] values) => library ? [.. values, damageType] : values;
            decimal Reduce(string hook, decimal value, object?[] values, string operation, bool includeVanilla = true)
            {
                foreach (AbstractModel listener in ActiveListeners(includeRun))
                {
                    values[1] = value;
                    decimal fallback = operation switch { "add" => 0m, "multiply" => 1m, "cap" => decimal.MaxValue, _ => value };
                    decimal modification = includeVanilla ? EvaluateQueryModel(listener, hook, values, fallback) : fallback;
                    if (library && listener is ILibraryAbstractModel)
                    {
                        if (operation == "replace")
                        {
                            values[1] = modification;
                            fallback = modification;
                        }

                        decimal extra = EvaluateQueryModel(listener, hook, WithType(values), fallback, typeof(ILibraryAbstractModel));
                        modification = operation switch
                        {
                            "add" => modification + extra,
                            "multiply" => modification * extra,
                            "cap" => Math.Min(modification, extra),
                            _ => extra
                        };
                    }

                    decimal before = value;
                    value = operation switch
                    {
                        "add" => value + modification,
                        "multiply" => value * modification,
                        "cap" => Math.Min(value, modification),
                        _ => modification
                    };
                    if (before != value && !modifiers.Contains(listener))
                    {
                        modifiers.Add(listener);
                    }
                }

                return value;
            }

            void WriteModifiers()
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].Name == "modifiers" && args[i] is IncomingDamagePreviewHookReader.Reference output)
                    {
                        output.Value = modifiers;
                    }
                }
            }

            if (name is "ModifyDamage" or "ModifyChaoDamage" or "ModifyBlock")
            {
                string stem = name;
                // Cap 参数没有 amount，单独处理，避免将 ValueProp 写进数值槽。
                object?[] values = name == "ModifyBlock" ? [target, amount, props, card, play]
                    : [target, amount, props, dealer, card, play];
                if (card?.Enchantment is { } enchantment && name != "ModifyChaoDamage")
                {
                    amount += EvaluateQueryModel(enchantment, name == "ModifyBlock" ? "EnchantBlockAdditive" : "EnchantDamageAdditive",
                        name == "ModifyBlock" ? [amount] : [amount, props], 0m);
                    amount *= EvaluateQueryModel(enchantment, name == "ModifyBlock" ? "EnchantBlockMultiplicative" : "EnchantDamageMultiplicative",
                        name == "ModifyBlock" ? [amount] : [amount, props], 1m);
                }

                bool additive = true;
                bool multiplicative = true;
                bool applyCap = name != "ModifyBlock";
                if (Read("modifyDamageHookType") is ModifyDamageHookType damagePhases)
                {
                    additive = damagePhases.HasFlag(ModifyDamageHookType.Additive);
                    multiplicative = damagePhases.HasFlag(ModifyDamageHookType.Multiplicative);
                    applyCap = library || damagePhases.HasFlag(ModifyDamageHookType.Cap);
                }
                else if (Read("modifyChaoDamageHookType") is ModifyChaoDamageHookType chaosPhases)
                {
                    additive = chaosPhases.HasFlag(ModifyChaoDamageHookType.Additive);
                    multiplicative = chaosPhases.HasFlag(ModifyChaoDamageHookType.Multiplicative);
                }

                if (additive)
                {
                    amount = Reduce(stem + "Additive", amount, values, "add", name != "ModifyChaoDamage");
                }

                if (multiplicative)
                {
                    amount = Reduce(stem + "Multiplicative", amount, values, "multiply", name != "ModifyChaoDamage");
                }

                if (applyCap)
                {
                    foreach (AbstractModel listener in ActiveListeners(includeRun))
                    {
                        object?[] capArgs = [target, props, dealer, card, play];
                        decimal cap = name == "ModifyChaoDamage" ? decimal.MaxValue
                            : EvaluateQueryModel(listener, stem + "Cap", capArgs, decimal.MaxValue);
                        if (library && listener is ILibraryAbstractModel)
                        {
                            cap = Math.Min(cap, EvaluateQueryModel(listener, stem + "Cap", WithType(capArgs), decimal.MaxValue,
                                typeof(ILibraryAbstractModel)));
                        }

                        if (amount > cap)
                        {
                            amount = cap;
                            if (!modifiers.Contains(listener))
                            {
                                modifiers.Add(listener);
                            }
                        }
                    }
                }

                result = Math.Max(0m, amount);
            }
            else if (name is "ModifyHpLost" or "ModifyHpLostBeforeOsty" or "ModifyHpLostAfterOsty")
            {
                HpLossHookPhase phases;
                if (library)
                {
                    phases = name.EndsWith("AfterOsty", StringComparison.Ordinal)
                        ? HpLossHookPhase.AfterOsty : HpLossHookPhase.BeforeOsty;
                }
                else
                {
                    phases = (HpLossHookPhase)Read("phases")!;
                }
                foreach (string phase in new[] { "BeforeOsty", "AfterOsty" })
                {
                    if (!(phase == "BeforeOsty" ? phases.HasFlag(HpLossHookPhase.BeforeOsty) : phases.HasFlag(HpLossHookPhase.AfterOsty)))
                    {
                        continue;
                    }

                    foreach (string suffix in new[] { "", "Late" })
                    {
                        amount = Reduce("ModifyHpLost" + phase + suffix, amount, [target, amount, props, dealer, card], "replace");
                    }
                }

                if (phases.HasFlag(HpLossHookPhase.AfterOsty))
                {
                    ApplyFinalClamps();
                }

                result = amount;
            }
            else if (name is "ModifyDamageTarget" or "ModifyUnblockedDamageTarget")
            {
                Creature receiver = target!;
                foreach (AbstractModel listener in ActiveListeners())
                {
                    if (name == "ModifyUnblockedDamageTarget")
                    {
                        receiver = EvaluateQueryModel(listener, name, [receiver, amount, props, dealer], receiver);
                    }

                    if (library && listener is ILibraryAbstractModel)
                    {
                        receiver = EvaluateQueryModel(listener, name, [receiver, amount, props, dealer, damageType], receiver,
                            typeof(ILibraryAbstractModel));
                    }
                }

                result = receiver;
            }
            else if (name is "ShouldClearBlock" or "ShouldDie")
            {
                result = true;
                foreach (AbstractModel listener in ActiveListeners(includeRun))
                {
                    if (!EvaluateQueryModel(listener, name, [target], true))
                    {
                        result = false;
                        if (Read("preventer") is IncomingDamagePreviewHookReader.Reference output)
                        {
                            output.Value = listener;
                        }

                        break;
                    }
                }
            }
            else if (name is "ModifyPowerAmountGiven" or "ModifyPowerAmountReceived")
            {
                PowerModel power = (PowerModel)(Read("power") ?? Read("canonicalPower"))!;
                Creature? giver = Read("giver") as Creature;
                if (name == "ModifyPowerAmountGiven")
                {
                    foreach (string phase in new[] { "Additive", "Multiplicative" })
                    {
                        foreach (AbstractModel listener in ActiveListeners())
                        {
                            decimal modifier = EvaluateQueryModel(listener, name + phase,
                                [power, giver, amount, target, card], phase == "Additive" ? 0m : 1m);
                            decimal before = amount;
                            amount = phase == "Additive" ? amount + modifier : amount * modifier;
                            if (before != amount && !modifiers.Contains(listener))
                            {
                                modifiers.Add(listener);
                            }
                        }
                    }
                }
                else
                {
                    foreach (AbstractModel listener in ActiveListeners())
                    {
                        object? updated = amount;
                        var output = new IncomingDamagePreviewHookReader.Reference(() => updated, value => updated = value);
                        if (EvaluateQueryModel(listener, "TryModifyPowerAmountReceived", [power, target, amount, giver, output], false))
                        {
                            amount = Convert.ToDecimal(updated);
                            modifiers.Add(listener);
                        }
                    }
                }

                result = amount;
            }
            else
            {
                return false;
            }

            WriteModifiers();
            return true;
        }
        finally
        {
            AffectedCreature = previousTarget;
        }
    }

    private T EvaluateQueryModel<T>(AbstractModel source, string name, object?[] args, T fallback, Type? contract = null)
    {
        if (contract == null)
        {
            args = GameApi.IncomingDamageModifierArguments(name, args);
        }

        var key = (source.GetType(), name, args.Length, contract);
        if (!QueryMethods.TryGetValue(key, out MethodInfo? method))
        {
            if (contract != null)
            {
                InterfaceMapping map = source.GetType().GetInterfaceMap(contract);
                int index = Array.FindIndex(map.InterfaceMethods, candidate => candidate.Name == name
                    && candidate.GetParameters().Length == args.Length);
                method = index >= 0 ? map.TargetMethods[index] : null;
            }
            else
            {
                foreach (MethodInfo candidate in source.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (candidate.Name == name && candidate.GetParameters().Length == args.Length)
                    {
                        method = candidate;
                        break;
                    }
                }
            }

            // 基类和默认接口的空实现不需要解释与事务快照。
            if (method?.DeclaringType == typeof(AbstractModel) || method?.DeclaringType?.IsInterface == true)
            {
                method = null;
            }

            QueryMethods[key] = method;
        }

        if (method == null)
        {
            return fallback;
        }

        object? result = fallback;
        if (!HookReader.TryExecute(source, method, () => result = HookReader.Invoke(method, source, args), isQuery: true))
        {
            return fallback;
        }

        if (result is T value)
        {
            return value;
        }

        RecordUnsupportedHook(source, method, "Preview query returned an incompatible value");
        return fallback;
    }
}
