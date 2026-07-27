using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

public class PlayerInventory : NetworkBehaviour
{
    public NetworkVariable<int> MatchSeed = new NetworkVariable<int>(0);
    public static int GlobalMatchSeed = 0;

    public int circuits = 0;
    public int metalPipes = 0;
    public int ironPlates = 0;
    public int chemicals = 0;
    public int plasticPipes = 0;
    public int scrapBatteries = 0;
    
    [Header("Currency")]
    public int credits = 0;

    public int basicGasMasks = 0;
    public int advancedGasMasks = 0;
    public bool hasUVFlashlight = false;
    public bool hasCrowbar = false;
    public bool hasShovel = false;
    public bool hasMachete = false;
    public bool hasAxe = false;
    public bool hasBat = false;

    [Header("Shop Consumables")]
    public int healthPacks = 0;
    public int oxygenTanks = 0;

    [Header("Escape & Loot")]
    public bool hasEscapeKey = false;
    public int rareLootCount = 0;

    void Start()
    {
        if (GlobalPlayerData.hasSavedData)
        {
            circuits = GlobalPlayerData.circuits;
            metalPipes = GlobalPlayerData.metalPipes;
            ironPlates = GlobalPlayerData.ironPlates;
            chemicals = GlobalPlayerData.chemicals;
            plasticPipes = GlobalPlayerData.plasticPipes;
            scrapBatteries = GlobalPlayerData.scrapBatteries;
            credits = GlobalPlayerData.credits;
            basicGasMasks = GlobalPlayerData.basicGasMasks;
            advancedGasMasks = GlobalPlayerData.advancedGasMasks;
            hasUVFlashlight = GlobalPlayerData.hasUVFlashlight;
            hasCrowbar = GlobalPlayerData.hasCrowbar;
            hasShovel = GlobalPlayerData.hasShovel;
            hasMachete = GlobalPlayerData.hasMachete;
            hasAxe = GlobalPlayerData.hasAxe;
            hasBat = GlobalPlayerData.hasBat;
            rareLootCount = GlobalPlayerData.rareLootCount;
            healthPacks = GlobalPlayerData.healthPacks;
            oxygenTanks = GlobalPlayerData.oxygenTanks;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer && IsOwner)
        {
            MatchSeed.Value = (int)(System.DateTime.Now.Ticks % 100000000);
        }
    }

    void Update()
    {
        if (GlobalMatchSeed == 0 && MatchSeed.Value != 0)
        {
            GlobalMatchSeed = MatchSeed.Value;
            Debug.Log($"[MapSync] GlobalMatchSeed set to: {GlobalMatchSeed}");
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy(); // NetworkBehaviour requires base.OnDestroy()
        
        GlobalPlayerData.circuits = circuits;
        GlobalPlayerData.metalPipes = metalPipes;
        GlobalPlayerData.ironPlates = ironPlates;
        GlobalPlayerData.chemicals = chemicals;
        GlobalPlayerData.plasticPipes = plasticPipes;
        GlobalPlayerData.scrapBatteries = scrapBatteries;
        GlobalPlayerData.credits = credits;
        GlobalPlayerData.basicGasMasks = basicGasMasks;
        GlobalPlayerData.advancedGasMasks = advancedGasMasks;
        GlobalPlayerData.hasUVFlashlight = hasUVFlashlight;
        GlobalPlayerData.hasCrowbar = hasCrowbar;
        GlobalPlayerData.hasShovel = hasShovel;
        GlobalPlayerData.hasMachete = hasMachete;
        GlobalPlayerData.hasAxe = hasAxe;
        GlobalPlayerData.hasBat = hasBat;
        GlobalPlayerData.rareLootCount = rareLootCount;
        GlobalPlayerData.healthPacks = healthPacks;
        GlobalPlayerData.oxygenTanks = oxygenTanks;
        GlobalPlayerData.hasSavedData = true;

        // Lưu xuống ổ cứng
        GlobalPlayerData.Save();
    }

    public void AddScrap(string type, int amount)
    {
        if (IsSpawned && !IsOwner && !IsServer) return; // Chỉ cập nhật túi đồ cho chủ sở hữu hoặc khi test offline
        
        switch (type.ToLower())
        {
            case "circuit":
                circuits += amount;
                break;
            case "metal_pipe":
            case "metal pipe":
                metalPipes += amount;
                break;
            case "iron_plate":
            case "iron plate":
                ironPlates += amount;
                break;
            case "chemical":
                chemicals += amount;
                break;
            case "pipe":
            case "plastic":
            case "plastic_pipe":
            case "plastic pipe":
            case "rubber":
                plasticPipes += amount;
                break;
            case "battery":
                scrapBatteries += amount;
                break;
            case "key":
            case "escape_key":
                hasEscapeKey = true;
                Debug.Log("<color=yellow>Obtained Escape Key!</color>");
                break;
            case "rare_loot":
            case "relic":
                rareLootCount += amount;
                Debug.Log($"<color=cyan>Obtained Rare Loot! Total: {rareLootCount}</color>");
                break;
        }
        if (ItemNotificationManager.Instance != null)
        {
            ItemNotificationManager.Instance.ShowNotification(type, amount);
        }

        Debug.Log($"Added {amount} {type}. Inventory: C={circuits}, MP={metalPipes}, IP={ironPlates}, Ch={chemicals}, Pl={plasticPipes}, Bat={scrapBatteries}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void SyncDestroyItemServerRpc(Vector3 pos, string itemType)
    {
        SyncDestroyItemClientRpc(pos, itemType);
    }

    [ClientRpc]
    private void SyncDestroyItemClientRpc(Vector3 pos, string itemType)
    {
        // Ignore on the client that actually picked it up (they destroyed it locally already)
        if (IsOwner) return;

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
    public void SyncLootChestItemServerRpc(Vector3 chestPos, string itemType)
    {
        SyncLootChestItemClientRpc(chestPos, itemType);
    }

    [ClientRpc]
    private void SyncLootChestItemClientRpc(Vector3 chestPos, string itemType)
    {
        if (IsOwner) return; // The one who looted it already removed it locally

        // Find the chest at chestPos
        Collider[] colls = Physics.OverlapSphere(chestPos, 1.0f);
        foreach (var col in colls)
        {
            Chest chest = col.GetComponent<Chest>();
            if (chest != null)
            {
                // Remove the item from this chest
                var entry = chest.items.Find(e => e.itemType == itemType);
                if (entry != null)
                {
                    chest.RemoveItem(entry);
                }
                break;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SyncEscapeEventServerRpc(int eventId)
    {
        SyncEscapeEventClientRpc(eventId);
    }

    [ClientRpc]
    private void SyncEscapeEventClientRpc(int eventId)
    {
        if (eventId == 0) // Unlock Escape Door
        {
            EscapeManager.Instance?.UnlockEscape();
        }
        else if (eventId == 1) // Beacon Build
        {
            EscapeBeacon beacon = Object.FindAnyObjectByType<EscapeBeacon>();
            if (beacon != null) beacon.ForceBuild();
        }
        else if (eventId == 2) // Reactor Meltdown
        {
            EscapeReactor reactor = Object.FindAnyObjectByType<EscapeReactor>();
            if (reactor != null) reactor.ForceShutdown();
        }
        else if (eventId == 3) // Extraction System Assemble step
        {
            ExtractionSystem ex = Object.FindAnyObjectByType<ExtractionSystem>();
            if (ex != null) ex.ForceAssembleStep();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SyncAssemblyPartServerRpc(string partName, Vector3 pos)
    {
        SyncAssemblyPartClientRpc(partName, pos);
    }

    [ClientRpc]
    private void SyncAssemblyPartClientRpc(string partName, Vector3 pos)
    {
        if (IsOwner) return; // Người nhặt tự xử lý local
        
        Collider[] colls = Physics.OverlapSphere(pos, 0.5f);
        foreach (var col in colls)
        {
            EscapePart part = col.GetComponent<EscapePart>();
            if (part != null)
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
        SyncCipherNoteClientRpc(noteIndex, digits, pos);
    }

    [ClientRpc]
    private void SyncCipherNoteClientRpc(int noteIndex, string digits, Vector3 pos)
    {
        if (IsOwner) return; // Người nhặt tự xử lý local
        
        Collider[] colls = Physics.OverlapSphere(pos, 0.5f);
        foreach (var col in colls)
        {
            CipherNote note = col.GetComponent<CipherNote>();
            if (note != null)
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
        
        int totalValue = 0;
        totalValue += circuits * ShopData.CircuitSellPrice;
        totalValue += metalPipes * ShopData.MetalPipeSellPrice;
        totalValue += ironPlates * ShopData.IronPlateSellPrice;
        totalValue += chemicals * ShopData.ChemicalSellPrice;
        totalValue += plasticPipes * ShopData.PlasticPipeSellPrice;
        totalValue += scrapBatteries * ShopData.BatterySellPrice;
        
        circuits = 0;
        metalPipes = 0;
        ironPlates = 0;
        chemicals = 0;
        plasticPipes = 0;
        scrapBatteries = 0;
        
        if (IsSpawned)
        {
            AddCreditsServerRpc(totalValue);
        }
        else
        {
            credits += totalValue;
            GlobalPlayerData.credits = credits;
        }
        
        Debug.Log($"[Store] Sold all scrap for {totalValue} Energy Cells!");
        
        if (ItemNotificationManager.Instance != null)
        {
            ItemNotificationManager.Instance.ShowNotification("Energy Cells", totalValue);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void AddCreditsServerRpc(int amount)
    {
        UpdateCreditsClientRpc(amount);
    }

    [ClientRpc]
    private void UpdateCreditsClientRpc(int amount)
    {
        credits += amount;
        GlobalPlayerData.credits = credits;
    }

    /// <summary>
    /// Trừ credits nếu đủ tiền. Trả về true nếu thành công.
    /// </summary>
    public bool SpendCredits(int amount)
    {
        if (credits >= amount)
        {
            if (IsSpawned)
            {
                AddCreditsServerRpc(-amount);
            }
            else
            {
                credits -= amount;
                GlobalPlayerData.credits = credits;
            }
            return true;
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
    public void RequestLoadSceneServerRpc(string sceneName)
    {
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
        hasUVFlashlight = false;
        hasCrowbar = false;
        hasShovel = false;
        hasMachete = false;
        hasAxe = false;
        hasBat = false;

        healthPacks = 0;
        oxygenTanks = 0;
        
        Debug.Log("<color=red>[Inventory]</color> All items lost due to death.");
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
}