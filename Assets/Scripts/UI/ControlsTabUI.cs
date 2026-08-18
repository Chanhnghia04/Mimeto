using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ControlsTabUI : MonoBehaviour
{
    private ScrollRect scrollRect;
    private RectTransform content;

    private bool isListening = false;
    private string listeningAction = null;

    private void Awake()
    {
        BuildUI();
    }

    private void OnEnable()
    {
        RefreshUI();
        if (KeybindManager.Instance != null)
        {
            KeybindManager.Instance.OnBindingsChanged += RefreshUI;
        }
    }

    private void OnDisable()
    {
        isListening = false;
        if (KeybindManager.Instance != null)
        {
            KeybindManager.Instance.OnBindingsChanged -= RefreshUI;
        }
    }

    private void BuildUI()
    {
        if (scrollRect != null) return;

        // 1. Create ScrollView
        GameObject scrollViewObj = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scrollViewObj.transform.SetParent(transform, false);
        
        RectTransform srRect = scrollViewObj.GetComponent<RectTransform>();
        srRect.anchorMin = Vector2.zero;
        srRect.anchorMax = Vector2.one;
        srRect.offsetMin = new Vector2(10, 60); // padding for reset button
        srRect.offsetMax = new Vector2(-10, -10);

        Image srImage = scrollViewObj.GetComponent<Image>();
        srImage.color = new Color(0, 0, 0, 0);

        scrollRect = scrollViewObj.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 20f;

        // 2. Create Viewport
        GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportObj.transform.SetParent(scrollViewObj.transform, false);
        
        RectTransform vpRect = viewportObj.GetComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = Vector2.zero;
        vpRect.offsetMax = Vector2.zero;

        Image vpImage = viewportObj.GetComponent<Image>();
        vpImage.color = Color.white;
        Mask vpMask = viewportObj.GetComponent<Mask>();
        vpMask.showMaskGraphic = false;

        scrollRect.viewport = vpRect;

        // 3. Create Content
        GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObj.transform.SetParent(viewportObj.transform, false);
        content = contentObj.GetComponent<RectTransform>();
        
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = contentObj.GetComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.spacing = 5f;
        vlg.padding = new RectOffset(10, 10, 10, 10);

        ContentSizeFitter csf = contentObj.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = content;

        // 4. Create Reset Button
        CreateResetButton();
    }

    private void CreateHeader(string title)
    {
        GameObject headerObj = new GameObject("HeaderRow", typeof(RectTransform));
        headerObj.transform.SetParent(content, false);
        RectTransform rt = headerObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 40);

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(headerObj.transform, false);
        RectTransform txtRt = textObj.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = title;
        tmp.fontSize = 20;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        ColorUtility.TryParseHtmlString("#E0F7FA", out Color c);
        tmp.color = c;
    }

    private void RefreshUI()
    {
        if (KeybindManager.Instance == null || content == null) return;

        // Clean up
        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }

        CreateHeader("PHÍM ĐIỀU KHIỂN");

        foreach (var entry in KeybindManager.Instance.Bindings)
        {
            CreateRow(entry);
        }
    }

    private void CreateRow(KeybindManager.KeybindEntry entry)
    {
        GameObject rowObj = new GameObject("KeybindRow_" + entry.actionName, typeof(RectTransform), typeof(Image));
        rowObj.transform.SetParent(content, false);
        RectTransform rt = rowObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 40);

        Image bg = rowObj.GetComponent<Image>();
        ColorUtility.TryParseHtmlString("#16213E", out Color bgColor);
        bgColor.a = 0.8f;
        bg.color = bgColor;

        // Action Text
        GameObject labelObj = new GameObject("ActionText", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(rowObj.transform, false);
        RectTransform labelRt = labelObj.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0, 0);
        labelRt.anchorMax = new Vector2(0.5f, 1);
        labelRt.offsetMin = new Vector2(10, 0);
        labelRt.offsetMax = Vector2.zero;

        TextMeshProUGUI labelTmp = labelObj.GetComponent<TextMeshProUGUI>();
        labelTmp.text = entry.displayName;
        labelTmp.fontSize = 16;
        labelTmp.alignment = TextAlignmentOptions.Left;
        ColorUtility.TryParseHtmlString("#E0F7FA", out Color textColor);
        labelTmp.color = textColor;

        // Key Button
        GameObject btnObj = new GameObject("KeyButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(rowObj.transform, false);
        RectTransform btnRt = btnObj.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0.1f);
        btnRt.anchorMax = new Vector2(0.95f, 0.9f);
        btnRt.offsetMin = Vector2.zero;
        btnRt.offsetMax = Vector2.zero;

        Image btnBg = btnObj.GetComponent<Image>();
        btnBg.color = new Color(0, 0, 0, 0.5f);

        Button btn = btnObj.GetComponent<Button>();

        GameObject btnTextObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        btnTextObj.transform.SetParent(btnObj.transform, false);
        RectTransform btnTextRt = btnTextObj.GetComponent<RectTransform>();
        btnTextRt.anchorMin = Vector2.zero;
        btnTextRt.anchorMax = Vector2.one;
        btnTextRt.offsetMin = Vector2.zero;
        btnTextRt.offsetMax = Vector2.zero;

        TextMeshProUGUI btnTmp = btnTextObj.GetComponent<TextMeshProUGUI>();
        btnTmp.text = KeybindManager.GetKeyDisplayName(entry.currentKey);
        btnTmp.fontSize = 16;
        btnTmp.alignment = TextAlignmentOptions.Center;
        
        if (entry.isRebindable)
        {
            ColorUtility.TryParseHtmlString("#00E5FF", out Color cyanColor);
            btnTmp.color = cyanColor;
            btn.onClick.AddListener(() => StartListening(entry.actionName, btnTmp));
        }
        else
        {
            btn.interactable = false;
            btnTmp.color = Color.gray;
        }
    }

    private void CreateResetButton()
    {
        GameObject btnObj = new GameObject("ResetButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(transform, false);
        RectTransform btnRt = btnObj.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0);
        btnRt.anchorMax = new Vector2(0.5f, 0);
        btnRt.pivot = new Vector2(0.5f, 0);
        btnRt.sizeDelta = new Vector2(200, 40);
        btnRt.anchoredPosition = new Vector2(0, 10);

        Image btnBg = btnObj.GetComponent<Image>();
        ColorUtility.TryParseHtmlString("#0F3460", out Color btnColor);
        btnBg.color = btnColor;

        Button btn = btnObj.GetComponent<Button>();
        btn.onClick.AddListener(() => {
            if (KeybindManager.Instance != null)
            {
                KeybindManager.Instance.ResetToDefaults();
            }
        });

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRt = textObj.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = "MẶC ĐỊNH";
        tmp.fontSize = 16;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        ColorUtility.TryParseHtmlString("#E0F7FA", out Color textColor);
        tmp.color = textColor;
    }

    private void StartListening(string actionName, TextMeshProUGUI textComponent)
    {
        if (isListening) return;

        isListening = true;
        listeningAction = actionName;

        textComponent.text = "Nhấn phím...";
        ColorUtility.TryParseHtmlString("#FF6B6B", out Color redColor);
        textComponent.color = redColor;
    }

    private void Update()
    {
        if (isListening)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                isListening = false;
                RefreshUI();
                return;
            }

            foreach (KeyCode kc in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (kc != KeyCode.Escape && Input.GetKeyDown(kc))
                {
                    if (KeybindManager.Instance != null)
                    {
                        KeybindManager.Instance.SetBinding(listeningAction, kc);
                    }
                    isListening = false;
                    break;
                }
            }
        }
    }
}
