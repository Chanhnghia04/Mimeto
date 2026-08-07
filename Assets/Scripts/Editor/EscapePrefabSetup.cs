#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// Editor tool: Tạo prefab từ file .glb trong Models/ → lưu vào Resources/EscapeAssets/
/// để Resources.Load hoạt động trong cả Editor lẫn Build.
///
/// Chạy: Tools → Escape Setup → Create Escape Prefabs
/// </summary>
public static class EscapePrefabSetup
{
    // Mapping: (tên prefab trong Resources, đường dẫn glb gốc)
    private static readonly (string prefabName, string glbPath)[] Mappings =
    {
        ("Mesh_Gear",          "Assets/Models/Mesh_Gear_Assets/selected.glb"),
        ("Mesh_FuelCanister",  "Assets/Models/Mesh_FuelCanister_Assets/selected.glb"),
        ("Mesh_CircuitBoard",  "Assets/Models/Mesh_CircuitBoard_Assets/selected.glb"),
        ("Mesh_Antenna",       "Assets/Models/Mesh_Antenna_Assets/selected.glb"),
        ("Mesh_Keypad",        "Assets/Models/Mesh_Keypad_Assets/selected.glb"),
        ("Mesh_Reactor",       "Assets/Models/Mesh_Reactor_Assets/selected.glb"),
    };

    [MenuItem("Tools/Escape Setup/Create Escape Prefabs")]
    public static void CreateEscapePrefabs()
    {
        const string targetFolder = "Assets/Resources/EscapeAssets";

        // Tạo folder nếu chưa có
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(targetFolder))
            AssetDatabase.CreateFolder("Assets/Resources", "EscapeAssets");

        int created = 0;
        int skipped = 0;

        foreach (var (prefabName, glbPath) in Mappings)
        {
            string prefabPath = $"{targetFolder}/{prefabName}.prefab";

            // Kiểm tra file nguồn tồn tại
            if (!File.Exists(glbPath))
            {
                Debug.LogWarning($"[EscapePrefabSetup] Không tìm thấy: {glbPath} — bỏ qua.");
                skipped++;
                continue;
            }

            // Load model gốc từ AssetDatabase
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            if (modelAsset == null)
            {
                Debug.LogWarning($"[EscapePrefabSetup] Không load được model: {glbPath} — bỏ qua.");
                skipped++;
                continue;
            }

            // Instantiate tạm để tạo prefab
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            instance.name = prefabName;

            // Lưu thành prefab
            bool success;
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out success);
            Object.DestroyImmediate(instance);

            if (success)
            {
                Debug.Log($"[EscapePrefabSetup] ✓ Đã tạo: {prefabPath}");
                created++;
            }
            else
            {
                Debug.LogError($"[EscapePrefabSetup] ✗ Lỗi tạo prefab: {prefabPath}");
            }
        }

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Escape Prefab Setup",
            $"Hoàn tất!\n\n• Tạo thành công: {created}\n• Bỏ qua: {skipped}\n\n" +
            $"Prefab đã lưu tại:\n{targetFolder}/",
            "OK"
        );
    }
}
#endif
