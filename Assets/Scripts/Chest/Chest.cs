using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gắn vào prefab rương. Khi Player bấm E sẽ mở ChestUI hiển thị các ô item.
/// Implements IInteractable để hoạt động với InteractionSystem sẵn có.
/// </summary>
public class Chest : MonoBehaviour, IInteractable
{
    [System.Serializable]
    public class ChestItemEntry
    {
        public string itemType;   // trùng key trong PlayerInventory.AddScrap()
        public int amount;
    }

    [Header("Chest Contents (generated at runtime)")]
    public List<ChestItemEntry> items = new List<ChestItemEntry>();

    [Header("Random Loot Settings")]
    [Tooltip("Số slot item ngẫu nhiên sẽ được tạo ra khi rương spawn")]
    public int minSlots = 2;
    public int maxSlots = 5;

    [Header("State")]
    public bool isOpen = false;
    public bool isEmpty = false;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip openClip;

    // ── Danh sách item có thể xuất hiện trong rương ──────────────────────────
    private static readonly string[] PossibleItems = new string[]
    {
        "circuit",
        "metal_pipe",
        "iron_plate",
        "chemical",
        "plastic_pipe",
        "battery"
    };

    // ── Trọng số xác suất (index tương ứng với PossibleItems) ────────────────
    // Số càng lớn -> xuất hiện càng nhiều
    private static readonly int[] ItemWeights = new int[]
    {
        20,  // circuit       (mạch điện)
        25,  // metal_pipe    (ống kim loại)
        20,  // iron_plate    (tấm sắt)
        15,  // chemical      (ống sắt hoá chất)
        15,  // plastic_pipe  (ống nhựa)
        5    // battery       (pin - hiếm hơn)
    };

    private Animator _animator;
    private static readonly int OpenHash = Animator.StringToHash("Open");

    // ── Cached reference tới ChestUI (tìm một lần) ──────────────────────────
    private static ChestUI _chestUI;

    void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        GenerateLoot();
    }

    // ── Sinh loot ngẫu nhiên ─────────────────────────────────────────────────

    void GenerateLoot()
    {
        items.Clear();
        int slotCount = Random.Range(minSlots, maxSlots + 1);

        for (int i = 0; i < slotCount; i++)
        {
            string type = PickWeightedItem();
            int amount  = GetRandomAmount(type);

            // Nếu item cùng loại đã có thì cộng thêm số lượng
            ChestItemEntry existing = items.Find(e => e.itemType == type);
            if (existing != null)
            {
                existing.amount += amount;
            }
            else
            {
                items.Add(new ChestItemEntry { itemType = type, amount = amount });
            }
        }
    }

    string PickWeightedItem()
    {
        int totalWeight = 0;
        foreach (int w in ItemWeights) totalWeight += w;

        int roll = Random.Range(0, totalWeight);
        int cumulative = 0;
        for (int i = 0; i < ItemWeights.Length; i++)
        {
            cumulative += ItemWeights[i];
            if (roll < cumulative)
                return PossibleItems[i];
        }
        return PossibleItems[0];
    }

    int GetRandomAmount(string type)
    {
        switch (type)
        {
            case "metal_pipe": return Random.Range(1, 4);
            case "circuit":    return Random.Range(1, 3);
            default:           return Random.Range(1, 3);
        }
    }

    // ── IInteractable ────────────────────────────────────────────────────────

    public void Interact(GameObject interactor)
    {
        if (isEmpty)
        {
            Debug.Log("[Chest] Rương đã trống.");
            return;
        }

        // Tìm ChestUI (cache lần đầu)
        if (_chestUI == null)
            _chestUI = Object.FindAnyObjectByType<ChestUI>();

        if (_chestUI == null)
        {
            // Thử tìm kiếm thêm một lần nữa trong trường hợp UI vừa được instantiate
            _chestUI = Object.FindAnyObjectByType<ChestUI>();
            
            if (_chestUI == null)
            {
                Debug.LogError("[Chest] Không tìm thấy ChestUI trong scene! Hãy đảm bảo đã có GameObject gắn script ChestUI.");
                return;
            }
        }

        // Lấy PlayerInventory từ interactor (Tìm linh hoạt hơn)
        PlayerInventory inv = interactor.GetComponentInParent<PlayerInventory>();
        if (inv == null) 
        {
            inv = interactor.GetComponentInChildren<PlayerInventory>();
        }

        if (inv == null)
        {
            Debug.LogError($"[Chest] Không tìm thấy PlayerInventory trên {interactor.name}. Vui lòng kiểm tra cấu trúc Player.");
            return;
        }

        // Mở animation rương
        if (!isOpen)
        {
            isOpen = true;
            if (_animator != null)
                _animator.SetTrigger(OpenHash);
            
            if (audioSource != null && openClip != null)
                audioSource.PlayOneShot(openClip);
        }

        // Mở UI rương
        _chestUI.Open(this, inv);
    }

    /// <summary>
    /// Gọi từ ChestUI khi player lấy hết đồ.
    /// </summary>
    public void MarkEmpty()
    {
        isEmpty = true;
        items.Clear();
    }

    /// <summary>
    /// Xoá 1 item khỏi danh sách rương sau khi player lấy.
    /// </summary>
    public void RemoveItem(ChestItemEntry entry)
    {
        items.Remove(entry);
        if (items.Count == 0)
            MarkEmpty();
    }
}
