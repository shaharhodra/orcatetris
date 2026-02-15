using System;
using System.Collections.Generic;

namespace Modesto.UniToDo
{
    /// <summary>
    /// Class which contains the user custom settings for categories and priorities
    /// </summary>
    [Serializable]
    public class ToDoListSettings
    {
        public List<string> categories;
        public List<TaskPriority> priorities;

        public ToDoListSettings()
        {
            categories = new List<string>();
            priorities = new List<TaskPriority>();

        }
    }
}