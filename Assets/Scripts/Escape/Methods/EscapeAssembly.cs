using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Phương thức Assembly: Tự spawn 3 bộ phận trên bản đồ.
/// Player bấm [E] nhặt đủ 3 → Cửa thoát mở.
///
/// SETUP:
///   1. Tạo GameObject "EscapeAssembly" trong Scene, đặt INACTIVE.
///   2. Gắn script này vào.
///   3. EscapeManager sẽ tự bật nếu màn này chọn Assembly.
/// </summary>
public class EscapeAssembly : MonoBehaviour
{
    [Header("Spawn Config")]
    public int   totalParts          = 3;
    public float spawnRadius         = 5f; // FOR TESTING (was 40f)
    public float minDistFromPlayer   = 1f; // FOR TESTING (was 18f)
    public float minDistBetweenParts = 1f; // FOR TESTING (was 14f)

    private int _collected = 0;

    // Định nghĩa bộ phận: (tên, màu)
    private static readonly (string name, Color color)[] PartDefs =
    {
        ("Bánh Răng",           new Color(1.0f, 0.55f, 0.1f)),   // cam
        ("Bình Nhiên Liệu",     new Color(0.9f, 0.15f, 0.1f)),   // đỏ
        ("Bo Mạch Điều Khiển",  new Color(0.1f, 0.80f, 0.9f)),   // cyan
    };

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        SpawnAllParts();
        UpdateHUD();
    }

    // ── Spawning ──────────────────────────────────────────────────────────────

    void SpawnAllParts()
    {
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        Vector3 playerPos   = playerGO != null ? playerGO.transform.position : Vector3.zero;
        List<Vector3> used  = new List<Vector3>();

        int count = Mathf.Min(totalParts, PartDefs.Length);
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = FindValidPos(playerPos, used);
            used.Add(pos);
            SpawnPart(i, pos);
        }
    }

    void SpawnPart(int index, Vector3 worldPos)
    {
        string[] resourcePaths = {
            "EscapeAssets/Mesh_Gear",
            "EscapeAssets/Mesh_FuelCanister",
            "EscapeAssets/Mesh_CircuitBoard"
        };

        GameObject prefab = Resources.Load<GameObject>(resourcePaths[index % resourcePaths.Length]);
        GameObject go;

        if (prefab != null)
        {
            go = Instantiate(prefab);
            go.name = $"EscapePart_{PartDefs[index].name}";
            // Bù đắp độ cao thật thấp (sát mặt đất)
            go.transform.position = worldPos + Vector3.up * 0.15f;
        }
        else
        {
            // Fallback to primitive
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"EscapePart_{PartDefs[index].name}_Fallback";
            go.transform.position = worldPos + Vector3.up * 0.15f;
            go.transform.localScale = Vector3.one * 0.32f;
            
            // Material màu + emissive glow
            Renderer rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = PartDefs[index].color;
                mat.SetColor("_EmissionColor", PartDefs[index].color * 0.6f);
                mat.EnableKeyword("_EMISSION");
                rend.material = mat;
            }

            GameObject lg = new GameObject("Glow");
            lg.transform.SetParent(go.transform, false);
            Light l = lg.AddComponent<Light>();
            l.color = Color.yellow;
            l.range = 4f;
        }

        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        // Component xử lý tương tác
        EscapePart ep = go.GetComponent<EscapePart>();
        if (ep == null) ep = go.AddComponent<EscapePart>();
        ep.partName = PartDefs[index].name;
        ep.parentAssembly = this;

        // Add collider if missing
        if (go.GetComponent<Collider>() == null)
        {
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(1f, 1f, 1f);
        }

        // Bỏ Rigidbody đi vì mình sẽ cho nó lơ lửng lại
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);

        Debug.Log($"[EscapeAssembly] Spawn '{PartDefs[index].name}' tại {worldPos}");
    }

    Vector3 FindValidPos(Vector3 playerPos, List<Vector3> usedPos)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            Vector2 circle = Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = playerPos + new Vector3(circle.x, 0f, circle.y);

            if (Vector3.Distance(candidate, playerPos) < minDistFromPlayer) continue;

            bool tooClose = false;
            foreach (var u in usedPos)
                if (Vector3.Distance(candidate, u) < minDistBetweenParts) { tooClose = true; break; }
            if (tooClose) continue;

            // Tìm điểm NavMesh gần nhất
            if (UnityEngine.AI.NavMesh.SamplePosition(candidate, out UnityEngine.AI.NavMeshHit hit, 4f, UnityEngine.AI.NavMesh.AllAreas))
            {
                // QUAN TRỌNG: Loại bỏ những điểm nằm trên nóc nhà (Y chênh lệch quá 1.5 mét so với người chơi)
                if (Mathf.Abs(hit.position.y - playerPos.y) < 1.5f)
                {
                    return hit.position;
                }
            }
        }

        // Fallback: Quăng ngay trước mặt Player, giữ nguyên độ cao của Player
        return playerPos + GameObject.FindGameObjectWithTag("Player").transform.forward * 2f;
    }

    // ── Called by EscapePart when collected ──────────────────────────────────

    public void OnPartCollected(string partName)
    {
        _collected++;
        Debug.Log($"<color=orange>[EscapeAssembly] +1 bộ phận: {partName} ({_collected}/{totalParts})</color>");
        UpdateHUD();

        if (_collected >= totalParts)
        {
            Debug.Log("<color=lime>[EscapeAssembly] Đủ bộ phận! Đến cửa thoát ngay!</color>");
            if (EscapeManager.Instance != null)
                EscapeManager.Instance.IsReadyToAssemble = true;
        }
    }

    void UpdateHUD()
    {
        string msg = _collected >= totalParts
            ? "Đủ bộ phận! Đến cửa thoát để lắp ráp!"
            : $"Bộ phận: {_collected}/{totalParts} — Tìm thêm trên bản đồ";
        EscapeManager.Instance?.ReportProgress(msg, (float)_collected / totalParts);
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Bộ phận thoát hiểm. Tự được spawn bởi EscapeAssembly.
/// Player bấm [E] để nhặt.
/// </summary>
public class EscapePart : MonoBehaviour, IInteractable
{
    [HideInInspector] public string         partName;
    [HideInInspector] public EscapeAssembly parentAssembly;

    private float _baseY;
    private float _phase;

    void Start()
    {
        _baseY = transform.position.y;
        _phase = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        // Float lên xuống thật nhẹ + xoay
        _phase += Time.deltaTime;
        Vector3 p = transform.position;
        p.y = _baseY + Mathf.Sin(_phase * 1.5f) * 0.05f; // Biên độ rất nhỏ (5cm)
        transform.position = p;
        transform.Rotate(Vector3.up * 20f * Time.deltaTime, Space.World);
    }

    public void Interact(GameObject interactor)
    {
        parentAssembly?.OnPartCollected(partName);
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.65f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.7f, $"[{partName}]");
    }
#endif
}
