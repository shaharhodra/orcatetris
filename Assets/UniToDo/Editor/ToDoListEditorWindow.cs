using UnityEditor;
using UnityEngine;

namespace Modesto.UniToDo
{
    /// <summary>
    /// The UniToDo window.
    /// The class is splitted in trhee files for readability.
    /// </summary>
    public partial class ToDoListEditorWindow : EditorWindow
    {
        //icons used in the window
        [SerializeField] private Texture _notFoundTexture;
        [SerializeField] private Texture _todoTaskButtonIcon;
        [SerializeField] private Texture _doneTaskButtonIcon;
        [SerializeField] private Texture _inApprovalTaskButtonIcon;
        [SerializeField] private Texture _kebabMenuIcon;
        [SerializeField] private Texture _trashbinIcon;
        [SerializeField] private Texture _noCategoryIcon;
        [SerializeField] private Texture _searchIcon;
        [SerializeField] private Texture _mainIcon;

        //the configuration is the definition of a todolist with its tasks and settings
        [SerializeField] private Configuration _configuration;
        private CustomAssetPostProcessor _assetPostProcessor;
        //defines wheter a todolistconfig files has been found
        private bool _configFound;
        //the placeholder and the string in the add quick task textbox
        private string _addTaskplaceHolder = "Quick Add Task...";
        private string _addTaskValue;
        //defines whether there are changes to write to the todolistconfig file
        private bool _unsavedChanges;
        //defines whether bulk edit is active
        private bool _bulkEdit;
        //the string in the searchbar
        private string _searchbarFilter;
        //used to enable scrollview in the window
        private Vector2 _scrollPos;

        [MenuItem("Tools/UniToDo")]
        static void Init()
        {
            ToDoListEditorWindow window = (ToDoListEditorWindow)EditorWindow.GetWindow(typeof(ToDoListEditorWindow));
            window.Show();
        }

        private void OnEnable()
        {
            InitValues();
            InitGraphics();
            ReadConfig();
        }

        //initialize the variables or reset their values
        private void InitValues()
        {
            //initialize post processor to notify window when assetDatabase changes
            _assetPostProcessor = new CustomAssetPostProcessor();
            _assetPostProcessor.AddOnDeleteListener(() => ReadConfig(true));
            _assetPostProcessor.AddOnMoveListener(() => ReadConfig(true));

            //initialize other required variables
            _addTaskValue = string.Empty;
            _searchbarFilter = string.Empty;
            _bulkEdit = false;
        }

        //init all the icons and other GUI parameters
        private void InitGraphics()
        {
            minSize = new Vector2(200, 200);

            //init title
            _mainIcon ??= EditorGUIUtility.Load("Installed") as Texture;
            titleContent = new GUIContent("UniToDo", _mainIcon);

            //init other textures used in UnitToDo if they are null
            _notFoundTexture ??= (Texture)Resources.Load("icon_noConfig") as Texture;
            _todoTaskButtonIcon ??= EditorGUIUtility.Load("TestNormal") as Texture;
            _doneTaskButtonIcon ??= EditorGUIUtility.Load("TestPassed") as Texture;
            _inApprovalTaskButtonIcon ??= EditorGUIUtility.Load("Loading") as Texture;
            _kebabMenuIcon ??= EditorGUIUtility.Load("d__Menu") as Texture;
            _trashbinIcon ??= EditorGUIUtility.Load("d_TreeEditor.Trash") as Texture;
            _noCategoryIcon ??= EditorGUIUtility.Load("sv_icon_none") as Texture;
            _searchIcon ??= EditorGUIUtility.Load("d_Search Icon") as Texture;
        }

        void OnGUI()
        {
            if (!_configFound)
            {
                DrawNoConfigWindow();
                return;
            }

            DrawTopBar();

            if (_configuration.tasks.Count > 0)
                DrawSearchBar();

            DrawTaskList();
            DrawFooter();
        }

        //check if config file exists
        public void ReadConfig(bool forceRefresh = false)
        {
            if (_configuration == null || forceRefresh)
            {
                TextAsset configJson = (TextAsset)Resources.Load(DataHandler.ConfigFileName) as TextAsset;
                _configFound = configJson != null;

                if (_configFound)
                    _configuration = new Configuration(configJson.text);
                else
                    _configuration = null;

                _unsavedChanges = false;
            }
        }

        //update the todolist config files.
        //triggered when apply changes button is clicked
        public void SaveConfig()
        {
            _searchbarFilter = string.Empty;
            _unsavedChanges = false;
            DataHandler.SaveConfiguration(_configuration);
        }
    }
}