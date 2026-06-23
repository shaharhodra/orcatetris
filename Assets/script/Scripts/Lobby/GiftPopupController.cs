using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using System.Collections;

// Attach to the Gift Popup GameObject in the Adventure Lobby scene.
// Shows on lobby entry, grants random level progress, then animates cubes flying to the level button.
public class GiftPopupController : MonoBehaviour
{
    [Header("Popup UI")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private float autoCloseDuration = 3f;

    [Header("Level Skip Settings")]
    [SerializeField] private int minLevelsSkip = 1;
    [SerializeField] private int maxLevelsSkip = 3;

    [Header("Popup Animation")]
    [SerializeField] private float popupScaleDuration = 0.35f;
    [SerializeField] private Ease popupEase = Ease.OutBack;

    public static System.Action OnGiftClosed;

    private int levelsGranted = 0;

    private void Awake()
    {
        if (popupRoot != null)
            popupRoot.transform.localScale = Vector3.zero;
    }

    private void Start()
    {
        Show();
    }

    private void Show()
    {
        // Only grant bonus once per day
        if (PlayerManeger.instance == null || !PlayerManeger.instance.IsDailyBonusAvailable())
        {
            OnGiftClosed?.Invoke();
            return;
        }

        levelsGranted = Random.Range(minLevelsSkip, maxLevelsSkip + 1);

        // Sync DisplayLevel to real level first, then add the visual bonus on top
        var progress = PlayerManeger.instance.PlayerProgress;
        progress.DisplayLevel = progress.HighestUnlockedLevel + levelsGranted;
        PlayerManeger.instance.MarkDailyBonusUsed();

        if (rewardText != null)
            rewardText.text = $"+{levelsGranted}";

        if (popupRoot != null)
        {
            popupRoot.SetActive(true);
            popupRoot.transform
                .DOScale(1f, popupScaleDuration)
                .SetEase(popupEase)
                .OnComplete(() => StartCoroutine(AutoClose()));
        }
    }

    private IEnumerator AutoClose()
    {
        yield return new WaitForSeconds(autoCloseDuration);

        if (popupRoot != null)
        {
            popupRoot.transform
                .DOScale(0f, popupScaleDuration * 0.7f)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    popupRoot.SetActive(false);
                    OnGiftClosed?.Invoke();
                });
        }
        else
        {
            OnGiftClosed?.Invoke();
        }
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}
