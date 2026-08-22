using UnityEngine;
using System;
using System.Collections.Generic;
using DG.Tweening;

public class GridPlacer : MonoBehaviour
{
    [SerializeField] private GridBoard board;
    // No need to serialize ScoreManager; use the singleton instance instead.

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    public event Action<Shape> OnShapePlaced;
    public event Action<LineClearResult> OnLinesCleared;
    public event Action OnNoLinesCleared;
    public event Action<List<Vector2Int>> OnBlocksPlacedBeforeClear;

    [Header("Scoring")]
    [SerializeField] private int scorePerPlacedCell = 1;
    [SerializeField] private int scorePerClearedCell = 2;

    /// <summary>
    /// Overwrites the scoring values from Remote Config. Called by GameConfigApplier after the
    /// scene's Awake pass — the scene file's serialized overrides land before that, and these two
    /// fields are a good example of why that ordering matters: the code defaults are 1 and 2, but
    /// the scene ships 10 and 100.
    /// </summary>
    public void ApplyRemoteSettings()
    {
        scorePerPlacedCell = GameSettings.GetInt(RemoteConfigKeys.ScorePerPlacedCell);
        scorePerClearedCell = GameSettings.GetInt(RemoteConfigKeys.ScorePerClearedCell);
        debugLogs = GameSettings.GetBool(RemoteConfigKeys.DebugLogGridPlacer);
    }

    // Fixed sorting order for any block once it sits on the grid, so tray shapes
    // (sortingOrder 20, set in ShapeTrayManager) always render above them,
    // regardless of whatever order the source prefab happened to be authored with.
    private const int PlacedBlockSortingOrder = 2;

    [Header("Block Entry Animation")]
    [SerializeField] private bool animateBlockEntry = false;
    [SerializeField] private float blockEntryDuration = 0.5f;
    [SerializeField] private Ease blockEntryEase = Ease.OutBack;
    [SerializeField] private float blockEntryDistance = 6f;
    [SerializeField] private float blockEntryDelayPerBlock = 0.05f;

    [Header("Impressive Placement Feedback")]
    [Tooltip("When a placed shape is bigger than this many cells AND either came from a gift combo or barely had anywhere else to go (see Tight Fit Max Valid Spots below), every one of its blocks gets a little celebratory pop plus a dedicated sound.")]
    [SerializeField] private bool enableImpressivePlacementFeedback = true;
    [Tooltip("Minimum cell count for a shape to ever qualify — small 1-2 cell shapes never feel like an achievement to fit in.")]
    [SerializeField] private int impressivePlacementMinCells = 3;
    [Tooltip("A non-gift shape counts as a tight/impressive fit when, right before this placement, the board had at most this many valid spots left for it (counting the spot just used).")]
    [SerializeField] private int tightFitMaxValidPlacements = 3;
    [SerializeField] private float impressiveBlockPunchScale = 0.25f;
    [SerializeField] private float impressiveBlockPunchDuration = 0.35f;
    [SerializeField] private int impressiveBlockPunchVibrato = 6;
    [SerializeField] private float impressiveBlockDelayPerBlock = 0.04f;

    public bool CanPlaceShape(Shape shape, Vector2Int targetCell)
    {
        var offsets = shape.GetCells(board.cellSize);
        foreach (var offset in offsets)
        {
            Vector2Int cell = targetCell + offset;

            if (!board.IsInside(cell))
            {
                //Debug.Log($"[CanPlaceShape] '{shape.name}' FAIL at {targetCell}: cell {cell} is outside grid.");
                return false;
            }

            if (board.IsOccupied(cell))
            {
                //Debug.Log($"[CanPlaceShape] '{shape.name}' FAIL at {targetCell}: cell {cell} is occupied.");
                return false;
            }
        }

        //Debug.Log($"[CanPlaceShape] '{shape.name}' can be placed at {targetCell}.");
        return true;
    }

    // Returns true if this placement cleared at least one line — callers use this to
    // skip the generic "place" sound in favor of the line-clear/combo/board-cleared
    // sound, instead of stacking both on the same placement.
    public bool PlaceShape(Shape shape, Vector2Int targetCell)
    {
        if (shape == null)
            return false;

        var offsets = shape.GetCells(board.cellSize);

        // Must be judged against the board as it stood before this placement, so compute it
        // now, before the loop below starts occupying cells.
        bool isImpressivePlacement = enableImpressivePlacementFeedback && IsImpressivePlacement(shape, offsets);

        var childBlocks = new System.Collections.Generic.Dictionary<Vector2Int, Transform>(offsets.Length);
        int childCount = shape.transform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            var child = shape.transform.GetChild(i);
            if (child == null)
                continue;

            if (child.GetComponent<Collider2D>() == null)
                continue;

            Vector3 lp = child.localPosition;
            int x = Mathf.RoundToInt(lp.x / board.cellSize);
            int y = Mathf.RoundToInt(lp.y / board.cellSize);
            var key = new Vector2Int(x, y);
            if (!childBlocks.ContainsKey(key))
                childBlocks.Add(key, child);
        }

        var placedCells = new List<Vector2Int>(offsets.Length);
        int blockIndex = 0;

        foreach (var offset in offsets)
        {
            Vector2Int cell = targetCell + offset;
            board.SetOccupied(cell, true);
            placedCells.Add(cell);

            if (childBlocks.TryGetValue(offset, out var block) && block != null)
            {
                block.SetParent(board.transform, true);

                var blockRenderer = block.GetComponent<SpriteRenderer>();
                if (blockRenderer != null)
                    blockRenderer.sortingOrder = PlacedBlockSortingOrder;

                Vector3 targetPosition = board.GridToWorld(cell);

                if (animateBlockEntry)
                {
                    AnimateBlockEntry(block, targetPosition, blockIndex * blockEntryDelayPerBlock);
                }
                else
                {
                    block.position = targetPosition;
                }

                if (isImpressivePlacement)
                {
                    float entryDelay = animateBlockEntry ? blockEntryDuration : 0f;
                    AnimateImpressiveBlock(block, entryDelay + blockIndex * impressiveBlockDelayPerBlock);
                }

                board.SetPlacedBlock(cell, block.gameObject);

                // Mark as placed so ThemeBlock uses squareColor
                var themeBlock = block.GetComponent<ThemeBlock>();
                if (themeBlock != null)
                    themeBlock.MarkAsPlaced();

                // If the block has a predefined symbol, register it on the grid
                var blockSymbol = block.GetComponent<BlockSymbol>();
                if (blockSymbol != null && blockSymbol.HasSymbol)
                {
                    var icon = blockSymbol.DetachIcon();
                    if (icon != null)
                        icon.transform.SetParent(board.transform, true);
                    board.SetSymbol(cell, blockSymbol.SymbolType, icon);
                }
                
                blockIndex++;
            }
        }

        // Layered on top of whatever placement/line-clear sound plays below, same as
        // PlayLineClearPossible elsewhere in this codebase — a distinct "well done" cue,
        // not a replacement for the normal placement feedback.
        if (isImpressivePlacement && SoundManager.instance != null)
            SoundManager.instance.PlayImpressivePlacement();

        // Fire before line clearing so Adventure symbols can be assigned to just-placed blocks
        OnBlocksPlacedBeforeClear?.Invoke(placedCells);

        var scoreManager = ScoreManager.instance;

        LineClearResult clearResult;

        bool isClassicMode = AppManager.instance != null &&
                             AppManager.instance.CurrentGameMode == AppManager.GameMode.Classic;

        if (scoreManager != null && isClassicMode)
        {
            scoreManager.AddScore(offsets.Length * scorePerPlacedCell);
            clearResult = board.ClearFullLinesDetailed();
            if (clearResult.CellsCleared > 0)
                scoreManager.AddScore(clearResult.CellsCleared * scorePerClearedCell);
        }
        else
        {
            clearResult = board.ClearFullLinesDetailed();
        }

        if (clearResult.LinesCleared > 0)
        {
            if (debugLogs)
            {
               // Debug.Log($"[GridPlacer] Lines cleared: lines={clearResult.LinesCleared} (rows={clearResult.RowsCleared}, cols={clearResult.ColumnsCleared}), cells={clearResult.CellsCleared}");
            }

            bool boardCleared = board.IsBoardEmpty();

            // Play only one sound for this placement: the full-board-clear sting takes
            // over instead of stacking on top of the regular line-clear sound. The
            // combo-tier stinger is NOT played here — ComboController/ComboManager
            // already fire it (via ComboUIBridge) off this same OnLinesCleared event
            // below, with its own streak-based tier count. Calling PlayCombo() here
            // too meant two combo clips (often different tiers) played on top of each
            // other on every 2nd+ clear in a streak.
            if (SoundManager.instance != null)
            {
                if (boardCleared)
                    SoundManager.instance.PlayBoardCleared();
                else
                    SoundManager.instance.PlayLineClear();
            }

            if (boardCleared && ThemeManager.instance != null)
                ThemeManager.instance.TriggerBoardCleared();

            OnLinesCleared?.Invoke(clearResult);
        }
        else
        {
            if (debugLogs)
            {
              //  Debug.Log("[GridPlacer] No lines cleared -> breaking combo");
            }
            OnNoLinesCleared?.Invoke();
        }

        // אחרי שהלוח עודכן וקווים נמחקו, נודיע על הצבת הצורה.
        OnShapePlaced?.Invoke(shape);

        // כעת אפשר להשמיד את אובייקט הצורה המקורי
        Destroy(shape.gameObject);

        return clearResult.LinesCleared > 0;
    }

    // A shape counts as an impressive placement when it's bigger than a trivial 1-2 cell
    // piece AND either arrived as a gift combo (curated to fill a gap the player couldn't
    // have set up on their own) or was a genuine tight fit — very few other spots on the
    // board would have accepted it. CountValidPlacements is checked against the board as
    // it stood before this placement, so callers must call this before any occupancy
    // mutation.
    private bool IsImpressivePlacement(Shape shape, Vector2Int[] offsets)
    {
        if (offsets == null || offsets.Length < impressivePlacementMinCells)
            return false;

        if (shape.IsGiftShape)
            return true;

        return board.CountValidPlacements(shape, tightFitMaxValidPlacements) <= tightFitMaxValidPlacements;
    }

    private void AnimateImpressiveBlock(Transform block, float delay)
    {
        if (block == null)
            return;

        block.DOPunchScale(Vector3.one * impressiveBlockPunchScale, impressiveBlockPunchDuration, impressiveBlockPunchVibrato)
            .SetDelay(delay);
    }

    private void AnimateBlockEntry(Transform block, Vector3 targetPosition, float delay)
    {
        // Choose random side to enter from
        Vector3 entryDirection = GetRandomEntryDirection();
        Vector3 startPosition = targetPosition + entryDirection * blockEntryDistance;
        
        block.position = startPosition;
        
        // Animate to target position
        block.DOMove(targetPosition, blockEntryDuration)
            .SetDelay(delay)
            .SetEase(blockEntryEase);
    }

    private Vector3 GetRandomEntryDirection()
    {
        // Randomly choose one of 4 sides (left, right, top, bottom)
        int side = UnityEngine.Random.Range(0, 4);
        
        switch (side)
        {
            case 0: return Vector3.left;   // From left
            case 1: return Vector3.right;  // From right
            case 2: return Vector3.up;     // From top
            case 3: return Vector3.down;   // From bottom
            default: return Vector3.left;
        }
    }
}
