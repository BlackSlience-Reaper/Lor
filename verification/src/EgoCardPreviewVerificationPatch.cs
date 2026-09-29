using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using Environment = System.Environment;

using LibraryOfRuina;
using LibraryOfRuina.core;

namespace LibraryOfRuinaVerification;

/// <summary>
/// Records what every E.G.O. card exposes in the compendium and in enemy previews: canonical and mutable
/// models, before and after the upgrade preview, after each <c>SetPreviewDamage</c> overload in the orders the
/// monsters use, after <c>SetEnemyAttackPreview</c>, and after cloning or downgrading a configured preview.
/// One <c>REC</c> line per facet, so two builds can be diffed line by line. Everything the base class may
/// declare differently between builds (preview methods, the preview-damage field) is reached by reflection,
/// so the same suite compiles and runs against either build.
/// </summary>
internal static class EgoCardPreviewVerificationPatch
{
    private const string VerifyArg = "lor-verify-ego-card-preview";
    private const string LogPrefix = "[LibraryOfRuina.EgoCardPreview.Verify] ";
    private const string PreviewInterfaceName = "LibraryOfRuina.cards.IEnemyAttackPreviewCard";
    private const string PreviewDamageFieldPrefix = "_previewDamage";

    private static readonly int[] PreviewArguments = [31, 32, 33];
    private static readonly int[] EnemyDamages = [41, 42, 43, 44, 45];

    private static bool _started;
    private static int _records;

    internal static void Start()
    {
        if (_started || !HasVerifyArg())
        {
            return;
        }

        _started = true;
        Callable.From(Run).CallDeferred();
    }

    private static bool HasVerifyArg() =>
        CommandLineHelper.HasArg(VerifyArg)
        || Environment.GetCommandLineArgs().Any(argument => string.Equals(
            argument.TrimStart('-'),
            VerifyArg,
            StringComparison.OrdinalIgnoreCase));

    private static void Run()
    {
        try
        {
            Assembly modAssembly = typeof(LibraryOfRuinaInitializer).Assembly;
            Type previewInterface = modAssembly.GetType(PreviewInterfaceName, throwOnError: true)!;
            MethodInfo setEnemyAttackPreview = previewInterface.GetMethod("SetEnemyAttackPreview")!;

            CardModel[] cards = ModelDb.AllCards
                .Where(card => card.GetType().Assembly == modAssembly)
                .Where(card => card.GetType().Name.Contains("EgoCard", StringComparison.Ordinal)
                               || previewInterface.IsInstanceOfType(card))
                .OrderBy(card => card.GetType().FullName, StringComparer.Ordinal)
                .ToArray();

            var failures = new List<string>();
            foreach (CardModel canonical in cards)
            {
                RecordCard(canonical, previewInterface, setEnemyAttackPreview, failures);
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    "E.G.O. preview calls the monsters make threw for "
                    + failures.Count
                    + " case(s):\n"
                    + string.Join("\n", failures));
            }

            Log.Info(LogPrefix + "EGO_CARD_PREVIEW_OK cards=" + cards.Length + " records=" + _records);
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception exception)
        {
            Log.Error(LogPrefix + "EGO_CARD_PREVIEW_FAILED: " + exception);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void RecordCard(
        CardModel canonical,
        Type previewInterface,
        MethodInfo setEnemyAttackPreview,
        List<string> failures)
    {
        Type type = canonical.GetType();
        string id = type.Name;
        bool isPreviewCard = previewInterface.IsInstanceOfType(canonical);

        Record(id, "canon.meta", () => DescribeMeta(canonical, isPreviewCard));
        Record(id, "canon.vars", () => DescribeVars(canonical));
        Record(id, "canon.desc", () => canonical.GetDescriptionForPile(PileType.None));
        Record(id, "canon.upgradeDesc", () => canonical.GetDescriptionForUpgradePreview());
        Record(id, "canon.stored", () => DescribeStored(canonical));

        MethodInfo? upgradePreview = type.GetMethod("UpgradePreview", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        MethodInfo[] setPreviewDamage = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(static method => method.Name == "SetPreviewDamage")
            .Where(static method => method.GetParameters().All(static p => p.ParameterType == typeof(int)))
            .OrderBy(static method => method.GetParameters().Length)
            .ToArray();
        Record(id, "api", () =>
            "UpgradePreview=" + (upgradePreview != null)
            + " SetPreviewDamage=[" + string.Join(",", setPreviewDamage.Select(static m => m.GetParameters().Length)) + "]"
            + " SetEnemyAttackPreview=" + isPreviewCard);

        CardModel fresh = canonical.ToMutable();
        RecordState(id, "mut", fresh);

        CardModel upgraded = canonical.ToMutable();
        Invoke(id, "up", failures, () => Upgrade(upgraded, upgradePreview));
        RecordState(id, "up", upgraded);

        CardModel upgradeShown = canonical.ToMutable();
        Record(id, "upShown.desc", () =>
        {
            upgradeShown.UpgradeInternal();
            return upgradeShown.GetDescriptionForUpgradePreview();
        });
        Record(id, "upShown.vars", () => DescribeVars(upgradeShown));

        foreach (MethodInfo overload in setPreviewDamage)
        {
            int arity = overload.GetParameters().Length;
            object[] arguments = PreviewArguments.Take(arity).Cast<object>().ToArray();
            string tag = "set" + arity;

            CardModel set = canonical.ToMutable();
            Invoke(id, tag, failures, () => overload.Invoke(set, arguments));
            RecordState(id, tag, set);

            CardModel upThenSet = canonical.ToMutable();
            Invoke(id, "up+" + tag, failures, () =>
            {
                Upgrade(upThenSet, upgradePreview);
                overload.Invoke(upThenSet, arguments);
            });
            RecordState(id, "up+" + tag, upThenSet);

            CardModel setThenUp = canonical.ToMutable();
            Invoke(id, tag + "+up", failures, () =>
            {
                overload.Invoke(setThenUp, arguments);
                Upgrade(setThenUp, upgradePreview);
            });
            RecordState(id, tag + "+up", setThenUp);

            if (isPreviewCard)
            {
                for (int count = 1; count <= EnemyDamages.Length; count++)
                {
                    int damageCount = count;
                    CardModel full = canonical.ToMutable();
                    string fullTag = "up+" + tag + "+enemy" + damageCount;
                    Invoke(id, fullTag, failures, () =>
                    {
                        Upgrade(full, upgradePreview);
                        overload.Invoke(full, arguments);
                        setEnemyAttackPreview.Invoke(full, [EnemyDamages.Take(damageCount).ToArray(), 3]);
                    });
                    RecordState(id, fullTag, full);
                }
            }

            CardModel clone = (CardModel)upThenSet.ClonePreservingMutability();
            RecordState(id, "up+" + tag + ".clone", clone);

            Record(id, "up+" + tag + ".downgrade", () =>
            {
                upThenSet.DowngradeInternal();
                return "level=" + upThenSet.CurrentUpgradeLevel
                       + " cost=" + DescribeCost(upThenSet)
                       + " vars=" + DescribeVars(upThenSet)
                       + " stored=" + DescribeStored(upThenSet);
            });
        }

        if (isPreviewCard)
        {
            foreach (int hits in new[] { 0, 2 })
            {
                for (int count = 0; count <= EnemyDamages.Length; count++)
                {
                    int damageCount = count;
                    CardModel enemy = canonical.ToMutable();
                    string tag = "enemy" + damageCount + "x" + hits;
                    Invoke(id, tag, failures, () =>
                        setEnemyAttackPreview.Invoke(enemy, [EnemyDamages.Take(damageCount).ToArray(), hits]));
                    RecordState(id, tag, enemy);
                }
            }
        }
    }

    private static void Upgrade(CardModel card, MethodInfo? upgradePreview)
    {
        if (upgradePreview != null)
        {
            upgradePreview.Invoke(card, null);
            return;
        }

        card.UpgradeInternal();
        card.FinalizeUpgradeInternal();
    }

    private static void RecordState(string id, string scenario, CardModel card)
    {
        Record(id, scenario + ".state", () =>
            "level=" + card.CurrentUpgradeLevel
            + " title=" + card.Title
            + " cost=" + DescribeCost(card)
            + " keywords=" + string.Join(",", card.Keywords.OrderBy(static k => k.ToString(), StringComparer.Ordinal)));
        Record(id, scenario + ".vars", () => DescribeVars(card));
        Record(id, scenario + ".desc", () => card.GetDescriptionForPile(PileType.None));
        Record(id, scenario + ".stored", () => DescribeStored(card));
    }

    private static string DescribeMeta(CardModel card, bool isPreviewCard)
    {
        Type type = card.GetType();
        MethodInfo? staticPortrait = type.GetMethod(
            "GetPortraitResourcePath",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly,
            Type.EmptyTypes);
        return "type=" + card.Type
               + " rarity=" + card.Rarity
               + " target=" + card.TargetType
               + " cost=" + DescribeCost(card)
               + " maxUpgrade=" + card.MaxUpgradeLevel
               + " library=" + card.ShouldShowInCardLibrary
               + " pool=" + Safe(() => card.Pool.Id.ToString())
               + " visualPool=" + Safe(() => card.VisualCardPool.Id.ToString())
               + " keywords=" + string.Join(",", card.CanonicalKeywords.Select(static k => k.ToString()))
               + " portrait=" + Safe(() => card.PortraitPath)
               + " beta=" + Safe(() => card.BetaPortraitPath)
               + " all=[" + Safe(() => string.Join(",", card.AllPortraitPaths)) + "]"
               + " staticPortrait=" + (staticPortrait == null ? "-" : Safe(() => (string)staticPortrait.Invoke(null, null)!))
               + " title=" + Safe(() => card.Title)
               + " hoverTips=[" + Safe(() => string.Join(",", card.HoverTips.Select(static tip => tip.GetType().Name))) + "]"
               + " enemyPreview=" + isPreviewCard;
    }

    private static string DescribeCost(CardModel card) =>
        Safe(() => card.EnergyCost.Canonical + "/" + card.EnergyCost.GetResolved()
                   + (card.EnergyCost.WasJustUpgraded ? "*" : ""));

    private static string DescribeVars(CardModel card)
    {
        var builder = new StringBuilder();
        foreach (DynamicVar variable in card.DynamicVars.Values)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(variable.Name)
                .Append(':')
                .Append(ShortTypeName(variable.GetType()))
                .Append('=')
                .Append(variable.BaseValue.ToString(CultureInfo.InvariantCulture))
                .Append('/')
                .Append(variable.PreviewValue.ToString(CultureInfo.InvariantCulture))
                .Append('/')
                .Append(variable.EnchantedValue.ToString(CultureInfo.InvariantCulture));
            if (variable.WasJustUpgraded)
            {
                builder.Append('*');
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// The preview damage a card keeps outside its DynamicVars. Before the base class took it over, each card
    /// declared its own <c>_previewDamage</c> (or <c>_previewDamageA/B/C</c>); A is the same slot as the base
    /// class field, so it is reported under the base name.
    /// </summary>
    private static string DescribeStored(CardModel card)
    {
        var values = new SortedDictionary<string, int>(StringComparer.Ordinal);
        for (Type? type = card.GetType(); type != null && type != typeof(CardModel); type = type.BaseType)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (field.FieldType != typeof(int) || !field.Name.StartsWith(PreviewDamageFieldPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                string name = field.Name == PreviewDamageFieldPrefix + "A" ? PreviewDamageFieldPrefix : field.Name;
                values[name] = (int)field.GetValue(card)!;
            }
        }

        return values.Count == 0
            ? "-"
            : string.Join(" ", values.Select(static pair => pair.Key + "=" + pair.Value));
    }

    private static string ShortTypeName(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        string name = type.Name[..type.Name.IndexOf('`')];
        return name + "<" + string.Join(",", type.GetGenericArguments().Select(ShortTypeName)) + ">";
    }

    private static void Invoke(string id, string scenario, List<string> failures, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            Exception inner = exception is TargetInvocationException { InnerException: { } cause } ? cause : exception;
            failures.Add(id + " " + scenario + ": " + inner.GetType().Name + ": " + inner.Message);
            Emit(id, scenario + ".invoke", "EXC:" + inner.GetType().Name);
        }
    }

    private static void Record(string id, string facet, Func<string> read) => Emit(id, facet, Safe(read));

    private static string Safe(Func<string> read)
    {
        try
        {
            return read();
        }
        catch (Exception exception)
        {
            Exception inner = exception is TargetInvocationException { InnerException: { } cause } ? cause : exception;
            return "EXC:" + inner.GetType().Name;
        }
    }

    private static void Emit(string id, string facet, string value)
    {
        _records++;
        Log.Info(LogPrefix + "REC " + id + " " + facet + " " + value.Replace("\r", "\\r").Replace("\n", "\\n"));
    }
}
