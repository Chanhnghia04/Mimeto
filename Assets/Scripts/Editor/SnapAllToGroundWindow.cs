#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor window that snaps every GameObject in the scene so its mesh
/// bottom sits exactly on the ground surface beneath it.
///
/// Open via: Tools → Mimeto → 🔧 Snap All Objects to Ground
/// </summary>
public class SnapAllToGroundWindow : EditorWindow
{
    // ── Settings ──────────────────────────────────────────────────────────────
    private LayerMask _groundMask       = ~0;          // "Everything" by default
    private float     _rayOriginHeight  = 200f;        // Cast from this height above object
    private bool      _onlyRootObjects  = true;        // Skip parented objects
    private bool      _skipTerrain      = true;        // Don't move Terrain objects
    private bool      _skipCameras      = true;
    private bool      _skipLights       = true;
    private bool      _skipCanvas       = true;
    private bool      _skipSpawnPoints  = true;        // Don't move MimicSpawnPoints
    private bool      _skipMimicSpawner = true;
    private bool      _skipSelected     = false;       // Snap ONLY selected objects
    private float     _minRendererSize  = 0.01f;       // Ignore tiny/invisible renderers

    // ── Preview state ─────────────────────────────────────────────────────────
    private List<SnapCandidate> _candidates = new List<SnapCandidate>();
    private Vector2             _scrollPos;
    private bool                _previewDone = false;
    private int                 _skippedCount;

    // ── Styles ────────────────────────────────────────────────────────────────
    private GUIStyle _headerStyle;
    private GUIStyle _boldLabel;
    private bool     _stylesInit = false;

    private struct SnapCandidate
    {
        public GameObject Go;
        public Vector3    CurrentPos;
        public Vector3    TargetPos;
        public float      Delta;          // Y movement
        public string     GroundHitName;
        public bool       WillMove;       // false if delta is negligible
    }

    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Mimeto/🔧 Snap All Objects to Ground", priority = 60)]
    public static void OpenWindow()
    {
        var win = GetWindow<SnapAllToGroundWindow>("Snap to Ground");
        win.minSize = new Vector2(420, 520);
        win.Show();
    }

    // ── GUI ───────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        InitStyles();

        // ── Header ───────────────────────────────────────────────────────────
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("🔧 Snap All Objects to Ground", _headerStyle);
        EditorGUILayout.LabelField(
            "Moves every object so its mesh bottom sits exactly on the ground surface.",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(8);

        // ── Settings ─────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
        DrawSeparator();

        _groundMask      = LayerMaskField("Ground Layer(s)", _groundMask);
        _rayOriginHeight = EditorGUILayout.FloatField(
            new GUIContent("Ray Origin Height", "Cast ray from this many units ABOVE each object"),
            _rayOriginHeight);
        _minRendererSize = EditorGUILayout.FloatField(
            new GUIContent("Min Renderer Size", "Skip objects whose bounds are smaller than this (particles, etc.)"),
            _minRendererSize);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Scope", EditorStyles.boldLabel);
        _skipSelected    = EditorGUILayout.Toggle("Only Selected Objects", _skipSelected);
        _onlyRootObjects = EditorGUILayout.Toggle(
            new GUIContent("Only Root Objects", "Skip child objects — snapping parents is usually enough"),
            _onlyRootObjects);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Skip These Types", EditorStyles.boldLabel);
        _skipTerrain      = EditorGUILayout.Toggle("Terrain", _skipTerrain);
        _skipCameras      = EditorGUILayout.Toggle("Cameras", _skipCameras);
        _skipLights       = EditorGUILayout.Toggle("Lights (light-only objects)", _skipLights);
        _skipCanvas       = EditorGUILayout.Toggle("UI Canvas", _skipCanvas);
        _skipSpawnPoints  = EditorGUILayout.Toggle("MimicSpawnPoints", _skipSpawnPoints);
        _skipMimicSpawner = EditorGUILayout.Toggle("MimicSpawner", _skipMimicSpawner);

        EditorGUILayout.Space(10);

        // ── Action Buttons ────────────────────────────────────────────────────
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("🔍 Preview (Dry Run)", GUILayout.Height(36)))
                RunPreview();

            GUI.enabled = _previewDone && _candidates.Count > 0;
            if (GUILayout.Button("✅ Apply Snap", GUILayout.Height(36)))
                ApplySnap();
            GUI.enabled = true;
        }

        // ── Preview Results ───────────────────────────────────────────────────
        if (!_previewDone) return;

        EditorGUILayout.Space(8);
        DrawSeparator();

        int willMove = _candidates.Count(c => c.WillMove);
        EditorGUILayout.LabelField(
            $"Preview — {_candidates.Count} candidates  |  {willMove} will move  |  {_skippedCount} skipped",
            EditorStyles.boldLabel);

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos,
            GUILayout.ExpandHeight(true));

        foreach (var c in _candidates)
        {
            Color prev = GUI.color;
            GUI.color = c.WillMove ? new Color(0.6f, 1f, 0.6f) : new Color(0.8f, 0.8f, 0.8f);

            using (new EditorGUILayout.HorizontalScope("box"))
            {
                GUI.color = prev;

                // Object name — clicking selects it
                if (GUILayout.Button(c.Go.name,
                    EditorStyles.linkLabel, GUILayout.Width(180)))
                {
                    Selection.activeGameObject = c.Go;
                    SceneView.FrameLastActiveSceneView();
                }

                GUILayout.Label(
                    c.WillMove
                        ? $"Y: {c.CurrentPos.y:F3}  →  {c.TargetPos.y:F3}  (Δ {c.Delta:+0.000;-0.000})"
                        : "Already on ground",
                    GUILayout.ExpandWidth(true));

                if (c.WillMove)
                    GUILayout.Label(c.GroundHitName, EditorStyles.miniLabel, GUILayout.Width(100));
            }

            GUI.color = prev;
        }

        EditorGUILayout.EndScrollView();
    }

    // ── Preview ───────────────────────────────────────────────────────────────

    private void RunPreview()
    {
        _candidates.Clear();
        _skippedCount = 0;
        _previewDone  = false;

        IEnumerable<GameObject> pool = _skipSelected
            ? Selection.gameObjects.AsEnumerable()
            : CollectAllObjects();

        foreach (GameObject go in pool)
        {
            if (ShouldSkip(go)) { _skippedCount++; continue; }

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (!HasValidRenderer(renderers)) { _skippedCount++; continue; }

            // Combined world-space bounds
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);

            if (bounds.size.magnitude < _minRendererSize) { _skippedCount++; continue; }

            // Cast from above
            float    originY  = Mathf.Max(bounds.max.y + 1f, go.transform.position.y + _rayOriginHeight);
            Vector3  origin   = new Vector3(go.transform.position.x, originY, go.transform.position.z);
            float    maxDist  = originY - bounds.min.y + 10f;

            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDist, _groundMask))
            {
                _skippedCount++;
                continue; // No ground found
            }

            // Don't snap if the hit object is the object itself or a child
            if (hit.transform == go.transform ||
                hit.transform.IsChildOf(go.transform))
            {
                _skippedCount++;
                continue;
            }

            float pivotToBottom = go.transform.position.y - bounds.min.y;
            Vector3 targetPos   = new Vector3(
                go.transform.position.x,
                hit.point.y + pivotToBottom,
                go.transform.position.z
            );

            float delta = targetPos.y - go.transform.position.y;

            _candidates.Add(new SnapCandidate
            {
                Go           = go,
                CurrentPos   = go.transform.position,
                TargetPos    = targetPos,
                Delta        = delta,
                GroundHitName = hit.collider.gameObject.name,
                WillMove     = Mathf.Abs(delta) > 0.001f
            });
        }

        _candidates = _candidates.OrderByDescending(c => Mathf.Abs(c.Delta)).ToList();
        _previewDone = true;
        Repaint();
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    private void ApplySnap()
    {
        int moved = 0;

        Undo.SetCurrentGroupName("Snap All to Ground");
        int group = Undo.GetCurrentGroup();

        foreach (var c in _candidates)
        {
            if (!c.WillMove || c.Go == null) continue;

            Undo.RecordObject(c.Go.transform, "Snap to Ground");
            c.Go.transform.position = c.TargetPos;
            EditorUtility.SetDirty(c.Go);
            moved++;
        }

        Undo.CollapseUndoOperations(group);

        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

        Debug.Log($"[SnapToGround] ✅ Snapped {moved} objects to ground. Scene saved. (Ctrl+Z to undo all)");

        EditorUtility.DisplayDialog(
            "Snap Complete ✅",
            $"Snapped {moved} object(s) to ground.\n\n" +
            "Scene has been saved.\nPress Ctrl+Z (Edit → Undo) to revert all changes at once.",
            "OK");

        // Refresh preview
        RunPreview();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IEnumerable<GameObject> CollectAllObjects()
    {
        if (_onlyRootObjects)
        {
            return UnityEngine.SceneManagement.SceneManager
                .GetActiveScene()
                .GetRootGameObjects();
        }
        return Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include);
    }

    private bool ShouldSkip(GameObject go)
    {
        if (go == null || !go.activeInHierarchy) return true;
        if (_onlyRootObjects && go.transform.parent != null) return true;
        if (_skipTerrain      && go.GetComponent<Terrain>()         != null) return true;
        if (_skipCameras      && go.GetComponentInChildren<Camera>() != null) return true;
        if (_skipLights       && IsLightOnly(go)) return true;
        if (_skipCanvas       && go.GetComponent<Canvas>()           != null) return true;
        if (_skipSpawnPoints  && go.GetComponent<MimicSpawnPoint>()  != null) return true;
        if (_skipMimicSpawner && go.GetComponent<MimicSpawner>()     != null) return true;
        return false;
    }

    private static bool IsLightOnly(GameObject go)
    {
        var comps = go.GetComponents<Component>();
        return comps.All(c => c is Transform || c is Light);
    }

    private bool HasValidRenderer(Renderer[] renderers)
    {
        return renderers.Length > 0 && renderers.Any(r => r.enabled);
    }

    // ── LayerMask field helper ────────────────────────────────────────────────

    private static LayerMask LayerMaskField(string label, LayerMask mask)
    {
        var layers     = new List<string>();
        var layerNums  = new List<int>();
        for (int i = 0; i < 32; i++)
        {
            string name = LayerMask.LayerToName(i);
            if (!string.IsNullOrEmpty(name)) { layers.Add(name); layerNums.Add(i); }
        }

        int flags = 0;
        for (int i = 0; i < layerNums.Count; i++)
            if ((mask.value & (1 << layerNums[i])) != 0) flags |= (1 << i);

        flags = EditorGUILayout.MaskField(label, flags, layers.ToArray());

        int result = 0;
        for (int i = 0; i < layerNums.Count; i++)
            if ((flags & (1 << i)) != 0) result |= (1 << layerNums[i]);

        return result;
    }

    private void InitStyles()
    {
        if (_stylesInit) return;
        _headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 14,
            alignment = TextAnchor.MiddleLeft
        };
        _boldLabel = new GUIStyle(EditorStyles.boldLabel);
        _stylesInit = true;
    }

    private static void DrawSeparator()
    {
        var rect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 1f));
        EditorGUILayout.Space(2);
    }
}
#endif
