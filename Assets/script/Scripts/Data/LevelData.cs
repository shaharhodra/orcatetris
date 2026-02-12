using System;
using System.Collections.Generic;

[Serializable]
public class LevelData
{
    public int Level;
    public int TimeInSeconds;
    public int GridRows;
    public int GridColumns;
    public int NumberOfShapes;
    public string LevelName;
    public List<ShapeData> Shapes;
}
