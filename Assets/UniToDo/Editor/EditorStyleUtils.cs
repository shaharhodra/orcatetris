using UnityEditor;
using UnityEngine;

namespace Modesto.UniToDo
{
    /// <summary>
    /// Utils class to customize editor elements
    /// </summary>
    public static class EditorStyleUtils
    {
        public static GUIStyle LabelStyle(TextAnchor? alignment = null, FontStyle? fontStyle = null, int fontSize = 0, Color? fontColor = null)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.alignment = alignment != null ? alignment.Value : GUI.skin.label.alignment;
            style.fontStyle = fontStyle != null ? fontStyle.Value : GUI.skin.label.fontStyle;
            style.fontSize = fontSize != 0 ? fontSize : GUI.skin.label.fontSize;
            style.normal.textColor = fontColor != null ? fontColor.Value : GUI.skin.label.normal.textColor;

            return style;
        }

        public static GUIStyle ButtonStyle(TextAnchor? alignment = null, FontStyle? fontStyle = null, int fontSize = 0, Color? fontColor = null, float height = 0)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.alignment = alignment != null ? alignment.Value : GUI.skin.button.alignment;
            style.fontStyle = fontStyle != null ? fontStyle.Value : GUI.skin.button.fontStyle;
            style.fontSize = fontSize != 0 ? fontSize : GUI.skin.button.fontSize;
            style.normal.textColor = fontColor != null ? fontColor.Value : GUI.skin.button.normal.textColor;
            style.fixedHeight = height == 0 ? GUI.skin.button.fixedHeight : height;
            return style;
        }

        public static GUIStyle FoldoutStyle(Color? fontColor)
        {
            GUIStyle style = new GUIStyle(EditorStyles.foldout);
            style.normal.textColor = fontColor != null ? fontColor.Value : GUI.skin.label.normal.textColor;
            return style;
        }
    }
}
