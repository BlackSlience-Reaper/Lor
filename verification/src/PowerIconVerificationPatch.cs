using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Powers.LibraryPowerMode;
using LibraryOfRuina;
using LibraryOfRuina.content.abnormalities.AddictedEmployee;
using LibraryOfRuina.core;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using Environment = System.Environment;


namespace LibraryOfRuinaVerification;

internal static class PowerIconVerificationPatch
{
    private const string VerifyArg = "lor-verify-power-icons";
    private const string LogPrefix = "[LibraryOfRuina.PowerIcons.Verify] ";

    private static bool _started;

    internal static void Start()
    {
        if (_started || !HasVerifyArg())
        {
            return;
        }

        _started = true;
        Callable.From(() => TaskHelper.RunSafely(RunAsync())).CallDeferred();
    }

    private static bool HasVerifyArg() =>
        CommandLineHelper.HasArg(VerifyArg)
        || Environment.GetCommandLineArgs().Any(arg =>
            string.Equals(
                arg.TrimStart('-'),
                VerifyArg,
                StringComparison.OrdinalIgnoreCase));

    private static async Task RunAsync()
    {
        try
        {
            MethodInfo powerBadgeFactory = ResolvePowerBadgeFactory();
            MethodInfo monsterIntentsGetter = ResolveMonsterIntentsGetter();
            Type libraryPowerType = typeof(LibraryPowerModel);
            Assembly modAssembly = typeof(LibraryOfRuinaInitializer).Assembly;
            Assembly libraryAssembly = libraryPowerType.Assembly;
            PowerModel[] powers = ModelDb.AllPowers
                .Where(power =>
                    power.GetType().Assembly == modAssembly
                    || power.GetType().Assembly == libraryAssembly)
                .Where(power => power.IsVisible)
                .OrderBy(power => power.Id.Entry, StringComparer.Ordinal)
                .ToArray();

            var failures = new List<string>();
            int dynamicVariantCount = 0;
            foreach (PowerModel power in powers)
            {
                VerifyPower(power, power.Id.ToString(), powerBadgeFactory, verifyIntentBadge: true, failures);
                dynamicVariantCount += VerifyDynamicVariants(power, powerBadgeFactory, failures);
            }

            int monsterCount = VerifyMonsterIntentAssets(
                modAssembly,
                monsterIntentsGetter,
                failures,
                out int intentPowerAssetCount,
                out int skippedMonsterCount);

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    "Power icon verification failed for "
                    + failures.Count
                    + " check(s):\n"
                    + string.Join("\n", failures));
            }

            IntentBadge bindingBadge = await VerifyAddictedEmployeeRuntimePreload();
            Log.Info(
                LogPrefix
                + "POWER_ICONS_OK count="
                + powers.Length
                + " dynamicVariants="
                + dynamicVariantCount
                + " monsters="
                + monsterCount
                + " intentPowerAssets="
                + intentPowerAssetCount
                + " skippedMonsters="
                + skippedMonsterCount
                + " bindingBadgePath="
                + bindingBadge.IconPath);
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "POWER_ICONS_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task<IntentBadge> VerifyAddictedEmployeeRuntimePreload()
    {
        AddictedEmployee monster = ModelDb.Monster<AddictedEmployee>();
        AssetLoadingSession session = PreloadManager.Cache.CreateSession(
            "PowerIconVerifier.AddictedEmployee",
            monster.AssetPaths);
        await NAssetLoader.Instance.LoadInTheBackground(session);

        IntentBadge bindingBadge = IntentBadge.FromPower<LibraryBindingPower>(3, "1", "3");
        if (!PreloadManager.Cache.ContainsKey(bindingBadge.IconPath))
        {
            throw new InvalidOperationException(
                "AddictedEmployee runtime preload omitted Binding icon " + bindingBadge.IconPath);
        }

        Texture2D? texture = bindingBadge.GetTexture();
        if (!GodotTextureSafety.IsValid(texture) || IsMissingPowerTexture(texture))
        {
            throw new InvalidOperationException(
                "AddictedEmployee Binding badge returned a missing/invalid cached texture");
        }

        return bindingBadge;
    }

    private static string? VerifyPower(
        PowerModel power,
        string label,
        MethodInfo powerBadgeFactory,
        bool verifyIntentBadge,
        ICollection<string> failures)
    {
        string? resolvedPath = null;
        if (!PowerIconResolver.TryResolve(power, out ResolvedPowerIcon resolved)
            || !GodotTextureSafety.IsValid(resolved.Texture)
            || IsMissingPowerTexture(resolved.Texture))
        {
            failures.Add(label + ": no valid icon fallback");
        }
        else
        {
            resolvedPath = resolved.Path;
        }

        VerifyLibraryPathIsolation(power, label, failures);
        VerifyGetter(power, label, "Icon", static model => model.Icon, failures);
        VerifyGetter(power, label, "BigIcon", static model => model.BigIcon, failures);
        if (verifyIntentBadge)
        {
            VerifyIntentBadge(power, label, powerBadgeFactory, failures);
        }

        return resolvedPath;
    }

    private static void VerifyLibraryPathIsolation(
        PowerModel power,
        string label,
        ICollection<string> failures)
    {
        if (power is not LibraryPowerModel libraryPower)
        {
            return;
        }

        const string libraryRoot = "res://LibraryOfRuinaLib/";
        bool libraryOwned = power.GetType().Assembly == typeof(LibraryPowerModel).Assembly;
        if (!libraryOwned
            && libraryPower.ResolvedBigIconPath.StartsWith(libraryRoot, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(label + ": downstream power was routed into LibraryOfRuinaLib assets");
        }

        if (!libraryPower.IsDynamic && !libraryOwned)
        {
            return;
        }

        if (!string.Equals(power.PackedIconPath, libraryPower.PackedIconPath, StringComparison.Ordinal))
        {
            failures.Add(label + ": PowerModel.PackedIconPath bypassed LibraryPowerModel path");
        }

        if (!string.Equals(power.ResolvedBigIconPath, libraryPower.ResolvedBigIconPath, StringComparison.Ordinal))
        {
            failures.Add(label + ": PowerModel.ResolvedBigIconPath bypassed LibraryPowerModel path");
        }
    }

    private static int VerifyDynamicVariants(
        PowerModel canonicalPower,
        MethodInfo powerBadgeFactory,
        ICollection<string> failures)
    {
        if (canonicalPower is not LibraryMultipleModePowerModel canonicalMultipleMode)
        {
            return 0;
        }

        try
        {
            var mutablePower = (LibraryMultipleModePowerModel)canonicalMultipleMode.ToMutable();
            LibraryPowerMode defaultMode = mutablePower.Mode;
            Type familyType = ResolveModeFamily(defaultMode.GetType());
            Type[] modeTypes = defaultMode.GetType().Assembly.GetTypes()
                .Where(type =>
                    !type.IsAbstract
                    && familyType.IsAssignableFrom(type)
                    && type.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();

            int verified = 0;
            foreach (Type modeType in modeTypes)
            {
                if (Activator.CreateInstance(modeType) is not LibraryPowerMode mode)
                {
                    failures.Add(canonicalPower.Id + ": could not create mode " + modeType.FullName);
                    continue;
                }

                mode.SourcePower = mutablePower;
                mutablePower.Mode = mode;
                string label = canonicalPower.Id + " mode=" + mode.Name;
                string? path = VerifyPower(
                    mutablePower,
                    label,
                    powerBadgeFactory,
                    verifyIntentBadge: false,
                    failures);
                string expectedSuffix = "_" + mode.Name.ToLowerInvariant() + ".png";
                if (path != null && !path.EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(label + ": resolved non-mode icon " + path);
                }

                verified++;
            }

            return verified;
        }
        catch (Exception ex)
        {
            failures.Add(
                canonicalPower.Id
                + ": dynamic mode verification threw "
                + ex.GetType().Name
                + ": "
                + ex.Message);
            return 0;
        }
    }

    private static Type ResolveModeFamily(Type modeType)
    {
        Type familyType = modeType;
        while (familyType.BaseType != null
               && familyType.BaseType != typeof(LibraryPowerMode))
        {
            familyType = familyType.BaseType;
        }

        return familyType;
    }

    private static void VerifyIntentBadge(
        PowerModel power,
        string label,
        MethodInfo powerBadgeFactory,
        ICollection<string> failures)
    {
        try
        {
            IntentBadge? badge = powerBadgeFactory
                .MakeGenericMethod(power.GetType())
                .Invoke(null, [1, null, null]) as IntentBadge;
            if (badge == null)
            {
                failures.Add(label + ": IntentBadge.FromPower returned null");
                return;
            }

            if (badge.IconPath.Contains("missing_power", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(label + ": IntentBadge resolved missing icon path " + badge.IconPath);
                return;
            }

            VerifyTexturePath(label + ": IntentBadge", badge.IconPath, failures);
        }
        catch (TargetInvocationException ex)
        {
            Exception actual = ex.InnerException ?? ex;
            failures.Add(
                label
                + ": IntentBadge.FromPower threw "
                + actual.GetType().Name
                + ": "
                + actual.Message);
        }
        catch (Exception ex)
        {
            failures.Add(
                label
                + ": IntentBadge verification threw "
                + ex.GetType().Name
                + ": "
                + ex.Message);
        }
    }

    private static int VerifyMonsterIntentAssets(
        Assembly modAssembly,
        MethodInfo monsterIntentsGetter,
        ICollection<string> failures,
        out int intentPowerAssetCount,
        out int skippedMonsterCount)
    {
        intentPowerAssetCount = 0;
        skippedMonsterCount = 0;
        Type[] monsterTypes = ModelDb.AllAbstractModelSubtypes
            .Where(type =>
                type.Assembly == modAssembly
                && !type.IsAbstract
                && typeof(MonsterModel).IsAssignableFrom(type)
                && ModelDb.Contains(type))
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        foreach (Type monsterType in monsterTypes)
        {
            string label = "MONSTER." + ModelDb.GetId(monsterType).Entry;
            try
            {
                MonsterModel monster = ModelDb.GetById<MonsterModel>(ModelDb.GetId(monsterType));
                var intents = (IEnumerable<AbstractIntent>?)monsterIntentsGetter.Invoke(monster, null)
                    ?? Array.Empty<AbstractIntent>();
                string[] powerIntentAssets = intents
                    .SelectMany(intent => intent.AssetPaths)
                    .Where(IsPowerIconAsset)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (powerIntentAssets.Length == 0)
                {
                    continue;
                }

                HashSet<string> monsterAssets = monster.AssetPaths.ToHashSet(StringComparer.Ordinal);
                foreach (string intentAsset in powerIntentAssets)
                {
                    intentPowerAssetCount++;
                    if (!monsterAssets.Contains(intentAsset))
                    {
                        failures.Add(label + ": AssetPaths missing actual intent asset " + intentAsset);
                        continue;
                    }

                    VerifyTexturePath(label + ": intent asset", intentAsset, failures);
                }
            }
            catch (TargetInvocationException ex)
            {
                Exception actual = ex.InnerException ?? ex;
                skippedMonsterCount++;
                failures.Add(
                    label
                    + ": intent asset audit threw "
                    + actual.GetType().Name
                    + ": "
                    + actual.Message);
            }
            catch (Exception ex)
            {
                skippedMonsterCount++;
                failures.Add(
                    label
                    + ": intent asset audit threw "
                    + ex.GetType().Name
                    + ": "
                    + ex.Message);
            }
        }

        return monsterTypes.Length;
    }

    private static bool IsPowerIconAsset(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && (path.Contains("/powers/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/power_atlas.sprites/", StringComparison.OrdinalIgnoreCase));

    private static MethodInfo ResolvePowerBadgeFactory()
    {
        foreach (MethodInfo method in typeof(IntentBadge).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (method.Name == nameof(IntentBadge.FromPower)
                && method.IsGenericMethodDefinition
                && parameters.Length == 3
                && parameters[0].ParameterType == typeof(int))
            {
                return method;
            }
        }

        throw new MissingMethodException(typeof(IntentBadge).FullName, nameof(IntentBadge.FromPower));
    }

    private static MethodInfo ResolveMonsterIntentsGetter()
    {
        return typeof(MonsterModel).GetMethod(
                   "GetIntents",
                   BindingFlags.Instance | BindingFlags.NonPublic)
               ?? throw new MissingMethodException(typeof(MonsterModel).FullName, "GetIntents");
    }

    private static void VerifyGetter(
        PowerModel power,
        string label,
        string getterName,
        Func<PowerModel, Texture2D?> getter,
        ICollection<string> failures)
    {
        try
        {
            Texture2D? texture = getter(power);
            if (!GodotTextureSafety.IsValid(texture) || IsMissingPowerTexture(texture))
            {
                failures.Add(label + ": " + getterName + " returned missing/invalid texture");
            }
        }
        catch (Exception ex)
        {
            failures.Add(
                label
                + ": "
                + getterName
                + " threw "
                + ex.GetType().Name
                + ": "
                + ex.Message);
        }
    }

    private static void VerifyTexturePath(
        string label,
        string path,
        ICollection<string> failures)
    {
        if (!ResourceLoader.Exists(path) && !FileAccess.FileExists(path))
        {
            failures.Add(label + " does not exist " + path);
            return;
        }

        try
        {
            Texture2D? texture = ResourceLoader.Load<Texture2D>(
                path);
            if (!GodotTextureSafety.IsValid(texture) || IsMissingPowerTexture(texture))
            {
                failures.Add(label + " returned missing/invalid texture at " + path);
            }
        }
        catch (Exception ex)
        {
            failures.Add(
                label
                + " threw "
                + ex.GetType().Name
                + " at "
                + path
                + ": "
                + ex.Message);
        }
    }

    private static bool IsMissingPowerTexture(Texture2D? texture)
    {
        if (texture == null)
        {
            return true;
        }

        if (texture.ResourcePath.Contains("missing_power", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return texture is AtlasTexture atlas
               && atlas.Atlas?.ResourcePath.Contains(
                   "missing_power",
                   StringComparison.OrdinalIgnoreCase) == true;
    }
}
