using System;
using UnityEngine;

public sealed class ComboManager
{
    public event Action<ComboEventArgs> OnComboStep;
    public event Action OnComboEnded;

    public int ComboCount { get; private set; }
    public int TotalLinesClearedInCombo { get; private set; }

    public bool IsActive => ComboCount > 0;

    public void RegisterClear(int linesClearedThisStep)
    {
        if (linesClearedThisStep <= 0)
            return;

        TotalLinesClearedInCombo += linesClearedThisStep;

        int newComboCount = Mathf.Max(0, TotalLinesClearedInCombo - 1);
        if (newComboCount <= 0)
        {
            ComboCount = 0;
            return;
        }

        ComboCount = newComboCount;
        var tier = GetTier(ComboCount);
        OnComboStep?.Invoke(new ComboEventArgs(ComboCount, linesClearedThisStep, TotalLinesClearedInCombo, tier));
    }

    public void BreakCombo()
    {
        if (ComboCount > 0)
            OnComboEnded?.Invoke();

        ComboCount = 0;
        TotalLinesClearedInCombo = 0;
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
