using System.Collections.Generic;
using UnityEngine;

public enum ShopItemCategory
{
    Consumable,
    Equipment,
    Utility
}

public enum MarketEvent
{
    Normal,
    TechBoom,         // Phế liệu điện tử bán được giá RẤT CAO
    MetalShortage,    // Phế liệu kim loại bán được giá CAO
    MedicalCrisis,    // Đồ hồi máu bán rất đắt
    BlackFriday       // Mọi thứ trong shop giảm giá 50%
}

public struct ShopItemData
{
    public string id;
    public string displayName;
    public string description;
    public int basePrice;
    public int currentPrice;
    public ShopItemCategory category;

    public ShopItemData(string id, string displayName, string description, int basePrice, ShopItemCategory category)
    {
        this.id = id;
        this.displayName = displayName;
        this.description = description;
        this.basePrice = basePrice;
        this.currentPrice = basePrice;
        this.category = category;
    }
}

public static class ShopData
{
    public static MarketEvent CurrentEvent = MarketEvent.Normal;
    public static string EventDescription = "MARKET STATUS: NORMAL";

    public static List<ShopItemData> BuyableItems = new List<ShopItemData>
    {
        new ShopItemData("health_pack",       "Health Pack",        "Restores 50 HP instantly.",                    50,  ShopItemCategory.Consumable),
        new ShopItemData("full_health_kit",   "Full Health Kit",    "Fully restores health to maximum.",           120,  ShopItemCategory.Consumable),
        new ShopItemData("antidote",          "Parasite Antidote",  "Cures parasite infection immediately.",       150,  ShopItemCategory.Consumable),
        new ShopItemData("basic_gas_mask",    "Basic Gas Mask",     "80% toxin protection. Lasts ~60 seconds.",     80,  ShopItemCategory.Equipment),
        new ShopItemData("advanced_gas_mask", "Advanced Gas Mask",  "95% toxin protection. Lasts ~300 seconds.",   200,  ShopItemCategory.Equipment),
        new ShopItemData("battery_pack",      "Battery Pack",       "Contains 3 scrap batteries.",                  60,  ShopItemCategory.Utility),
        new ShopItemData("chemical_canister", "Chemical Canister",  "Contains 2 chemical compounds.",               40,  ShopItemCategory.Utility),
        new ShopItemData("circuit_board",     "Circuit Board",      "Contains 2 circuit modules.",                  35,  ShopItemCategory.Utility),
        new ShopItemData("oxygen_tank",       "Oxygen Tank",        "Restores oxygen to maximum when used.",       100,  ShopItemCategory.Consumable),
    };

    // Scrap base prices
    public const int BASE_CircuitPrice = 15;
    public const int BASE_MetalPipePrice = 10;
    public const int BASE_IronPlatePrice = 20;
    public const int BASE_ChemicalPrice = 25;
    public const int BASE_PlasticPipePrice = 5;
    public const int BASE_BatteryPrice = 30;

    // Current sell prices
    public static int CircuitSellPrice = 15;
    public static int MetalPipeSellPrice = 10;
    public static int IronPlateSellPrice = 20;
    public static int ChemicalSellPrice = 25;
    public static int PlasticPipeSellPrice = 5;
    public static int BatterySellPrice = 30;

    /// <summary>
    /// Random ra một sự kiện kinh tế mới, gọi khi Player về lại WaitingRoom.
    /// </summary>
    public static void RollMarketEvent()
    {
        // Random từ 0 đến 4
        CurrentEvent = (MarketEvent)Random.Range(0, 5);

        // Reset về giá gốc
        for (int i = 0; i < BuyableItems.Count; i++)
        {
            var item = BuyableItems[i];
            item.currentPrice = item.basePrice;
            BuyableItems[i] = item;
        }

        CircuitSellPrice = BASE_CircuitPrice;
        MetalPipeSellPrice = BASE_MetalPipePrice;
        IronPlateSellPrice = BASE_IronPlatePrice;
        ChemicalSellPrice = BASE_ChemicalPrice;
        PlasticPipeSellPrice = BASE_PlasticPipePrice;
        BatterySellPrice = BASE_BatteryPrice;

        switch (CurrentEvent)
        {
            case MarketEvent.Normal:
                EventDescription = "MARKET STATUS: NORMAL (PRICES STABLE)";
                break;

            case MarketEvent.TechBoom:
                EventDescription = "EVENT: TECH BOOM! (+100% TECH SCRAP VALUE)";
                CircuitSellPrice = (int)(BASE_CircuitPrice * 2.0f);
                BatterySellPrice = (int)(BASE_BatteryPrice * 2.0f);
                break;

            case MarketEvent.MetalShortage:
                EventDescription = "EVENT: METAL SHORTAGE! (+50% METAL SCRAP VALUE)";
                MetalPipeSellPrice = (int)(BASE_MetalPipePrice * 1.5f);
                IronPlateSellPrice = (int)(BASE_IronPlatePrice * 1.5f);
                break;

            case MarketEvent.MedicalCrisis:
                EventDescription = "EVENT: MEDICAL CRISIS! (+50% MEDICAL ITEM COST)";
                for (int i = 0; i < BuyableItems.Count; i++)
                {
                    if (BuyableItems[i].id == "health_pack" || BuyableItems[i].id == "full_health_kit")
                    {
                        var item = BuyableItems[i];
                        item.currentPrice = (int)(item.basePrice * 1.5f);
                        BuyableItems[i] = item;
                    }
                }
                break;

            case MarketEvent.BlackFriday:
                EventDescription = "EVENT: BLACK FRIDAY! (-50% ALL SHOP PRICES)";
                for (int i = 0; i < BuyableItems.Count; i++)
                {
                    var item = BuyableItems[i];
                    item.currentPrice = Mathf.Max(1, (int)(item.basePrice * 0.5f));
                    BuyableItems[i] = item;
                }
                break;
        }
    }
}
