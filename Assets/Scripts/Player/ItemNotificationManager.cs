using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class ItemNotificationManager : MonoBehaviour
{
    public static ItemNotificationManager Instance;

    public GameObject notificationPrefab;
    public Transform container;
    public float displayDuration = 3f;

    [Header("Sprites")]
    public Sprite circuit;
    public Sprite metalPipe;
    public Sprite ironPlate;
    public Sprite chemical;
    public Sprite plastic;
    public Sprite battery;
    public Sprite key;
    public Sprite rare;

    private Dictionary<string, Sprite> spriteMap;

    void Awake()
    {
        Instance = this;
        spriteMap = new Dictionary<string, Sprite>
        {
            { "circuit", circuit },
            { "metal_pipe", metalPipe },
            { "metal pipe", metalPipe },
            { "iron_plate", ironPlate },
            { "iron plate", ironPlate },
            { "chemical", chemical },
            { "pipe", plastic },
            { "plastic", plastic },
            { "plastic_pipe", plastic },
            { "plastic pipe", plastic },
            { "battery", battery },
            { "key", key },
            { "escape_key", key },
            { "rare_loot", rare },
            { "relic", rare }
        };
    }

    public void ShowNotification(string type, int amount)
    {
        GameObject go = Instantiate(notificationPrefab, container);
        Image icon = go.transform.Find("Icon").GetComponent<Image>();
        TextMeshProUGUI text = go.transform.Find("Text").GetComponent<TextMeshProUGUI>();

        string key = type.ToLower();
        if (spriteMap.ContainsKey(key))
        {
            icon.sprite = spriteMap[key];
        }

        text.text = $"+{amount} {type.Replace("_", " ")}";
        Destroy(go, displayDuration);
    }
}
