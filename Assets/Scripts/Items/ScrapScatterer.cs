using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Spawns scrap items at random NavMesh positions using the match seed.
/// Every time the Map scene loads with a new seed, scraps appear at different locations.
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

    [Header("Random Spawn Settings")]
    [Tooltip("Khoảng cách tối thiểu giữa 2 scrap")]
    public float minDistanceBetweenScraps = 6f;

    [Tooltip("Khoảng cách tối thiểu từ scrap đến spawn point (0,0,0)")]
    public float minDistanceFromSpawn = 8f;

    private System.Random _rng;
    private List<Vector3> _usedPositions = new List<Vector3>();

    // Bảng định nghĩa loại + số lượng scrap cần spawn
    private static readonly (string type, int amount)[] scrapTable = new (string, int)[]
    {
        ("circuit", 1),
        ("circuit", 1),
        ("metal_pipe", 2),
        ("metal_pipe", 2),
        ("metal_pipe", 2),
        ("metal_pipe", 2),
        ("chemical", 1),
        ("chemical", 1),
        ("pipe", 1),
        ("pipe", 1),
        ("battery", 1),
        ("battery", 1),
    };

    System.Collections.IEnumerator Start()
    {
        // Đợi seed từ Host
        while (PlayerInventory.GlobalMatchSeed == 0) yield return null;

        // Đợi Player spawn và rơi xuống đất (giống EscapeManager)
        while (GameObject.FindGameObjectWithTag("Player") == null)
            yield return null;
        yield return new WaitForSeconds(2f);

        _rng = new System.Random(PlayerInventory.GlobalMatchSeed + 5001);
        ScatterRandomly();
    }

    /// <summary>
    /// Spawn scraps tại vị trí random trên NavMesh dùng seed.
    /// </summary>
    void ScatterRandomly()
    {
        _usedPositions.Clear();

        // Lấy NavMesh triangulation
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        if (tri.vertices == null || tri.vertices.Length == 0)
        {
            Debug.LogError("[ScrapScatterer] Không tìm thấy NavMesh! Fallback về vị trí cố định.");
            ScatterFallback();
            return;
        }

        // Tính diện tích từng tam giác + prefix sum
        int triCount = tri.indices.Length / 3;
        float[] cumulative = new float[triCount];
        float totalArea = 0f;
        for (int i = 0; i < triCount; i++)
        {
            Vector3 a = tri.vertices[tri.indices[i * 3]];
            Vector3 b = tri.vertices[tri.indices[i * 3 + 1]];
            Vector3 c = tri.vertices[tri.indices[i * 3 + 2]];
            float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            totalArea += area;
            cumulative[i] = totalArea;
        }

        // Spawn từng scrap
        int spawned = 0;
        foreach (var (type, amount) in scrapTable)
        {
            Vector3 pos = FindRandomNavMeshPos(tri, cumulative, totalArea, triCount);
            if (pos == Vector3.zero)
            {
                Debug.LogWarning($"[ScrapScatterer] Không tìm được vị trí cho scrap '{type}'");
                continue;
            }

            CreateScrap(type, pos, amount);
            _usedPositions.Add(pos);
            spawned++;
        }

        // Spawn Extraction Point ở vị trí random nếu chưa có
        if (FindAnyObjectByType<ExtractionSystem>() == null)
        {
            Vector3 exPos = FindRandomNavMeshPos(tri, cumulative, totalArea, triCount);
            if (exPos == Vector3.zero) exPos = new Vector3(0, 0, -5);
            
            GameObject ex = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ex.name = "ExtractionSystem";
            ex.AddComponent<ExtractionSystem>();
            SpawnUtils.SnapToGround(ex, FindGroundPoint(exPos));
            SpawnUtils.FitColliders(ex);
        }

        Debug.Log($"[ScrapScatterer] ✓ Đã scatter {spawned} scraps ngẫu nhiên (Seed: {PlayerInventory.GlobalMatchSeed})");
    }

    Vector3 FindRandomNavMeshPos(NavMeshTriangulation tri, float[] cumulative, float totalArea, int triCount)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            // Chọn tam giác ngẫu nhiên theo diện tích
            float roll = (float)_rng.NextDouble() * totalArea;
            int ti = System.Array.BinarySearch(cumulative, roll);
            if (ti < 0) ti = ~ti;
            ti = Mathf.Clamp(ti, 0, triCount - 1);

            Vector3 va = tri.vertices[tri.indices[ti * 3]];
            Vector3 vb = tri.vertices[tri.indices[ti * 3 + 1]];
            Vector3 vc = tri.vertices[tri.indices[ti * 3 + 2]];

            float r1 = Mathf.Sqrt((float)_rng.NextDouble());
            float r2 = (float)_rng.NextDouble();
            Vector3 pt = (1 - r1) * va + (r1 * (1 - r2)) * vb + (r1 * r2) * vc;

            // Kiểm tra khoảng cách từ spawn
            if (Vector3.Distance(pt, Vector3.zero) < minDistanceFromSpawn) continue;

            // Kiểm tra khoảng cách với các scrap khác
            bool tooClose = false;
            foreach (Vector3 used in _usedPositions)
            {
                if (Vector3.Distance(pt, used) < minDistanceBetweenScraps) { tooClose = true; break; }
            }
            if (tooClose) continue;

            // Snap lên NavMesh
            if (NavMesh.SamplePosition(pt, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                return hit.position;
        }
        return Vector3.zero;
    }

    /// <summary>Fallback khi không có NavMesh — dùng vị trí cũ hardcoded</summary>
    void ScatterFallback()
    {
        CreateScrap("circuit",    new Vector3( 10, 0,  10), 1);
        CreateScrap("circuit",    new Vector3(-10, 0,  10), 1);
        CreateScrap("metal_pipe", new Vector3( 10, 0, -10), 2);
        CreateScrap("metal_pipe", new Vector3(-10, 0, -10), 2);
        CreateScrap("metal_pipe", new Vector3(  0, 0,  10), 2);
        CreateScrap("metal_pipe", new Vector3( 12, 0,  -5), 2);
        CreateScrap("chemical",   new Vector3(  5, 0,  15), 1);
        CreateScrap("chemical",   new Vector3( -5, 0,  15), 1);
        CreateScrap("pipe",       new Vector3( 15, 0,   0), 1);
        CreateScrap("pipe",       new Vector3(-15, 0,   0), 1);
        CreateScrap("battery",    new Vector3(  8, 0,   5), 1);
        CreateScrap("battery",    new Vector3( -8, 0,   5), 1);

        if (FindAnyObjectByType<ExtractionSystem>() == null)
        {
            GameObject ex = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ex.name = "ExtractionSystem";
            ex.AddComponent<ExtractionSystem>();
            SnapPrimitiveToGround(ex, new Vector3(0, 0, -5));
            SpawnUtils.FitColliders(ex);
        }
    }

    [ContextMenu("Scatter Scraps (Legacy Fixed Positions)")]
    public void Scatter()
    {
        ScatterFallback();
    }

    // ── Create a single scrap object ─────────────────────────────────────────

    void CreateScrap(string type, Vector3 targetXZ, int amount = -1)
    {
        if (amount < 0)
            amount = (type == "metal_pipe" || type == "metal pipe") ? 2 : 1;

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