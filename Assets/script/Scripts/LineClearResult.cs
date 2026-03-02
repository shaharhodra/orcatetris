using System;

[Serializable]
public readonly struct LineClearResult
{
    public int RowsCleared { get; }
    public int ColumnsCleared { get; }
    public int CellsCleared { get; }

    public int LinesCleared => RowsCleared + ColumnsCleared;

    public LineClearResult(int rowsCleared, int columnsCleared, int cellsCleared)
    {
        RowsCleared = rowsCleared;
        ColumnsCleared = columnsCleared;
        CellsCleared = cellsCleared;
    }
}
