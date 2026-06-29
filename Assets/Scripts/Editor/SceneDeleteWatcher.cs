#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Intercepts ALL asset deletion attempts in the Editor and blocks deletion
/// of any scene listed in ProtectedScenes — including drag-to-trash,
/// Delete key in Project window, and AssetDatabase.DeleteAsset() calls.
///
/// Also prevents the protected scenes from being removed from Build Settings.
/// </summary>
[InitializeOnLoad]
public class SceneDeleteWatcher : UnityEditor.AssetModificationProcessor
{
    // ── Protected scenes — add any scene path you never want deleted ──────────
    private static readonly HashSet<string> ProtectedScenes = new HashSet<string>
    {
        "Assets/Scenes/Map.unity",
    };

    // ─────────────────────────────────────────────────────────────────────────

    static SceneDeleteWatcher()
    {
        // Hook into Build Settings changes so Map can't be silently removed
        EditorBuildSettings.sceneListChanged += OnBuildScenesChanged;
        Debug.Log("[SceneDeleteWatcher] Active — Map.unity is protected from deletion.");
    }

    // ── AssetModificationProcessor hook ──────────────────────────────────────

    /// <summary>
    /// Called by Unity BEFORE any asset is deleted.
    /// Return AssetDeleteResult.DidNotDelete to block, FailedDelete to abort with error.
    /// </summary>
    public static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
    {
        if (!ProtectedScenes.Contains(assetPath))
            return AssetDeleteResult.DidNotDelete; // Not protected — allow normally

        // ── Block the deletion ────────────────────────────────────────────────
        string fileName = System.IO.Path.GetFileName(assetPath);

        bool force = EditorUtility.DisplayDialog(
            $"🔒 Protected Scene: {fileName}",
            $"'{assetPath}' is a PROTECTED scene and cannot be deleted normally.\n\n" +
            "This scene is the main game scene. Deleting it will break the project.\n\n" +
            "If you are absolutely sure you want to delete it, use:\n" +
            "  Tools → Mimeto → 🔓 Force Delete Protected Scene\n\n" +
            "Otherwise, press Cancel to keep the file safe.",
            "Cancel (Keep Safe)",
            "Delete Anyway (Dangerous!)");

        if (force)
        {
            // User chose "Delete Anyway" — warn once more
            bool reallyForce = EditorUtility.DisplayDialog(
                "⚠️ FINAL WARNING",
                $"You are about to permanently delete:\n\n  {assetPath}\n\n" +
                "This CANNOT be undone. Are you 100% sure?",
                "Cancel", "Yes, Delete It");

            if (reallyForce) // "Cancel" = index 0 = keep safe
                return AssetDeleteResult.DidNotDelete; // Block

            // They clicked "Yes, Delete It" — allow
            Debug.LogWarning($"[SceneDeleteWatcher] Force-deleted protected scene: {assetPath}");
            return AssetDeleteResult.DidNotDelete; // Let Unity proceed after this
        }

        // User chose "Cancel (Keep Safe)" — block deletion
        Debug.Log($"[SceneDeleteWatcher] 🛡 Blocked deletion of protected scene: {assetPath}");
        return AssetDeleteResult.FailedDelete; // Hard block
    }

    // ── Build Settings hook ───────────────────────────────────────────────────

    private static void OnBuildScenesChanged()
    {
        var buildScenes  = EditorBuildSettings.scenes.ToList();
        var missingPaths = new List<string>();

        foreach (string protectedPath in ProtectedScenes)
        {
            bool found = buildScenes.Any(s => s.path == protectedPath);
            if (!found) missingPaths.Add(protectedPath);
        }

        if (missingPaths.Count == 0) return;

        // ── A protected scene was removed from Build Settings — restore it ────
        foreach (string missing in missingPaths)
        {
            Debug.LogWarning(
                $"[SceneDeleteWatcher] 🛡 '{missing}' was removed from Build Settings. Restoring it at index 0.");

            buildScenes.RemoveAll(s => s.path == missing);
            buildScenes.Insert(0, new EditorBuildSettingsScene(missing, true));
        }

        // Suppress the loop: unsubscribe, apply, resubscribe
        EditorBuildSettings.sceneListChanged -= OnBuildScenesChanged;
        EditorBuildSettings.scenes = buildScenes.ToArray();
        EditorBuildSettings.sceneListChanged += OnBuildScenesChanged;
    }

    // ── Force-delete escape hatch (for advanced users only) ──────────────────

    [MenuItem("Tools/Mimeto/🔓 Force Delete Protected Scene (Dangerous)", priority = 30)]
    public static void ForceDeleteProtectedScene()
    {
        string path = EditorUtility.OpenFilePanel(
            "Select Protected Scene to Force-Delete",
            "Assets/Scenes", "unity");

        if (string.IsNullOrEmpty(path)) return;

        // Convert absolute path to relative project path
        string projectPath = Application.dataPath.Replace("/Assets", "");
        string relativePath = path.Replace(projectPath + "/", "").Replace("\\", "/");

        if (!ProtectedScenes.Contains(relativePath))
        {
            EditorUtility.DisplayDialog("Not Protected",
                $"'{relativePath}' is not in the protected list. Delete it normally.", "OK");
            return;
        }

        bool confirm = EditorUtility.DisplayDialog(
            "⚠️ Force Delete — FINAL WARNING",
            $"You are about to PERMANENTLY delete the protected scene:\n\n  {relativePath}\n\n" +
            "This will break your project if this is the main scene.\n" +
            "There is NO undo.\n\nAre you absolutely sure?",
            "No, Keep It Safe",
            "Yes, Delete Permanently");

        if (confirm) return; // "No" = index 0 = safe

        // Close the scene first if open
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            if (EditorSceneManager.GetSceneAt(i).path == relativePath)
            {
                EditorSceneManager.CloseScene(EditorSceneManager.GetSceneAt(i), false);
                break;
            }
        }

        // Temporarily unsubscribe Build Settings watcher to avoid restore loop
        EditorBuildSettings.sceneListChanged -= OnBuildScenesChanged;
        bool ok = AssetDatabase.DeleteAsset(relativePath);
        EditorBuildSettings.sceneListChanged += OnBuildScenesChanged;

        AssetDatabase.Refresh();

        if (ok)
        {
            Debug.LogWarning($"[SceneDeleteWatcher] Force-deleted: {relativePath}");
            EditorUtility.DisplayDialog("Deleted", $"'{relativePath}' has been deleted.", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Failed", $"Could not delete '{relativePath}'.", "OK");
        }
    }

    // ── Inspector utility: show protection status ─────────────────────────────

    [MenuItem("Tools/Mimeto/Show Protected Scenes", priority = 31)]
    public static void ShowProtectedScenes()
    {
        string list = string.Join("\n  • ", ProtectedScenes);
        EditorUtility.DisplayDialog(
            "🔒 Protected Scenes",
            $"The following scenes are protected from deletion:\n\n  • {list}\n\n" +
            "To add more, edit SceneDeleteWatcher.cs → ProtectedScenes.",
            "OK");
    }
}
#endif
