using UnityEngine;

[CreateAssetMenu(fileName = "ThemeData", menuName = "OrcaTetris/Theme Data")]
public class ThemeData : ScriptableObject
{
    [Header("General")]
    public string themeName = "Default";

    [Header("Camera")]
    public Color cameraBackgroundColor = Color.black;

    [Header("Background Sprite")]
    public Sprite backgroundSprite;
    public Color backgroundTint = Color.white;

    [Header("Grid Colors")]
    public Color gridCellEmptyColor = new Color(1f, 1f, 1f, 0.05f);
    public Color gridCellOccupiedColor = Color.white;

    [Header("Block Sprite")]
    public Sprite blockSprite;           // ספרייט לבלוקי הצורות
    public Color blockColor = Color.white;

    [Header("Grid Cell Sprite")]
    public Sprite gridCellSprite;        // ספרייט לתאי הגריד

    [Header("UI Colors")]
    public Color uiPrimaryColor = Color.white;
    public Color uiSecondaryColor = new Color(0.8f, 0.8f, 0.8f, 1f);

    [Header("Transition")]
    [Tooltip("Duration in seconds to lerp between themes")]
    public float transitionDuration = 1.5f;
}
