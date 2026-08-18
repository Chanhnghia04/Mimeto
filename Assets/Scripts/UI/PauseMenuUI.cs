using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Netcode;

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
    }

    private void CreateUI()
    {
        // 1. Create Canvas
        GameObject canvasObj = new GameObject("PauseMenuCanvas");
        canvasObj.transform.SetParent(transform);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
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

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Only works during gameplay (not StartGame scene)
            if (SceneManager.GetActiveScene().name == "StartGame")
                return;

            // Respect PlayerController minigame state
            if (PlayerController.OpenMinigameCount > 0)
                return;

            // If Settings is open when ESC is pressed, close Settings first
            if (SettingsUI.Instance != null && SettingsUI.Instance.IsOpen)
            {
                SettingsUI.Instance.CloseSettings();
                return;
            }

            if (_isOpen)
            {
                CloseMenu();
                return;
            }

            // Ngăn chặn PauseMenu tự động mở nếu người chơi đang bấm ESC để thoát khỏi một UI khác (Shop, Chest, Inventory...)
            PlayerController pc = Object.FindAnyObjectByType<PlayerController>();
            if (pc != null && pc.IsUIOpen())
            {
                return;
            }

            OpenMenu();
        }
    }

    public void OpenMenu()
    {
        _isOpen = true;
        _uiContainer.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
        CloseMenu();
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
