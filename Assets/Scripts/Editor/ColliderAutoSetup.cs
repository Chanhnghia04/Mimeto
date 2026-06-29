#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Automatically adds a BoxCollider (fitted to mesh bounds) to every
/// MeshRenderer in the scene that does not already have a Collider.
///
/// Special rule — TREES:
///   Only the lower trunk portion gets a collider (not the canopy/leaves).
///   Trunk uses a FIXED diameter so grouped trees (parent + children)
///   never get a collider that's as wide as the whole canopy.
///
///   TrunkDiameter   — width & depth of trunk collider in Unity units.
///   TrunkHeightRatio — trunk height as fraction of full mesh height.
///
/// Runs once automatically on the next compile.
/// Re-run: Tools → Mimeto → 🔁 Re-Run Add Colliders
/// Reset:  Tools → Mimeto → ⚙ Reset Add Colliders Flag
/// </summary>
[InitializeOnLoad]
public static class ColliderAutoSetup
{
    private const string DoneKey = "Mimeto_ColliderSetup_Done_v3";

    // ── Tree trunk settings ───────────────────────────────────────────────────
    /// <summary>
    /// Fixed width AND depth of the trunk BoxCollider in Unity units.
    /// Does NOT scale with canopy — prevents the "too wide" problem on grouped trees.
    /// Increase for large trees, decrease for thin ones.
    /// </summary>
    private const float TrunkDiameter   = 0.45f;

    /// <summary>Trunk height as a fraction of the individual mesh height (0–1).</summary>
    private const float TrunkHeightRatio = 0.35f;

    // ── Keywords used to identify tree objects (case-insensitive) ────────────
    private static readonly string[] TreeKeywords =
    {
        "tree", "cây", "cay", "palm", "pine", "oak", "birch",
        "spruce", "cedar", "willow", "bamboo", "trunk", "thantree",
    };

    // ── Types to skip entirely ────────────────────────────────────────────────
    private static readonly System.Type[] SkipComponents =
    {
        typeof(Terrain),
        typeof(ParticleSystem),
        typeof(Canvas),
        typeof(UnityEngine.UI.Graphic),
        typeof(Camera),
        typeof(MimicSpawnPoint),
        typeof(MimicSpawner),
        typeof(MimicAI),
    };

    // ─────────────────────────────────────────────────────────────────────────

    static ColliderAutoSetup()
    {
        if (EditorPrefs.GetBool(DoneKey, false)) return;
        EditorApplication.delayCall += Run;
    }

    // ── Menu items ────────────────────────────────────────────────────────────

    [MenuItem("Tools/Mimeto/🔁 Re-Run Add Colliders", priority = 52)]
    public static void ReRun()
    {
        EditorPrefs.DeleteKey(DoneKey);
        Run();
    }

    [MenuItem("Tools/Mimeto/⚙ Reset Add Colliders Flag", priority = 53)]
    public static void ResetFlag()
    {
        EditorPrefs.DeleteKey(DoneKey);
        Debug.Log("[ColliderAutoSetup] Flag reset. Will run again on next compile.");
        EditorUtility.DisplayDialog("Reset", "Collider auto-setup will run on next compile.", "OK");
    }

    // ── Core ──────────────────────────────────────────────────────────────────

    public static void Run()
    {
        EditorApplication.delayCall -= Run;
        Debug.Log("[ColliderAutoSetup] ▶ Scanning scene for objects without colliders…");

        MeshRenderer[] allRenderers = Object.FindObjectsByType<MeshRenderer>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        Undo.SetCurrentGroupName("Auto: Add Box Colliders");
        int group = Undo.GetCurrentGroup();

        int addedNormal = 0;
        int addedTree   = 0;
        int skipped     = 0;

        var processed = new HashSet<int>();

        foreach (MeshRenderer mr in allRenderers)
        {
            GameObject go = mr.gameObject;
            int id = go.GetInstanceID();
            if (processed.Contains(id)) continue;
            processed.Add(id);

            // ── Skip checks ───────────────────────────────────────────────────
            if (ShouldSkip(go))            { skipped++; continue; }
            if (go.GetComponent<Collider>() != null) { skipped++; continue; }

            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) { skipped++; continue; }

            Bounds meshBounds = mf.sharedMesh.bounds; // local space

            bool isTree = IsTree(go);

            if (isTree)
            {
                // ── TREE: trunk-only collider ─────────────────────────────────
                // Skip root group objects with no own mesh — children will be
                // processed individually so each trunk gets its own tight collider.
                if (mf == null || mf.sharedMesh == null) { skipped++; continue; }

                Undo.RecordObject(go, "Add BoxCollider");
                BoxCollider bc = Undo.AddComponent<BoxCollider>(go);
                ApplyTrunkCollider(bc, meshBounds);
                addedTree++;
            }
            else
            {
                // ── Normal object: full mesh bounds ───────────────────────────
                Undo.RecordObject(go, "Add BoxCollider");
                BoxCollider bc = Undo.AddComponent<BoxCollider>(go);
                bc.center = meshBounds.center;
                bc.size   = meshBounds.size;
                addedNormal++;
            }

            EditorUtility.SetDirty(go);
        }

        Undo.CollapseUndoOperations(group);

        // Save all dirty scenes
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

        string msg =
            $"Normal objects : {addedNormal} collider(s) added (full mesh bounds)\n" +
            $"Trees          : {addedTree} trunk collider(s) added " +
            $"(bottom {TrunkHeightRatio * 100:0}% height, diameter {TrunkDiameter} units)\n" +
            $"Skipped        : {skipped}\n\n" +
            "Scene saved. Ctrl+Z to undo all.";

        Debug.Log($"[ColliderAutoSetup] ✅\n{msg}");
        EditorUtility.DisplayDialog("Box Colliders Added ✅", msg, "OK");
    }

    // ── Tree collider: bottom trunk only ─────────────────────────────────────

    /// <summary>
    /// Sets the BoxCollider to cover only the lower trunk of a tree.
    /// Width and depth use TrunkDiameter (fixed units) — NOT scaled by canopy
    /// bounds — so grouped trees never produce an oversized collider.
    ///
    ///  Full mesh (may be wide):    Collider region:
    ///  ┌────────────────────┐
    ///  │      (canopy)      │      (no collider here)
    ///  │                    │
    ///  ├────────────────────┤
    ///  │       trunk        │    ┌──────┐
    ///  └────────────────────┘    │ 0.45 │  ← fixed TrunkDiameter
    ///                            └──────┘    height = 35% of mesh height
    /// </summary>
    private static void ApplyTrunkCollider(BoxCollider bc, Bounds b)
    {
        float trunkHeight = b.size.y * TrunkHeightRatio;

        // Fixed diameter — independent of canopy width
        float trunkWidth  = TrunkDiameter;
        float trunkDepth  = TrunkDiameter;

        // Sit at the bottom of the mesh, centred on XZ
        float centerY = b.min.y + (trunkHeight * 0.5f);

        bc.center = new Vector3(b.center.x, centerY, b.center.z);
        bc.size   = new Vector3(trunkWidth, trunkHeight, trunkDepth);
    }

    // ── Tree detection ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the GameObject is considered a tree:
    ///   • Has a Unity Tree component
    ///   • Tag is "Tree"
    ///   • Name or any ancestor's name contains a tree keyword
    /// </summary>
    private static bool IsTree(GameObject go)
    {
        // Unity terrain tree
        if (go.GetComponent<Tree>() != null) return true;

        // Tag
        if (go.CompareTag("Tree")) return true;

        // Walk up the hierarchy checking names
        Transform t = go.transform;
        while (t != null)
        {
            string lower = t.name.ToLowerInvariant();
            foreach (string kw in TreeKeywords)
            {
                if (lower.Contains(kw)) return true;
            }
            t = t.parent;
        }

        return false;
    }

    // ── General skip check ────────────────────────────────────────────────────

    private static bool ShouldSkip(GameObject go)
    {
        foreach (var type in SkipComponents)
            if (go.GetComponent(type) != null) return true;

        string name = go.name;
        if (name.StartsWith("~") || name.StartsWith("__")) return true;

        return false;
    }
}
#endif
