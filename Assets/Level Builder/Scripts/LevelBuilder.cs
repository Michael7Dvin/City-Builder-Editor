using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

public class LevelBuilder : EditorWindow
{
    private const string _groundTilesTabName = "Ground Tiles";
    private const string _buildingsTabName = "Buildings";
    private const string _natureTabName = "Nature";

    private const string _pathToGroundTiles = "Assets/Level Builder/Resources/Ground Tiles";
    private const string _pathToBuildings = "Assets/Level Builder/Resources/Buildings";
    private const string _pathToNature = "Assets/Level Builder/Resources/Nature";

    private Vector2 _scrollPosition;
    private int _selectedElement;
    private List<GameObject> _catalog = new List<GameObject>();
    private bool _building;

    private int _selectedTabNumber = 0;

    private Dictionary<string, string> _tabs = new Dictionary<string, string>()
    {
        {_buildingsTabName, _pathToBuildings},
        {_groundTilesTabName, _pathToGroundTiles},
        {_natureTabName, _pathToNature},
    };

    private GameObject _createdObject;
    private GameObject _parent;

    [MenuItem("Level/Builder")]
    private static void ShowWindow()
    {
        GetWindow(typeof(LevelBuilder));
    }

    private void OnFocus()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnGUI()
    {
        _parent = (GameObject)EditorGUILayout.ObjectField("Parent", _parent, typeof(GameObject), true);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        
        if (_createdObject != null)
        {
            EditorGUILayout.LabelField("Created Object Settings");
            Transform createdTransform = _createdObject.transform;
            createdTransform.position = EditorGUILayout.Vector3Field("Position", createdTransform.position);
            createdTransform.rotation = Quaternion.Euler(EditorGUILayout.Vector3Field("Position", createdTransform.rotation.eulerAngles));
            createdTransform.localScale = EditorGUILayout.Vector3Field("Position", createdTransform.localScale);
        }
        
        _selectedTabNumber = GUILayout.Toolbar(_selectedTabNumber, _tabs.Keys.ToArray());
        
        RefreshCatalogInFolder(_tabs.ElementAt(_selectedTabNumber).Value);
        
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        _building = GUILayout.Toggle(_building, "Start building", "Button", GUILayout.Height(60));
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        DrawCatalog(GetCatalogIcons());
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (_building)
        {
            if (Raycast(out Vector3 contactPoint))
            {
                DrawPounter(contactPoint, Color.red);

                if (CheckInput())
                {
                    CreateObject(contactPoint);
                }

                sceneView.Repaint();
            }
        }
    }

    private bool Raycast(out Vector3 contactPoint)
    {
        Ray guiRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        contactPoint = Vector3.zero;

        if (Physics.Raycast(guiRay, out RaycastHit raycastHit))
        {
            contactPoint = raycastHit.point;
            return true;
        }

        return false;
    }

    private void DrawPounter(Vector3 position, Color color)
    {
        Vector3 boundsSize = _catalog[_selectedElement].GetComponent<MeshRenderer>().bounds.size;
        Handles.color = color;
        Handles.DrawWireCube(position, boundsSize);
    }

    private bool CheckInput()
    {
        HandleUtility.AddDefaultControl(0);

        return Event.current.type == EventType.MouseDown && Event.current.button == 0;
    }

    private void CreateObject(Vector3 position)
    {
        if (_selectedElement < _catalog.Count)
        {
            GameObject prefab = _catalog[_selectedElement];
            _createdObject = Instantiate(prefab);
            _createdObject.transform.position = position;
            _createdObject.transform.parent = _parent.transform;

            Undo.RegisterCreatedObjectUndo(_createdObject, "Create Building");
        }
    }

    private void DrawCatalog(List<GUIContent> catalogIcons)
    {
        int gridSizeInPixels = 80;
        int collumns = (int) (position.width / gridSizeInPixels);
        int rows = (_catalog.Count / collumns) + 1;

        GUILayout.Label("Buildings");

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        _selectedElement = GUILayout.SelectionGrid
            (_selectedElement,
            catalogIcons.ToArray(),
            collumns,
            GUILayout.MaxWidth(collumns * gridSizeInPixels),
            GUILayout.MaxHeight(rows * gridSizeInPixels));
        
        EditorGUILayout.EndScrollView();
    }

    private List<GUIContent> GetCatalogIcons()
    {
        List<GUIContent> catalogIcons = new List<GUIContent>();

        foreach (var element in _catalog)
        {
            Texture2D texture = AssetPreview.GetAssetPreview(element);
            catalogIcons.Add(new GUIContent(texture));
        }

        return catalogIcons;
    }

    private void RefreshCatalogInFolder(string path)
    {
        _catalog.Clear();
        
        System.IO.Directory.CreateDirectory(path);
        string[] prefabFiles = System.IO.Directory.GetFiles(path, "*.prefab");

        foreach (var prefabFile in prefabFiles)
            _catalog.Add(AssetDatabase.LoadAssetAtPath(prefabFile, typeof(GameObject)) as GameObject);
    }

    private void RefreshCatalog()
    {
        _catalog.Clear();

        System.IO.Directory.CreateDirectory(_pathToBuildings);
        string[] prefabFiles = System.IO.Directory.GetFiles(_pathToBuildings, "*.prefab");
        foreach (var prefabFile in prefabFiles)
            _catalog.Add(AssetDatabase.LoadAssetAtPath(prefabFile, typeof(GameObject)) as GameObject);
    }
}