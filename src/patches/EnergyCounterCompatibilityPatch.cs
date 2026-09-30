using System;
using Godot;
using HarmonyLib;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace LibraryOfRuina.patches;






[HarmonyPatch(typeof(NEnergyCounter), nameof(NEnergyCounter._Ready))]
public static class EnergyCounterCompatibilityPatch
{
    private static bool ExpectsEnergyVfx => VanillaPrivate.EnergyCounterBackVfx.IsAvailable;
    private static bool ExpectsBurstVfx => VanillaPrivate.EnergyCounterBackParticles.IsAvailable;
    private static readonly Type ParticlesContainerType = typeof(NParticlesContainer);


    [HarmonyPrefix]
    public static void Prefix(NEnergyCounter __instance)
    {
        if (ExpectsEnergyVfx && !ExpectsBurstVfx)
        {
            EnsureParticleAlias(__instance, aliasName: "EnergyVfxBack", fallbackName: "BurstBack");
            EnsureParticleAlias(__instance, aliasName: "EnergyVfxFront", fallbackName: "BurstFront");
            return;
        }

        if (ExpectsBurstVfx && !ExpectsEnergyVfx)
        {
            EnsureParticleAlias(__instance, aliasName: "BurstBack", fallbackName: "EnergyVfxBack");
            EnsureParticleAlias(__instance, aliasName: "BurstFront", fallbackName: "EnergyVfxFront");
            return;
        }

        
        EnsureParticleAlias(__instance, aliasName: "EnergyVfxBack", fallbackName: "BurstBack");
        EnsureParticleAlias(__instance, aliasName: "EnergyVfxFront", fallbackName: "BurstFront");
        EnsureParticleAlias(__instance, aliasName: "BurstBack", fallbackName: "EnergyVfxBack");
        EnsureParticleAlias(__instance, aliasName: "BurstFront", fallbackName: "EnergyVfxFront");
    }

    private static void EnsureParticleAlias(NEnergyCounter counter, string aliasName, string fallbackName)
    {
        Type? expectedType = GetExpectedType(aliasName);
        if (expectedType == null)
        {
            return;
        }

        Node? existingAlias = GetNodeByNameOrUnique(counter, aliasName);
        if (existingAlias != null && expectedType.IsInstanceOfType(existingAlias))
        {
            existingAlias.UniqueNameInOwner = true;
            return;
        }

        if (existingAlias != null)
        {
            RemoveAliasNode(existingAlias);
        }

        Node? fallback = GetNodeByNameOrUnique(counter, fallbackName);
        Node? alias = CreateAliasNode(expectedType, fallback);

        if (alias == null)
        {
            return;
        }

        alias.Name = aliasName;
        alias.UniqueNameInOwner = true;
        if (alias is CpuParticles2D cpuParticles)
        {
            cpuParticles.Emitting = false;
        }

        counter.AddChild(alias);
        alias.Owner = counter;

        LorLog.WarnOnce(
            "EnergyCounterCompat.AliasAdded",
            "[EnergyCounterCompat] Added runtime-compatible energy counter VFX alias nodes.");
    }

    private static Type? GetExpectedType(string aliasName)
    {
        return aliasName switch
        {
            "BurstBack" or "BurstFront" => typeof(CpuParticles2D),
            "EnergyVfxBack" or "EnergyVfxFront" => ParticlesContainerType,
            _ => null
        };
    }

    private static Node? GetNodeByNameOrUnique(Node parent, string nodeName)
    {
        return parent.GetNodeOrNull<Node>("%" + nodeName) ?? parent.GetNodeOrNull<Node>(nodeName);
    }

    private static Node? CreateAliasNode(Type expectedType, Node? fallback)
    {
        if (fallback != null && expectedType.IsInstanceOfType(fallback))
        {
            if (fallback.Duplicate() is Node duplicated)
            {
                return duplicated;
            }
        }

        if (!typeof(Node).IsAssignableFrom(expectedType))
        {
            return null;
        }

        Node? created = Activator.CreateInstance(expectedType) as Node;
        if (created == null)
        {
            return null;
        }

        InitializeParticlesContainerField(created);
        return created;
    }

    private static void InitializeParticlesContainerField(Node node)
    {
        if (node is NParticlesContainer container)
        {
            VanillaPrivate.ParticlesContainerParticles.Set(container, []);
        }
    }

    private static void RemoveAliasNode(Node alias)
    {
        Node? parent = alias.GetParent();
        if (parent == null)
        {
            return;
        }

        parent.RemoveChild(alias);
        alias.QueueFree();
    }
}
