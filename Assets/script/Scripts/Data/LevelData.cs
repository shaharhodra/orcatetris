
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LevelData
{
	public GameTypes GameType;
	public string LevelName;
	public int Level;
	public int GridRows;
	public int GridColumns;
	public int NumberOfShapes;
	public int DifficultyLevel;
	public int DifficultyThreshold;
	public int ScorePerPlaceCell;
	public int ScorePerClearCell;
	public List<ColorLevels> ColorLevels;
	//public List<ShapeData> Shapes;
}

[Serializable]
public class ColorLevels
{
	public int NumberOfCombos;
	public Color Color;
}

public enum GameTypes
{
	Classic = 0,
	Adventure = 1,
}
