using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.patches;

internal static class CurrentRunSaveValidationState
{
    private const string PopupTitle = "Continue Run Disabled";
    private static string? _pendingPopupBody;
    private static string? _lastInvalidReason;

    public static void QueuePopup(string reason)
    {
        if (string.Equals(_lastInvalidReason, reason, StringComparison.Ordinal))
        {
            return;
        }

        _lastInvalidReason = reason;
        _pendingPopupBody =
            "A continue-run save was blocked before the main menu loaded.\n\n"
            + reason
            + "\n\nRe-enable the missing mod to recover the run, or choose Abandon Run to clear it.";
    }

    public static void ResetSession()
    {
        _lastInvalidReason = null;
        _pendingPopupBody = null;
    }

    public static string? ConsumePopupBody()
    {
        string? popupBody = _pendingPopupBody;
        _pendingPopupBody = null;
        return popupBody;
    }

    public static string GetPopupTitle() => PopupTitle;
}

internal static class CurrentRunSaveValidator
{
    public static bool TryRewriteInvalidResult(ref ReadSaveResult<SerializableRun> result)
    {
        if (!result.Success || result.SaveData == null)
        {
            return false;
        }

        if (!TryGetFailureReason(result.SaveData, out string reason))
        {
            return false;
        }

        Log.Warn("[LibraryOfRuina] Blocking continue-run save because it references missing data: " + reason);
        CurrentRunSaveValidationState.QueuePopup(reason);
        result = new ReadSaveResult<SerializableRun>(
            ReadSaveStatus.ValidationFailed,
            "Continue run blocked by validation guard: " + reason);
        return true;
    }

    private static bool TryGetFailureReason(SerializableRun save, out string reason)
    {
        if (save.Players.Count == 0)
        {
            reason = "Continue save contains no players.";
            return true;
        }

        for (int i = 0; i < save.Players.Count; i++)
        {
            SerializablePlayer player = save.Players[i];
            ModelId? characterId = player.CharacterId;
            if (characterId == null || characterId == ModelId.none)
            {
                reason = $"Continue save is missing a character id for player {i + 1}.";
                return true;
            }

            if (ModelDb.GetByIdOrNull<CharacterModel>(characterId) == null)
            {
                reason = $"Missing character model '{characterId}' for player {i + 1}.";
                return true;
            }
        }

        if (save.Acts.Count == 0)
        {
            reason = "Continue save contains no acts.";
            return true;
        }

        if (save.CurrentActIndex < 0 || save.CurrentActIndex >= save.Acts.Count)
        {
            reason = $"Continue save has an invalid current act index ({save.CurrentActIndex}).";
            return true;
        }

        for (int i = 0; i < save.Acts.Count; i++)
        {
            ModelId? actId = save.Acts[i].Id;
            if (actId == null || actId == ModelId.none)
            {
                reason = $"Continue save is missing an act id in slot {i + 1}.";
                return true;
            }

            if (ModelDb.GetByIdOrNull<ActModel>(actId) == null)
            {
                reason = $"Missing act model '{actId}' in slot {i + 1}.";
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }
}

[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.LoadRunSave))]
internal static class CurrentRunSaveValidationPatch
{
    [HarmonyPostfix]
    public static void Postfix(ref ReadSaveResult<SerializableRun> __result)
    {
        if (!CurrentRunSaveValidator.TryRewriteInvalidResult(ref __result))
        {
            CurrentRunSaveValidationState.ResetSession();
        }
    }
}

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu.RefreshButtons))]
internal static class MainMenuInvalidRunSavePopupPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        if (!SaveManager.Instance.HasRunSave)
        {
            CurrentRunSaveValidationState.ResetSession();
            return;
        }

        string? popupBody = CurrentRunSaveValidationState.ConsumePopupBody();
        if (string.IsNullOrEmpty(popupBody))
        {
            return;
        }

        string popupBodyText = popupBody;

        Callable.From(() =>
        {
            NModalContainer? modalContainer = NModalContainer.Instance;
            if (modalContainer == null)
            {
                Log.Warn("[LibraryOfRuina] Unable to show invalid continue-run popup: NModalContainer.Instance is null.");
                return;
            }

            NErrorPopup? popup = NErrorPopup.Create(CurrentRunSaveValidationState.GetPopupTitle(), popupBodyText, showReportBugButton: false);
            if (popup == null)
            {
                return;
            }

            modalContainer.Add(popup);
            modalContainer.ShowBackstop();
        }).CallDeferred();
    }
}
