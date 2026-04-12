using System;
using System.Collections.Generic;

[Serializable]
public struct LineClearResult
{
    public int RowsCleared { get; }
    public int ColumnsCleared { get; }
    public int CellsCleared { get; }

    public int LinesCleared => RowsCleared + ColumnsCleared;

    // Adventure mode: symbols cleared per ColectionTypes
    public Dictionary<ColectionTypes, int> ClearedSymbols { get; }

    public LineClearResult(int rowsCleared, int columnsCleared, int cellsCleared, Dictionary<ColectionTypes, int> clearedSymbols = null)
    {
        RowsCleared = rowsCleared;
        ColumnsCleared = columnsCleared;
        CellsCleared = cellsCleared;
        ClearedSymbols = clearedSymbols;
    }
}
