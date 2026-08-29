using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(BoxCollider))]
public class InfoBoard : MonoBehaviour, IInteractable
{
    [Header("Guide panel")]
    [SerializeField] private GameObject guidePanelPrefab;
    [SerializeField] private bool showGuideOnFirstWaitingVisit = true;
    [SerializeField] private string guidePlayerPrefsKey = "Mimeto_WaitingGuideShown";

    public bool isOpen = false;

    private static readonly GuidePage[] GUIDE_PAGES =
    {
        new GuidePage(
            "DI CHUYỂN & TƯƠNG TÁC",
            "WASD: di chuyển   |   Chuột: nhìn\n" +
            "Shift: chạy   |   Ctrl: cúi người   |   Space: nhảy\n" +
            "E: tương tác với bảng, trạm và vật phẩm.\n" +
            "1 / 2 / 3: chọn ô trang bị trên hotbar."),
        new GuidePage(
            "SINH TỒN",
            "Theo dõi OXY liên tục; vào vùng an toàn hoặc mua bình O2 để nạp.\n" +
            "Đặt đèn pin vào hotbar rồi nhấn F để bật/tắt.\n" +
            "Mặt nạ giúp giảm ảnh hưởng của vùng khí độc."),
        new GuidePage(
            "MỤC TIÊU & THOÁT",
            "Khám phá khu vực, nhặt Scrap và vật phẩm cần thiết.\n" +
            "Mang Scrap về Reclaimer để đổi EC, rồi mua trang bị tại Shop.\n" +
            "Hoàn thành mục tiêu, tập hợp đủ đội và dùng cửa thoát để trở về Waiting.")
    };

    private GameObject _guidePanel;
    private TMP_Text _contextText;
    private Button _closeButton;
    private Button _nextButton;
    private Button _backButton;
    private int _pageIndex;

    private struct GuidePage
    {
        public readonly string Heading;
        public readonly string Body;

        public GuidePage(string heading, string body)
        {
            Heading = heading;
            Body = body;
        }
    }

    private void Awake()
    {
        EnsureGuidePanel();
    }

    private void Start()
    {
        if (!showGuideOnFirstWaitingVisit || PlayerPrefs.GetInt(guidePlayerPrefsKey, 0) != 0)
            return;

        if (!EnsureGuidePanel())
            return;

        PlayerPrefs.SetInt(guidePlayerPrefsKey, 1);
        PlayerPrefs.Save();
        OpenGuide();
    }

    private void OnDisable()
    {
        CloseGuide();
    }

    public void Interact(GameObject interactor)
    {
        OpenGuide();
    }

    private void OpenGuide()
    {
        if (isOpen || !EnsureGuidePanel())
            return;

        _pageIndex = 0;
        RefreshPage();
        _guidePanel.SetActive(true);
        isOpen = true;
        PlayerController.OpenMinigameCount++;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void CloseGuide()
    {
        if (_guidePanel != null)
            _guidePanel.SetActive(false);

        if (!isOpen)
            return;

        isOpen = false;
        PlayerController.OpenMinigameCount = Mathf.Max(0, PlayerController.OpenMinigameCount - 1);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (!isOpen)
            return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (Input.GetKeyDown(KeyCode.Escape))
            CloseGuide();
    }

    private bool EnsureGuidePanel()
    {
        if (_guidePanel != null)
            return true;

        if (guidePanelPrefab == null)
        {
            Debug.LogWarning("[InfoBoard] Chưa gán prefab HuongDan trong scene Waiting.");
            return false;
        }

        // Instantiate as its own screen-space Canvas so the prefab keeps the exact
        // RectTransform coordinates authored by the designer, independent of any
        // Waiting HUD canvas (which may be scaled/disabled).
        _guidePanel = Instantiate(guidePanelPrefab);
        _guidePanel.name = guidePanelPrefab.name + "_Instance";
        ConfigureGuideCanvas(_guidePanel);
        _guidePanel.SetActive(false);

        // Keep the prefab's own title and button labels. Only Context is
        // updated by the guide pages; support the current lowercase names and
        // the previous capitalization for backward compatibility.
        _contextText = FindText("context", "Context");
        _closeButton = FindButton("close", "Close");
        _nextButton = FindButton("next", "Next");
        _backButton = FindButton("back", "Back");

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveAllListeners();
            _closeButton.onClick.AddListener(CloseGuide);
        }

        if (_nextButton != null)
        {
            _nextButton.onClick.RemoveAllListeners();
            _nextButton.onClick.AddListener(NextPage);
        }

        if (_backButton != null)
        {
            _backButton.onClick.RemoveAllListeners();
            _backButton.onClick.AddListener(PreviousPage);
        }

        if (_contextText == null || _closeButton == null ||
            _nextButton == null || _backButton == null)
        {
            Debug.LogWarning("[InfoBoard] Prefab HuongDan cần có context, close, next và back.");
        }

        return true;
    }

    private void ConfigureGuideCanvas(GameObject panel)
    {
        Canvas canvas = panel.GetComponent<Canvas>();
        if (canvas == null)
            canvas = panel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = panel.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = panel.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (panel.GetComponent<GraphicRaycaster>() == null)
            panel.AddComponent<GraphicRaycaster>();

        // The prefab is already authored as a full-screen root. Do not rewrite
        // its RectTransform here; doing so would undo the designer's placement.
    }

    private TMP_Text FindText(params string[] childNames)
    {
        foreach (string childName in childNames)
        {
            Transform child = _guidePanel.transform.Find(childName);
            TMP_Text text = child != null ? child.GetComponent<TMP_Text>() : null;
            if (text != null)
                return text;
        }

        return null;
    }

    private Button FindButton(params string[] childNames)
    {
        foreach (string childName in childNames)
        {
            Transform child = _guidePanel.transform.Find(childName);
            Button button = child != null ? child.GetComponent<Button>() : null;
            if (button != null)
                return button;
        }

        return null;
    }

    private void RefreshPage()
    {
        GuidePage page = GUIDE_PAGES[Mathf.Clamp(_pageIndex, 0, GUIDE_PAGES.Length - 1)];
        if (_contextText != null)
            _contextText.text = page.Heading + "\n\n" + page.Body;

        if (_backButton != null)
            _backButton.interactable = _pageIndex > 0;
        if (_nextButton != null)
            _nextButton.interactable = _pageIndex < GUIDE_PAGES.Length - 1;
    }

    private void NextPage()
    {
        if (_pageIndex >= GUIDE_PAGES.Length - 1)
            return;

        _pageIndex++;
        RefreshPage();
    }

    private void PreviousPage()
    {
        if (_pageIndex <= 0)
            return;

        _pageIndex--;
        RefreshPage();
    }
}
