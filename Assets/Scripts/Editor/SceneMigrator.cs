#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Editor tool: moves ALL GameObjects from a source scene into the Map scene.
///
/// Usage: Unity menu → Tools → Mimeto → Move Scene 0 → Map
///
/// What it does:
///   1. Saves all unsaved changes
///   2. Opens the source scene additively (so both scenes are loaded at once)
///   3. Moves every root GameObject from the source scene into Map.unity
///   4. Removes the source scene from Build Settings and replaces it with Map.unity
///   5. Saves Map.unity
/// </summary>
public static class SceneMigrator
{
    // ── Paths — change these if your scene paths differ ────────────────────
    private const string SourceScenePath = "Assets/Scenes/MimetoStation.unity";
    private const string TargetScenePath = "Assets/Scenes/Map.unity";
    // ───────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Mimeto/Migrate — Move MimetoStation → Map", priority = 10)]
    public static void MigrateToMap()
    {
        // ── Step 0: confirm ─────────────────────────────────────────────────
        bool go = EditorUtility.DisplayDialog(
            "Migrate Scene Contents",
            $"This will move ALL GameObjects from:\n\n" +
            $"  {SourceScenePath}\n\ninto:\n\n  {TargetScenePath}\n\n" +
            "• Both scenes must exist.\n" +
            "• All unsaved changes will be saved first.\n" +
            "• The source scene file itself is NOT deleted (only its objects are moved).\n\n" +
            "Proceed?",
            "Yes, Move Everything", "Cancel");

        if (!go) return;

        // ── Step 1: save everything currently open ───────────────────────────
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.Log("[SceneMigrator] Cancelled — unsaved changes were not saved.");
            return;
        }

        // ── Step 2: validate paths ───────────────────────────────────────────
        if (!AssetExists(SourceScenePath))
        {
            EditorUtility.DisplayDialog("Error",
                $"Source scene not found:\n{SourceScenePath}", "OK");
            return;
        }
        if (!AssetExists(TargetScenePath))
        {
            EditorUtility.DisplayDialog("Error",
                $"Target scene not found:\n{TargetScenePath}", "OK");
            return;
        }

        // ── Step 3: open target scene (single) ──────────────────────────────
        Scene targetScene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

        // ── Step 4: open source scene additively ────────────────────────────
        Scene sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);

        // ── Step 5: collect all root objects from source ─────────────────────
        GameObject[] sourceRoots = sourceScene.GetRootGameObjects();

        if (sourceRoots.Length == 0)
        {
            EditorUtility.DisplayDialog("Nothing to Move",
                $"The source scene '{SourceScenePath}' has no GameObjects.", "OK");
            EditorSceneManager.CloseScene(sourceScene, false);
            return;
        }

        // ── Step 6: filter out objects that already exist by name in target ──
        GameObject[] targetRoots = targetScene.GetRootGameObjects();
        HashSet<string> existingNames = new HashSet<string>(targetRoots.Select(g => g.name));

        var toMove   = new List<GameObject>();
        var skipped  = new List<string>();

        foreach (GameObject root in sourceRoots)
        {
            if (existingNames.Contains(root.name))
            {
                skipped.Add(root.name);
            }
            else
            {
                toMove.Add(root);
            }
        }

        // Ask user what to do with duplicates
        if (skipped.Count > 0)
        {
            string dupList = string.Join("\n  • ", skipped);
            int choice = EditorUtility.DisplayDialogComplex(
                "Duplicate Names Detected",
                $"These GameObjects already exist in the target scene:\n\n  • {dupList}\n\n" +
                "What should happen to the duplicates?",
                "Move Anyway (rename with _OLD suffix)",
                "Skip Duplicates",
                "Cancel Migration");

            if (choice == 2) // Cancel
            {
                EditorSceneManager.CloseScene(sourceScene, false);
                return;
            }

            if (choice == 0) // Move anyway — rename existing in target
            {
                foreach (GameObject root in targetRoots)
                {
                    if (skipped.Contains(root.name))
                        root.name = root.name + "_OLD";
                }
                toMove.AddRange(sourceRoots.Where(g => skipped.Contains(g.name)));
            }
            // choice == 1: skip — toMove already excludes them
        }

        // ── Step 7: actually move ─────────────────────────────────────────────
        int movedCount = 0;
        foreach (GameObject obj in toMove)
        {
            SceneManager.MoveGameObjectToScene(obj, targetScene);
            movedCount++;
        }

        // ── Step 8: close source scene (don't save — objects are gone from it) ─
        EditorSceneManager.CloseScene(sourceScene, false);

        // ── Step 9: update Build Settings ────────────────────────────────────
        UpdateBuildSettings();

        // ── Step 10: save target scene ────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(targetScene);
        EditorSceneManager.SaveScene(targetScene);

        // ── Done ──────────────────────────────────────────────────────────────
        string skipMsg = skipped.Count > 0
            ? $"\n\nSkipped {skipped.Count} duplicate(s): {string.Join(", ", skipped)}"
            : "";

        Debug.Log($"[SceneMigrator] ✅ Moved {movedCount} GameObjects from '{SourceScenePath}' to '{TargetScenePath}'.{skipMsg}");

        EditorUtility.DisplayDialog(
            "Migration Complete ✅",
            $"Moved {movedCount} GameObject(s) into:\n{TargetScenePath}\n\n" +
            $"Build Settings updated — Map is now Scene 0.{skipMsg}",
            "OK");
    }

    // ── Build Settings helper ─────────────────────────────────────────────────

    private static void UpdateBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        // Remove source scene from build list
        scenes.RemoveAll(s => s.path == SourceScenePath);

        // Add target scene at index 0 if not already present
        bool targetExists = scenes.Any(s => s.path == TargetScenePath);
        if (!targetExists)
        {
            scenes.Insert(0, new EditorBuildSettingsScene(TargetScenePath, true));
        }
        else
        {
            // Make sure Map is at index 0
            var mapEntry = scenes.First(s => s.path == TargetScenePath);
            scenes.Remove(mapEntry);
            mapEntry = new EditorBuildSettingsScene(TargetScenePath, true);
            scenes.Insert(0, mapEntry);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[SceneMigrator] Build Settings updated: Map.unity is now Scene 0.");
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static bool AssetExists(string path)
        => !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path));

    // ── Also expose a quick "just update build settings" menu item ─────────────

    [MenuItem("Tools/Mimeto/Fix Build Settings (Map = Scene 0)", priority = 11)]
    public static void FixBuildSettingsOnly()
    {
        UpdateBuildSettings();
        EditorUtility.DisplayDialog("Done", "Build Settings updated.\nMap.unity is now Scene 0.", "OK");
    }

    // ── Delete old scenes ─────────────────────────────────────────────────────

    // Paths of scenes to delete
    private static readonly string[] ScenesToDelete = new[]
    {
        "Assets/Scenes/SampleScene.unity",
        "Assets/Scenes/MimetoStation.unity",
    };

    [MenuItem("Tools/Mimeto/🗑 Delete Old Scenes (SampleScene + MimetoStation)", priority = 20)]
    public static void DeleteOldScenes()
    {
        // ── Collect which files actually exist ───────────────────────────────
        var toDelete = new List<string>();
        foreach (string path in ScenesToDelete)
        {
            if (AssetExists(path))
                toDelete.Add(path);
        }

        if (toDelete.Count == 0)
        {
            EditorUtility.DisplayDialog("Nothing to Delete",
                "Neither SampleScene.unity nor MimetoStation.unity were found in the project.\n" +
                "They may have already been deleted.", "OK");
            return;
        }

        // ── Safety: make sure Map.unity is NOT in the delete list ────────────
        toDelete.RemoveAll(p => p == TargetScenePath);

        // ── Confirm with user ─────────────────────────────────────────────────
        string fileList = string.Join("\n  • ", toDelete);
        bool confirm = EditorUtility.DisplayDialog(
            "⚠️ Delete Scenes — Cannot Be Undone",
            $"The following scene files will be permanently deleted:\n\n  • {fileList}\n\n" +
            "• These scenes will also be removed from Build Settings.\n" +
            "• This action CANNOT be undone.\n\n" +
            "Make sure you have already migrated everything to Map.unity before continuing.",
            "Yes, Delete Permanently", "Cancel");

        if (!confirm) return;

        // ── Close any of these scenes if currently open ───────────────────────
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            Scene openScene = EditorSceneManager.GetSceneAt(i);
            if (toDelete.Contains(openScene.path))
            {
                // Don't save — we're deleting it
                EditorSceneManager.CloseScene(openScene, false);
            }
        }

        // ── Remove from Build Settings ────────────────────────────────────────
        var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        buildScenes.RemoveAll(s => toDelete.Contains(s.path));
        EditorBuildSettings.scenes = buildScenes.ToArray();

        // ── Delete the asset files ────────────────────────────────────────────
        var deleted  = new List<string>();
        var failed   = new List<string>();

        foreach (string path in toDelete)
        {
            bool ok = AssetDatabase.DeleteAsset(path);
            if (ok) deleted.Add(path);
            else    failed.Add(path);
        }

        AssetDatabase.Refresh();

        // ── Report ────────────────────────────────────────────────────────────
        string result = $"Deleted {deleted.Count} scene(s):\n  • " +
                        string.Join("\n  • ", deleted.Select(System.IO.Path.GetFileName));

        if (failed.Count > 0)
            result += $"\n\nFailed to delete {failed.Count}:\n  • " +
                      string.Join("\n  • ", failed.Select(System.IO.Path.GetFileName));

        Debug.Log($"[SceneMigrator] 🗑 {result}");
        EditorUtility.DisplayDialog(
            failed.Count == 0 ? "Deleted ✅" : "Partially Deleted ⚠️",
            result, "OK");
    }
}
#endif
