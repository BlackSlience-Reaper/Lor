using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using LibraryOfRuina.content.abnormalities.FairyFestival;
using LibraryOfRuina.content.abnormalities.KingOfGreed;
using LibraryOfRuina.content.abnormalities.Nosferatu;
using LibraryOfRuina.content.abnormalities.RoadHome;
using LibraryOfRuina.content.abnormalities.WrathServant;
using LibraryOfRuina.content.liberation.Art;
using LibraryOfRuina.content.liberation.Language;
using LibraryOfRuina.content.liberation.Natural;
using LibraryOfRuina.content.liberation.Philosophy;
using LibraryOfRuina.content.liberation.Social;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.powers;
using LibraryOfRuina.specialguests.Iori;
using LibraryOfRuina.specialguests.Kali;
using LibraryOfRuina.specialguests.Rnfmabj;
using LibraryOfRuina.specialguests.Xiao;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Combat.HealthBars;

namespace LibraryOfRuina.ui;

internal static class LibraryHealthBarForecastColors
{
    internal static readonly Color HpLock = new(0.82f, 0.84f, 0.86f);

    internal static readonly Color Corrosion = new(0.16f, 0.42f, 0.06f);

    internal static readonly Color RnfmabjCorrosion = new(0.09f, 0.10f, 0.10f);

    internal static readonly Color Spore = new(0.90f, 0.84f, 0.42f);
}

internal static class LibraryHealthBarForecastFeature
{
    private const string ModId = "LibraryOfRuina";
    private const string HpLockSourceId = "hp-lock.v1";
    private const int HpLockOrder = -1000;
    private static bool _initialized;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        HealthBarForecastRegistry.Register(
            ModId,
            HpLockSourceId,
            new HpLockForecastSource());
        _initialized = true;
    }

    internal static bool IsHealthBarLocked(Creature creature)
    {
        if (creature.Powers
            .OfType<LibraryFakeDeathPowerModel>()
            .Any(static power => power.IsHealthBarLockActive))
        {
            return true;
        }
        if (creature.Powers
            .OfType<LanguageFloorDipsiaTransformPower>()
            .Any(static power => power.IsPendingTransform))
        {
            return true;
        }
        if (creature.Powers
            .OfType<NosferatuTransformPower>()
            .Any(static power => power.IsPendingTransform))
        {
            return true;
        }
        if (creature.Powers
            .OfType<PhilosophyFloorTwilightPeacePowerBase>()
            .Any(static power => power.IsHealthBarLockActive))
        {
            return true;
        }
        if (creature.Powers
            .OfType<GreenStemHermitProtectionPower>()
            .Any(static power => power.IsHealthBarLockActive))
        {
            return true;
        }
        if (creature.Powers.OfType<LibraryOfRuina.content.liberation.Natural.NaturalFloorExploitedPower>()
            .Any(static power => power.IsHealthBarLockActive))
        {
            return true;
        }
        if (creature.Powers.OfType<NaturalFloorFlickeringDesirePower>()
            .Any(static power => power.IsHealthBarLockActive))
        {
            return true;
        }
        if (creature.Powers.OfType<LibraryOfRuinaFlickeringDesirePower>()
            .Any(static power => power.IsHealthBarLockActive))
        {
            return true;
        }
        if (creature.Powers
            .OfType<FairyMassCarePower>()
            .Any(static power => power.IsHealthBarLockActive))
        {
            return true;
        }
        if (creature.Powers.OfType<RoadHomeHouseProtectionPower>().Any())
        {
            return true;
        }
        if (creature.Powers.OfType<ArtFloorDustbornWinterStasisPower>().Any())
        {
            return true;
        }

        // if (creature.Powers.OfType<IoriDimensionalWalkPassivePower>().Any())
        // {
        //     return true;
        // }
        return creature.Monster switch
        {
            LibraryOfRuina.reverberation.GearChurch.ReverberationEileen eileen => eileen.IsHealthBarLockActive,
            LanguageFloorCobaltScar scar => scar.IsHealthBarLockActive,
            FalseThrone throne => throne.IsHealthBarLockActive,
            Kali kali => kali.IsHealthBarLockActive,
            XiaoStageOne { IsFakeDead: true } => true,
            RnfmabjHandBase { IsFakeDead: true } => true,
            IoriMonsterBase {IsHealthBarLockActive: true} => true,
            _ => false
        };
    }

    private sealed class HpLockForecastSource : IHealthBarForecastSource
    {
        public IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(
            HealthBarForecastContext context)
        {
            Creature creature = context.Creature;
            if (creature.CurrentHp <= 0 || creature.IsDead || !IsHealthBarLocked(creature))
            {
                return [];
            }

            return HealthBarForecasts.Single(
                creature.CurrentHp,
                LibraryHealthBarForecastColors.HpLock,
                HealthBarForecastGrowthDirection.FromRight,
                HpLockOrder,
                overlayMaterial: null,
                overlaySelfModulate: null,
                affectsHpLabel: false);
        }

    }
}

[HarmonyPatch(typeof(NHealthBar), "RefreshForeground")]
[HarmonyAfter("com.ritsukage.sts2-RitsuLib.framework-core")]
internal static class LibraryHealthBarLockForegroundPatch
{
    private static readonly string[] ForecastContainerNames =
    [
        "RitsuForecastRightContainer",
        "RitsuForecastLeftContainer",
        "BaseLibForecastRightContainer",
        "BaseLibForecastLeftContainer",
        "LibraryStatusDamageForecastContainer"
    ];

    private static readonly ConditionalWeakTable<NHealthBar, ForecastVisibilitySnapshot>
        VisibilitySnapshots = new();

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
        NHealthBar __instance,
        Creature ____creature,
        Control ____hpForeground,
        Control ____hpForegroundContainer,
        Control ____poisonForeground,
        Control ____doomForeground,
        float ____expectedMaxFgWidth)
    {
        ApplyLockedVisualState(
            __instance,
            ____creature,
            ____hpForeground,
            ____hpForegroundContainer,
            ____poisonForeground,
            ____doomForeground,
            ____expectedMaxFgWidth);
    }

    internal static void ApplyLockedVisualState(
        NHealthBar healthBar,
        Creature creature,
        Control hpForeground,
        Control hpForegroundContainer,
        Control poisonForeground,
        Control doomForeground,
        float expectedMaxFgWidth)
    {
        bool locked = LibraryHealthBarForecastFeature.IsHealthBarLocked(creature);
        if (!locked)
        {
            RestoreForecastContainers(healthBar);
            return;
        }

        CaptureForecastContainers(healthBar, poisonForeground);
        HideForecastContainers(poisonForeground);
        hpForeground.Visible = true;
        hpForeground.OffsetLeft = 0f;
        float maxForegroundWidth = expectedMaxFgWidth > 0f
            ? expectedMaxFgWidth
            : hpForegroundContainer.Size.X;
        if (creature.CurrentHp > 0 && creature.MaxHp > 0)
        {
            float currentWidth = Math.Max(
                (float)creature.CurrentHp / creature.MaxHp * maxForegroundWidth,
                12f);
            hpForeground.OffsetRight = currentWidth - maxForegroundWidth;
        }
        else
        {
            hpForeground.OffsetRight = 0f;
        }
        hpForeground.SelfModulate = LibraryHealthBarForecastColors.HpLock;
        poisonForeground.Visible = false;
        doomForeground.Visible = false;
    }

    private static void HideForecastContainers(Control poisonForeground)
    {
        Node? parent = poisonForeground.GetParent();
        if (parent == null)
        {
            return;
        }

        foreach (string nodeName in ForecastContainerNames)
        {
            if (parent.GetNodeOrNull<Control>(nodeName) is { } container)
            {
                container.Visible = false;
            }
        }
    }

    private static void CaptureForecastContainers(
        NHealthBar healthBar,
        Control poisonForeground)
    {
        Node? parent = poisonForeground.GetParent();
        if (parent == null)
        {
            return;
        }

        ForecastVisibilitySnapshot snapshot = VisibilitySnapshots.GetValue(
            healthBar,
            static _ => new ForecastVisibilitySnapshot());
        foreach (string nodeName in ForecastContainerNames)
        {
            if (parent.GetNodeOrNull<Control>(nodeName) is { } container)
            {
                snapshot.Capture(container);
            }
        }
    }

    private static void RestoreForecastContainers(NHealthBar healthBar)
    {
        if (!VisibilitySnapshots.TryGetValue(healthBar, out ForecastVisibilitySnapshot? snapshot))
        {
            return;
        }

        foreach (ForecastContainerVisibility entry in snapshot.Containers)
        {
            if (GodotObject.IsInstanceValid(entry.Container))
            {
                entry.Container.Visible = entry.Visible;
            }
        }

        VisibilitySnapshots.Remove(healthBar);
    }

    private sealed class ForecastVisibilitySnapshot
    {
        public List<ForecastContainerVisibility> Containers { get; } = [];

        public void Capture(Control container)
        {
            if (Containers.Any(entry => ReferenceEquals(entry.Container, container)))
            {
                return;
            }

            Containers.Add(new ForecastContainerVisibility(container, container.Visible));
        }
    }

    private sealed record ForecastContainerVisibility(
        Control Container,
        bool Visible);
}

[HarmonyPatch(typeof(NHealthBar), "SetHpBarContainerSizeWithOffsetsImmediately")]
[HarmonyAfter("com.ritsukage.sts2-RitsuLib.framework-core")]
internal static class LibraryHealthBarLockResizePatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
        NHealthBar __instance,
        Creature ____creature,
        Control ____hpForeground,
        Control ____hpForegroundContainer,
        Control ____poisonForeground,
        Control ____doomForeground,
        float ____expectedMaxFgWidth)
    {
        LibraryHealthBarLockForegroundPatch.ApplyLockedVisualState(
            __instance,
            ____creature,
            ____hpForeground,
            ____hpForegroundContainer,
            ____poisonForeground,
            ____doomForeground,
            ____expectedMaxFgWidth);
    }
}
