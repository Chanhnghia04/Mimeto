using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Netcode;

[DefaultExecutionOrder(-100)]
public class PauseMenuUI : MonoBehaviour
{
    public static PauseMenuUI Instance { get; private set; }

    private GameObject _uiContainer;
    private bool _isOpen = false;
    public bool IsOpen => _isOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoSpawn()
    {
        if (Instance == null)
        {
            GameObject obj = new GameObject("PauseMenuManager");
            obj.AddComponent<PauseMenuUI>();
        }
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreateUI();
        _uiContainer.SetActive(false);
        _isOpen = false;
        
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Reset state mỗi khi load scene mới
        _isOpen = false;
        if (_uiContainer != null)
        {
            _uiContainer.SetActive(false);
        }
    }

    private void CreateUI()
    {
        // 1. Create Canvas
        GameObject canvasObj = new GameObject("PauseMenuCanvas");
        canvasObj.transform.SetParent(transform);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // 2. Main Container
        _uiContainer = new GameObject("Container");
        _uiContainer.transform.SetParent(canvasObj.transform, false);
        RectTransform containerRect = _uiContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;

        // 3. Dark Overlay (Semi-transparent black 70%)
        GameObject overlay = new GameObject("Overlay");
        overlay.transform.SetParent(_uiContainer.transform, false);
        RectTransform overlayRect = overlay.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        Image overlayImg = overlay.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.7f);

        // 4. Center Panel for Vertical Layout
        GameObject centerPanel = new GameObject("CenterPanel");
        centerPanel.transform.SetParent(_uiContainer.transform, false);
        RectTransform centerRect = centerPanel.AddComponent<RectTransform>();
        centerRect.anchorMin = new Vector2(0.5f, 0.5f);
        centerRect.anchorMax = new Vector2(0.5f, 0.5f);
        centerRect.sizeDelta = new Vector2(400, 500);

        VerticalLayoutGroup layout = centerPanel.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 12;
        layout.childControlHeight = false;
        layout.childControlWidth = false;

        // 5. Title Text "MIMETO"
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(centerPanel.transform, false);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "MIMETO";
        titleText.fontSize = 48;
        ColorUtility.TryParseHtmlString("#00E5FF", out Color titleColor);
        titleText.color = titleColor;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontStyle = FontStyles.Bold;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(400, 80);

        // Add some spacing between title and buttons
        GameObject spacer = new GameObject("Spacer");
        spacer.transform.SetParent(centerPanel.transform, false);
        spacer.AddComponent<RectTransform>().sizeDelta = new Vector2(400, 20);

        // 6. Buttons
        CreateButton(centerPanel.transform, "TIẾP TỤC", OnResumeClicked);
        CreateButton(centerPanel.transform, "CÀI ĐẶT", OnSettingsClicked);
        CreateButton(centerPanel.transform, "RỜI PHÒNG", OnDisconnectClicked);
        CreateButton(centerPanel.transform, "THOÁT GAME", OnQuitClicked);
    }

    private void CreateButton(Transform parent, string text, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObj = new GameObject(text + "_Button");
        buttonObj.transform.SetParent(parent, false);
        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(320, 55);

        Image img = buttonObj.AddComponent<Image>();
        ColorUtility.TryParseHtmlString("#16213E", out Color normalColor);
        img.color = normalColor;

        Button btn = buttonObj.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        ColorBlock cb = btn.colors;
        cb.normalColor = normalColor;
        ColorUtility.TryParseHtmlString("#0F3460", out Color hoverColor);
        cb.highlightedColor = hoverColor;
        cb.pressedColor = hoverColor;
        cb.selectedColor = normalColor;
        btn.colors = cb;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
        tmpText.text = text;
        tmpText.fontSize = 20;
        ColorUtility.TryParseHtmlString("#E0F7FA", out Color textColor);
        tmpText.color = textColor;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.fontStyle = FontStyles.Bold;
    }

    private PlayerController _cachedLocalPlayer;

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            // 1. Ưu tiên đóng SettingsUI nếu đang mở
            if (SettingsUI.Instance != null && SettingsUI.Instance.IsOpen)
            {
                SettingsUI.Instance.CloseSettings();
                return;
            }

            // 2. Không mở Pause Menu trong main menu
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName == "StartGame")
            {
                return;
            }

            // 3. Đang mở thì đóng
            if (_isOpen)
            {
                CloseMenu();
                return;
            }

            // 4. Kiểm tra xem có UI nào khác đang mở không. 
            // Nếu có, ta bỏ qua không mở PauseMenu để nhường phím ESC cho UI đó tự đóng.
            if (_cachedLocalPlayer == null)
            {
                if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening && Unity.Netcode.NetworkManager.Singleton.LocalClient != null && Unity.Netcode.NetworkManager.Singleton.LocalClient.PlayerObject != null)
                {
                    _cachedLocalPlayer = Unity.Netcode.NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerController>();
                }
                
                if (_cachedLocalPlayer == null) 
                {
                    PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                    foreach (var p in allPlayers)
                    {
                        if (p.IsOwner)
                        {
                            _cachedLocalPlayer = p;
                            break;
                        }
                    }
                }
            }

            if (_cachedLocalPlayer != null)
            {
                // Nếu bất kì UI nào khác (ngoài PauseMenu) đang mở, nhường quyền phím ESC
                if (_cachedLocalPlayer.IsUIOpen())
                {
                    return;
                }
            }

            OpenMenu();
        }
    }

    public void OpenMenu()
    {
        // Phá hủy toàn bộ Canvas cũ để tạo lại mới 100%, đảm bảo không bị lỗi tàng hình
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
        
        CreateUI();
        _isOpen = true;
        
        // Ép sorting order cao nhất
        Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
        foreach (Canvas c in canvases)
        {
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 32767;
            c.enabled = true;
            c.gameObject.SetActive(true);
        }

        if (_uiContainer != null)
        {
            _uiContainer.SetActive(true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject es = new GameObject("EventSystem_Auto");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }

    public void CloseMenu()
    {
        _isOpen = false;
        _uiContainer.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnResumeClicked()
    {
        CloseMenu();
    }

    private void OnSettingsClicked()
    {
        CloseMenu();
        if (SettingsUI.Instance != null)
        {
            SettingsUI.Instance.OpenSettings();
        }
    }

    private void OnDisconnectClicked()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
        
        // Không gọi CloseMenu() ở đây vì nó sẽ khóa chuột khi về StartGame
        _isOpen = false;
        _uiContainer.SetActive(false);
        
        SceneManager.LoadScene("StartGame");
    }

    private void OnQuitClicked()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
