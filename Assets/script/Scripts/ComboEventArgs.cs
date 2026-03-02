using System;

public sealed class ComboEventArgs : EventArgs
{
    public int ComboCount { get; }
    public int LinesClearedThisStep { get; }
    public int TotalLinesClearedInCombo { get; }
    public ComboTier Tier { get; }

    public ComboEventArgs(int comboCount, int linesClearedThisStep, int totalLinesClearedInCombo, ComboTier tier)
    {
        ComboCount = comboCount;
        LinesClearedThisStep = linesClearedThisStep;
        TotalLinesClearedInCombo = totalLinesClearedInCombo;
        Tier = tier;
    }
}
