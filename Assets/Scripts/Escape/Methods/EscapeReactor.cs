using UnityEngine;
using System.Collections;

/// <summary>
/// Phương thức Reactor: Tắt lò phản ứng bằng scrap.
/// Sau khi tắt: Oxygen không cạn nữa → Cửa thoát mở.
///
/// SETUP:
///   1. Tạo GameObject "EscapeReactor" trong Scene (dùng Cylinder/Cube lớn).
///   2. Thêm Collider để Raycast bắt được.
///   3. Gắn script này vào. Tuỳ chọn: kéo Light, AudioSource vào.
///   4. Đặt ở vị trí trung tâm bản đồ (nơi nguy hiểm nhất).
///   5. EscapeManager sẽ tự bật nếu màn này chọn Reactor.
/// </summary>
public class EscapeReactor : MonoBehaviour, IInteractable
{
    [Header("Chi phí tắt lò")]
    public int requiredChemicals = 3;
    public int requiredCircuits  = 2;

    [Header("Đèn Lò")]
    public Light reactorLight;
    public Color dangerColor   = new Color(1f,   0.12f, 0.05f);  // đỏ nguy hiểm
    public Color shutdownColor = new Color(0.1f, 0.65f, 0.2f);   // xanh lá an toàn

    [Header("Hiệu ứng Nổ Lò (Meltdown)")]
    public float meltdownTime    = 10f;
    public float explosionRadius = 50f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip   shutdownClip;
    public AudioClip   alarmClip;
    public AudioClip   explosionClip;

    // ── State ─────────────────────────────────────────────────────────────────
    private bool  _isShutdown = false;
    private bool  _isMeltdown = false;
    private float _meltdownTimer = 0f;

    // OnGUI message
    private string _msg = ""; private Color _msgColor; private float _msgTimer;

    public bool isUIOpen = false;
    private PlayerInventory _currentInv;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        // Tìm đèn trong con trước khi tạo mới
        if (reactorLight == null)
        {
            reactorLight = GetComponentInChildren<Light>();
        }

        if (reactorLight == null)
        {
            GameObject lg = new GameObject("ReactorLight");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = Vector3.up * 1.2f;
            reactorLight = lg.AddComponent<Light>();
            reactorLight.type      = LightType.Point;
            reactorLight.range     = 10f;
            reactorLight.intensity = 3f;
        }
        reactorLight.color = dangerColor;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }

        EscapeManager.Instance?.ReportProgress(
            $"Tắt lò phản ứng (cần {requiredChemicals} Chemical + {requiredCircuits} Circuit)", 0f);
    }

    void Update()
    {
        if (isUIOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            isUIOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (_msgTimer > 0) _msgTimer -= Time.deltaTime;
        if (_isShutdown) return;

        if (_isMeltdown)
        {
            _meltdownTimer -= Time.deltaTime;
            
            // Nhấp nháy đèn đỏ cực nhanh theo nhịp tim
            float pulse = Mathf.PingPong(Time.time * (10f + (meltdownTime - _meltdownTimer)), 1f);
            if (reactorLight != null) reactorLight.intensity = pulse * 5f;
            return;
        }

        // Nhấp nháy đèn đỏ bình thường khi chưa nạp
        float idlePulse = Mathf.Sin(Time.time * 2.8f) * 0.5f + 1f;
        if (reactorLight != null) reactorLight.intensity = idlePulse * 3.2f;
    }

    // ── IInteractable ─────────────────────────────────────────────────────────

    public void Interact(GameObject interactor)
    {
        if (_isShutdown)
        {
            ShowMsg("Lò đã được tắt.", Color.green);
            return;
        }

        PlayerInventory inv = interactor.GetComponentInParent<PlayerInventory>()
                           ?? interactor.GetComponentInChildren<PlayerInventory>();
        if (inv == null) return;

        isUIOpen = true;
        _currentInv = inv;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ── Shutdown Animation ────────────────────────────────────────────────────

    public void ForceShutdown()
    {
        if (_isShutdown || _isMeltdown) return;
        StartCoroutine(ShutdownSequence());
    }

    IEnumerator ShutdownSequence()
    {
        _isMeltdown = true;
        _meltdownTimer = meltdownTime;
        
        ShowMsg("CẢNH BÁO QUÁ TẢI!", Color.red);
        EscapeManager.Instance?.ReportProgress("LÒ SẮP NỔ! CHẠY NGAY!", 0.9f);

        if (audioSource != null && shutdownClip != null)
            audioSource.PlayOneShot(shutdownClip);

        if (alarmClip == null) alarmClip = Resources.Load<AudioClip>("Audio/alarm");
        float nextBeep = _meltdownTimer;
        
        // Chờ đếm ngược
        while (_meltdownTimer > 0)
        {
            if (_meltdownTimer <= nextBeep)
            {
                if (audioSource != null && alarmClip != null) audioSource.PlayOneShot(alarmClip);
                nextBeep -= 1f; // kêu mỗi 1 giây
            }
            yield return null;
        }

        // --- BÙM! PHÁT NỔ ---
        if (explosionClip == null) explosionClip = Resources.Load<AudioClip>("Audio/explosion");
        if (audioSource != null && explosionClip != null) audioSource.PlayOneShot(explosionClip);

        _isMeltdown = false;
        _isShutdown = true;
        
        // Tạo hiệu ứng nổ (Shockwave Sphere)
        GameObject shockwave = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shockwave.transform.position = transform.position;
        Destroy(shockwave.GetComponent<Collider>());
        
        Renderer rend = shockwave.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            // Set Transparent Mode
            mat.SetFloat("_Surface", 1); 
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            
            mat.color = new Color(1f, 0.4f, 0f, 0.8f); // Cam đỏ
            mat.SetColor("_EmissionColor", new Color(2f, 0.5f, 0f));
            mat.EnableKeyword("_EMISSION");
            rend.material = mat;
        }

        ExplosionEffect fx = shockwave.AddComponent<ExplosionEffect>();
        fx.maxRadius = explosionRadius;

        // Tính toán sát thương nổ với Player
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            foreach (GameObject player in players)
            {
                if (player != null)
                {
                    float dist = Vector3.Distance(transform.position, player.transform.position);
                    if (dist <= explosionRadius)
                    {
                        PlayerSurvival ps = player.GetComponent<PlayerSurvival>();
                        if (ps != null) ps.TakeDamage(9999f, "Nổ Lò Phản Ứng!");
                    }
                }
            }
        }

        // 1. Tắt Mesh và Collider của lò phản ứng để nó "biến mất"
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach(var r in renderers) r.enabled = false;
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach(var c in colliders) c.enabled = false;
        if (reactorLight != null) reactorLight.enabled = false;

        // 3. Xóa dòng chữ màu xanh (đã bỏ hàm ShowMsg ở đây)
        Debug.Log("<color=lime>[EscapeReactor] Nổ hoàn tất! Lò đã bị phá hủy. Escape unlocked!</color>");

        EscapeManager.Instance?.UnlockEscape();
    }

    // ── OnGUI message ─────────────────────────────────────────────────────────

    void ShowMsg(string msg, Color color) { _msg = msg; _msgColor = color; _msgTimer = 4f; }

    void OnGUI()
    {
        if (_msgTimer > 0)
        {
            GUIStyle s = new GUIStyle { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            s.normal.textColor = Color.black;
            GUI.Label(new Rect(2f, Screen.height * 0.65f + 2, Screen.width, 50), _msg, s);
            s.normal.textColor = _msgColor;
            GUI.Label(new Rect(0f, Screen.height * 0.65f,     Screen.width, 50), _msg, s);
        }

        if (_isMeltdown)
        {
            DrawMeltdownWarning();
        }

        if (isUIOpen) DrawReactorUI();
    }

    void DrawMeltdownWarning()
    {
        float pulse = Mathf.PingPong(Time.time * 5f, 1f);
        
        float panelW = Mathf.Min(Screen.width * 0.6f, 600f);
        float panelH = 100f; 
        float panelX = (Screen.width - panelW) / 2f;
        float panelY = Screen.height * 0.1f;
        
        // Nền đen mờ
        GUI.color = new Color(0.1f, 0f, 0f, 0.9f);
        GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);
        
        // Viền đỏ chớp
        GUI.color = new Color(1f, 0f, 0f, pulse);
        GUI.DrawTexture(new Rect(panelX, panelY, panelW, 4), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(panelX, panelY + panelH - 4, panelW, 4), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle s = new GUIStyle();
        s.fontSize = 32;
        s.fontStyle = FontStyle.Bold;
        s.alignment = TextAnchor.MiddleCenter;

        string alarmText = $"LÒ PHẢN ỨNG QUÁ TẢI\nPHÁT NỔ TRONG: {_meltdownTimer:F1}s";

        s.normal.textColor = Color.black;
        GUI.Label(new Rect(panelX + 2f, panelY + 2f, panelW, panelH), alarmText, s);
        
        s.normal.textColor = new Color(1f, 0.2f, 0.2f, Mathf.Lerp(0.7f, 1f, pulse));
        GUI.Label(new Rect(panelX, panelY, panelW, panelH), alarmText, s);
    }

    void DrawReactorUI()
    {
        float width = 600;
        float height = 400;
        Rect windowRect = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);

        GUI.Box(windowRect, "");

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 32;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.UpperCenter;
        titleStyle.normal.textColor = new Color(1f, 0.4f, 0.1f);

        GUI.Label(new Rect(windowRect.x, windowRect.y + 20, windowRect.width, 50), "LÒ PHẢN ỨNG", titleStyle);

        GUIStyle descStyle = new GUIStyle(GUI.skin.label);
        descStyle.fontSize = 20;
        descStyle.alignment = TextAnchor.UpperCenter;
        descStyle.normal.textColor = Color.white;

        bool hasRes = _currentInv != null && _currentInv.HasResources(requiredCircuits, 0, requiredChemicals);

        string desc = hasRes 
            ? $"Đã đủ nguyên liệu ({requiredCircuits} Circuit, {requiredChemicals} Chemical).\nNhấn nút KÍCH NỔ để phá hủy lõi lò!" 
            : $"Bạn chưa đủ nguyên liệu!\nYêu cầu: {requiredCircuits} Circuit, {requiredChemicals} Chemical\nHiện có: {_currentInv.circuits} Circuit, {_currentInv.chemicals} Chemical.";

        GUI.Label(new Rect(windowRect.x + 50, windowRect.y + 100, windowRect.width - 100, 100), desc, descStyle);

        // Nút kích hoạt
        GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
        btnStyle.fontSize = 24;
        btnStyle.fontStyle = FontStyle.Bold;
        btnStyle.normal.textColor = new Color(1f, 0.3f, 0.3f); // Đỏ nguy hiểm

        GUI.enabled = hasRes;

        if (GUI.Button(new Rect(windowRect.x + 150, windowRect.y + 220, 300, 60), "KÍCH NỔ LÒ PHẢN ỨNG", btnStyle))
        {
            if (_currentInv != null)
            {
                _currentInv.ConsumeResources(requiredCircuits, 0, requiredChemicals);
                _currentInv.SyncEscapeEventServerRpc(2); // Event 2 = Reactor Shutdown
            }
            isUIOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        GUI.enabled = true;

        if (GUI.Button(new Rect(windowRect.x + 250, windowRect.y + 320, 100, 40), "ĐÓNG", GUI.skin.button))
        {
            isUIOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = _isShutdown ? Color.green : new Color(1f, 0.1f, 0.05f, 0.6f);
        Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(1.6f, 2.2f, 1.6f));
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.8f,
            _isShutdown
                ? "[Lò: ĐÃ TẮT]"
                : $"[Lò Phản Ứng]\n{requiredChemicals}x Chemical + {requiredCircuits}x Circuit");
    }
#endif
}

public class ExplosionEffect : MonoBehaviour
{
    public float maxRadius = 50f;
    private float timer = 0f;
    private Renderer rend;
    private Color initialColor;

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null) initialColor = rend.material.color;
    }

    void Update()
    {
        timer += Time.deltaTime * 2.5f; // Tốc độ lan tỏa của vụ nổ
        
        // Vòng tròn to ra dần (cấp số nhân để tạo cảm giác bùng nổ mạnh)
        float currentRadius = Mathf.Lerp(1f, maxRadius * 2f, Mathf.Pow(timer, 1.5f));
        transform.localScale = new Vector3(currentRadius, currentRadius, currentRadius);
        
        // Mờ dần đi
        if (rend != null)
        {
            Color c = initialColor;
            c.a = Mathf.Lerp(initialColor.a, 0f, timer);
            rend.material.color = c;
        }

        if (timer >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
