using UnityEngine;

/// <summary>
/// HUD Nhiệm vụ cực kỳ "Wow" với hiệu ứng Slide, Fade, Neon Border và Scanlines.
/// </summary>
public class EscapeHUD : MonoBehaviour
{
    [Header("Panel Settings")]
    public float panelWidth = 650f;
    public float panelHeight = 240f;
    public float slideSpeed = 12f;
    
    // Runtime
    private bool _isUIOpen = false;
    private float _openAnim = 0f; // 0 to 1
    private float _visualProgress = 0f;
    private float _bannerTimer = 0f;
    private bool _showBanner = false;

    // GUI Styles & Textures
    private bool _stylesReady = false;
    private GUIStyle _titleStyle;
    private GUIStyle _descStyle;
    private GUIStyle _detailStyle;
    private GUIStyle _hintStyle;
    private Texture2D _bgTex;
    private Texture2D _accentTex;
    private Texture2D _scanlineTex;

    void Start()
    {
        if (EscapeManager.Instance != null)
            EscapeManager.Instance.OnEscapeUnlocked += HandleUnlocked;
    }

    void HandleUnlocked()
    {
        _showBanner = true;
        _bannerTimer = 5f;
        _isUIOpen = true; // Tự động mở bảng để xem objective cuối
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q)) _isUIOpen = !_isUIOpen;
        else if (Input.GetKeyDown(KeyCode.Escape) && _isUIOpen) _isUIOpen = false;

        // Smooth animation mượt mà
        _openAnim = Mathf.Lerp(_openAnim, _isUIOpen ? 1f : 0f, Time.deltaTime * slideSpeed);
        
        if (EscapeManager.Instance != null)
            _visualProgress = Mathf.Lerp(_visualProgress, EscapeManager.Instance.ProgressValue, Time.deltaTime * 5f);

        if (_bannerTimer > 0f)
        {
            _bannerTimer -= Time.deltaTime;
            if (_bannerTimer <= 0f) _showBanner = false;
        }
    }

    void OnGUI()
    {
        if (EscapeManager.Instance == null) return;
        EnsureStyles();

        if (_showBanner) DrawBanner();

        // Nút nhắc nhở ở góc dưới phải
        if (_openAnim < 0.01f)
        {
            GUI.Label(new Rect(Screen.width - 250f, Screen.height - 40f, 240f, 30f), "[Q] DATA LOG / NHIỆM VỤ", _hintStyle);
            return;
        }

        // --- DRAW MAIN PANEL ---
        float alpha = _openAnim;
        // Slide từ trên xuống với gia tốc mượt (SmoothStep)
        float py = Mathf.Lerp(-panelHeight, (Screen.height - panelHeight) / 2f - 40f, Mathf.SmoothStep(0f, 1f, _openAnim));
        float px = (Screen.width - panelWidth) / 2f;

        Color oldColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);

        // 1. Background kính mờ tối màu
        GUI.DrawTexture(new Rect(px, py, panelWidth, panelHeight), _bgTex);

        // 2. Scanlines mờ ảo (hiệu ứng màn hình máy tính cũ/sci-fi)
        GUI.color = new Color(1f, 1f, 1f, alpha * 0.15f);
        GUI.DrawTextureWithTexCoords(new Rect(px, py, panelWidth, panelHeight), _scanlineTex, new Rect(0, 0, panelWidth, panelHeight / 4f));
        
        // 3. Viền Accent (Trái) neon phát sáng
        bool isDone = EscapeManager.Instance.IsEscapeUnlocked;
        Color accentCol = isDone ? new Color(0.2f, 1f, 0.4f) : new Color(1f, 0.65f, 0f); // Xanh lá nếu xong, Cam nếu đang làm
        
        GUI.color = new Color(accentCol.r, accentCol.g, accentCol.b, alpha);
        GUI.DrawTexture(new Rect(px, py, 4f, panelHeight), _accentTex);
        // Header line (ngăn cách tiêu đề)
        GUI.DrawTexture(new Rect(px + 4, py + 45f, panelWidth - 24f, 1f), _accentTex);

        // 4. Texts
        GUI.color = new Color(1f, 1f, 1f, alpha);
        string statusIcon = isDone ? "[SECURE]" : "[ACTIVE]";
        GUI.Label(new Rect(px + 20, py + 10, panelWidth - 40, 30), $"{statusIcon} {EscapeManager.Instance.GetMethodName().ToUpper()}", _titleStyle);
        
        GUI.Label(new Rect(px + 20, py + 55, panelWidth - 40, 25), "STATUS: " + EscapeManager.Instance.ProgressMessage, _descStyle);

        // 5. Progress Bar kiểu tương lai (có viền glow)
        float barY = py + 85f;
        float barW = panelWidth - 40f;
        
        // Nền đen
        GUI.color = new Color(0.05f, 0.05f, 0.05f, alpha * 0.9f);
        GUI.DrawTexture(new Rect(px + 20, barY, barW, 12f), _accentTex); 
        
        // Glow viền thanh tiến trình
        GUI.color = new Color(accentCol.r, accentCol.g, accentCol.b, alpha * 0.25f);
        GUI.DrawTexture(new Rect(px + 18, barY - 2, barW + 4, 16f), _accentTex); 
        
        // Fill chạy mượt mà
        if (_visualProgress > 0f)
        {
            GUI.color = new Color(accentCol.r, accentCol.g, accentCol.b, alpha);
            GUI.DrawTexture(new Rect(px + 20, barY, barW * _visualProgress, 12f), _accentTex);
        }

        // 6. Detailed Text (kiểu dữ liệu hệ thống)
        GUI.color = new Color(1f, 1f, 1f, alpha * 0.85f);
        GUI.Label(new Rect(px + 20, py + 115, panelWidth - 40, 100),
            EscapeManager.Instance.GetMethodDetailedDescription(), _detailStyle);

        // 7. Footer nhắc nhở
        GUI.color = new Color(1f, 1f, 1f, alpha * 0.4f);
        GUI.Label(new Rect(px, py + panelHeight - 25, panelWidth - 15, 20), "Bấm [Q] hoặc [ESC] để đóng", _hintStyle);

        GUI.color = oldColor;
    }

    void DrawBanner()
    {
        // Smooth banner animation
        float t = Mathf.Clamp01(_bannerTimer / 5f);
        float alpha = Mathf.Sin(t * Mathf.PI); // Fade in and out theo hình sin

        float bw = Screen.width;
        float bh = 80f;
        float bx = 0;
        float by = Screen.height * 0.15f; // Hiện ở 15% màn hình từ trên xuống

        Color old = GUI.color;
        
        // Dải băng đen ngang màn hình
        GUI.color = new Color(0f, 0f, 0f, alpha * 0.85f);
        GUI.DrawTexture(new Rect(0, by, bw, bh), _accentTex);

        // Viền Xanh lá cây ở trên và dưới
        GUI.color = new Color(0.1f, 1f, 0.3f, alpha);
        GUI.DrawTexture(new Rect(0, by, bw, 2), _accentTex);
        GUI.DrawTexture(new Rect(0, by + bh - 2, bw, 2), _accentTex);

        // Chữ Banner bự
        GUIStyle bannerText = new GUIStyle(_titleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 28 };
        bannerText.normal.textColor = new Color(0.2f, 1f, 0.4f);
        GUI.Label(new Rect(bx, by, bw, bh), "⚠ OBJECTIVE COMPLETE - PROCEED TO EXTRACTION ⚠", bannerText);

        GUI.color = old;
    }

    void EnsureStyles()
    {
        if (_stylesReady) return;
        _stylesReady = true;

        _bgTex = MakeTex(new Color(0.04f, 0.05f, 0.07f, 0.95f));
        _accentTex = MakeTex(Color.white);
        _scanlineTex = CreateScanlineTexture();

        _titleStyle = new GUIStyle { fontSize = 18, fontStyle = FontStyle.Bold };
        _titleStyle.normal.textColor = new Color(0.9f, 0.95f, 1f);

        _descStyle = new GUIStyle { fontSize = 13, fontStyle = FontStyle.Bold };
        _descStyle.normal.textColor = new Color(0.7f, 0.8f, 0.9f);

        // Giãn dòng (lineHeight) cho text chi tiết dễ đọc hơn
        _detailStyle = new GUIStyle { fontSize = 12, wordWrap = true };
        _detailStyle.normal.textColor = new Color(0.6f, 0.7f, 0.75f);

        _hintStyle = new GUIStyle { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        _hintStyle.normal.textColor = Color.white;
    }

    static Texture2D MakeTex(Color c)
    {
        var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t;
    }

    // Tạo texture sọc ngang (Scanlines) kiểu màn hình vi tính Sci-fi
    static Texture2D CreateScanlineTexture()
    {
        var t = new Texture2D(2, 4);
        for(int y=0; y<4; y++) {
            for(int x=0; x<2; x++) {
                // Hàng chẵn trong suốt, hàng lẻ màu đen mờ
                t.SetPixel(x, y, y % 2 == 0 ? new Color(0,0,0,0) : new Color(0,0,0,0.4f));
            }
        }
        t.filterMode = FilterMode.Point;
        t.Apply();
        return t;
    }

    void OnDestroy()
    {
        if (EscapeManager.Instance != null) EscapeManager.Instance.OnEscapeUnlocked -= HandleUnlocked;
    }
}
