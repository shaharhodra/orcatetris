using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using TMPro;

// Attach to the DailyBonusPopup GameObject (main menu scene), alongside PopUpService.
// Shows automatically when the menu opens (no lobby button): checks the player in for today,
// shows their current consecutive-day streak for a few seconds, then closes by itself.
// Every 5th consecutive day also grants a +2 level-skip bonus via
// PlayerManeger.CheckInDailyLoginStreak.
public class DailyBonusPopupController : MonoBehaviour
{
    [SerializeField] private PopUpService popUpService;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private float showDelay = 0.5f;
    [SerializeField] private float displaySeconds = 3f;
    [Tooltip("Show only the first time the menu opens in each app session, not every return to the menu.")]
    [SerializeField] private bool showOncePerSession = true;

    private static bool shownThisSession;

    private void Start()
    {
        if (showOncePerSession && shownThisSession)
            return;

        ShowOnMenuOpenAsync().Forget();
    }

    private async UniTaskVoid ShowOnMenuOpenAsync()
    {
        // Let the menu appear (and singletons finish initializing) before popping up.
        await UniTask.Delay(TimeSpan.FromSeconds(showDelay));
        if (this == null)
            return;

        shownThisSession = true;
        Show();
    }

    public void Show()
    {
        int streak = 0;
        bool streakBonusGranted = false;

        if (PlayerManeger.instance != null)
        {
            if (PlayerManeger.instance.IsLoginStreakCheckInPending())
                streakBonusGranted = PlayerManeger.instance.CheckInDailyLoginStreak();

            streak = PlayerManeger.instance.LoginStreak;
        }

        // Daily bonus disabled (Remote Config) — nothing to report.
        if (streak <= 0)
            return;

        if (rewardText != null)
        {
            int daysToBonus = 5 - streak % 5;
            rewardText.text = streakBonusGranted
                ? $"Day {streak} in a row!\n+2 Levels Bonus!"
                : $"Day {streak} in a row!\n{daysToBonus} more day{(daysToBonus == 1 ? "" : "s")} to +2 levels!";
        }

        popUpService.ShowForSeconds(displaySeconds);
    }

    public void OnCloseClicked()
    {
        popUpService.OnXButtonClicked();
    }
}
