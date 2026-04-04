using System;
using UnityEngine;

public sealed class ComboManager
{
    public event Action<ComboEventArgs> OnComboStep;
    public event Action OnComboEnded;

    public int ComboCount { get; private set; }
    public int TotalLinesClearedInCombo { get; private set; }
    public bool IsActive => ComboCount > 0;

    // כמה ניסיונות הצבת צורה ברצף בלי שנמחקה אף שורה
    public int FailedAttemptsWithoutClear { get; private set; }

    private const int MAX_FAILED_ATTEMPTS = 10;

    public ComboManager(MonoBehaviour runner)
    {
        // currently we don't need the runner; kept for compatibility if we ever reintroduce coroutines
    }

    public void RegisterClear(int linesClearedThisStep)
    {
        if (linesClearedThisStep <= 0)
            return;

        // בכל פעם שנמחקות שורות, מתחילים/ממשיכים קומבו ומאפסים את מונה הניסיונות הכושלים
        FailedAttemptsWithoutClear = 0;

        TotalLinesClearedInCombo += linesClearedThisStep;

        // קומבו מתחיל רק מהשורה השנייה שנמחקה בסדרה
        if (TotalLinesClearedInCombo >= 2)
        {
            ComboCount = TotalLinesClearedInCombo - 1; // שורה שנייה = קומבו 1
            var tier = GetTier(ComboCount);
            OnComboStep?.Invoke(new ComboEventArgs(ComboCount, linesClearedThisStep, TotalLinesClearedInCombo, tier));
        }
    }

    // קריאה כאשר בוצעה הצבת צורה בלי שנמחקה שורה
    public void RegisterFailedAttempt()
    {
        // אם עדיין לא היה שום ניקוי שורה, אין קומבו להתייחס אליו
        if (TotalLinesClearedInCombo <= 0)
            return;

        FailedAttemptsWithoutClear++;

        if (FailedAttemptsWithoutClear >= MAX_FAILED_ATTEMPTS)
        {
            BreakCombo();
        }
    }

    public void BreakCombo()
    {
        if (ComboCount > 0)
            OnComboEnded?.Invoke();

        ResetCombo();
    }

    private void ResetCombo()
    {
        ComboCount = 0;
        TotalLinesClearedInCombo = 0;
        FailedAttemptsWithoutClear = 0;
    }

    private static ComboTier GetTier(int comboCount)
    {
        if (comboCount <= 0)
            return ComboTier.None;

        if (comboCount == 1) return ComboTier.Combo1;
        if (comboCount == 2) return ComboTier.Combo2;
        if (comboCount == 3) return ComboTier.Combo3;
        if (comboCount == 4) return ComboTier.Combo4;
        return ComboTier.Combo5Plus;
    }
}
