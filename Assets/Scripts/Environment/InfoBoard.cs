using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class InfoBoard : MonoBehaviour, IInteractable
{
    public bool isOpen = false;

    // ── COLORS ────────────────────────────────────────────────────────
    private static readonly Color BG_COLOR    = new Color(0.02f, 0.05f, 0.08f, 0.95f);
    private static readonly Color BORDER_CYAN = new Color(0.1f, 0.8f, 1.0f, 1f);
    private static readonly Color TEXT_CYAN   = new Color(0.4f, 0.9f, 1.0f, 1f);
    private static readonly Color TEXT_DIM    = new Color(0.2f, 0.6f, 0.7f, 0.6f);
    private static readonly Color WARNING_RED = new Color(1.0f, 0.2f, 0.2f, 1f);
    private static readonly Color TITLE_BG    = new Color(0.05f, 0.2f, 0.3f, 0.8f);

    private Texture2D _wh;
    private float _alpha = 0f;
    private float _scanlineY = 0f;

    void Awake()
    {
        _wh = new Texture2D(1, 1);
        _wh.SetPixel(0, 0, Color.white);
        _wh.Apply();
    }

    public void Interact(GameObject interactor)
    {
        if (isOpen) return;
        isOpen = true;
        _alpha = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void CloseBoard()
    {
        isOpen = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!isOpen) return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        float dt = Time.unscaledDeltaTime;
        _alpha = Mathf.Lerp(_alpha, 1f, dt * 10f);
        _scanlineY = (_scanlineY + dt * 200f) % 600f;

        if (Input.GetKeyDown(KeyCode.Escape)) CloseBoard();
    }

    void OnGUI()
    {
        if (!isOpen) return;
        GUI.depth = -10;

        float sw = Screen.width, sh = Screen.height;

        // Dark overlay
        GUI.color = new Color(0, 0, 0, 0.8f * _alpha);
        GUI.DrawTexture(new Rect(0, 0, sw, sh), _wh);

        // Panel size
        float pw = Mathf.Min(sw * 0.5f, 800f);
        float ph = Mathf.Min(sh * 0.7f, 600f);
        float px = (sw - pw) * 0.5f;
        float py = (sh - ph) * 0.5f;

        // Glitch offset
        float ox = (Random.value > 0.98f) ? Random.Range(-5f, 5f) : 0f;

        GUI.BeginGroup(new Rect(px + ox, py, pw, ph));

        // Main BG
        GUI.color = new Color(BG_COLOR.r, BG_COLOR.g, BG_COLOR.b, BG_COLOR.a * _alpha);
        GUI.DrawTexture(new Rect(0, 0, pw, ph), _wh);

        // Grid lines (subtle)
        GUI.color = new Color(BORDER_CYAN.r, BORDER_CYAN.g, BORDER_CYAN.b, 0.05f * _alpha);
        for (float x = 0; x < pw; x += 40f) GUI.DrawTexture(new Rect(x, 0, 1, ph), _wh);
        for (float y = 0; y < ph; y += 40f) GUI.DrawTexture(new Rect(0, y, pw, 1), _wh);

        // Borders
        GUI.color = new Color(BORDER_CYAN.r, BORDER_CYAN.g, BORDER_CYAN.b, 0.8f * _alpha);
        GUI.DrawTexture(new Rect(0, 0, pw, 2), _wh); // Top
        GUI.DrawTexture(new Rect(0, ph - 2, pw, 2), _wh); // Bottom
        GUI.DrawTexture(new Rect(0, 0, 2, ph), _wh); // Left
        GUI.DrawTexture(new Rect(pw - 2, 0, 2, ph), _wh); // Right

        // Header
        GUI.color = new Color(TITLE_BG.r, TITLE_BG.g, TITLE_BG.b, TITLE_BG.a * _alpha);
        GUI.DrawTexture(new Rect(0, 0, pw, 60), _wh);
        
        GUI.color = new Color(1, 1, 1, _alpha);
        var titleStyle = Sty(28, FontStyle.Bold, TEXT_CYAN, TextAnchor.MiddleLeft);
        GUI.Label(new Rect(20, 0, pw, 60), "TERMINAL // SYSTEM_RULES", titleStyle);

        // Close Button
        var btnStyle = Sty(20, FontStyle.Bold, TEXT_CYAN, TextAnchor.MiddleCenter);
        if (GUI.Button(new Rect(pw - 60, 10, 40, 40), "X", btnStyle)) CloseBoard();

        // Content
        float contentY = 80f;
        DrawSection(">>> SURVIVAL TIPS", "• Keep an eye on your Oxygen levels.\n• Refill O2 at Safe Zones or by purchasing Oxygen Tanks.\n• Gas masks slow down Oxygen depletion in toxic areas.", pw, ref contentY);
        DrawSection(">>> DANGER ZONES", "• RED ZONES: Highly toxic, fast O2 drain.\n• ABANDONED SECTORS: Mutants and traps ahead. Bring weapons.\n• DARK AREAS: Flashlight required.", pw, ref contentY);
        DrawSection(">>> ECONOMY", "• Collect Scrap from crates and enemies.\n• Sell Scrap at the Reclaimer Station for EC (Energy Credits).\n• Use EC at the Shop or try your luck at the Mini-Games.", pw, ref contentY, WARNING_RED);

        // Scanline effect
        GUI.color = new Color(BORDER_CYAN.r, BORDER_CYAN.g, BORDER_CYAN.b, 0.1f * _alpha);
        GUI.DrawTexture(new Rect(0, _scanlineY, pw, 4), _wh);

        GUI.EndGroup();
    }

    void DrawSection(string title, string body, float pw, ref float yPos, Color? titleColor = null)
    {
        Color tc = titleColor ?? TEXT_CYAN;
        var hStyle = Sty(18, FontStyle.Bold, new Color(tc.r, tc.g, tc.b, _alpha), TextAnchor.MiddleLeft);
        var bStyle = Sty(14, FontStyle.Normal, new Color(TEXT_DIM.r, TEXT_DIM.g, TEXT_DIM.b, _alpha), TextAnchor.UpperLeft);

        GUI.Label(new Rect(30, yPos, pw - 60, 24), title, hStyle);
        yPos += 30;
        
        float bodyHeight = bStyle.CalcHeight(new GUIContent(body), pw - 60);
        GUI.Label(new Rect(30, yPos, pw - 60, bodyHeight), body, bStyle);
        yPos += bodyHeight + 20;
    }

    GUIStyle Sty(int sz, FontStyle fs, Color col, TextAnchor a)
    {
        var s = new GUIStyle();
        s.fontSize = sz;
        s.fontStyle = fs;
        s.normal.textColor = col;
        s.alignment = a;
        s.richText = true;
        s.wordWrap = true;
        return s;
    }
}
