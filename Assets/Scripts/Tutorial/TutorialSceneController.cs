using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene tutorial độc lập, có thể chạy offline để người mới học cơ chế Mimeto.
/// Scene tự dựng một phòng 3D nhỏ, các trạm kiến thức và UI hướng dẫn bằng OnGUI.
/// Không dùng NetworkVariable nên mỗi người chơi có thể mở/đóng riêng.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialSceneController : MonoBehaviour
{
    [Header("Startup")]
    [SerializeField] private bool autoOpenOnStart = true;
    [SerializeField] private bool buildEnvironmentAtRuntime = true;
    [SerializeField] private string returnScene = "StartGame";
    [SerializeField] private bool autoReturnToStartGame = true;
    [SerializeField] private float returnDelayAfterCompletion = 2.5f;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4.5f;
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float lookSensitivity = 2.2f;
    [SerializeField] private float interactionDistance = 4f;
    [SerializeField] private float crouchCameraDrop = 0.5f;
    [SerializeField] private float crouchTransitionSpeed = 12f;
    [SerializeField] private float crouchControllerHeight = 1.2f;

    [Header("Guided Gameplay Course")]
    [SerializeField] private bool buildGameplayCourse = true;
    [SerializeField] private bool showGuidanceLine = true;
    [SerializeField] private float courseInteractionDistance = 3.2f;

    [Header("Gameplay Asset Mode")]
    [SerializeField] private bool useGameplayAssets;
    [SerializeField] private GameObject gameplayMapPrefab;
    [SerializeField] private GameObject gameplayPlayerInstance;
    [SerializeField] private GameObject gameplayMonsterInstance;
    [SerializeField] private GameObject gameplayCircuitInstance;
    [SerializeField] private GameObject gameplayOxygenInstance;
    [SerializeField] private GameObject gameplaySafeBaseInstance;
    [SerializeField] private GameObject gameplayExitInstance;
    [SerializeField] private Vector3 gameplayPlayerSpawn = new Vector3(-64.1f, 1.2f, -69.3f);
    [SerializeField] private Vector3 gameplayMonsterPosition = new Vector3(-31f, 1.2f, -42f);
    [SerializeField] private Vector3 gameplayCircuitPosition = new Vector3(-54f, 1.2f, -60f);
    [SerializeField] private Vector3 gameplayOxygenPosition = new Vector3(-45f, 1.2f, -52f);
    [SerializeField] private Vector3 gameplaySafeBasePosition = new Vector3(-64.1f, 0.8f, -69.3f);
    [SerializeField] private Vector3 gameplayExitPosition = new Vector3(-65f, 2f, -81.44f);

    private readonly List<TutorialWorldStation> _stations = new List<TutorialWorldStation>();
    private readonly List<GameObject> _runtimeObjects = new List<GameObject>();
    private readonly List<TutorialPage> _pages = new List<TutorialPage>();

    private Camera _camera;
    private CharacterController _characterController;
    private Transform _player;
    private Material _floorMaterial;
    private Material _wallMaterial;
    private GUIStyle _titleStyle;
    private GUIStyle _bodyStyle;
    private GUIStyle _smallStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _stationStyle;
    private Texture2D _whiteTexture;
    private bool _panelOpen;
    private bool _cursorUnlocked;
    private int _pageIndex;
    private float _yaw;
    private float _pitch;
    private string _statusMessage = "Khám phá các trạm màu cyan để học từng phần.";
    private float _statusTimer;
    private TutorialWorldStation _focusedStation;
    private TutorialCollectible _focusedCollectible;
    private TutorialExitTarget _focusedExit;
    private TutorialCollectible _guideCircuit;
    private TutorialCollectible _guideOxygenTank;
    private TutorialSafeZone _guideSafeZone;
    private TutorialMonster _guideMonster;
    private TutorialExitTarget _guideExit;
    private LineRenderer _guidanceLine;
    private Animator _playerAnimator;
    private Light _playerFlashlight;
    private GameObject _gameplayMapRoot;
    private bool _usingGameplayAssets;
    private float _verticalVelocity;
    private Vector3 _cameraStandingLocalPosition;
    private bool _cameraPositionInitialized;
    private float _standingControllerHeight;
    private Vector3 _standingControllerCenter;
    private bool _controllerDimensionsInitialized;
    private Vector3 _monsterLessonPoint;
    private int _guideStep;
    private bool _courseComplete;
    private Coroutine _returnToStartCoroutine;

    private static readonly Color Cyan = new Color(0.05f, 0.9f, 1f, 1f);
    private static readonly Color Green = new Color(0.2f, 1f, 0.55f, 1f);
    private static readonly Color Yellow = new Color(1f, 0.78f, 0.16f, 1f);
    private static readonly Color Red = new Color(1f, 0.25f, 0.3f, 1f);
    private static readonly Color Purple = new Color(0.78f, 0.35f, 1f, 1f);

    [Serializable]
    private sealed class TutorialPage
    {
        public string title;
        public string subtitle;
        public string body;
        public Color accent;

        public TutorialPage(string title, string subtitle, string body, Color accent)
        {
            this.title = title;
            this.subtitle = subtitle;
            this.body = body;
            this.accent = accent;
        }
    }

    private void Awake()
    {
        BuildPages();
        if (buildEnvironmentAtRuntime)
            BuildEnvironment();
    }

    private void Start()
    {
        _panelOpen = autoOpenOnStart;
        SetCursor(_panelOpen);
    }

    /// <summary>
    /// Editor/QA hook used to configure a tutorial scene with the same map,
    /// player and enemy assets as the real gameplay scene.
    /// </summary>
    public void ConfigureGameplayAssets(
        GameObject mapPrefab,
        GameObject playerInstance,
        GameObject monsterInstance,
        GameObject circuitInstance,
        GameObject oxygenInstance,
        GameObject safeBaseInstance,
        GameObject exitInstance)
    {
        useGameplayAssets = true;
        gameplayMapPrefab = mapPrefab;
        gameplayPlayerInstance = playerInstance;
        gameplayMonsterInstance = monsterInstance;
        gameplayCircuitInstance = circuitInstance;
        gameplayOxygenInstance = oxygenInstance;
        gameplaySafeBaseInstance = safeBaseInstance;
        gameplayExitInstance = exitInstance;
    }

    private void Update()
    {
        if (_statusTimer > 0f)
            _statusTimer -= Time.unscaledDeltaTime;

        if (_panelOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                ClosePanel();
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                NextPage();

            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OpenPage(_pageIndex);
            return;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            OpenPage(Mathf.Clamp(_pageIndex, 0, _pages.Count - 1));
            return;
        }

        if (Input.GetKeyDown(KeyCode.F1))
        {
            PlayerPrefs.DeleteKey("Mimeto_TutorialVersion");
            PlayerPrefs.DeleteKey("Mimeto_TutorialSkipped");
            PlayerPrefs.Save();
            ResetGuidedCourseForTesting();
            SetStatus("Đã reset trạng thái tutorial cho lần test tiếp theo.", 3f);
        }

        UpdateLook();
        UpdateMovement();
        UpdateGameplayActions();
        UpdateFocusedStation();
        UpdateGuidedCourse();

        if (Input.GetKeyDown(KeyCode.E))
            InteractWithFocusedObject();
    }

    private void ResetGuidedCourseForTesting()
    {
        if (_returnToStartCoroutine != null)
        {
            StopCoroutine(_returnToStartCoroutine);
            _returnToStartCoroutine = null;
        }

        _courseComplete = false;
        _guideStep = 0;
        _pageIndex = 0;
        _focusedStation = null;
        _focusedCollectible = null;
        _focusedExit = null;

        if (_guideCircuit != null)
            _guideCircuit.ResetForTesting();
        if (_guideOxygenTank != null)
            _guideOxygenTank.ResetForTesting();
        if (_guideMonster != null)
            _guideMonster.SetTrainingActive(false);

        if (_characterController != null && _characterController.enabled)
            UpdateCrouchPose(false);
        UpdateGuideLine();
    }

    private void BuildPages()
    {
        _pages.Clear();
        _pages.Add(new TutorialPage(
            "MIMETO // CHÀO MỪNG",
            "Mục tiêu của game",
            "Bạn cùng đồng đội đi vào khu vực độc hại, thu thập tài nguyên, hoàn thành một nhiệm vụ thoát hiểm ngẫu nhiên và trở về bằng cửa thoát. Nếu chết, vật phẩm của chuyến đi sẽ mất.\n\nScene này là phòng học offline: hãy đi tới các trạm phát sáng, nhìn vào trạm và nhấn E để xem lại từng phần.", Cyan));
        _pages.Add(new TutorialPage(
            "01 // ĐIỀU KHIỂN",
            "Các phím cần nhớ",
            "WASD: di chuyển\nChuột: nhìn\nE: tương tác, nhặt đồ, mở cửa\nLeft Shift: chạy nhanh\nC hoặc Ctrl: cúi\nSpace: nhảy / thoát chỗ trốn\nChuột trái: tấn công\nI: túi đồ\nF: đèn pin\nR: mở mục tiêu thoát\nESC: đóng bảng / mở lại hướng dẫn\n\nNếu đã đổi phím, hãy xem lại Settings để dùng phím hiện tại.", Green));
        _pages.Add(new TutorialPage(
            "02 // WAITING",
            "Phòng chuẩn bị trước chuyến đi",
            "Waiting là nơi cả đội chuẩn bị:\n\n• Shop: mua mặt nạ gas, thuốc, Oxygen Tank và vũ khí.\n• Reclaimer: bán Scrap lấy Energy Credits.\n• InfoBoard: xem mẹo Oxygen, vùng nguy hiểm và Economy.\n• Safe Zone: hồi Oxygen.\n\nBạn không bắt buộc phải mua gì để đi tiếp; hãy phối hợp cả đội và kiểm tra túi trước khi xuất phát.", Yellow));
        _pages.Add(new TutorialPage(
            "03 // BẮT ĐẦU EXPEDITION",
            "Cách từ Waiting vào Map",
            "Khi cả đội sẵn sàng, mọi người đứng trong vùng gần trạm vận chuyển. Khi đủ người trong vùng, server đếm ngược 5 giây rồi tự chuyển sang Map.\n\nHint E trên trạm chỉ là dòng gợi ý cũ; cơ chế hiện tại là đứng cùng nhau trong vùng, không phải nhấn E để bắt đầu.", Cyan));
        _pages.Add(new TutorialPage(
            "04 // SINH TỒN",
            "Oxygen, máu và Stamina",
            "Theo dõi Oxygen, máu và Stamina trên HUD. Trong vùng độc hại, Oxygen giảm nhanh nếu không có mặt nạ. Hết Oxygen sẽ làm mất máu. Safe Zone hồi Oxygen; Oxygen Tank dùng để dự phòng.\n\nChạy nhanh tiêu hao Stamina. Đi bộ hoặc cúi giúp bạn kiểm soát tiếng động và giữ sức lâu hơn.", Green));
        _pages.Add(new TutorialPage(
            "05 // ÂM THANH & KẺ ĐỊCH",
            "Stealth là cách sống sót",
            "Mutant và ExilerAI có thể nghe tiếng động. Chạy, đánh nhau và để nhịp tim tăng sẽ làm bạn dễ bị phát hiện.\n\nCúi người, đứng yên hoặc vào Hiding Spot để giảm nguy cơ. Dùng vũ khí khi cần, nhưng luôn giữ một đường rút về Safe Zone.", Red));
        _pages.Add(new TutorialPage(
            "06 // BỐN CÁCH THOÁT",
            "Mục tiêu được chọn ngẫu nhiên mỗi ván",
            "ASSEMBLY: tìm Gear, Fuel Tank và Circuit Board rồi hoàn thành các bước lắp ráp.\n\nBEACON: thu 2 Circuits + 1 Battery, lắp Beacon và sống sót 180 giây.\n\nCIPHER: tìm 2 ghi chú, ghép mã 4 số và nhập tại Keypad. Nhập sai gây mất máu.\n\nREACTOR: thu 3 Chemicals + 2 Circuits, kích hoạt Reactor và tránh vụ nổ trong bán kính khoảng 50m.", Purple));
        _pages.Add(new TutorialPage(
            "07 // HOÀN THÀNH VÁN",
            "Cửa thoát và dữ liệu",
            "Nhấn R để mở EscapeHUD và xem mục tiêu hiện tại. Khi nhiệm vụ hoàn thành, đi tới cửa thoát và nhấn E.\n\nThắng: vật phẩm được lưu.\nChết hoặc thua: vật phẩm của chuyến đi bị xóa.\n\nSau khi xem xong, đóng bảng và hoàn thành đủ 5 bước thực hành trên Map. Khi mở cửa thoát, scene mới tự về StartGame để tạo phòng và chơi thật.", Cyan));
    }

    private void BuildEnvironment()
    {
        if (useGameplayAssets)
        {
            BuildGameplayAssetEnvironment();
            return;
        }

        _floorMaterial = CreateMaterial("Tutorial Floor", new Color(0.035f, 0.065f, 0.09f));
        _wallMaterial = CreateMaterial("Tutorial Wall", new Color(0.06f, 0.1f, 0.14f));

        CreatePrimitive("TrainingFloor", PrimitiveType.Cube, new Vector3(0f, -0.25f, 0f), new Vector3(36f, 0.5f, 24f), _floorMaterial, transform);
        CreatePrimitive("BackWall", PrimitiveType.Cube, new Vector3(0f, 3f, 11.75f), new Vector3(36f, 6f, 0.5f), _wallMaterial, transform);
        CreatePrimitive("LeftWall", PrimitiveType.Cube, new Vector3(-17.75f, 3f, 0f), new Vector3(0.5f, 6f, 24f), _wallMaterial, transform);
        CreatePrimitive("RightWall", PrimitiveType.Cube, new Vector3(17.75f, 3f, 0f), new Vector3(0.5f, 6f, 24f), _wallMaterial, transform);
        CreatePrimitive("CeilingBeam", PrimitiveType.Cube, new Vector3(0f, 6f, 0f), new Vector3(36f, 0.25f, 24f), _wallMaterial, transform);

        CreateStripe(new Vector3(0f, 0.015f, -4.5f), new Vector3(26f, 0.02f, 0.08f), Cyan);
        CreateStripe(new Vector3(0f, 0.015f, 4.5f), new Vector3(26f, 0.02f, 0.08f), Purple);
        CreateStripe(new Vector3(-9f, 0.015f, 0f), new Vector3(0.08f, 0.02f, 16f), Yellow);
        CreateStripe(new Vector3(9f, 0.015f, 0f), new Vector3(0.08f, 0.02f, 16f), Green);

        CreateStation("WelcomeStation", "01  WELCOME", new Vector3(-10f, 0f, -1.5f), Cyan, 0);
        CreateStation("ControlsStation", "02  CONTROLS", new Vector3(-5f, 0f, 3.4f), Green, 1);
        CreateStation("WaitingStation", "03  WAITING", new Vector3(0f, 0f, 3.4f), Yellow, 2);
        CreateStation("StartStation", "04  START RUN", new Vector3(5f, 0f, 3.4f), Cyan, 3);
        CreateStation("SurvivalStation", "05  SURVIVAL", new Vector3(10f, 0f, -1.5f), Green, 4);
        CreateStation("StealthStation", "06  STEALTH", new Vector3(5f, 0f, -5.5f), Red, 5);
        CreateStation("ObjectiveStation", "07  OBJECTIVES", new Vector3(0f, 0f, -5.5f), Purple, 6);
        CreateStation("FinishStation", "08  EXTRACTION", new Vector3(-5f, 0f, -5.5f), Cyan, 7);

        CreatePrimitive("CentralConsole", PrimitiveType.Cube, new Vector3(0f, 1.2f, -0.7f), new Vector3(3.8f, 2.4f, 1.1f), CreateMaterial("Console", new Color(0.02f, 0.18f, 0.24f)), transform);
        CreateStripe(new Vector3(0f, 2.45f, -1.27f), new Vector3(2.8f, 0.04f, 0.04f), Cyan);

        CreatePlayer();
        CreateLighting();
        if (buildGameplayCourse)
            BuildGameplayCourse();
    }

    private void BuildGameplayAssetEnvironment()
    {
        _usingGameplayAssets = true;

        if (_gameplayMapRoot == null)
        {
            if (gameplayMapPrefab != null)
            {
                _gameplayMapRoot = Instantiate(gameplayMapPrefab);
                _runtimeObjects.Add(_gameplayMapRoot);
            }
            else
            {
                _gameplayMapRoot = GameObject.Find("GameplayMap_Training") ?? GameObject.Find("Map");
            }
        }

        if (_gameplayMapRoot != null)
        {
            _gameplayMapRoot.name = "GameplayMap_Training";
            _gameplayMapRoot.transform.SetParent(transform, true);
            DisableGameplayNetworkScripts(_gameplayMapRoot);
        }

        CreatePlayer();
        CreateLighting();
        if (buildGameplayCourse)
            BuildGameplayCourse();
    }

    private void CreatePlayer()
    {
        if (_usingGameplayAssets)
        {
            SetupGameplayPlayer();
            return;
        }

        var playerObject = new GameObject("TutorialPlayer");
        playerObject.transform.SetParent(transform, false);
        playerObject.transform.position = new Vector3(0f, 1.05f, -9f);
        _player = playerObject.transform;
        _characterController = playerObject.AddComponent<CharacterController>();
        _characterController.height = 1.8f;
        _characterController.radius = 0.32f;
        _characterController.center = new Vector3(0f, 0.9f, 0f);
        _characterController.stepOffset = 0.3f;
        _standingControllerHeight = _characterController.height;
        _standingControllerCenter = _characterController.center;
        _controllerDimensionsInitialized = true;

        var existingCamera = Camera.main;
        var cameraObject = existingCamera != null ? existingCamera.gameObject : new GameObject("TutorialCamera");
        cameraObject.transform.SetParent(_player, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0.72f, 0f);
        cameraObject.transform.localRotation = Quaternion.identity;
        _camera = existingCamera != null ? existingCamera : cameraObject.AddComponent<Camera>();
        _cameraStandingLocalPosition = _camera.transform.localPosition;
        _cameraPositionInitialized = true;
        _camera.tag = "MainCamera";
        _camera.fieldOfView = 72f;
        _camera.nearClipPlane = 0.05f;
        _camera.farClipPlane = 100f;
        if (cameraObject.GetComponent<AudioListener>() == null)
            cameraObject.AddComponent<AudioListener>();
    }

    private void SetupGameplayPlayer()
    {
        GameObject playerObject = gameplayPlayerInstance;
        if (playerObject == null)
        {
            Debug.LogError("[Tutorial] Gameplay asset mode is enabled but no Player instance was assigned.");
            return;
        }

        playerObject.name = "TutorialPlayer";
        playerObject.transform.SetParent(transform, true);
        playerObject.transform.position = gameplayPlayerSpawn;
        playerObject.SetActive(true);
        // PlayerInput/NetworkBehaviour can re-enable themselves from OnEnable;
        // run the offline filter after activation so the tutorial stays local.
        DisableGameplayNetworkScripts(playerObject);

        _player = playerObject.transform;
        _characterController = playerObject.GetComponent<CharacterController>();
        if (_characterController == null)
            _characterController = playerObject.AddComponent<CharacterController>();
        _characterController.enabled = true;
        _standingControllerHeight = _characterController.height;
        _standingControllerCenter = _characterController.center;
        _controllerDimensionsInitialized = _standingControllerHeight > 0f;

        _playerAnimator = playerObject.GetComponentInChildren<Animator>(true);
        _playerFlashlight = playerObject.GetComponentInChildren<Light>(true);
        foreach (var light in playerObject.GetComponentsInChildren<Light>(true))
        {
            if (light.name.IndexOf("UV", StringComparison.OrdinalIgnoreCase) >= 0
                || light.name.IndexOf("Flash", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _playerFlashlight = light;
                break;
            }
        }
        Camera selectedCamera = null;
        foreach (var candidate in playerObject.GetComponentsInChildren<Camera>(true))
        {
            if (candidate.name == "PlayerCamera" || selectedCamera == null)
                selectedCamera = candidate;
        }

        if (selectedCamera == null)
        {
            var cameraObject = new GameObject("PlayerCamera");
            cameraObject.transform.SetParent(_player, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            selectedCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        _camera = selectedCamera;
        _cameraStandingLocalPosition = _camera.transform.localPosition;
        _cameraPositionInitialized = true;
        _camera.tag = "MainCamera";
        _camera.enabled = true;
        _camera.fieldOfView = 60f;

        foreach (var candidate in FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (candidate != _camera)
                candidate.gameObject.SetActive(false);
        }
    }

    private void DisableGameplayNetworkScripts(GameObject root)
    {
        if (root == null)
            return;

        foreach (var behaviour in root.GetComponentsInChildren<Behaviour>(true))
        {
            string typeName = behaviour.GetType().Name;
            bool disable = typeName.Contains("Network", StringComparison.OrdinalIgnoreCase)
                || typeName == "PlayerController"
                || typeName == "PlayerInventory"
                || typeName == "PlayerSurvival"
                || typeName == "InteractionSystem"
                || typeName == "InventoryUI"
                || typeName == "InventoryUISetup"
                || typeName == "EquipmentManager"
                || typeName == "PlayerInput"
                || typeName == "PlayerSpawnHandler"
                || typeName == "NavMeshAgent"
                || typeName == "MutantAI"
                || typeName == "MonsterAudioEmitter"
                || typeName == "ItemSpawner"
                || typeName == "MimicSpawner"
                || typeName == "MutantSpawner"
                || typeName == "RandomEventManager"
                || typeName == "BaseSetup"
                || typeName == "ExtractionSystem"
                || typeName == "EscapeHUD"
                || typeName == "EscapeRandomizer"
                || typeName == "GameManager"
                || typeName == "EscapeManager";

            if (disable)
                behaviour.enabled = false;
        }

        foreach (var collider in root.GetComponentsInChildren<CapsuleCollider>(true))
            collider.enabled = false;
    }

    private void CreateLighting()
    {
        var directional = GameObject.Find("Directional Light");
        if (directional == null)
        {
            directional = new GameObject("TutorialKeyLight");
            directional.transform.SetParent(transform, false);
        }
        directional.transform.rotation = Quaternion.Euler(50f, -25f, 0f);
        var sun = directional.GetComponent<Light>() ?? directional.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 0.45f;
        sun.color = new Color(0.55f, 0.78f, 1f);

        CreatePointLight("CyanGuideLight", new Vector3(-9f, 3.2f, -1.5f), Cyan, 5f, 7f);
        CreatePointLight("PurpleGuideLight", new Vector3(0f, 3.2f, -5.5f), Purple, 5f, 7f);
        CreatePointLight("GreenGuideLight", new Vector3(10f, 3.2f, -1.5f), Green, 5f, 7f);
    }

    private void BuildGameplayCourse()
    {
        if (_usingGameplayAssets)
        {
            BuildGameplayAssetCourse();
            return;
        }

        // Khu thực hành mô phỏng một đoạn Map: nhặt item → lấy Oxygen Tank
        // → vào Safe Zone → cúi tránh quái → tới cửa thoát.
        CreatePrimitive("ToxicFloor", PrimitiveType.Cube, new Vector3(0f, 0.02f, 2.7f), new Vector3(23f, 0.06f, 7.2f), CreateMaterial("Toxic Floor", new Color(0.16f, 0.045f, 0.055f)), transform);
        CreatePrimitive("SafeFloor", PrimitiveType.Cylinder, new Vector3(-8f, 0.07f, -0.7f), new Vector3(3.5f, 0.08f, 3.5f), CreateMaterial("Safe Zone Floor", Green), transform);
        CreatePrimitive("MonsterLaneLeft", PrimitiveType.Cube, new Vector3(2.7f, 1.2f, 4.9f), new Vector3(0.18f, 2.4f, 4.2f), CreateMaterial("Monster Lane Light", Red), transform);
        CreatePrimitive("MonsterLaneRight", PrimitiveType.Cube, new Vector3(12.6f, 1.2f, 4.9f), new Vector3(0.18f, 2.4f, 4.2f), CreateMaterial("Monster Lane Light", Red), transform);

        _guideCircuit = CreateCollectible("CircuitBoard", "Circuit Board", "Vật phẩm mẫu: nhìn vào item và nhấn E để nhặt.", new Vector3(-8f, 0.85f, -6.2f), Cyan);
        _guideOxygenTank = CreateCollectible("OxygenTank", "Oxygen Tank", "Bình Oxygen dự phòng cho vùng độc hại.", new Vector3(-8f, 0.85f, -3.2f), Green);

        var safeZoneObject = new GameObject("SafeZone_Practice");
        safeZoneObject.transform.SetParent(transform, false);
        safeZoneObject.transform.position = new Vector3(-8f, 0f, -0.7f);
        _guideSafeZone = safeZoneObject.AddComponent<TutorialSafeZone>();
        _guideSafeZone.radius = 2.2f;
        _guideSafeZone.accentColor = Green;
        var safeTrigger = safeZoneObject.AddComponent<SphereCollider>();
        safeTrigger.isTrigger = true;
        safeTrigger.radius = 2.2f;

        _monsterLessonPoint = new Vector3(5.5f, 0f, 4.9f);
        _guideMonster = CreateMonster(new Vector3(8f, 0f, 4.9f));
        _guideMonster.Configure(_player);
        _guideMonster.SetTrainingActive(false);

        var exitObject = new GameObject("TutorialExitDoor");
        exitObject.transform.SetParent(transform, false);
        exitObject.transform.position = new Vector3(0f, 1.5f, 10.55f);
        _guideExit = exitObject.AddComponent<TutorialExitTarget>();
        _guideExit.exitHint = "Nhấn E để hoàn thành bài thực hành";
        _guideExit.accentColor = Cyan;
        var exitTrigger = exitObject.AddComponent<BoxCollider>();
        exitTrigger.isTrigger = true;
        exitTrigger.size = new Vector3(2.4f, 3f, 1.4f);

        CreatePrimitive("ExitPillarLeft", PrimitiveType.Cube, new Vector3(-1.55f, 1.7f, 10.55f), new Vector3(0.35f, 3.4f, 0.5f), CreateMaterial("Exit Frame", Cyan), transform);
        CreatePrimitive("ExitPillarRight", PrimitiveType.Cube, new Vector3(1.55f, 1.7f, 10.55f), new Vector3(0.35f, 3.4f, 0.5f), CreateMaterial("Exit Frame", Cyan), transform);
        CreatePrimitive("ExitHeader", PrimitiveType.Cube, new Vector3(0f, 3.35f, 10.55f), new Vector3(3.45f, 0.35f, 0.5f), CreateMaterial("Exit Frame", Cyan), transform);

        var lineObject = new GameObject("GuidanceLine");
        lineObject.transform.SetParent(transform, false);
        _runtimeObjects.Add(lineObject);
        _guidanceLine = lineObject.AddComponent<LineRenderer>();
        _guidanceLine.useWorldSpace = true;
        _guidanceLine.positionCount = 2;
        _guidanceLine.startWidth = 0.11f;
        _guidanceLine.endWidth = 0.055f;
        _guidanceLine.numCornerVertices = 4;
        _guidanceLine.numCapVertices = 4;
        _guidanceLine.material = CreateMaterial("Guidance Line", Cyan);
        UpdateGuideLine();
    }

    private void BuildGameplayAssetCourse()
    {
        // These are real gameplay prefabs/scene assets. Only the tutorial
        // interaction and local training logic are added around them.
        _monsterLessonPoint = gameplayMonsterPosition;
        _guideCircuit = SetupGameplayCollectible(gameplayCircuitInstance, "Circuit Board", "Circuit Board thật: nhìn vào item và nhấn E để nhặt.", gameplayCircuitPosition, Cyan);
        _guideOxygenTank = SetupGameplayCollectible(gameplayOxygenInstance, "Oxygen Tank", "Vật phẩm dự phòng cho vùng độc hại.", gameplayOxygenPosition, Green);

        _guideSafeZone = SetupGameplaySafeZone();
        _guideMonster = SetupGameplayMonster();
        _guideExit = SetupGameplayExit();

        var lineObject = new GameObject("GuidanceLine");
        lineObject.transform.SetParent(transform, false);
        _runtimeObjects.Add(lineObject);
        _guidanceLine = lineObject.AddComponent<LineRenderer>();
        _guidanceLine.useWorldSpace = true;
        _guidanceLine.positionCount = 2;
        _guidanceLine.startWidth = 0.16f;
        _guidanceLine.endWidth = 0.08f;
        _guidanceLine.numCornerVertices = 6;
        _guidanceLine.numCapVertices = 6;
        _guidanceLine.material = CreateMaterial("Gameplay Guidance Line", Cyan);
        UpdateGuideLine();
    }

    private TutorialCollectible SetupGameplayCollectible(GameObject itemObject, string itemName, string description, Vector3 position, Color color)
    {
        if (itemObject == null)
        {
            Debug.LogError("[Tutorial] Missing gameplay item instance for " + itemName);
            return null;
        }

        itemObject.name = itemName.Replace(" ", "");
        itemObject.transform.SetParent(transform, true);
        itemObject.transform.position = position;
        DisableGameplayNetworkScripts(itemObject);
        itemObject.SetActive(true);

        var collectible = itemObject.GetComponent<TutorialCollectible>();
        if (collectible == null)
            collectible = itemObject.AddComponent<TutorialCollectible>();
        collectible.itemName = itemName;
        collectible.itemDescription = description;
        collectible.accentColor = color;
        return collectible;
    }

    private TutorialSafeZone SetupGameplaySafeZone()
    {
        GameObject safeObject = gameplaySafeBaseInstance;
        if (safeObject == null)
        {
            safeObject = new GameObject("SafeZone_Practice");
            safeObject.transform.SetParent(transform, false);
        }

        safeObject.name = "SafeZone_Practice";
        safeObject.transform.SetParent(transform, true);
        safeObject.transform.position = gameplaySafeBasePosition;
        DisableGameplayNetworkScripts(safeObject);
        safeObject.SetActive(true);

        var safeZone = safeObject.GetComponent<TutorialSafeZone>();
        if (safeZone == null)
            safeZone = safeObject.AddComponent<TutorialSafeZone>();
        safeZone.radius = 5f;
        safeZone.accentColor = Green;

        var trigger = safeObject.GetComponent<SphereCollider>();
        if (trigger == null)
            trigger = safeObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = safeZone.radius;
        return safeZone;
    }

    private TutorialMonster SetupGameplayMonster()
    {
        if (gameplayMonsterInstance == null)
        {
            Debug.LogError("[Tutorial] Missing EnamiMutant gameplay prefab instance.");
            return null;
        }

        gameplayMonsterInstance.name = "TutorialMutant_Enami";
        gameplayMonsterInstance.transform.SetParent(transform, true);
        gameplayMonsterInstance.transform.position = gameplayMonsterPosition;
        DisableGameplayNetworkScripts(gameplayMonsterInstance);
        gameplayMonsterInstance.SetActive(true);

        var monster = gameplayMonsterInstance.GetComponent<TutorialMonster>();
        if (monster == null)
            monster = gameplayMonsterInstance.AddComponent<TutorialMonster>();
        monster.patrolDistance = 7f;
        monster.patrolSpeed = 1.1f;
        monster.chaseSpeed = 4.2f;
        monster.detectionRadius = 12f;
        monster.catchDistance = 1.75f;
        monster.Configure(_player);
        monster.SetTrainingActive(false);
        return monster;
    }

    private TutorialExitTarget SetupGameplayExit()
    {
        GameObject exitObject = gameplayExitInstance;
        if (exitObject == null)
        {
            exitObject = new GameObject("TutorialExitDoor");
            exitObject.transform.SetParent(transform, false);
        }

        exitObject.name = "TutorialExitDoor";
        exitObject.transform.SetParent(transform, true);
        exitObject.transform.position = gameplayExitPosition;
        DisableGameplayNetworkScripts(exitObject);
        exitObject.SetActive(true);

        var exit = exitObject.GetComponent<TutorialExitTarget>();
        if (exit == null)
            exit = exitObject.AddComponent<TutorialExitTarget>();
        exit.exitHint = "Nhấn E để hoàn thành bài thực hành";
        exit.accentColor = Cyan;

        var collider = exitObject.GetComponent<Collider>();
        if (collider == null)
        {
            var box = exitObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(3f, 3f, 2f);
        }
        return exit;
    }

    private TutorialCollectible CreateCollectible(string objectName, string itemName, string description, Vector3 position, Color color)
    {
        var itemObject = CreatePrimitive(objectName, PrimitiveType.Cube, position, new Vector3(0.65f, 0.65f, 0.65f), CreateMaterial(objectName + " Material", color), transform);
        var collectible = itemObject.AddComponent<TutorialCollectible>();
        collectible.itemName = itemName;
        collectible.itemDescription = description;
        collectible.accentColor = color;
        CreatePointLight(objectName + " Light", position + Vector3.up * 0.8f, color, 2.5f, 2.5f);
        return collectible;
    }

    private TutorialMonster CreateMonster(Vector3 position)
    {
        var monsterObject = CreatePrimitive("TutorialMutant", PrimitiveType.Capsule, position + Vector3.up * 1.25f, new Vector3(1.1f, 1.25f, 1.1f), CreateMaterial("Mutant Demo", new Color(0.42f, 0.04f, 0.06f)), transform);
        var monster = monsterObject.AddComponent<TutorialMonster>();
        monster.patrolDistance = 2.8f;
        monster.patrolSpeed = 1.2f;
        monster.chaseSpeed = 3.6f;
        monster.detectionRadius = 7f;
        monster.catchDistance = 1.25f;
        CreatePrimitive("MutantEye", PrimitiveType.Sphere, position + Vector3.up * 2.15f + Vector3.forward * 0.48f, new Vector3(0.18f, 0.18f, 0.18f), CreateMaterial("Mutant Eye", Red), monsterObject.transform);
        CreatePointLight("Mutant Warning Light", position + Vector3.up * 2.2f, Red, 3f, 2.5f);
        return monster;
    }

    private void UpdateGuidedCourse()
    {
        if (!buildGameplayCourse || _player == null || _courseComplete)
            return;

        if (_guideStep == 0 && _guideCircuit != null && _guideCircuit.IsCollected)
        {
            _guideStep = 1;
            SetStatus("Đã nhặt Circuit Board. Tiếp theo hãy nhặt Oxygen Tank bằng E.", 4f);
            UpdateGuideLine();
        }
        else if (_guideStep == 1 && _guideOxygenTank != null && _guideOxygenTank.IsCollected)
        {
            _guideStep = 2;
            SetStatus("Đã có Oxygen Tank. Hãy đi vào vòng xanh Safe Zone.", 4f);
            UpdateGuideLine();
        }
        else if (_guideStep == 2 && _guideSafeZone != null && _guideSafeZone.Contains(_player.position))
        {
            _guideStep = 3;
            if (_guideMonster != null)
                _guideMonster.SetTrainingActive(true);
            SetStatus("Safe Zone đã ghi nhận. Tiếp cận quái và giữ C/Ctrl để cúi tránh bị phát hiện.", 5f);
            UpdateGuideLine();
        }
        else if (_guideStep == 3 && Vector3.Distance(_player.position, _monsterLessonPoint) <= 2.3f && IsCrouching())
        {
            _guideStep = 4;
            if (_guideMonster != null)
                _guideMonster.SetTrainingActive(false);
            SetStatus("Bạn đã thực hành cúi tránh quái. Đi theo line tới cửa thoát và nhấn E.", 5f);
            UpdateGuideLine();
        }

        UpdateGuideLine();
    }

    public void HandleMonsterCaught()
    {
        if (_courseComplete || _player == null || _guideStep < 3)
            return;

        _characterController.enabled = false;
        Vector3 checkpoint = _usingGameplayAssets
            ? new Vector3(gameplaySafeBasePosition.x, gameplayPlayerSpawn.y, gameplaySafeBasePosition.z)
            : new Vector3(-8f, 1.05f, -0.7f);
        _player.position = checkpoint;
        _characterController.enabled = true;
        if (_guideMonster != null)
            _guideMonster.SetTrainingActive(false);
        SetStatus("Mutant đã bắt được bạn. Đây là checkpoint Safe Zone; hãy thử cúi và đi chậm hơn.", 5f);
    }

    private void InteractWithFocusedObject()
    {
        if (_focusedStation != null)
        {
            OpenPage(_focusedStation.pageIndex);
            return;
        }

        if (_focusedCollectible != null && !_focusedCollectible.IsCollected)
        {
            _focusedCollectible.Collect();
            SetStatus("Đã nhận " + _focusedCollectible.itemName + ". " + _focusedCollectible.itemDescription, 4f);
            UpdateGuidedCourse();
            return;
        }

        if (_focusedExit != null)
        {
            if (_guideStep < 4)
            {
                SetStatus("Cửa chưa sẵn sàng. Hãy đi theo đường line và hoàn thành từng bước trước.", 4f);
                return;
            }

            _courseComplete = true;
            PlayerPrefs.SetInt("Mimeto_TutorialVersion", 1);
            PlayerPrefs.Save();
            SetStatus("Hoàn thành bài thực hành! Bạn đã biết cách nhặt item, dùng Safe Zone, né quái và thoát Map.", 6f);
            OpenPage(7);
            QueueReturnToStartGame();
        }
    }

    private void UpdateGuideLine()
    {
        if (_guidanceLine == null || !showGuidanceLine || _player == null)
            return;

        Vector3 target;
        switch (_guideStep)
        {
            case 0: target = _guideCircuit != null ? _guideCircuit.transform.position : _player.position; break;
            case 1: target = _guideOxygenTank != null ? _guideOxygenTank.transform.position : _player.position; break;
            case 2: target = _guideSafeZone != null ? _guideSafeZone.transform.position : _player.position; break;
            case 3: target = _monsterLessonPoint + Vector3.up * 0.1f; break;
            default: target = _guideExit != null ? _guideExit.transform.position : _player.position; break;
        }

        _guidanceLine.positionCount = 2;
        _guidanceLine.SetPosition(0, _player.position + Vector3.up * 0.05f);
        _guidanceLine.SetPosition(1, target + Vector3.up * 0.05f);
        _guidanceLine.enabled = !_courseComplete && showGuidanceLine;
    }

    private bool IsCrouching()
    {
        return Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
    }

    private void CreatePointLight(string objectName, Vector3 position, Color color, float range, float intensity)
    {
        var lightObject = new GameObject(objectName);
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.position = position;
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
    }

    private void CreateStation(string objectName, string title, Vector3 position, Color color, int page)
    {
        var root = new GameObject(objectName);
        root.transform.SetParent(transform, false);
        root.transform.position = position;
        var station = root.AddComponent<TutorialWorldStation>();
        station.Configure(page, title, color);
        _stations.Add(station);

        CreatePrimitive("Pedestal", PrimitiveType.Cube, position + Vector3.up * 0.55f, new Vector3(1.35f, 1.1f, 0.85f), CreateMaterial(objectName + " Pedestal", new Color(0.035f, 0.09f, 0.12f)), root.transform);
        var screen = CreatePrimitive("Screen", PrimitiveType.Cube, position + Vector3.up * 1.45f, new Vector3(1.05f, 0.65f, 0.08f), CreateMaterial(objectName + " Screen", color), root.transform);
        screen.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        CreatePrimitive("Beacon", PrimitiveType.Cylinder, position + Vector3.up * 2.05f, new Vector3(0.18f, 0.38f, 0.18f), CreateMaterial(objectName + " Beacon", color), root.transform);
        CreatePointLight(objectName + " Light", position + Vector3.up * 2.1f, color, 3.5f, 3.5f);
    }

    private GameObject CreatePrimitive(string objectName, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Material material, Transform parent)
    {
        var primitive = GameObject.CreatePrimitive(primitiveType);
        primitive.name = objectName;
        primitive.transform.SetParent(parent, true);
        primitive.transform.position = position;
        primitive.transform.localScale = scale;
        if (material != null)
            primitive.GetComponent<Renderer>().sharedMaterial = material;
        _runtimeObjects.Add(primitive);
        return primitive;
    }

    private void CreateStripe(Vector3 position, Vector3 scale, Color color)
    {
        CreatePrimitive("FloorGuide", PrimitiveType.Cube, position, scale, CreateMaterial("Guide " + color, color), transform);
    }

    private Material CreateMaterial(string materialName, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
        var material = new Material(shader) { name = materialName, color = color };
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.4f);
        }
        return material;
    }

    private void UpdateLook()
    {
        if (_camera == null || _player == null)
            return;

        float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;
        _yaw += mouseX;
        _pitch = Mathf.Clamp(_pitch - mouseY, -80f, 80f);
        _player.rotation = Quaternion.Euler(0f, _yaw, 0f);
        _camera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    private void UpdateMovement()
    {
        if (_characterController == null)
            return;

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 input = Vector3.ClampMagnitude(new Vector3(horizontal, 0f, vertical), 1f);
        bool crouching = IsCrouching();
        bool sprinting = Input.GetKey(KeyCode.LeftShift) && !crouching;
        UpdateCrouchPose(crouching);
        float speed = crouching ? walkSpeed * 0.5f : (sprinting ? sprintSpeed : walkSpeed);
        Vector3 move = _player.TransformDirection(input) * speed;
        if (_characterController.isGrounded)
        {
            _verticalVelocity = -2f;
            if (Input.GetKeyDown(KeyCode.Space) && !crouching)
            {
                _verticalVelocity = Mathf.Sqrt(1.5f * -2f * -9.81f);
                _playerAnimator?.SetTrigger("Jump");
            }
        }
        else
        {
            _verticalVelocity += -9.81f * Time.deltaTime;
        }
        move.y = _verticalVelocity;
        _characterController.Move(move * Time.deltaTime);

        if (_playerAnimator != null)
        {
            float animationScale = sprinting ? 1.5f : (crouching ? 0.5f : 1f);
            float targetX = _characterController.isGrounded ? input.x * animationScale : 0f;
            float targetY = _characterController.isGrounded ? input.z * animationScale : 0f;
            _playerAnimator.SetFloat("InputX", Mathf.Lerp(_playerAnimator.GetFloat("InputX"), targetX, Time.deltaTime * 8f));
            _playerAnimator.SetFloat("InputY", Mathf.Lerp(_playerAnimator.GetFloat("InputY"), targetY, Time.deltaTime * 8f));
            _playerAnimator.SetBool("isSneaking", crouching);
            _playerAnimator.SetBool("isGrounded", _characterController.isGrounded);
        }
    }

    private void UpdateCrouchPose(bool crouching)
    {
        if (_camera != null && _cameraPositionInitialized)
        {
            Vector3 targetCameraPosition = _cameraStandingLocalPosition;
            if (crouching)
                targetCameraPosition.y -= Mathf.Max(0f, crouchCameraDrop);

            _camera.transform.localPosition = Vector3.Lerp(
                _camera.transform.localPosition,
                targetCameraPosition,
                Time.deltaTime * Mathf.Max(1f, crouchTransitionSpeed));
        }

        if (_characterController == null || !_controllerDimensionsInitialized)
            return;

        float targetHeight = crouching
            ? Mathf.Clamp(crouchControllerHeight, _characterController.radius * 2f, _standingControllerHeight)
            : _standingControllerHeight;
        float heightDelta = _standingControllerHeight - targetHeight;
        Vector3 targetCenter = _standingControllerCenter;
        targetCenter.y -= heightDelta * 0.5f;

        _characterController.height = Mathf.Lerp(
            _characterController.height,
            targetHeight,
            Time.deltaTime * Mathf.Max(1f, crouchTransitionSpeed));
        _characterController.center = Vector3.Lerp(
            _characterController.center,
            targetCenter,
            Time.deltaTime * Mathf.Max(1f, crouchTransitionSpeed));
    }

    private void UpdateGameplayActions()
    {
        if (Input.GetKeyDown(KeyCode.F) && _playerFlashlight != null)
            _playerFlashlight.enabled = !_playerFlashlight.enabled;

        if (Input.GetMouseButtonDown(0) && _playerAnimator != null)
            _playerAnimator.SetTrigger("Punch1");

        if (Input.GetKeyDown(KeyCode.I))
            SetStatus("Túi đồ tutorial: Circuit Board và Oxygen Tank được ghi nhận theo từng bước.", 4f);
    }

    private void UpdateFocusedStation()
    {
        _focusedStation = null;
        _focusedCollectible = null;
        _focusedExit = null;
        if (_camera == null)
            return;

        Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Max(interactionDistance, courseInteractionDistance)))
        {
            _focusedStation = hit.collider.GetComponentInParent<TutorialWorldStation>();
            _focusedCollectible = hit.collider.GetComponentInParent<TutorialCollectible>();
            _focusedExit = hit.collider.GetComponentInParent<TutorialExitTarget>();
        }
    }

    private void OpenPage(int page)
    {
        _pageIndex = Mathf.Clamp(page, 0, _pages.Count - 1);
        _panelOpen = true;
        SetCursor(true);
    }

    private void NextPage()
    {
        if (_pageIndex < _pages.Count - 1)
        {
            _pageIndex++;
            return;
        }

        // Đọc hết các trang không đồng nghĩa đã hoàn thành bài thực hành trên Map.
        // Chỉ bước tương tác với cửa thoát sau khi _guideStep == 4 mới được phép
        // đánh dấu tutorial hoàn tất và tự chuyển về StartGame.
        if (!_courseComplete)
        {
            SetStatus("Bạn đã xem hết phần hướng dẫn. Hãy đóng bảng và hoàn thành đủ 5 bước trên Map trước khi quay về StartGame.", 5f);
            ClosePanel();
            return;
        }

        SetStatus("Đã hoàn thành hướng dẫn. Bạn có thể mở lại bằng R hoặc nhấn E tại các trạm.", 4f);
        ClosePanel();
        QueueReturnToStartGame();
    }

    private void PreviousPage()
    {
        if (_pageIndex > 0)
            _pageIndex--;
    }

    private void ClosePanel()
    {
        _panelOpen = false;
        SetCursor(false);
    }

    private void SetCursor(bool unlocked)
    {
        _cursorUnlocked = unlocked;
        Cursor.visible = unlocked;
        Cursor.lockState = unlocked ? CursorLockMode.None : CursorLockMode.Locked;
    }

    private void SetStatus(string message, float duration)
    {
        _statusMessage = message;
        _statusTimer = duration;
    }

    private void EnsureStyles()
    {
        if (_whiteTexture == null)
        {
            _whiteTexture = new Texture2D(1, 1);
            _whiteTexture.SetPixel(0, 0, Color.white);
            _whiteTexture.Apply();
        }

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true
        };
        _bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 21,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
            richText = true
        };
        _smallStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true
        };
        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        _stationStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
    }

    private void OnGUI()
    {
        EnsureStyles();
        DrawTopBar();
        DrawCourseHud();
        DrawStationLabels();

        if (!_panelOpen)
        {
            DrawCrosshairAndHint();
            DrawQuickActions();
            return;
        }

        DrawTutorialPanel();
    }

    private void DrawTopBar()
    {
        GUI.color = new Color(0.01f, 0.025f, 0.05f, 0.92f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, 62f), _whiteTexture);
        GUI.color = Cyan;
        GUI.Label(new Rect(28f, 8f, Screen.width * 0.55f, 42f), "MIMETO // TRAINING FACILITY", _titleStyle);
        GUI.color = new Color(0.75f, 0.9f, 1f);
        GUI.Label(new Rect(Screen.width - 420f, 16f, 390f, 30f), "SCENE TUTORIAL OFFLINE  •  TEST BUILD", _smallStyle);
        GUI.color = Color.white;
    }

    private void DrawStationLabels()
    {
        if (_camera == null)
            return;

        foreach (var station in _stations)
        {
            if (station == null)
                continue;
            Vector3 screen = _camera.WorldToScreenPoint(station.transform.position + Vector3.up * 2.65f);
            if (screen.z <= 0f)
                continue;

            float x = screen.x - 90f;
            float y = Screen.height - screen.y - 12f;
            GUI.color = station.accentColor;
            GUI.Label(new Rect(x, y, 180f, 28f), station.stationTitle, _stationStyle);
        }
        GUI.color = Color.white;
    }

    private void DrawCrosshairAndHint()
    {
        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        GUI.color = new Color(0.7f, 1f, 1f, 0.9f);
        GUI.DrawTexture(new Rect(cx - 1f, cy - 9f, 2f, 18f), _whiteTexture);
        GUI.DrawTexture(new Rect(cx - 9f, cy - 1f, 18f, 2f), _whiteTexture);

        GUI.color = new Color(0f, 0f, 0f, 0.78f);
        GUI.DrawTexture(new Rect(25f, Screen.height - 100f, Screen.width - 50f, 62f), _whiteTexture);
        GUI.color = Cyan;
        string hint;
        if (_focusedCollectible != null && !_focusedCollectible.IsCollected)
            hint = "[E]  Nhặt " + _focusedCollectible.itemName;
        else if (_focusedExit != null)
            hint = "[E]  " + _focusedExit.exitHint;
        else if (_focusedStation != null)
            hint = "[E]  " + _focusedStation.interactHint + "   •   " + _focusedStation.stationTitle;
        else
            hint = "WASD di chuyển   •   Chuột nhìn   •   E tương tác   •   R mở hướng dẫn   •   ESC mở/đóng bảng";
        GUI.Label(new Rect(45f, Screen.height - 88f, Screen.width - 90f, 36f), hint, _smallStyle);

        if (_statusTimer > 0f)
        {
            GUI.color = Green;
            GUI.Label(new Rect(45f, Screen.height - 136f, Screen.width - 90f, 30f), _statusMessage, _smallStyle);
        }
        GUI.color = Color.white;
    }

    private void DrawCourseHud()
    {
        if (!buildGameplayCourse || _panelOpen)
            return;

        string[] steps =
        {
            "1/5  Đi theo line và nhặt Circuit Board bằng E",
            "2/5  Nhặt Oxygen Tank bằng E",
            "3/5  Đi vào vòng xanh Safe Zone",
            "4/5  Giữ C/Ctrl để cúi qua vùng quái",
            "5/5  Đi tới cửa và nhấn E để thoát",
            "HOÀN TẤT  Bạn đã hoàn thành bài thực hành"
        };
        int displayStep = Mathf.Clamp(_guideStep, 0, steps.Length - 2);
        if (_courseComplete)
            displayStep = steps.Length - 1;

        GUI.color = new Color(0.01f, 0.035f, 0.06f, 0.9f);
        GUI.DrawTexture(new Rect(25f, 78f, 520f, 68f), _whiteTexture);
        GUI.color = _courseComplete ? Green : Cyan;
        GUI.Label(new Rect(42f, 86f, 485f, 28f), "GUIDED GAMEPLAY // MỤC TIÊU", _smallStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(42f, 113f, 485f, 25f), steps[displayStep], _smallStyle);
        GUI.color = Color.white;
    }

    private void DrawQuickActions()
    {
        float width = 210f;
        float x = Screen.width - width - 28f;
        float y = 82f;
        if (GUI.Button(new Rect(x, y, width, 42f), "MỞ HƯỚNG DẪN [R]", _buttonStyle))
            OpenPage(_pageIndex);
        if (GUI.Button(new Rect(x, y + 50f, width, 42f), "VỀ START GAME", _buttonStyle))
            ReturnToStartGame();
    }

    private void DrawTutorialPanel()
    {
        var page = _pages[Mathf.Clamp(_pageIndex, 0, _pages.Count - 1)];
        GUI.color = new Color(0f, 0f, 0f, 0.78f);
        GUI.DrawTexture(new Rect(0f, 62f, Screen.width, Screen.height - 62f), _whiteTexture);

        float panelWidth = Mathf.Min(1060f, Screen.width - 100f);
        float panelHeight = Mathf.Min(700f, Screen.height - 130f);
        float px = (Screen.width - panelWidth) * 0.5f;
        float py = 88f + Mathf.Max(0f, (Screen.height - 820f) * 0.15f);

        GUI.color = new Color(0.025f, 0.055f, 0.09f, 0.98f);
        GUI.DrawTexture(new Rect(px, py, panelWidth, panelHeight), _whiteTexture);
        GUI.color = page.accent;
        GUI.DrawTexture(new Rect(px, py, panelWidth, 5f), _whiteTexture);
        GUI.DrawTexture(new Rect(px, py + panelHeight - 3f, panelWidth, 3f), _whiteTexture);

        GUI.color = page.accent;
        GUI.Label(new Rect(px + 36f, py + 28f, panelWidth - 72f, 46f), page.title, _titleStyle);
        GUI.color = new Color(0.55f, 0.8f, 0.92f);
        GUI.Label(new Rect(px + 38f, py + 78f, panelWidth - 76f, 32f), page.subtitle, _smallStyle);

        GUI.color = Color.white;
        GUI.Label(new Rect(px + 42f, py + 132f, panelWidth - 84f, panelHeight - 240f), page.body, _bodyStyle);

        float dotsStart = px + 40f;
        float dotsY = py + panelHeight - 91f;
        for (int i = 0; i < _pages.Count; i++)
        {
            GUI.color = i == _pageIndex ? page.accent : new Color(0.35f, 0.48f, 0.55f);
            GUI.DrawTexture(new Rect(dotsStart + i * 24f, dotsY, 14f, 6f), _whiteTexture);
        }

        GUI.color = Color.white;
        if (GUI.Button(new Rect(px + panelWidth - 410f, py + panelHeight - 114f, 120f, 46f), "ĐÓNG", _buttonStyle))
            ClosePanel();
        if (_pageIndex > 0 && GUI.Button(new Rect(px + panelWidth - 280f, py + panelHeight - 114f, 120f, 46f), "QUAY LẠI", _buttonStyle))
            PreviousPage();
        string nextLabel = _pageIndex < _pages.Count - 1 ? "TIẾP TỤC" : "HOÀN TẤT";
        if (GUI.Button(new Rect(px + panelWidth - 150f, py + panelHeight - 114f, 120f, 46f), nextLabel, _buttonStyle))
            NextPage();

        GUI.color = new Color(0.55f, 0.75f, 0.85f);
        GUI.Label(new Rect(px + 42f, py + panelHeight - 48f, panelWidth - 500f, 28f), "Enter/Space: tiếp tục   •   ESC: đóng   •   F1: reset trạng thái test", _smallStyle);
        GUI.color = Color.white;
    }

    private void ReturnToStartGame()
    {
        SetCursor(true);
        if (string.IsNullOrWhiteSpace(returnScene))
        {
            Debug.LogError("[Tutorial] Return scene is empty; cannot continue to StartGame.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(returnScene))
        {
            Debug.LogError("[Tutorial] Scene '" + returnScene + "' is not enabled in Build Settings.");
            return;
        }

        SceneManager.LoadScene(returnScene);
    }

    private void QueueReturnToStartGame()
    {
        // Guard thứ hai để không có luồng UI hoặc phím tắt nào chuyển scene
        // khi bài thực hành chưa thật sự hoàn tất.
        if (!autoReturnToStartGame || !_courseComplete || _returnToStartCoroutine != null)
            return;

        _returnToStartCoroutine = StartCoroutine(ReturnToStartGameAfterDelay());
    }

    private IEnumerator ReturnToStartGameAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, returnDelayAfterCompletion));
        ReturnToStartGame();
    }

    private void OnDestroy()
    {
        SetCursor(true);
        if (_whiteTexture != null)
            Destroy(_whiteTexture);
        foreach (var item in _runtimeObjects)
        {
            if (item != null)
                Destroy(item);
        }
    }
}
