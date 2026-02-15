using System.IO;
using UnityEditor;
using UnityEngine;

namespace Modesto.UniToDo
{
    /// <summary>
    /// The editor widnow to edit the todolist configuration
    /// </summary>
    public class ConfigurationEditorWindow : EditorWindow
    {
        private Configuration _configuration;
        private ToDoListEditorWindow _mainWindow;
        private Vector2 _scrollPos;

        public void Init(ToDoListEditorWindow mainWindow)
        {
            ConfigurationEditorWindow window = (ConfigurationEditorWindow)EditorWindow.GetWindow(typeof(ConfigurationEditorWindow));
            _mainWindow = mainWindow;
            Show();
        }

        private void OnEnable()
        {
            var icon = EditorGUIUtility.Load("Installed") as Texture;
            minSize = new Vector2(300, 300);
            titleContent = new GUIContent("UniToDo - Configuration", icon);
            _configuration = DataHandler.CreateConfiguration();
        }

        void OnGUI()
        {
            if (_configuration != null)
            {
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

                //draw general settings
                EditorGUILayout.BeginVertical("Box");
                EditorGUILayout.LabelField("Edit Settings", EditorStyles.boldLabel);

                float defaultLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 160;
                _configuration.showInApprovalTasks = EditorGUILayout.Toggle("Show approval tasks",
                    _configuration.showInApprovalTasks, EditorStyles.toggle);
                _configuration.showDoneTasks = EditorGUILayout.Toggle("Show done tasks", _configuration.showDoneTasks, EditorStyles.toggle);
                _configuration.useCategories = EditorGUILayout.Toggle("Use categories", _configuration.useCategories, EditorStyles.toggle);
                _configuration.usePriorities = EditorGUILayout.Toggle("Use priorities", _configuration.usePriorities, EditorStyles.toggle);

                //draw label color settings
                EditorGUILayout.Space();
                _configuration.toDoLabelColor = EditorGUILayout.ColorField("ToDo Label Color", _configuration.toDoLabelColor);
                _configuration.inApprovalLabelColor = EditorGUILayout.ColorField("Approval Label Color", _configuration.inApprovalLabelColor);
                _configuration.doneLabelColor = EditorGUILayout.ColorField("Done Label Color", _configuration.doneLabelColor);
                EditorGUILayout.Space();

                EditorGUIUtility.labelWidth = defaultLabelWidth;

                //draw categories and priorities settings
                if (_configuration.useCategories)
                    DrawCategoriesConfig();
                EditorGUILayout.Space();
                if (_configuration.usePriorities)
                    DrawPrioritiesConfig();
                EditorGUILayout.Space();

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndScrollView();

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Apply Configuration"))
                    SaveConfig();
            }
        }

        private void DrawCategoriesConfig()
        {
            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField("Categories Configuration", EditorStyleUtils.LabelStyle(null, FontStyle.Bold, 13));
            EditorGUILayout.Space();

            float defaultLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 100;
            if (_configuration.CategoriesCount > 0)
            {
                for (int i = 0; i < _configuration.CategoriesCount; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    _configuration.settings.categories[i] = EditorGUILayout.TextField("Category " + (i + 1), _configuration.settings.categories[i]);

                    if (GUILayout.Button(" X ", GUILayout.Width(20)))
                        _configuration.RemoveCategory(i);

                    EditorGUILayout.EndHorizontal();
                }
            }
            else
                EditorGUILayout.LabelField("No categories yet. Start by creating one or more categories.");
            EditorGUILayout.Space();
            EditorGUIUtility.labelWidth = defaultLabelWidth;
            if (GUILayout.Button("Add Category"))
                _configuration.AddCategory();

            EditorGUILayout.EndVertical();
        }

        private void DrawPrioritiesConfig()
        {
            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField("Priorities Configuration", EditorStyleUtils.LabelStyle(null, FontStyle.Bold, 13));
            EditorGUILayout.Space();

            if (_configuration.PrioritiesCount > 0)
            {
                for (int i = 0; i < _configuration.PrioritiesCount; i++)
                {
                    EditorGUILayout.BeginHorizontal();

                    EditorGUIUtility.labelWidth = 70;
                    _configuration.settings.priorities[i].name = EditorGUILayout.TextField("Priority: " + (i + 1), _configuration.settings.priorities[i].name);
                    _configuration.settings.priorities[i].color = EditorGUILayout.ColorField("Color:", _configuration.settings.priorities[i].color);

                    if (GUILayout.Button(" X ", GUILayout.Width(20)))
                        _configuration.RemovePriority(i);

                    EditorGUILayout.EndHorizontal();
                }
            }
            else
                EditorGUILayout.LabelField("No priorities yet. Start by creating one or more priorities.");
            EditorGUILayout.Space();

            if (GUILayout.Button("Add Priority"))
                _configuration.AddPriority();
            EditorGUILayout.EndVertical();
        }

        private void SaveConfig()
        {
            if (_configuration.useCategories && _configuration.CategoriesCount <= 0)
            {
                Debug.LogError("Use categories has been turned off because you didn't add any category. " +
                    "You can enable it any time by changing configuration");
                _configuration.useCategories = false;
            }

            if (_configuration.usePriorities && _configuration.PrioritiesCount <= 0)
            {
                Debug.LogError("Use priorities has been turned off because you didn't add any priority. " +
                    "You can enable it any time by changing configuration");
                _configuration.usePriorities = false;
            }

            DataHandler.SaveConfiguration(_configuration);
            _mainWindow.ReadConfig(true);
            Close();
        }
    }
}