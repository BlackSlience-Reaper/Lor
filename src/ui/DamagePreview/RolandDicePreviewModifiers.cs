using System;
using System.Linq.Expressions;
using HarmonyLib;
using LibraryLib.Localization.LibraryDynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.ui.DamagePreview;

internal static class RolandDicePreviewModifiers
{
    private static Func<CardModel, LibraryDice, (decimal Minimum, decimal Maximum)>? _getPreview;

    private static bool _initialized;

    internal static (decimal Minimum, decimal Maximum) Get(CardModel card, LibraryDice die)
    {
        if (!_initialized)
        {
            _getPreview = BindPreview();
            _initialized = true;
        }

        return _getPreview?.Invoke(card, die) ?? (0m, 0m);
    }

    private static Func<CardModel, LibraryDice, (decimal Minimum, decimal Maximum)>? BindPreview()
    {
        // 罗兰为可选模组；直接复用其卡面预览入口，统一处理 Power、卡牌、回合和单骰修正，
        // 并由该入口扣除已经写入骰子的加成，避免重复计算。
        Type? modifiers = AccessTools.TypeByName("RolandMod.Cards.RolandDiceValueModifiers");
        if (modifiers == null)
        {
            return null;
        }

        var method = AccessTools.Method(modifiers, "GetPreview", [typeof(CardModel), typeof(LibraryDice)]);
        var card = Expression.Parameter(typeof(CardModel), "card");
        var die = Expression.Parameter(typeof(LibraryDice), "die");
        var result = Expression.Variable(method.ReturnType, "modifier");
        var tuple = Expression.New(
            typeof(ValueTuple<decimal, decimal>).GetConstructor([typeof(decimal), typeof(decimal)])!,
            Expression.Property(result, "MinimumDelta"),
            Expression.Property(result, "MaximumDelta"));
        var body = Expression.Block([result], Expression.Assign(result, Expression.Call(method, card, die)), tuple);
        return Expression.Lambda<Func<CardModel, LibraryDice, (decimal Minimum, decimal Maximum)>>(
            body, card, die).Compile();
    }
}
