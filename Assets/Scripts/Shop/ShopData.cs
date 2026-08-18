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
        new ShopItemData("health_pack",       "Health Pack",        "Restores 50 HP instantly.",                   100,  ShopItemCategory.Consumable),
        new ShopItemData("full_health_kit",   "Full Health Kit",    "Fully restores health to maximum.",           250,  ShopItemCategory.Consumable),
        new ShopItemData("antidote",          "Parasite Antidote",  "Cures parasite infection immediately.",       300,  ShopItemCategory.Consumable),
        new ShopItemData("basic_gas_mask",    "Basic Gas Mask",     "80% toxin protection. Lasts ~180 seconds.",   150,  ShopItemCategory.Equipment),
        new ShopItemData("advanced_gas_mask", "Advanced Gas Mask",  "95% toxin protection. Lasts ~900 seconds.",   400,  ShopItemCategory.Equipment),
        new ShopItemData("flashlight",        "Flashlight",         "Provides visibility in dark areas.",          120,  ShopItemCategory.Equipment),
        new ShopItemData("axe",               "Axe",                "A reliable melee weapon for close combat.",   150,  ShopItemCategory.Equipment),
        new ShopItemData("machete",           "Machete",            "A sharp blade, excellent for slashing.",      150,  ShopItemCategory.Equipment),
        new ShopItemData("bag_10_slots",      "Backpack (10 Slots)",      "Expands inventory limit to 10 slots.",           500,  ShopItemCategory.Utility),
        new ShopItemData("bag_15_slots",      "Backpack (15 Slots)",      "Expands inventory limit to 15 slots.",           900,  ShopItemCategory.Utility),
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
        ApplyMarketEvent((MarketEvent)Random.Range(0, 5));
    }

    public static void ApplyMarketEvent(MarketEvent newEvent)
    {
        CurrentEvent = newEvent;

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
