#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Small editor utility for recovering the Waiting scene after Unity reports
/// WaitingRoom.blend as a missing prefab. The scene itself is intentionally not
/// rewritten: importing the source asset is enough for Unity to reconnect the
/// existing prefab instance and preserve its overrides/components.
/// </summary>
public static class WaitingSceneRepair
{
    private const string WaitingRoomBlend = "Assets/Models/Space_Station_Kit/WaitingRoom.blend";
    private const string WaitingScene = "Assets/Scenes/Waiting.unity";

    [MenuItem("Tools/Mimeto/Waiting/Diagnose Waiting Scene")]
    public static void Diagnose()
    {
        bool sourceExists = File.Exists(WaitingRoomBlend);
        bool sceneExists = File.Exists(WaitingScene);
        var importedAsset = AssetDatabase.LoadAssetAtPath<GameObject>(WaitingRoomBlend);

        Debug.Log($"[Mimeto] Waiting scene: {(sceneExists ? "found" : "MISSING")} ({WaitingScene})");
        Debug.Log($"[Mimeto] WaitingRoom.blend: {(sourceExists ? "found" : "MISSING")} ({WaitingRoomBlend})");

        if (importedAsset == null)
        {
            Debug.LogWarning(
                "[Mimeto] WaitingRoom.blend is not imported. " +
                "Install/associate Blender, then run Tools > Mimeto > Waiting > Repair Waiting Room Import.");
            return;
        }

        Debug.Log($"[Mimeto] WaitingRoom imported successfully: {importedAsset.name}");
    }

    [MenuItem("Tools/Mimeto/Waiting/Repair Waiting Room Import")]
    public static void RepairImport()
    {
        if (!File.Exists(WaitingRoomBlend))
        {
            Debug.LogError($"[Mimeto] Cannot repair: missing source asset {WaitingRoomBlend}");
            return;
        }

        // Force Unity to invoke the Blender model importer again. Once the
        // source is imported, the existing Waiting scene prefab GUID resolves.
        AssetDatabase.ImportAsset(WaitingRoomBlend, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        var importedAsset = AssetDatabase.LoadAssetAtPath<GameObject>(WaitingRoomBlend);
        if (importedAsset == null)
        {
            Debug.LogError(
                "[Mimeto] WaitingRoom.blend is still not imported. " +
                "Check Blender's file association and the Console import error.");
            return;
        }

        Debug.Log(
            "[Mimeto] WaitingRoom import repaired. Reopen Waiting.unity and verify the room prefab is visible.");
    }
}
#endif
