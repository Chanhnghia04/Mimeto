using UnityEngine;

/// <summary>
/// Singleton trung tâm — chọn random 1 trong 4 phương thức thoát mỗi màn.
/// [DefaultExecutionOrder(-50)] đảm bảo Awake() chạy trước tất cả script khác.
///
/// SETUP:
///   1. Tạo GameObject "EscapeManager" trong Scene.
///   2. Gắn EscapeManager + EscapeHUD vào đó.
///   3. Đặt 4 GameObject method (EscapeAssembly, EscapeBeacon, EscapeCipher, EscapeReactor)
///      vào Scene — để INACTIVE trong Inspector.
///   4. EscapeManager sẽ tự random bật 1 cái mỗi màn.
/// </summary>
[DefaultExecutionOrder(-50)]
public class EscapeManager : MonoBehaviour
{
    public static EscapeManager Instance { get; private set; }

    // ── State ────────────────────────────────────────────────────────────────
    public EscapeMethodType CurrentMethod    { get; private set; }
    public bool             IsEscapeUnlocked { get; private set; }
    public string           ProgressMessage  { get; private set; } = "";
    public float            ProgressValue    { get; private set; } = 0f;

    // ── Events ────────────────────────────────────────────────────────────────
    public event System.Action OnEscapeUnlocked;

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Ép Unity reset seed random theo thời gian thực để đảm bảo mỗi lần Play là 1 kết quả khác nhau
        Random.InitState((int)System.DateTime.Now.Ticks);

        // Random 1 trong 4 (từ 0 đến 3)
        CurrentMethod = (EscapeMethodType)Random.Range(0, 4);

        Debug.Log($"<color=cyan>[EscapeManager] Đã random nhiệm vụ màn này: <b>{GetMethodName()}</b></color>");
    }

    void Start()
    {
        // Khởi tạo tự động thay vì bắt người dùng kéo thả
        switch (CurrentMethod)
        {
            case EscapeMethodType.Assembly:
                gameObject.AddComponent<EscapeAssembly>();
                break;
            case EscapeMethodType.Beacon:
                SpawnBeacon();
                break;
            case EscapeMethodType.Cipher:
                SpawnCipherKeypad();
                break;
            case EscapeMethodType.Reactor:
                SpawnReactor();
                break;
        }

        ReportProgress(GetMethodInstruction(), 0f);
    }

    // ── Spawning Helpers ──────────────────────────────────────────────────────

    void SpawnBeacon()
    {
        GameObject prefab = Resources.Load<GameObject>("EscapeAssets/Mesh_Antenna");
        GameObject beacon;
        
        if (prefab != null)
        {
            beacon = Instantiate(prefab);
            beacon.name = "EscapeBeacon";
        }
        else
        {
            beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = "EscapeBeacon_Fallback";
            beacon.transform.localScale = new Vector3(0.3f, 2.5f, 0.3f); 
        }

        Vector3 spawnPos = GetRandomNavMeshPos(35f);
        // Bù đắp độ cao nếu dùng prefab (pivot thường ở tâm mesh)
        beacon.transform.position = spawnPos + (prefab != null ? Vector3.up * 0.5f : Vector3.up * 2.5f);

        // Thêm BoxCollider to một chút để dễ interact (vì ăng-ten mỏng)
        BoxCollider bc = beacon.GetComponent<BoxCollider>() ?? beacon.AddComponent<BoxCollider>();
        bc.size = new Vector3(2f, 2f, 2f);
        bc.center = new Vector3(0, 1f, 0);
        bc.isTrigger = false;

        if (beacon.GetComponent<EscapeBeacon>() == null)
            beacon.AddComponent<EscapeBeacon>();
            
        Debug.Log($"[EscapeManager] Đã spawn Beacon Ăng-ten tại {beacon.transform.position}");
    }

    void SpawnCipherKeypad()
    {
        GameObject prefab = Resources.Load<GameObject>("EscapeAssets/Mesh_Keypad");
        GameObject keypad;
        
        if (prefab != null)
        {
            keypad = Instantiate(prefab);
            keypad.name = "EscapeCipherKeypad";
        }
        else
        {
            keypad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            keypad.name = "EscapeCipherKeypad_Fallback";
            keypad.transform.localScale = new Vector3(0.25f, 0.4f, 0.08f);
            
            Renderer rend = keypad.GetComponent<Renderer>();
            if (rend != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.12f, 0.12f, 0.13f); // Xám đen
                rend.material = mat;
            }
        }

        // Tìm cửa thoát để đính thẳng vào tường ngay bên phải
        ExtractionSystem extraction = Object.FindAnyObjectByType<ExtractionSystem>();
        if (extraction != null)
        {
            // Đặt ngay sát bên phải cửa (khoảng 0.85m) và hơi lồi ra khỏi tường một chút
            keypad.transform.position = extraction.transform.position + Vector3.up * 1.35f + extraction.transform.right * 0.85f - extraction.transform.forward * 0.05f;
            keypad.transform.rotation = extraction.transform.rotation;
        }
        else
        {
            keypad.transform.position = GetRandomNavMeshPos(15f) + Vector3.up * 1.2f;
        }

        BoxCollider bc = keypad.GetComponent<BoxCollider>();
        if (bc == null)
        {
            bc = keypad.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.5f, 0.8f, 0.2f); // Size vừa đủ để dễ click
        }

        if (keypad.GetComponent<EscapeCipher>() == null)
            keypad.AddComponent<EscapeCipher>();
            
        Debug.Log($"[EscapeManager] Đã đính Keypad Cipher vào tường tại {keypad.transform.position}");
    }

    void SpawnReactor()
    {
        GameObject prefab = Resources.Load<GameObject>("EscapeAssets/Mesh_Reactor");
        GameObject reactor;
        
        if (prefab != null)
        {
            reactor = Instantiate(prefab);
            reactor.name = "EscapeReactor";
        }
        else
        {
            reactor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            reactor.name = "EscapeReactor_Fallback";
            reactor.transform.localScale = new Vector3(3.5f, 3.5f, 3.5f); 
            
            Renderer rend = reactor.GetComponent<Renderer>();
            if (rend != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.35f, 0.35f, 0.38f);
                rend.material = mat;
            }
        }

        Vector3 spawnPos = GetRandomNavMeshPos(45f);
        // Bù đắp độ cao (Reactor scale 2x nên cao khoảng 2m, offset 1m)
        reactor.transform.position = spawnPos + (prefab != null ? Vector3.up * 1f : Vector3.up * 3.5f);

        // BoxCollider to để dễ interact
        BoxCollider bc = reactor.GetComponent<BoxCollider>() ?? reactor.AddComponent<BoxCollider>();
        bc.size = new Vector3(2.5f, 2.5f, 2.5f);
        bc.center = new Vector3(0, 1.25f, 0);

        if (reactor.GetComponent<EscapeReactor>() == null)
            reactor.AddComponent<EscapeReactor>();
            
        Debug.Log($"[EscapeManager] Đã spawn Lò phản ứng khổng lồ tại {reactor.transform.position}");
    }

    Vector3 GetRandomNavMeshPos(float radius)
    {
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        Vector3 center = playerGO != null ? playerGO.transform.position : Vector3.zero;

        for (int i = 0; i < 30; i++)
        {
            Vector3 rand = Random.insideUnitSphere * radius;
            rand.y = 0f;
            // Ép xa player ít nhất 15m để không spawn ngay trước mặt
            if (Vector3.Distance(center, center + rand) < 15f) continue; 

            if (UnityEngine.AI.NavMesh.SamplePosition(center + rand, out UnityEngine.AI.NavMeshHit hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
            {
                return hit.position;
            }
        }
        return center;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Method script gọi hàm này khi player hoàn thành yêu cầu thoát.</summary>
    public void UnlockEscape()
    {
        if (IsEscapeUnlocked) return;
        IsEscapeUnlocked = true;
        ReportProgress("✓ Điều kiện thoát đã xong! Đến Cửa Thoát Hiểm ngay!", 1f);
        Debug.Log("<color=lime>[EscapeManager] ✓ ESCAPE UNLOCKED!</color>");
        OnEscapeUnlocked?.Invoke();
    }

    /// <summary>Method script gọi để cập nhật tiến độ hiển thị trên HUD.</summary>
    public void ReportProgress(string message, float progress01)
    {
        ProgressMessage = message;
        ProgressValue   = Mathf.Clamp01(progress01);
    }

    // ── Info Helpers ──────────────────────────────────────────────────────────

    public string GetMethodName()
    {
        return CurrentMethod switch
        {
            EscapeMethodType.Assembly => "Lắp Ráp Cửa Thoát",
            EscapeMethodType.Beacon   => "Phát Tín Hiệu Cứu Hộ",
            EscapeMethodType.Cipher   => "Giải Mã Cửa Thoát",
            EscapeMethodType.Reactor  => "Tắt Lò Phản Ứng",
            _                         => "???",
        };
    }

    public string GetMethodInstruction()
    {
        return CurrentMethod switch
        {
            EscapeMethodType.Assembly => "Thu thập 3 bộ phận rải trên bản đồ",
            EscapeMethodType.Beacon   => "Bấm [E] vào Beacon → Sống sót 3 phút",
            EscapeMethodType.Cipher   => "Tìm 2 mảnh ghi chú → Nhập mật mã vào bàn phím",
            EscapeMethodType.Reactor  => "Bấm [E] vào Lò Phản Ứng để tắt nó",
            _                         => "",
        };
    }

    public string GetMethodDetailedDescription()
    {
        return CurrentMethod switch
        {
            EscapeMethodType.Assembly => "• Cơ chế: Hệ thống rải 3 bộ phận ngẫu nhiên trên bản đồ.\n• Mục tiêu: Tìm đủ 3 bộ phận lơ lửng phát sáng (Bánh răng, Bình nhiên liệu, Bo mạch).\n• Tương tác: Lại gần và bấm [E] để nhặt.\n• Hoàn thành: Nhặt đủ 3/3 bộ phận, cửa thoát mở.",
            EscapeMethodType.Beacon   => "• Cơ chế: Trạm Ăng-ten phát tín hiệu sinh ra ngẫu nhiên.\n• Yêu cầu: Cần 2 Circuit + 1 Battery để khởi động.\n• Mục tiêu: Nạp nguyên liệu bằng phím [E], sau đó sống sót chạy trốn Mimic trong 3 phút.\n• Hoàn thành: Đếm ngược về 0, trực thăng đến, cửa thoát mở.",
            EscapeMethodType.Cipher   => "• Cơ chế: Bàn phím số đính bên phải cửa thoát. 2 mảnh ghi chú giấu ngẫu nhiên.\n• Mục tiêu: Tìm 2 mảnh ghi chú để biết 4 số mật mã. Nhập vào bàn phím [E].\n• Hình phạt: Nhập sai sẽ bị giật điện trừ 15 HP.\n• Hoàn thành: Nhập đúng 4 số, cửa thoát mở ngay lập tức.",
            EscapeMethodType.Reactor  => "• Cơ chế: Lò Phản Ứng khổng lồ báo động đỏ xuất hiện.\n• Yêu cầu: Cần 3 Chemical + 2 Circuit để tắt lò.\n• Mục tiêu: Lại gần lò, bấm [E] để tắt. Chờ 3 giây cho máy dừng hẳn.\n• Hoàn thành: Cửa thoát mở + Toàn bản đồ thành Safe Zone (dừng tụt Oxy).",
            _                         => "",
        };
    }
}
