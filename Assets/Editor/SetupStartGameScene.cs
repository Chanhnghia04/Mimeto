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
    [MenuItem("Tools/Setup StartGame Scene")]
    public static void SetupScene()
    {
        // 1. Tạo Scene mới
        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // 2. Khởi tạo Canvas
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

        // 3. Khởi tạo Background
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.12f, 1f);
        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // TITLE
        GameObject titleGO = CreateText(canvasGO.transform, "Title", "MIMETO", 80, new Vector2(0, 400), new Vector2(800, 100));

        // STATUS TEXT
        GameObject statusGO = CreateText(canvasGO.transform, "StatusText", "Khởi động...", 30, new Vector2(0, 300), new Vector2(1000, 50));

        // --- MAIN MENU PANEL ---
        GameObject mainMenuPanel = CreatePanel(canvasGO.transform, "MainMenuPanel", new Vector2(0, 0), new Vector2(600, 400));
        GameObject hostBtn = CreateButton(mainMenuPanel.transform, "HostButton", "TẠO PHÒNG (HOST)", new Vector2(0, 50), new Vector2(400, 80));
        GameObject clientBtn = CreateButton(mainMenuPanel.transform, "ClientButton", "TÌM PHÒNG (CLIENT)", new Vector2(0, -50), new Vector2(400, 80));

        // --- HOST PANEL ---
        GameObject hostPanel = CreatePanel(canvasGO.transform, "HostPanel", new Vector2(0, 0), new Vector2(600, 500));
        CreateText(hostPanel.transform, "Title", "TẠO PHÒNG", 40, new Vector2(0, 200), new Vector2(500, 50));
        GameObject hostStatusText = CreateText(hostPanel.transform, "HostStatusText", "Chọn chế độ", 24, new Vector2(0, 140), new Vector2(500, 40));
        GameObject lobbyNameInput = CreateInputField(hostPanel.transform, "LobbyNameInput", "Tên phòng...", new Vector2(0, 50), new Vector2(400, 60));
        GameObject publicBtn = CreateButton(hostPanel.transform, "PublicButton", "TẠO PUBLIC", new Vector2(0, -50), new Vector2(400, 60));
        GameObject privateBtn = CreateButton(hostPanel.transform, "PrivateButton", "TẠO PRIVATE", new Vector2(0, -130), new Vector2(400, 60));
        GameObject hostBackBtn = CreateButton(hostPanel.transform, "BackButton", "QUAY LẠI", new Vector2(0, -210), new Vector2(200, 50));
        hostPanel.SetActive(false);

        // --- CLIENT PANEL ---
        GameObject clientPanel = CreatePanel(canvasGO.transform, "ClientPanel", new Vector2(0, 0), new Vector2(800, 700));
        CreateText(clientPanel.transform, "Title", "TÌM PHÒNG", 40, new Vector2(0, 300), new Vector2(500, 50));
        GameObject clientStatusText = CreateText(clientPanel.transform, "ClientStatusText", "Đang tìm phòng...", 24, new Vector2(0, 250), new Vector2(700, 40));
        
        GameObject codeInput = CreateInputField(clientPanel.transform, "JoinCodeInput", "Nhập mã phòng Private...", new Vector2(-150, 180), new Vector2(300, 50));
        GameObject joinByCodeBtn = CreateButton(clientPanel.transform, "JoinButton", "VÀO BẰNG MÃ", new Vector2(170, 180), new Vector2(200, 50));
        GameObject refreshBtn = CreateButton(clientPanel.transform, "RefreshButton", "LÀM MỚI DANH SÁCH", new Vector2(0, 110), new Vector2(300, 50));

        // Scroll view cho danh sách lobby
        GameObject scrollGO = new GameObject("Scroll View");
        scrollGO.transform.SetParent(clientPanel.transform, false);
        Image scrollImg = scrollGO.AddComponent<Image>();
        scrollImg.color = new Color(0,0,0, 0.5f);
        ScrollRect scrollRect = scrollGO.AddComponent<ScrollRect>();
        RectTransform srRect = scrollGO.GetComponent<RectTransform>();
        srRect.anchoredPosition = new Vector2(0, -90);
        srRect.sizeDelta = new Vector2(700, 300);

        GameObject viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollGO.transform, false);
        viewportGO.AddComponent<RectMask2D>();
        RectTransform vpRect = viewportGO.GetComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero; vpRect.anchorMax = Vector2.one; vpRect.sizeDelta = Vector2.zero;

        GameObject contentGO = new GameObject("Container");
        contentGO.transform.SetParent(viewportGO.transform, false);
        RectTransform contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1); contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 300);
        VerticalLayoutGroup vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = false; vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false; vlg.spacing = 10;
        scrollRect.content = contentRect;
        scrollRect.viewport = vpRect;

        GameObject clientBackBtn = CreateButton(clientPanel.transform, "BackButton", "QUAY LẠI", new Vector2(0, -300), new Vector2(200, 50));
        clientPanel.SetActive(false);

        // --- ROOM INFO PANEL ---
        GameObject roomInfoPanel = CreatePanel(canvasGO.transform, "RoomInfoPanel", new Vector2(0, 0), new Vector2(600, 500));
        CreateText(roomInfoPanel.transform, "Title", "THÔNG TIN PHÒNG", 40, new Vector2(0, 200), new Vector2(500, 50));
        GameObject roomNameText = CreateText(roomInfoPanel.transform, "RoomNameText", "Tên phòng: ", 30, new Vector2(0, 130), new Vector2(500, 40));
        GameObject roomTypeText = CreateText(roomInfoPanel.transform, "RoomTypeText", "Loại: ", 26, new Vector2(0, 80), new Vector2(500, 40));
        GameObject roomCodeText = CreateText(roomInfoPanel.transform, "RoomCodeText", "Mã phòng: ", 36, new Vector2(0, 10), new Vector2(500, 50));
        roomCodeText.GetComponent<TextMeshProUGUI>().color = Color.yellow;
        GameObject roomPlayersText = CreateText(roomInfoPanel.transform, "RoomPlayersText", "Người chơi: 1/4", 26, new Vector2(0, -60), new Vector2(500, 40));
        
        GameObject copyCodeBtn = CreateButton(roomInfoPanel.transform, "CopyCodeButton", "COPY MÃ PHÒNG", new Vector2(-120, -140), new Vector2(220, 50));
        GameObject startWaitBtn = CreateButton(roomInfoPanel.transform, "StartWaitingButton", "BẮT ĐẦU VÀO GAME", new Vector2(120, -140), new Vector2(220, 50));
        startWaitBtn.GetComponent<Image>().color = new Color(0.2f, 0.8f, 0.2f);
        GameObject cancelRoomBtn = CreateButton(roomInfoPanel.transform, "CancelRoomButton", "HỦY PHÒNG", new Vector2(0, -210), new Vector2(200, 50));
        cancelRoomBtn.GetComponent<Image>().color = new Color(0.8f, 0.2f, 0.2f);
        roomInfoPanel.SetActive(false);

        // 4. Khởi tạo MultiplayerCenter
        GameObject mmGO = new GameObject("MultiplayerCenterManager");
        MultiplayerCenter mc = mmGO.AddComponent<MultiplayerCenter>();
        mc.hostButton = hostBtn.GetComponent<Button>();
        mc.clientButton = clientBtn.GetComponent<Button>();
        mc.backButton = hostBackBtn.GetComponent<Button>();
        
        mc.hostPanel = hostPanel;
        mc.lobbyNameInput = lobbyNameInput.GetComponent<TMP_InputField>();
        mc.publicButton = publicBtn.GetComponent<Button>();
        mc.privateButton = privateBtn.GetComponent<Button>();
        mc.hostStatusText = hostStatusText.GetComponent<TextMeshProUGUI>();

        mc.clientPanel = clientPanel;
        mc.joinCodeInput = codeInput.GetComponent<TMP_InputField>();
        mc.joinByCodeButton = joinByCodeBtn.GetComponent<Button>();
        mc.refreshLobbiesButton = refreshBtn.GetComponent<Button>();
        mc.lobbyListContainer = contentGO.transform;
        mc.clientStatusText = clientStatusText.GetComponent<TextMeshProUGUI>();

        mc.roomInfoPanel = roomInfoPanel;
        mc.roomNameText = roomNameText.GetComponent<TextMeshProUGUI>();
        mc.roomCodeText = roomCodeText.GetComponent<TextMeshProUGUI>();
        mc.roomTypeText = roomTypeText.GetComponent<TextMeshProUGUI>();
        mc.roomPlayersText = roomPlayersText.GetComponent<TextMeshProUGUI>();
        mc.startWaitingButton = startWaitBtn.GetComponent<Button>();
        mc.copyCodeButton = copyCodeBtn.GetComponent<Button>();
        mc.cancelRoomButton = cancelRoomBtn.GetComponent<Button>();

        mc.statusText = statusGO.GetComponent<TextMeshProUGUI>();

        // LOBBY ITEM PREFAB
        GameObject itemPrefab = CreateButton(null, "LobbyItem", "Lobby Name (1/4)", Vector2.zero, new Vector2(650, 60));
        mc.lobbyItemPrefab = itemPrefab;
        itemPrefab.SetActive(false);
        itemPrefab.transform.SetParent(mmGO.transform); // Hide in manager

        // 6. Khởi tạo NetworkManager của Netcode for GameObjects
        GameObject netGO = new GameObject("NetworkManager");
        NetworkManager netManager = netGO.AddComponent<NetworkManager>();
        UnityTransport utp = netGO.AddComponent<UnityTransport>();

        SerializedObject netSO = new SerializedObject(netManager);
        var netConfigProp = netSO.FindProperty("m_NetworkConfig");
        if (netConfigProp == null) netConfigProp = netSO.FindProperty("NetworkConfig");
        if (netConfigProp != null)
        {
            var transportProp = netConfigProp.FindPropertyRelative("NetworkTransport");
            if (transportProp != null)
            {
                transportProp.objectReferenceValue = utp;
                netSO.ApplyModifiedProperties();
            }
        }

        // 7. Lưu Scene
        if (!System.IO.Directory.Exists("Assets/Scenes")) System.IO.Directory.CreateDirectory("Assets/Scenes");
        string path = "Assets/Scenes/StartGame.unity";
        EditorSceneManager.SaveScene(newScene, path);
        
        Debug.Log("Đã tái tạo StartGame.unity dùng MultiplayerCenter.");
        EditorUtility.DisplayDialog("Thành công", "Scene StartGame đã được cập nhật với giao diện mới hoàn toàn (MultiplayerCenter)!", "OK");
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        GameObject p = new GameObject(name);
        p.transform.SetParent(parent, false);
        Image img = p.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0.8f);
        RectTransform r = p.GetComponent<RectTransform>();
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        return p;
    }

    private static GameObject CreateButton(Transform parent, string name, string text, Vector2 pos, Vector2 size)
    {
        GameObject btnGO = new GameObject(name);
        if (parent != null) btnGO.transform.SetParent(parent, false);
        Image img = btnGO.AddComponent<Image>();
        img.color = new Color(0.3f, 0.3f, 0.3f);
        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);
        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 24;

        RectTransform tmpRect = textGO.GetComponent<RectTransform>();
        tmpRect.anchorMin = Vector2.zero; tmpRect.anchorMax = Vector2.one; tmpRect.sizeDelta = Vector2.zero;

        RectTransform btnRect = btnGO.GetComponent<RectTransform>();
        btnRect.anchoredPosition = pos; btnRect.sizeDelta = size;
        return btnGO;
    }

    private static GameObject CreateText(Transform parent, string name, string text, float fontSize, Vector2 pos, Vector2 size)
    {
        GameObject t = new GameObject(name);
        t.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = t.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        RectTransform r = t.GetComponent<RectTransform>();
        r.anchoredPosition = pos; r.sizeDelta = size;
        return t;
    }

    private static GameObject CreateInputField(Transform parent, string name, string placeholderText, Vector2 pos, Vector2 size)
    {
        GameObject inputGO = new GameObject(name);
        inputGO.transform.SetParent(parent, false);
        Image bg = inputGO.AddComponent<Image>();
        bg.color = Color.white;
        TMP_InputField inputField = inputGO.AddComponent<TMP_InputField>();

        GameObject pGO = new GameObject("Placeholder");
        pGO.transform.SetParent(inputGO.transform, false);
        TextMeshProUGUI pTxt = pGO.AddComponent<TextMeshProUGUI>();
        pTxt.text = placeholderText; pTxt.color = new Color(0.2f,0.2f,0.2f,0.5f); pTxt.fontSize = 24;
        pTxt.alignment = TextAlignmentOptions.Left; pTxt.margin = new Vector4(10, 0, 0, 0);
        RectTransform pRect = pGO.GetComponent<RectTransform>();
        pRect.anchorMin = Vector2.zero; pRect.anchorMax = Vector2.one; pRect.sizeDelta = Vector2.zero;

        GameObject tGO = new GameObject("Text");
        tGO.transform.SetParent(inputGO.transform, false);
        TextMeshProUGUI txt = tGO.AddComponent<TextMeshProUGUI>();
        txt.color = Color.black; txt.fontSize = 24;
        txt.alignment = TextAlignmentOptions.Left; txt.margin = new Vector4(10, 0, 0, 0);
        RectTransform tRect = tGO.GetComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero; tRect.anchorMax = Vector2.one; tRect.sizeDelta = Vector2.zero;

        inputField.textComponent = txt;
        inputField.placeholder = pTxt;
        inputField.targetGraphic = bg;
        
        RectTransform r = inputGO.GetComponent<RectTransform>();
        r.anchoredPosition = pos; r.sizeDelta = size;
        return inputGO;
    }
}

