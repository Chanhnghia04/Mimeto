using UnityEngine;
using UnityEditor;

/// <summary>
/// Automatically reimports the fixed Scrap prefabs so Unity picks up
/// the rotation corrections made to the YAML files.
///
/// This script runs once via the Unity menu:
///   Tools → Scrap Setup → Reimport Fixed Prefabs
/// </summary>
[InitializeOnLoad]
public static class ScrapPrefabReimporter
{
    private static readonly string[] TargetPrefabs = new[]
    {
        "Assets/Prefabs/Items/Scrap_electrical-circuit.prefab",
        "Assets/Prefabs/Items/Scrap_IronPlate.prefab",
        "Assets/Prefabs/Items/Scrap_Battery.prefab",
        "Assets/Prefabs/Items/Scrap_Chemical.prefab",
        "Assets/Prefabs/Items/Scrap_MetalPipe.prefab",
        "Assets/Prefabs/Items/Scrap_PlasticPipe.prefab",
    };

    // Run automatically once when Unity finishes loading / domain reloads
    static ScrapPrefabReimporter()
    {
        // Defer until Editor is fully ready
        EditorApplication.delayCall += AutoReimportOnce;
    }

    private static void AutoReimportOnce()
    {
        // Only run once per session using SessionState
        const string sessionKey = "ScrapPrefabReimporter_Done";
        if (SessionState.GetBool(sessionKey, false)) return;
        SessionState.SetBool(sessionKey, true);

        ReimportPrefabs(silent: true);
    }

    [MenuItem("Tools/Scrap Setup/Reimport Fixed Prefabs")]
    public static void ReimportPrefabsMenu()
    {
        ReimportPrefabs(silent: false);
    }

    private static void ReimportPrefabs(bool silent)
    {
        int count = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string path in TargetPrefabs)
            {
                if (System.IO.File.Exists(path) ||
                    System.IO.File.Exists(System.IO.Path.Combine(
                        System.IO.Directory.GetCurrentDirectory(), path)))
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    count++;
                    Debug.Log($"[ScrapReimporter] Reimported: {path}");
                }
                else
                {
                    Debug.LogWarning($"[ScrapReimporter] Prefab not found, skipped: {path}");
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        if (!silent)
        {
            EditorUtility.DisplayDialog(
                "Reimport Complete",
                $"Successfully reimported {count} prefab(s).\n\n" +
                "Scrap_Circuit  → rotation fixed (was 180° flipped)\n" +
                "Scrap_IronPlate → rotation fixed (was 90° tilted)\n\n" +
                "Items should now spawn upright in the scene.",
                "OK");
        }
        else
        {
            Debug.Log($"[ScrapReimporter] Auto-reimported {count} Scrap prefabs on startup.");
        }
    }
}
