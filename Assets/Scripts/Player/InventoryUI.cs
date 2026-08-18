using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.InputSystem;

[System.Serializable]
public class GridSlot
{
    public GameObject bgObj;
    public Image icon;
    public TextMeshProUGUI amountText;
    public string currentItemType = "";
}

public class InventoryUI : MonoBehaviour
{
    public PlayerInventory inventory;
    public PlayerSurvival survival;

    public GameObject inventoryPanel;
    
    [Header("UI Grid Slots")]
    public List<GridSlot> gridSlots = new List<GridSlot>();

    [Header("Item Sprites")]
    public Sprite circuitSprite;
    public Sprite pipeSprite;
    public Sprite chemicalSprite;
    public Sprite plasticSprite;
    public Sprite batterySprite;
    public Sprite ironPlateSprite;

    private bool isVisible = false;
    private InputAction _inventoryAction;

    void Start()
    {
        var playerInput = GetComponentInParent<PlayerInput>();
        if (playerInput != null)
        {
            _inventoryAction = playerInput.actions.FindAction("Inventory");
        }
        else
        {
            _inventoryAction = InputSystem.actions?.FindAction("Inventory");
        }

        // Tự động tìm InventoryPanel bằng phương pháp tuyệt đối
        if (inventoryPanel == null)
        {
            GameObject[] allObjs = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var obj in allObjs)
            {
                // Chỉ lấy object nằm trong Scene (bỏ qua Prefab trong project) và phải có GridContainer (tránh nhầm panel bị hỏng)
                if (obj.name == "InventoryPanel" && obj.scene.IsValid() && obj.transform.Find("GridContainer") != null)
                {
                    inventoryPanel = obj;
                    break;
                }
            }
        }

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(isVisible);
            inventoryPanel.transform.localScale = new Vector3(0.64f, 0.64f, 1f);
        }
        else
        {
            Debug.LogWarning("[InventoryUI] Không tìm thấy InventoryPanel lúc khởi tạo. Có thể nó ở Scene khác (VD: Map) và sẽ được tìm lại khi bạn mở Inventory.");
        }
    }

    void Update()
    {
        // CHỈ xử lý UI cho người chơi local (tránh xung đột với các player khác qua mạng)
        if (inventory != null && inventory.IsSpawned && !inventory.IsOwner) return;

        bool inventoryPressed = false;
        
        if (_inventoryAction != null && _inventoryAction.WasPressedThisFrame())
        {
            inventoryPressed = true;
        }
        else if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame)
        {
            inventoryPressed = true;
        }

        if (inventoryPressed)
        {
            if (inventoryPanel == null)
            {
                GameObject[] allObjs = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var obj in allObjs)
                {
                    if (obj.name == "InventoryPanel" && obj.scene.IsValid() && obj.transform.Find("GridContainer") != null)
                    {
                        inventoryPanel = obj;
                        inventoryPanel.transform.localScale = new Vector3(0.64f, 0.64f, 1f);
                        break;
                    }
                }
            }

            // Rebuild gridSlots if they were lost during prefab instantiation
            if (inventoryPanel != null && (gridSlots == null || gridSlots.Count == 0 || gridSlots[0].bgObj == null))
            {
                gridSlots = new List<GridSlot>();
                Transform gridGo = inventoryPanel.transform.Find("GridContainer");
                if (gridGo != null)
                {
                    foreach (Transform slotBg in gridGo)
                    {
                        if (slotBg.name.StartsWith("Slot_"))
                        {
                            GridSlot slot = new GridSlot();
                            slot.bgObj = slotBg.gameObject;
                            Transform iconT = slotBg.Find("Icon");
                            if (iconT != null) slot.icon = iconT.GetComponent<Image>();
                            Transform amtT = slotBg.Find("Amount");
                            if (amtT != null) slot.amountText = amtT.GetComponent<TextMeshProUGUI>();
                            gridSlots.Add(slot);
                        }
                    }
                }
            }

            isVisible = !isVisible;
            Debug.Log($"[InventoryUI] Đã bấm I. Trạng thái bật: {isVisible}");
            
            if (inventoryPanel != null)
                inventoryPanel.SetActive(isVisible);
            else
                Debug.LogError("[InventoryUI] Lỗi: inventoryPanel bị NULL!");

            if (isVisible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                UpdateUI();
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        if (!isVisible) return;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame) TryEquipMask(false);
            else if (Keyboard.current.digit2Key.wasPressedThisFrame) TryEquipMask(true);
            else if (Keyboard.current.digit3Key.wasPressedThisFrame) TryUseAntidote();
            else if (Keyboard.current.digit4Key.wasPressedThisFrame) TryUseHealthPack();
            else if (Keyboard.current.digit5Key.wasPressedThisFrame) TryUseOxygenTank();
        }

        UpdateUI();
    }

    public void UpdateUI()
    {
        if (inventory == null) return;

        Dictionary<string, int> counts = new Dictionary<string, int>();
        if (inventory.circuits > 0) counts["circuit"] = inventory.circuits;
        if (inventory.metalPipes > 0) counts["metal_pipe"] = inventory.metalPipes;
        if (inventory.ironPlates > 0) counts["iron_plate"] = inventory.ironPlates;
        if (inventory.chemicals > 0) counts["chemical"] = inventory.chemicals;
        if (inventory.plasticPipes > 0) counts["plastic_pipe"] = inventory.plasticPipes;
        if (inventory.scrapBatteries > 0) counts["battery"] = inventory.scrapBatteries;

        // --- Bổ sung Tools & Consumables ---
        if (inventory.hasMachete) counts["machete"] = 1;
        if (inventory.hasAxe) counts["axe"] = 1;
        if (inventory.hasBat) counts["bat"] = 1;
        if (inventory.hasCrowbar) counts["crowbar"] = 1;
        if (inventory.hasShovel) counts["shovel"] = 1;
        if (inventory.hasFlashlight) counts["flashlight"] = 1;
        if (inventory.antidotes > 0) counts["antidote"] = inventory.antidotes;
        
        // --- Hiển thị Gas Mask vào Grid túi đồ ---
        if (inventory.basicGasMasks > 0) counts["basic_gasmask"] = inventory.basicGasMasks;
        if (inventory.advancedGasMasks > 0) counts["adv_gasmask"] = inventory.advancedGasMasks;
        // -----------------------------------

        // --- Bổ sung: TRỪ ĐI những đồ ĐÃ CẦM TRÊN HOTBAR để không bị phân thân ---
        var hotbarSys = GetComponent<HotbarSystem>();
        if (hotbarSys != null && hotbarSys.hotbarItems != null)
        {
            for (int i = 0; i < 3; i++)
            {
                string ht = hotbarSys.hotbarItems[i];
                if (!string.IsNullOrEmpty(ht) && counts.ContainsKey(ht))
                {
                    counts[ht]--;
                    if (counts[ht] <= 0) counts.Remove(ht);
                }
            }
        }
        // ------------------------------------------------------------------------

        // 1. Clear slots that have items we no longer have
        foreach (var slot in gridSlots)
        {
            if (!string.IsNullOrEmpty(slot.currentItemType) && !counts.ContainsKey(slot.currentItemType))
            {
                slot.currentItemType = "";
            }
        }

        // We need to distribute counts among slots that have this type.
        // First pass: assign up to 5 to existing slots of that type
        Dictionary<string, int> remainingCounts = new Dictionary<string, int>(counts);
        int[] slotAmounts = new int[gridSlots.Count];
        
        for (int i = 0; i < gridSlots.Count; i++)
        {
            if (i >= inventory.maxSlots)
            {
                gridSlots[i].bgObj.SetActive(false);
                gridSlots[i].currentItemType = "";
                continue;
            }
            gridSlots[i].bgObj.SetActive(true);
            
            string type = gridSlots[i].currentItemType;
            if (!string.IsNullOrEmpty(type) && remainingCounts.ContainsKey(type) && remainingCounts[type] > 0)
            {
                int toAdd = Mathf.Min(5, remainingCounts[type]);
                slotAmounts[i] = toAdd;
                remainingCounts[type] -= toAdd;
            }
            else
            {
                gridSlots[i].currentItemType = "";
            }
        }

        // Second pass: for any remaining counts, put them in empty slots
        foreach (var kvp in remainingCounts)
        {
            int amountLeft = kvp.Value;
            while (amountLeft > 0)
            {
                int toAdd = Mathf.Min(5, amountLeft);
                // Find empty slot
                bool placed = false;
                for (int i = 0; i < inventory.maxSlots && i < gridSlots.Count; i++)
                {
                    if (string.IsNullOrEmpty(gridSlots[i].currentItemType))
                    {
                        gridSlots[i].currentItemType = kvp.Key;
                        slotAmounts[i] = toAdd;
                        placed = true;
                        break;
                    }
                }
                if (!placed) break; // Should not happen if CanAddScrap logic is correct
                amountLeft -= toAdd;
            }
        }

        // 3. Refresh visual state
        for (int i = 0; i < gridSlots.Count; i++)
        {
            var slot = gridSlots[i];
            if (i >= inventory.maxSlots) continue;

            if (!string.IsNullOrEmpty(slot.currentItemType))
            {
                slot.icon.gameObject.SetActive(true);
                slot.icon.enabled = true;
                slot.icon.sprite = GetSpriteForType(slot.currentItemType);
                slot.icon.color = Color.white;
                slot.amountText.gameObject.SetActive(true);
                
                string t = slot.currentItemType;
                bool isTool = (t == "machete" || t == "axe" || t == "bat" || 
                               t == "crowbar" || t == "shovel" || t == "flashlight" || t == "antidote");
                
                if (isTool)
                {
                    slot.amountText.text = t.Substring(0, Mathf.Min(3, t.Length)).ToUpper() + "\nx" + slotAmounts[i];
                    slot.amountText.fontSize = 20; // Smaller to fit text
                }
                else
                {
                    slot.amountText.text = slotAmounts[i].ToString();
                    slot.amountText.fontSize = 24; // Default
                }
                
                // Add Drag & Drop Component
                if (slot.bgObj.GetComponent<UIDragItem>() == null)
                {
                    slot.bgObj.AddComponent<UIDragItem>();
                }
                slot.bgObj.GetComponent<UIDragItem>().itemType = slot.currentItemType;
            }
            else
            {
                slot.icon.enabled = false;
                slot.amountText.enabled = false;
                
                if (slot.bgObj.GetComponent<UIDragItem>() != null)
                {
                    slot.bgObj.GetComponent<UIDragItem>().itemType = "";
                }
            }
        }
    }

    [Header("Equipment Sprites")]
    public Sprite axeSprite;
    public Sprite macheteSprite;
    public Sprite antidoteSprite;
    public Sprite flashlightSprite;

    public Sprite GetSpriteForType(string type)
    {
        Sprite sp = null;
        switch (type)
        {
            case "circuit": sp = circuitSprite; break;
            case "metal_pipe": sp = pipeSprite; break;
            case "iron_plate": sp = ironPlateSprite; break;
            case "chemical": sp = chemicalSprite; break;
            case "plastic_pipe": sp = plasticSprite; break;
            case "battery": sp = batterySprite; break;
            
            // THÊM TRANG BỊ
            case "axe": sp = axeSprite; break;
            case "machete": sp = macheteSprite; break;
            case "antidote": sp = antidoteSprite; break;
            case "flashlight": sp = flashlightSprite; break;
        }

        // --- HỆ THỐNG LOAD ẢNH TUYỆT ĐỐI (TRÁNH LỖI TRẮNG XÓA) ---
        if (sp == null)
        {
            string iconName = "";
            switch (type)
            {
                case "circuit": iconName = "Scrap_electrical-circuit_Icon"; break;
                case "metal_pipe": iconName = "Scrap_MetalPipe_Icon"; break;
                case "iron_plate": iconName = "Scrap_IronPlate_Icon"; break; // Chữ P hoa đúng file trên ổ cứng
                case "chemical": iconName = "Scrap_Chemical_Icon"; break;
                case "plastic_pipe": iconName = "Scrap_PlasticPipe_Icon"; break;
                case "battery": iconName = "Scrap_Battery_Icon"; break;
                
                // THÊM TRANG BỊ VÀO RESOURCES LOAD FALLBACK
                case "axe": iconName = "Axe_Icon"; break;
                case "machete": iconName = "Machete_Icon"; break;
                case "antidote": iconName = "Antidote_Icon"; break;
                case "flashlight": iconName = "Flashlight_Icon"; break;
                case "basic_gasmask": iconName = "BasicGasMask_Icon"; break;
                case "adv_gasmask": iconName = "AdvancedGasMask_Icon"; break;
            }
            if (!string.IsNullOrEmpty(iconName))
            {
                // Load thẳng từ thư mục Resources/Icons/ của Unity (cực kỳ ổn định)
                sp = Resources.Load<Sprite>("Icons/" + iconName);
            }
        }

        return sp;
    }

    public void SwapSlots(int index1, int index2)
    {
        if (index1 < 0 || index1 >= gridSlots.Count || index2 < 0 || index2 >= gridSlots.Count) return;
        
        string temp = gridSlots[index1].currentItemType;
        gridSlots[index1].currentItemType = gridSlots[index2].currentItemType;
        gridSlots[index2].currentItemType = temp;
        
        UpdateUI();
    }

    private void TryEquipMask(bool advanced)
    {
        if (survival == null || inventory == null) return;

        // Prevent spamming the same mask type
        if (advanced && survival.netEquippedMask.Value == 2) return;
        if (!advanced && survival.netEquippedMask.Value == 1) return;

        if (advanced) 
        { 
            if (inventory.advancedGasMasks > 0) 
            { 
                if (survival.netEquippedMask.Value == 1) inventory.basicGasMasks++;
                inventory.advancedGasMasks--; 
                survival.netEquippedMask.Value = 2; 
            } 
        }
        else 
        { 
            if (inventory.basicGasMasks > 0) 
            { 
                if (survival.netEquippedMask.Value == 2) inventory.advancedGasMasks++;
                inventory.basicGasMasks--; 
                survival.netEquippedMask.Value = 1; 
            } 
        }
    }

    private void TryUseAntidote()
    {
        if (inventory == null || inventory.antidotes <= 0) return;
        
        RandomEventManager rem = FindAnyObjectByType<RandomEventManager>();
        if (rem != null && rem.infectedClientId == inventory.OwnerClientId)
        {
            inventory.antidotes--;
            rem.CureInfectionServerRpc(inventory.OwnerClientId);
            Debug.Log("[InventoryUI] Used Antidote!");
        }
    }

    private void TryUseHealthPack()
    {
        if (inventory == null || inventory.healthPacks <= 0 || survival == null) return;
        inventory.healthPacks--;
        survival.Heal(50f);
        Debug.Log("[InventoryUI] Used Health Pack!");
    }

    private void TryUseOxygenTank()
    {
        if (inventory == null || inventory.oxygenTanks <= 0 || survival == null) return;
        inventory.oxygenTanks--;
        survival.currentOxygen = survival.maxOxygen;
        Debug.Log("[InventoryUI] Used Oxygen Tank!");
    }
}
