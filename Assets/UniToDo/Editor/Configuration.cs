using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Modesto.UniToDo
{
    /// <summary>
    /// Configuration is the class in which all the info about settings and the tasks list are stored.
    /// </summary>
    [Serializable]
    public class Configuration
    {
        public int CategoriesCount => settings.categories.Count;
        public int PrioritiesCount => settings.priorities.Count;

        //define task with status in approval must be shown in the todolist
        public bool showInApprovalTasks;
        //define task with status done must be shown in the todolist
        public bool showDoneTasks;
        //define if use categories in the todolist
        public bool useCategories;
        //define if use priorities in the todolist
        public bool usePriorities;
        public ToDoListSettings settings;
        //define the colors of the status labels in the todo list
        public Color toDoLabelColor;
        public Color inApprovalLabelColor;
        public Color doneLabelColor;
        //the list of task
        public List<Task> tasks;

        public Configuration()
        {
            showInApprovalTasks = true;
            showDoneTasks = true;
            useCategories = false;
            usePriorities = false;
            //default unity label color 
            toDoLabelColor = new Color(0.89f, 0.89f, 0.89f);
            inApprovalLabelColor = new Color(0.89f, 0.89f, 0.89f);
            doneLabelColor = new Color(0.89f, 0.89f, 0.89f);
            settings = new ToDoListSettings();
            tasks = new List<Task>();
        }

        public Configuration(string json)
        {
            Configuration configuration = JsonUtility.FromJson<Configuration>(json);
            showInApprovalTasks = configuration.showInApprovalTasks;
            showDoneTasks = configuration.showDoneTasks;
            useCategories = configuration.useCategories;
            usePriorities = configuration.usePriorities;
            toDoLabelColor = configuration.toDoLabelColor;
            inApprovalLabelColor = configuration.inApprovalLabelColor;
            doneLabelColor = configuration.doneLabelColor;
            settings = configuration.settings;

            //when reading from or writing to json null priority will be stored as empty
            //to avoid that after you load task list, check if priority is empty to set that to null
            tasks = configuration.tasks;

            for(int i = 0; i < tasks.Count; i++)
            {
                if (string.IsNullOrEmpty(tasks[i].priority.name))
                    tasks[i].priority = null;
            }
        }

        public void AddCategory()
        {
            settings.categories.Add("New Category");
        }

        public void RemoveCategory(int index)
        {
            string selectedCategory = settings.categories[index];

            for (int i = 0; i < tasks.Count; i++)
            {
                if (tasks[i].category.Equals(selectedCategory))
                {
                    Debug.LogError("Unable to remove category " + selectedCategory + " because on or more Tasks depends on it");
                    return;
                }
            }

            settings.categories.RemoveAt(index);
        }

        public void AddPriority()
        {
            settings.priorities.Add(new TaskPriority("New Priority", GUI.skin.label.normal.textColor));
        }

        public void RemovePriority(int index)
        {
            TaskPriority selectedPriority = settings.priorities[index];

            for(int i = 0; i < tasks.Count; i++)
            {
                if(tasks[i].priority.name == selectedPriority.name)
                {
                    Debug.LogError("Unable to remove priority " + selectedPriority.name + " because on or more Tasks depends on it");
                    return;
                }
            }

            settings.priorities.RemoveAt(index);
        }

        public void AddTask(Task task)
        {
            tasks.Add(task);
        }

        public void RemoveTask(int index)
        {
            tasks.RemoveAt(index);
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }
    }
}