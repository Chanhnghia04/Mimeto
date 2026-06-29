using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor utility: copies the existing Scrap prefabs from Assets/Prefabs/Items/
/// into Assets/Resources/Scraps/ so they can be loaded at runtime via Resources.Load.
///
/// Usage: Unity menu → Tools → Scrap Setup → Copy Scraps to Resources
/// </summary>
public static class ScrapResourcesSetup
{
    private const string SrcFolder  = "Assets/Prefabs/Items";
    private const string DestFolder = "Assets/Resources/Scraps";

    // Maps Resources.Load key → source prefab filename
    private static readonly (string key, string srcFile)[] ScrapMap = new[]
    {
        ("circuit",    "Scrap_Circuit.prefab"),
        ("chemical",   "Scrap_Chemical.prefab"),
        ("pipe",       "Scrap_PlasticPipe.prefab"),
        ("metal pipe", "Scrap_MetalPipe.prefab"),
        ("metal_pipe", "Scrap_MetalPipe.prefab"),  // alias
        ("battery",    "Scrap_Battery.prefab"),
    };

    [MenuItem("Tools/Scrap Setup/Copy Scraps to Resources")]
    public static void CopyScrapsToResources()
    {
        // 1. Ensure destination folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(DestFolder))
            AssetDatabase.CreateFolder("Assets/Resources", "Scraps");

        int copied  = 0;
        int skipped = 0;

        foreach (var (key, srcFile) in ScrapMap)
        {
            string srcPath  = $"{SrcFolder}/{srcFile}";
            // Sanitise key for filename: replace space with underscore
            string safeKey  = key.Replace(" ", "_");
            string destPath = $"{DestFolder}/{safeKey}.prefab";

            if (!File.Exists(srcPath))
            {
                Debug.LogWarning($"[ScrapSetup] Source prefab not found: {srcPath}");
                continue;
            }

            if (File.Exists(destPath))
            {
                Debug.Log($"[ScrapSetup] Already exists, skipping: {destPath}");
                skipped++;
                continue;
            }

            bool ok = AssetDatabase.CopyAsset(srcPath, destPath);
            if (ok)
            {
                Debug.Log($"[ScrapSetup] Copied '{srcFile}' → '{destPath}'");
                copied++;
            }
            else
            {
                Debug.LogError($"[ScrapSetup] Failed to copy '{srcPath}' → '{destPath}'");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Scrap Resources Setup",
            $"Done!\n\nCopied:  {copied}\nSkipped: {skipped}\n\n" +
            $"Prefabs are now in '{DestFolder}'.\n" +
            "ScrapScatterer will load them at runtime automatically.",
            "OK");
    }

    [MenuItem("Tools/Scrap Setup/Verify Resources Folder")]
    public static void VerifyResourcesFolder()
    {
        bool ok = true;
        string report = "Resources/Scraps status:\n\n";

        foreach (var (key, _) in ScrapMap)
        {
            string safeKey = key.Replace(" ", "_");
            string path    = $"{DestFolder}/{safeKey}.prefab";
            bool exists    = File.Exists(path);
            report += $"  [{(exists ? "✓" : "✗")}] Scraps/{safeKey}\n";
            if (!exists) ok = false;
        }

        report += ok ? "\nAll prefabs found! ✓" : "\n⚠ Some prefabs missing. Run 'Copy Scraps to Resources'.";
        Debug.Log("[ScrapSetup] " + report);
        EditorUtility.DisplayDialog("Verify Resources", report, "OK");
    }
}
