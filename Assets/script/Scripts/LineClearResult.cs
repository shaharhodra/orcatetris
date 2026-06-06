using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ClearedSymbolVisual
{
    public ColectionTypes Type;
    public Vector3 WorldPosition;
    public GameObject IconObject;

    public ClearedSymbolVisual(ColectionTypes type, Vector3 worldPosition, GameObject iconObject)
    {
        Type = type;
        WorldPosition = worldPosition;
        IconObject = iconObject;
    }
}

[Serializable]
public struct LineClearResult
{
    public int RowsCleared { get; }
    public int ColumnsCleared { get; }
    public int CellsCleared { get; }

    public int LinesCleared => RowsCleared + ColumnsCleared;

    // Adventure mode: symbols cleared per ColectionTypes
    public Dictionary<ColectionTypes, int> ClearedSymbols { get; }
    public List<ClearedSymbolVisual> ClearedSymbolVisuals { get; }

    public LineClearResult(int rowsCleared, int columnsCleared, int cellsCleared, Dictionary<ColectionTypes, int> clearedSymbols = null, List<ClearedSymbolVisual> clearedSymbolVisuals = null)
    {
        RowsCleared = rowsCleared;
        ColumnsCleared = columnsCleared;
        CellsCleared = cellsCleared;
        ClearedSymbols = clearedSymbols;
        ClearedSymbolVisuals = clearedSymbolVisuals;
    }
}
