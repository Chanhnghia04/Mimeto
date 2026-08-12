using UnityEngine;
using UnityEditor;
using System.IO;

public class AutoGenerateAndAssignIcons : Editor
{
    [MenuItem("Tools/Mimeto/Tự Động Tạo & Gán 1-Click Toàn Bộ Icon")]
    public static void AutoRun()
    {
        string[] itemNames = {
            "Scrap_electrical-circuit",
            "Scrap_MetalPipe",
            "Scrap_Chemical",
            "Scrap_PlasticPipe",
            "Scrap_Battery",
            "Scrap_Ironplate"
        };

        // 1. Tìm InventoryUI trong Scene trước (nếu đang mở Scene có UI)
        InventoryUI ui = FindObjectOfType<InventoryUI>(true);
        GameObject prefabRoot = null;
        string prefabPath = "";

        // Nếu không có trong Scene, tìm Prefab của InventoryUI
        if (ui == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (obj != null && obj.GetComponentInChildren<InventoryUI>(true) != null)
                {
                    prefabPath = path;
                    prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                    ui = prefabRoot.GetComponentInChildren<InventoryUI>(true);
                    break;
                }
            }
        }

        if (ui == null)
        {
            Debug.LogError("Không tìm thấy InventoryUI trong Scene hay Prefab nào!");
            return;
        }

        if (!Directory.Exists("Assets/Resources/Icons")) Directory.CreateDirectory("Assets/Resources/Icons");

        foreach (string itemName in itemNames)
        {
            string path = "Assets/Prefabs/Items/" + itemName + ".prefab";
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (modelPrefab == null)
            {
                Debug.LogWarning("Không tìm thấy prefab: " + path);
                continue;
            }

            // Chụp ảnh
            Texture2D tex = CaptureIcon(modelPrefab, itemName);
            byte[] bytes = tex.EncodeToPNG();
            string iconPath = "Assets/Resources/Icons/" + itemName + "_Icon.png";
            File.WriteAllBytes(iconPath, bytes);
            AssetDatabase.Refresh();

            // Convert sang Sprite
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(iconPath);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            // Gán vào UI
            Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            if (itemName.Contains("circuit")) ui.circuitSprite = sp;
            else if (itemName.Contains("MetalPipe")) ui.pipeSprite = sp;
            else if (itemName.Contains("Chemical")) ui.chemicalSprite = sp;
            else if (itemName.Contains("PlasticPipe")) ui.plasticSprite = sp;
            else if (itemName.Contains("Battery")) ui.batterySprite = sp;
            else if (itemName.Contains("Ironplate")) ui.ironPlateSprite = sp;
        }

        // Lưu lại Prefab hoặc đánh dấu Scene bẩn
        if (prefabRoot != null)
        {
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            Debug.Log("<color=green>Đã tự động tạo và gán toàn bộ Icon vào Inventory Prefab!</color>");
        }
        else
        {
            EditorUtility.SetDirty(ui.gameObject);
            Debug.Log("<color=green>Đã tự động tạo và gán toàn bộ Icon vào InventoryUI trong Scene!</color>");
        }
    }

    static Texture2D CaptureIcon(GameObject targetModel, string itemName)
    {
        Vector3 studioPos = new Vector3(0, -5000, 0);
        
        // --- CUSTOM ROTATION CHO TỪNG MÓN ĐỒ ---
        Vector3 customRotation = new Vector3(15f, -45f, 0f); // Mặc định
        
        if (itemName == "Scrap_electrical-circuit") 
            customRotation = new Vector3(240f, 45f, 0f); // Lật 180 độ (60 + 180 = 240) để xem mặt đối diện (mặt trên)
        else if (itemName == "Scrap_Ironplate") 
            customRotation = new Vector3(75f, 30f, 0f); // Góc nhìn từ trên xuống để thấy rõ mảng kim loại
        else if (itemName == "Scrap_MetalPipe") 
            customRotation = new Vector3(15f, 90f, 45f); // Xoay ngang và hơi nghiêng để không bị vô hình (nếu camera chĩa thẳng vào lỗ ống)

        GameObject instance = Instantiate(targetModel, studioPos, Quaternion.Euler(customRotation));
        
        MonoBehaviour[] scripts = instance.GetComponentsInChildren<MonoBehaviour>();
        foreach (var script in scripts) DestroyImmediate(script);

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(instance.transform.position, Vector3.one * 0.5f);
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
        }

        GameObject camObj = new GameObject("PhotoCamera");
        Camera cam = camObj.AddComponent<Camera>();
        
        cam.transform.position = bounds.center + new Vector3(0, 0, -10f);
        cam.transform.LookAt(bounds.center);
        
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0); 
        cam.orthographic = true;
        // Phóng to Icon Ống nước lên 1 xíu vì nó mỏng
        float zoomFactor = (itemName == "Scrap_MetalPipe") ? 1.0f : 1.2f;
        cam.orthographicSize = bounds.extents.magnitude * zoomFactor;

        RenderTexture rt = new RenderTexture(256, 256, 24);
        cam.targetTexture = rt;
        Texture2D screenShot = new Texture2D(256, 256, TextureFormat.RGBA32, false);

        cam.Render();
        RenderTexture.active = rt;
        screenShot.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
        screenShot.Apply();

        cam.targetTexture = null;
        RenderTexture.active = null;
        DestroyImmediate(camObj);
        DestroyImmediate(instance);
        DestroyImmediate(rt); // Memory leak fix
        return screenShot;
    }
}
