#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Automatically removes BoxColliders from all tree objects in the scene.
/// Uses the same tree-detection logic as ColliderAutoSetup.
/// Runs once automatically on compile, then never again unless reset.
/// Re-run: Tools → Mimeto → 🌳 Re-Run Remove Tree Colliders
/// </summary>
[InitializeOnLoad]
public static class RemoveTreeColliders
{
    private const string DoneKey = "Mimeto_RemoveTreeColliders_Done_v2";

    private static readonly string[] TreeKeywords =
    {
        "tree", "cây", "cay", "palm", "pine", "oak", "birch",
        "spruce", "cedar", "willow", "bamboo", "trunk", "thantree",
    };

    static RemoveTreeColliders()
    {
        if (EditorPrefs.GetBool(DoneKey, false)) return;
        EditorApplication.delayCall += Run;
    }

    [MenuItem("Tools/Mimeto/🌳 Re-Run Remove Tree Colliders", priority = 55)]
    public static void ReRun()
    {
        EditorPrefs.DeleteKey(DoneKey);
        Run();
    }

    public static void Run()
    {
        EditorApplication.delayCall -= Run;

        // Tìm tất cả root GameObject — kiểm tra cả hierarchy
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        Undo.SetCurrentGroupName("Remove Tree Colliders");
        int group = Undo.GetCurrentGroup();

        int removed = 0;
        var processedRoots = new HashSet<int>();

        foreach (GameObject go in allObjects)
        {
            // Chỉ xử lý root của cây (tránh xử lý cùng cây nhiều lần)
            if (!IsTreeRoot(go)) continue;

            int rootId = go.GetInstanceID();
            if (processedRoots.Contains(rootId)) continue;
            processedRoots.Add(rootId);

            // Xóa Collider trên root VÀ toàn bộ children
            Collider[] allCols = go.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in allCols)
            {
                Undo.DestroyObjectImmediate(c);
                removed++;
            }

            if (allCols.Length > 0)
                EditorUtility.SetDirty(go);
        }

        Undo.CollapseUndoOperations(group);

        // Lưu scene
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var scene = EditorSceneManager.GetSceneAt(i);
            if (scene.isDirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        AssetDatabase.SaveAssets();
        EditorPrefs.SetBool(DoneKey, true);

        string msg = removed > 0
            ? $"Đã xóa {removed} Collider khỏi các object cây.\n\nScene đã lưu. Ctrl+Z để hoàn tác."
            : "Không tìm thấy Collider nào trên cây.";

        Debug.Log($"[RemoveTreeColliders] ✅ {msg}");
        EditorUtility.DisplayDialog("Xóa Collider Cây ✅", msg, "OK");
    }

    /// <summary>
    /// Trả về true nếu object NÀY là ROOT của cây
    /// (không phải child — tránh xử lý trùng lặp).
    /// </summary>
    private static bool IsTreeRoot(GameObject go)
    {
        // Bản thân object phải là cây
        if (!IsTree(go)) return false;

        // Nếu parent cũng là cây thì đây là child — bỏ qua
        if (go.transform.parent != null && IsTree(go.transform.parent.gameObject))
            return false;

        return true;
    }

    private static bool IsTree(GameObject go)
    {
        if (go.GetComponent<Tree>() != null) return true;
        if (go.CompareTag("Tree")) return true;

        Transform t = go.transform;
        while (t != null)
        {
            string lower = t.name.ToLowerInvariant();
            foreach (string kw in TreeKeywords)
                if (lower.Contains(kw)) return true;
            t = t.parent;
        }
        return false;
    }
}
#endif
