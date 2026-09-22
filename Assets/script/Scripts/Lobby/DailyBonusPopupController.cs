using UnityEngine;
using TMPro;

// Attach to the DailyBonusPopup GameObject (main menu scene), alongside PopUpService.
// Opened by the "daily" lobby button. Once per day it checks the player in and shows their
// current consecutive-day streak; every 5th consecutive day also grants a +2 level-skip
// bonus via PlayerManeger.CheckInDailyLoginStreak.
public class DailyBonusPopupController : MonoBehaviour
{
    [SerializeField] private PopUpService popUpService;
    [SerializeField] private TextMeshProUGUI rewardText;

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

        if (rewardText != null)
        {
            rewardText.text = streakBonusGranted
                ? $"Day {streak} in a row!\n+2 Levels Bonus!"
                : $"Day {streak} in a row!\nEvery 5th day gives +2 levels!";
        }

        popUpService.ShowAndStay();
    }

    public void OnCloseClicked()
    {
        popUpService.OnXButtonClicked();
    }
}
