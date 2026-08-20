using System.Collections.Generic;
using System.Linq;
using OrcaTetris.Adventure;

/// <summary>
/// Picks which 3 shapes to offer next in the tray. No lookahead, no hypothetical
/// combos — each candidate shape is judged only on what IT achieves by itself,
/// placed at its single best spot on the board exactly as it looks right now.
/// Shapes that clear a line are ranked first — a full clear beats everything,
/// then whoever clears the most lines, then (as a tiebreak) the smaller/simpler
/// shape — but difficulty caps how many of the 3 slots may be *multi-line*
/// clears. At max helpfulness all 3 can be, which is what made the tray feel
/// like it was solving the level for the player; at max difficulty only 1 is and
/// the other clearing slots drop to single-line options, so building the big
/// combo is the player's job again. Clearing shapes are never withheld outright,
/// and the cap lifts once the board is crowded — see TraySelectionCore for why
/// both of those are load-bearing. Slots not filled by a clear go to whichever
/// placeable shapes leave the board healthiest.
/// This keeps every offered shape something the player can actually look at
/// and see why it was offered — no 2-3-move-deep setup a player has no way to
/// perceive.
///
/// When decoys are enabled, difficulty also controls how many of the 3 slots
/// are guaranteed placeable at all: at max helpfulness all 3 are real options,
/// at max difficulty only 1 is — the other 2 are genuine decoys (shapes that
/// don't fit anywhere on the board right now), scaling linearly in between. On
/// a mostly empty board almost everything fits somewhere, so this only bites
/// once the board has enough clutter for decoys to actually exist.
///
/// Decoys are Classic-only. In Adventure they were an instant-loss trap rather
/// than a difficulty dial: the tray only refills once all 3 shapes are placed
/// (ShapeTrayManager.RefillIfNeeded), and Adventure has no revive, so the moment
/// the player placed the real shapes and only decoys were left the level ended
/// on the spot no matter how well they'd played — with every slot forced
/// placeable at deal time, whether the tray survives to the next refill is on
/// the player's own placements instead.
///
/// This class is only the Shape-prefab adapter; the policy itself lives in
/// <see cref="TraySelectionCore"/>, in an assembly the level solver can also
/// reference. That split is what lets AdventureSolvabilityTests validate the
/// real tray behaviour rather than a reimplementation of it.
/// </summary>
public class ShapeSelectionAlgorithm
{
    /// <param name="grid">Current board occupancy, [x, y].</param>
    /// <param name="shapePool">All shapes that may be offered.</param>
    /// <param name="cellSize">Passed through to Shape.GetCells to read each shape's footprint.</param>
    /// <param name="difficulty">
    /// 1 = most helpful/easy, 0 = most chaotic/hard. Controls three things:
    /// how many of the 3 slots are guaranteed placeable at all (3 at
    /// difficulty 1 down to 1 at difficulty 0, the rest filled with decoys
    /// that don't fit anywhere right now); how many slots may arrive with a
    /// multi-line clear already set up (again 3 down to 1 — the one dial that
    /// still bites in Adventure, where every slot is forced placeable); and — as a
    /// fallback when fewer than the real-slot count can clear anything on
    /// their own — whether remaining real slots prefer whichever placeable
    /// shape leaves the board healthiest (1) or are picked without regard to
    /// board health (0).
    /// </param>
    /// <param name="allowDecoys">
    /// Whether unplaceable shapes may fill the slots above the real-slot count.
    /// False forces all 3 slots to be genuinely placeable regardless of
    /// difficulty — required in Adventure, where an unplayable slot is an
    /// instant loss rather than a challenge.
    /// </param>
    public List<Shape> SelectTray(bool[,] grid, IReadOnlyList<Shape> shapePool, float cellSize, float difficulty, bool allowDecoys = true)
    {
        var result = new List<Shape>();
        if (grid == null || shapePool == null || shapePool.Count == 0)
            return result;

        var pool = shapePool.Where(p => p != null).Distinct().ToList();
        if (pool.Count == 0)
            return result;

        var offsets = pool.Select(p => p.GetCells(cellSize)).ToList();

        var picks = TraySelectionCore.SelectTray(
            grid, offsets, difficulty, allowDecoys, SharedRandom);

        foreach (int index in picks)
            result.Add(pool[index]);

        return result;
    }

    /// <summary>
    /// Adventure's tray policy: see <see cref="TraySelectionCore.SelectTrayChained"/> for why it's
    /// a different method rather than another <see cref="SelectTray"/> parameter. Classic doesn't
    /// use this — it has a revive safety net and gets its difficulty from decoys instead, which
    /// chaining has no equivalent of.
    /// </summary>
    public List<Shape> SelectTrayChained(bool[,] grid, IReadOnlyList<Shape> shapePool, float cellSize, float difficulty)
    {
        var result = new List<Shape>();
        if (grid == null || shapePool == null || shapePool.Count == 0)
            return result;

        var pool = shapePool.Where(p => p != null).Distinct().ToList();
        if (pool.Count == 0)
            return result;

        var offsets = pool.Select(p => p.GetCells(cellSize)).ToList();

        var picks = TraySelectionCore.SelectTrayChained(grid, offsets, difficulty, SharedRandom);

        foreach (int index in picks)
            result.Add(pool[index]);

        return result;
    }

    // TraySelectionCore takes an explicit randomness source so a simulation can be made
    // deterministic. Live gameplay just wants "random", so it shares one instance.
    //
    // Deliberately System.Random's own parameterless (clock-seeded) constructor rather than
    // seeding from UnityEngine.Random: this field initializes the first time a
    // ShapeSelectionAlgorithm is constructed, which happens inside ShapeTrayManager's own field
    // initializer — i.e. before Awake — and Unity throws if UnityEngine.Random is touched from a
    // MonoBehaviour's constructor phase. That exception aborted every field initializer after it
    // on ShapeTrayManager (activeShapes, loadedPrefabs, ...) and permanently broke this type for
    // the rest of the session, since a failed static constructor stays failed. System.Random has
    // no such restriction.
    private static readonly System.Random SharedRandom = new System.Random();
}
