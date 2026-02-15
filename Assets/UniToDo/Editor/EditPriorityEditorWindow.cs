using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Modesto.UniToDo
{
    /// <summary>
    /// A small window for change a task's priority. It opens when clicking on a task priority button. 
    /// </summary>
    public class EditPriorityEditorWindow : EditorWindow
    {
        private ToDoListEditorWindow _mainWindow;
        private List<TaskPriority> _priorities;
        private Task _task;
        private int _selectedPriority;
        private string[] _prioritiesNames;
        public void Init(ToDoListEditorWindow mainWindow, List<TaskPriority> priorities, Task task, int index)
        {
            EditPriorityEditorWindow window = (EditPriorityEditorWindow)EditorWindow.GetWindow(typeof(EditPriorityEditorWindow));
            _mainWindow = mainWindow;
            _priorities = priorities;
            _task = task;
            _selectedPriority = index;

            _prioritiesNames = new string[priorities.Count + 1];

            for (int i = 0; i < priorities.Count + 1; i++)
            {
                if (i > 0)
                    _prioritiesNames[i] = priorities[i - 1].name;
                else
                    _prioritiesNames[i] = "No Priority";
            }

            window.Show();
        }

        private void OnEnable()
        {
            var icon = EditorGUIUtility.Load("Installed") as Texture;
            maxSize = new Vector2(600, 200);
            minSize = new Vector2(500, 200);
            titleContent = new GUIContent("UniToDo - Edit Priority", icon);
        }

        private void OnGUI()
        {
            if (_task != null && _priorities != null)
            {
                EditorGUILayout.LabelField("Edit Priority", EditorStyles.boldLabel);
                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical("Box");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Task", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Priority");
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(_task.title);
                float defaultLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 0;
                _selectedPriority = EditorGUILayout.Popup("", _selectedPriority, _prioritiesNames);
                EditorGUIUtility.labelWidth = defaultLabelWidth;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Back"))
                    Close();

                if (GUILayout.Button("Save"))
                {
                    _mainWindow.EditTaskPriority(_task, _selectedPriority);
                    Close();
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
        }
    }
}
