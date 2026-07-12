using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

public class PlayerInventory : NetworkBehaviour
{
    public int circuits = 0;
    public int metalPipes = 0;
    public int ironPlates = 0;
    public int chemicals = 0;
    public int plasticPipes = 0;
    public int scrapBatteries = 0;

    public int basicGasMasks = 0;
    public int advancedGasMasks = 0;
    public bool hasUVFlashlight = false;
    public bool hasCrowbar = false;
    public bool hasShovel = false;
    public bool hasMachete = false;
    public bool hasAxe = false;
    public bool hasBat = false;

    [Header("Escape & Loot")]
    public bool hasEscapeKey = false;
    public int rareLootCount = 0;

    void Start()
    {
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
                Debug.Log("<color=yellow>Lấy được Chìa Khóa Thoát Hiểm!</color>");
                break;
            case "rare_loot":
            case "relic":
                rareLootCount += amount;
                Debug.Log($"<color=cyan>Lấy được Đồ Hiếm! Tổng: {rareLootCount}</color>");
                break;
        }
        if (ItemNotificationManager.Instance != null)
        {
            ItemNotificationManager.Instance.ShowNotification(type, amount);
        }

        Debug.Log($"Added {amount} {type}. Inventory: C={circuits}, MP={metalPipes}, IP={ironPlates}, Ch={chemicals}, Pl={plasticPipes}, Bat={scrapBatteries}");
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
}