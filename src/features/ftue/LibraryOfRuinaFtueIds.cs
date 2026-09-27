namespace LibraryOfRuina.features.ftue;

/// <summary>
/// Central registry of all FTUE IDs used by the Library of Ruina mod.
/// Versioned IDs act as tutorial fingerprints in the mod config. Changing the
/// current fingerprint makes the updated tutorial eligible to play once.
/// </summary>
public static class LibraryOfRuinaFtueIds
{
    public const string CurrentCombatTutorialFingerprint = "20260827";
    public const string CurrentUpdateLogTutorialFingerprint = "20260903123300";
    public const string UpdateLogLocationTutorialFingerprint = "20260903195237";
    public const string NeowSpecialGuestTutorialFingerprint = "20260827190630";

    /// <summary>
    /// Shown when the player enters a map node leading to a LoR encounter
    /// for the first time, explaining what Library of Ruina content is.
    /// </summary>
    public const string MapIntro = "library_of_ruina.map_intro_ftue";

    /// <summary>
    /// Shown at the start of the first LoR combat encounter,
    /// explaining LoR-specific combat mechanics (book pages, special intents, etc.).
    /// </summary>
    public const string CombatIntro = "library_of_ruina.combat_intro_ftue";

    /// <summary>
    /// Current combat tutorial ID. This is checked when the player enters the
    /// first Library of Ruina combat after a tutorial update.
    /// </summary>
    public const string CurrentCombatTutorial =
        CombatIntro + "." + CurrentCombatTutorialFingerprint;

    /// <summary>
    /// Shown once at the start of the first Library of Ruina combat after the
    /// v0.18.7 balance update, summarizing changes since v0.18.3.
    /// </summary>
    public const string CurrentUpdateLogTutorial =
        "library_of_ruina.update_log_ftue."
        + CurrentUpdateLogTutorialFingerprint;

    /// <summary>
    /// Shown once after entering the Library Act's Neow event to point players
    /// to the permanent update-log entry in mod settings.
    /// </summary>
    public const string UpdateLogLocationTutorial =
        "library_of_ruina.update_log_location_ftue."
        + UpdateLogLocationTutorialFingerprint;

    /// <summary>
    /// Shown after the Library Act's Neow layout is ready, explaining the
    /// Book Shadow relic, Special Guest choices, and the shared emotion track.
    /// </summary>
    public const string NeowSpecialGuestIntro =
        "library_of_ruina.neow_special_guest_intro_ftue."
        + NeowSpecialGuestTutorialFingerprint;

    /// <summary>
    /// Shown on the rewards screen after the first LoR combat,
    /// reminding players about LoR-specific rewards (book page relics, etc.).
    /// </summary>
    public const string RewardsIntro = "library_of_ruina.rewards_intro_ftue";

    /// <summary>
    /// Shown at the start of the first ordinary Library of Ruina combat,
    /// pointing at the enemy resistance icons.
    /// </summary>
    public const string FirstEnemy = "library_of_ruina.first_enemy_resistance_ftue";

    /// <summary>
    /// Shown at the start of the first abnormality combat,
    /// pointing at the enemy resistance icons.
    /// </summary>
    public const string FirstAbnormality = "library_of_ruina.first_abnormality_resistance_ftue";

    /// <summary>
    /// Shown at the start of the first floor liberation combat,
    /// pointing at the stagger resistance bar.
    /// </summary>
    public const string FirstLiberation = "library_of_ruina.first_liberation_stagger_ftue";

    /// <summary>
    /// Shown the first time an abnormality page relic appears on the rewards screen.
    /// </summary>
    public const string FirstAbnormalityPageReward = "library_of_ruina.first_abnormality_page_reward_ftue";
}
