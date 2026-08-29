using UnityEngine;
using Unity.Netcode;

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
public class EscapeManager : NetworkBehaviour
{
    public static EscapeManager Instance { get; private set; }

    // ── State ────────────────────────────────────────────────────────────────
    [Header("DEBUG TESTING")]
    [Tooltip("Tích vào đây nếu bạn muốn ép game luôn ra 1 nhiệm vụ nhất định để test")]
    public bool forceSpecificMethod = false;
    [Tooltip("Chọn nhiệm vụ bạn muốn test (chỉ có tác dụng khi tick ô trên)")]
    public EscapeMethodType specificMethodToForce;

    public EscapeMethodType CurrentMethod    { get; private set; }
    
    public NetworkVariable<bool> isEscapeUnlockedNet = new NetworkVariable<bool>(false);
    public bool             IsEscapeUnlocked { get { return isEscapeUnlockedNet.Value; } private set { } }
    
    public bool             IsReadyToAssemble { get; set; } = false;
    public string           ProgressMessage  { get; private set; } = "";
    public float            ProgressValue    { get; private set; } = 0f;

    // ── Events ────────────────────────────────────────────────────────────────
    public event System.Action OnEscapeUnlocked;

    // ─────────────────────────────────────────────────────────────────────────

    public NetworkVariable<bool> isBeaconBuiltNet = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> isReactorShutdownNet = new NetworkVariable<bool>(false);
    public NetworkVariable<int> assembleStepsNet = new NetworkVariable<int>(0);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        isEscapeUnlockedNet.OnValueChanged += (oldVal, newVal) =>
        {
            if (!oldVal && newVal)
            {
                ReportProgress("✓ Escape conditions met! Head to the Escape Door now!", 1f);
                Debug.Log("<color=lime>[EscapeManager] ✓ ESCAPE UNLOCKED!</color>");
                OnEscapeUnlocked?.Invoke();
            }
        };

        // Đồng bộ muộn cho Late Joiner
        isBeaconBuiltNet.OnValueChanged += (oldVal, newVal) => {
            if (newVal) Object.FindAnyObjectByType<EscapeBeacon>()?.ForceBuild();
        };
        isReactorShutdownNet.OnValueChanged += (oldVal, newVal) => {
            if (newVal) Object.FindAnyObjectByType<EscapeReactor>()?.ForceShutdown();
        };
        assembleStepsNet.OnValueChanged += (oldVal, newVal) => {
            if (newVal > oldVal) {
                var ex = Object.FindAnyObjectByType<ExtractionSystem>();
                if (ex != null) for(int i=0; i<(newVal - oldVal); i++) ex.ForceAssembleStep();
            }
        };
        
        // Kích hoạt ngay nếu trạng thái đã hoàn thành lúc join
        if (isBeaconBuiltNet.Value) Object.FindAnyObjectByType<EscapeBeacon>()?.ForceBuild();
        if (isReactorShutdownNet.Value) Object.FindAnyObjectByType<EscapeReactor>()?.ForceShutdown();
        if (assembleStepsNet.Value > 0) {
            var ex = Object.FindAnyObjectByType<ExtractionSystem>();
            if (ex != null) for(int i=0; i<assembleStepsNet.Value; i++) ex.ForceAssembleStep();
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    System.Collections.IEnumerator Start()
    {
        // QUAN TRỌNG: Đợi lấy Seed chung từ Host trước khi chọn Nhiệm vụ
        while (PlayerInventory.GlobalMatchSeed == 0) yield return null;
        
        yield return new UnityEngine.WaitForSeconds(2f);

        System.Random rng = new System.Random(PlayerInventory.GlobalMatchSeed);
        CurrentMethod = forceSpecificMethod ? specificMethodToForce : (EscapeMethodType)rng.Next(0, 4);
        Debug.Log($"<color=cyan>[EscapeManager] Đã random nhiệm vụ màn này: <b>{GetMethodName()}</b> (Seed: {PlayerInventory.GlobalMatchSeed})</color>");

        // Chờ đến khi Player được spawn (đặc biệt trong game Network)
        while (GameObject.FindGameObjectWithTag("Player") == null)
        {
            yield return null;
        }

        // QUAN TRỌNG: Đợi thêm 2 giây để Player rơi xuống chạm đất hoàn toàn!
        // Tránh tình trạng Player vừa spawn ở tít trên trời (Y = 9), làm đồ vật cũng bị spawn theo trên trời!
        yield return new WaitForSeconds(2f);

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

        Vector3 spawnPos = GetRandomNavMeshPos(25f);
        beacon.transform.position = spawnPos;

        // Tự động tính toán đáy của object để nhấc nó lên vừa khít mặt đất
        Renderer[] renderers = beacon.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float bottomY = bounds.min.y;
            float offset = beacon.transform.position.y - bottomY;
            beacon.transform.position = spawnPos + Vector3.up * offset;
        }
        else
        {
            beacon.transform.position = spawnPos + (prefab != null ? Vector3.up * 0.5f : Vector3.up * 2.5f);
        }

        // Thêm BoxCollider to một chút để dễ interact (vì ăng-ten mỏng)
        // Lưu ý: chia cho localScale để tránh trường hợp FBX có scale 100 làm collider to bằng cả bản đồ
        BoxCollider bc = beacon.GetComponent<BoxCollider>();
        if (bc == null) bc = beacon.AddComponent<BoxCollider>();
        Vector3 ls = beacon.transform.localScale;
        bc.size = new Vector3(2f / ls.x, 2f / ls.y, 2f / ls.z);
        bc.center = new Vector3(0, 1f / ls.y, 0);
        bc.isTrigger = true; // Sửa thành Trigger để tuyệt đối không đẩy player đi
        
        Rigidbody[] rbs = beacon.GetComponentsInChildren<Rigidbody>();
        foreach (var r in rbs) r.isKinematic = true;

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
            keypad.transform.position = GetRandomNavMeshPos(20f) + Vector3.up * 1.2f;
        }

        BoxCollider bc = keypad.GetComponent<BoxCollider>();
        if (bc == null)
        {
            bc = keypad.AddComponent<BoxCollider>();
        }
        Vector3 lsKeypad = keypad.transform.localScale;
        bc.size = new Vector3(0.5f / lsKeypad.x, 0.8f / lsKeypad.y, 0.2f / lsKeypad.z); // Size vừa đủ để dễ click
        bc.isTrigger = true;
        
        Rigidbody[] rbsKeypad = keypad.GetComponentsInChildren<Rigidbody>();
        foreach (var r in rbsKeypad) r.isKinematic = true;

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

        Vector3 spawnPos = GetRandomNavMeshPos(30f);
        reactor.transform.position = spawnPos;

        Renderer[] renderers = reactor.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float bottomY = bounds.min.y;
            float offset = reactor.transform.position.y - bottomY;
            reactor.transform.position = spawnPos + Vector3.up * offset;
        }
        else
        {
            reactor.transform.position = spawnPos + (prefab != null ? Vector3.up * 1f : Vector3.up * 3.5f);
        }

        // BoxCollider to để dễ interact (chia cho localScale để tránh phình to)
        BoxCollider bc = reactor.GetComponent<BoxCollider>();
        if (bc == null) bc = reactor.AddComponent<BoxCollider>();
        Vector3 lsReactor = reactor.transform.localScale;
        bc.size = new Vector3(2.5f / lsReactor.x, 2.5f / lsReactor.y, 2.5f / lsReactor.z);
        bc.center = new Vector3(0, 1.25f / lsReactor.y, 0);
        bc.isTrigger = true;
        
        Rigidbody[] rbsReactor = reactor.GetComponentsInChildren<Rigidbody>();
        foreach (var r in rbsReactor) r.isKinematic = true;

        if (reactor.GetComponent<EscapeReactor>() == null)
            reactor.AddComponent<EscapeReactor>();
            
        Debug.Log($"[EscapeManager] Đã spawn Lò phản ứng khổng lồ tại {reactor.transform.position}");
    }

    Vector3 GetRandomNavMeshPos(float minDistance)
    {
        Vector3 center = Vector3.zero;
        System.Random rng = new System.Random(PlayerInventory.GlobalMatchSeed + 1234);

        int maxAttempts = 100;
        Vector3 bestPos = center;
        float bestDist = -1f;

        for (int i = 0; i < maxAttempts; i++)
        {
            float x = (float)(rng.NextDouble() * 300.0 - 150.0);
            float z = (float)(rng.NextDouble() * 300.0 - 150.0);
            Vector3 randomPos = new Vector3(x, 0, z);

            if (UnityEngine.AI.NavMesh.SamplePosition(randomPos, out UnityEngine.AI.NavMeshHit hit, 50f, UnityEngine.AI.NavMesh.AllAreas))
            {
                if (Mathf.Abs(hit.position.y - center.y) > 4f) continue;
                
                float dist = Vector3.Distance(center, hit.position);
                if (dist >= minDistance)
                {
                    return hit.position;
                }

                if (dist > bestDist)
                {
                    bestDist = dist;
                    bestPos = hit.position;
                }
            }
        }
        return bestPos != center ? bestPos : center + new Vector3(minDistance, 0, minDistance);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Method script gọi hàm này khi player hoàn thành yêu cầu thoát.</summary>
    public void UnlockEscape()
    {
        if (IsServer)
        {
            if (isEscapeUnlockedNet.Value) return;
            isEscapeUnlockedNet.Value = true;
        }
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
            EscapeMethodType.Assembly => "Assemble Escape Door",
            EscapeMethodType.Beacon   => "Activate Rescue Beacon",
            EscapeMethodType.Cipher   => "Decode Escape Door",
            EscapeMethodType.Reactor  => "Disable Reactor",
            _                         => "???",
        };
    }

    public string GetMethodInstruction()
    {
        return CurrentMethod switch
        {
            EscapeMethodType.Assembly => "Collect 3 parts scattered on the map",
            EscapeMethodType.Beacon   => "Press [E] on Beacon → Survive for 3 minutes",
            EscapeMethodType.Cipher   => "Find 2 notes → Enter passcode on keypad",
            EscapeMethodType.Reactor  => "Press [E] on the Reactor to disable it",
            _                         => "",
        };
    }

    public string GetMethodDetailedDescription()
    {
        return CurrentMethod switch
        {
            EscapeMethodType.Assembly => "• Mechanic: 3 parts are scattered randomly on the map.\n• Goal: Find 3 glowing floating parts (Gear, Fuel Tank, Circuit Board).\n• Interact: Approach and press [E] to pick up.\n• Completion: Collect 3/3 parts, escape door opens.",
            EscapeMethodType.Beacon   => "• Mechanic: Rescue Antenna spawns randomly.\n• Requirement: Needs 2 Circuits + 1 Battery to start.\n• Goal: Insert materials with [E], then survive against the Mimic for 3 minutes.\n• Completion: Timer reaches 0, helicopter arrives, escape door opens.",
            EscapeMethodType.Cipher   => "• Mechanic: Numpad attached to the right of the escape door. 2 notes hidden randomly.\n• Goal: Find 2 notes to reveal a 4-digit code. Enter it into the keypad [E].\n• Penalty: Wrong code will shock you for -15 HP.\n• Completion: Enter correct 4 digits, escape door opens instantly.",
            EscapeMethodType.Reactor  => "• Mechanic: Giant red-alert Reactor appears.\n• Requirement: Needs 3 Chemicals + 2 Circuits to access the core.\n• Goal: Press [E] to insert materials, causing a meltdown overload reaction.\n• Warning: Reactor counts down 10s and EXPLODES. If you are within 50m, you DIE.\n• Completion: Reactor explodes releasing energy → Escape door opens.",
            _                         => "",
        };
    }

    [Unity.Netcode.ClientRpc]
    public void TriggerBeaconBuildClientRpc() { }

    [Unity.Netcode.ClientRpc]
    public void TriggerReactorShutdownClientRpc() { }

    [Unity.Netcode.ClientRpc]
    public void TriggerAssembleStepClientRpc() { }
}
