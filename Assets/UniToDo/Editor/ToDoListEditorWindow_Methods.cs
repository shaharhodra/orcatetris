using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace Modesto.UniToDo
{
    /// <summary>
    /// Partial class containing all the methods triggered from the main window buttons.
    /// </summary>
    public partial class ToDoListEditorWindow : EditorWindow
    {
        //delete all the selected tasks
        private void BulkDelete()
        {
            for (int i = _configuration.tasks.Count - 1; i >= 0; i--)
            {
                if (_configuration.tasks[i].selected)
                    _configuration.tasks.RemoveAt(i);
            }

            DeselectAll();
        }

        //change priority to all the selected tasks
        private void BulkSetPriority(TaskPriority priority)
        {
            for (int i = 0; i < _configuration.tasks.Count; i++)
            {
                if (_configuration.tasks[i].selected)
                    _configuration.tasks[i].priority = priority;
            }

            DeselectAll();
        }

        //change status to all the selected tasks
        private void BulkSetStatus(TaskStatus status)
        {
            for (int i = 0; i < _configuration.tasks.Count; i++)
            {
                if (_configuration.tasks[i].selected)
                    _configuration.tasks[i].status = status;
            }

            DeselectAll();
        }

        //deselect all tasks
        //used when a bulk operation is performed
        private void DeselectAll()
        {
            for (int i = 0; i < _configuration.tasks.Count; i++)
                _configuration.tasks[i].selected = false;
        }

        // triggered when user close edit or add a task
        public void EditTask(Task task, int taskPriority, int taskCategory, int taskStatus)
        {
            Task taskToEdit = _configuration.tasks.SingleOrDefault(t => t.code == task.code);

            if (taskToEdit != null)
            {
                taskToEdit.title = task.title;
                taskToEdit.description = task.description;
                taskToEdit.status = (TaskStatus)taskStatus;

                //categories and priorities index are shifted to always add a No category/No priority item to the dropdown
                if (_configuration.useCategories)
                {
                    if (taskCategory == 0)
                        taskToEdit.category = string.Empty;
                    else
                        taskToEdit.category = _configuration.settings.categories[taskCategory - 1];
                }

                if (_configuration.usePriorities)
                {
                    if (taskPriority == 0)
                        taskToEdit.priority = null;
                    else
                        taskToEdit.priority = _configuration.settings.priorities[taskPriority - 1];
                }
            }
            else
            {
                if (_configuration.useCategories)
                {
                    if (taskCategory == 0)
                        task.category = string.Empty;
                    else
                        task.category = _configuration.settings.categories[taskCategory - 1];
                }

                if (_configuration.usePriorities)
                {
                    if (taskPriority == 0)
                        task.priority = null;
                    else
                        task.priority = _configuration.settings.priorities[taskPriority - 1];
                }

                _configuration.AddTask(task);
                _unsavedChanges = true;
                AssetDatabase.Refresh();
            }
        }

        //defines if a task title/category is contained in the search bar string
        private bool IsInSearchFilter(string title, string category)
        {
            if (string.IsNullOrEmpty(_searchbarFilter))
                return true;

            if (title.ToLower().Contains(_searchbarFilter.ToLower())
                || !string.IsNullOrEmpty(category) && category.ToLower().Contains(_searchbarFilter.ToLower()))
                return true;

            return false;
        }

        private void OpenEditTaskWindow(ToDoListEditorWindow toDoListWindow, Task task)
        {
            EditTaskEditorWindow editPriorityWindow = GetWindow<EditTaskEditorWindow>();
            editPriorityWindow.Init(this, task, _configuration);
        }

        private void OpenEditConfigWindow()
        {
            ConfigurationEditorWindow configWindow = GetWindow<ConfigurationEditorWindow>();
            configWindow.Init(this);
        }

        //open the window to edit a task priority.
        //priority is been remapped to alwasy add a No Priority item in the dropdown
        private void OpenEditPriorityWindow(List<TaskPriority> priorities, Task task)
        {
            EditPriorityEditorWindow editPriorityWindow = GetWindow<EditPriorityEditorWindow>();
            int index = -1;

            if (task.priority != null)
                index = priorities.FindIndex(p => p.name == task.priority.name);

            editPriorityWindow.Init(this, priorities, task, index + 1);
        }

        //add a task with default parameters and title equal to the string in the quick add task textbox
        //if no string is inserted an error will be fired
        private void QuickAddTask(string addTaskValue)
        {
            if (string.IsNullOrEmpty(addTaskValue) || addTaskValue.Equals(_addTaskplaceHolder))
                Debug.LogError("Unable to add task because Title is Empty");
            else
            {
                _configuration.tasks.Add(new Task(addTaskValue));
                _unsavedChanges = true;
            }
        }

        public void ToggleBulkEdit()
        {
            _bulkEdit = !_bulkEdit;
        }

        public void ToggleSelection(List<Task> task)
        {
            if (task.Count > 0)
            {
                //if at least a task is turned off enable all task
                if (task.Any(t => !t.selected))
                {
                    for (int i = 0; i < task.Count(); i++)
                        task[i].selected = true;
                }

                else
                {
                    for (int i = 0; i < task.Count(); i++)
                        task[i].selected = false;
                }
            }
        }

        //triggered when the edittaskwindow is closed using the save button
        public void EditTaskPriority(Task task, int selectedPriority)
        {
            Task taskToEdit = _configuration.tasks.SingleOrDefault(t => t.code == task.code);

            if (selectedPriority == 0)
                taskToEdit.priority = null;
            else
                taskToEdit.priority = _configuration.settings.priorities[selectedPriority - 1];
        }
    }
}