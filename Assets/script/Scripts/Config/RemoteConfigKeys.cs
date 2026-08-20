using System.Collections.Generic;

/// <summary>
/// Every Remote Config key the game reads, with the default it falls back to.
///
/// This is the single source of truth, and the defaults here are load-bearing in two ways. They
/// are what <c>SetDefaultsAsync</c> registers, so a failed or throttled fetch — or a completely
/// empty console, which is the current state — resolves to them instead of to zero. And because
/// every default is set to the value the game already ships with, adding Remote Config changes no
/// behaviour by itself: the console starts out saying exactly what the build already does, and
/// only diverges when somebody deliberately changes a value.
///
/// Note that "the value the game ships with" is often NOT the C# field initializer — Unity
/// serializes overrides into the scene. The numbers below were read out of
/// <c>Assets/Scenes/gameScene.unity</c> where the field is a <c>[SerializeField]</c>, not out of
/// the class. <c>RemoteConfigDefaultsMatchShippedBehaviour</c> in the test suite pins that down so
/// the two can't drift apart silently.
///
/// Keys are flat rather than bundled into one JSON blob (which is what the old <c>GameConfig</c>
/// key did). Firebase Conditions and A/B Testing target individual parameters, so a blob makes it
/// impossible to run an experiment on a single value or to roll one back on its own.
/// </summary>
public static class RemoteConfigKeys
{
    // ---- Ads ------------------------------------------------------------------------------
    // The unit IDs default to Google's test units, which is what the build currently loads. That
    // means no revenue until they are switched in the console — deliberate, so internal testing
    // can't rack up invalid traffic on the real units and get the AdMob account flagged. Flipping
    // to production is a console change, not a build.
    public const string AdsEnabled = "ads_enabled";
    public const string AdsInterstitialUnitIdAndroid = "ads_interstitial_unit_id_android";
    public const string AdsRewardedUnitIdAndroid = "ads_rewarded_unit_id_android";
    public const string AdsInterstitialUnitIdIos = "ads_interstitial_unit_id_ios";
    public const string AdsRewardedUnitIdIos = "ads_rewarded_unit_id_ios";
    public const string AdsInterstitialCooldownSeconds = "ads_interstitial_cooldown_seconds";
    public const string AdsInterstitialMinLevelEnds = "ads_interstitial_min_level_ends";

    // Google's public test unit IDs. Safe to hammer, never pay out.
    public const string TestInterstitialUnitId = "ca-app-pub-3940256099942544/1033173712";
    public const string TestRewardedUnitId = "ca-app-pub-3940256099942544/5224354917";

    // ---- Adventure difficulty curves -------------------------------------------------------
    public const string AdvDifficultyCeiling = "adv_difficulty_ceiling";
    public const string AdvDifficultySaturationLevel = "adv_difficulty_saturation_level";
    public const string AdvInLevelDifficultyPeak = "adv_in_level_difficulty_peak";
    public const string AdvSizeSaturationLevel = "adv_size_saturation_level";
    public const string AdvTargetFloor = "adv_target_floor";
    public const string AdvTargetCeiling = "adv_target_ceiling";
    public const string AdvFillFloor = "adv_fill_floor";
    public const string AdvFillCeiling = "adv_fill_ceiling";
    public const string AdvMinPerType = "adv_min_per_type";
    public const string AdvFirstGeneratedLevel = "adv_first_generated_level";

    // Deliberately absent: a key for how many hand-authored levels precede generated ones.
    // AppManager holds it as a private const and GameManager holds a second copy of the same
    // value, so exposing it would mean either wiring one and silently leaving the other stale, or
    // untangling those two near-duplicate managers first. A console key that moves one of two
    // copies is worse than no key at all — the setting would appear to work and then not.

    // ---- Tray selection --------------------------------------------------------------------
    public const string TrayJackpotReliefFill = "tray_jackpot_relief_fill";
    public const string TrayMinSafeAnchors = "tray_min_safe_anchors";
    public const string TrayRobustAnchorCap = "tray_robust_anchor_cap";

    // ---- Gifts ------------------------------------------------------------------------------
    public const string GiftEnabled = "gift_enabled";
    public const string GiftCooldownRefillsClassic = "gift_cooldown_refills_classic";
    public const string GiftAdventureScale = "gift_adventure_scale";
    public const string GiftAdventureCooldownRefills = "gift_adventure_cooldown_refills";

    // ---- Classic difficulty and scoring ----------------------------------------------------
    public const string ClassicScoreForMaxDifficulty = "classic_score_for_max_difficulty";
    public const string ClassicRandomInitialShapes = "classic_random_initial_shapes";
    public const string ScorePerPlacedCell = "score_per_placed_cell";
    public const string ScorePerClearedCell = "score_per_cleared_cell";

    // ---- Revive and economy ------------------------------------------------------------------
    public const string ReviveMaxPerRun = "revive_max_per_run";
    public const string ReviveCountdownSeconds = "revive_countdown_seconds";
    public const string NoMovesReviveDelay = "no_moves_revive_delay";
    public const string DailyBonusEnabled = "daily_bonus_enabled";
    public const string DailyBonusMinLevels = "daily_bonus_min_levels";
    public const string DailyBonusMaxLevels = "daily_bonus_max_levels";

    // ---- Operations --------------------------------------------------------------------------
    // Both of these currently ship ENABLED (the scene overrides the false in code), so the
    // defaults here deliberately turn them off — the one place this schema intentionally changes
    // shipped behaviour, because per-refill Debug.Log on a release build is a real frame cost.
    public const string DebugLogShapeSelection = "debug_log_shape_selection";
    // Named for GridPlacer, which is what it actually silences. There is a separate PlaceManager
    // class with its own debugLogs flag; conflating the two in the key name would have sent
    // whoever flipped it looking at the wrong logs.
    public const string DebugLogGridPlacer = "debug_log_grid_placer";

    /// <summary>
    /// Registered with Firebase via <c>SetDefaultsAsync</c>. Also what every getter falls back to,
    /// so the game behaves identically whether the fetch succeeded, failed, or was throttled.
    /// </summary>
    public static Dictionary<string, object> BuildDefaults()
    {
        return new Dictionary<string, object>
        {
            // Ads
            { AdsEnabled, true },
            { AdsInterstitialUnitIdAndroid, TestInterstitialUnitId },
            { AdsRewardedUnitIdAndroid, TestRewardedUnitId },
            { AdsInterstitialUnitIdIos, TestInterstitialUnitId },
            { AdsRewardedUnitIdIos, TestRewardedUnitId },
            // 0 = no gating, matching today's behaviour where an interstitial fires on every
            // level end, every fail, and every Classic loss with nothing in between.
            { AdsInterstitialCooldownSeconds, 0L },
            { AdsInterstitialMinLevelEnds, 0L },

            // Adventure curves — mirror AdventureLevelCurves
            { AdvDifficultyCeiling, 75L },
            { AdvDifficultySaturationLevel, 65L },
            { AdvInLevelDifficultyPeak, 90L },
            { AdvSizeSaturationLevel, 150L },
            { AdvTargetFloor, 12L },
            { AdvTargetCeiling, 45L },
            { AdvFillFloor, 8L },
            { AdvFillCeiling, 18L },
            { AdvMinPerType, 3L },
            { AdvFirstGeneratedLevel, 4L },

            // Tray selection — mirror TraySelectionCore
            { TrayJackpotReliefFill, 0.45d },
            { TrayMinSafeAnchors, 3L },
            { TrayRobustAnchorCap, 12L },

            // Gifts — ShapeTrayManager
            { GiftEnabled, true },
            { GiftCooldownRefillsClassic, 3L },
            { GiftAdventureScale, 0.35d },
            { GiftAdventureCooldownRefills, 6L },

            // Classic difficulty and scoring. scorePerPlacedCell/scorePerClearedCell are 1 and 2
            // in code but 10 and 100 in the scene — the scene wins.
            { ClassicScoreForMaxDifficulty, 500000d },
            { ClassicRandomInitialShapes, 2L },
            { ScorePerPlacedCell, 10L },
            { ScorePerClearedCell, 100L },

            // Revive and economy. noMovesReviveDelay is 0.7 in code, 5 in the scene.
            { ReviveMaxPerRun, 3L },
            { ReviveCountdownSeconds, 5d },
            { NoMovesReviveDelay, 5d },
            { DailyBonusEnabled, true },
            { DailyBonusMinLevels, 1L },
            { DailyBonusMaxLevels, 3L },

            // Operations
            { DebugLogShapeSelection, false },
            { DebugLogGridPlacer, false },
        };
    }
}
