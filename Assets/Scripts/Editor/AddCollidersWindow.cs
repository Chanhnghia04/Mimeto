#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor window that finds every MeshRenderer in the scene without a Collider
/// and adds a BoxCollider fitted exactly to the mesh bounds.
///
/// Open via: Tools → Mimeto → 📦 Add Box Colliders to All Objects
/// </summary>
public class AddCollidersWindow : EditorWindow
{
    // ── Settings ──────────────────────────────────────────────────────────────
    private bool _skipIfHasAnyCollider = true;   // Don't touch objects already collided
    private bool _skipTerrain          = true;
    private bool _skipParticles        = true;
    private bool _skipUI               = true;
    private bool _skipInactive         = false;  // Include inactive GameObjects
    private bool _skipSpawnPoints      = true;
    private bool _fitToMeshBounds      = true;   // size = mesh.bounds.size (vs default unit)
    private bool _addToChildMeshes     = true;   // Add collider on each child mesh separately
    private bool _onlySelected         = false;

    // ── Preview state ─────────────────────────────────────────────────────────
    private List<ColliderCandidate> _candidates = new List<ColliderCandidate>();
    private Vector2 _scroll;
    private bool    _previewDone;
    private int     _skippedCount;
    private int     _alreadyHasCount;

    // ── Styles ────────────────────────────────────────────────────────────────
    private GUIStyle _headerStyle;
    private bool     _stylesInit;

    private class ColliderCandidate
    {
        public GameObject Go;
        public MeshFilter MF;         // mesh whose bounds will be used
        public Bounds     LocalBounds; // mesh.bounds in local space
        public bool       AlreadyHasCollider;
        public string     WhySkipped;  // non-null = will be skipped
    }

    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Mimeto/📦 Add Box Colliders to All Objects", priority = 61)]
    public static void OpenWindow()
    {
        var win = GetWindow<AddCollidersWindow>("Add Colliders");
        win.minSize = new Vector2(440, 560);
        win.Show();
    }

    private void OnGUI()
    {
        InitStyles();

        // ── Header ───────────────────────────────────────────────────────────
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("📦 Add Box Colliders to All Objects", _headerStyle);
        EditorGUILayout.LabelField(
            "Finds every MeshRenderer without a Collider and adds a BoxCollider " +
            "fitted to the mesh bounds.",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(8);

        // ── Options ───────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Scope", EditorStyles.boldLabel);
        DrawLine();
        _onlySelected      = EditorGUILayout.Toggle("Only Selected Objects", _onlySelected);
        _skipInactive      = EditorGUILayout.Toggle("Include Inactive Objects", _skipInactive);
        _addToChildMeshes  = EditorGUILayout.Toggle(
            new GUIContent("Per-Child Mesh",
                "Add a BoxCollider on each child GameObject that has a MeshRenderer. " +
                "Recommended — gives the tightest fit for complex models."),
            _addToChildMeshes);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Collider Fitting", EditorStyles.boldLabel);
        DrawLine();
        _fitToMeshBounds = EditorGUILayout.Toggle(
            new GUIContent("Fit to Mesh Bounds",
                "Sets BoxCollider.center and .size from the mesh's local-space bounds. " +
                "If off, the default unit BoxCollider is added."),
            _fitToMeshBounds);
        _skipIfHasAnyCollider = EditorGUILayout.Toggle(
            new GUIContent("Skip If Already Has Collider",
                "Don't overwrite GameObjects that already have any type of Collider."),
            _skipIfHasAnyCollider);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Skip These Types", EditorStyles.boldLabel);
        DrawLine();
        _skipTerrain     = EditorGUILayout.Toggle("Terrain (has TerrainCollider)", _skipTerrain);
        _skipParticles   = EditorGUILayout.Toggle("Particle Systems", _skipParticles);
        _skipUI          = EditorGUILayout.Toggle("UI Canvas / Graphic", _skipUI);
        _skipSpawnPoints = EditorGUILayout.Toggle("MimicSpawnPoints / MimicSpawner", _skipSpawnPoints);

        EditorGUILayout.Space(10);

        // ── Buttons ───────────────────────────────────────────────────────────
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("🔍 Preview", GUILayout.Height(36)))
                RunPreview();

            GUI.enabled = _previewDone && _candidates.Any(c => c.WhySkipped == null);
            if (GUILayout.Button("✅ Apply", GUILayout.Height(36)))
                ApplyColliders();
            GUI.enabled = true;
        }

        if (!_previewDone) return;

        // ── Summary bar ───────────────────────────────────────────────────────
        int toAdd    = _candidates.Count(c => c.WhySkipped == null);
        int skipped  = _candidates.Count(c => c.WhySkipped != null);

        EditorGUILayout.Space(6);
        DrawLine();

        Color prev = GUI.color;
        GUI.color = new Color(0.6f, 1f, 0.7f);
        EditorGUILayout.LabelField(
            $"Preview — Will add collider: {toAdd}   |   Skipped: {skipped}",
            EditorStyles.boldLabel);
        GUI.color = prev;

        // ── Scrollable list ───────────────────────────────────────────────────
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

        // Show will-add first, then skipped
        var ordered = _candidates
            .OrderBy(c => c.WhySkipped != null)   // nulls (to-add) first
            .ThenBy(c => c.Go.name)
            .ToList();

        string lastCategory = "";
        foreach (var c in ordered)
        {
            bool willAdd = c.WhySkipped == null;
            string category = willAdd ? "✅ Will Add Collider" : "⬛ Skipped";

            if (category != lastCategory)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(category, EditorStyles.boldLabel);
                lastCategory = category;
            }

            GUI.color = willAdd ? new Color(0.6f, 1f, 0.6f) : new Color(0.75f, 0.75f, 0.75f);
            using (new EditorGUILayout.HorizontalScope("box"))
            {
                GUI.color = prev;

                // Clickable name
                if (GUILayout.Button(c.Go.name,
                    EditorStyles.linkLabel, GUILayout.Width(190)))
                {
                    Selection.activeGameObject = c.Go;
                    SceneView.FrameLastActiveSceneView();
                }

                if (willAdd)
                {
                    string sizeStr = c.MF != null
                        ? $"size ({c.LocalBounds.size.x:F2}, {c.LocalBounds.size.y:F2}, {c.LocalBounds.size.z:F2})"
                        : "bounds from renderers";
                    GUILayout.Label(sizeStr, GUILayout.ExpandWidth(true));
                }
                else
                {
                    GUILayout.Label(c.WhySkipped, EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                }
            }
            GUI.color = prev;
        }

        EditorGUILayout.EndScrollView();
    }

    // ── Preview ───────────────────────────────────────────────────────────────

    private void RunPreview()
    {
        _candidates.Clear();
        _skippedCount     = 0;
        _alreadyHasCount  = 0;
        _previewDone      = false;

        // Collect candidate GameObjects
        IEnumerable<GameObject> pool;
        if (_onlySelected)
        {
            // All selected + their children
            pool = Selection.gameObjects
                .SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)
                    .Select(r => r.gameObject))
                .Distinct();
        }
        else
        {
            FindObjectsInactive inactive = _skipInactive
                ? FindObjectsInactive.Exclude
                : FindObjectsInactive.Include;

            pool = Object.FindObjectsByType<MeshRenderer>(inactive)
                         .Select(r => r.gameObject);
        }

        // If per-child mode is OFF, use only root GameObjects
        if (!_addToChildMeshes)
        {
            pool = pool
                .Select(go => go.transform.root.gameObject)
                .Distinct();
        }

        foreach (GameObject go in pool)
        {
            string skip = GetSkipReason(go);

            MeshFilter mf = go.GetComponent<MeshFilter>();
            Bounds localBounds = default;
            if (mf != null && mf.sharedMesh != null)
                localBounds = mf.sharedMesh.bounds;

            _candidates.Add(new ColliderCandidate
            {
                Go            = go,
                MF            = mf,
                LocalBounds   = localBounds,
                AlreadyHasCollider = go.GetComponent<Collider>() != null,
                WhySkipped    = skip
            });
        }

        _previewDone = true;
        Repaint();
    }

    private string GetSkipReason(GameObject go)
    {
        if (go == null) return "null";

        if (_skipTerrain && go.GetComponent<Terrain>() != null)
            return "Terrain";

        if (_skipParticles && go.GetComponent<ParticleSystem>() != null)
            return "ParticleSystem";

        if (_skipUI && (go.GetComponent<Canvas>() != null || go.GetComponent<UnityEngine.UI.Graphic>() != null))
            return "UI Element";

        if (_skipSpawnPoints &&
            (go.GetComponent<MimicSpawnPoint>() != null || go.GetComponent<MimicSpawner>() != null))
            return "Mimic system object";

        if (_skipIfHasAnyCollider && go.GetComponent<Collider>() != null)
            return "Already has collider";

        MeshFilter mf = go.GetComponent<MeshFilter>();
        MeshRenderer mr = go.GetComponent<MeshRenderer>();

        if (mr == null) return "No MeshRenderer";
        if (mf == null || mf.sharedMesh == null) return "No mesh";

        return null; // null = will add
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    private void ApplyColliders()
    {
        var toProcess = _candidates.Where(c => c.WhySkipped == null).ToList();
        if (toProcess.Count == 0) return;

        Undo.SetCurrentGroupName("Add Box Colliders");
        int group = Undo.GetCurrentGroup();

        int added   = 0;
        int failed  = 0;

        foreach (var c in toProcess)
        {
            if (c.Go == null) continue;

            // Re-check in case the scene changed between preview and apply
            if (_skipIfHasAnyCollider && c.Go.GetComponent<Collider>() != null)
                continue;

            Undo.RecordObject(c.Go, "Add BoxCollider");

            BoxCollider bc = Undo.AddComponent<BoxCollider>(c.Go);
            if (bc == null) { failed++; continue; }

            if (_fitToMeshBounds)
            {
                MeshFilter mf = c.Go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    // Local-space bounds — perfect fit
                    bc.center = mf.sharedMesh.bounds.center;
                    bc.size   = mf.sharedMesh.bounds.size;
                }
                else
                {
                    // Fallback: world bounds → local space
                    Renderer[] renderers = c.Go.GetComponentsInChildren<Renderer>();
                    if (renderers.Length > 0)
                    {
                        Bounds world = renderers[0].bounds;
                        foreach (var r in renderers) world.Encapsulate(r.bounds);

                        bc.center = c.Go.transform.InverseTransformPoint(world.center);
                        Vector3 s = c.Go.transform.InverseTransformVector(world.size);
                        bc.size   = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                    }
                }
            }

            EditorUtility.SetDirty(c.Go);
            added++;
        }

        Undo.CollapseUndoOperations(group);

        // Save scene
        Scene scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[AddColliders] ✅ Added BoxCollider to {added} objects. " +
                  (failed > 0 ? $"{failed} failed. " : "") +
                  "Scene saved. Ctrl+Z to undo all.");

        EditorUtility.DisplayDialog(
            "Done ✅",
            $"Added BoxCollider to {added} object(s).\n\n" +
            (failed > 0 ? $"⚠ {failed} failed (check Console).\n\n" : "") +
            "Scene saved. Press Ctrl+Z to undo everything at once.",
            "OK");

        RunPreview(); // refresh list
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private void InitStyles()
    {
        if (_stylesInit) return;
        _headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 14,
            alignment = TextAnchor.MiddleLeft
        };
        _stylesInit = true;
    }

    private static void DrawLine()
    {
        var rect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f));
        EditorGUILayout.Space(2);
    }
}
#endif
