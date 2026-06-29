using UnityEngine;
using UnityEditor;

/// <summary>
/// Runs automatically when Unity finishes compiling to force-reimport
/// all Scrap FBX models and Prefabs so Unity picks up the axis-conversion
/// and rotation fixes applied to the .meta and .prefab YAML files.
/// </summary>
[InitializeOnLoad]
public static class ScrapRotationFixer
{
    private static readonly string[] FbxPaths =
    {
        "Assets/Models/Item/machdien/base_basic_shaded.fbx",
        "Assets/Models/Item/binhhoachat/source/Silent Hill 1 Meshes - Chemical.fbx",
        "Assets/Models/Item/ongnhua/source/PIPE.fbx",
        "Assets/Models/Item/ongkimloai/source/OIL PIPE.fbx",
        "Assets/Models/Item/pin/source/AA_Battery.fbx",
    };

    private static readonly string[] PrefabPaths =
    {
        "Assets/Prefabs/Items/Scrap_Circuit.prefab",
        "Assets/Prefabs/Items/Scrap_IronPlate.prefab",
        "Assets/Prefabs/Items/Scrap_Battery.prefab",
        "Assets/Prefabs/Items/Scrap_Chemical.prefab",
        "Assets/Prefabs/Items/Scrap_MetalPipe.prefab",
        "Assets/Prefabs/Items/Scrap_PlasticPipe.prefab",
    };

    static ScrapRotationFixer()
    {
        EditorApplication.delayCall += AutoFixOnce;
    }

    static void AutoFixOnce()
    {
        const string key = "ScrapRotationFixer_v3";
        if (SessionState.GetBool(key, false)) return;
        SessionState.SetBool(key, true);

        Debug.Log("[ScrapRotationFixer] Auto-reimporting Scrap FBX + Prefabs...");
        ForceReimportAll();
    }

    [MenuItem("Tools/Scrap Setup/Force Reimport FBX + Prefabs")]
    public static void ForceReimportAll()
    {
        AssetDatabase.StartAssetEditing();
        int count = 0;
        try
        {
            foreach (string path in FbxPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    Debug.Log($"[ScrapRotationFixer] Reimported FBX: {path}");
                    count++;
                }
                else
                {
                    Debug.LogWarning($"[ScrapRotationFixer] FBX not found: {path}");
                }
            }

            foreach (string path in PrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    Debug.Log($"[ScrapRotationFixer] Reimported Prefab: {path}");
                    count++;
                }
                else
                {
                    Debug.LogWarning($"[ScrapRotationFixer] Prefab not found: {path}");
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        string msg = $"Reimported {count} asset(s).\n\n" +
                     "Changes applied:\n" +
                     "• FBX bakeAxisConversion = 1 (fixes axis flip from Blender/Maya)\n" +
                     "• All Scrap prefab rotations reset to identity (0°, 0°, 0°)\n\n" +
                     "Items should now spawn upright in the scene.";

        Debug.Log("[ScrapRotationFixer] " + msg);

        if (!Application.isBatchMode)
            EditorUtility.DisplayDialog("Scrap Rotation Fix Complete", msg, "OK");
    }
}
