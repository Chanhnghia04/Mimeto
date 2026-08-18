using UnityEngine;

public static class GlobalPlayerData
{
    public static bool hasSavedData = false;

    public static int circuits = 0;
    public static int metalPipes = 0;
    public static int ironPlates = 0;
    public static int chemicals = 0;
    public static int plasticPipes = 0;
    public static int scrapBatteries = 0;
    
    public static int maxSlots = 5;

    public static int credits = 0;

    public static int basicGasMasks = 0;
    public static int advancedGasMasks = 0;
    public static bool hasFlashlight = false;
    public static bool hasUVFlashlight = false;
    public static bool hasCrowbar = false;
    public static bool hasShovel = false;
    public static bool hasMachete = false;
    public static bool hasAxe = false;
    public static bool hasBat = false;
    
    public static int rareLootCount = 0;

    // Shop consumables
    public static int healthPacks = 0;
    public static int oxygenTanks = 0;
    public static int antidotes = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        Load();
    }

    public static void Save()
    {
        PlayerDataSave data = new PlayerDataSave
        {
            circuits = circuits,
            metalPipes = metalPipes,
            ironPlates = ironPlates,
            chemicals = chemicals,
            plasticPipes = plasticPipes,
            scrapBatteries = scrapBatteries,
            maxSlots = maxSlots,
            credits = credits,
            basicGasMasks = basicGasMasks,
            advancedGasMasks = advancedGasMasks,
            hasFlashlight = hasFlashlight,
            hasUVFlashlight = hasUVFlashlight,
            hasCrowbar = hasCrowbar,
            hasShovel = hasShovel,
            hasMachete = hasMachete,
            hasAxe = hasAxe,
            hasBat = hasBat,
            rareLootCount = rareLootCount,
            healthPacks = healthPacks,
            oxygenTanks = oxygenTanks,
            antidotes = antidotes
        };

        string json = JsonUtility.ToJson(data);
        string key = "Mimeto_SaveData" + Application.dataPath.GetHashCode();
        PlayerPrefs.SetString(key, json);
        PlayerPrefs.Save();
        hasSavedData = true;
        Debug.Log("[GlobalPlayerData] Dữ liệu đã được LƯU vào ổ cứng.");
    }

    public static void Load()
    {
        string key = "Mimeto_SaveData" + Application.dataPath.GetHashCode();
        if (PlayerPrefs.HasKey(key))
        {
            string json = PlayerPrefs.GetString(key);
            PlayerDataSave data = JsonUtility.FromJson<PlayerDataSave>(json);

            circuits = data.circuits;
            metalPipes = data.metalPipes;
            ironPlates = data.ironPlates;
            chemicals = data.chemicals;
            plasticPipes = data.plasticPipes;
            scrapBatteries = data.scrapBatteries;
            
            // Backward compatibility for maxSlots
            maxSlots = data.maxSlots == 0 ? 5 : data.maxSlots;

            credits = data.credits;
            basicGasMasks = data.basicGasMasks;
            advancedGasMasks = data.advancedGasMasks;
            hasFlashlight = data.hasFlashlight;
            hasUVFlashlight = data.hasUVFlashlight;
            hasCrowbar = data.hasCrowbar;
            hasShovel = data.hasShovel;
            hasMachete = data.hasMachete;
            hasAxe = data.hasAxe;
            hasBat = data.hasBat;
            rareLootCount = data.rareLootCount;
            healthPacks = data.healthPacks;
            oxygenTanks = data.oxygenTanks;
            antidotes = data.antidotes;
            
            hasSavedData = true;
            Debug.Log("[GlobalPlayerData] Dữ liệu đã được TẢI từ ổ cứng.");
        }
        else
        {
            Debug.Log("[GlobalPlayerData] Chưa có dữ liệu cũ, bắt đầu mới.");
        }
    }

    public static void ClearData()
    {
        string key = "Mimeto_SaveData" + Application.dataPath.GetHashCode();
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        hasSavedData = false;
        
        circuits = 0;
        metalPipes = 0;
        ironPlates = 0;
        chemicals = 0;
        plasticPipes = 0;
        scrapBatteries = 0;
        maxSlots = 5;
        credits = 0;
        basicGasMasks = 0;
        advancedGasMasks = 0;
        hasFlashlight = false;
        hasUVFlashlight = false;
        hasCrowbar = false;
        hasShovel = false;
        hasMachete = false;
        hasAxe = false;
        hasBat = false;
        rareLootCount = 0;
        healthPacks = 0;
        oxygenTanks = 0;
        antidotes = 0;
        
        Debug.Log("[GlobalPlayerData] Đã XÓA toàn bộ dữ liệu lưu trữ.");
    }
}

[System.Serializable]
public class PlayerDataSave
{
    public int circuits;
    public int metalPipes;
    public int ironPlates;
    public int chemicals;
    public int plasticPipes;
    public int scrapBatteries;
    public int maxSlots;
    public int credits;
    public int basicGasMasks;
    public int advancedGasMasks;
    public bool hasFlashlight;
    public bool hasUVFlashlight;
    public bool hasCrowbar;
    public bool hasShovel;
    public bool hasMachete;
    public bool hasAxe;
    public bool hasBat;
    public int rareLootCount;
    public int healthPacks;
    public int oxygenTanks;
    public int antidotes;
}
