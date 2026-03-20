using System;
using System.Collections;
using UnityEngine;

public sealed class ComboManager
{
    public event Action<ComboEventArgs> OnComboStep;
    public event Action OnComboEnded;

    public int ComboCount { get; private set; }
    public int TotalLinesClearedInCombo { get; private set; }
    public bool IsActive => ComboCount > 0;
    public bool IsWaitingForCombo => !IsActive && comboTimerCoroutine != null; // ממתין לשורה שנייה
    public float TimeSinceLastClear { get; private set; }

    private const float COMBO_TIME_WINDOW = 5f; // 5 שניות לקומבו
    private MonoBehaviour coroutineRunner;
    private Coroutine comboTimerCoroutine;

    public ComboManager(MonoBehaviour runner)
    {
        coroutineRunner = runner;
    }

    public void RegisterClear(int linesClearedThisStep)
    {
        if (linesClearedThisStep <= 0)
            return;

        // אם אין טיימר פעיל, התחל טיימר (שורה ראשונה)
        if (comboTimerCoroutine == null)
        {
            StartComboTimer();
            TimeSinceLastClear = 0f;
            return; // לא להפעיל קומבו עדיין
        }

        // אם יש טיימר פעיל ועדיין בטווח הזמן, השורה השנייה מפעילה קומבו
        if (TimeSinceLastClear < COMBO_TIME_WINDOW)
        {
            TotalLinesClearedInCombo += linesClearedThisStep;
            TimeSinceLastClear = 0f;

            // עצור את הטיימר הישן והתחל חדש
            StopComboTimer();
            comboTimerCoroutine = coroutineRunner.StartCoroutine(ComboTimerCoroutine());

            // חישוב קומבו (מתחיל מ-1 כי זו השורה השנייה)
            ComboCount = TotalLinesClearedInCombo - 1; // שורה שנייה = קומבו 1
            var tier = GetTier(ComboCount);
            OnComboStep?.Invoke(new ComboEventArgs(ComboCount, linesClearedThisStep, TotalLinesClearedInCombo, tier));
        }
        else
        {
            // אם עבר הזמן, התחל מחדש
            ResetCombo();
            StartComboTimer();
            TimeSinceLastClear = 0f;
        }
    }

    private void StartComboTimer()
    {
        StopComboTimer();
        comboTimerCoroutine = coroutineRunner.StartCoroutine(ComboTimerCoroutine());
    }

    private IEnumerator ComboTimerCoroutine()
    {
        TimeSinceLastClear = 0f;
        
        while (TimeSinceLastClear < COMBO_TIME_WINDOW)
        {
            TimeSinceLastClear += Time.deltaTime;
            yield return null;
        }

        // אם הגענו לפה, עברו 5 שניות בלי ניקוי - לשבור את הקומבו
        BreakCombo();
    }

    private void StopComboTimer()
    {
        if (comboTimerCoroutine != null && coroutineRunner != null)
        {
            coroutineRunner.StopCoroutine(comboTimerCoroutine);
            comboTimerCoroutine = null;
        }
    }

    public void BreakCombo()
    {
        StopComboTimer();
        
        if (ComboCount > 0)
            OnComboEnded?.Invoke();

        ResetCombo();
    }

    private void ResetCombo()
    {
        ComboCount = 0;
        TotalLinesClearedInCombo = 0;
        TimeSinceLastClear = 0f;
        StopComboTimer(); // עצור את הטיימר
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
