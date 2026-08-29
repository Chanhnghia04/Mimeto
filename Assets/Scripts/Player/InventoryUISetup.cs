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

        // --- CRAFTING PANEL REMOVED ---

#if UNITY_EDITOR
        EditorUtility.SetDirty(invUI);
        EditorUtility.SetDirty(player);
        AssetDatabase.SaveAssets();
#endif
        Debug.Log("Visual UI Setup Updated!");
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
