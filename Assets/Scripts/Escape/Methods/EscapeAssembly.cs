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
    public float spawnRadius         = 40f;
    public float minDistFromPlayer   = 18f;
    public float minDistBetweenParts = 14f;

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
            // Bù đắp độ cao (Mesh cao 1m -> offset 0.5m)
            go.transform.position = worldPos + Vector3.up * 0.5f;
        }
        else
        {
            // Fallback to primitive
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"EscapePart_{PartDefs[index].name}_Fallback";
            go.transform.position = worldPos + Vector3.up * 0.3f;
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
        }

        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        // Point light nhỏ để dễ thấy từ xa
        GameObject lg = new GameObject("Glow");
        lg.transform.SetParent(go.transform, false);
        Light l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = PartDefs[index].color;
        l.intensity = 1.8f;
        l.range = 4f;

        // Component xử lý tương tác
        EscapePart ep = go.GetComponent<EscapePart>() ?? go.AddComponent<EscapePart>();
        ep.partName = PartDefs[index].name;
        ep.parentAssembly = this;

        // Add collider if missing
        if (go.GetComponent<Collider>() == null)
        {
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(1f, 1f, 1f);
        }

        Debug.Log($"[EscapeAssembly] Spawn '{PartDefs[index].name}' tại {worldPos}");
    }

    Vector3 FindValidPos(Vector3 playerPos, List<Vector3> usedPos)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            Vector3 rand = Random.insideUnitSphere * spawnRadius;
            rand.y = 0f;
            Vector3 candidate = playerPos + rand;

            if (Vector3.Distance(candidate, playerPos) < minDistFromPlayer) continue;

            bool tooClose = false;
            foreach (var u in usedPos)
                if (Vector3.Distance(candidate, u) < minDistBetweenParts) { tooClose = true; break; }
            if (tooClose) continue;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                return hit.position;
        }

        // Fallback
        Vector3 fb = Random.insideUnitSphere * spawnRadius; fb.y = 0f;
        return NavMesh.SamplePosition(fb, out NavMeshHit fhit, 12f, NavMesh.AllAreas)
            ? fhit.position
            : playerPos + fb;
    }

    // ── Called by EscapePart when collected ──────────────────────────────────

    public void OnPartCollected(string partName)
    {
        _collected++;
        Debug.Log($"<color=orange>[EscapeAssembly] +1 bộ phận: {partName} ({_collected}/{totalParts})</color>");
        UpdateHUD();

        if (_collected >= totalParts)
        {
            Debug.Log("<color=lime>[EscapeAssembly] Đủ bộ phận! Mở cửa thoát!</color>");
            EscapeManager.Instance?.UnlockEscape();
        }
    }

    void UpdateHUD()
    {
        string msg = _collected >= totalParts
            ? "Đủ bộ phận! Đến cửa thoát ngay!"
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
        // Float lên xuống + xoay nhẹ
        _phase += Time.deltaTime;
        Vector3 p = transform.position;
        p.y = _baseY + Mathf.Sin(_phase * 1.4f) * 0.09f;
        transform.position = p;
        transform.Rotate(Vector3.up * 28f * Time.deltaTime, Space.World);
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
