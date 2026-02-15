using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Modesto.UniToDo
{
    /// <summary>
    /// Partial class containing all the methods used to draw the main window sections.
    /// </summary>
    public partial class ToDoListEditorWindow : EditorWindow
    {
        private void DrawTopBar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Edit Settings", EditorStyles.toolbarButton))
                OpenEditConfigWindow();

            var defaultColor = GUI.backgroundColor;
            if (_bulkEdit)
                GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f);
            if (GUILayout.Button("Bulk Edit", EditorStyles.toolbarButton))
                ToggleBulkEdit();
            GUI.backgroundColor = defaultColor;
            GUILayout.EndHorizontal();

            if (_bulkEdit)
                DrawBulkEditMenu();
        }

        private void DrawBulkEditMenu()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Delete", EditorStyles.toolbarButton))
                BulkDelete();
            if (GUILayout.Button("Set Status", EditorStyles.toolbarDropDown))
            {
                GenericMenu orderMenu = new GenericMenu();
                orderMenu.AddItem(new GUIContent("ToDo"), false, () => BulkSetStatus(TaskStatus.ToDo));
                orderMenu.AddItem(new GUIContent("In Approval"), false, () => BulkSetStatus(TaskStatus.InApproval));
                orderMenu.AddItem(new GUIContent("Done"), false, () => BulkSetStatus(TaskStatus.Done));
                orderMenu.DropDown(new Rect(position.width - 300, 24, 0, 16));
            }

            if (_configuration.usePriorities && _configuration.PrioritiesCount > 0)
            {
                if (GUILayout.Button("Set Priority", EditorStyles.toolbarDropDown))
                {
                    GenericMenu priorityMenu = new GenericMenu();

                    for (int i = 0; i < _configuration.PrioritiesCount; i++)
                    {
                        TaskPriority priority = _configuration.settings.priorities[i];
                        priorityMenu.AddItem(new GUIContent(priority.name), false, () => BulkSetPriority(priority));
                        Debug.Log("Added item with index " + i);
                    }

                    priorityMenu.DropDown(new Rect(position.width - 20, 24, 0, 16));
                }
            }

            GUILayout.EndHorizontal();
        }

        private void DrawSearchBar()
        {
            EditorGUILayout.LabelField("Search by name/category", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();

            _searchbarFilter = EditorGUILayout.TextField(_searchbarFilter);

            if (GUILayout.Button(new GUIContent(_searchIcon), GUILayout.Width(30), GUILayout.Height(20)))
            { }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAddQuickTask()
        {
            EditorGUILayout.LabelField("Quick Add Task", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();

            _addTaskValue = EditorGUILayout.TextField(_addTaskValue);

            if (GUILayout.Button("+", GUILayout.Width(30)))
            {
                QuickAddTask(_addTaskValue);
                GUI.FocusControl(null);
                _addTaskValue = string.Empty;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFooter()
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add Task"))
                OpenEditTaskWindow(this, new Task("new task"));
            if (GUILayout.Button("Revert Changes"))
            {
                if (EditorUtility.DisplayDialog("Revert changes", "Revert changes will lose al changes not saved are you sure?", "Revert", "Back") && _unsavedChanges)
                    ReadConfig(true);
            }

            if (_unsavedChanges)
            {
                if (GUILayout.Button("Apply changes", EditorStyleUtils.ButtonStyle(null, FontStyle.Bold, 0, Color.red)))
                    SaveConfig();
            }
            else
            {
                if (GUILayout.Button("Apply changes"))
                    Debug.Log("UniToDo is up to date");
            }
        }

        private void DrawTaskList()
        {
            EditorGUI.BeginChangeCheck();
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("UniToDo - Tasks", EditorStyleUtils.LabelStyle(null, FontStyle.Bold, 14, null));
            EditorGUILayout.Space();

            if (_configuration.tasks.Count > 0)
                DrawTasks();

            if (EditorGUI.EndChangeCheck())
                _unsavedChanges = true;

            DrawAddQuickTask();

            EditorGUILayout.EndScrollView();
        }

        //the window shown when no config file is found
        private void DrawNoConfigWindow()
        {
            GUILayout.BeginArea(new Rect(0, 0, position.width, position.height));
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical();

            float xPosition = (float)position.width / 2 - 128 / 2;
            float yPosition = (float)position.height / 2 - 128 - 40;
            GUI.DrawTexture(new Rect(xPosition, yPosition, 128, 128), _notFoundTexture);
            GUILayout.Label("Configuration not found.", EditorStyleUtils.LabelStyle(TextAnchor.MiddleCenter));
            GUILayout.Label("Create a configuration to start using UniToDo!", EditorStyleUtils.LabelStyle(TextAnchor.MiddleCenter, FontStyle.Bold));
            EditorGUILayout.Space();
            if (GUILayout.Button("Create Configuration"))
                OpenEditConfigWindow();

            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
        }

        private void DrawTasks()
        {
            DrawToDoTask();
            EditorGUILayout.Space();
            DrawInApprovalTask();
            EditorGUILayout.Space();
            DrawDoneTask();
            EditorGUILayout.Space();
        }
        
        private void DrawToDoTask()
        {
            if (_configuration.tasks.Count > 0 && _configuration.tasks.Any(t => t.status == TaskStatus.ToDo))
            {
                EditorGUILayout.BeginVertical("Box");

                if (_bulkEdit)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("To Do", EditorStyleUtils.LabelStyle(null, FontStyle.Bold, 0, _configuration.toDoLabelColor));

                    if (GUILayout.Button("Toggle", EditorStyles.miniButton, GUILayout.Width(50)))
                        ToggleSelection(_configuration.tasks.Where(t => t.status == TaskStatus.ToDo).ToList());

                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.LabelField("To Do", EditorStyleUtils.LabelStyle(null, FontStyle.Bold, 0, _configuration.toDoLabelColor));
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Task", EditorStyleUtils.LabelStyle(TextAnchor.MiddleLeft, null, 10));

                if (_configuration.usePriorities && _configuration.PrioritiesCount > 0)
                    EditorGUILayout.LabelField("Priority", EditorStyleUtils.LabelStyle(TextAnchor.MiddleCenter, null, 10), GUILayout.Width(40));

                EditorGUILayout.LabelField("Edit", EditorStyleUtils.LabelStyle(TextAnchor.MiddleCenter, null, 10), GUILayout.Width(40));
                EditorGUILayout.LabelField("Delete", EditorStyleUtils.LabelStyle(TextAnchor.MiddleCenter, null, 10), GUILayout.Width(40));
                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < _configuration.tasks.Count; i++)
                {
                    if (_configuration.tasks[i].status == TaskStatus.ToDo &&
                        (IsInSearchFilter(_configuration.tasks[i].title, _configuration.tasks[i].category)))
                    {
                        EditorGUILayout.BeginHorizontal();

                        if (_bulkEdit)
                            _configuration.tasks[i].selected = EditorGUILayout.Toggle(_configuration.tasks[i].selected, GUILayout.Width(20));

                        if (GUILayout.Button(new GUIContent(_todoTaskButtonIcon), GUILayout.Width(30)))
                        {
                            _configuration.tasks[i].status = TaskStatus.Done;
                            _unsavedChanges = true;
                            GUIUtility.ExitGUI();
                        }

                        _configuration.tasks[i].title = EditorGUILayout.TextField(_configuration.tasks[i].title);

                        if (_configuration.usePriorities)
                        {
                            if (_configuration.tasks[i].priority != null && !string.IsNullOrEmpty(_configuration.tasks[i].priority.name))
                            {
                                var defaultColor = GUI.backgroundColor;
                                GUI.backgroundColor = _configuration.tasks[i].priority.color;
                                if (GUILayout.Button(" ",
                                    EditorStyleUtils.ButtonStyle(null, null, 0, _configuration.tasks[i].priority.color),
                                    GUILayout.Width(40)))
                                    OpenEditPriorityWindow(_configuration.settings.priorities, _configuration.tasks[i]);
                                GUI.backgroundColor = defaultColor;
                            }
                            else
                            {
                                if (GUILayout.Button(new GUIContent(_noCategoryIcon), GUILayout.Width(40), GUILayout.Height(20)))
                                    OpenEditPriorityWindow(_configuration.settings.priorities, _configuration.tasks[i]);
                            }
                        }

                        if (GUILayout.Button(new GUIContent(_kebabMenuIcon), GUILayout.Width(40)))
                            OpenEditTaskWindow(this, _configuration.tasks[i]);

                        if (GUILayout.Button(new GUIContent(_trashbinIcon), GUILayout.Width(40)))
                        {
                            _configuration.RemoveTask(i);
                            _unsavedChanges = true;
                            GUIUtility.ExitGUI();
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                }
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawInApprovalTask()
        {
            if (_configuration.tasks.Any(t => t.status == TaskStatus.InApproval) && _configuration.showInApprovalTasks)
            {
                EditorGUILayout.BeginVertical("Box");

                if (_bulkEdit)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("In Approval", EditorStyleUtils.LabelStyle(null, FontStyle.Bold, 0, _configuration.inApprovalLabelColor));

                    if (GUILayout.Button("Toggle", EditorStyles.miniButton, GUILayout.Width(50)))
                        ToggleSelection(_configuration.tasks.Where(t => t.status == TaskStatus.InApproval).ToList());

                    EditorGUILayout.EndHorizontal();
                }
                else
                    EditorGUILayout.LabelField("In Approval", EditorStyleUtils.LabelStyle(null, FontStyle.Bold, 0, _configuration.inApprovalLabelColor));

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Task", EditorStyleUtils.LabelStyle(TextAnchor.MiddleLeft, null, 10));

                EditorGUILayout.LabelField("Edit", EditorStyleUtils.LabelStyle(TextAnchor.MiddleCenter, null, 10), GUILayout.Width(40));
                EditorGUILayout.LabelField("Delete", EditorStyleUtils.LabelStyle(TextAnchor.MiddleCenter, null, 10), GUILayout.Width(40));
                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < _configuration.tasks.Count; i++)
                {
                    if (_configuration.tasks[i].status == TaskStatus.InApproval &&
                        (IsInSearchFilter(_configuration.tasks[i].title, _configuration.tasks[i].category)))
                    {
                        EditorGUILayout.BeginHorizontal();

                        if (_bulkEdit)
                            _configuration.tasks[i].selected = EditorGUILayout.Toggle(_configuration.tasks[i].selected, GUILayout.Width(20));

                        if (GUILayout.Button(new GUIContent(_inApprovalTaskButtonIcon), GUILayout.Width(30)))
                        {
                            _configuration.tasks[i].status = TaskStatus.Done;
                            _unsavedChanges = true;
                            GUIUtility.ExitGUI();
                        }

                        _configuration.tasks[i].title = EditorGUILayout.TextField(_configuration.tasks[i].title);

                        if (_configuration.usePriorities)
                        {
                            if (_configuration.tasks[i].priority != null && !string.IsNullOrEmpty(_configuration.tasks[i].priority.name))
                            {
                                var defaultColor = GUI.backgroundColor;
                                GUI.backgroundColor = _configuration.tasks[i].priority.color;
                                if (GUILayout.Button(" ",
                                    EditorStyleUtils.ButtonStyle(null, null, 0, _configuration.tasks[i].priority.color),
                                    GUILayout.Width(40)))
                                    OpenEditPriorityWindow(_configuration.settings.priorities, _configuration.tasks[i]);
                                GUI.backgroundColor = defaultColor;
                            }
                            else
                            {
                                if (GUILayout.Button(new GUIContent(_noCategoryIcon), GUILayout.Width(40), GUILayout.Height(20)))
                                    OpenEditPriorityWindow(_configuration.settings.priorities, _configuration.tasks[i]);
                            }
                        }

                        if (GUILayout.Button(new GUIContent(_kebabMenuIcon), GUILayout.Width(40)))
                            OpenEditTaskWindow(this, _configuration.tasks[i]);

                        if (GUILayout.Button(new GUIContent(_trashbinIcon), GUILayout.Width(40)))
                        {
                            _configuration.RemoveTask(i);
                            GUIUtility.ExitGUI();
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                }
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawDoneTask()
        {
            if (_configuration.tasks.Count > 0 && _configuration.showDoneTasks && _configuration.tasks.Any(t => t.status == TaskStatus.Done))
            {
                EditorGUILayout.BeginVertical("Box");

                if (_bulkEdit)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Done", EditorStyleUtils.LabelStyle(null, FontStyle.Bold, 0, _configuration.doneLabelColor));

                    if (GUILayout.Button("Toggle", EditorStyles.miniButton, GUILayout.Width(50)))
                        ToggleSelection(_configuration.tasks.Where(t => t.status == TaskStatus.Done).ToList());

                    EditorGUILayout.EndHorizontal();
                }
                else
                    EditorGUILayout.LabelField("Done", EditorStyleUtils.LabelStyle(null, FontStyle.Bold, 0, _configuration.doneLabelColor));

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Task", EditorStyleUtils.LabelStyle(TextAnchor.MiddleLeft, null, 10));

                EditorGUILayout.LabelField("Edit", EditorStyleUtils.LabelStyle(TextAnchor.MiddleCenter, null, 10), GUILayout.Width(40));
                EditorGUILayout.LabelField("Delete", EditorStyleUtils.LabelStyle(TextAnchor.MiddleCenter, null, 10), GUILayout.Width(40));
                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < _configuration.tasks.Count; i++)
                {
                    if (_configuration.tasks[i].status == TaskStatus.Done &&
                        (IsInSearchFilter(_configuration.tasks[i].title, _configuration.tasks[i].category)))
                    {
                        EditorGUILayout.BeginHorizontal();

                        if (_bulkEdit)
                            _configuration.tasks[i].selected = EditorGUILayout.Toggle(_configuration.tasks[i].selected, GUILayout.Width(20));

                        if (GUILayout.Button(new GUIContent(_doneTaskButtonIcon), GUILayout.Width(30)))
                        {
                            _configuration.tasks[i].status = TaskStatus.ToDo;
                            _unsavedChanges = true;
                            GUIUtility.ExitGUI();
                        }

                        _configuration.tasks[i].title = EditorGUILayout.TextField(_configuration.tasks[i].title);

                        if (GUILayout.Button(new GUIContent(_kebabMenuIcon), GUILayout.Width(40)))
                            OpenEditTaskWindow(this, _configuration.tasks[i]);

                        if (GUILayout.Button(new GUIContent(_trashbinIcon), GUILayout.Width(40)))
                        {
                            _configuration.RemoveTask(i);
                            GUIUtility.ExitGUI();
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                }

                EditorGUILayout.EndVertical();
            }
        }
    }
}