using System;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.features.settings;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Saves;

namespace LibraryOfRuina.features.ftue;

/// <summary>
/// Queues the current LoR combat tutorial from SetUpCombat. Current Beta keeps
/// StartCombatInternal alive for the whole combat, so a postfix on that method
/// cannot own combat-start FTUE timing.
/// </summary>
public static class LibraryOfRuinaCombatFtuePatch
{
    internal static void QueueForCombat(
        CombatManager combatManager,
        CombatState combatState,
        EncounterModel encounter)
    {
        string updateLogFtueId = LibraryOfRuinaFtueIds.CurrentUpdateLogTutorial;
        bool updateLogShown = FtueGuard.HasBeenShown(updateLogFtueId);
        Log.Info(
            $"[LibraryOfRuina.FTUE] Update log fingerprint check: "
            + $"fingerprint={LibraryOfRuinaFtueIds.CurrentUpdateLogTutorialFingerprint}, "
            + $"shown={updateLogShown}, enabled={LibraryOfRuinaSettings.FtueTutorialEnabled}.");

        FtueConfig config;
        if (FtueGuard.ShouldShow(updateLogFtueId))
        {
            config = new FtueConfig(updateLogFtueId, UpdateLogFtuePages20260903);
        }
        else
        {
            string combatFtueId = LibraryOfRuinaFtueIds.CurrentCombatTutorial;
            bool combatTutorialShown = FtueGuard.HasBeenShown(combatFtueId);
            Log.Info(
                $"[LibraryOfRuina.FTUE] Combat tutorial fingerprint check: "
                + $"fingerprint={LibraryOfRuinaFtueIds.CurrentCombatTutorialFingerprint}, "
                + $"shown={combatTutorialShown}, enabled={LibraryOfRuinaSettings.FtueTutorialEnabled}.");

            if (!FtueGuard.ShouldShow(combatFtueId))
                return;

            config = ResolveFtueConfig(encounter);
        }

        CombatId? combatId = combatManager.CurrentCombatId;

        if (SaveManager.Instance.SeenFtue("combat_rules_ftue"))
        {
            _ = TaskHelper.RunSafely(ShowCombatFtue(combatManager, combatId, config));
            return;
        }

        Action<CombatState>? onCombatBegan = null;
        onCombatBegan = beganState =>
        {
            combatManager.CombatBegan -= onCombatBegan;
            if (!ReferenceEquals(beganState, combatState)
                || !IsCurrentCombat(combatManager, combatId))
            {
                return;
            }

            _ = TaskHelper.RunSafely(ShowCombatFtue(combatManager, combatId, config));
        };
        combatManager.CombatBegan += onCombatBegan;
    }

    private static async Task ShowCombatFtue(
        CombatManager combatManager,
        CombatId? combatId,
        FtueConfig config)
    {
        NModalContainer? modalContainer = NModalContainer.Instance;
        var tree = modalContainer?.GetTree();
        if (tree == null) return;

        await modalContainer!.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        if (!IsCurrentCombat(combatManager, combatId))
            return;

        while (NModalContainer.Instance?.OpenModal != null)
        {
            await NModalContainer.Instance!.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            if (!IsCurrentCombat(combatManager, combatId))
                return;
        }

        modalContainer = NModalContainer.Instance;
        if (modalContainer == null || !IsCurrentCombat(combatManager, combatId))
            return;

        if (!FtueGuard.TryConsumeShowRequest(config.FtueId))
            return;

        var popup = NLibraryOfRuinaCombatRulesFtue.Create(
            config.FtueId,
            config.Pages);

        modalContainer.Add(popup);
        Log.Info($"LibraryOfRuina: Showing FTUE {config.FtueId}");
    }

    private static bool IsCurrentCombat(CombatManager combatManager, CombatId? combatId)
    {
        return combatId.HasValue
            && Nullable.Equals(combatManager.CurrentCombatId, combatId)
            && (combatManager.IsStarting || combatManager.IsInProgress);
    }

    private static FtueConfig ResolveFtueConfig(EncounterModel encounter)
    {
        LibraryOfRuinaCombatFtuePage[] encounterPages = FtueGuard.IsLiberationEncounter(encounter)
            ? LiberationCombatFtuePages
            : FtueGuard.IsAbnormalityEncounter(encounter)
                ? AbnormalityCombatFtuePages
                : GuestCombatFtuePages;

        return new FtueConfig(
            LibraryOfRuinaFtueIds.CurrentCombatTutorial,
            [.. encounterPages, .. CombatFtuePages20260827]);
    }

    private static readonly LibraryOfRuinaCombatFtuePage[] LiberationCombatFtuePages =
    [
        new(
            "LOR_FIRST_LIBERATION_FTUE_TITLE_1",
            "LOR_FIRST_LIBERATION_FTUE_BODY_1",
            "res://images/ftue/liberation_ftue_0.png"),
        new(
            "LOR_FIRST_LIBERATION_FTUE_TITLE_2",
            "LOR_FIRST_LIBERATION_FTUE_BODY_2",
            "res://images/ftue/liberation_ftue_1.png"),
        new(
            "LOR_FIRST_LIBERATION_FTUE_TITLE_3",
            "LOR_FIRST_LIBERATION_FTUE_BODY_3",
            "res://images/ftue/liberation_ftue_2.png"),
    ];

    private static readonly LibraryOfRuinaCombatFtuePage[] GuestCombatFtuePages =
    [
        new(
            "LOR_FIRST_ENEMY_FTUE_TITLE",
            "LOR_FIRST_ENEMY_FTUE_BODY",
            "res://images/ftue/guest_ftue_0.png"),
        new(
            "LOR_FIRST_ENEMY_FTUE_TITLE_1",
            "LOR_FIRST_ENEMY_FTUE_BODY_1",
            "res://images/ftue/guest_ftue_1.png"),
        new(
            "LOR_FIRST_ENEMY_FTUE_TITLE_2",
            "LOR_FIRST_ENEMY_FTUE_BODY_2",
            "res://images/ftue/guest_ftue_2.png"),
    ];

    private static readonly LibraryOfRuinaCombatFtuePage[] AbnormalityCombatFtuePages =
    [
        new(
            "LOR_FIRST_ABNORMALITY_FTUE_TITLE_1",
            "LOR_FIRST_ABNORMALITY_FTUE_BODY_1",
            "res://images/ftue/abnormality_ftue_0.png"),
        new(
            "LOR_FIRST_ABNORMALITY_FTUE_TITLE_2",
            "LOR_FIRST_ABNORMALITY_FTUE_BODY_2",
            "res://images/ftue/abnormality_ftue_1.png"),
        new(
            "LOR_FIRST_ABNORMALITY_FTUE_TITLE_3",
            "LOR_FIRST_ABNORMALITY_FTUE_BODY_3",
            "res://images/ftue/abnormality_ftue_2.png"),
    ];

    private static readonly LibraryOfRuinaCombatFtuePage[] CombatFtuePages20260827 =
    [
        new(
            "LOR_COMBAT_FTUE_20260827_RESISTANCE_TITLE",
            "LOR_COMBAT_FTUE_20260827_RESISTANCE_BODY",
            null,
            LibraryOfRuinaCombatFtueVisual.ResistanceExamples),
        new(
            "LOR_COMBAT_FTUE_20260827_MAPPING_TITLE",
            "LOR_COMBAT_FTUE_20260827_MAPPING_BODY",
            null,
            LibraryOfRuinaCombatFtueVisual.AttackTypeMapping),
    ];

    private static readonly LibraryOfRuinaCombatFtuePage[] UpdateLogFtuePages20260902 =
    [
        new(
            "LOR_UPDATE_LOG_20260902_NOTICE_TITLE",
            "LOR_UPDATE_LOG_20260902_NOTICE_BODY",
            "res://LibraryOfRuina/mod_image.png",
            LibraryOfRuinaCombatFtueVisual.CenteredImage),
        new(
            "LOR_UPDATE_LOG_20260902_SCOPE_TITLE",
            "LOR_UPDATE_LOG_20260902_SCOPE_BODY",
            "res://LibraryOfRuina/mod_image.png",
            LibraryOfRuinaCombatFtueVisual.CenteredImage),
        new(
            "LOR_UPDATE_LOG_20260902_LITERATURE_TITLE",
            "LOR_UPDATE_LOG_20260902_LITERATURE_BODY",
            "res://images/backgrounds/literature_floor_liberation_encounter/creature_map_latitia_composite.png",
            LibraryOfRuinaCombatFtueVisual.CenteredImage),
        new(
            "LOR_UPDATE_LOG_20260902_ENCOUNTERS_TITLE",
            "LOR_UPDATE_LOG_20260902_ENCOUNTERS_BODY",
            "res://images/backgrounds/blue_star_strong/blue_star_strong_background.png",
            LibraryOfRuinaCombatFtueVisual.CenteredImage),
        new(
            "LOR_UPDATE_LOG_20260902_PAGES_TITLE",
            "LOR_UPDATE_LOG_20260902_PAGES_BODY",
            "res://images/relics/blue_star_page_relic.png",
            LibraryOfRuinaCombatFtueVisual.CenteredImage),
        new(
            "LOR_UPDATE_LOG_20260902_BALANCE_TITLE",
            "LOR_UPDATE_LOG_20260902_BALANCE_BODY",
            "res://images/ftue/combat_ftue_0.png",
            LibraryOfRuinaCombatFtueVisual.CenteredImage),
    ];

    private static readonly LibraryOfRuinaCombatFtuePage[] UpdateLogFtuePages20260903 =
    [
        new(
            "LOR_UPDATE_LOG_20260903_OVERVIEW_TITLE",
            "LOR_UPDATE_LOG_20260903_OVERVIEW_BODY",
            null),
        new(
            "LOR_UPDATE_LOG_20260903_ENEMIES_TITLE",
            "LOR_UPDATE_LOG_20260903_ENEMIES_BODY",
            null),
        new(
            "LOR_UPDATE_LOG_20260903_PAGES_TITLE",
            "LOR_UPDATE_LOG_20260903_PAGES_BODY",
            null),
        new(
            "LOR_UPDATE_LOG_20260903_REWARDS_TITLE",
            "LOR_UPDATE_LOG_20260903_REWARDS_BODY",
            null),
        new(
            "LOR_UPDATE_LOG_20260903_REWARDS_2_TITLE",
            "LOR_UPDATE_LOG_20260903_REWARDS_2_BODY",
            null),
        new(
            "LOR_UPDATE_LOG_20260903_SYSTEM_TITLE",
            "LOR_UPDATE_LOG_20260903_SYSTEM_BODY",
            null),
    ];

    private readonly record struct FtueConfig(
        string FtueId,
        LibraryOfRuinaCombatFtuePage[] Pages);
}
