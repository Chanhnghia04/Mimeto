using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

public class PlayerInventory : NetworkBehaviour
{
    public NetworkVariable<int> MatchSeed = new NetworkVariable<int>(0);
    public static int GlobalMatchSeed = 0;

    // --- Events for Server-Authoritative Minigames & Shop ---
    public static event System.Action<ulong, int, int, int> OnSlotSpinResult;
    public static event System.Action<ulong, int, int, int, int> OnDiceRollResult;
    public static event System.Action<int[], int[]> OnBlackjackStartResult;
    public static event System.Action<int> OnBlackjackHitResult;
    public static event System.Action<int[]> OnBlackjackStandResult;
    public static event System.Action<ulong, string> OnShopBuyResult;

    // --- Blackjack Server State (per player) ---
    private List<int> _bjDeck = new List<int>();
    private List<int> _bjPlayerHand = new List<int>();
    private List<int> _bjDealerHand = new List<int>();
    private int _bjBetAmount = 0;
    private bool _bjIsActive = false;

    public int circuits = 0;
    public int metalPipes = 0;
    public int ironPlates = 0;
    public int chemicals = 0;
    public int plasticPipes = 0;
    public int scrapBatteries = 0;
    
    public int maxSlots = 5;

    [Header("Currency")]
    public int credits = 0;

    public int basicGasMasks = 0;
    public int advancedGasMasks = 0;
    public bool hasFlashlight = false;
    public bool hasUVFlashlight = false;
    public bool hasCrowbar = false;
    public bool hasShovel = false;
    public bool hasMachete = false;
    public bool hasAxe = false;
    public bool hasBat = false;

    [Header("Shop Consumables")]
    public int healthPacks = 0;
    public int oxygenTanks = 0;
    public int antidotes = 0;

    [Header("Escape & Loot")]
    public bool hasEscapeKey = false;
    public int rareLootCount = 0;
    public bool hasEscaped = false;

    void Start()
    {
        // Moved to OnNetworkSpawn to check IsOwner
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsOwner)
        {
            if (GlobalPlayerData.hasSavedData)
            {
                circuits = GlobalPlayerData.circuits;
                metalPipes = GlobalPlayerData.metalPipes;
                ironPlates = GlobalPlayerData.ironPlates;
                chemicals = GlobalPlayerData.chemicals;
                plasticPipes = GlobalPlayerData.plasticPipes;
                scrapBatteries = GlobalPlayerData.scrapBatteries;
                maxSlots = GlobalPlayerData.maxSlots;
                credits = GlobalPlayerData.credits;
                basicGasMasks = GlobalPlayerData.basicGasMasks;
                advancedGasMasks = GlobalPlayerData.advancedGasMasks;
                hasFlashlight = GlobalPlayerData.hasFlashlight;
                hasUVFlashlight = GlobalPlayerData.hasUVFlashlight;
                hasCrowbar = GlobalPlayerData.hasCrowbar;
                hasShovel = GlobalPlayerData.hasShovel;
                hasMachete = GlobalPlayerData.hasMachete;
                hasAxe = GlobalPlayerData.hasAxe;
                hasBat = GlobalPlayerData.hasBat;
                rareLootCount = GlobalPlayerData.rareLootCount;
                healthPacks = GlobalPlayerData.healthPacks;
                oxygenTanks = GlobalPlayerData.oxygenTanks;
                antidotes = GlobalPlayerData.antidotes;
            }

            if (!IsServer)
            {
                InitSaveDataServerRpc(credits);
            }
        }

        // --- ĐỒNG BỘ TIỀN TỆ VÀ SỰ KIỆN SHOP TỪ HOST CHO CLIENT MỚI VÀO ---
        if (IsServer && !IsOwner)
        {
            if (Unity.Netcode.NetworkManager.Singleton.ConnectedClients.TryGetValue(Unity.Netcode.NetworkManager.ServerClientId, out var hostClient))
            {
                if (hostClient.PlayerObject != null && hostClient.PlayerObject.TryGetComponent(out PlayerInventory hostInv))
                {
                    this.credits = hostInv.credits;
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
                    };
                    SyncInitialCreditsClientRpc(hostInv.credits, clientRpcParams);
                    SyncShopEventClientRpc((int)ShopData.CurrentEvent, clientRpcParams);
                }
            }
        }

        if (IsServer && IsOwner)
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName == "WaitingRoom")
            {
                ShopData.RollMarketEvent();
                SyncShopEventClientRpc((int)ShopData.CurrentEvent);
            }
            else
            {
                MatchSeed.Value = (int)(System.DateTime.Now.Ticks % 100000000);
            }
        }
        if (MatchSeed.Value != 0)
        {
            GlobalMatchSeed = MatchSeed.Value;
        }
    }

    [ServerRpc]
    public void InitSaveDataServerRpc(int initialCredits)
    {
        // Chức năng cũ, Client nộp tiền save của nó lên (dù hiện tại ta ưu tiên dùng tiền của Host)
        // credits = initialCredits;
    }

    [ClientRpc]
    public void SyncInitialCreditsClientRpc(int hostCredits, ClientRpcParams rpcParams = default)
    {
        credits = hostCredits;
        GlobalPlayerData.credits = hostCredits;
        Debug.Log($"[Tiền Tệ Chung] Đã đồng bộ số tiền từ Host: {credits}");
    }

    [ClientRpc]
    public void SyncShopEventClientRpc(int eventId, ClientRpcParams rpcParams = default)
    {
        ShopData.ApplyMarketEvent((MarketEvent)eventId);
        Debug.Log($"[Shop] Đã đồng bộ sự kiện Shop từ Host: {ShopData.CurrentEvent}");
    }

    void Update()
    {
        if (MatchSeed.Value != 0 && GlobalMatchSeed != MatchSeed.Value)
        {
            GlobalMatchSeed = MatchSeed.Value;
            Debug.Log($"[MapSync] GlobalMatchSeed updated to: {GlobalMatchSeed}");
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        GlobalMatchSeed = 0;
        
        if (IsOwner)
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName == "Map" || sceneName == "PollutedZone")
            {
                if (hasEscaped)
                {
                    SaveData(true);
                    Debug.Log("[PlayerInventory] Đã thoát hiểm thành công -> Lưu dữ liệu.");
                }
                else
                {
                    Debug.Log("[PlayerInventory] Thoát giữa chừng khi chưa hoàn thành nhiệm vụ -> MẤT SẠCH ĐỒ (Phạt).");
                    ClearInventoryOnDeath(); // Hàm này giờ đã gọi SaveData(true) nên đồ sẽ bị xóa sạch khỏi ổ cứng
                }
            }
            else
            {
                // Đang ở Waiting hoặc sảnh thì cứ lưu bình thường
                SaveData(true);
            }
        }
    }

    private void SaveData(bool force = false)
    {
        if (!force)
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if ((sceneName == "Map" || sceneName == "PollutedZone") && !hasEscaped)
            {
                return; // KHÔNG tự động lưu khi đang nhặt đồ trong map (trừ khi đã escape)
            }
        }

        GlobalPlayerData.circuits = circuits;
        GlobalPlayerData.metalPipes = metalPipes;
        GlobalPlayerData.ironPlates = ironPlates;
        GlobalPlayerData.chemicals = chemicals;
        GlobalPlayerData.plasticPipes = plasticPipes;
        GlobalPlayerData.scrapBatteries = scrapBatteries;
        GlobalPlayerData.maxSlots = maxSlots;
        GlobalPlayerData.credits = credits;
        GlobalPlayerData.basicGasMasks = basicGasMasks;
        GlobalPlayerData.advancedGasMasks = advancedGasMasks;
        GlobalPlayerData.hasFlashlight = hasFlashlight;
        GlobalPlayerData.hasUVFlashlight = hasUVFlashlight;
        GlobalPlayerData.hasCrowbar = hasCrowbar;
        GlobalPlayerData.hasShovel = hasShovel;
        GlobalPlayerData.hasMachete = hasMachete;
        GlobalPlayerData.hasAxe = hasAxe;
        GlobalPlayerData.hasBat = hasBat;
        GlobalPlayerData.rareLootCount = rareLootCount;
        GlobalPlayerData.healthPacks = healthPacks;
        GlobalPlayerData.oxygenTanks = oxygenTanks;
        GlobalPlayerData.antidotes = antidotes;
        GlobalPlayerData.hasSavedData = true;

        GlobalPlayerData.Save();
    }

    public void RegenerateSeed()
    {
        if (!IsServer) return;
        MatchSeed.Value = (int)(System.DateTime.Now.Ticks % 100000000);
        GlobalMatchSeed = MatchSeed.Value;
        Debug.Log($"[PlayerInventory] Seed mới cho màn chơi: {GlobalMatchSeed}");
    }

    public override void OnDestroy()
    {
        base.OnDestroy(); // NetworkBehaviour requires base.OnDestroy()
        GlobalMatchSeed = 0;
    }

    public int CalculateUsedSlots()
    {
        int toolsCount = (hasMachete ? 1 : 0) + (hasAxe ? 1 : 0) + (hasBat ? 1 : 0) + 
                         (hasCrowbar ? 1 : 0) + (hasShovel ? 1 : 0) + (hasFlashlight ? 1 : 0) + antidotes;
        
        return Mathf.CeilToInt(circuits / 5f) + 
               Mathf.CeilToInt(metalPipes / 5f) + 
               Mathf.CeilToInt(ironPlates / 5f) + 
               Mathf.CeilToInt(chemicals / 5f) + 
               Mathf.CeilToInt(plasticPipes / 5f) + 
               Mathf.CeilToInt(scrapBatteries / 5f) + toolsCount;
    }

    public bool CanAddScrap(string type, int amount)
    {
        int tempC = circuits, tempM = metalPipes, tempI = ironPlates;
        int tempCh = chemicals, tempP = plasticPipes, tempB = scrapBatteries;
        int tMachete = hasMachete ? 1 : 0;
        int tAxe = hasAxe ? 1 : 0;
        int tBat = hasBat ? 1 : 0;
        int tCrowbar = hasCrowbar ? 1 : 0;
        int tShovel = hasShovel ? 1 : 0;
        int tFlashlight = hasFlashlight ? 1 : 0;
        int tempAntidotes = antidotes;
        
        switch (type.ToLower())
        {
            case "circuit": tempC += amount; break;
            case "metal_pipe":
            case "metal pipe": tempM += amount; break;
            case "iron_plate":
            case "iron plate": tempI += amount; break;
            case "chemical": tempCh += amount; break;
            case "plastic_pipe":
            case "plastic pipe":
            case "pipe":
            case "plastic":
            case "rubber": tempP += amount; break;
            case "battery": tempB += amount; break;
            case "machete": if (tMachete > 0) return false; tMachete = 1; break;
            case "axe": if (tAxe > 0) return false; tAxe = 1; break;
            case "bat": if (tBat > 0) return false; tBat = 1; break;
            case "crowbar": if (tCrowbar > 0) return false; tCrowbar = 1; break;
            case "shovel": if (tShovel > 0) return false; tShovel = 1; break;
            case "flashlight": if (tFlashlight > 0) return false; tFlashlight = 1; break;
            case "antidote": tempAntidotes += amount; break;
            case "basic_gasmask":
            case "adv_gasmask":
            case "medkit":
            case "oxygen":
                // Consumables usually stack or take a slot, assuming they take a slot for capacity check here
                // We'll just let them pass if used <= maxSlots, they don't have separate hardcaps here
                break;
            default: break; 
        }

        int used = Mathf.CeilToInt(tempC / 5f) + Mathf.CeilToInt(tempM / 5f) +
                   Mathf.CeilToInt(tempI / 5f) + Mathf.CeilToInt(tempCh / 5f) +
                   Mathf.CeilToInt(tempP / 5f) + Mathf.CeilToInt(tempB / 5f) + 
                   tMachete + tAxe + tBat + tCrowbar + tShovel + tFlashlight + tempAntidotes;
                   
        // Note: For simplicity, Gas Masks, Medkits, and Oxygen were missing from used slots calculation in original code.
        // We will keep it as original to not break inventory limit logic unexpectedly.

        return used <= maxSlots;
    }

    public void AddScrap(string type, int amount)
    {
        if (IsSpawned && !IsOwner && !IsServer) return; // Chỉ cập nhật túi đồ cho chủ sở hữu hoặc khi test offline
        
        switch (type.ToLower())
        {
            case "circuit": circuits += amount; break;
            case "metal_pipe": case "metal pipe": metalPipes += amount; break;
            case "iron_plate": case "iron plate": ironPlates += amount; break;
            case "chemical": chemicals += amount; break;
            case "pipe": case "plastic": case "plastic_pipe": case "plastic pipe": case "rubber": plasticPipes += amount; break;
            case "battery": scrapBatteries += amount; break;
            case "key": case "escape_key": hasEscapeKey = true; Debug.Log("<color=yellow>Obtained Escape Key!</color>"); break;
            case "rare_loot": case "relic": rareLootCount += amount; Debug.Log($"<color=cyan>Obtained Rare Loot! Total: {rareLootCount}</color>"); break;
            case "machete": hasMachete = true; break;
            case "axe": hasAxe = true; break;
            case "bat": hasBat = true; break;
            case "crowbar": hasCrowbar = true; break;
            case "shovel": hasShovel = true; break;
            case "flashlight": hasFlashlight = true; break;
            case "antidote": antidotes += amount; break;
            case "basic_gasmask": basicGasMasks += amount; break;
            case "adv_gasmask": advancedGasMasks += amount; break;
            case "medkit": healthPacks += amount; break;
            case "oxygen": oxygenTanks += amount; break;
        }

        SaveData();
        

        if (ItemNotificationManager.Instance != null)
        {
            ItemNotificationManager.Instance.ShowNotification(type, amount);
        }

        Debug.Log($"Added {amount} {type}. Inventory: C={circuits}, MP={metalPipes}, IP={ironPlates}, Ch={chemicals}, Pl={plasticPipes}, Bat={scrapBatteries}");
    }

    public bool HasItem(string type)
    {
        switch (type.ToLower())
        {
            case "circuit": return circuits > 0;
            case "metal_pipe": case "metal pipe": return metalPipes > 0;
            case "iron_plate": case "iron plate": return ironPlates > 0;
            case "chemical": return chemicals > 0;
            case "pipe": case "plastic": case "plastic_pipe": case "plastic pipe": case "rubber": return plasticPipes > 0;
            case "battery": return scrapBatteries > 0;
            case "machete": return hasMachete;
            case "axe": return hasAxe;
            case "bat": return hasBat;
            case "crowbar": return hasCrowbar;
            case "shovel": return hasShovel;
            case "flashlight": return hasFlashlight;
            case "antidote": return antidotes > 0;
            case "basic_gasmask": return basicGasMasks > 0;
            case "adv_gasmask": return advancedGasMasks > 0;
            case "medkit": return healthPacks > 0;
            case "oxygen": return oxygenTanks > 0;
            default: return false;
        }
    }

    public void RemoveItem(string type, int amount = 1)
    {
        if (IsSpawned && !IsOwner && !IsServer) return;
        
        switch (type.ToLower())
        {
            case "circuit": circuits = Mathf.Max(0, circuits - amount); break;
            case "metal_pipe": case "metal pipe": metalPipes = Mathf.Max(0, metalPipes - amount); break;
            case "iron_plate": case "iron plate": ironPlates = Mathf.Max(0, ironPlates - amount); break;
            case "chemical": chemicals = Mathf.Max(0, chemicals - amount); break;
            case "pipe": case "plastic": case "plastic_pipe": case "plastic pipe": case "rubber": plasticPipes = Mathf.Max(0, plasticPipes - amount); break;
            case "battery": scrapBatteries = Mathf.Max(0, scrapBatteries - amount); break;
            case "machete": hasMachete = false; break;
            case "axe": hasAxe = false; break;
            case "bat": hasBat = false; break;
            case "crowbar": hasCrowbar = false; break;
            case "shovel": hasShovel = false; break;
            case "flashlight": hasFlashlight = false; break;
            case "antidote": antidotes = Mathf.Max(0, antidotes - amount); break;
            case "basic_gasmask": basicGasMasks = Mathf.Max(0, basicGasMasks - amount); break;
            case "adv_gasmask": advancedGasMasks = Mathf.Max(0, advancedGasMasks - amount); break;
            case "medkit": healthPacks = Mathf.Max(0, healthPacks - amount); break;
            case "oxygen": oxygenTanks = Mathf.Max(0, oxygenTanks - amount); break;
        }
        SaveData();
    }

    public void ForceUnequipIfDropped(string itemType)
    {
        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null) pc.UnequipIfEquipped(itemType.ToLower());
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestDropItemServerRpc(string itemType, ServerRpcParams rpcParams = default)
    {
        var clientId = rpcParams.Receive.SenderClientId;
        var clientInv = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId)?.GetComponent<PlayerInventory>();
        
        if (clientInv == null || !clientInv.HasItem(itemType)) return;

        clientInv.RemoveItem(itemType, 1);
        clientInv.ForceUnequipIfDroppedClientRpc(itemType, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } } });

        Vector3 dropPos = clientInv.transform.position + clientInv.transform.forward * 1.5f + Vector3.up * 1f;
        SpawnDroppedItemClientRpc(itemType, dropPos);
        
        RemoveItemClientRpc(itemType, 1, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } } });
    }

    [ClientRpc]
    public void ForceUnequipIfDroppedClientRpc(string itemType, ClientRpcParams rpcParams = default)
    {
        if (IsServer) return;
        ForceUnequipIfDropped(itemType);
    }

    [ClientRpc]
    public void RemoveItemClientRpc(string itemType, int amount, ClientRpcParams rpcParams = default)
    {
        if (IsServer) return;
        RemoveItem(itemType, amount);
        
        InventoryUI ui = GetComponent<InventoryUI>();
        if (ui != null) ui.UpdateUI();
    }

    [ClientRpc]
    public void SpawnDroppedItemClientRpc(string itemType, Vector3 pos)
    {
        GameObject prefabToSpawn = null;
        EquipmentManager eq = GetComponent<EquipmentManager>();
        
        if (eq != null)
        {
            switch (itemType.ToLower())
            {
                case "axe": prefabToSpawn = eq.axePrefab; break;
                case "machete": prefabToSpawn = eq.machetePrefab; break;
                case "bat": prefabToSpawn = eq.batPrefab; break;
                case "crowbar": prefabToSpawn = eq.crowbarPrefab; break;
                case "shovel": prefabToSpawn = eq.shovelPrefab; break;
                case "flashlight": prefabToSpawn = eq.flashlightPrefab; break;
                case "antidote": prefabToSpawn = eq.antidotePrefab; break;
                case "basic_gasmask": case "adv_gasmask": prefabToSpawn = eq.gasMaskPrefab; break;
            }
        }

        if (prefabToSpawn == null)
            prefabToSpawn = Resources.Load<GameObject>("DroppedItems/" + itemType);

        GameObject box;
        if (prefabToSpawn != null)
        {
            box = Instantiate(prefabToSpawn, pos, Quaternion.identity);
            
            // Fix scale and visual issues for some prefabs
            box.transform.localScale = Vector3.one;
            box.layer = LayerMask.NameToLayer("Default");
        }
        else
        {
            box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.position = pos;
            box.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        }
        
        // Khôi phục scale của mô hình con bên trong GasMask. User yêu cầu to gấp đôi
        Transform maskChild = box.transform.Find("default");
        if (maskChild != null && itemType.Contains("gasmask"))
        {
            maskChild.localScale = new Vector3(0.16f, 0.15f, 0.17f);
            
            // Hiện lại Mesh vì lúc đeo lên có thể nó bị tắt
            foreach (var r in box.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
            }
        }

        if (box.GetComponent<Rigidbody>() == null) 
        {
            var rb = box.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous; // Ngăn rơi xuyên sàn
        }

        ScrapItem scrap = box.GetComponent<ScrapItem>();
        if (scrap == null) scrap = box.AddComponent<ScrapItem>();
        
        // Buộc ScrapItem tính toán lại collider ngay lập tức (vì AddComponent có thể chạy Awake muộn)
        scrap.RebuildCollider();
        
        BoxCollider bc = box.GetComponent<BoxCollider>();
        if (bc == null || bc.size == Vector3.zero || bc.size.magnitude < 0.01f)
        {
            if (bc == null) bc = box.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.5f, 0.5f, 0.5f);
        }

        scrap.scrapType = itemType;
        scrap.amount = 1;
        scrap.rootObject = box;
        scrap.interactHint = "Press [E] to pick up";

        // Giữ tên sạch để dễ debug
        box.name = "Dropped_" + itemType;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestPickupItemServerRpc(Vector3 pos, string itemType, int amount, ServerRpcParams rpcParams = default)
    {
        var clientId = rpcParams.Receive.SenderClientId;
        // Check capacity using Server's knowledge of the player's inventory
        var clientInv = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId)?.GetComponent<PlayerInventory>();
        if (clientInv != null && !clientInv.CanAddScrap(itemType, amount)) return;

        Collider[] colls = Physics.OverlapSphere(pos, 2.0f);
        foreach (var col in colls)
        {
            ScrapItem scrap = col.GetComponent<ScrapItem>();
            if (scrap != null && scrap.scrapType == itemType && scrap.gameObject.activeInHierarchy)
            {
                // Tắt ngay lập tức để chặn các RPC nhặt đồ khác trong cùng một frame
                
                scrap.gameObject.SetActive(false);
                
                NetworkObject no = scrap.GetComponent<NetworkObject>();
                if (no != null && no.IsSpawned) no.Despawn();
                else Destroy(scrap.gameObject);

                // Keep server copy in sync
                if (clientInv != null) clientInv.AddScrap(itemType, amount);

                AddScrapClientRpc(itemType, amount, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } } });
                SyncDestroyItemClientRpc(pos, itemType);
                Destroy(scrap.rootObject != null ? scrap.rootObject : scrap.gameObject);
                break;
            }
        }
    }

    [ClientRpc]
    public void AddScrapClientRpc(string itemType, int amount, ClientRpcParams clientRpcParams = default)
    {
        if (IsServer) return;
        AddScrap(itemType, amount);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SyncDestroyItemServerRpc(Vector3 pos, string itemType)
    {
        SyncDestroyItemClientRpc(pos, itemType);
    }

    [ClientRpc]
    public void SyncDestroyItemClientRpc(Vector3 pos, string itemType)
    {

        // Tăng bán kính tìm kiếm lên 2.0f để bù trừ sai lệch vị trí qua mạng
        Collider[] colls = Physics.OverlapSphere(pos, 2.0f);
        foreach (var col in colls)
        {
            ScrapItem scrap = col.GetComponent<ScrapItem>();
            // Phải check thêm scrapType để tránh xóa nhầm đồ nằm gần nhau
            if (scrap != null && scrap.scrapType == itemType)
            {
                Destroy(scrap.rootObject != null ? scrap.rootObject : scrap.gameObject);
                break;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestLootChestItemServerRpc(Vector3 chestPos, string itemType, ServerRpcParams rpcParams = default)
    {
        Collider[] colls = Physics.OverlapSphere(chestPos, 1.0f);
        foreach (var col in colls)
        {
            Chest chest = col.GetComponent<Chest>();
            if (chest != null)
            {
                var entry = chest.items.Find(e => e.itemType == itemType);
                if (entry != null)
                {
                    int amount = entry.amount;
                    var clientId = rpcParams.Receive.SenderClientId;
                    
                    var clientInv = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId)?.GetComponent<PlayerInventory>();
                    if (clientInv != null && !clientInv.CanAddScrap(itemType, amount)) return;

                    // Keep server copy in sync
                    if (clientInv != null) clientInv.AddScrap(itemType, amount);

                    AddScrapClientRpc(itemType, amount, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } } });
                    
                    chest.RemoveItem(entry);
                    ChestUI ui = Object.FindAnyObjectByType<ChestUI>();
                    if (ui != null) ui.RefreshIfOpen(chest);
                    
                    SyncLootChestItemClientRpc(chestPos, itemType);
                }
                break;
            }
        }
    }

    [ClientRpc]
    public void SyncLootChestItemClientRpc(Vector3 chestPos, string itemType)
    {
        if (IsServer) return; // Server already removed it

        Collider[] colls = Physics.OverlapSphere(chestPos, 1.0f);
        foreach (var col in colls)
        {
            Chest chest = col.GetComponent<Chest>();
            if (chest != null)
            {
                var entry = chest.items.Find(e => e.itemType == itemType);
                if (entry != null)
                {
                    chest.RemoveItem(entry);
                    ChestUI ui = Object.FindAnyObjectByType<ChestUI>();
                    if (ui != null) ui.RefreshIfOpen(chest);
                }
                break;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SyncEscapeEventServerRpc(int eventId)
    {
        if (EscapeManager.Instance == null) return;
        
        if (eventId == 0) // Unlock Escape Door
        {
            EscapeManager.Instance.UnlockEscape();
        }
        else if (eventId == 1) // Beacon Build
        {
            EscapeManager.Instance.isBeaconBuiltNet.Value = true;
        }
        else if (eventId == 2) // Reactor Meltdown
        {
            EscapeManager.Instance.isReactorShutdownNet.Value = true;
        }
        else if (eventId == 3) // Extraction System Assemble step
        {
            EscapeManager.Instance.assembleStepsNet.Value++;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SyncAssemblyPartServerRpc(string partName, Vector3 pos)
    {
        Collider[] colls = Physics.OverlapSphere(pos, 0.5f);
        foreach (var col in colls)
        {
            EscapePart part = col.GetComponent<EscapePart>();
            if (part != null && part.partName == partName)
            {
                SyncAssemblyPartClientRpc(partName, pos);
                part.parentAssembly?.OnPartCollected(part.partName);
                Destroy(part.gameObject);
                break;
            }
        }
    }

    [ClientRpc]
    public void SyncAssemblyPartClientRpc(string partName, Vector3 pos)
    {
        if (IsServer) return; // Server đã xóa
        
        Collider[] colls = Physics.OverlapSphere(pos, 0.5f);
        foreach (var col in colls)
        {
            EscapePart part = col.GetComponent<EscapePart>();
            if (part != null && part.partName == partName)
            {
                part.parentAssembly?.OnPartCollected(part.partName);
                Destroy(part.gameObject);
                break;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SyncCipherNoteServerRpc(int noteIndex, string digits, Vector3 pos)
    {
        Collider[] colls = Physics.OverlapSphere(pos, 0.5f);
        foreach (var col in colls)
        {
            CipherNote note = col.GetComponent<CipherNote>();
            if (note != null && note.noteIndex == noteIndex)
            {
                SyncCipherNoteClientRpc(noteIndex, digits, pos);
                note.parentCipher?.OnNoteFound(note.noteIndex, note.digits);
                Destroy(note.gameObject);
                break;
            }
        }
    }

    [ClientRpc]
    public void SyncCipherNoteClientRpc(int noteIndex, string digits, Vector3 pos)
    {
        if (IsServer) return; // Server đã xử lý
        
        Collider[] colls = Physics.OverlapSphere(pos, 0.5f);
        foreach (var col in colls)
        {
            CipherNote note = col.GetComponent<CipherNote>();
            if (note != null && note.noteIndex == noteIndex)
            {
                note.parentCipher?.OnNoteFound(note.noteIndex, note.digits);
                Destroy(note.gameObject);
                break;
            }
        }
    }

    public bool HasResources(int c, int mp, int ch, int pl = 0, int bgm = 0, int bat = 0, int ip = 0)
    {
        return circuits >= c && metalPipes >= mp && chemicals >= ch && plasticPipes >= pl && basicGasMasks >= bgm && scrapBatteries >= bat && ironPlates >= ip;
    }

    public void ConsumeResources(int c, int mp, int ch, int pl = 0, int bgm = 0, int bat = 0, int ip = 0)
    {
        circuits -= c;
        metalPipes -= mp;
        chemicals -= ch;
        plasticPipes -= pl;
        basicGasMasks -= bgm;
        scrapBatteries -= bat;
        ironPlates -= ip;
    }

    public void AddGasMask(bool advanced)
    {
        if (advanced) advancedGasMasks++;
        else basicGasMasks++;
        Debug.Log($"Added {(advanced ? "Advanced" : "Basic")} Gas Mask. Total BGM={basicGasMasks}, AGM={advancedGasMasks}");
    }

    public void SellAllScrap()
    {
        if (IsSpawned && !IsOwner && !IsServer) return;
        
        if (IsSpawned)
        {
            RequestSellScrapServerRpc(circuits, metalPipes, ironPlates, chemicals, plasticPipes, scrapBatteries);
        }
        else
        {
            int totalValue = 0;
            totalValue += circuits * ShopData.CircuitSellPrice;
            totalValue += metalPipes * ShopData.MetalPipeSellPrice;
            totalValue += ironPlates * ShopData.IronPlateSellPrice;
            totalValue += chemicals * ShopData.ChemicalSellPrice;
            totalValue += plasticPipes * ShopData.PlasticPipeSellPrice;
            totalValue += scrapBatteries * ShopData.BatterySellPrice;
            credits += totalValue;
            GlobalPlayerData.credits = credits;
            Debug.Log($"[Store] Sold all scrap for {totalValue} Energy Cells!");
        }
        
        circuits = 0;
        metalPipes = 0;
        ironPlates = 0;
        chemicals = 0;
        plasticPipes = 0;
        scrapBatteries = 0;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestSellScrapServerRpc(int c, int mp, int ip, int ch, int pl, int bat, ServerRpcParams rpcParams = default)
    {
        var clientId = rpcParams.Receive.SenderClientId;
        var clientInv = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId)?.GetComponent<PlayerInventory>();
        if (clientInv != null)
        {
            c = Mathf.Clamp(c, 0, clientInv.circuits);
            mp = Mathf.Clamp(mp, 0, clientInv.metalPipes);
            ip = Mathf.Clamp(ip, 0, clientInv.ironPlates);
            ch = Mathf.Clamp(ch, 0, clientInv.chemicals);
            pl = Mathf.Clamp(pl, 0, clientInv.plasticPipes);
            bat = Mathf.Clamp(bat, 0, clientInv.scrapBatteries);

            int totalValue = 0;
            totalValue += c * ShopData.CircuitSellPrice;
            totalValue += mp * ShopData.MetalPipeSellPrice;
            totalValue += ip * ShopData.IronPlateSellPrice;
            totalValue += ch * ShopData.ChemicalSellPrice;
            totalValue += pl * ShopData.PlasticPipeSellPrice;
            totalValue += bat * ShopData.BatterySellPrice;

            clientInv.circuits -= c;
            clientInv.metalPipes -= mp;
            clientInv.ironPlates -= ip;
            clientInv.chemicals -= ch;
            clientInv.plasticPipes -= pl;
            clientInv.scrapBatteries -= bat;
            
            clientInv.SaveData();

            AddCreditsServerInternal(totalValue);
            SellScrapResultClientRpc(totalValue, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } } });
        }
    }

    [ClientRpc]
    public void SellScrapResultClientRpc(int totalValue, ClientRpcParams rpcParams = default)
    {
        Debug.Log($"[Store] Sold all scrap for {totalValue} Energy Cells!");
        if (ItemNotificationManager.Instance != null)
        {
            ItemNotificationManager.Instance.ShowNotification("Energy Cells", totalValue);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void AddCreditsServerRpc(int amount, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

        // Khi client bán đồ, gọi hàm này để thêm tiền (amount > 0)
        // Nếu client muốn trừ tiền, ta chặn lại để tránh hack
        if (amount < 0) 
        {
            Debug.LogWarning("[Security] Chặn yêu cầu trừ tiền trái phép từ Client!");
            return;
        }
        
        AddCreditsServerInternal(amount);
    }

    private void AddCreditsServerInternal(int amount)
    {
        if (!IsServer) return;

        // Cập nhật NGAY LẬP TỨC trên Server cho tất cả PlayerInventory
        PlayerInventory[] allInvs = FindObjectsByType<PlayerInventory>(FindObjectsInactive.Exclude);
        foreach(var inv in allInvs)
        {
            inv.credits += amount;
            if (inv.credits < 0) inv.credits = 0;
            if (inv.IsOwner) GlobalPlayerData.credits = inv.credits;
        }

        UpdateSharedCreditsClientRpc(amount);
    }

    [ClientRpc]
    public void UpdateSharedCreditsClientRpc(int amount)
    {
        if (IsServer) return; // Host đã cập nhật ở AddCreditsServerInternal rồi, không cộng đúp
        
        // Find local player and update
        PlayerInventory[] allInvs = FindObjectsByType<PlayerInventory>(FindObjectsInactive.Exclude);
        foreach(var inv in allInvs)
        {
            if (inv.IsOwner)
            {
                inv.credits += amount;
                if (inv.credits < 0) inv.credits = 0;
                GlobalPlayerData.credits = inv.credits;
            }
        }
    }

    /// <summary>
    /// Trừ credits. Trả về true nếu thành công. 
    /// CHÚ Ý: CHỈ NÊN GỌI TRÊN SERVER để phân xử công bằng!
    /// </summary>
    public bool SpendCredits(int amount)
    {
        if (credits >= amount)
        {
            // Trừ ngay lập tức trên Server cho TẤT CẢ mọi người
            if (IsServer) 
            {
                AddCreditsServerInternal(-amount);
                return true;
            }
            else 
            {
                // Nếu là Client gọi trực tiếp (ví dụ mua Shop mà chưa check Server), 
                // thì ta chỉ trừ ảo để hiển thị, chờ Server xác nhận.
                credits -= amount;
                if (credits < 0) credits = 0;
                GlobalPlayerData.credits = credits;
                return true;
            }
        }
        return false;
    }

    public void AddCredits(int amount)
    {
        if (IsSpawned)
        {
            AddCreditsServerRpc(amount);
        }
        else
        {
            credits += amount;
            GlobalPlayerData.credits = credits;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestLoadSceneServerRpc(string sceneName, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != Unity.Netcode.NetworkManager.ServerClientId) return;
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.SceneManager != null)
        {
            Unity.Netcode.NetworkManager.Singleton.SceneManager.LoadScene(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }
    }

    public void ClearInventoryOnDeath()
    {
        circuits = 0;
        metalPipes = 0;
        ironPlates = 0;
        chemicals = 0;
        plasticPipes = 0;
        scrapBatteries = 0;
        rareLootCount = 0;
        hasEscapeKey = false;
        
        basicGasMasks = 0;
        advancedGasMasks = 0;
        hasFlashlight = false;
        hasUVFlashlight = false;
        hasCrowbar = false;
        hasShovel = false;
        hasMachete = false;
        hasAxe = false;
        hasBat = false;

        healthPacks = 0;
        oxygenTanks = 0;
        antidotes = 0;
        
        Debug.Log("<color=red>[Inventory]</color> All items lost due to death.");
        SaveData(true); // Phải lưu lại vào ổ cứng ngay lập tức để ghi đè việc mất đồ
    }

    private Texture2D _currencyBgTex;

    void OnGUI()
    {
        if (IsSpawned && !IsOwner) return;
        
        // CHỈ HIỆN TRONG SCENE WaitingRoom
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "WaitingRoom") return;

        if (_currencyBgTex == null)
        {
            _currencyBgTex = new Texture2D(1, 1);
            _currencyBgTex.SetPixel(0, 0, new Color(0.04f, 0.05f, 0.07f, 0.90f));
            _currencyBgTex.Apply();
        }

        // Vẽ UI ở góc TRÊN CÙNG BÊN PHẢI (Top-Right)
        float panelW = 160f;
        float panelH = 45f;
        float px = Screen.width - panelW - 20f;
        float py = 20f; // Cách mép trên 20px

        GUI.DrawTexture(new Rect(px, py, panelW, panelH), _currencyBgTex);
        
        Color colAmber = new Color(1.000f, 0.702f, 0.000f);
        GUI.color = colAmber;
        Texture2D tex = Texture2D.whiteTexture;
        float len = 8f; float thick = 2f;
        
        // Vẽ góc trang trí Sci-fi
        GUI.DrawTexture(new Rect(px, py, len, thick), tex);
        GUI.DrawTexture(new Rect(px, py, thick, len), tex);
        GUI.DrawTexture(new Rect(px + panelW - len, py, len, thick), tex);
        GUI.DrawTexture(new Rect(px + panelW - thick, py, thick, len), tex);
        GUI.DrawTexture(new Rect(px, py + panelH - thick, len, thick), tex);
        GUI.DrawTexture(new Rect(px, py + panelH - len, thick, len), tex);
        GUI.DrawTexture(new Rect(px + panelW - len, py + panelH - thick, len, thick), tex);
        GUI.DrawTexture(new Rect(px + panelW - thick, py + panelH - len, thick, len), tex);
        GUI.color = Color.white;

        GUIStyle style = new GUIStyle();
        style.fontSize = 18;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = colAmber;
        style.alignment = TextAnchor.MiddleCenter;

        GUI.Label(new Rect(px, py, panelW, panelH), $"◈  EC: {credits}  ◈", style);
    }

    // ====================================================================================
    // SERVER-AUTHORITATIVE MINIGAMES & SHOP LOGIC
    // ====================================================================================

    // --- SHOP ---
    [ServerRpc(RequireOwnership = false)]
    public void RequestBuyItemServerRpc(ulong stationNetworkObjectId, string itemId, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        
        int price = 0;
        var itemIndex = ShopData.BuyableItems.FindIndex(x => x.id == itemId);
        if (itemIndex != -1) price = ShopData.BuyableItems[itemIndex].currentPrice;
        else return;

        if (credits < price) return;
        SpendCredits(price); // deducts on server, syncs to client

        switch (itemId)
        {
            case "antidote": antidotes++; break;
            case "health_pack": healthPacks++; break;
            case "full_health_kit": healthPacks += 2; break;
            case "basic_gas_mask": basicGasMasks++; break;
            case "advanced_gas_mask": advancedGasMasks++; break;
            case "flashlight": hasFlashlight = true; break;
            case "axe": hasAxe = true; break;
            case "machete": hasMachete = true; break;
            case "bag_10_slots": if (maxSlots < 10) maxSlots = 10; break;
            case "bag_15_slots": if (maxSlots < 15) maxSlots = 15; break;
            case "battery_pack": scrapBatteries += 3; break;
            case "chemical_canister": chemicals += 2; break;
            case "circuit_board": circuits += 2; break;
            case "oxygen_tank": oxygenTanks++; break;
        }

        BuyItemResultClientRpc(stationNetworkObjectId, itemId, maxSlots, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { rpcParams.Receive.SenderClientId } } });
    }
    
    [ClientRpc]
    public void BuyItemResultClientRpc(ulong stationNetworkObjectId, string itemId, int newMaxSlots, ClientRpcParams rpcParams = default)
    {
        maxSlots = newMaxSlots;
        OnShopBuyResult?.Invoke(stationNetworkObjectId, itemId);
    }

    // --- SLOT MACHINE ---
    [ServerRpc(RequireOwnership = false)]
    public void RequestSlotSpinServerRpc(ulong stationNetworkObjectId, int betAmount, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (credits < betAmount || betAmount < 10) return;
        SpendCredits(betAmount);
        
        int[] WGHT = { 1, 3, 6, 6, 6, 6, 4 };
        float[] PAY = { 10f, 5f, 2.5f, 2f, 2f, 2f, 3f };
        int[] result = new int[3];
        
        for (int i=0; i<3; i++) {
            int tot = 0; foreach(int w in WGHT) tot += w;
            int r = Random.Range(0, tot);
            for(int j=0; j<WGHT.Length; j++) { r-=WGHT[j]; if(r<0) { result[i] = j; break; } }
            if (result[i] == 0 && r >= 0) result[i] = WGHT.Length - 1;
        }
        
        int a = result[0], b = result[1], c = result[2];
        if (a == b && b == c) AddCredits(Mathf.RoundToInt(betAmount * PAY[a]));
        else if (a == b || b == c || a == c) AddCredits(betAmount);
        
        SlotSpinResultClientRpc(stationNetworkObjectId, result[0], result[1], result[2], new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { rpcParams.Receive.SenderClientId } } });
    }

    [ClientRpc]
    public void SlotSpinResultClientRpc(ulong stationNetworkObjectId, int r0, int r1, int r2, ClientRpcParams rpcParams = default)
    {
        OnSlotSpinResult?.Invoke(stationNetworkObjectId, r0, r1, r2);
    }

    // --- DICE DUEL ---
    [ServerRpc(RequireOwnership = false)]
    public void RequestDiceRollServerRpc(ulong stationNetworkObjectId, int betAmount, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (credits < betAmount || betAmount < 10) return;
        SpendCredits(betAmount);
        
        int p1 = Random.Range(1, 7); int p2 = Random.Range(1, 7);
        int d1 = Random.Range(1, 7); int d2 = Random.Range(1, 7);
        int pt = p1 + p2; int dt2 = d1 + d2;
        
        if (pt > dt2) AddCredits((p1 == p2) ? Mathf.RoundToInt(betAmount * 2.5f) : betAmount * 2);
        else if (pt == dt2) AddCredits(betAmount);
        
        DiceRollResultClientRpc(stationNetworkObjectId, p1, p2, d1, d2, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { rpcParams.Receive.SenderClientId } } });
    }

    [ClientRpc]
    public void DiceRollResultClientRpc(ulong stationNetworkObjectId, int p1, int p2, int d1, int d2, ClientRpcParams rpcParams = default)
    {
        OnDiceRollResult?.Invoke(stationNetworkObjectId, p1, p2, d1, d2);
    }

    // --- BLACKJACK ---
    [ServerRpc(RequireOwnership = false)]
    public void RequestBlackjackStartServerRpc(int betAmount, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (credits < betAmount || betAmount < 10) return;
        SpendCredits(betAmount);
        _bjBetAmount = betAmount;
        
        _bjDeck.Clear();
        for(int i=0; i<52; i++) _bjDeck.Add(i);
        for(int i=0; i<52; i++) { int r = Random.Range(i, 52); int t = _bjDeck[i]; _bjDeck[i] = _bjDeck[r]; _bjDeck[r] = t; }
        
        _bjPlayerHand.Clear(); _bjDealerHand.Clear();
        _bjPlayerHand.Add(_bjDeck[0]); _bjDeck.RemoveAt(0);
        _bjDealerHand.Add(_bjDeck[0]); _bjDeck.RemoveAt(0);
        _bjPlayerHand.Add(_bjDeck[0]); _bjDeck.RemoveAt(0);
        _bjDealerHand.Add(_bjDeck[0]); _bjDeck.RemoveAt(0); // hidden
        
        bool isXiBang = (_bjPlayerHand[0] % 13 == 0) && (_bjPlayerHand[1] % 13 == 0);
        if (isXiBang) {
            AddCredits(Mathf.RoundToInt(_bjBetAmount * 3.0f)); // Xì Bàng pays 3x
            _bjIsActive = false;
        }
        else if (GetBjScore(_bjPlayerHand) == 21) {
            AddCredits(Mathf.RoundToInt(_bjBetAmount * 2.5f));
            _bjIsActive = false;
        } else {
            _bjIsActive = true;
        }
        
        BlackjackStartClientRpc(_bjPlayerHand.ToArray(), _bjDealerHand.ToArray(), new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { rpcParams.Receive.SenderClientId } } });
    }

    [ClientRpc]
    public void BlackjackStartClientRpc(int[] playerHand, int[] dealerHand, ClientRpcParams rpcParams = default)
    {
        OnBlackjackStartResult?.Invoke(playerHand, dealerHand);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestBlackjackHitServerRpc(ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (!_bjIsActive) return;
        if (_bjDeck.Count == 0) return;
        int card = _bjDeck[0]; _bjDeck.RemoveAt(0);
        _bjPlayerHand.Add(card);
        
        int pScore = GetBjScore(_bjPlayerHand);
        if (pScore > 21) {
            _bjIsActive = false;
        }
        else if (pScore <= 21 && _bjPlayerHand.Count >= 5) {
            AddCredits(_bjBetAmount * 2); // Charlie
            _bjIsActive = false;
        }
        
        BlackjackHitClientRpc(card, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { rpcParams.Receive.SenderClientId } } });
    }

    [ClientRpc]
    public void BlackjackHitClientRpc(int card, ClientRpcParams rpcParams = default)
    {
        OnBlackjackHitResult?.Invoke(card);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestBlackjackStandServerRpc(ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (!_bjIsActive) return;
        int pScore = GetBjScore(_bjPlayerHand);
        if (pScore > 21 || _bjPlayerHand.Count >= 5) return; // Already resolved
        
        List<int> drawn = new List<int>();
        while (GetBjScore(_bjDealerHand) < 17) {
            int card = _bjDeck[0]; _bjDeck.RemoveAt(0);
            _bjDealerHand.Add(card);
            drawn.Add(card);
        }
        
        int dScore = GetBjScore(_bjDealerHand);
        if (dScore > 21 || pScore > dScore) AddCredits(_bjBetAmount * 2);
        else if (pScore == dScore) AddCredits(_bjBetAmount);
        
        _bjIsActive = false;
        
        BlackjackStandClientRpc(drawn.ToArray(), new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { rpcParams.Receive.SenderClientId } } });
    }

    [ClientRpc]
    public void BlackjackStandClientRpc(int[] drawnCards, ClientRpcParams rpcParams = default)
    {
        OnBlackjackStandResult?.Invoke(drawnCards);
    }

    private int GetBjScore(System.Collections.Generic.List<int> hand) {
        int score = 0, aces = 0;
        foreach(int c in hand) {
            int val = (c % 13) + 1;
            if (val > 10) val = 10;
            score += val;
            if (val == 1) aces++;
        }
        while (aces > 0 && score + 10 <= 21) { score += 10; aces--; }
        return score;
    }
}
