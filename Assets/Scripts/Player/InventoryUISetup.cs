using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class InventoryUISetup : MonoBehaviour
{
    [ContextMenu("Setup Visual UI")]
    public void Setup()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null) return;

        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("InventoryCanvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }

#if UNITY_EDITOR
        Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI_Panel_Industrial_Clean.png");
        Sprite btnSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI_Button_Hazard_Clean.png");
        Sprite slotSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI_Slot_Frame.png");
#else
        Sprite panelSprite = null;
        Sprite btnSprite = null;
        Sprite slotSprite = null;
#endif

        // --- CLEANUP OLD UI ---
List<GameObject> toDestroy = new List<GameObject>();
        foreach (Transform child in canvas.transform)
        {
            if (child.name == "InventoryPanel" || child.name == "CraftingPanel")
            {
                toDestroy.Add(child.gameObject);
            }
        }
        foreach (GameObject obj in toDestroy)
        {
            DestroyImmediate(obj);
        }

        // --- INVENTORY PANEL ---
        GameObject invPanel = CreatePanel(canvas.transform, "InventoryPanel", new Vector2(0, 0), new Vector2(480, 300));
        if (panelSprite != null)
        {
            Image pImg = invPanel.GetComponent<Image>();
            pImg.sprite = panelSprite;
            pImg.type = Image.Type.Sliced;
            pImg.color = Color.white;
        }
        invPanel.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0.5f);
        invPanel.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
        invPanel.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
        invPanel.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        invPanel.AddComponent<UIDrag>(); 
        invPanel.SetActive(false);

        InventoryUI invUI = player.GetComponent<InventoryUI>();
        if (invUI == null) invUI = player.AddComponent<InventoryUI>();
#if UNITY_EDITOR
        Undo.RecordObject(invUI, "Setup Inventory UI");
#endif
        invUI.inventoryPanel = invPanel;
        invUI.inventory = player.GetComponent<PlayerInventory>();
        invUI.survival = player.GetComponent<PlayerSurvival>();
        invUI.gridSlots.Clear();

        // --- GRID CONTAINER ---
        GameObject gridGo = new GameObject("GridContainer");
        gridGo.transform.SetParent(invPanel.transform, false);
        RectTransform gridRect = gridGo.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 0.5f);
        gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        gridRect.anchoredPosition = Vector2.zero;
        gridRect.sizeDelta = new Vector2(440, 260);

        GridLayoutGroup gridLayout = gridGo.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(80, 80);
        gridLayout.spacing = new Vector2(10, 10);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 5;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.childAlignment = TextAnchor.MiddleCenter;

        for (int i = 0; i < 15; i++)
        {
            GameObject slotBg = new GameObject("Slot_" + i);
            slotBg.transform.SetParent(gridGo.transform, false);
            Image bgImg = slotBg.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            bgImg.raycastTarget = true;

            InventoryItemDrag drag = slotBg.AddComponent<InventoryItemDrag>();
            drag.slotIndex = i;

            GameObject iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(slotBg.transform, false);
            Image iconImg = iconGo.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            RectTransform iconR = iconGo.GetComponent<RectTransform>();
            iconR.anchorMin = Vector2.zero;
            iconR.anchorMax = Vector2.one;
            iconR.sizeDelta = new Vector2(-15, -15);

            GameObject amtGo = new GameObject("Amount");
            amtGo.transform.SetParent(slotBg.transform, false);
            TextMeshProUGUI amtText = amtGo.AddComponent<TextMeshProUGUI>();
            amtText.fontSize = 16;
            amtText.alignment = TextAlignmentOptions.BottomRight;
            amtText.raycastTarget = false;
            RectTransform amtR = amtGo.GetComponent<RectTransform>();
            amtR.anchorMin = Vector2.zero;
            amtR.anchorMax = Vector2.one;
            amtR.sizeDelta = new Vector2(-8, -8);

            GridSlot slot = new GridSlot();
            slot.bgObj = slotBg;
            slot.icon = iconImg;
            slot.amountText = amtText;
            invUI.gridSlots.Add(slot);
        }

        // --- CRAFTING PANEL (Fixed & Scrollable) ---
        GameObject craftPanel = CreatePanel(canvas.transform, "CraftingPanel", new Vector2(0, 0), new Vector2(700, 500));
        if (panelSprite != null)
        {
            Image pImg = craftPanel.GetComponent<Image>();
            pImg.sprite = panelSprite;
            pImg.type = Image.Type.Sliced;
            pImg.color = Color.white; // Use texture color
        }
        else
        {
            craftPanel.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.07f, 0.95f);
        }

        craftPanel.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0.5f);
        craftPanel.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
        craftPanel.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
        craftPanel.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        craftPanel.SetActive(false);

        CraftingUI craftUI = player.GetComponent<CraftingUI>();
        if (craftUI == null) craftUI = player.AddComponent<CraftingUI>();
#if UNITY_EDITOR
        Undo.RecordObject(craftUI, "Setup Crafting UI");
#endif
        craftUI.craftingPanel = craftPanel;
        craftUI.inventory = invUI.inventory;
        craftUI.survival = invUI.survival;

        GameObject titleGo = new GameObject("Title");
        titleGo.transform.SetParent(craftPanel.transform, false);
        TextMeshProUGUI titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "CRAFTING STATION";
        titleTxt.fontSize = 32;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = new Color(0.96f, 0.48f, 0f, 1f); // Hazard Orange
        titleGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 215);

        // Scroll View Setup
        GameObject scrollViewGo = new GameObject("CraftingScrollView");
        scrollViewGo.transform.SetParent(craftPanel.transform, false);
        RectTransform scrollRectTransform = scrollViewGo.AddComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0, 0);
        scrollRectTransform.anchorMax = new Vector2(1, 1);
        scrollRectTransform.offsetMin = new Vector2(30, 80);
        scrollRectTransform.offsetMax = new Vector2(-30, -80);
        ScrollRect scrollRect = scrollViewGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 25;

        GameObject viewportGo = new GameObject("Viewport");
        viewportGo.transform.SetParent(scrollViewGo.transform, false);
        RectTransform viewportRect = viewportGo.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportGo.AddComponent<Image>().color = new Color(0, 0, 0, 0.2f);
        viewportGo.AddComponent<RectMask2D>();
        scrollRect.viewport = viewportRect;

        GameObject contentGo = new GameObject("Content");
        contentGo.transform.SetParent(viewportGo.transform, false);
        RectTransform contentRect = contentGo.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 400);
        scrollRect.content = contentRect;

        GridLayoutGroup contentGrid = contentGo.AddComponent<GridLayoutGroup>();
        contentGrid.cellSize = new Vector2(150, 240);
        contentGrid.spacing = new Vector2(20, 25);
        contentGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        contentGrid.constraintCount = 4;
        contentGrid.childAlignment = TextAnchor.UpperCenter;
        contentGrid.padding = new RectOffset(15, 15, 15, 15);

        contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Recipes
        craftUI.craftBasicButton = CreateVisualCraftOption(contentGo.transform, "Basic Mask", btnSprite, slotSprite, out craftUI.basicRecipeIcons);
craftUI.craftAdvancedButton = CreateVisualCraftOption(contentGo.transform, "Adv Mask", btnSprite, slotSprite, out craftUI.advancedRecipeIcons);
        craftUI.craftUVButton = CreateVisualCraftOption(contentGo.transform, "UV Light", btnSprite, slotSprite, out craftUI.uvRecipeIcons);
        craftUI.craftCrowbarButton = CreateVisualCraftOption(contentGo.transform, "Crowbar", btnSprite, slotSprite, out craftUI.crowbarRecipeIcons);
        craftUI.craftShovelButton = CreateVisualCraftOption(contentGo.transform, "Shovel", btnSprite, slotSprite, out craftUI.shovelRecipeIcons);
        craftUI.craftMacheteButton = CreateVisualCraftOption(contentGo.transform, "Machete", btnSprite, slotSprite, out craftUI.macheteRecipeIcons);
        craftUI.craftAxeButton = CreateVisualCraftOption(contentGo.transform, "Fire Axe", btnSprite, slotSprite, out craftUI.axeRecipeIcons);
        craftUI.craftBatButton = CreateVisualCraftOption(contentGo.transform, "Spiked Bat", btnSprite, slotSprite, out craftUI.batRecipeIcons);

        GameObject statusGo = new GameObject("StatusText");
        statusGo.transform.SetParent(craftPanel.transform, false);
        craftUI.statusText = statusGo.AddComponent<TextMeshProUGUI>();
        craftUI.statusText.alignment = TextAlignmentOptions.Center;
        craftUI.statusText.fontSize = 20;
        craftUI.statusText.color = new Color(0.82f, 0.84f, 0.86f, 1f); // Ashen
        statusGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -220);

#if UNITY_EDITOR
        EditorUtility.SetDirty(invUI);
        EditorUtility.SetDirty(craftUI);
        EditorUtility.SetDirty(player);
        AssetDatabase.SaveAssets();
#endif
        Debug.Log("Visual UI Setup Updated!");
}

    Button CreateVisualCraftOption(Transform parent, string label, Sprite btnSprite, Sprite slotSprite, out Image[] recipeIcons)
    {
        GameObject container = new GameObject(label + "_Container");
        container.transform.SetParent(parent, false);
        RectTransform contRect = container.AddComponent<RectTransform>();
        contRect.sizeDelta = new Vector2(150, 240);

        Image bg = container.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.11f, 0.12f, 0.9f); // Corroded steel tone
        
        // Add a subtle border or frame if needed
        GameObject frameGo = new GameObject("Frame");
        frameGo.transform.SetParent(container.transform, false);
        Image frameImg = frameGo.AddComponent<Image>();
        frameImg.sprite = slotSprite;
        frameImg.type = Image.Type.Sliced;
        frameImg.color = new Color(1, 1, 1, 0.3f);
        RectTransform frameR = frameGo.GetComponent<RectTransform>();
        frameR.anchorMin = Vector2.zero;
        frameR.anchorMax = Vector2.one;
        frameR.sizeDelta = Vector2.zero;

        GameObject titleGo = new GameObject("Title");
        titleGo.transform.SetParent(container.transform, false);
        TextMeshProUGUI t = titleGo.AddComponent<TextMeshProUGUI>();
        t.text = label;
        t.fontSize = 16;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.82f, 0.84f, 0.86f, 1f);
        RectTransform tRect = titleGo.GetComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0.5f, 1);
        tRect.anchorMax = new Vector2(0.5f, 1);
        tRect.pivot = new Vector2(0.5f, 1);
        tRect.anchoredPosition = new Vector2(0, -10);
        tRect.sizeDelta = new Vector2(140, 30);

        recipeIcons = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject frame = new GameObject("Recipe_" + i);
            frame.transform.SetParent(container.transform, false);
            Image fImg = frame.AddComponent<Image>();
            fImg.sprite = slotSprite;
            fImg.type = Image.Type.Sliced;
            fImg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            
            RectTransform fRect = frame.GetComponent<RectTransform>();
            fRect.anchoredPosition = new Vector2(0, 65 - (i * 38));
            fRect.sizeDelta = new Vector2(34, 34);

            GameObject icon = new GameObject("Icon");
            icon.transform.SetParent(frame.transform, false);
            recipeIcons[i] = icon.AddComponent<Image>();
            recipeIcons[i].preserveAspect = true;
            RectTransform iRect = icon.GetComponent<RectTransform>();
            iRect.anchorMin = Vector2.zero;
            iRect.anchorMax = Vector2.one;
            iRect.sizeDelta = new Vector2(-6, -6);
        }

        GameObject btnGo = new GameObject("CraftButton");
        btnGo.transform.SetParent(container.transform, false);
        Button btn = btnGo.AddComponent<Button>();
        Image btnImg = btnGo.AddComponent<Image>();
        btnImg.sprite = btnSprite;
        btnImg.type = Image.Type.Sliced;
        btnImg.color = Color.white;
        
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1, 1, 1, 1.2f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        btn.colors = colors;

        RectTransform btnRect = btnGo.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0);
        btnRect.anchorMax = new Vector2(0.5f, 0);
        btnRect.pivot = new Vector2(0.5f, 0);
        btnRect.anchoredPosition = new Vector2(0, 15);
        btnRect.sizeDelta = new Vector2(130, 40);

        GameObject btnTxtGo = new GameObject("BtnTxt");
        btnTxtGo.transform.SetParent(btnGo.transform, false);
        TextMeshProUGUI btnTxt = btnTxtGo.AddComponent<TextMeshProUGUI>();
        btnTxt.text = "CRAFT";
        btnTxt.fontSize = 14;
        btnTxt.fontStyle = FontStyles.Bold;
        btnTxt.alignment = TextAlignmentOptions.Center;
        btnTxt.color = Color.black;
        RectTransform btnTxtRect = btnTxtGo.GetComponent<RectTransform>();
        btnTxtRect.anchorMin = Vector2.zero;
        btnTxtRect.anchorMax = Vector2.one;
        btnTxtRect.sizeDelta = Vector2.zero;

        btnGo.AddComponent<UIJuice>();

        return btn;
    }

    GameObject CreatePanel(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        panel.AddComponent<Image>().color = new Color(0, 0, 0, 0.8f);
        return panel;
    }

    void CreateText(Transform parent, string content, int size, Vector2 pos)
    {
        GameObject go = new GameObject(content);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = content;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        go.GetComponent<RectTransform>().anchoredPosition = pos;
    }
}
