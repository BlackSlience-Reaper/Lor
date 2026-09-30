using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using LibraryOfRuina;
using LibraryOfRuina.content.abnormalities.JudgementBird;
using LibraryOfRuina.content.abnormalities.QueenOfHatred;
using LibraryOfRuina.content.specialguests;
using LibraryOfRuina.core;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.features.ftue;
using LibraryOfRuina.features.intentgraph;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Saves.Runs;
using Environment = System.Environment;


namespace LibraryOfRuinaVerification;

internal static class CodeHealthVerificationPatch
{
    private const string VerifyArg = "lor-verify-code-health";
    private const string LogPrefix = "[LibraryOfRuina.CodeHealth.Verify] ";
    private static bool _started;

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
            VerifyMutableCollectionIsolation();
            VerifySpecialGuestEventIsolation();
            VerifyCompatibilityFingerprint();
            VerifyOptionalPatchTargets();
            VerifyResourceContracts();
            VerifyPlaybackFallbacks();
            Log.Info(LogPrefix + "CODE_HEALTH_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception exception)
        {
            Log.Error(LogPrefix + "CODE_HEALTH_FAILED: " + exception);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void VerifyPlaybackFallbacks()
    {
        if (QueenOfHatredInversionVideoController.ResolveFallbackDelay(0) <= 0
            || JudgementBirdJudgementVideoController.ResolveFallbackDelay(0) <= 0)
        {
            throw new InvalidOperationException(
                "A blocking video controller has no finite unknown-length fallback.");
        }
    }

    private static void VerifyCompatibilityFingerprint()
    {
        string schema = SavedPropertiesTypeCacheCompat
            .BuildSchemaFingerprintMaterial();
        foreach (Type enumType in SavedPropertiesTypeCacheCompat
                     .GetAllModSavedPropertyTypes()
                     .SelectMany(type => type.GetProperties(
                         BindingFlags.Instance
                         | BindingFlags.Public
                         | BindingFlags.NonPublic))
                     .Where(property =>
                         property.GetCustomAttribute<SavedPropertyAttribute>()
                         != null)
                     .Select(property =>
                     {
                         Type propertyType = Nullable.GetUnderlyingType(
                                 property.PropertyType)
                             ?? property.PropertyType;
                         return propertyType.IsArray
                             ? propertyType.GetElementType()
                             : propertyType;
                     })
                     .OfType<Type>()
                     .Where(static type => type.IsEnum)
                     .Distinct())
        {
            if (!schema.Contains(
                    "enum|" + enumType.FullName + "|",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "SavedProperty enum is absent from schema fingerprint: "
                    + enumType.FullName);
            }
        }
    }

    private static void VerifyOptionalPatchTargets()
    {
        if (!LibraryOfRuinaRewardsFtuePatch.Prepare()
            || LibraryOfRuinaRewardsFtuePatch.TargetMethod().DeclaringType
            != typeof(NRewardsScreen))
        {
            throw new InvalidOperationException(
                "Rewards FTUE resolved an invalid Harmony target.");
        }

        if (!LibraryOfRuinaEncounterTrackerPatch.Prepare()
            || LibraryOfRuinaEncounterTrackerPatch.TargetMethod().DeclaringType
            != typeof(CombatManager))
        {
            throw new InvalidOperationException(
                "Encounter FTUE resolved an invalid Harmony target.");
        }
    }

    private static void VerifyResourceContracts()
    {
        foreach (string key in new[]
                 {
                     "POOL_EGO_TIP",
                     "POOL_CHARACTER_EGO_TIP"
                 })
        {
            string text = new LocString("card_library", key)
                .GetFormattedText();
            if (string.IsNullOrWhiteSpace(text)
                || text.Contains(key, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Card-library hover localization is missing: " + key);
            }
        }

        RequireSecondaryInitialState(
            "LibraryOfRuina.content.abnormalities.Nosferatu.BloodBat",
            "THIRST");
        RequireSecondaryInitialState(
            "LibraryOfRuina.content.abnormalities.Nosferatu.Nosferatu",
            "GRACEFUL_REST");
    }

    private static void RequireSecondaryInitialState(
        string monsterTypeName,
        string stateId)
    {
        IntentGraphMonsterConfig? config = IntentGraphConfigRepository
            .GetConfigForMonster(monsterTypeName);
        if (config?.SecondaryInitialStates.Contains(
                stateId,
                StringComparer.Ordinal) != true)
        {
            throw new InvalidOperationException(
                "Intent graph is missing secondary initial state "
                + monsterTypeName
                + ":"
                + stateId);
        }
    }

    private static void VerifyMutableCollectionIsolation()
    {
        Assembly modAssembly = typeof(LibraryOfRuinaInitializer).Assembly;
        var failures = new List<string>();

        foreach (Type type in ModelDb.AllAbstractModelSubtypes
                     .Where(candidate =>
                         candidate.Assembly == modAssembly
                         && !candidate.IsAbstract)
                     .OrderBy(candidate => candidate.FullName, StringComparer.Ordinal))
        {
            AbstractModel? canonical = ModelDb.GetByIdOrNull<AbstractModel>(
                ModelDb.GetId(type));
            if (canonical == null)
            {
                continue;
            }

            AbstractModel first = canonical.MutableClone();
            AbstractModel second = canonical.MutableClone();
            foreach (FieldInfo field in EnumerateModInstanceFields(type, modAssembly))
            {
                if (!IsMutableCollectionType(field.FieldType))
                {
                    continue;
                }

                object? firstValue = field.GetValue(first);
                object? secondValue = field.GetValue(second);
                if (firstValue == null
                    || secondValue == null
                    || IsSharedEmptyArray(firstValue, secondValue)
                    || !ReferenceEquals(firstValue, secondValue))
                {
                    continue;
                }

                failures.Add((type.FullName ?? type.Name) + "." + field.Name);
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                "Mutable model clones share collection fields: "
                + string.Join(", ", failures));
        }
    }

    private static void VerifySpecialGuestEventIsolation()
    {
        Type? concreteType = ModelDb.AllAbstractModelSubtypes.FirstOrDefault(
            static candidate =>
                !candidate.IsAbstract
                && typeof(SpecialGuestMonsterBase).IsAssignableFrom(candidate));
        if (concreteType == null)
        {
            throw new InvalidOperationException(
                "No concrete SpecialGuestMonsterBase model was registered.");
        }

        AbstractModel canonical = ModelDb.GetById<AbstractModel>(
            ModelDb.GetId(concreteType));
        var source = (SpecialGuestMonsterBase)canonical.MutableClone();
        source.EmotionChanged += static () => { };
        var clone = (SpecialGuestMonsterBase)source.MutableClone();
        FieldInfo eventField = typeof(SpecialGuestMonsterBase).GetField(
                nameof(SpecialGuestMonsterBase.EmotionChanged),
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(
                nameof(SpecialGuestMonsterBase),
                nameof(SpecialGuestMonsterBase.EmotionChanged));
        if (eventField.GetValue(clone) != null)
        {
            throw new InvalidOperationException(
                "SpecialGuestMonsterBase clone retained EmotionChanged subscribers.");
        }
    }

    private static IEnumerable<FieldInfo> EnumerateModInstanceFields(
        Type type,
        Assembly modAssembly)
    {
        for (Type? current = type;
             current != null && current.Assembly == modAssembly;
             current = current.BaseType)
        {
            foreach (FieldInfo field in current.GetFields(
                         BindingFlags.Instance
                         | BindingFlags.Public
                         | BindingFlags.NonPublic
                         | BindingFlags.DeclaredOnly))
            {
                if (!field.IsStatic)
                {
                    yield return field;
                }
            }
        }
    }

    private static bool IsMutableCollectionType(Type type)
    {
        if (type.IsArray)
        {
            return true;
        }

        if (typeof(IDictionary).IsAssignableFrom(type)
            || typeof(IList).IsAssignableFrom(type))
        {
            return true;
        }

        if (!type.IsGenericType)
        {
            return false;
        }

        Type definition = type.GetGenericTypeDefinition();
        return definition == typeof(HashSet<>)
            || definition == typeof(Queue<>)
            || definition == typeof(Stack<>)
            || definition == typeof(SortedDictionary<,>);
    }

    private static bool IsSharedEmptyArray(object first, object second) =>
        ReferenceEquals(first, second)
        && first is Array { Length: 0 };
}
