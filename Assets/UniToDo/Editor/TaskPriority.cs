using System;
using UnityEngine;

namespace Modesto.UniToDo
{
    /// <summary>
    /// Class defining a priority for tasks.
    /// </summary>
    [Serializable]
    public class TaskPriority
    {
        public string name;
        public Color color;

        public TaskPriority(string name, Color color)
        {
            this.name = name;
            this.color = color;
        }
    }
}