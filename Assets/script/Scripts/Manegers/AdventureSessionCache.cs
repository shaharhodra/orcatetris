using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// In-memory (not persisted to disk) snapshot of an in-progress Adventure level, so a
/// player who exits to the lobby via BackButton and re-enters the same level resumes
/// exactly where they left off. Lives for the process lifetime as a plain static cache —
/// closing the app clears it naturally, matching the "only while the app stays open" requirement.
/// </summary>
public static class AdventureSessionCache
{
    private static AdventureLevelSnapshot cached;

    /// <summary>
    /// Captures the current gameplay scene's board, tray and target state for the level
    /// currently loaded in AppManager. No-op if not in Adventure mode or the gameplay
    /// components can't be found (e.g. called from outside the game scene).
    /// </summary>
    public static void CaptureCurrentScene()
    {
        if (AppManager.instance == null || AppManager.instance.CurrentGameMode != AppManager.GameMode.Adventure)
            return;

        var levelData = AppManager.instance.CurrentLevelData;
        if (levelData == null)
            return;

        var board = Object.FindFirstObjectByType<GridBoard>();
        var tray = Object.FindFirstObjectByType<ShapeTrayManager>();
        var adventureManager = Object.FindFirstObjectByType<AdventureManager>();

        if (board == null || tray == null || adventureManager == null)
            return;

        cached = new AdventureLevelSnapshot
        {
            levelNumber = levelData.Level,
            gridBlocks = board.CaptureCurrentBlocks(),
            traySlots = tray.CaptureTrayState(),
            currentWaveIndex = tray.CurrentWaveIndex,
            remainingTargets = new Dictionary<ColectionTypes, int>(adventureManager.RemainingTargets)
        };
    }

    /// <summary>
    /// Returns the cached snapshot only if it matches the requested level number, else null.
    /// </summary>
    public static AdventureLevelSnapshot GetSnapshotFor(int levelNumber)
    {
        return (cached != null && cached.levelNumber == levelNumber) ? cached : null;
    }

    /// <summary>
    /// Drops the cached snapshot. Call whenever the level is finished with (completed,
    /// failed-and-retried) so a stale mid-level state can't leak into the next attempt.
    /// </summary>
    public static void Clear()
    {
        cached = null;
    }
}

public class AdventureLevelSnapshot
{
    public int levelNumber;
    public List<InitialBlockData> gridBlocks;
    public List<TraySlotSnapshot> traySlots;
    public int currentWaveIndex;
    public Dictionary<ColectionTypes, int> remainingTargets;
}

public class TraySlotSnapshot
{
    public int slotIndex;
    public string shapeName;
    public List<SymbolData> symbols;
}
