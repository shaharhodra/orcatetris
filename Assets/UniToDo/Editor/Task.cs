using System;

namespace Modesto.UniToDo
{
    /// <summary>
    /// Class representing a task.
    /// </summary>
    [Serializable]
    public class Task
    {
        public string code;
        public string title;
        public string description;
        public string category;
        public TaskStatus status;
        public TaskPriority priority;

        //used when bulk editing only, so no need to serialize
        [NonSerialized]public bool selected;

        public Task(string title)
        {
            this.title = title;
            status = TaskStatus.ToDo;
            code = Utils.GetCode(16);
            selected = false;
        }
    }
}
