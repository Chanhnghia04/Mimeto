using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
using UnityEngine.InputSystem;

/// <summary>
/// UI hiển thị các ô item trong rương.
/// - Mỗi ô hiển thị icon + tên + số lượng của một loại item.
/// - Double-click (hoặc click 2 lần nhanh) vào ô -> chuyển item vào kho Player.
/// 
/// SETUP TRONG INSPECTOR:
///   1. Tạo GameObject "ChestUI" trong scene, gắn script này.
///   2. Gắn các field: chestPanel, slotContainer, slotPrefab, closeButton.
///   3. Gắn Sprite cho từng loại item vào itemSprites[].
///   4. Đặt "ChestUI" GameObject vào đúng Canvas (cùng Canvas với InventoryUI).
/// </summary>
public class ChestUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Panel cha chứa toàn bộ UI rương (bật/tắt khi mở/đóng)")]
    public GameObject chestPanel;

    [Tooltip("Transform chứa các slot item (dùng GridLayoutGroup)")]
    public Transform slotContainer;

    [Tooltip("Prefab của 1 slot item trong rương")]
    public GameObject slotPrefab;

    [Tooltip("Nút đóng UI rương")]
    public Button closeButton;

    [Tooltip("Text hiển thị tiêu đề (tuỳ chọn)")]
    public TextMeshProUGUI titleText;

    [Header("Item Sprites (kéo đúng thứ tự)")]
    public Sprite circuitSprite;
    public Sprite metalPipeSprite;
    public Sprite ironPlateSprite;
    public Sprite chemicalSprite;
    public Sprite plasticPipeSprite;
    public Sprite batterySprite;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private Chest _currentChest;
    private PlayerInventory _playerInventory;
    private readonly List<GameObject> _spawnedSlots = new List<GameObject>();

    // Input action E (Interact) để đóng UI
    private InputAction _interactAction;

    void Start()
    {
        if (chestPanel != null) chestPanel.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        // Tìm action Interact (phím E) từ InputSystem để đóng rương
        var playerInput = FindAnyObjectByType<PlayerInput>();
        if (playerInput != null)
            _interactAction = playerInput.actions.FindAction("Interact");
        else
            _interactAction = InputSystem.actions?.FindAction("Interact");
    }

    void Update()
    {
        // Bấm E khi UI rương đang mở -> đóng lại
        if (chestPanel != null && chestPanel.activeSelf)
        {
            if (_interactAction != null && _interactAction.WasPressedThisFrame())
            {
                Close();
            }
        }
    }

    // ── Mở UI ────────────────────────────────────────────────────────────────

    public void Open(Chest chest, PlayerInventory inventory)
    {
        _currentChest     = chest;
        _playerInventory  = inventory;

        if (titleText != null)
            titleText.text = "🗃  RƯƠNG";

        RebuildSlots();

        if (chestPanel != null) chestPanel.SetActive(true);

        // Hiện con trỏ
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    // ── Đóng UI ──────────────────────────────────────────────────────────────

    public void Close()
    {
        if (chestPanel != null) chestPanel.SetActive(false);
        ClearSlots();

        _currentChest    = null;
        _playerInventory = null;

        // Khoá con trỏ lại
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    // ── Tạo lại các slot từ danh sách item trong rương ───────────────────────

    void RebuildSlots()
    {
        ClearSlots();

        if (_currentChest == null || slotContainer == null || slotPrefab == null) return;

        foreach (Chest.ChestItemEntry entry in _currentChest.items)
        {
            GameObject slotGO = Instantiate(slotPrefab, slotContainer);
            slotGO.name = "ChestSlot_" + entry.itemType;
            _spawnedSlots.Add(slotGO);

            // Gắn dữ liệu & sự kiện vào slot
            ChestSlotUI slotUI = slotGO.GetComponent<ChestSlotUI>();
            if (slotUI != null)
            {
                slotUI.Setup(entry, GetSpriteForType(entry.itemType), OnSlotDoubleClicked);
            }
        }
    }

    void ClearSlots()
    {
        foreach (GameObject go in _spawnedSlots)
            if (go != null) Destroy(go);
        _spawnedSlots.Clear();
    }

    // ── Xử lý khi slot được double-click ─────────────────────────────────────

    void OnSlotDoubleClicked(Chest.ChestItemEntry entry)
    {
        if (_playerInventory == null || _currentChest == null) return;

        // Chuyển item vào kho Player
        _playerInventory.AddScrap(entry.itemType, entry.amount);
        Debug.Log($"[ChestUI] Lấy từ rương: {entry.itemType} x{entry.amount}");

        // Xoá item khỏi rương
        _currentChest.RemoveItem(entry);

        // Cập nhật lại UI
        RebuildSlots();

        // Nếu rương hết đồ, đóng UI
        if (_currentChest.isEmpty)
        {
            Close();
        }
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    Sprite GetSpriteForType(string type)
    {
        switch (type)
        {
            case "circuit":      return circuitSprite;
            case "metal_pipe":   return metalPipeSprite;
            case "iron_plate":   return ironPlateSprite;
            case "chemical":     return chemicalSprite;
            case "plastic_pipe": return plasticPipeSprite;
            case "battery":      return batterySprite;
            default:             return null;
        }
    }

    // ── Lấy tên hiển thị đẹp hơn ─────────────────────────────────────────────

    public static string GetDisplayName(string type)
    {
        switch (type)
        {
            case "circuit":      return "Mạch Điện";
            case "metal_pipe":   return "Ống Kim Loại";
            case "iron_plate":   return "Tấm Sắt";
            case "chemical":     return "Bình Hoá Chất";
            case "plastic_pipe": return "Ống Nhựa";
            case "battery":      return "Pin";
            default:             return type;
        }
    }
}
