using System;
using Godot;
using LibraryOfRuina.powers.ArtFloorLiberation;
using LibraryOfRuina.powers.JudgementBird;
using LibraryOfRuina.powers.LanguageFloorLiberation;
using LibraryOfRuina.powers.PriceOfSilence;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.infra.helpers;

internal readonly struct ResolvedPowerIcon(string path, Texture2D texture)
{
    internal string Path { get; } = path;

    internal Texture2D Texture { get; } = texture;
}

internal static class PowerIconResolver
{
    internal static bool TryResolve(PowerModel power, out ResolvedPowerIcon resolved)
    {
        foreach (string path in GetCandidates(power))
        {
            try
            {
                if (!PathExists(path))
                {
                    continue;
                }

                Texture2D? texture = ResourceLoader.Load<Texture2D>(
                    path);
                if (!GodotTextureSafety.IsValid(texture))
                {
                    Log.Warn($"[LibraryOfRuina.PowerIcon] Icon exists but failed to load: {path}");
                    continue;
                }

                GodotTextureSafety.RegisterSourcePath(texture, path);
                resolved = new ResolvedPowerIcon(path, texture);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn(
                    "[LibraryOfRuina.PowerIcon] Icon load threw for "
                    + power.Id
                    + " path=" + path
                    + " error=" + ex.GetType().Name
                    + ": " + ex.Message);
            }
        }

        resolved = default;
        return false;
    }

    private static IReadOnlyList<string> GetCandidates(PowerModel power)
    {
        var candidates = new List<string>();

        if (power is LibraryPowerModel { IsDynamic: true } dynamicPower)
        {
            AddIfMissing(candidates, dynamicPower.ResolvedBigIconPath);
        }

        string id = ResolveIconId(power);
        AddIfMissing(candidates, ImageHelper.GetImagePath($"powers/{id}.png"));
        AddIfMissing(candidates, ImageHelper.GetImagePath($"powers/beta/{id}.png"));

        if (power is LibraryPowerModel libraryPower)
        {
            AddIfMissing(candidates, libraryPower.ResolvedBigIconPath);
        }

        return candidates;
    }

    private static string ResolveIconId(PowerModel power)
    {
        if (power is JudgementBirdPassivePower)
        {
            return "library_passive_green";
        }

        if (UsesGreenPassiveIcon(power))
        {
            return "art_floor_green_passive_power";
        }

        return power switch
        {
            TimeTraceMarkedPlayerPower => "price_of_silence_your_time_passive_power",
            ArtFloorNextTurnCollapsePower => "art_floor_collapse_power",
            LanguageFloorDipsiaHydrophobiaPassivePower => "nosferatu_hydrophobia_passive_power",
            LanguageFloorDipsiaTransformPower => "nosferatu_transform_power",
            _ => power.Id.Entry.ToLowerInvariant()
        };
    }

    private static bool PathExists(string path) =>
        ResourceLoader.Exists(path) || FileAccess.FileExists(path);

    private static void AddIfMissing(ICollection<string> paths, string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && !paths.Contains(path))
        {
            paths.Add(path);
        }
    }

    private static bool UsesGreenPassiveIcon(PowerModel power)
    {
        return power is LanguageFloorMimicryFormOneEvolutionPower
            or LanguageFloorMimicryFormTwoEvolutionPower
            or LanguageFloorMimicryFormTwoRegenerationPower
            or LanguageFloorMimicryHardenPower
            or LanguageFloorMimicryFormThreeRegenerationPower
            or LanguageFloorWolfHowlingNightmarePassivePower
            or MostBeautifulPerformancePower
            or SilentPerformancePower
            or ArtFloorEnsemblePower
            or ArtFloorErosionPower
            or BeyondFragmentTentaclePower
            or BeyondFragmentIncomprehensiblePower
            or ArtFloorLittleGalaxyEternalFarewellPower
            or ArtFloorLittleGalaxyPebblePower
            or ArtFloorGalaxyDoNotLeaveMePower
            or ArtFloorPleasureJoyThornsPower
            or ArtFloorPleasureSoftBodyPower
            or ArtFloorPleasureUnbearablePleasurePower
            or ArtFloorPleasureExplodingHeadPower
            or ArtFloorSuffocatingAtonementPower
            or ArtFloorUnfadingFlowerPower
            or ArtFloorClayDollPower
            or ArtFloorFinalDaCapoCyclePower
            or ArtFloorFinalDaCapoAriaPower
            or ArtFloorFinalDaCapoPerformerPassivePower;
    }
}
