
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
	public int ScorePerCoinThreshold;
	public int CoinsPerThreshold;
	public List<LevelTargetData> LevelTargets;

	//public List<ShapeData> Shapes;
}

[Serializable]
public class ColorLevels
{
	public int NumberOfCombos;
	public Color Color;
}

public enum ColectionTypes
{
	Circles = 0,
	Squares = 1,	
	Stars = 2,
	Triangles = 3,

}

public enum GameTypes
{
	Classic = 0,
	Adventure = 1,
}

[Serializable]
public class LevelTargetData
{
	
	public int Target;
	public Color Color;
	public ColectionTypes TargetItem;

}


