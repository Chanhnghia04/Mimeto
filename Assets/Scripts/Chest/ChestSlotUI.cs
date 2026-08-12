using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

/// <summary>
/// Gắn vào Prefab của mỗi slot trong UI rương.
/// Xử lý double-click (2 lần click trong 0.4 giây) để lấy đồ.
///
/// Cấu trúc Prefab slot gợi ý:
///   SlotPrefab (Image - background)
///   ├─ ItemIcon   (Image)
///   ├─ ItemName   (TextMeshProUGUI)
///   ├─ AmountText (TextMeshProUGUI)
///   └─ HintText   (TextMeshProUGUI - "Double-click để lấy")
/// </summary>
public class ChestSlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References (tự gán hoặc tìm theo tên)")]
    public Image itemIcon;
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI amountText;
    public TextMeshProUGUI hintText;
    public Image background;

    [Header("Visual Settings")]
    public Color normalColor   = new Color(0.15f, 0.15f, 0.15f, 0.85f);
    public Color hoverColor    = new Color(0.25f, 0.55f, 0.85f, 0.9f);
    public Color clickedColor  = new Color(0.1f,  0.8f,  0.3f,  0.9f);

    // ── Runtime ───────────────────────────────────────────────────────────────
    private Chest.ChestItemEntry _entry;
    private Action<Chest.ChestItemEntry> _onDoubleClick;

    private float _lastClickTime = -10f;
    private const float DoubleClickThreshold = 0.45f;

    // ── Setup ─────────────────────────────────────────────────────────────────

    public void Setup(Chest.ChestItemEntry entry, Sprite icon, Action<Chest.ChestItemEntry> onDoubleClick)
    {
        _entry         = entry;
        _onDoubleClick = onDoubleClick;

        // Auto-find children nếu chưa gán
        if (itemIcon     == null) itemIcon     = transform.Find("ItemIcon")?.GetComponent<Image>();
        if (itemNameText == null) itemNameText = transform.Find("ItemName")?.GetComponent<TextMeshProUGUI>();
        if (amountText   == null) amountText   = transform.Find("AmountText")?.GetComponent<TextMeshProUGUI>();
        if (hintText     == null) hintText     = transform.Find("HintText")?.GetComponent<TextMeshProUGUI>();
        if (background   == null) background   = GetComponent<Image>();

        // Đặt nội dung
        if (itemIcon != null)
        {
            itemIcon.sprite  = icon;
            itemIcon.enabled = icon != null;
        }

        if (itemNameText != null)
            itemNameText.text = "";

        if (amountText != null)
            amountText.text = $"x{entry.amount}";

        if (hintText != null)
            hintText.text = "Double-click để lấy";

        SetBackground(normalColor);
    }

    // ── IPointerClickHandler ─────────────────────────────────────────────────

    public void OnPointerClick(PointerEventData eventData)
    {
        float now = Time.unscaledTime;

        if (now - _lastClickTime <= DoubleClickThreshold)
        {
            // Double-click!
            SetBackground(clickedColor);
            _onDoubleClick?.Invoke(_entry);
        }
        else
        {
            // Single-click: chỉ highlight
            SetBackground(hoverColor);
        }

        _lastClickTime = now;
    }

    // ── IPointerEnterHandler / IPointerExitHandler ────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetBackground(hoverColor);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetBackground(normalColor);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    void SetBackground(Color c)
    {
        if (background != null) background.color = c;
    }
}
