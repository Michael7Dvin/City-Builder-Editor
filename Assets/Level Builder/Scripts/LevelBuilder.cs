using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Numerics;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class LevelBuilder : EditorWindow
{
    private const string _groundTilesTabName = "Ground Tiles";
    private const string _buildingsTabName = "Buildings";
    private const string _natureTabName = "Nature";

    private const string _pathToGroundTiles = "Assets/Level Builder/Resources/Ground Tiles";
    private const string _pathToBuildings = "Assets/Level Builder/Resources/Buildings";
    private const string _pathToNature = "Assets/Level Builder/Resources/Nature";

    private Vector3 _currentRotation;

    private Vector3 _rotationValue = new Vector3(0, 90, 0);
    
    private Vector2 _scrollPosition;
    private int _selectedElement;
    private Vector3 _selectedElementSize;
    private List<GameObject> _catalog = new List<GameObject>();
    private bool _building;
    private LayerMask _layerForBuild;
    private LayerMask _buildingsLayer;

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
        _layerForBuild = EditorGUILayout.LayerField("Ground Layer", _layerForBuild);
        _buildingsLayer = EditorGUILayout.LayerField("Buildings Layer", _buildingsLayer);
        // _buildingsLayer = EditorGUILayout.MaskField("Buildings", _buildingsLayer);
        //
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        
        if (_createdObject != null)
        {
            EditorGUILayout.LabelField("Created Object Settings");
            Transform createdTransform = _createdObject.transform;
            createdTransform.position = EditorGUILayout.Vector3Field("Position", createdTransform.position);
            createdTransform.rotation = Quaternion.Euler(EditorGUILayout.Vector3Field("Rotation", createdTransform.rotation.eulerAngles));
            createdTransform.localScale = EditorGUILayout.Vector3Field("Scale", createdTransform.localScale);
        }
        
        _selectedTabNumber = GUILayout.Toolbar(_selectedTabNumber, _tabs.Keys.ToArray());
        
        RefreshCatalogInFolder(_tabs.ElementAt(_selectedTabNumber).Value);
        
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        _building = GUILayout.Toggle(_building, "Start building", "Button", GUILayout.Height(60));
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.BeginVertical(GUI.skin.window);
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        DrawCatalog(GetCatalogIcons());
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (_building)
        {   
            Vector3 boundsSize = _catalog[_selectedElement].GetComponent<MeshRenderer>().bounds.size;
 
            if (CheckRotateClockwiseInput())
            {
                Debug.Log("Clockwise");
                RotateOject(_rotationValue);
            }

            if (CheckRotateCounterClockwiseInput())
            {
                Debug.Log("CounterClockwise");
                RotateOject(-_rotationValue);
            }
            
            if (Raycast(out Vector3 contactPoint))
            {
                DrawPounter(contactPoint);
                
                if (CheckInput())
                {
                    if (IsAbleToPlaceObject(contactPoint, boundsSize))
                    {
                        CreateObject(contactPoint, _currentRotation);
                    }
                }

                sceneView.Repaint();
            }
        }
    }

    private bool Raycast(out Vector3 contactPoint)
    {
        Ray guiRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        contactPoint = Vector3.zero;

        // if (Physics.Raycast(guiRay, out RaycastHit raycastHit))
        if (Physics.Raycast(guiRay, out RaycastHit raycastHit))
        {
            contactPoint = raycastHit.point;
            if (raycastHit.collider.gameObject.layer == _layerForBuild.value)
            {
                return true;
            }
        }

        return false;
    }

    private void DrawPounter(Vector3 position)
    {
        _catalog[_selectedElement].GetComponent<MeshRenderer>().transform.rotation = Quaternion.Euler(_currentRotation);
        Vector3 boundsSize = _catalog[_selectedElement].GetComponent<MeshRenderer>().bounds.size;
        
        // Handles.color = color;
        Handles.color = IsAbleToPlaceObject(position, boundsSize) ? Color.green : Color.red;
        Handles.DrawWireCube(position, boundsSize);
    }

    private bool CheckRotateClockwiseInput()
    {
        bool keyPressed = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.E;
        return keyPressed;
    }

    private bool CheckRotateCounterClockwiseInput()
    {
        bool keyPressed = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Q;
        return keyPressed;
    }
    
    private bool CheckInput()
    {
        HandleUtility.AddDefaultControl(0);

        return Event.current.type == EventType.MouseDown && Event.current.button == 0;
    }

    private void CreateObject(Vector3 position, Vector3 rotation)
    {
        if (_selectedElement < _catalog.Count)
        {
            GameObject prefab = _catalog[_selectedElement];
            _createdObject = Instantiate(prefab);
            _createdObject.transform.position = position;
            _createdObject.transform.parent = _parent.transform;
            _createdObject.layer = _buildingsLayer.value;
            _createdObject.transform.rotation = Quaternion.Euler(rotation);
            Debug.Log($"created object rotation: {_createdObject.transform.rotation.x} {_createdObject.transform.rotation.y} {_createdObject.transform.rotation.z}");

            Undo.RegisterCreatedObjectUndo(_createdObject, "Create Building");
        }
    }
    
    private void DrawCatalog(List<GUIContent> catalogIcons)
    {
        GUILayout.Label("Buildings");
        _selectedElement = GUILayout.SelectionGrid(_selectedElement, catalogIcons.ToArray(), 4, GUILayout.Width(400), GUILayout.Height(1000));
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

    private void RotateOject(Vector3 rotation)
    {
        _currentRotation += rotation;

        if (_currentRotation.y < -360 || _currentRotation.y > 360)
            _currentRotation.y = 90;
        
        Debug.Log($"current rotation X: {_currentRotation.x} Y: {_currentRotation.y} Z: {_currentRotation.z}");
    }

    private bool IsAbleToPlaceObject(Vector3 position, Vector3 boxSize)
    {
        Vector3 halfBoxSize = boxSize / 2;
        // Collider[] hitColliders = Physics.OverlapBox(position, halfBoxSize, Quaternion.identity, _buildingsLayer.value);
        // Collider[] hitColliders = Physics.OverlapBox(position, boxSize, Quaternion.identity, _buildingsLayer);
        Collider[] hitColliders = Physics.OverlapBox(position, halfBoxSize);
        
        Debug.Log($"hitCollider: {hitColliders.Length}");
        // if (hitColliders.Length > 0 && hitColliders[0].gameObject.layer == _buildingsLayer.value)
        if (hitColliders.Length == 1)
        {
            return true;
        }
        
        return false;
    }

    private void RefreshCatalogInFolder(string path)
    {
        _catalog.Clear();
        // Debug.Log(path);
        
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