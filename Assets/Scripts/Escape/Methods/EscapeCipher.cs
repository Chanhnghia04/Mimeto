using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Phương thức Cipher: Tạo mật mã 4 chữ số ngẫu nhiên, spawn 2 ghi chú mỗi chứa 2 chữ số.
/// Script này cũng là "bàn phím cửa thoát" — player bấm [E] để nhập mã.
///
/// SETUP:
///   1. Tạo GameObject "EscapeCipher" (đây là bàn phím cạnh cửa thoát), đặt INACTIVE.
///   2. Gắn BoxCollider để Raycast bắt được.
///   3. Gắn script này vào.
///   4. EscapeManager sẽ tự bật nếu màn này chọn Cipher.
/// </summary>
public class EscapeCipher : MonoBehaviour, IInteractable
{
    [Header("Spawn ghi chú")]
    public float spawnRadius       = 35f;
    public float minDistFromPlayer = 15f;

    [Header("Penalty khi nhập sai")]
    public float wrongCodeDamage = 15f;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private string   _code        = "";
    private bool[]   _notesFound  = { false, false };
    private StringBuilder _input  = new StringBuilder();
    private bool     _keypadOpen  = false;
    private bool     _unlocked    = false;
    private bool     _wrongFlash  = false;
    private float    _wrongTimer  = 0f;
    private PlayerInventory _currentInv;
    private System.Random _rng;
    
    public bool IsKeypadOpen => _keypadOpen;

    // ── Styles ────────────────────────────────────────────────────────────────
    private GUIStyle _panelStyle;
    private GUIStyle _codeStyle;
    private GUIStyle _btnStyle;
    private GUIStyle _btnDisabledStyle;
    private GUIStyle _hintStyle;
    private GUIStyle _subHintStyle;
    private bool     _stylesReady = false;

    // ─────────────────────────────────────────────────────────────────────────

    System.Collections.IEnumerator Start()
    {
        while (PlayerInventory.GlobalMatchSeed == 0) yield return null;
        _rng = new System.Random(PlayerInventory.GlobalMatchSeed + 3001);

        // Tạo mật mã 4 chữ số (dùng _rng để đồng bộ)
        _code = _rng.Next(1000, 9999).ToString();
        Debug.Log($"[EscapeCipher] Mật mã màn này: {_code}  (dev log)");

        // Chờ Player spawn
        while (GameObject.FindGameObjectWithTag("Player") == null) yield return null;

        SpawnNotes();
        UpdateHUD(false);
    }

    // ── Note Spawning ─────────────────────────────────────────────────────────

    void SpawnNotes()
    {
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        Vector3 playerPos   = playerGO != null ? playerGO.transform.position : Vector3.zero;
        List<Vector3> used  = new List<Vector3>();

        for (int i = 0; i < 2; i++)
        {
            Vector3 pos = FindValidPos(playerPos, used);
            used.Add(pos);
            CreateNote(i, pos);
        }
    }

    void CreateNote(int noteIndex, Vector3 worldPos)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = $"CipherNote_{noteIndex}";
        go.transform.position = worldPos + Vector3.up * 0.15f;
        go.transform.rotation = Quaternion.Euler(90f, 0, 0);
        go.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);

        // Material màu cho tờ giấy note
        Renderer rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(1f, 0.93f, 0.15f);
            mat.SetColor("_EmissionColor", new Color(0.55f, 0.45f, 0f));
            mat.EnableKeyword("_EMISSION");
            rend.material = mat;
        }

        // Point light nhỏ
        GameObject lg = new GameObject("NoteLight");
        lg.transform.SetParent(go.transform, false);
        Light l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.9f, 0.25f);
        l.intensity = 1.2f;
        l.range = 2.5f;
        l.shadows = LightShadows.None;

        CipherNote cn = go.GetComponent<CipherNote>();
        if (cn == null) cn = go.AddComponent<CipherNote>();
        cn.noteIndex = noteIndex;
        cn.parentCipher = this;
        // Note 0 = 2 chữ số đầu, Note 1 = 2 chữ số cuối
        cn.digits = noteIndex == 0 ? _code.Substring(0, 2) : _code.Substring(2, 2);

        if (go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>();
        
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);

        Debug.Log($"[EscapeCipher] Spawn ghi chú {noteIndex}: digits={cn.digits} tại {worldPos}");
    }

    Vector3 FindValidPos(Vector3 playerPos, List<Vector3> used)
    {
        UnityEngine.AI.NavMeshTriangulation navData = UnityEngine.AI.NavMesh.CalculateTriangulation();
        if (navData.vertices.Length == 0) return playerPos + Vector3.forward * 20f;

        int triCount = navData.indices.Length / 3;

        // PRE-COMPUTE tất cả random values => chuỗi random không bị lệch bởi Physics
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

        int fallbackCount = Mathf.Min(navData.vertices.Length, 50);
        int[] fallbackIndices = new int[fallbackCount];
        for (int i = 0; i < fallbackCount; i++)
        {
            fallbackIndices[i] = _rng.Next(0, navData.vertices.Length);
        }

        for (int a = 0; a < maxAttempts; a++)
        {
            int t = triIndices[a];
            int v1 = navData.indices[t * 3];
            int v2 = navData.indices[t * 3 + 1];
            int v3 = navData.indices[t * 3 + 2];

            Vector3 pt = Vector3.Lerp(navData.vertices[v1], navData.vertices[v2], lerpA[a]);
            pt = Vector3.Lerp(pt, navData.vertices[v3], lerpB[a]);

            if (Mathf.Abs(pt.y - playerPos.y) > 4f) continue;
            if (Vector3.Distance(pt, playerPos) < minDistFromPlayer) continue;

            bool tooClose = false;
            foreach (var u in used) if (Vector3.Distance(pt, u) < 12f) { tooClose = true; break; }
            if (tooClose) continue;

            if (Physics.CheckSphere(pt + Vector3.up * 0.5f, 0.2f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                continue;

            return pt;
        }
        
        for (int i = 0; i < fallbackCount; i++)
        {
            Vector3 v = navData.vertices[fallbackIndices[i]];
            if (!Physics.CheckSphere(v + Vector3.up * 0.5f, 0.2f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                return v;
        }
        return navData.vertices[fallbackIndices[0]];
    }

    // ── Called by CipherNote ──────────────────────────────────────────────────

    public void OnNoteFound(int noteIndex, string digits)
    {
        if (noteIndex >= 0 && noteIndex < _notesFound.Length)
            _notesFound[noteIndex] = true;

        int found = FoundCount();
        Debug.Log($"[EscapeCipher] Tìm thấy ghi chú {noteIndex}: '{digits}' ({found}/2)");
        UpdateHUD(true);
    }

    int FoundCount()
    {
        int n = 0;
        foreach (bool b in _notesFound) if (b) n++;
        return n;
    }

    /// <summary>Trả về mật mã với ký tự '?' ở vị trí chưa biết.</summary>
    string GetKnownDisplay()
    {
        char[] d = { '?', '?', '?', '?' };
        if (_notesFound[0]) { d[0] = _code[0]; d[1] = _code[1]; }
        if (_notesFound[1]) { d[2] = _code[2]; d[3] = _code[3]; }
        return new string(d);
    }

    void UpdateHUD(bool forceOpen = false)
    {
        if (_unlocked) return;
        int found = FoundCount();
        float prog = found / 2f * 0.8f; // 80% max until entered correctly
        string known = GetKnownDisplay();
        string msg = found < 2
            ? $"Ghi chú ({found}/2) | Mật mã: {known} | Đến bàn phím cửa thoát"
            : $"Mật mã đầy đủ: {known} | Đến bàn phím cửa thoát để nhập!";
        EscapeManager.Instance?.ReportProgress(msg, prog);

        if (forceOpen)
        {
            // Tự động bật màn hình HUD bên phải lên để user thấy mã vừa nhặt
            EscapeHUD hud = Object.FindAnyObjectByType<EscapeHUD>();
            if (hud != null) hud.ForceOpenHUD();
        }
    }

    // ── IInteractable: đây là bàn phím cạnh cửa thoát ────────────────────────

    public void Interact(GameObject interactor)
    {
        if (_unlocked) { Debug.Log("[EscapeCipher] Đã mở rồi."); return; }

        PlayerInventory inv = interactor.GetComponentInParent<PlayerInventory>()
                           ?? interactor.GetComponentInChildren<PlayerInventory>();
        if (inv != null) _currentInv = inv;

        _keypadOpen = !_keypadOpen;
        _input.Clear();

        Cursor.lockState = _keypadOpen ? CursorLockMode.None    : CursorLockMode.Locked;
        Cursor.visible   = _keypadOpen;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (_wrongTimer > 0) { _wrongTimer -= Time.deltaTime; if (_wrongTimer <= 0) _wrongFlash = false; }

        if (_keypadOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            _keypadOpen = false;
            _input.Clear();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }
    }

    // ── OnGUI: Bàn phím số ────────────────────────────────────────────────────

    void OnGUI()
    {
        if (!_keypadOpen) return;
        EnsureStyles();

        const float pw = 280f, ph = 380f;
        float px = (Screen.width  - pw) / 2f;
        float py = (Screen.height - ph) / 2f;

        // Background panel
        GUI.Box(new Rect(px, py, pw, ph), GUIContent.none, _panelStyle);

        // Tiêu đề
        GUI.Label(new Rect(px + 10, py + 10, pw - 20, 28), "🔒  NHẬP MẬT MÃ CỬA THOÁT", _hintStyle);

        // Gợi ý mật mã đã biết
        string known = GetKnownDisplay();
        GUI.Label(new Rect(px + 10, py + 40, pw - 20, 22), $"Ghi chú: {known}", _subHintStyle);

        // Display ô nhập
        Color bgCol = _wrongFlash ? new Color(0.7f, 0.05f, 0.05f, 0.8f) : new Color(0.04f, 0.08f, 0.04f, 0.9f);
        Color oc    = GUI.color;
        GUI.color = bgCol;
        GUI.DrawTexture(new Rect(px + 14, py + 68, pw - 28, 54), Texture2D.whiteTexture);
        GUI.color = oc;

        string inputDisplay = _input.ToString().PadRight(4, '_');
        GUI.Label(new Rect(px + 14, py + 68, pw - 28, 54), inputDisplay, _codeStyle);

        // Divider
        GUI.color = new Color(0.25f, 0.35f, 0.25f, 0.5f);
        GUI.DrawTexture(new Rect(px + 10, py + 130, pw - 20, 1), Texture2D.whiteTexture);
        GUI.color = oc;

        // Nút số: 3x4 grid (1-9, ⌫, 0, ✓)
        string[] keys = { "1","2","3","4","5","6","7","8","9","⌫","0","✓" };
        const float bw = 72f, bh = 50f, gap = 6f;
        float sx = px + 18f, sy = py + 142f;

        for (int i = 0; i < keys.Length; i++)
        {
            float bx = sx + (i % 3) * (bw + gap);
            float by = sy + (i / 3) * (bh + gap);

            bool enabled = keys[i] switch
            {
                "✓" => _input.Length == 4,
                "⌫" => _input.Length > 0,
                _   => true
            };

            if (GUI.Button(new Rect(bx, by, bw, bh), keys[i],
                enabled ? _btnStyle : _btnDisabledStyle))
            {
                if (enabled) PressKey(keys[i]);
            }
        }

        // ESC hint
        GUIStyle esc = new GUIStyle { fontSize = 10, alignment = TextAnchor.MiddleCenter };
        esc.normal.textColor = new Color(0.35f, 0.38f, 0.33f);
        GUI.Label(new Rect(px, py + ph - 24, pw, 22), "[ESC] Đóng", esc);
    }

    private AudioSource _audioSource;

    void PressKey(string key)
    {
        if (_audioSource == null)
        {
            _audioSource = gameObject.GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0f; // Âm thanh 2D để nghe rõ nhất trong UI
        }

        AudioClip beep = Resources.Load<AudioClip>("Audio/button_press");
        if (beep != null) 
            _audioSource.PlayOneShot(beep);
        else
            Debug.LogError("[EscapeCipher] Không tìm thấy file âm thanh tại Resources/Audio/button_press.wav!");

        switch (key)
        {
            case "⌫": if (_input.Length > 0) _input.Remove(_input.Length - 1, 1); break;
            case "✓": SubmitCode(); break;
            default:  if (_input.Length < 4) _input.Append(key); break;
        }
    }

    void SubmitCode()
    {
        string entered = _input.ToString();
        if (entered == _code)
        {
            _unlocked   = true;
            _keypadOpen = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
            Debug.Log("<color=lime>[EscapeCipher] Mật mã đúng! Cửa mở!</color>");
            if (_currentInv != null)
                _currentInv.SyncEscapeEventServerRpc(0); // Event 0 = Unlock
            else
                EscapeManager.Instance?.UnlockEscape();
        }
        else
        {
            Debug.Log($"[EscapeCipher] Mật mã sai: {entered} (đúng: {_code})");
            _input.Clear();
            _wrongFlash = true;
            _wrongTimer = 1f;

            // Phạt: trừ máu
            PlayerSurvival ps = Object.FindAnyObjectByType<PlayerSurvival>();
            if (ps != null) ps.TakeDamage(wrongCodeDamage, "Mật mã sai!");
        }
    }

    // ── Style Init ────────────────────────────────────────────────────────────

    void EnsureStyles()
    {
        if (_stylesReady) return;
        _stylesReady = true;

        _panelStyle = new GUIStyle();
        _panelStyle.normal.background = MakeTex(new Color(0.07f, 0.09f, 0.07f, 0.97f));

        _codeStyle = new GUIStyle
        {
            fontSize  = 36, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        _codeStyle.normal.textColor = new Color(0.2f, 1f, 0.4f);

        _hintStyle = new GUIStyle
        {
            fontSize  = 12, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        _hintStyle.normal.textColor = new Color(0.7f, 0.75f, 0.68f);

        _subHintStyle = new GUIStyle
        {
            fontSize  = 12,
            alignment = TextAnchor.MiddleCenter
        };
        _subHintStyle.normal.textColor = new Color(0.95f, 0.88f, 0.3f);

        _btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 22, fontStyle = FontStyle.Bold
        };
        _btnStyle.normal.textColor  = new Color(0.92f, 0.94f, 0.88f);
        _btnStyle.normal.background = MakeTex(new Color(0.18f, 0.24f, 0.16f));
        _btnStyle.hover.background  = MakeTex(new Color(0.28f, 0.38f, 0.24f));
        _btnStyle.hover.textColor   = Color.white;

        _btnDisabledStyle = new GUIStyle(_btnStyle);
        _btnDisabledStyle.normal.textColor  = new Color(0.32f, 0.35f, 0.30f);
        _btnDisabledStyle.normal.background = MakeTex(new Color(0.1f, 0.12f, 0.09f));
    }

    static Texture2D MakeTex(Color c)
    {
        var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = _unlocked ? Color.green : new Color(1f, 0.9f, 0.1f, 0.7f);
        Gizmos.DrawWireCube(transform.position, new Vector3(0.6f, 1.2f, 0.2f));
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.8f,
            $"[KEYPAD]  Code: {(_code.Length > 0 ? _code : "????")}");
    }
#endif
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Ghi chú chứa 2 chữ số của mật mã. Tự được spawn bởi EscapeCipher.
/// Player bấm [E] để nhặt.
/// </summary>
public class CipherNote : MonoBehaviour, IInteractable
{
    [HideInInspector] public int          noteIndex;
    [HideInInspector] public EscapeCipher parentCipher;
    [HideInInspector] public string       digits = "??";

    private float _baseY;
    private float _phase;

    void Start()
    {
        _baseY = transform.position.y;
        _phase = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        _phase += Time.deltaTime;
        Vector3 p = transform.position;
        p.y = _baseY + Mathf.Sin(_phase * 1.8f) * 0.04f;
        transform.position = p;
    }

    public void Interact(GameObject interactor)
    {
        Debug.Log($"[CipherNote] Tìm thấy ghi chú {noteIndex}: chữ số '{digits}'");
        
        PlayerInventory inv = interactor.GetComponentInParent<PlayerInventory>()
                           ?? interactor.GetComponentInChildren<PlayerInventory>();
        if (inv != null && inv.IsOwner)
        {
            inv.SyncCipherNoteServerRpc(noteIndex, digits, transform.position);
        }

        parentCipher?.OnNoteFound(noteIndex, digits);
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f,
            $"[Ghi Chú {noteIndex}]  digits={digits}");
    }
#endif
}
