using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class BuildSettingsUI
{
    [MenuItem("Tools/Build Settings UI")]
    public static void BuildUI()
    {
        // Find Canvas
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }
        
        // Đảm bảo Canvas này luôn nằm trên cùng (đè lên các UI khác)
        canvas.sortingOrder = 100;

        // Settings Manager
        GameObject managerObj = new GameObject("SettingsManager");
        SettingsUI settingsScript = managerObj.AddComponent<SettingsUI>();

        // Main Panel (Sci-Fi Glassmorphism style)
        GameObject panelObj = new GameObject("SettingsPanel");
        panelObj.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.2f, 0.1f);
        panelRect.anchorMax = new Vector2(0.8f, 0.9f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        
        Image panelImg = panelObj.AddComponent<Image>();
        panelImg.color = new Color(0.02f, 0.05f, 0.08f, 0.95f); // Deep Sci-Fi dark blue
        
        Outline panelOutline = panelObj.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0f, 0.8f, 1f, 0.5f); // Cyan glow border
        panelOutline.effectDistance = new Vector2(2, -2);
        
        // Thêm animation bật lên mượt mà
        panelObj.AddComponent<UITweenAnimator>();
        
        settingsScript.settingsPanel = panelObj;

        // Close Button
        GameObject closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(panelObj.transform, false);
        RectTransform closeRect = closeBtnObj.AddComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1, 1);
        closeRect.anchorMax = new Vector2(1, 1);
        closeRect.anchoredPosition = new Vector2(-25, -25);
        closeRect.sizeDelta = new Vector2(40, 40);
        Image closeImg = closeBtnObj.AddComponent<Image>();
        closeImg.color = Color.red;
        Button closeBtn = closeBtnObj.AddComponent<Button>();
        GameObject closeTextObj = new GameObject("Text");
        closeTextObj.transform.SetParent(closeBtnObj.transform, false);
        TextMeshProUGUI closeText = closeTextObj.AddComponent<TextMeshProUGUI>();
        closeText.text = "X";
        closeText.fontStyle = FontStyles.Bold;
        closeText.alignment = TextAlignmentOptions.Center;
        closeText.color = Color.white;
        
        UIJuice closeJuice = closeBtnObj.AddComponent<UIJuice>();
        closeJuice.glowColor = Color.red;
        
        settingsScript.closeButton = closeBtn;

        // Tabs container
        GameObject tabsObj = new GameObject("TabButtons");
        tabsObj.transform.SetParent(panelObj.transform, false);
        RectTransform tabsRect = tabsObj.AddComponent<RectTransform>();
        tabsRect.anchorMin = new Vector2(0, 0.9f);
        tabsRect.anchorMax = new Vector2(1, 1);
        tabsRect.offsetMin = Vector2.zero;
        tabsRect.offsetMax = Vector2.zero;
        HorizontalLayoutGroup hlg = tabsObj.AddComponent<HorizontalLayoutGroup>();
        hlg.childForceExpandWidth = true;

        // Create Tabs Function
        Button CreateTabBtn(string name, string label)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(tabsObj.transform, false);
            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.1f, 0.15f, 0.2f, 1f); // Darker blue-grey
            
            Button btn = btnObj.AddComponent<Button>();
            
            // Add hover juice
            UIJuice juice = btnObj.AddComponent<UIJuice>();
            juice.glowColor = new Color(0f, 0.8f, 1f, 0.8f); // Cyan glow
            
            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.8f, 0.9f, 1f, 1f);
            return btn;
        }

        settingsScript.tabRoomButton = CreateTabBtn("TabRoomBtn", "Room");
        settingsScript.tabAudioButton = CreateTabBtn("TabAudioBtn", "Audio");
        settingsScript.tabGraphicsButton = CreateTabBtn("TabGraphicsBtn", "Graphics");

        // Tab Contents container
        GameObject contentObj = new GameObject("TabContents");
        contentObj.transform.SetParent(panelObj.transform, false);
        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 0);
        contentRect.anchorMax = new Vector2(1, 0.9f);
        contentRect.offsetMin = new Vector2(20, 20);
        contentRect.offsetMax = new Vector2(-20, -20);

        GameObject CreateTabPanel(string name)
        {
            GameObject p = new GameObject(name);
            p.transform.SetParent(contentObj.transform, false);
            RectTransform r = p.AddComponent<RectTransform>();
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            return p;
        }

        GameObject roomPanel = CreateTabPanel("RoomPanel");
        GameObject audioPanel = CreateTabPanel("AudioPanel");
        GameObject graphicsPanel = CreateTabPanel("GraphicsPanel");

        settingsScript.roomPanel = roomPanel;
        settingsScript.audioPanel = audioPanel;
        settingsScript.graphicsPanel = graphicsPanel;

        // Room Panel content
        VerticalLayoutGroup vlgRoom = roomPanel.AddComponent<VerticalLayoutGroup>();
        vlgRoom.spacing = 20;
        
        TextMeshProUGUI CreateText(string name, string text, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.color = Color.white;
            tmp.fontSize = 24;
            return tmp;
        }

        settingsScript.roomNameText = CreateText("RoomNameText", "Room: ...", roomPanel.transform);
        settingsScript.playerCountText = CreateText("PlayerCountText", "Players: ...", roomPanel.transform);
        settingsScript.pingText = CreateText("PingText", "Ping: ...", roomPanel.transform);

        GameObject leaveBtnObj = new GameObject("LeaveRoomBtn");
        leaveBtnObj.transform.SetParent(roomPanel.transform, false);
        leaveBtnObj.AddComponent<Image>().color = new Color(0.8f, 0.1f, 0.1f, 1f);
        Button leaveBtn = leaveBtnObj.AddComponent<Button>();
        
        UIJuice leaveJuice = leaveBtnObj.AddComponent<UIJuice>();
        leaveJuice.glowColor = Color.red;
        
        TextMeshProUGUI leaveTxt = CreateText("Text", "LEAVE ROOM", leaveBtnObj.transform);
        leaveTxt.alignment = TextAlignmentOptions.Center;
        leaveTxt.fontStyle = FontStyles.Bold;
        
        settingsScript.leaveRoomButton = leaveBtn;

        // Audio Panel content
        VerticalLayoutGroup vlgAudio = audioPanel.AddComponent<VerticalLayoutGroup>();
        vlgAudio.spacing = 30;

        Slider CreateSlider(string name, string label, Transform parent)
        {
            GameObject container = new GameObject(name + "Container");
            container.transform.SetParent(parent, false);
            CreateText("Label", label, container.transform);
            
            GameObject sliderObj = new GameObject(name);
            sliderObj.transform.SetParent(container.transform, false);
            sliderObj.AddComponent<RectTransform>().sizeDelta = new Vector2(160, 20);
            Slider slider = sliderObj.AddComponent<Slider>();
            return slider;
        }

        settingsScript.masterVolumeSlider = CreateSlider("MasterSlider", "Master Volume", audioPanel.transform);
        settingsScript.sfxVolumeSlider = CreateSlider("SFXSlider", "SFX Volume", audioPanel.transform);
        settingsScript.musicVolumeSlider = CreateSlider("MusicSlider", "Music Volume", audioPanel.transform);

        // Graphics Panel content
        VerticalLayoutGroup vlgGraphics = graphicsPanel.AddComponent<VerticalLayoutGroup>();
        vlgGraphics.spacing = 30;

        TMP_Dropdown CreateDropdown(string name, string label, Transform parent)
        {
            GameObject container = new GameObject(name + "Container");
            container.transform.SetParent(parent, false);
            CreateText("Label", label, container.transform);

            GameObject ddObj = new GameObject(name);
            ddObj.transform.SetParent(container.transform, false);
            ddObj.AddComponent<Image>();
            TMP_Dropdown dd = ddObj.AddComponent<TMP_Dropdown>();
            
            // Mock template so it doesn't crash, though it won't look great without proper prefab
            GameObject template = new GameObject("Template");
            template.transform.SetParent(ddObj.transform, false);
            template.AddComponent<RectTransform>();
            dd.template = template.GetComponent<RectTransform>();
            template.SetActive(false);

            return dd;
        }

        settingsScript.resolutionDropdown = CreateDropdown("ResDropdown", "Resolution", graphicsPanel.transform);
        settingsScript.qualityDropdown = CreateDropdown("QualityDropdown", "Graphics Quality", graphicsPanel.transform);

        GameObject toggleObj = new GameObject("FullscreenToggle");
        toggleObj.transform.SetParent(graphicsPanel.transform, false);
        settingsScript.fullscreenToggle = toggleObj.AddComponent<Toggle>();

        Debug.Log("Settings UI Built Successfully!");
    }
}
