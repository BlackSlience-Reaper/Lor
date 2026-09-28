using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.compat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rewards;
using LibraryOfRuina.helpers;

namespace LibraryOfRuina.relics;

internal static class AbnormalityPageRewardPreselection
{
    private static readonly Lazy<IReadOnlyDictionary<ModelId, PageRelicRegistration>> PageRelicsById =
        new(DiscoverPageRelics);

    private static readonly ConcurrentDictionary<Type, bool> PageChoiceCardTypeCache =
        new();

    [ThreadStatic]
    private static bool _isPreselectingPageReward;

    public static bool IsPreselectingPageReward => _isPreselectingPageReward;

    public static bool IsPageRelic(RelicModel? relic)
    {
        return relic != null && TryGetRegistration(relic, out _);
    }

    public static bool IsPageRelicChoiceCard(CardModel? card)
    {
        if (card == null)
        {
            return false;
        }

        return PageChoiceCardTypeCache.GetOrAdd(
            card.GetType(),
            _ => IsRegisteredPageChoiceCard(card));
    }

    public static async Task<bool> TryPreselectPageChoice(RelicModel relic, Player player)
    {
        if (!TryGetRegistration(relic, out PageRelicRegistration registration)
            || HasConcreteMode(relic, registration))
        {
            return true;
        }

        _isPreselectingPageReward = true;
        try
        {
            relic.Owner = player;
            IReadOnlyList<CardModel> options = CreateChoiceCards(relic, registration);
            CardModel? chosenCard;
            try
            {
                chosenCard = await ChoosePageCard(options, player);
            }
            catch (Exception exception) when (ShouldKeepRewardAvailableOnOverlayLifecycleException(exception))
            {
                Log.Warn("[LibraryOfRuina.PageRelic] "
                    + relic.GetType().Name
                    + " page choice overlay failed before relic obtain; reward remains available. exception="
                    + exception.GetType().Name
                    + ": "
                    + exception.Message);
                return false;
            }

            if (chosenCard == null)
            {
                Log.Info("[LibraryOfRuina.PageRelic] "
                    + relic.GetType().Name
                    + " page choice skipped before relic obtain; reward remains available.");
                return false;
            }

            ApplyChosenMode(relic, chosenCard, registration);
            return true;
        }
        finally
        {
            _isPreselectingPageReward = false;
        }
    }

    public static bool HasConcreteModeForPatch(RelicModel relic) =>
        TryGetRegistration(relic, out PageRelicRegistration registration)
        && HasConcreteMode(relic, registration);

    public static async Task ApplyPostObtainedEffects(RelicModel relic)
    {
        if (!TryGetRegistration(relic, out PageRelicRegistration registration)
            || registration.PostObtainedEffects.Count == 0
            || !HasConcreteMode(relic, registration))
        {
            return;
        }

        object mode = GetMode(relic, registration);
        foreach (PageRelicPostObtainedEffect effect in registration.PostObtainedEffects)
        {
            if (effect.AppliesTo(mode))
            {
                await InvokePostObtainedEffect(relic, effect, mode);
            }
        }
    }

    private static Task<CardModel?> ChoosePageCard(
        IReadOnlyList<CardModel> options,
        Player player)
    {
        if (!LocalContext.IsMe(player) || NGame.IsMainThread())
        {
            return ChoosePageCardCore(options, player);
        }

        TaskCompletionSource<CardModel?> completionSource = new();
        Callable.From(async () =>
        {
            try
            {
                completionSource.SetResult(await ChoosePageCardCore(options, player));
            }
            catch (Exception ex)
            {
                completionSource.SetException(ex);
            }
        }).CallDeferred();
        return completionSource.Task;
    }

    private static Task<CardModel?> ChoosePageCardCore(
        IReadOnlyList<CardModel> options,
        Player player) =>
        CardSelectCmd.FromChooseACardScreen(
            new BlockingPlayerChoiceContext(),
            options,
            player,
            canSkip: true);

    private static void ApplyChosenMode(
        RelicModel relic,
        CardModel chosenCard,
        PageRelicRegistration registration)
    {
        object? mode = registration.ResolveModeFromChoiceCard.Invoke(null, [chosenCard]);
        if (mode == null)
        {
            throw AbnormalityPageRewardHelper.UnexpectedPageChoiceCard(chosenCard);
        }

        MethodInfo? setModeMethod = AccessTools.Method(relic.GetType(), "SetMode", [mode.GetType()]);
        if (setModeMethod != null && setModeMethod.ReturnType == typeof(void))
        {
            setModeMethod.Invoke(relic, [mode]);
            return;
        }

        MethodInfo setter = registration.ModeProperty.GetSetMethod(nonPublic: true)
            ?? throw new MissingMethodException(relic.GetType().FullName, "set_Mode");
        setter.Invoke(relic, [mode]);
        AccessTools.Method(typeof(RelicModel), "RelicIconChanged")?.Invoke(relic, []);
        AccessTools.Method(relic.GetType(), "UpdateModeUiState")?.Invoke(relic, []);
    }

    private static IReadOnlyList<CardModel> CreateChoiceCards(
        RelicModel relic,
        PageRelicRegistration registration)
    {
        return (IReadOnlyList<CardModel>)registration.CreateModeChoiceCards.Invoke(relic, [])!;
    }

    private static bool HasConcreteMode(RelicModel relic, PageRelicRegistration registration)
    {
        object? mode = registration.ModeProperty.GetValue(relic);
        if (mode == null)
        {
            return false;
        }

        return Convert.ToInt32(mode) != 0;
    }

    private static object GetMode(RelicModel relic, PageRelicRegistration registration)
    {
        return registration.ModeProperty.GetValue(relic)
            ?? throw new MissingMemberException(relic.GetType().FullName, "Mode");
    }

    private static bool TryGetRegistration(RelicModel relic, out PageRelicRegistration registration)
    {
        return PageRelicsById.Value.TryGetValue(relic.Id, out registration!);
    }

    private static bool IsRegisteredPageChoiceCard(CardModel card)
    {
        foreach (PageRelicRegistration registration in PageRelicsById.Value.Values)
        {
            try
            {
                object? mode = registration.ResolveModeFromChoiceCard.Invoke(
                    null,
                    [card]);
                if (mode != null && Convert.ToInt32(mode) != 0)
                {
                    return true;
                }
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException is InvalidOperationException)
            {
            }
        }

        return false;
    }

    private static IReadOnlyDictionary<ModelId, PageRelicRegistration> DiscoverPageRelics()
    {
        Dictionary<ModelId, PageRelicRegistration> registrations = [];
        foreach (Type type in LibraryAssemblyTypes.All)
        {
            if (type.IsAbstract || !typeof(RelicModel).IsAssignableFrom(type))
            {
                continue;
            }

            PropertyInfo? modeProperty = AccessTools.Property(type, "Mode");
            MethodInfo? createModeChoiceCards = FindCreateModeChoiceCards(type);
            MethodInfo? resolveModeFromChoiceCard = FindResolveModeFromChoiceCard(type, modeProperty);
            if (modeProperty == null || createModeChoiceCards == null || resolveModeFromChoiceCard == null)
            {
                continue;
            }

            ModelId id = ModelDb.GetId(type);
            registrations[id] = new PageRelicRegistration(
                type,
                createModeChoiceCards,
                resolveModeFromChoiceCard,
                modeProperty,
                DiscoverPostObtainedEffects(type, modeProperty.PropertyType));
        }

        Log.Info("[LibraryOfRuina.PageRelic] Auto-registered "
            + registrations.Count
            + " abnormality page relic preselection entries.");
        return registrations;
    }

    private static MethodInfo? FindCreateModeChoiceCards(Type type)
    {
        return type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(static method =>
                method.Name == "CreateModeChoiceCards"
                && method.GetParameters().Length == 0
                && typeof(IReadOnlyList<CardModel>).IsAssignableFrom(method.ReturnType));
    }

    private static MethodInfo? FindResolveModeFromChoiceCard(Type type, PropertyInfo? modeProperty)
    {
        if (modeProperty?.PropertyType.IsEnum != true)
        {
            return null;
        }

        return type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return method.Name == "ResolveModeFromChoiceCard"
                    && method.ReturnType == modeProperty.PropertyType
                    && parameters.Length == 1
                    && parameters[0].ParameterType == typeof(CardModel);
            });
    }

    private static IReadOnlyList<PageRelicPostObtainedEffect> DiscoverPostObtainedEffects(Type type, Type modeType)
    {
        List<PageRelicPostObtainedEffect> effects = [];
        foreach (MethodInfo method in type.GetMethods(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            foreach (AbnormalityPagePostObtainEffectAttribute attribute in method.GetCustomAttributes<AbnormalityPagePostObtainEffectAttribute>(inherit: false))
            {
                ValidatePostObtainedEffectMethod(type, method, modeType);
                effects.Add(new PageRelicPostObtainedEffect(method, attribute.ModeValue));
            }
        }

        return effects;
    }

    private static void ValidatePostObtainedEffectMethod(Type relicType, MethodInfo method, Type modeType)
    {
        if (method.ReturnType != typeof(void) && !typeof(Task).IsAssignableFrom(method.ReturnType))
        {
            throw new InvalidOperationException(relicType.FullName
                + "."
                + method.Name
                + " marked with AbnormalityPagePostObtainEffect must return void or Task.");
        }

        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length > 1 || (parameters.Length == 1 && parameters[0].ParameterType != modeType))
        {
            throw new InvalidOperationException(relicType.FullName
                + "."
                + method.Name
                + " marked with AbnormalityPagePostObtainEffect must accept no parameters or one "
                + modeType.Name
                + " parameter.");
        }
    }

    private static async Task InvokePostObtainedEffect(
        RelicModel relic,
        PageRelicPostObtainedEffect effect,
        object mode)
    {
        object? result = effect.Method.GetParameters().Length == 0
            ? effect.Method.Invoke(relic, [])
            : effect.Method.Invoke(relic, [mode]);
        if (result is Task task)
        {
            await task;
        }
    }

    private static bool ShouldKeepRewardAvailableOnOverlayLifecycleException(Exception exception)
    {
        if (exception is not NullReferenceException && exception is not ObjectDisposedException)
        {
            return false;
        }

        string stackTrace = exception.StackTrace ?? string.Empty;
        return stackTrace.Contains(
                "MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NChooseACardSelectionScreen.AfterOverlayShown",
                StringComparison.Ordinal)
            || stackTrace.Contains(
                "MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NChooseACardSelectionScreen.AfterOverlayHidden",
                StringComparison.Ordinal);
    }

    private sealed class PageRelicRegistration(
        Type relicType,
        MethodInfo createModeChoiceCards,
        MethodInfo resolveModeFromChoiceCard,
        PropertyInfo modeProperty,
        IReadOnlyList<PageRelicPostObtainedEffect> postObtainedEffects)
    {
        public Type RelicType { get; } = relicType;

        public MethodInfo CreateModeChoiceCards { get; } = createModeChoiceCards;

        public MethodInfo ResolveModeFromChoiceCard { get; } = resolveModeFromChoiceCard;

        public PropertyInfo ModeProperty { get; } = modeProperty;

        public IReadOnlyList<PageRelicPostObtainedEffect> PostObtainedEffects { get; } = postObtainedEffects;
    }

    private sealed class PageRelicPostObtainedEffect(MethodInfo method, int? modeValue)
    {
        public MethodInfo Method { get; } = method;

        public int? ModeValue { get; } = modeValue;

        public bool AppliesTo(object mode)
        {
            return !ModeValue.HasValue || Convert.ToInt32(mode) == ModeValue.Value;
        }
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class AbnormalityPagePostObtainEffectAttribute : Attribute
{
    public AbnormalityPagePostObtainEffectAttribute()
    {
    }

    public AbnormalityPagePostObtainEffectAttribute(int modeValue)
    {
        ModeValue = modeValue;
    }

    public int? ModeValue { get; }
}

[HarmonyPatch(typeof(RelicReward), "OnSelect")]
internal static class AbnormalityPageRelicRewardSelectPatch
{
    private static readonly FieldInfo? RelicField = AccessTools.Field(typeof(RelicReward), "_relic");
    private static readonly FieldInfo? WasTakenField = AccessTools.Field(typeof(RelicReward), "_wasTaken");
    private static readonly MethodInfo? ClaimedRelicSetter =
        AccessTools.PropertySetter(typeof(RelicReward), nameof(RelicReward.ClaimedRelic));

    public static bool Prefix(RelicReward __instance, ref Task<bool> __result)
    {
        if (RelicField?.GetValue(__instance) is not RelicModel relic
            || !AbnormalityPageRewardPreselection.IsPageRelic(relic))
        {
            return true;
        }

        __result = SelectPageRelicReward(__instance, relic);
        return false;
    }

    private static async Task<bool> SelectPageRelicReward(RelicReward reward, RelicModel relic)
    {
        if (!await AbnormalityPageRewardPreselection.TryPreselectPageChoice(relic, reward.Player))
        {
            return false;
        }

        Log.Info($"Obtained {relic.Id} from relic reward");
        RelicModel claimedRelic = await RelicCmd.Obtain(relic, reward.Player);
        ClaimedRelicSetter?.Invoke(reward, [claimedRelic]);
        RewardSyncCompat.SyncObtainedRelicForReward(relic);
        WasTakenField?.SetValue(reward, true);
        return true;
    }
}

[HarmonyPatch(typeof(RelicCmd), nameof(RelicCmd.Obtain), typeof(RelicModel), typeof(Player), typeof(int))]
internal static class AbnormalityPageRelicObtainPatch
{
    public static bool Prefix(
        RelicModel relic,
        Player player,
        ref Task<RelicModel> __result,
        out bool __state)
    {
        __state = false;
        if (!AbnormalityPageRewardPreselection.IsPageRelic(relic))
        {
            return true;
        }

        // 页遗物是每次 run 唯一的非堆叠遗物。旧版 Beta 奖励补丁额外发送
        // RewardObtainedMessage 时，对端会在原生 RewardsSetSynchronizer 已经回放
        // RelicReward.OnSelect 后再次进入此处。直接复用已有实例，确保背包、拾取后
        // 效果和 PlayerChoiceSynchronizer 的 choice ID 都只推进一次。
        RelicModel? existing = relic.IsStackable
            ? null
            : player.Relics.FirstOrDefault(owned => owned.Id == relic.Id);
        if (existing != null)
        {
            Log.Warn(
                "[LibraryOfRuina.PageRelic] Ignored duplicate non-stackable page relic obtain; "
                + $"player={player.NetId} relic={relic.Id}.");
            __result = Task.FromResult(existing);
            return false;
        }

        __state = AbnormalityPageRewardPreselection.HasConcreteModeForPatch(relic);
        return true;
    }

    public static void Postfix(bool __state, ref Task<RelicModel> __result)
    {
        if (!__state)
        {
            return;
        }

        __result = ApplyPostObtainedPageEffects(__result);
    }

    private static async Task<RelicModel> ApplyPostObtainedPageEffects(Task<RelicModel> obtainTask)
    {
        RelicModel relic = await obtainTask;
        await AbnormalityPageRewardPostObtainedEffects.Apply(relic);
        return relic;
    }
}

internal static class AbnormalityPageRewardPostObtainedEffects
{
    public static Task Apply(RelicModel relic) =>
        AbnormalityPageRewardPreselection.ApplyPostObtainedEffects(relic);
}
