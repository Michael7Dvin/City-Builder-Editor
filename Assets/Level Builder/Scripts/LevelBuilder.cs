using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UniRx;
using Cysharp.Threading.Tasks;
using System;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine.UIElements;

public class LevelBuilder : EditorWindow
{
    [Flags]
    public enum LevelBuilderStatus
    {
        Nothing = 0,
        Everything = -1,
        Enabled = 1 << 0,
        OpionsApplied = 1 << 1,
        BuildMode = 1 << 2,
    }

    public readonly ReactiveProperty<LevelBuilderStatus> _status = new ReactiveProperty<LevelBuilderStatus>(LevelBuilderStatus.Nothing);
    private bool _buildingToggleStatus = false;

    private Options _options;
    private Input _input;
    private RayCaster _rayCaster;
    private Catalog _catalog;
    private CreateAvailability _createAvailability;
    private Creator _creation;
    private CurrentObjectEditor _currentObjectEditor;
    private Preview _preview;

    public readonly CompositeDisposable _disposable = new CompositeDisposable();

    [MenuItem("Level/Builder")]
    private static void ShowWindow()
    {
        GetWindow(typeof(LevelBuilder));
    }

    private async void Initialize()
    {
        _options = new Options();

        await new WaitUntil(() => _options.IsApplied == true);

        _input = new Input();
        _rayCaster = new RayCaster(_options);
        _catalog = new Catalog();
        _currentObjectEditor = new CurrentObjectEditor(_input, _catalog, _disposable);
        _createAvailability = new CreateAvailability(_options, _currentObjectEditor, _rayCaster);
        _creation = new Creator(_options, _input, _rayCaster, _currentObjectEditor, _createAvailability, _disposable);
        _preview = new Preview(_status, _options, _rayCaster, _catalog, _createAvailability, _currentObjectEditor, _disposable);

        _status.Value |= LevelBuilderStatus.OpionsApplied;
    }

    private void OnEnable()
    {
        _status.Value |= LevelBuilderStatus.Enabled;
        Initialize();
    }
    private void OnDisable()
    {
        _status.Value |= LevelBuilderStatus.Nothing;
        _disposable.Clear();
    }

    private void OnFocus()
    {
        if (_status.Value.HasFlag(LevelBuilderStatus.Enabled))
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }
    }

    private void OnGUI()
    {
        if (_status.Value.HasFlag(LevelBuilderStatus.OpionsApplied) == true)
        {
            _buildingToggleStatus = GUILayout.Toggle(_buildingToggleStatus, "Start building", "Button", GUILayout.Height(60));

            if (_buildingToggleStatus == true)
            {
                _status.Value |= LevelBuilderStatus.BuildMode;
            }
            else
            {
                _status.Value &= ~LevelBuilderStatus.BuildMode;
            }

            _catalog.Draw(position);
        }
        else
        {
            _options.Draw();
        }                
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (_status.Value.HasFlag(LevelBuilderStatus.BuildMode) == true)
        {
            _rayCaster.Raycast();
            _preview.Draw(sceneView);
            _input.CheckInputs();
            _createAvailability.Update();
        }      
    }

    public class Options
    {
        public bool IsApplied { get; private set; }

        public GameObject Parent { get; private set; }

        public Material BuildAllowed { get; private set; }
        public Material BuildDisallowed { get; private set; }

        public LayerMask GroundLayer{ get; private set; }
        public LayerMask BuildingsLayer { get; private set; }
      

        public void Draw()
        {
            EditorGUILayout.LabelField("Set up");

            Parent = (GameObject)EditorGUILayout.ObjectField("Parent", Parent, typeof(GameObject), true);

            BuildAllowed = (Material)EditorGUILayout.ObjectField("Build Allowed Material", BuildAllowed, typeof(Material), true);
            BuildDisallowed = (Material)EditorGUILayout.ObjectField("Build Disallowed Material", BuildDisallowed, typeof(Material), true);

            GroundLayer = EditorGUILayout.LayerField("Ground Layer", GroundLayer);
            BuildingsLayer = EditorGUILayout.LayerField("Buildings Layer", BuildingsLayer);

            if (GUILayout.Button("Apply"))
            {
                if (Parent != null && BuildAllowed != null && BuildDisallowed != null && GroundLayer.value != 0 && BuildingsLayer.value != 0)
                {
                    IsApplied = true;
                }
            }
        }
    }
    public class Input
    {
        public MouseKey LeftMouse { get; private set; } = new MouseKey(0);

        public KeyboardKey Q { get; private set; } = new KeyboardKey(KeyCode.Q);
        public KeyboardKey E { get; private set; } = new KeyboardKey(KeyCode.E);
        public KeyboardKey LeftShift { get; private set; } = new KeyboardKey(KeyCode.LeftShift);

        public void CheckInputs()
        {
            HandleUtility.AddDefaultControl(0);

            LeftMouse.CheckInput();
            
            Q.CheckInput();
            E.CheckInput();
            LeftShift.CheckInput();
        }

        public class MouseKey
        {
            private readonly int _button;

            private readonly ReactiveProperty<bool> _keyDown = new ReactiveProperty<bool>();

            public MouseKey(int button)
            {
                _button = button;
            }

            public IReadOnlyReactiveProperty<bool> KeyDown => _keyDown;

            public void CheckInput()
            {
                if (Event.current.button == _button && Event.current.type == EventType.MouseDown)
                {
                    _keyDown.Value = true;
                }
                else if (Event.current.button == _button && Event.current.type == EventType.MouseUp)
                {
                    _keyDown.Value = false;
                }
            }
        }
        public class KeyboardKey
        {
            private readonly KeyCode _keyCode;

            private readonly ReactiveProperty<bool> _keyDown = new ReactiveProperty<bool>();

            public KeyboardKey(KeyCode keyCode)
            {
                _keyCode = keyCode;
            }

            public IReadOnlyReactiveProperty<bool> KeyDown => _keyDown;

            public void CheckInput()
            {
                if (Event.current.keyCode == _keyCode && Event.current.type == EventType.KeyDown)
                {
                    _keyDown.Value = true;
                }
                else if (Event.current.keyCode == _keyCode && Event.current.type == EventType.KeyUp)
                {
                    _keyDown.Value = false;
                }
            }
        }
    }
    public class RayCaster
    {
        private readonly Options _options;

        public RayCaster(Options options)
        {
            _options = options;
        }

        public Vector3 HitPoint { get; private set; }
        public GameObject HitGround { get; private set; }

        public bool IsHitGround => HitGround != null;

        public bool Raycast()
        {
            Ray guiRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

            HitPoint = Vector3.zero;
            HitGround = null;

            if (Physics.Raycast(guiRay, out RaycastHit raycastHit, Mathf.Infinity, 1 << _options.GroundLayer.value))
            {
                HitPoint = raycastHit.point;
                HitGround = raycastHit.collider.gameObject;
                return true;               
            }

            return false;
        }
    }
    public class Catalog
    {
        private const string _groundTilesTabName = "Ground Tiles";
        private const string _buildingsTabName = "Buildings";
        private const string _natureTabName = "Nature";

        private const string _pathToGroundTiles = "Assets/Level Builder/Resources/Ground Tiles";
        private const string _pathToBuildings = "Assets/Level Builder/Resources/Buildings";
        private const string _pathToNature = "Assets/Level Builder/Resources/Nature";

        private readonly List<CatalogPage> pages = new List<CatalogPage>()
        {
            new CatalogPage(_pathToGroundTiles),
            new CatalogPage(_pathToBuildings),
            new CatalogPage(_pathToNature),
        };
        private readonly ReactiveProperty<GameObject> _selectedElement = new ReactiveProperty<GameObject>();

        private readonly Dictionary<string, string> _tabs = new Dictionary<string, string>()
        {
            {_groundTilesTabName, _pathToGroundTiles},
            {_buildingsTabName, _pathToBuildings},
            {_natureTabName, _pathToNature},
        };
        private int _selectedTabNumber = 0;

        private Vector2 _scrollPosition;

        public IReadOnlyReactiveProperty<GameObject> SelectedElement => _selectedElement;

        public void Draw(Rect editorWindowPosition)
        {
            _selectedTabNumber = GUILayout.Toolbar(_selectedTabNumber, _tabs.Keys.ToArray());

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            pages[_selectedTabNumber].Draw(editorWindowPosition);
            EditorGUILayout.EndScrollView();

            _selectedElement.Value = pages[_selectedTabNumber].SelectedElement;
        }

        public class CatalogPage
        {
            private readonly string _pathToResources;
            private int _selectedElementIndex = 0;

            public CatalogPage(string pathToResources)
            {
                _pathToResources = pathToResources;
            }

            public GameObject SelectedElement { get; private set; }

            private List<GameObject> Elements = new List<GameObject>();
            private List<GUIContent> Icons
            {
                get
                {
                    List<GUIContent> catalogIcons = new List<GUIContent>();

                    foreach (var element in Elements)
                    {
                        Texture2D texture = AssetPreview.GetAssetPreview(element);
                        catalogIcons.Add(new GUIContent(texture));
                    }

                    return catalogIcons;
                }
            }

            public void Draw(Rect editorWindowPosition)
            {
                RefreshInFolder(_pathToResources);

                int gridSizeInPixels = 80;
                int collumns = (int)(editorWindowPosition.width / gridSizeInPixels);
                int rows = (Elements.Count / collumns) + 1;

                _selectedElementIndex = GUILayout.SelectionGrid
                    (_selectedElementIndex,
                    Icons.ToArray(),
                    collumns,
                    GUILayout.MaxWidth(collumns * gridSizeInPixels),
                    GUILayout.MaxHeight(rows * gridSizeInPixels));

                SelectedElement = Elements[_selectedElementIndex];
            }

            private void RefreshInFolder(string path)
            {
                Elements.Clear();

                System.IO.Directory.CreateDirectory(path);
                string[] prefabFiles = System.IO.Directory.GetFiles(path, "*.prefab");

                foreach (var prefabFile in prefabFiles)
                    Elements.Add(AssetDatabase.LoadAssetAtPath(prefabFile, typeof(GameObject)) as GameObject);
            }
        }
    }
    public class CreateAvailability
    {
        private readonly Options _options;
        private readonly CurrentObjectEditor _currentObjectEditor;
        private readonly RayCaster _rayCaster;

        private readonly Vector3[] _directionVectors = new[]
        {
            Vector3.forward,
            Vector3.right,
            Vector3.back,
            Vector3.left,
        };

        private readonly ReactiveProperty<bool> _availability = new ReactiveProperty<bool>();

        public CreateAvailability(Options options, CurrentObjectEditor currentObjectEditor, RayCaster rayCaster)
        {
            _options = options;
            _currentObjectEditor = currentObjectEditor;
            _rayCaster = rayCaster;
        }

        public IReadOnlyReactiveProperty<bool> Availability => _availability;

        public void Update()
        {
            _availability.Value = IsOverlapOtherObjects();
        }

        private bool IsOverlapOtherObjects()
        {
            Vector3 halfBoxSize = _currentObjectEditor.CurrentObject.GetComponent<MeshRenderer>().bounds.size / 2;
            Collider[] hitColliders = Physics.OverlapBox(_rayCaster.HitPoint, halfBoxSize, Quaternion.identity, 1 << _options.BuildingsLayer.value);

            if (hitColliders.Length == 1)
            {
                return false;
            }

            return true;
        }

        private bool IsOverlapRoadTile()
        {
            Vector3 cursorPosition = new Vector3();
            Vector3 halfBoxSize = _currentObjectEditor.CurrentObject.GetComponent<MeshRenderer>().bounds.size / 2;
            Collider[] hitColliders = Physics.OverlapBox(cursorPosition, halfBoxSize);
            bool isRoadDetected = false;
            Collider roadCollider = new Collider();

            foreach (var collider in hitColliders)
            {
                // if (collider.GetComponent<Road>()) -- расскомментировать есть ли есть пустышка Road
                {
                    isRoadDetected = true;
                    roadCollider = collider;
                    return true;
                }
            }

            return false;
        }
    }
    public class Creator
    {
        private readonly Options _options;
        private readonly Input _input;
        private readonly RayCaster _rayCaster;
        private readonly CurrentObjectEditor _currentObjectEditor;
        private readonly CreateAvailability _availabilityChecker;

        public GameObject LastCreatedObject { get; private set; }

        public Creator(Options options, Input input, RayCaster raycaster, CurrentObjectEditor currentObjectEditor, CreateAvailability createAvailability, CompositeDisposable disposable)
        {
            _options = options;
            _input = input;
            _rayCaster = raycaster;
            _currentObjectEditor = currentObjectEditor;
            _availabilityChecker = createAvailability;

            _input
                .LeftMouse
                .KeyDown
                .Skip(1)
                .Where(_ => _ == true)
                .Subscribe(_ => Create(_rayCaster.HitPoint))
                .AddTo(disposable);
        }

        private void Create(Vector3 position)
        {
            if (_availabilityChecker.Availability.Value)
            {
                LastCreatedObject = Instantiate(_currentObjectEditor.CurrentObject, position, _currentObjectEditor.CurrentObject.transform.rotation);
                LastCreatedObject.transform.parent = _options.Parent.transform;

                Undo.RegisterCreatedObjectUndo(LastCreatedObject, "Create Building");
            }
        }
    }
    public class CurrentObjectEditor
    {
        private readonly Input _input;

        public GameObject CurrentObject;

        public CurrentObjectEditor(Input input, Catalog catalog, CompositeDisposable disposable)
        {
            _input = input;

            _input
                .Q
                .KeyDown
                .Skip(1)
                .Where(_ => _ == true && _input.LeftShift.KeyDown.Value == true)
                .Subscribe(_ => RotateY(-90))
                .AddTo(disposable);

            _input
                .E
                .KeyDown
                .Skip(1)
                .Where(_ => _ == true && _input.LeftShift.KeyDown.Value == true)
                .Subscribe(_ => RotateY(90))
                .AddTo(disposable);

            Observable
                .EveryFixedUpdate()
                .Skip(1)
                .Where(_ => _input.LeftShift.KeyDown.Value == false)
                .Subscribe(_ =>
                {
                    if (_input.Q.KeyDown.Value == true)
                        RotateY(-0.5f);
                    if (_input.E.KeyDown.Value == true)
                        RotateY(0.5f);
                })
                .AddTo(disposable);

            catalog
                .SelectedElement
                .Subscribe(_ => CurrentObject = _)
                .AddTo(disposable);
        }

        private void RotateY(float angle)
        {
            CurrentObject.transform.rotation = Quaternion.Euler(0, CurrentObject.transform.rotation.eulerAngles.y + angle, 0);
        }
    }
    public class Preview
    {
        private readonly Options _options;
        private readonly RayCaster _rayCaster;
        private readonly CurrentObjectEditor _currentObjectEditor;

        private GameObject _preview;
        private MeshRenderer _previewMeshRenderer;

        public Preview(ReactiveProperty<LevelBuilderStatus> levelBuilderStatus, Options options, RayCaster rayCaster, Catalog catalog, CreateAvailability createAvailability, CurrentObjectEditor currentObjectEditor, CompositeDisposable disposable)
        {
            _options = options;
            _rayCaster = rayCaster;
            _currentObjectEditor = currentObjectEditor;

            catalog
                .SelectedElement
                .Skip(1)
                .Subscribe(_ => ReCreatePreviewGameobject(_))
                .AddTo(disposable);

            levelBuilderStatus
                .Subscribe(_ =>
                {
                    if (_.HasFlag(LevelBuilderStatus.BuildMode) == false)
                    {
                        if (_preview != null)
                            DestroyImmediate(_preview);
                    }
                });
          
            createAvailability
                .Availability
                .Skip(1)
                .Subscribe(value => SetPreviewMaterial(value))
                .AddTo(disposable);
        }

        public void Draw(SceneView sceneView)
        {
            if (_rayCaster.IsHitGround == true)
            {
                DrawPreview(_rayCaster.HitPoint);
                sceneView.Repaint();
            }
        }

        private void DrawPreview(Vector3 mousePosition)
        {
            if (_preview != null)
            {
                _previewMeshRenderer.enabled = true;
                _preview.transform.position = mousePosition;
                _preview.transform.rotation = _currentObjectEditor.CurrentObject.transform.rotation;
            }
        }

        private void ReCreatePreviewGameobject(GameObject prefab)
        {
            if (_preview != null)
            {
                DestroyImmediate(_preview);
                _previewMeshRenderer = null;
            }

            _preview = Instantiate(prefab);

            _preview.transform.parent = _options.Parent.transform;
            _previewMeshRenderer = _preview.GetComponent<MeshRenderer>();
            _previewMeshRenderer.enabled = false;

            // Change
            SetPreviewMaterial(true);
        }
        private void SetPreviewMaterial(bool buildAvailability)
        {
            if (buildAvailability == true)
            {
                SetMaterial(_options.BuildAllowed);
            }
            else
            {
                SetMaterial(_options.BuildDisallowed);
            }

            void SetMaterial(Material material)
            {
                Material[] copy = new Material[_previewMeshRenderer.sharedMaterials.Length];
                
                for (int i = 0; i < copy.Length; i++)
                {
                    copy[i] = material;
                }
                _previewMeshRenderer.materials = copy;
            }
        }
    }
}