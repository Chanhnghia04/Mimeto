using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Spawn rương ngẫu nhiên KHẮP TOÀN BỘ MAP bằng cách đọc NavMesh triangulation.
/// Không cần giới hạn vùng spawn thủ công — tự động phủ toàn bộ diện tích NavMesh.
///
/// SETUP:
///   1. Gắn script này vào GameObject "ChestSpawner" trong scene.
///   2. Gán chestPrefab = prefab rương (có Chest.cs + Collider).
///   3. Chỉnh chestCount = số rương muốn có trên toàn map.
///   4. Chạy game → rương tự xuất hiện khắp nơi.
/// </summary>
public class ChestSpawner : MonoBehaviour
{
    [Header("Chest Prefab")]
    [Tooltip("Prefab rương đã gắn Chest.cs + Collider")]
    public GameObject chestPrefab;

    [Header("Spawn Settings")]
    [Tooltip("Tổng số rương spawn trên toàn map")]
    public int chestCount = 12;

    [Tooltip("Khoảng cách tối thiểu giữa 2 rương")]
    public float minDistanceBetweenChests = 8f;

    [Tooltip("Khoảng cách tối thiểu từ rương đến vị trí spawn của Player (0,0,0)")]
    public float minDistanceFromSpawn = 10f;

    [Tooltip("Số lần thử tối đa để tìm vị trí hợp lệ cho mỗi rương")]
    public int maxAttemptsPerChest = 50;

    [Tooltip("Tỉ lệ % diện tích NavMesh được dùng khi sample (1.0 = toàn bộ map)")]
    [Range(0.1f, 1f)]
    public float coverageRatio = 1f;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private List<Vector3> _navMeshPoints = new List<Vector3>();
    private List<Vector3> _usedPositions  = new List<Vector3>();
    private System.Random _rng;

    void Start()
    {
        StartCoroutine(WaitAndSpawn());
    }

    System.Collections.IEnumerator WaitAndSpawn()
    {
        float timeout = 10f;
        float elapsed = 0f;
        while (PlayerInventory.GlobalMatchSeed == 0 && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        int seed = PlayerInventory.GlobalMatchSeed != 0 ? PlayerInventory.GlobalMatchSeed : UnityEngine.Random.Range(1, int.MaxValue);
        _rng = new System.Random(seed + 4001);

        // Lấy toàn bộ điểm trên NavMesh từ triangulation
        BuildNavMeshPointPool();

        if (_navMeshPoints.Count == 0)
        {
            Debug.LogError("[ChestSpawner] Không tìm thấy NavMesh! Hãy bake NavMesh trước khi chạy.");
            yield break;
        }

        SpawnAllChests();
    }

    // ── Bước 1: Đọc NavMesh triangulation → tạo pool điểm ngẫu nhiên ─────────

    void BuildNavMeshPointPool()
    {
        _navMeshPoints.Clear();

        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        if (tri.vertices == null || tri.vertices.Length == 0) return;

        // Số điểm cần tạo = chestCount * maxAttempts để có đủ lựa chọn
        int sampleCount = chestCount * maxAttemptsPerChest;

        // Tạo điểm ngẫu nhiên trên NavMesh bằng cách:
        // 1. Chọn tam giác ngẫu nhiên theo trọng số diện tích
        // 2. Lấy điểm ngẫu nhiên trong tam giác đó
        // → Phân bố đều theo diện tích thật, không bị bias về góc map

        // Tính diện tích từng tam giác
        int triCount = tri.indices.Length / 3;
        float[] areas = new float[triCount];
        float totalArea = 0f;

        for (int i = 0; i < triCount; i++)
        {
            Vector3 a = tri.vertices[tri.indices[i * 3]];
            Vector3 b = tri.vertices[tri.indices[i * 3 + 1]];
            Vector3 c = tri.vertices[tri.indices[i * 3 + 2]];
            float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            areas[i]    = area;
            totalArea  += area;
        }

        // Tạo prefix sum để chọn tam giác theo xác suất tỉ lệ thuận diện tích
        float[] cumulative = new float[triCount];
        cumulative[0] = areas[0];
        for (int i = 1; i < triCount; i++)
            cumulative[i] = cumulative[i - 1] + areas[i];

        // Sample điểm ngẫu nhiên
        for (int s = 0; s < sampleCount; s++)
        {
            // Chọn tam giác ngẫu nhiên theo diện tích
            float roll = (float)_rng.NextDouble() * totalArea;
            int   ti   = System.Array.BinarySearch(cumulative, roll);
            if (ti < 0) ti = ~ti;
            ti = Mathf.Clamp(ti, 0, triCount - 1);

            Vector3 va = tri.vertices[tri.indices[ti * 3]];
            Vector3 vb = tri.vertices[tri.indices[ti * 3 + 1]];
            Vector3 vc = tri.vertices[tri.indices[ti * 3 + 2]];

            // Điểm ngẫu nhiên trong tam giác (barycentric)
            float r1 = Mathf.Sqrt((float)_rng.NextDouble());
            float r2 = (float)_rng.NextDouble();
            Vector3 point = (1 - r1) * va + (r1 * (1 - r2)) * vb + (r1 * r2) * vc;

            // PRE-COMPUTE để tránh lệch chuỗi vì SamplePosition
            // _rng được gọi xong xuôi ở trên rồi
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            {
                _navMeshPoints.Add(hit.position);
            }
        }

        // Xáo trộn để không bị bias theo thứ tự
        Shuffle(_navMeshPoints);

        Debug.Log($"[ChestSpawner] Đã tạo pool {_navMeshPoints.Count} điểm trên NavMesh " +
                  $"(diện tích tổng ~{totalArea:F0} m²).");
    }

    // ── Bước 2: Spawn rương vào các điểm hợp lệ ─────────────────────────────

    [ContextMenu("Spawn Chests Now")]
    public void SpawnAllChests()
    {
        if (chestPrefab == null)
        {
            // Auto-load fallback from Resources if not assigned
            chestPrefab = Resources.Load<GameObject>("Prefabs/Chest");

            if (chestPrefab != null)
            {
                Debug.LogWarning($"[ChestSpawner] chestPrefab was not assigned. Auto-loaded from Resources: {chestPrefab.name}");
            }
            else
            {
                Debug.LogError("[ChestSpawner] chestPrefab chưa gán VÀ không tìm thấy trong Resources/Prefabs/Chest!");
                return;
            }
        }

        _usedPositions.Clear();
        int spawned = 0;

        foreach (Vector3 candidate in _navMeshPoints)
        {
            if (spawned >= chestCount) break;

            // Không spawn quá gần spawn point
            if (Vector3.Distance(candidate, Vector3.zero) < minDistanceFromSpawn)
                continue;

            // Không spawn chồng lên rương khác
            if (IsTooClose(candidate))
                continue;

            SpawnChest(candidate);
            _usedPositions.Add(candidate);
            spawned++;
        }

        // Nếu pool không đủ, log cảnh báo
        if (spawned < chestCount)
        {
            Debug.LogWarning($"[ChestSpawner] Chỉ spawn được {spawned}/{chestCount} rương. " +
                             $"Thử giảm minDistanceBetweenChests ({minDistanceBetweenChests}m) " +
                             $"hoặc tăng maxAttemptsPerChest.");
        }
        else
        {
            Debug.Log($"[ChestSpawner] ✓ Đã spawn {spawned} rương khắp map.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void SpawnChest(Vector3 position)
    {
        Quaternion rot   = Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f);
        GameObject chest = Instantiate(chestPrefab, position, rot);
        chest.name       = "Chest_" + _rng.Next(1000, 9999);
        
        // Snap the chest to the ground so it doesn't sink halfway
        SpawnUtils.SnapToGround(chest, position);
    }


    bool IsTooClose(Vector3 pos)
    {
        foreach (Vector3 used in _usedPositions)
            if (Vector3.Distance(pos, used) < minDistanceBetweenChests)
                return true;
        return false;
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j    = _rng.Next(0, i + 1);
            T   tmp  = list[i];
            list[i]  = list[j];
            list[j]  = tmp;
        }
    }

    // ── Gizmo: hiển thị vị trí đã spawn khi chạy game ────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_usedPositions == null) return;
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.9f);
        foreach (Vector3 pos in _usedPositions)
            Gizmos.DrawWireSphere(pos, 0.5f);
    }
#endif
}
