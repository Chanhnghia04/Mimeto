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

        // Current item types present in PlayerInventory
        Dictionary<string, int> counts = new Dictionary<string, int>();
        if (inventory.circuits > 0) counts["circuit"] = inventory.circuits;
        if (inventory.metalPipes > 0) counts["metal_pipe"] = inventory.metalPipes;
        if (inventory.ironPlates > 0) counts["iron_plate"] = inventory.ironPlates;
        if (inventory.chemicals > 0) counts["chemical"] = inventory.chemicals;
        if (inventory.plasticPipes > 0) counts["plastic_pipe"] = inventory.plasticPipes;
        if (inventory.scrapBatteries > 0) counts["battery"] = inventory.scrapBatteries;

        // 1. Remove items from slots if they are no longer in inventory
        foreach (var slot in gridSlots)
        {
            if (!string.IsNullOrEmpty(slot.currentItemType) && !counts.ContainsKey(slot.currentItemType))
            {
                slot.currentItemType = "";
            }
        }

        // 2. Add new items to first available slots
        foreach (var kvp in counts)
        {
            bool alreadyInSlot = false;
            foreach (var slot in gridSlots)
            {
                if (slot.currentItemType == kvp.Key)
                {
                    alreadyInSlot = true;
                    break;
                }
            }

            if (!alreadyInSlot)
            {
                // Find empty slot
                foreach (var slot in gridSlots)
                {
                    if (string.IsNullOrEmpty(slot.currentItemType))
                    {
                        slot.currentItemType = kvp.Key;
                        break;
                    }
                }
            }
        }

        // 3. Refresh visual state
        for (int i = 0; i < gridSlots.Count; i++)
        {
            var slot = gridSlots[i];
            if (slot.bgObj != null) slot.bgObj.SetActive(true);

            if (!string.IsNullOrEmpty(slot.currentItemType))
            {
                slot.icon.gameObject.SetActive(true);
                slot.icon.sprite = GetSpriteForType(slot.currentItemType);
                slot.icon.color = Color.white;
                
                slot.amountText.gameObject.SetActive(true);
                slot.amountText.text = counts[slot.currentItemType].ToString();
            }
            else
            {
                slot.icon.gameObject.SetActive(false);
                slot.amountText.gameObject.SetActive(false);
            }
        }
    }

    private Sprite GetSpriteForType(string type)
    {
        switch (type)
        {
            case "circuit": return circuitSprite;
            case "metal_pipe": return pipeSprite;
            case "iron_plate": return ironPlateSprite;
            case "chemical": return chemicalSprite;
            case "plastic_pipe": return plasticSprite;
            case "battery": return batterySprite;
        }
        return null;
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
        if (advanced && survival.activeMaskType == GasMaskType.Advanced) return;
        if (!advanced && survival.activeMaskType == GasMaskType.Basic) return;

        if (advanced) 
        { 
            if (inventory.advancedGasMasks > 0) 
            { 
                if (survival.activeMaskType == GasMaskType.Basic) inventory.basicGasMasks++;
                inventory.advancedGasMasks--; 
                survival.EquipMask(GasMaskType.Advanced); 
            } 
        }
        else 
        { 
            if (inventory.basicGasMasks > 0) 
            { 
                if (survival.activeMaskType == GasMaskType.Advanced) inventory.advancedGasMasks++;
                inventory.basicGasMasks--; 
                survival.EquipMask(GasMaskType.Basic); 
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
