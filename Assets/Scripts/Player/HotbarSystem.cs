using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class HotbarSystem : MonoBehaviour
{
    public static HotbarSystem Instance;
    public string[] hotbarItems = new string[3];

    private GameObject hotbarCanvas;
    private GameObject hotbarPanelObj;
    private GameObject targetInvPanel;
    private Image[] slotImages = new Image[3];

    public Sprite fallbackSprite;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        // Loại bỏ logic ở Start vì IsOwner có thể chưa sẵn sàng nếu script đã nằm sẵn trên Prefab
    }

    void Update()
    {
        var pc = GetComponent<PlayerController>();
        if (pc != null && !pc.IsOwner) return;

        // Bắt đầu khởi tạo UI nếu chưa có (khi IsOwner đã là true)
        if (hotbarCanvas == null)
        {
            CreateHotbarUI();
        }

        HandleHotkeys();

        // HIỂN THỊ CHUNG VỚI TÚI ĐỒ: Hotbar chỉ hiện ra khi InventoryPanel đang được mở
        var invUI = GetComponent<InventoryUI>();
        if (hotbarPanelObj != null && invUI != null && invUI.inventoryPanel != null)
        {
            if (hotbarPanelObj.activeSelf != invUI.inventoryPanel.activeInHierarchy)
            {
                hotbarPanelObj.SetActive(invUI.inventoryPanel.activeInHierarchy);
            }
        }
    }

    void CreateHotbarUI()
    {
        // QUAY LẠI CÁCH CHẮC CHẮN NHẤT: Dùng 1 Canvas riêng đè lên trên cùng (không sợ bị che, không sợ lệch)
        hotbarCanvas = new GameObject("HotbarCanvas");
        Canvas canvas = hotbarCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 99; // Lớp trên cùng
        
        CanvasScaler scaler = hotbarCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        
        hotbarCanvas.AddComponent<GraphicRaycaster>();

        // Tạo khung Panel chứa 3 ô Hotbar
        hotbarPanelObj = new GameObject("HotbarPanel");
        hotbarPanelObj.transform.SetParent(hotbarCanvas.transform, false);
        hotbarPanelObj.SetActive(false); // Ẩn lúc mới tạo để Update tự check scene
        
        RectTransform rt = hotbarPanelObj.AddComponent<RectTransform>();
        // Neo ở rìa bên TRÁI màn hình
        rt.anchorMin = new Vector2(0, 0.5f);
        rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(110, 0); // Dịch sang phải thêm 30px (80 -> 110)
        rt.sizeDelta = new Vector2(150, 480);

        VerticalLayoutGroup vlg = hotbarPanelObj.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 20; // Khoảng cách rộng ra xíu cho thoáng

        for (int i = 0; i < 3; i++)
        {
            GameObject slotObj = new GameObject("HotbarSlot_" + i);
            slotObj.transform.SetParent(hotbarPanelObj.transform, false);
            
            RectTransform slotRt = slotObj.AddComponent<RectTransform>();
            slotRt.sizeDelta = new Vector2(96, 96);
            
            Image bgImg = slotObj.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.12f, 0.15f, 0.9f); // Nền xám xanh đen mờ, ngầu hơn
            
            // Viền phát sáng tech-style
            Outline outline = slotObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.1f, 0.8f, 1f, 0.5f); // Màu viền cyan trong suốt
            outline.effectDistance = new Vector2(3, -3);
            
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(slotObj.transform, false);
            RectTransform iconRt = iconObj.AddComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(8, 8); // Thu nhỏ icon lại tí để có khoảng lùi với viền
            iconRt.offsetMax = new Vector2(-8, -8);
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.enabled = false;
            slotImages[i] = iconImg;
            
            GameObject textObj = new GameObject("HotkeyText");
            textObj.transform.SetParent(slotObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0, 1);
            textRt.anchorMax = new Vector2(0, 1);
            textRt.pivot = new Vector2(0, 1);
            textRt.anchoredPosition = new Vector2(8, -8);
            textRt.sizeDelta = new Vector2(30, 30);
            
            TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>();
            txt.text = (i + 1).ToString();
            txt.fontSize = 22;
            txt.color = new Color(1f, 0.8f, 0.2f, 1f); // Màu vàng gold xịn xò
            txt.fontStyle = FontStyles.Bold;

            GameObject nameObj = new GameObject("ItemNameText");
            nameObj.transform.SetParent(slotObj.transform, false);
            RectTransform nameRt = nameObj.AddComponent<RectTransform>();
            nameRt.anchorMin = Vector2.zero; nameRt.anchorMax = Vector2.one;
            nameRt.offsetMin = new Vector2(0, 5); // Nhích tên lên một chút khỏi viền dưới
            nameRt.offsetMax = new Vector2(0, -5);
            TextMeshProUGUI nameTxt = nameObj.AddComponent<TextMeshProUGUI>();
            nameTxt.text = "";
            nameTxt.fontSize = 16;
            nameTxt.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            nameTxt.alignment = TextAlignmentOptions.Bottom; // Căn dưới cùng thay vì giữa để không đè icon
            nameTxt.fontStyle = FontStyles.Bold;

            UIDropSlot dropSlot = slotObj.AddComponent<UIDropSlot>();
            dropSlot.slotIndex = i;
            
            UIDragItem dragItem = slotObj.AddComponent<UIDragItem>();
        }
    }

    void HandleHotkeys()
    {
        var pc = GetComponent<PlayerController>();
        if (pc == null || !pc.IsOwner) return;

        bool uiOpen = pc.IsUIOpen() || PlayerSurvival.IsGameOverUIOpen();
        if (uiOpen) return;

        // Check hotkeys
        if (Input.GetKeyDown(KeyCode.Alpha1)) EquipFromSlot(0, pc);
        if (Input.GetKeyDown(KeyCode.Alpha2)) EquipFromSlot(1, pc);
        if (Input.GetKeyDown(KeyCode.Alpha3)) EquipFromSlot(2, pc);
    }

    void EquipFromSlot(int index, PlayerController pc)
    {
        string itemId = hotbarItems[index];
        if (string.IsNullOrEmpty(itemId)) return;

        pc.EquipWeaponFromHotbar(itemId);
    }

    public void OnItemDropped(int slotIndex, string itemType)
    {
        if (string.IsNullOrEmpty(itemType)) return;
        
        // Remove from other slots if it's unique (like tools)
        for (int i = 0; i < 3; i++)
        {
            if (hotbarItems[i] == itemType)
            {
                hotbarItems[i] = "";
                slotImages[i].enabled = false;
                
                // Clear the text too
                Transform oldNameT = slotImages[i].transform.parent.Find("ItemNameText");
                if (oldNameT != null)
                {
                    var oldTxt = oldNameT.GetComponent<TextMeshProUGUI>();
                    if (oldTxt != null) oldTxt.text = "";
                }
                
                UIDragItem oldDrag = slotImages[i].transform.parent.GetComponent<UIDragItem>();
                if (oldDrag != null) oldDrag.itemType = "";
            }
        }

        hotbarItems[slotIndex] = itemType;
        
        UIDragItem newDrag = slotImages[slotIndex].transform.parent.GetComponent<UIDragItem>();
        if (newDrag != null) newDrag.itemType = itemType;
        
        InventoryUI invUI = GetComponent<InventoryUI>();
        Sprite sp = null;
        if (invUI != null) sp = invUI.GetSpriteForType(itemType);
        
        slotImages[slotIndex].sprite = sp != null ? sp : fallbackSprite;
        slotImages[slotIndex].enabled = true;
        
        // Cập nhật Text tên món đồ
        Transform nameT = slotImages[slotIndex].transform.parent.Find("ItemNameText");
        if (nameT != null)
        {
            var txt = nameT.GetComponent<TextMeshProUGUI>();
            if (txt != null)
            {
                if (sp == null) // Nếu không có hình, hiển thị 3 chữ cái đầu
                    txt.text = itemType.Substring(0, Mathf.Min(3, itemType.Length)).ToUpper();
                else
                    txt.text = "";
            }
        }
        
        // Cập nhật lại kho đồ để nó giấu món này đi
        if (invUI != null)
        {
            invUI.UpdateUI();
        }
    }
    public void ClearSlot(int slotIndex)
    {
        hotbarItems[slotIndex] = "";
        slotImages[slotIndex].enabled = false;
        
        Transform nameT = slotImages[slotIndex].transform.parent.Find("ItemNameText");
        if (nameT != null)
        {
            var txt = nameT.GetComponent<TextMeshProUGUI>();
            if (txt != null) txt.text = "";
        }
        
        UIDragItem drag = slotImages[slotIndex].transform.parent.GetComponent<UIDragItem>();
        if (drag != null) drag.itemType = "";

        InventoryUI invUI = GetComponent<InventoryUI>();
        if (invUI != null) invUI.UpdateUI();
    }
}

public class UIDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public string itemType = "";
    private GameObject dragIcon;
    private Canvas canvas;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(itemType)) return;

        dragIcon = new GameObject("DragIcon");
        canvas = GetComponentInParent<Canvas>();
        dragIcon.transform.SetParent(canvas.transform, false);
        dragIcon.transform.SetAsLastSibling();

        Image img = dragIcon.AddComponent<Image>();
        Image myImg = transform.Find("Icon")?.GetComponent<Image>();
        if (myImg != null) img.sprite = myImg.sprite;
        img.raycastTarget = false;

        RectTransform rt = dragIcon.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(60, 60);
        rt.position = Input.mousePosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragIcon != null)
        {
            dragIcon.transform.position = Input.mousePosition;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragIcon != null)
        {
            Destroy(dragIcon);
        }
        
        // Kiểm tra xem có phải là đang kéo 1 ô Hotbar ra ngoài không
        UIDropSlot myDrop = GetComponent<UIDropSlot>();
        if (myDrop != null && HotbarSystem.Instance != null)
        {
            GameObject droppedOn = eventData.pointerCurrentRaycast.gameObject;
            // Nếu ném ra ngoài không trúng ô Hotbar nào khác -> Tháo vũ khí (Clear)
            if (droppedOn == null || droppedOn.GetComponentInParent<UIDropSlot>() == null)
            {
                HotbarSystem.Instance.ClearSlot(myDrop.slotIndex);
            }
        }
    }
}

public class UIDropSlot : MonoBehaviour, IDropHandler
{
    public int slotIndex;

    public void OnDrop(PointerEventData eventData)
    {
        UIDragItem dragItem = eventData.pointerDrag?.GetComponent<UIDragItem>();
        if (dragItem != null && !string.IsNullOrEmpty(dragItem.itemType))
        {
            if (HotbarSystem.Instance != null)
            {
                HotbarSystem.Instance.OnItemDropped(slotIndex, dragItem.itemType);
            }
        }
    }
}
