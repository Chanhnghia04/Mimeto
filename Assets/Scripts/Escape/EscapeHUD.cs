using UnityEngine;

/// <summary>
/// MÔI TRƯỜNG ẢO (VIRTUAL RESOLUTION): 1920x1080.
/// HUD Nhiệm vụ cực kỳ "Wow" với thiết kế Sci-fi Hologram, Typewriter, Segmented Bar.
/// </summary>
public class EscapeHUD : MonoBehaviour
{
    [Header("Panel Settings")]
    public float panelWidth = 500f;
    public float panelHeight = 700f;
    public float slideSpeed = 12f;
    
    // Runtime
    private bool _isUIOpen = false;
    private float _openAnim = 0f; // 0 to 1
    private float _visualProgress = 0f;
    private float _bannerTimer = 0f;
    private bool _showBanner = false;
    
    // Effects
    private float _noiseOffset = 0f;
    private float _typewriterProgress = 0f;
    private string _cachedDesc = "";
    
    // GUI Styles & Textures
    private bool _stylesReady = false;
    private GUIStyle _titleStyle;
    private GUIStyle _subtitleStyle;
    private GUIStyle _descStyle;
    private GUIStyle _detailStyle;
    private GUIStyle _hintStyle;
    
    private Texture2D _bgTex;
    private Texture2D _accentTex;
    private Texture2D _scanlineTex;
    private Texture2D _gridTex;

    // Virtual Resolution Base
    private readonly float V_WIDTH = 1920f;
    private readonly float V_HEIGHT = 1080f;

    void Start()
    {
        if (EscapeManager.Instance != null)
            EscapeManager.Instance.OnEscapeUnlocked += HandleUnlocked;
    }

    void HandleUnlocked()
    {
        _showBanner = true;
        _bannerTimer = 6f;
        _isUIOpen = true; 
    }

    public void ForceOpenHUD()
    {
        _isUIOpen = true;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) 
        {
            _isUIOpen = !_isUIOpen;
            if (_isUIOpen) _typewriterProgress = 0f; // Reset hiệu ứng gõ chữ khi mở
        }
        else if (Input.GetKeyDown(KeyCode.Escape) && _isUIOpen) 
        {
            _isUIOpen = false;
        }

        // Slide animation bằng Spring nhún nhẹ
        _openAnim = Mathf.Lerp(_openAnim, _isUIOpen ? 1f : 0f, Time.deltaTime * slideSpeed);
        
        if (EscapeManager.Instance != null)
            _visualProgress = Mathf.Lerp(_visualProgress, EscapeManager.Instance.ProgressValue, Time.deltaTime * 8f);

        if (_bannerTimer > 0f)
        {
            _bannerTimer -= Time.deltaTime;
            if (_bannerTimer <= 0f) _showBanner = false;
        }
        
        _noiseOffset += Time.deltaTime * 15f;
        if (_isUIOpen) _typewriterProgress += Time.deltaTime * 60f; // Tốc độ gõ chữ (60 ký tự / giây)
    }

    void OnGUI()
    {
        if (EscapeManager.Instance == null) return;
        EnsureStyles();

        // 0. Set Virtual Resolution Scaling
        Vector3 scale = new Vector3(Screen.width / V_WIDTH, Screen.height / V_HEIGHT, 1f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, scale);

        if (_showBanner) DrawBanner();

        // Nút nhắc nhở ở góc dưới phải (Lúc đóng)
        if (_openAnim < 0.01f)
        {
            GUI.color = new Color(0f, 1f, 1f, 0.8f + Mathf.Sin(Time.time * 5f) * 0.2f);
            GUI.Label(new Rect(V_WIDTH - 280f, V_HEIGHT - 60f, 260f, 40f), "[R] SYSTEM UPLINK", _hintStyle);
            GUI.color = Color.white;
            return;
        }

        // --- DRAW MODERN SIDE PANEL ---
        float alpha = _openAnim;
        // Ease Out Back (Tạo cảm giác nảy nhẹ khi panel trượt ra)
        float easeAnim = 1f - Mathf.Pow(1f - _openAnim, 3f); 
        
        float px = V_WIDTH - (panelWidth * easeAnim) - 40f; // Cách lề phải 40px
        float py = (V_HEIGHT - panelHeight) / 2f;

        Color oldColor = GUI.color;
        bool isDone = EscapeManager.Instance.IsEscapeUnlocked;
        Color accentCol = isDone ? new Color(0.1f, 1f, 0.4f) : new Color(0f, 0.9f, 1f); 
        Color alertCol = new Color(1f, 0.2f, 0.2f);

        // --- 1. Lớp Nền & Kính ---
        GUI.color = new Color(0.02f, 0.04f, 0.08f, alpha * 0.95f);
        GUI.DrawTexture(new Rect(px, py, panelWidth, panelHeight), _accentTex);
        
        // --- 2. Lưới Grid & Scanlines ---
        GUI.color = new Color(accentCol.r, accentCol.g, accentCol.b, alpha * 0.15f);
        GUI.DrawTextureWithTexCoords(new Rect(px, py, panelWidth, panelHeight), _gridTex, new Rect(0, 0, panelWidth / 30f, panelHeight / 30f));

        GUI.color = new Color(1f, 1f, 1f, alpha * 0.1f);
        GUI.DrawTextureWithTexCoords(new Rect(px, py, panelWidth, panelHeight), _scanlineTex, new Rect(0, _noiseOffset * 0.1f, panelWidth, panelHeight / 4f));

        // --- 3. Viền Tech (Sci-fi Corners) ---
        DrawTechCorners(px, py, panelWidth, panelHeight, accentCol, alpha);

        // --- 4. Nội dung ---
        float contentX = px + 40f;
        float contentW = panelWidth - 80f;

        // Header Glitch
        Vector2 titleOffset = Vector2.zero;
        if (Random.value > 0.96f) titleOffset = new Vector2(Random.Range(-3f, 3f), 0); // Glitch nhẹ

        GUI.color = new Color(accentCol.r, accentCol.g, accentCol.b, alpha);
        GUI.Label(new Rect(contentX + titleOffset.x, py + 40, contentW, 20), "SYS.OBJ // OVERRIDE PROTOCOL", _subtitleStyle);
        
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(new Rect(contentX + titleOffset.x, py + 65, contentW, 40), EscapeManager.Instance.GetMethodName().ToUpper(), _titleStyle);

        // Status Line
        GUI.color = isDone ? accentCol : alertCol;
        GUI.DrawTexture(new Rect(contentX, py + 120, contentW, 2f), _accentTex);
        
        GUI.color = new Color(1f, 1f, 1f, alpha);
        string statusText = isDone ? "> STATUS: CLEARED" : "> STATUS: IN PROGRESS";
        GUI.Label(new Rect(contentX, py + 135, contentW, 30), statusText, _descStyle);
        GUI.Label(new Rect(contentX, py + 165, contentW, 30), EscapeManager.Instance.ProgressMessage, _subtitleStyle);

        // --- 5. Segmented Progress Bar ---
        float barY = py + 210f;
        float barH = 24f;
        int totalSegments = 20;
        float gap = 4f;
        float segmentW = (contentW - (gap * (totalSegments - 1))) / totalSegments;

        int activeSegments = Mathf.RoundToInt(_visualProgress * totalSegments);
        
        for (int i = 0; i < totalSegments; i++)
        {
            float segX = contentX + (i * (segmentW + gap));
            if (i < activeSegments)
            {
                // Segment đang active
                GUI.color = new Color(accentCol.r, accentCol.g, accentCol.b, alpha);
                GUI.DrawTexture(new Rect(segX, barY, segmentW, barH), _accentTex);
            }
            else
            {
                // Segment trống
                GUI.color = new Color(1f, 1f, 1f, alpha * 0.1f);
                GUI.DrawTexture(new Rect(segX, barY, segmentW, barH), _accentTex);
            }
        }

        // --- 6. Typewriter Description ---
        GUI.color = new Color(0.8f, 0.9f, 1f, alpha * 0.9f);
        string fullText = EscapeManager.Instance.GetMethodDetailedDescription();
        
        // Reset text nếu nhiệm vụ đổi
        if (_cachedDesc != fullText)
        {
            _cachedDesc = fullText;
            _typewriterProgress = 0f;
        }

        int charsToShow = Mathf.Min(fullText.Length, Mathf.FloorToInt(_typewriterProgress));
        string displayText = fullText.Substring(0, charsToShow);
        
        // Thêm con trỏ nhấp nháy
        if (charsToShow < fullText.Length || Mathf.FloorToInt(Time.time * 2f) % 2 == 0)
        {
            displayText += "<color=#00ffff>_</color>";
        }

        GUI.Label(new Rect(contentX, py + 270, contentW, 350), displayText, _detailStyle);

        // --- 7. Footer ---
        GUI.color = new Color(accentCol.r, accentCol.g, accentCol.b, alpha * 0.5f);
        GUI.DrawTexture(new Rect(contentX, py + panelHeight - 60, contentW, 1f), _accentTex);
        
        GUI.color = new Color(1f, 1f, 1f, alpha * 0.4f);
        GUI.Label(new Rect(contentX, py + panelHeight - 50, contentW, 30), "[R/ESC] CLOSE TERMINAL", _hintStyle);

        GUI.matrix = Matrix4x4.identity; // Trả lại matrix cũ
        GUI.color = oldColor;
    }

    void DrawBanner()
    {
        float t = Mathf.Clamp01(_bannerTimer / 6f);
        float alpha = Mathf.Sin(t * Mathf.PI); // Fade in & out mượt
        float pulse = Mathf.Abs(Mathf.Sin(Time.time * 8f)); // Chớp nháy nhanh

        float bw = V_WIDTH;
        float bh = 140f;
        float by = V_HEIGHT * 0.15f; 

        // Nền
        GUI.color = new Color(0.1f, 0f, 0f, alpha * 0.95f);
        GUI.DrawTexture(new Rect(0, by, bw, bh), _accentTex);

        // Viền Đỏ chớp nháy
        GUI.color = new Color(1f, 0.1f, 0.1f, alpha * pulse);
        GUI.DrawTexture(new Rect(0, by, bw, 4), _accentTex);
        GUI.DrawTexture(new Rect(0, by + bh - 4, bw, 4), _accentTex);
        
        // Scanline trên banner
        GUI.color = new Color(1f, 1f, 1f, alpha * 0.2f);
        GUI.DrawTextureWithTexCoords(new Rect(0, by, bw, bh), _scanlineTex, new Rect(0, _noiseOffset * 0.2f, bw, bh / 2f));

        // Chữ
        GUIStyle bannerText = new GUIStyle(_titleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 42 };
        bannerText.normal.textColor = new Color(1f, 0.3f, 0.3f, alpha);
        
        // Glitch text ngẫu nhiên
        Vector2 offset = (pulse > 0.8f && Random.value > 0.5f) ? new Vector2(Random.Range(-5f, 5f), 0) : Vector2.zero;
        GUI.Label(new Rect(offset.x, by, bw, bh), "[ WARNING: MISSION CLEARED - EVACUATE NOW ]", bannerText);
    }

    void DrawTechCorners(float x, float y, float w, float h, Color color, float alpha)
    {
        GUI.color = new Color(color.r, color.g, color.b, alpha * 0.8f);
        float len = 40f;
        float thick = 4f;

        // Góc trên trái
        GUI.DrawTexture(new Rect(x, y, len, thick), _accentTex);
        GUI.DrawTexture(new Rect(x, y, thick, len), _accentTex);
        // Góc trên phải
        GUI.DrawTexture(new Rect(x + w - len, y, len, thick), _accentTex);
        GUI.DrawTexture(new Rect(x + w - thick, y, thick, len), _accentTex);
        // Góc dưới trái
        GUI.DrawTexture(new Rect(x, y + h - thick, len, thick), _accentTex);
        GUI.DrawTexture(new Rect(x, y + h - len, thick, len), _accentTex);
        // Góc dưới phải
        GUI.DrawTexture(new Rect(x + w - len, y + h - thick, len, thick), _accentTex);
        GUI.DrawTexture(new Rect(x + w - thick, y + h - len, thick, len), _accentTex);
    }

    void EnsureStyles()
    {
        if (_stylesReady) return;
        _stylesReady = true;

        _bgTex = MakeTex(new Color(0.02f, 0.03f, 0.05f, 1f));
        _accentTex = MakeTex(Color.white);
        _scanlineTex = CreateScanlineTexture();
        _gridTex = CreateGridTexture();

        _titleStyle = new GUIStyle { fontSize = 38, fontStyle = FontStyle.Bold };
        _titleStyle.normal.textColor = new Color(0.95f, 0.98f, 1f);

        _subtitleStyle = new GUIStyle { fontSize = 16, fontStyle = FontStyle.Bold };
        _subtitleStyle.normal.textColor = new Color(0.4f, 0.6f, 0.8f);

        _descStyle = new GUIStyle { fontSize = 22, fontStyle = FontStyle.Bold };
        _descStyle.normal.textColor = new Color(1f, 1f, 1f);

        _detailStyle = new GUIStyle { fontSize = 18, wordWrap = true, richText = true };
        _detailStyle.normal.textColor = new Color(0.7f, 0.85f, 1f);

        _hintStyle = new GUIStyle { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        _hintStyle.normal.textColor = Color.white;
    }

    static Texture2D MakeTex(Color c)
    {
        var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t;
    }

    static Texture2D CreateScanlineTexture()
    {
        var t = new Texture2D(2, 4);
        for(int y=0; y<4; y++) 
            for(int x=0; x<2; x++) 
                t.SetPixel(x, y, y % 2 == 0 ? new Color(0,0,0,0) : new Color(0,0,0,0.6f));
        t.filterMode = FilterMode.Point;
        t.Apply();
        return t;
    }

    static Texture2D CreateGridTexture()
    {
        var t = new Texture2D(30, 30);
        for(int y=0; y<30; y++) 
            for(int x=0; x<30; x++) 
                t.SetPixel(x, y, (x == 0 || y == 0) ? new Color(1,1,1,0.25f) : new Color(0,0,0,0));
        t.filterMode = FilterMode.Bilinear;
        t.wrapMode = TextureWrapMode.Repeat;
        t.Apply();
        return t;
    }

    void OnDestroy()
    {
        if (EscapeManager.Instance != null) EscapeManager.Instance.OnEscapeUnlocked -= HandleUnlocked;
    }
}