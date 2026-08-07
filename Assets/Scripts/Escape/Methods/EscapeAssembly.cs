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
    public float minDistFromPlayer   = 25f; 
    public float minDistBetweenParts = 15f; 

    private int _collected = 0;
    private System.Random _rng;

    // Định nghĩa bộ phận: (tên, màu)
    private static readonly (string name, Color color)[] PartDefs =
    {
        ("Bánh Răng",           new Color(1.0f, 0.55f, 0.1f)),   // cam
        ("Bình Nhiên Liệu",     new Color(0.9f, 0.15f, 0.1f)),   // đỏ
        ("Bo Mạch Điều Khiển",  new Color(0.1f, 0.80f, 0.9f)),   // cyan
    };

    // ─────────────────────────────────────────────────────────────────────────

    System.Collections.IEnumerator Start()
    {
        while (PlayerInventory.GlobalMatchSeed == 0) yield return null;
        _rng = new System.Random(PlayerInventory.GlobalMatchSeed + 2001);
        
        // Chờ Player spawn
        while (GameObject.FindGameObjectWithTag("Player") == null) yield return null;
        
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
            go.transform.position = worldPos + Vector3.up * 0.15f;
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"EscapePart_{PartDefs[index].name}_Fallback";
            go.transform.position = worldPos + Vector3.up * 0.15f;
            go.transform.localScale = Vector3.one * 0.32f;
            
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

        // Dùng _rng thay vì Random.Range
        go.transform.rotation = Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f);

        EscapePart ep = go.GetComponent<EscapePart>();
        if (ep == null) ep = go.AddComponent<EscapePart>();
        ep.partName = PartDefs[index].name;
        ep.parentAssembly = this;

        if (go.GetComponent<Collider>() == null)
        {
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(1f, 1f, 1f);
        }

        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);

        Debug.Log($"[EscapeAssembly] Spawn '{PartDefs[index].name}' tại {worldPos}");
    }

    Vector3 FindValidPos(Vector3 playerPos, List<Vector3> usedPos)
    {
        UnityEngine.AI.NavMeshTriangulation navData = UnityEngine.AI.NavMesh.CalculateTriangulation();
        if (navData.vertices.Length == 0) return playerPos + Vector3.forward * 20f;

        int triCount = navData.indices.Length / 3;

        // PRE-COMPUTE tất cả random values => chuỗi random không bao giờ bị lệch bởi Physics
        int maxAttempts = 200;
        int[] triIndices = new int[maxAttempts];
        float[] lerpA    = new float[maxAttempts];
        float[] lerpB    = new float[maxAttempts];
        for (int i = 0; i < maxAttempts; i++)
        {
            triIndices[i] = _rng.Next(0, triCount);
            lerpA[i]      = (float)_rng.NextDouble();
            lerpB[i]      = (float)_rng.NextDouble();
        }

        // Fallback randoms
        int fallbackCount = Mathf.Min(navData.vertices.Length, 50);
        int[] fallbackIndices = new int[fallbackCount];
        for (int i = 0; i < fallbackCount; i++)
        {
            fallbackIndices[i] = _rng.Next(0, navData.vertices.Length);
        }

        // Tìm vị trí hợp lệ
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            int t = triIndices[attempt];
            int v1 = navData.indices[t * 3];
            int v2 = navData.indices[t * 3 + 1];
            int v3 = navData.indices[t * 3 + 2];

            Vector3 pt = Vector3.Lerp(navData.vertices[v1], navData.vertices[v2], lerpA[attempt]);
            pt = Vector3.Lerp(pt, navData.vertices[v3], lerpB[attempt]);

            if (Mathf.Abs(pt.y - playerPos.y) > 4f) continue;
            if (Vector3.Distance(pt, playerPos) < minDistFromPlayer) continue;

            bool tooClose = false;
            foreach (var u in usedPos)
                if (Vector3.Distance(pt, u) < minDistBetweenParts) { tooClose = true; break; }
            if (tooClose) continue;

            if (Physics.CheckSphere(pt + Vector3.up * 0.5f, 0.2f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                continue;

            return pt;
        }

        // Fallback
        for (int i = 0; i < fallbackCount; i++)
        {
            Vector3 v = navData.vertices[fallbackIndices[i]];
            if (!Physics.CheckSphere(v + Vector3.up * 0.5f, 0.2f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                return v;
        }
        return navData.vertices[fallbackIndices[0]];
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
        AudioClip clip = Resources.Load<AudioClip>("Audio/assemble");
        if (clip != null)
        {
            GameObject tempAudio = new GameObject("TempAssembleAudio");
            AudioSource src = tempAudio.AddComponent<AudioSource>();
            src.spatialBlend = 0f; // Âm thanh 2D để nghe rõ 100%
            src.PlayOneShot(clip);
            Destroy(tempAudio, clip.length + 0.1f);
        }
        
        PlayerInventory inv = interactor.GetComponentInParent<PlayerInventory>()
                           ?? interactor.GetComponentInChildren<PlayerInventory>();
        if (inv != null && inv.IsOwner)
        {
            inv.SyncAssemblyPartServerRpc(partName, transform.position);
        }
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
