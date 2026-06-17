using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "TutorialTypes", menuName = "Tutorial Types")]
public class TutorialScripableObject : ScriptableObject 
{
   public enum TutorialType
   {
      EnterLevel,
      PlaceItem,
      Objective,
    }

    [Serializable]
    public class TutorialData
    {
        public string title;
        public int Index;
        public TutorialType tutorialType;
        public string description;
        public Sprite icon;
    }

    public List<TutorialData> Tutorials;

}
