using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UniRx;
using Cysharp.Threading.Tasks;

public class LevelBuilder : EditorWindow
{
    private readonly ReactiveProperty<bool> _isEditorActive = new ReactiveProperty<bool>(true);
    private readonly ReactiveProperty<bool> _isInCreationMode = new ReactiveProperty<bool>();

    private Options _options;
    private Input _input;
    private RayCaster _rayCaster;
    private Catalog _catalog;
    private CreateAvailability _createAvailability;
    private Creator _creation;
    private CurrentObjectEditor _currentObjectEditor;
    private Preview _preview;

    private GameObject _createdObject;

    public readonly CompositeDisposable _disposable = new CompositeDisposable();

    public IReadOnlyReactiveProperty<bool> IsEditorActive => _isEditorActive;
    public IReadOnlyReactiveProperty<bool> IsInCreationMode => _isInCreationMode;

    [MenuItem("Level/Builder")]
    private static void ShowWindow()
    {
        GetWindow(typeof(LevelBuilder));
    }

    private async void Initialize()
    {
        _options = new Options();

        await new WaitUntil(() => _options.IsOptionsApplied.Value == true);

        _input = new Input();
        _rayCaster = new RayCaster();
        _catalog = new Catalog();
        _currentObjectEditor = new CurrentObjectEditor(_input, _catalog, _disposable);
        _createAvailability = new CreateAvailability(_currentObjectEditor, _rayCaster);
        _creation = new Creator(_options, _input, _rayCaster, _currentObjectEditor, _disposable);
        _preview = new Preview(this, _options, _rayCaster, _catalog, _createAvailability, _currentObjectEditor, _disposable);
    }

    private void OnEnable()
    {
        _isEditorActive.Value = true;
        Initialize();
    }
    private void OnDisable()
    {
        _isEditorActive.Value = false;
        _disposable.Clear();
    }

    private void OnFocus()
    {
        if (_isEditorActive.Value == true)
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }
    }

    private void OnGUI()
    {
        if (_isEditorActive.Value == true)
        {
            if (_options.IsOptionsApplied.Value == true)
            {
                if (_createdObject != null)
                {
                    EditorGUILayout.LabelField("Created Object Settings");
                    Transform transformToCreate = _createdObject.transform;
                    transformToCreate.position = EditorGUILayout.Vector3Field("Position", transformToCreate.position);
                    transformToCreate.rotation = Quaternion.Euler(EditorGUILayout.Vector3Field("Position", transformToCreate.rotation.eulerAngles));
                    transformToCreate.localScale = EditorGUILayout.Vector3Field("Position", transformToCreate.localScale);
                }
                            
                _isInCreationMode.Value = GUILayout.Toggle(_isInCreationMode.Value, "Start building", "Button", GUILayout.Height(60));
                _catalog.Draw(position);
            }
            else
            {
                _options.Draw();
            }        
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (_isEditorActive.Value == true)
        {
            if (_options.IsOptionsApplied.Value == true)
            {
                if (_isInCreationMode.Value)
                {
                    _rayCaster.Raycast();
                    _preview.Draw(sceneView);
                    _input.CheckInputs();
                    _createAvailability.Update();
                }
            }
        }
    }

    public class Options
    {
        private readonly ReactiveProperty<bool> _isOptionsApplied = new ReactiveProperty<bool>();

        public IReadOnlyReactiveProperty<bool> IsOptionsApplied => _isOptionsApplied;

        public GameObject Parent { get; private set; }

        public Material BuildAllowed { get; private set; }
        public Material BuildDisallowed { get; private set; }

        public void Draw()
        {
            EditorGUILayout.LabelField("Set up");

            Parent = (GameObject)EditorGUILayout.ObjectField("Parent", Parent, typeof(GameObject), true);

            BuildAllowed = (Material)EditorGUILayout.ObjectField("Build Allowed Material", BuildAllowed, typeof(Material), true);
            BuildDisallowed = (Material)EditorGUILayout.ObjectField("Build Disallowed Material", BuildDisallowed, typeof(Material), true);

            if (GUILayout.Button("Apply"))
            {
                if (Parent != null && BuildAllowed != null && BuildDisallowed != null)
                {
                    _isOptionsApplied.Value = true;
                }
            }
        }
    }
    public class Input
    {
        public MouseKey Left { get; private set; } = new MouseKey(0);

        public KeyboardKey Q { get; private set; } = new KeyboardKey(KeyCode.Q);
        public KeyboardKey E { get; private set; } = new KeyboardKey(KeyCode.E);
        public KeyboardKey LeftShift { get; private set; } = new KeyboardKey(KeyCode.LeftShift);

        public void CheckInputs()
        {
            HandleUtility.AddDefaultControl(0);

            Left.CheckInput();
            
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
        public Vector3 HitPoint { get; private set; }
        public GameObject HitGround { get; private set; }

        public bool IsHitGround => HitGround != null;

        public bool Raycast()
        {
            Ray guiRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

            HitPoint = Vector3.zero;
            HitGround = null;

            if (Physics.Raycast(guiRay, out RaycastHit raycastHit))
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
        private readonly CurrentObjectEditor _currentObjectEditor;
        private readonly RayCaster _rayCaster = new RayCaster();

        private readonly ReactiveProperty<bool> _availability = new ReactiveProperty<bool>();

        public CreateAvailability(CurrentObjectEditor currentObjectEditor, RayCaster rayCaster)
        {
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
            Collider[] hitColliders = Physics.OverlapBox(_rayCaster.HitPoint, halfBoxSize);

            if (hitColliders.Length == 1)
            {
                return true;
            }

            return false;
        }
    }
    public class Creator
    {
        private readonly Options _options;
        private readonly Input _input;
        private readonly RayCaster _rayCaster;
        private readonly Catalog _catalog;
        private readonly CurrentObjectEditor _currentObjectEditor;

        public GameObject LastCreatedObject { get; private set; }

        public Creator(Options options, Input input, RayCaster raycaster, CurrentObjectEditor currentObjectEditor, CompositeDisposable disposable)
        {
            _options = options;
            _input = input;
            _rayCaster = raycaster;
            _currentObjectEditor = currentObjectEditor;

            _input
                .Left
                .KeyDown
                .Skip(1)
                .Where(_ => _ == true)
                .Subscribe(_ => Create(_rayCaster.HitPoint))
                .AddTo(disposable);
        }

        private void Create(Vector3 position)
        {
            LastCreatedObject = Instantiate(_currentObjectEditor.CurrentObject, position, _currentObjectEditor.CurrentObject.transform.rotation);
            LastCreatedObject.transform.parent = _options.Parent.transform;
      
            Undo.RegisterCreatedObjectUndo(LastCreatedObject, "Create Building");
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

        public Preview(LevelBuilder levelBuilder, Options options, RayCaster rayCaster, Catalog catalog, CreateAvailability createAvailability, CurrentObjectEditor currentObjectEditor, CompositeDisposable disposable)
        {
            _options = options;
            _rayCaster = rayCaster;
            _currentObjectEditor = currentObjectEditor;

            catalog
                .SelectedElement
                .Skip(1)
                .Subscribe(_ => ReCreatePreviewGameobject(_))
                .AddTo(disposable);

            levelBuilder
                ._isEditorActive
                .Where(_ => _ == false)
                .Subscribe(_ =>
                {
                    if (_preview != null)
                        DestroyImmediate(_preview);
                })
                .AddTo(disposable);

            levelBuilder
                ._isInCreationMode
                .Where(_ => _ == false)
                .Subscribe(_ =>
                {
                    if (_preview != null)
                        DestroyImmediate(_preview);
                })
                .AddTo(disposable);

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