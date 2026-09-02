using UnityEngine;
using System.Collections;

/// <summary>
/// Phương thức Beacon: Bấm [E] để xây beacon (tốn scrap) → đếm ngược 3 phút sống sót.
/// Trong thời gian đếm ngược, cần tránh Mimic cho đến khi "đội cứu hộ" đến.
///
/// SETUP:
///   1. Tạo GameObject "EscapeBeacon" trong Scene, đặt INACTIVE.
///   2. Dùng Cylinder primitive (height ~2m) cho hình dáng antenna.
///   3. Gắn script này vào. Tuỳ chọn: kéo Light, AudioSource vào.
///   4. EscapeManager sẽ tự bật nếu màn này chọn Beacon.
/// </summary>
public class EscapeBeacon : MonoBehaviour, IInteractable
{
    [Header("Chi phí xây dựng")]
    public int requiredCircuits  = 2;
    public int requiredBatteries = 1;

    [Header("Đếm ngược")]
    [Tooltip("Thời gian sống sót sau khi bật beacon (giây)")]
    public float countdownSeconds = 180f;

    [Header("Đèn Beacon")]
    public Light beaconLight;
    public Color activeColor = new Color(0.1f, 0.75f, 1f);

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip   buildClip;
    public AudioClip   rescueClip;
    public AudioClip   pingClip;

    // ── State ─────────────────────────────────────────────────────────────────
    private bool  _isBuilt    = false;
    private bool  _isDone     = false;
    public float _remaining = 0f;

    // OnGUI message
    private string _msg      = "";
    private Color  _msgColor = Color.white;
    private float  _msgTimer = 0f;

    public bool isUIOpen = false;
    private PlayerInventory _currentInv;
    
    private float _pulseTimer = 0f;
    private Texture2D _hazardTex;
    private Texture2D _blackTex;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        _remaining = countdownSeconds;

        // Tìm đèn trong con trước khi tạo mới
        if (beaconLight == null)
        {
            beaconLight = GetComponentInChildren<Light>();
        }

        if (beaconLight == null)
        {
            GameObject lg = new GameObject("BeaconLight");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = Vector3.up * 1.2f;
            beaconLight = lg.AddComponent<Light>();
            beaconLight.type      = LightType.Point;
            beaconLight.color     = Color.grey;
            beaconLight.intensity = 0.6f;
            beaconLight.range     = 6f;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void Update()
    {
        if (isUIOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            isUIOpen = false;
        }

        if (_msgTimer > 0) _msgTimer -= Time.deltaTime;
        if (!_isBuilt || _isDone) return;

        // Nhấp nháy đèn xanh
        float pulse = Mathf.Sin(Time.time * (2f + _remaining < 30f ? 6f : 2f)) * 0.5f + 1f;
        if (beaconLight != null)
        {
            beaconLight.color     = activeColor;
            beaconLight.intensity = pulse * 2.5f;
        }

        // Đếm ngược
        _remaining -= Time.deltaTime;
        float progress = 1f - (_remaining / countdownSeconds);
        EscapeManager.Instance?.ReportProgress(
            $"Sống sót thêm: {FormatTime(Mathf.Max(0, _remaining))}  ←  Đội cứu hộ đang đến",
            progress);

        // Báo động thu hút Mimic mỗi 5 giây
        _pulseTimer += Time.deltaTime;
        if (_pulseTimer >= 5f)
        {
            _pulseTimer = 0f;
            
            // Phát âm thanh ping
            if (audioSource != null)
            {
                if (pingClip == null) pingClip = Resources.Load<AudioClip>("SFX/Click");
                if (pingClip != null) audioSource.PlayOneShot(pingClip);
            }

            ExilerAI[] exilers = FindObjectsByType<ExilerAI>(FindObjectsSortMode.None);
            foreach (var e in exilers) e.ForceInvestigate(transform.position);

            MutantAI[] mutants = FindObjectsByType<MutantAI>(FindObjectsSortMode.None);
            foreach (var m in mutants) m.ForceInvestigate(transform.position);
        }

        if (_remaining <= 0f) StartCoroutine(RescueArrived());
    }

    // ── IInteractable ─────────────────────────────────────────────────────────

    public void Interact(GameObject interactor)
    {
        if (_isBuilt)
        {
            ShowMsg("Beacon đang phát tín hiệu...", new Color(0.1f, 0.8f, 1f));
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

    // ── Build + Countdown ─────────────────────────────────────────────────────

    public void ForceBuild()
    {
        if (_isBuilt) return;
        _isBuilt = true;
        
        // Đảm bảo object và parent được active để AudioSource và UI có thể hoạt động
        gameObject.SetActive(true);
        if (transform.parent != null) transform.parent.gameObject.SetActive(true);
        
        if (audioSource != null)
        {
            if (buildClip == null) buildClip = Resources.Load<AudioClip>("Audio/alarm");
            if (buildClip != null)
            {
                audioSource.enabled = true;
                audioSource.volume = 1f;
                audioSource.spatialBlend = 1f;
                audioSource.maxDistance = 100f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.clip = buildClip;
                audioSource.loop = true;
                audioSource.Play();
            }
        }
        
        Debug.Log("<color=cyan>[EscapeBeacon] Beacon kích hoạt! Đếm ngược bắt đầu!</color>");
        
        EscapeHUD hud = Object.FindAnyObjectByType<EscapeHUD>(FindObjectsInactive.Include);
        if (hud != null) hud.ForceOpenHUD();
    }

    IEnumerator RescueArrived()
    {
        _isDone = true;

        if (beaconLight != null) beaconLight.color = Color.green;
        if (audioSource != null) { audioSource.Stop(); if (rescueClip != null) audioSource.PlayOneShot(rescueClip); }

        ShowMsg("ĐỘI CỨU HỘ ĐÃ ĐẾN! Đến cửa thoát ngay!", Color.green);
        Debug.Log("<color=lime>[EscapeBeacon] Đội cứu hộ đến! Escape unlocked!</color>");

        EscapeManager.Instance?.UnlockEscape();
        yield break;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static string FormatTime(float s)
        => $"{(int)(s / 60):00}:{(int)(s % 60):00}";

    void ShowMsg(string msg, Color color) { _msg = msg; _msgColor = color; _msgTimer = 4f; }

    void OnGUI()
    {
        DrawHUDMessages();
        if (isUIOpen) DrawBeaconUI();

        if (_isBuilt && !_isDone)
        {
            DrawAlarmUI();
        }
    }

    void CreateTextures()
    {
        if (_blackTex == null)
        {
            _blackTex = new Texture2D(1, 1);
            _blackTex.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.05f, 0.95f));
            _blackTex.Apply();
        }
        if (_hazardTex == null)
        {
            int size = 32;
            _hazardTex = new Texture2D(size, size);
            _hazardTex.wrapMode = TextureWrapMode.Repeat;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isYellow = ((x + y) % size) < (size / 2);
                    _hazardTex.SetPixel(x, y, isYellow ? new Color(1f, 0.85f, 0f) : new Color(0.1f, 0.1f, 0.1f));
                }
            }
            _hazardTex.Apply();
        }
    }

    void DrawAlarmUI()
    {
        CreateTextures();

        float pulse = Mathf.PingPong(Time.time * 3f, 1f);
        
        float panelW = Mathf.Min(Screen.width * 0.7f, 800f);
        float panelH = 160f; // Tăng chiều cao để chữ vừa vặn bên trong
        float panelX = (Screen.width - panelW) / 2f;
        float panelY = Screen.height * 0.1f;
        
        // 1. Red Neon Glow effect
        Color neonColor = new Color(1f, 0f, 0f, 0.05f + 0.1f * pulse);
        for (int i = 1; i <= 6; i++)
        {
            GUI.color = neonColor;
            float expand = i * 6f;
            GUI.DrawTexture(new Rect(panelX - expand, panelY - expand, panelW + expand * 2, panelH + expand * 2), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;

        // 2. Black Background Panel
        GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), _blackTex);

        // 3. Hazard Stripes Border
        float borderThickness = 26f; // Dòng sọc to ra
        float repeatX = panelW / 32f;
        float offset = -Time.time * 0.8f; // Scrolling effect
        
        // Cạnh trên
        GUI.DrawTextureWithTexCoords(
            new Rect(panelX, panelY, panelW, borderThickness), 
            _hazardTex, 
            new Rect(offset, 0, repeatX, borderThickness / 32f)
        );
        // Cạnh dưới
        GUI.DrawTextureWithTexCoords(
            new Rect(panelX, panelY + panelH - borderThickness, panelW, borderThickness), 
            _hazardTex, 
            new Rect(offset, 0, repeatX, borderThickness / 32f)
        );

        // 4. Text Content (Bright Reddish-Pink)
        GUIStyle s = new GUIStyle();
        s.fontSize = 36;
        s.fontStyle = FontStyle.Bold;
        s.alignment = TextAnchor.MiddleCenter;

        string alarmText = $"BẦY QUÁI VẬT ĐANG TỚI\nCỨU HỘ ĐẾN SAU: {FormatTime(Mathf.Max(0, _remaining))}";

        // Đổ bóng chữ đen
        s.normal.textColor = Color.black;
        GUI.Label(new Rect(panelX + 2f, panelY + 2f, panelW, panelH), alarmText, s);
        
        // Chữ màu Đỏ Hồng (Reddish-Pink) có chớp nháy
        s.normal.textColor = new Color(1f, 0.2f, 0.4f, Mathf.Lerp(0.8f, 1f, pulse));
        GUI.Label(new Rect(panelX, panelY, panelW, panelH), alarmText, s);
    }

    void DrawHUDMessages()
    {
        if (_msgTimer <= 0) return;
        GUIStyle s = new GUIStyle { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        s.normal.textColor = Color.black;
        GUI.Label(new Rect(2f, Screen.height * 0.65f + 2, Screen.width, 50), _msg, s);
        s.normal.textColor = _msgColor;
        GUI.Label(new Rect(0f, Screen.height * 0.65f,     Screen.width, 50), _msg, s);
    }

    void DrawBeaconUI()
    {
        float width = 600;
        float height = 400;
        Rect windowRect = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);

        GUI.Box(windowRect, "");

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 32;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.UpperCenter;
        titleStyle.normal.textColor = new Color(0.1f, 0.8f, 1f);

        GUI.Label(new Rect(windowRect.x, windowRect.y + 20, windowRect.width, 50), "TRẠM ĂNG-TEN CỨU HỘ", titleStyle);

        GUIStyle descStyle = new GUIStyle(GUI.skin.label);
        descStyle.fontSize = 20;
        descStyle.alignment = TextAnchor.UpperCenter;
        descStyle.normal.textColor = Color.white;

        bool hasRes = _currentInv != null && _currentInv.HasResources(requiredCircuits, 0, 0, 0, 0, requiredBatteries);

        string desc = hasRes 
            ? $"Đã đủ nguyên liệu ({requiredCircuits} Circuit, {requiredBatteries} Battery).\nNhấn nút KÍCH HOẠT để gọi cứu hộ!" 
            : $"Bạn chưa đủ nguyên liệu!\nYêu cầu: {requiredCircuits} Circuit, {requiredBatteries} Battery\nHiện có: {_currentInv.circuits} Circuit, {_currentInv.scrapBatteries} Battery.";

        GUI.Label(new Rect(windowRect.x + 50, windowRect.y + 100, windowRect.width - 100, 100), desc, descStyle);

        // Nút kích hoạt
        GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
        btnStyle.fontSize = 24;
        btnStyle.fontStyle = FontStyle.Bold;

        GUI.enabled = hasRes;

        if (GUI.Button(new Rect(windowRect.x + 150, windowRect.y + 220, 300, 60), "KÍCH HOẠT BEACON", btnStyle))
        {
            if (_currentInv != null)
            {
                _currentInv.ConsumeResources(requiredCircuits, 0, 0, 0, 0, requiredBatteries);
                _currentInv.SyncEscapeEventServerRpc(1); // 1 = Beacon Build
            }
            isUIOpen = false;
        }

        GUI.enabled = true;

        if (GUI.Button(new Rect(windowRect.x + 250, windowRect.y + 320, 100, 40), "ĐÓNG", GUI.skin.button))
        {
            isUIOpen = false;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = _isBuilt ? new Color(0.1f, 0.8f, 1f, 0.6f) : new Color(0.5f, 0.5f, 0.5f, 0.4f);
        Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(0.7f, 2f, 0.7f));
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.5f,
            _isBuilt ? $"[BEACON ON]  {FormatTime(_remaining)}" : $"[BEACON OFF]  {requiredCircuits}x Circuit + {requiredBatteries}x Battery");
    }
#endif
}
