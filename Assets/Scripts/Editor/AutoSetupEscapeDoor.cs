using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class AutoSetupEscapeDoor
{
    static AutoSetupEscapeDoor()
    {
        EditorApplication.delayCall += DoSetup;
    }

    [MenuItem("Tools/Mimeto/Setup Escape Door & Key")]
    public static void ManualSetup()
    {
        DoSetupInternal(true);
    }

    static void DoSetup()
    {
        if (EditorPrefs.GetBool("AutoSetupEscapeDoor_Done_v1", false)) return;
        DoSetupInternal(false);
        EditorPrefs.SetBool("AutoSetupEscapeDoor_Done_v1", true);
    }

    static void DoSetupInternal(bool force)
    {
        // Nếu không force và đã có cửa thì bỏ qua
        if (!force && Object.FindFirstObjectByType<ExtractionSystem>() != null)
        {
            return;
        }

        // 1. Tạo Cửa Thoát Hiểm
        GameObject door = new GameObject("EscapeDoor_WinPoint");
        door.transform.position = new Vector3(0, 1.25f, 5); 
        
        // Model Cửa
        GameObject doorModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorModel.transform.SetParent(door.transform);
        doorModel.transform.localPosition = Vector3.zero;
        doorModel.transform.localScale = new Vector3(2, 2.5f, 0.2f);
        
        // Cửa màu xanh lục
        Renderer doorRend = doorModel.GetComponent<Renderer>();
        if (doorRend != null) doorRend.sharedMaterial.color = new Color(0.2f, 0.8f, 0.3f);
        Object.DestroyImmediate(doorModel.GetComponent<Collider>()); // Bỏ collider mặc định

        var bc = door.AddComponent<BoxCollider>();
        bc.size = new Vector3(2.5f, 3f, 1f); // Box to hơn để dễ bấm
        door.AddComponent<ExtractionSystem>();

        // 2. Tạo Chìa khóa
        GameObject key = new GameObject("EscapeKey_Sample");
        key.transform.position = new Vector3(-2, 0.5f, 3);
        
        GameObject keyModel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        keyModel.transform.SetParent(key.transform);
        keyModel.transform.localPosition = Vector3.zero;
        keyModel.transform.localScale = new Vector3(0.2f, 0.5f, 0.2f);
        
        // Chìa khóa màu vàng
        Renderer keyRend = keyModel.GetComponent<Renderer>();
        if (keyRend != null) keyRend.sharedMaterial.color = Color.yellow;
        Object.DestroyImmediate(keyModel.GetComponent<Collider>());
        
        ScrapItem keyScrap = key.AddComponent<ScrapItem>();
        keyScrap.scrapType = "key";
        keyScrap.amount = 1;
        keyScrap.interactHint = "Press [E] to pick up Escape Key";

        // 3. Tạo Đồ hiếm
        GameObject loot = new GameObject("RareLoot_Sample");
        loot.transform.position = new Vector3(2, 0.5f, 3);
        
        GameObject lootModel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lootModel.transform.SetParent(loot.transform);
        lootModel.transform.localPosition = Vector3.zero;
        lootModel.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        
        // Đồ hiếm màu tím
        Renderer lootRend = lootModel.GetComponent<Renderer>();
        if (lootRend != null) lootRend.sharedMaterial.color = new Color(0.6f, 0.1f, 0.8f);
        Object.DestroyImmediate(lootModel.GetComponent<Collider>());
        
        ScrapItem lootScrap = loot.AddComponent<ScrapItem>();
        lootScrap.scrapType = "rare_loot";
        lootScrap.amount = 1;
        lootScrap.interactHint = "Press [E] to pick up Rare Relic";

        Selection.activeGameObject = door;
        Debug.Log("<color=green>[Thành Công] Đã thiết lập Cửa Thoát Hiểm, Chìa Khóa, và Đồ Hiếm vào Scene!</color>");
    }
}
