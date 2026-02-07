using UnityEngine;

public class PopUpConditionButton : MonoBehaviour
{
    [Header("למי לשלוח את הטריגר")]
    [SerializeField] private PopUpService targetService;

    [Header("איזה מצב לשלוח")]
    [SerializeField] private PopUpCondition conditionToSend = PopUpCondition.OnGameStart;

    // את הפונקציה הזו תחבר לכפתור ב-Inspector
    public void OnButtonClicked()
    {
        if (targetService == null)
        {
            Debug.LogWarning("PopUpConditionButton: targetService is not assigned.");
            return;
        }

        // שולח את המצב שהוגדר באינספקטור
        targetService.RunIfConditionMet(conditionToSend);
    }
}
