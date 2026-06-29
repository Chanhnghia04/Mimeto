using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Spawns scrap items, a Workbench and an Extraction Point in the scene.
///
/// SETUP REQUIRED (one-time):
///   In the Unity Editor, run: Tools → Scrap Setup → Copy Scraps to Resources
///   This copies Assets/Prefabs/Items/Scrap_*.prefab into Assets/Resources/Scraps/
///   so that Resources.Load works in both Editor AND in actual game builds.
/// </summary>
public class ScrapScatterer : MonoBehaviour
{
    public GameObject scrapPrefab; // Not used here — prefabs are loaded by type name

    [Tooltip("Layer(s) considered as ground when snapping scraps.")]
    public LayerMask groundLayer = ~0; // Default: everything

    [Tooltip("Height above each target XZ position from which to cast the ground ray.")]
    public float groundRayHeight = 50f;

    [ContextMenu("Scatter Scraps")]
    public void Scatter()
    {
        // ── Scrap items ───────────────────────────────────────────────────────
        CreateScrap("circuit",    new Vector3( 10, 0,  10));
        CreateScrap("circuit",    new Vector3(-10, 0,  10));
        CreateScrap("metal_pipe", new Vector3( 10, 0, -10));
        CreateScrap("metal_pipe", new Vector3(-10, 0, -10));
        CreateScrap("metal_pipe", new Vector3(  0, 0,  10));
        CreateScrap("metal_pipe", new Vector3( 12, 0,  -5));
        CreateScrap("chemical",   new Vector3(  5, 0,  15));
        CreateScrap("chemical",   new Vector3( -5, 0,  15));
        CreateScrap("pipe",       new Vector3( 15, 0,   0));
        CreateScrap("pipe",       new Vector3(-15, 0,   0));
        CreateScrap("battery",    new Vector3(  8, 0,   5));
        CreateScrap("battery",    new Vector3( -8, 0,   5));

        // ── Workbench ─────────────────────────────────────────────────────────
        GameObject wb = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wb.name = "Workbench";
        wb.AddComponent<Workbench>();
        SnapPrimitiveToGround(wb, new Vector3(0, 0, 5));
        SpawnUtils.FitColliders(wb);

        // ── Extraction Point ──────────────────────────────────────────────────
        GameObject ex = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ex.name = "ExtractionSystem";
        ex.AddComponent<ExtractionSystem>();
        SnapPrimitiveToGround(ex, new Vector3(0, 0, -5));
        SpawnUtils.FitColliders(ex);
    }

    // ── Create a single scrap object ─────────────────────────────────────────

    void CreateScrap(string type, Vector3 targetXZ)
    {
        int amount = (type == "metal_pipe" || type == "metal pipe") ? 2 : 1;

        Vector3 groundPoint = FindGroundPoint(targetXZ);

        // ── Try to load prefab from Resources/Scraps (works in Editor AND builds) ──
        // Key mapping: spaces → underscore so filenames are valid
        string resourceKey = "Scraps/" + type.Replace(" ", "_");
        GameObject prefab = Resources.Load<GameObject>(resourceKey);

        if (prefab != null)
        {
#if UNITY_EDITOR
            // In Editor: instantiate as a prefab instance to keep the link
            GameObject go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
#else
            // In builds: regular instantiate
            GameObject go = Instantiate(prefab);
#endif
            go.name = "Scrap_" + type;
            go.transform.localScale = Vector3.one;

            // Ensure ScrapItem data is set (prefab already has it, but refresh to be safe)
            ScrapItem si = go.GetComponent<ScrapItem>();
            if (si == null) si = go.AddComponent<ScrapItem>();
            si.scrapType  = type;
            si.amount     = amount;
            si.rootObject = go;

            // 1. Fit BoxColliders to mesh bounds BEFORE snapping
            SpawnUtils.FitColliders(go);

            // 2. Snap bottom of mesh to ground surface
            SpawnUtils.SnapToGround(go, groundPoint);
            return;
        }

        // ── EDITOR-ONLY fallback: load directly from Assets/Models via AssetDatabase ──
        // This path is only reached if Resources/Scraps has not been populated yet.
        // Run: Tools → Scrap Setup → Copy Scraps to Resources to fix this.
#if UNITY_EDITOR
        string fbxPath = GetFbxPath(type);
        if (!string.IsNullOrEmpty(fbxPath))
        {
            GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxPrefab != null)
            {
                Debug.LogWarning($"[ScrapScatterer] Loaded '{type}' from AssetDatabase (Editor only). " +
                                 "Run 'Tools → Scrap Setup → Copy Scraps to Resources' for build support.");

                GameObject go = PrefabUtility.InstantiatePrefab(fbxPrefab) as GameObject;
                go.name = "Scrap_" + type;
                go.transform.localScale = Vector3.one;

                ScrapItem si = go.AddComponent<ScrapItem>();
                si.scrapType  = type;
                si.amount     = amount;
                si.rootObject = go;

                SpawnUtils.FitColliders(go);
                SpawnUtils.SnapToGround(go, groundPoint);
                return;
            }
        }
#endif

        // ── Final fallback: coloured primitive cube (works everywhere) ────────
        Debug.LogWarning($"[ScrapScatterer] No prefab found for '{type}'. " +
#if UNITY_EDITOR
                         "Run 'Tools → Scrap Setup → Copy Scraps to Resources' to fix this. " +
#endif
                         "Using primitive fallback.");

        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fallback.name = "Scrap_" + type;
        fallback.transform.localScale = Vector3.one * 0.3f;

        ScrapItem fallbackSi = fallback.AddComponent<ScrapItem>();
        fallbackSi.scrapType = type;
        fallbackSi.amount    = amount;

        // Colour coding for easy identification
        Renderer rend = fallback.GetComponent<Renderer>();
        if (rend != null)
        {
            if (type == "circuit")                             rend.material.color = Color.green;
            if (type == "metal pipe" || type == "metal_pipe") rend.material.color = Color.grey;
            if (type == "chemical")                            rend.material.color = Color.yellow;
            if (type == "pipe")                                rend.material.color = new Color(0.1f, 0.4f, 0.8f);
            if (type == "battery")                             rend.material.color = Color.red;
        }

        SpawnUtils.FitColliders(fallback);
        SpawnUtils.SnapToGround(fallback, groundPoint);
    }

    // ── Ground detection ──────────────────────────────────────────────────────

    private Vector3 FindGroundPoint(Vector3 pos)
    {
        Vector3 origin = new Vector3(pos.x, groundRayHeight, pos.z);

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                            groundRayHeight * 2f, groundLayer))
        {
            return hit.point;
        }

        Debug.LogWarning($"[ScrapScatterer] No ground found at XZ ({pos.x}, {pos.z}). " +
                         "Placing at Y=0. Check that the ground has a collider and is on the correct layer.");
        return new Vector3(pos.x, 0f, pos.z);
    }

    private void SnapPrimitiveToGround(GameObject obj, Vector3 targetXZ)
    {
        Vector3 groundPoint = FindGroundPoint(targetXZ);
        SpawnUtils.SnapToGround(obj, groundPoint);
    }

    // ── FBX path lookup (Editor-only fallback) ────────────────────────────────
#if UNITY_EDITOR
    private static string GetFbxPath(string type)
    {
        switch (type)
        {
            case "circuit":    return "Assets/Models/Item/machdien/base_basic_shaded.fbx";
            case "chemical":   return "Assets/Models/Item/binhhoachat/source/Silent Hill 1 Meshes - Chemical.fbx";
            case "pipe":       return "Assets/Models/Item/ongnhua/source/PIPE.fbx";
            case "metal_pipe":
            case "metal pipe": return "Assets/Models/Item/ongkimloai/source/pipe.obj";
            case "battery":    return "Assets/Models/Item/pin/source/AA_Battery.fbx";
            default:           return null;
        }
    }
#endif
}