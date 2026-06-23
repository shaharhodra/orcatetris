using UnityEngine;
using DG.Tweening;
using System.Collections;
using TMPro;

public class LobbyLevelManager : MonoBehaviour
{
    [SerializeField] private LobbyLevelCell[] levelCells;
    [SerializeField] private RectTransform pointer;

    [Header("Level Unlock Animation")]
    [SerializeField] private float punchScale = 0.25f;
    [SerializeField] private float punchDuration = 0.4f;
    [SerializeField] private float coverAnimDelay = 0.12f;
    [SerializeField] private TextMeshProUGUI currentLevelLabel;

    private void Start()
    {
        GiftPopupController.OnGiftClosed += RefreshAfterGift;
        StartCoroutine(Init());
    }

    private void OnDisable()
    {
        if (pointer != null)
            pointer.DOKill();
        GiftPopupController.OnGiftClosed -= RefreshAfterGift;
    }

    private void RefreshAfterGift()
    {
        StartCoroutine(Init(animate: true));
    }

    private IEnumerator Init(bool animate = false)
    {
        if (levelCells == null || levelCells.Length == 0)
            yield break;

        while (PlayerManeger.instance == null)
            yield return null;

        int highest = PlayerManeger.instance.PlayerProgress.HighestUnlockedLevel;

        // If no bonus was granted today, keep DisplayLevel in sync with real level
        var prog = PlayerManeger.instance.PlayerProgress;
        if (!PlayerManeger.instance.IsDailyBonusAvailable() && prog.DisplayLevel < highest)
            prog.DisplayLevel = highest;

        int display = prog.DisplayLevel;

        if (currentLevelLabel != null)
            currentLevelLabel.text = $"Level {display}";

        LobbyLevelCell currentCell = null; // exact match: the level the player is about to play
        int coverAnimIndex = 0;

        for (int i = 0; i < levelCells.Length; i++)
        {
            var cell = levelCells[i];
            if (cell == null)
                continue;

            bool isUnlocked = cell.LevelIndex <= highest;
            bool isCompleted = cell.LevelIndex < highest;

            cell.SetState(isUnlocked, isCompleted);

            if (isCompleted)
            {
                if (animate)
                {
                    // Triggered by gift popup — fly cover in with stagger
                    cell.ResetCover();
                    cell.AnimateCoverIn(delay: coverAnimIndex * coverAnimDelay);
                    coverAnimIndex++;
                }
                else
                {
                    // First load — place cover instantly, no animation
                    cell.PlaceCoverInstant();
                }
            }

            // The current level is exactly HighestUnlockedLevel — this is where the pointer goes
            if (cell.LevelIndex == highest)
                currentCell = cell;
        }

        if (currentCell != null)
        {
            var target = currentCell.GetComponent<RectTransform>();

            // Punch the current level cell after covers land
            if (animate && target != null)
            {
                float totalCoverDelay = coverAnimIndex * coverAnimDelay + 0.5f;
                DOVirtual.DelayedCall(totalCoverDelay, () =>
                {
                    target.DOKill(false);
                    target.DOPunchScale(Vector3.one * punchScale, punchDuration, 5, 0.5f);
                });
            }

            if (pointer != null && target != null)
            {
                pointer.SetParent(target, worldPositionStays: false);
                pointer.anchoredPosition = Vector2.zero;
                pointer.localScale = Vector3.one;

                pointer.DOKill();
                pointer.DOScale(1.1f, 0.5f)
                       .SetLoops(-1, LoopType.Yoyo)
                       .SetEase(Ease.InOutSine);
            }
        }
    }
}
