using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class SetupStartGameScene : EditorWindow
{
    [MenuItem("Tools/Setup StartGame Scene (HORROR THEME)")]
    public static void SetupScene()
    {
        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        GameObject canvasGO = new GameObject("Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();
        
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // BACKGROUND (PITCH BLACK / DARK RED TINT)
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.02f, 0.01f, 0.01f, 1f); // Almost black with a hint of blood red
        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one; bgRect.sizeDelta = Vector2.zero;

        // TITLE
        GameObject titleGO = CreateText(canvasGO.transform, "Title", "M I M E T O", 140, new Vector2(0, 380), new Vector2(1000, 150));
        titleGO.GetComponent<TextMeshProUGUI>().color = new Color(0.6f, 0.05f, 0.05f); // Blood Red
        titleGO.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        
        // STATUS TEXT
        GameObject statusGO = CreateText(canvasGO.transform, "StatusText", "Trạng thái: Đang kết nối...", 24, new Vector2(0, 250), new Vector2(1000, 50));
        statusGO.GetComponent<TextMeshProUGUI>().color = new Color(0.6f, 0.6f, 0.6f); // Grey
        statusGO.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Italic;

        // --- 1. MAIN MENU PANEL ---
        GameObject mainMenuPanel = CreatePanel(canvasGO.transform, "MainMenuPanel", new Vector2(0, -50), new Vector2(500, 500));
        VerticalLayoutGroup mmVLG = mainMenuPanel.AddComponent<VerticalLayoutGroup>();
        mmVLG.childAlignment = TextAnchor.MiddleCenter; mmVLG.spacing = 20;
        mmVLG.childControlWidth = false; mmVLG.childControlHeight = false;

        CreateButton(mainMenuPanel.transform, "PlayButton", "C H Ơ I", new Vector2(0,0), new Vector2(400, 70), new Color(0.4f, 0.05f, 0.05f));
        CreateButton(mainMenuPanel.transform, "ContinueButton", "T I Ế P   T Ụ C", new Vector2(0,0), new Vector2(400, 70), new Color(0.1f, 0.1f, 0.1f));
        CreateButton(mainMenuPanel.transform, "InstructButton", "C À I   Đ Ặ T", new Vector2(0,0), new Vector2(400, 70), new Color(0.1f, 0.1f, 0.1f));
        CreateButton(mainMenuPanel.transform, "ExitButton", "T H O Á T", new Vector2(0,0), new Vector2(400, 70), new Color(0.1f, 0.1f, 0.1f));

        // --- 2. LOBBY LIST PANEL ---
        GameObject lobbyListPanel = CreatePanel(canvasGO.transform, "LobbyListPanel", new Vector2(0, -50), new Vector2(900, 700));
        CreateText(lobbyListPanel.transform, "Title", "DANH SÁCH KHU VỰC", 45, new Vector2(0, 300), new Vector2(800, 60)).GetComponent<TextMeshProUGUI>().color = new Color(0.8f, 0.2f, 0.2f);
        
        CreateButton(lobbyListPanel.transform, "CreateRoomButton", "TẠO KHU VỰC", new Vector2(-200, 220), new Vector2(300, 60), new Color(0.4f, 0.05f, 0.05f));
        CreateButton(lobbyListPanel.transform, "RefreshButton", "TÌM KIẾM TÍN HIỆU", new Vector2(200, 220), new Vector2(300, 60), new Color(0.15f, 0.15f, 0.15f));

        GameObject scrollGO = new GameObject("Scroll View");
        scrollGO.transform.SetParent(lobbyListPanel.transform, false);
        Image scrollImg = scrollGO.AddComponent<Image>(); scrollImg.color = new Color(0, 0, 0, 0.8f);
        ScrollRect scrollRect = scrollGO.AddComponent<ScrollRect>();
        RectTransform srRect = scrollGO.GetComponent<RectTransform>();
        srRect.anchoredPosition = new Vector2(0, -40); srRect.sizeDelta = new Vector2(800, 420);

        GameObject viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollGO.transform, false);
        viewportGO.AddComponent<RectMask2D>();
        RectTransform vpRect = viewportGO.GetComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero; vpRect.anchorMax = Vector2.one; vpRect.sizeDelta = Vector2.zero;

        GameObject contentGO = new GameObject("Container");
        contentGO.transform.SetParent(viewportGO.transform, false);
        RectTransform contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1); contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1); contentRect.sizeDelta = new Vector2(0, 400);
        VerticalLayoutGroup vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = false; vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false; vlg.spacing = 10;
        scrollRect.content = contentRect; scrollRect.viewport = vpRect;

        CreateButton(lobbyListPanel.transform, "BackButton", "QUAY LẠI", new Vector2(0, -300), new Vector2(250, 60), new Color(0.1f, 0.1f, 0.1f));
        lobbyListPanel.SetActive(false);

        // --- 3. CREATE ROOM PANEL ---
        GameObject createRoomPanel = CreatePanel(canvasGO.transform, "CreateRoomPanel", new Vector2(0, -50), new Vector2(700, 600));
        CreateText(createRoomPanel.transform, "Title", "TẠO KHU VỰC", 45, new Vector2(0, 240), new Vector2(600, 60)).GetComponent<TextMeshProUGUI>().color = new Color(0.8f, 0.2f, 0.2f);
        
        CreateInputField(createRoomPanel.transform, "RoomNameInput", "Nhập tên khu vực...", new Vector2(0, 120), new Vector2(500, 60));
        
        CreateButton(createRoomPanel.transform, "SetPublicButton", "CÔNG KHAI", new Vector2(-130, 20), new Vector2(240, 60), new Color(0.1f, 0.1f, 0.1f));
        CreateButton(createRoomPanel.transform, "SetPrivateButton", "BÍ MẬT", new Vector2(130, 20), new Vector2(240, 60), new Color(0.1f, 0.1f, 0.1f));
        
        CreateButton(createRoomPanel.transform, "ConfirmCreateButton", "XÁC NHẬN TẠO", new Vector2(0, -110), new Vector2(500, 70), new Color(0.4f, 0.05f, 0.05f));
        CreateButton(createRoomPanel.transform, "CancelButton", "HỦY BỎ", new Vector2(0, -220), new Vector2(250, 60), new Color(0.1f, 0.1f, 0.1f));
        createRoomPanel.SetActive(false);

        // --- 4. ROOM INFO PANEL ---
        GameObject roomInfoPanel = CreatePanel(canvasGO.transform, "RoomInfoPanel", new Vector2(0, -50), new Vector2(700, 600));
        CreateText(roomInfoPanel.transform, "Title", "THÔNG TIN KHU VỰC", 45, new Vector2(0, 240), new Vector2(600, 60)).GetComponent<TextMeshProUGUI>().color = new Color(0.8f, 0.2f, 0.2f);
        
        CreateInputField(roomInfoPanel.transform, "EditRoomNameInput", "Tên khu vực...", new Vector2(0, 140), new Vector2(500, 60));
        CreateText(roomInfoPanel.transform, "RoomTypeText", "Loại: Công khai", 28, new Vector2(0, 60), new Vector2(500, 40));
        
        GameObject roomCodeText = CreateText(roomInfoPanel.transform, "RoomCodeText", "Mã truy cập: ", 40, new Vector2(0, -10), new Vector2(500, 50));
        roomCodeText.GetComponent<TextMeshProUGUI>().color = new Color(0.8f, 0.2f, 0.2f); // Red code
        
        CreateText(roomInfoPanel.transform, "RoomPlayersText", "Người sống sót: 1/4", 28, new Vector2(0, -70), new Vector2(500, 40));
        
        CreateButton(roomInfoPanel.transform, "CopyCodeButton", "COPY MÃ TRUY CẬP", new Vector2(-140, -150), new Vector2(260, 60), new Color(0.15f, 0.15f, 0.15f));
        CreateButton(roomInfoPanel.transform, "StartWaitingButton", "BẮT ĐẦU", new Vector2(140, -150), new Vector2(260, 60), new Color(0.4f, 0.05f, 0.05f));
        
        CreateButton(roomInfoPanel.transform, "CancelRoomButton", "GIẢI TÁN", new Vector2(0, -230), new Vector2(250, 60), new Color(0.1f, 0.1f, 0.1f));
        roomInfoPanel.SetActive(false);

        // 6. Khởi tạo MultiplayerCenter
        GameObject mmGO = new GameObject("MultiplayerCenterManager");
        MultiplayerCenter mc = mmGO.AddComponent<MultiplayerCenter>();
        mc.statusText = statusGO.GetComponent<TextMeshProUGUI>();

        // LOBBY ITEM PREFAB
        GameObject itemPrefab = CreateButton(null, "LobbyItem", "Khu vực (1/4)", Vector2.zero, new Vector2(760, 70), new Color(0.05f, 0.05f, 0.05f));
        mc.lobbyItemPrefab = itemPrefab;
        itemPrefab.SetActive(false);
        itemPrefab.transform.SetParent(mmGO.transform);

        // 7. Instantiate User's NetworkManager Prefab
        GameObject netPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/NetworkManager.prefab");
        if (netPrefab != null)
        {
            GameObject netGO = (GameObject)PrefabUtility.InstantiatePrefab(netPrefab);
            netGO.name = "NetworkManager";
        }
        else
        {
            Debug.LogError("COULD NOT FIND NetworkManager.prefab!");
        }

        
        // Set everything to UI layer
        foreach (Transform t in canvasGO.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = LayerMask.NameToLayer("UI");
        }

        
        // Disable raycastTarget on all graphics except buttons
        foreach (var graphic in canvasGO.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic.GetComponent<Button>() == null)
            {
                graphic.raycastTarget = false;
            }
            else
            {
                graphic.raycastTarget = true;
            }
        }

        
        GameObject playBtn = GameObject.Find("PlayButton");
        if (playBtn != null)
        {
            ForceClicker fc = playBtn.AddComponent<ForceClicker>();
            fc.currentPanel = mainMenuPanel;
            fc.targetPanel = lobbyListPanel;
        }
        
        // Also ensure EventSystem is standard and active
        var es = GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
        if (es == null) {
            es = new GameObject("EventSystem").AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        if (!System.IO.Directory.Exists("Assets/Scenes")) System.IO.Directory.CreateDirectory("Assets/Scenes");
        string path = "Assets/Scenes/StartGame.unity";
        EditorSceneManager.SaveScene(newScene, path);
        Debug.Log("Đã tái tạo StartGame.unity giao diện HORROR!");
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        GameObject p = new GameObject(name);
        p.transform.SetParent(parent, false);
        Image img = p.AddComponent<Image>();
        img.color = new Color(0.02f, 0.02f, 0.02f, 0.9f); // Almost black, slightly transparent
        RectTransform r = p.GetComponent<RectTransform>();
        r.anchoredPosition = pos; r.sizeDelta = size;
        
        // Minimal red blood border
        UnityEngine.UI.Outline outline = p.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0.3f, 0.0f, 0.0f, 0.6f);
        outline.effectDistance = new Vector2(2, -2);
        return p;
    }

    private static GameObject CreateButton(Transform parent, string name, string text, Vector2 pos, Vector2 size, Color btnColor)
    {
        GameObject btnGO = new GameObject(name);
        if (parent != null) btnGO.transform.SetParent(parent, false);
        Image img = btnGO.AddComponent<Image>();
        img.color = btnColor;
        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;

        UnityEngine.UI.Outline outline = btnGO.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0,0,0, 0.8f);
        outline.effectDistance = new Vector2(2, -2);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);
        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.color = new Color(0.85f, 0.85f, 0.85f); // Ash white
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 24;
        
        RectTransform rt = textGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;

        RectTransform r = btnGO.GetComponent<RectTransform>();
        r.anchoredPosition = pos; r.sizeDelta = size;
        return btnGO;
    }

    private static GameObject CreateText(Transform parent, string name, string text, float fontSize, Vector2 pos, Vector2 size)
    {
        GameObject t = new GameObject(name);
        t.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = t.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = new Color(0.8f, 0.8f, 0.8f); // Dirty white
        tmp.alignment = TextAlignmentOptions.Center;
        
        UnityEngine.UI.Shadow shadow = t.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor = new Color(0,0,0, 0.9f);
        shadow.effectDistance = new Vector2(3, -3);

        RectTransform r = t.GetComponent<RectTransform>();
        r.anchoredPosition = pos; r.sizeDelta = size;
        return t;
    }

    private static GameObject CreateInputField(Transform parent, string name, string placeholderText, Vector2 pos, Vector2 size)
    {
        GameObject inputGO = new GameObject(name);
        inputGO.transform.SetParent(parent, false);
        Image bg = inputGO.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.05f, 0.9f); // Dark background
        
        UnityEngine.UI.Outline outline = inputGO.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0.2f, 0.0f, 0.0f, 0.8f); // Dark red border
        outline.effectDistance = new Vector2(1, -1);

        TMP_InputField inputField = inputGO.AddComponent<TMP_InputField>();

        GameObject pGO = new GameObject("Placeholder");
        pGO.transform.SetParent(inputGO.transform, false);
        TextMeshProUGUI pTxt = pGO.AddComponent<TextMeshProUGUI>();
        pTxt.text = placeholderText; pTxt.color = new Color(0.4f, 0.4f, 0.4f, 0.6f); 
        pTxt.fontSize = 26; pTxt.fontStyle = FontStyles.Italic;
        pTxt.alignment = TextAlignmentOptions.Left; pTxt.margin = new Vector4(15, 0, 0, 0);
        
        RectTransform pRect = pGO.GetComponent<RectTransform>();
        pRect.anchorMin = Vector2.zero; pRect.anchorMax = Vector2.one; pRect.sizeDelta = Vector2.zero;

        GameObject tGO = new GameObject("Text");
        tGO.transform.SetParent(inputGO.transform, false);
        TextMeshProUGUI tTxt = tGO.AddComponent<TextMeshProUGUI>();
        tTxt.color = new Color(0.9f, 0.9f, 0.9f); tTxt.fontSize = 28; tTxt.fontStyle = FontStyles.Bold;
        tTxt.alignment = TextAlignmentOptions.Left; tTxt.margin = new Vector4(15, 0, 0, 0);
        
        RectTransform tRect = tGO.GetComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero; tRect.anchorMax = Vector2.one; tRect.sizeDelta = Vector2.zero;

        inputField.textComponent = tTxt;
        inputField.placeholder = pTxt;

        RectTransform r = inputGO.GetComponent<RectTransform>();
        r.anchoredPosition = pos; r.sizeDelta = size;
        return inputGO;
    }
}
