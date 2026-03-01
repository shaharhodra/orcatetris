using UnityEngine;

public class PopUpGameManager : MonoBehaviour
{
    private PopUpService[] popUpServices;

    private void Awake()
    {
        popUpServices = FindObjectsOfType<PopUpService>();
    }

    private void Start()
    {
        // טריגר בתחילת המשחק לכל הפופאפים המתאימים
        TriggerPopUps(PopUpCondition.OnGameStart);
    }

    private void TriggerPopUps(PopUpCondition condition)
    {
        if (popUpServices == null || popUpServices.Length == 0)
        {
            return;
        }

        foreach (var service in popUpServices)
        {
            if (service != null)
            {
                service.RunIfConditionMet(condition);
            }
        }
    }

    // דוגמאות נוספות לאירועים, אם תרצה להשתמש בהמשך
    public void OnPlayerWin()
    {
        TriggerPopUps(PopUpCondition.OnWin);
    }

    public void OnPlayerLose()
    {
        TriggerPopUps(PopUpCondition.OnLose);
    }
}
