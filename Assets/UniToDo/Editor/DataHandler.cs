using System.IO;
using UnityEditor;
using UnityEngine;

namespace Modesto.UniToDo
{
    /// <summary>
    /// Simple class to read and write persistent data
    /// </summary>
    public static class DataHandler
    {
        public static string ConfigFileName = "todolistconfig";

        public static void SaveConfiguration(Configuration configuration)
        {
            string json = configuration.ToJson();
            string filePath = Path.Combine("Assets/UniToDo/Editor/Resources/", ConfigFileName + ".json");

            if (File.Exists(filePath))
                File.Delete(filePath);

            File.WriteAllText(filePath, json);

            AssetDatabase.Refresh();
        }

        public static Configuration CreateConfiguration()
        {
            Configuration configuration = new Configuration();

            TextAsset configJson = (TextAsset)Resources.Load(ConfigFileName) as TextAsset;

            if (configJson != null)
                configuration = new Configuration(configJson.text);

            return configuration;
        }
    }
}