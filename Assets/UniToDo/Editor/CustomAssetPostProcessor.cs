using System;
using UnityEditor;
using UnityEngine;

namespace Modesto.UniToDo
{
    /// <summary>
    /// A post processor to check if the todolistconfig file has been deleted/added to the Assetsdatabase
    /// </summary>
    public class CustomAssetPostProcessor : AssetPostprocessor
    {
        static Action ConfigDelete;
        static Action ConfigMove;

        public void AddOnDeleteListener(Action onconfigDelete) { ConfigDelete = onconfigDelete; }
        public void AddOnMoveListener(Action onconfigMove) { ConfigMove = onconfigMove; }

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string str in deletedAssets)
            {
                ConfigDelete?.Invoke();
            }

            for (int i = 0; i < movedAssets.Length; i++)
            {
                Debug.Log("Moved Asset: " + movedAssets[i].LastIndexOf("/") + " from: " + movedFromAssetPaths[i]);
                ConfigMove?.Invoke();
            }
        }
    }
}
