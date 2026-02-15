using UnityEngine;
using UnityEditor;

namespace Modesto.UniToDo
{
    /// <summary>
    /// A window to edit a taks in depth.
    /// </summary>
    public class EditTaskEditorWindow : EditorWindow
    {
        private ToDoListEditorWindow _mainWindow;
        private Task _task;
        private Vector2 _scrollPos;
        private Vector2 _scrollDescriptionPos;
        private Configuration _configuration;
        private int _taskCategory;
        private int _taskPriority;
        private int _taskStatus;
        private string[] _prioritiesNames;
        private string[] _taskStatusNames;
        private string[] _categoriesNames;
        private float _defaultLabelWidth;

        public void Init(ToDoListEditorWindow mainWindow, Task task, Configuration configuration)
        {
            _mainWindow = mainWindow;
            _task = task;
            _configuration = configuration;

            if (_configuration.useCategories)
            {
                //metti a +1 per avere il no category a i = 0
                //in generale ogni volta che ci sono queste bestiate +- 1 il motivo e quello
                _taskCategory = configuration.settings.categories.FindIndex(c => c.Equals(task.category)) + 1;

                _categoriesNames = new string[configuration.settings.categories.Count + 1];

                for (int i = 0; i < configuration.settings.categories.Count + 1; i++)
                {
                    if (i > 0)
                        _categoriesNames[i] = configuration.settings.categories[i - 1];
                    else
                        _categoriesNames[i] = "No Category";
                }
            }

            if (_configuration.usePriorities)
            {
                if (_task.priority != null)
                    _taskPriority = configuration.settings.priorities.FindIndex(p => p.name.Equals(task.priority.name)) + 1;
                else
                    _taskPriority = 0;

                _prioritiesNames = new string[configuration.settings.priorities.Count + 1];

                for (int i = 0; i < configuration.settings.priorities.Count + 1; i++)
                {
                    if (i > 0)
                        _prioritiesNames[i] = configuration.settings.priorities[i - 1].name;
                    else
                        _prioritiesNames[i] = "No Priority";
                }
            }

            _taskStatus = (int)task.status;

            _taskStatusNames = new string[3] { "ToDo", "In Approval", "Done" };
            Show();
        }

        private void OnEnable()
        {
            var icon = EditorGUIUtility.Load("Installed") as Texture;
            minSize = new Vector2(300, 500);
            titleContent = new GUIContent("UniToDo - Edit Task", icon);
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField("Edit Task", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Title", EditorStyles.boldLabel);
            _task.title = EditorGUILayout.TextField(_task.title);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
            _scrollDescriptionPos = EditorGUILayout.BeginScrollView(_scrollDescriptionPos, GUILayout.Height(120));
            _task.description = EditorGUILayout.TextArea(_task.description, GetTextAreaStyle(), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            if (_configuration.useCategories)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Category", EditorStyles.boldLabel);
                _defaultLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 0;
                _taskCategory = EditorGUILayout.Popup("", _taskCategory, _categoriesNames);
                EditorGUIUtility.labelWidth = _defaultLabelWidth;
            }

            if (_configuration.usePriorities)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Priority", EditorStyles.boldLabel);
                _defaultLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 0;
                _taskPriority = EditorGUILayout.Popup("", _taskPriority, _prioritiesNames);
                EditorGUIUtility.labelWidth = _defaultLabelWidth;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            _defaultLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 0;
            _taskStatus = EditorGUILayout.Popup("", _taskStatus, _taskStatusNames);
            EditorGUIUtility.labelWidth = _defaultLabelWidth;

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Update Task"))
                UpdateTask();
        }

        private GUIStyle GetTextAreaStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.textArea);
            style.wordWrap = true;
            return style;
        }

        private void UpdateTask()
        {
            _mainWindow.EditTask(_task, _taskPriority, _taskCategory, _taskStatus);
            Close();
        }
    }
}
