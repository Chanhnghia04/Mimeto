using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public class SlotMachineStation : MonoBehaviour, IInteractable
{
    public bool isOpen = false;
    private PlayerInventory _inventory;

    // ── NEON RETRO ARCADE Palette ─────────────────────────────────────────────
    private static readonly Color NEON_RED    = new Color(1.00f, 0.08f, 0.20f);
    private static readonly Color NEON_YELLOW = new Color(1.00f, 0.92f, 0.00f);
    private static readonly Color NEON_CYAN   = new Color(0.00f, 1.00f, 0.95f);
    private static readonly Color NEON_PINK   = new Color(1.00f, 0.10f, 0.80f);
    private static readonly Color NEON_GREEN  = new Color(0.10f, 1.00f, 0.30f);
    private static readonly Color CHROME      = new Color(0.78f, 0.78f, 0.82f);
    private static readonly Color CHROME_DIM  = new Color(0.35f, 0.35f, 0.40f);
    private static readonly Color CABINET_BG  = new Color(0.05f, 0.05f, 0.07f);
    private static readonly Color SCREEN_BG   = new Color(0.02f, 0.02f, 0.04f);
    private static readonly Color REEL_BG     = new Color(0.06f, 0.06f, 0.09f);

    // ── Symbols + weights + payouts ───────────────────────────────────────────
    private static readonly string[] SYM   = { "7", "★", "♦", "♣", "♥", "♠", "BAR" };
    private static readonly int[]    WGHT  = {  1,    3,   6,   6,   6,   6,    4   };
    private static readonly float[]  PAY   = { 10f, 5f, 2.5f, 2f, 2f, 2f, 3f };
    // Two-of-a-kind: return bet
    // Three-of-a-kind: PAY[sym] * bet

    // ── Game state ────────────────────────────────────────────────────────────
    enum State { Idle, Spinning, Result }
    private State   _state    = State.Idle;
    private int     _bet      = 10;
    private int[]   _result   = new int[3];
    private float[] _spinVal  = new float[3];
    private float[] _stopAt   = new float[3];
    private bool[]  _stopped  = new bool[3];
    private float   _spinStart;
    private string  _msg      = "INSERT COIN  ►  SET BET  ►  SPIN";
    private bool    _win;

    // ── Animation ─────────────────────────────────────────────────────────────
    private float _alpha   = 0f;
    private float _scale   = 0.82f;
    private float _flash   = 0f;
    private float _neonT   = 0f;   // neon tube flicker
    private float _leverT  = 0f;   // lever pull animation

    private struct Spark { public Vector2 pos, vel; public float life, max; public Color col; public float sz; }
    private List<Spark> _sparks = new List<Spark>();

    // ── Textures ──────────────────────────────────────────────────────────────
    private Texture2D _wh, _overlay, _scanline, _chromeTex;

    void Awake()
    {
        _wh       = Texture2D.whiteTexture;
        _overlay  = Mk(new Color(0.02f, 0.00f, 0.04f, 0.96f));
        _chromeTex= Mk(new Color(0.55f, 0.55f, 0.60f, 1f));

        // Scanline
        _scanline = new Texture2D(2, 4);
        _scanline.filterMode = FilterMode.Point;
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 2; x++)
                _scanline.SetPixel(x, y, y % 2 == 0 ? new Color(0,0,0,0) : new Color(0,0,0,0.35f));
        _scanline.Apply();
    }

    Texture2D Mk(Color c) { var t = new Texture2D(1,1); t.SetPixel(0,0,c); t.Apply(); return t; }

    public void Interact(GameObject interactor)
    {
        if (isOpen) return;
        var inv = interactor.GetComponentInParent<PlayerInventory>()
               ?? interactor.GetComponentInChildren<PlayerInventory>();
        if (inv == null) return;
        _inventory = inv;
        isOpen = true;
        _alpha = 0f; _scale = 0.82f; _flash = 0f; _leverT = 0f;
        _state = State.Idle; _win = false;
        _msg = "INSERT COIN  ►  SET BET  ►  SPIN";
        _sparks.Clear();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    void CloseStation()
    {
        isOpen = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    void Update()
    {
        if (!isOpen) return;

        // ── Enforce cursor every frame ─────────────────────────────────────
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        float dt = Time.unscaledDeltaTime;
        _alpha  = Mathf.Lerp(_alpha, 1f, dt * 10f);
        _scale  = Mathf.Lerp(_scale, 1f, dt * 9f);
        _flash  = Mathf.Max(0f, _flash  - dt * 1.8f);
        _neonT += dt;
        _leverT = Mathf.Max(0f, _leverT - dt * 3f);

        if (_state == State.Spinning) DoSpin(dt);

        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            var s = _sparks[i]; s.life -= dt;
            s.pos += s.vel * dt; s.vel += new Vector2(0, 90f) * dt;
            _sparks[i] = s;
            if (s.life <= 0f) _sparks.RemoveAt(i);
        }

        if (Input.GetKeyDown(KeyCode.Escape)) CloseStation();
    }

    // ── Spin ─────────────────────────────────────────────────────────────────
    void StartSpin()
    {
        if (_inventory.credits < _bet || _bet < 10)
        { _msg = "!! INSUFFICIENT COINS !!"; return; }
        _inventory.SpendCredits(_bet);
        _state = State.Spinning; _win = false;
        _spinStart = Time.unscaledTime;
        _msg = "S P I N N I N G . . .";
        _leverT = 1f;
        for (int i = 0; i < 3; i++)
        {
            _result[i]  = WeightedPick();
            _spinVal[i] = 0f;
            _stopped[i] = false;
            _stopAt[i]  = 1.1f + i * 0.6f;
        }
    }

    int WeightedPick()
    {
        int tot = 0; foreach (int w in WGHT) tot += w;
        int r = Random.Range(0, tot);
        for (int i = 0; i < WGHT.Length; i++) { r -= WGHT[i]; if (r < 0) return i; }
        return WGHT.Length - 1;
    }

    void DoSpin(float dt)
    {
        float el = Time.unscaledTime - _spinStart;
        bool all = true;
        for (int i = 0; i < 3; i++)
        {
            if (!_stopped[i])
            {
                _spinVal[i] += dt * (20f - i * 2f);
                if (el >= _stopAt[i]) { _spinVal[i] = _result[i]; _stopped[i] = true; }
                else all = false;
            }
        }
        if (all) Evaluate();
    }

    void Evaluate()
    {
        _state = State.Result;
        int a = _result[0], b = _result[1], c = _result[2];
        if (a == b && b == c)
        {
            float m = PAY[a]; int pay = Mathf.RoundToInt(_bet * m);
            _inventory.AddCredits(pay); _win = true; _flash = 1f;
            _msg = a == 0 ? $">>> LUCKY 7 JACKPOT! x{m:0} = +{pay} EC <<<" : $">>> THREE OF A KIND x{m:0.0} = +{pay} EC <<<";
            Boom();
        }
        else if (a == b || b == c || a == c)
        { _inventory.AddCredits(_bet); _win = false; _msg = "TWO MATCH — Bet returned"; }
        else
        { _win = false; _msg = "No match. Try again!"; }
    }

    void Boom()
    {
        Color[] cols = { NEON_YELLOW, NEON_RED, NEON_CYAN, NEON_PINK, NEON_GREEN };
        float cx = Screen.width * .5f, cy = Screen.height * .5f;
        for (int i = 0; i < 80; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f), sp = Random.Range(60f, 420f);
            _sparks.Add(new Spark {
                pos = new Vector2(cx, cy),
                vel = new Vector2(Mathf.Cos(a)*sp, Mathf.Sin(a)*sp - 200f),
                life = Random.Range(0.5f, 1.6f), max = 1.6f,
                col  = cols[Random.Range(0, cols.Length)],
                sz   = Random.Range(4f, 12f)
            });
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  O N G U I  —  NEON RETRO ARCADE
    // ═══════════════════════════════════════════════════════════════════════
    void OnGUI()
    {
        if (!isOpen || _inventory == null) return;
        GUI.depth = -10;

        float sw = Screen.width, sh = Screen.height, t = Time.unscaledTime;

        // ── Dark cinema overlay ────────────────────────────────────────────
        GUI.color = new Color(1,1,1,_alpha);
        GUI.DrawTexture(new Rect(0,0,sw,sh), _overlay);

        // Scanlines on bg
        GUI.color = new Color(1,1,1, 0.07f * _alpha);
        GUI.DrawTextureWithTexCoords(new Rect(0,0,sw,sh), _scanline,
            new Rect(0, t * 0.03f, sw/2f, sh/4f));

        // Sparks
        foreach (var sp in _sparks)
        {
            float a = Mathf.Clamp01(sp.life/sp.max);
            GUI.color = new Color(sp.col.r,sp.col.g,sp.col.b, a*_alpha);
            GUI.DrawTexture(new Rect(sp.pos.x-sp.sz*.5f,sp.pos.y-sp.sz*.5f,sp.sz,sp.sz),_wh);
        }

        // ── Cabinet dimensions ─────────────────────────────────────────────
        float cw = Mathf.Min(sw * 0.65f, 760f) * _scale;
        float ch = Mathf.Min(sh * 0.82f, 620f) * _scale;
        float cx = (sw - cw) * .5f;
        float cy = (sh - ch) * .5f;

        // ── Outer cabinet body (dark chrome frame) ─────────────────────────
        // Thick chrome border
        GUI.color = new Color(CHROME_DIM.r,CHROME_DIM.g,CHROME_DIM.b,_alpha);
        GUI.DrawTexture(new Rect(cx-6,cy-6,cw+12,ch+12),_wh);
        GUI.color = new Color(CHROME.r,CHROME.g,CHROME.b,_alpha);
        GUI.DrawTexture(new Rect(cx-4,cy-4,cw+8,ch+8),_wh);
        GUI.color = new Color(CABINET_BG.r,CABINET_BG.g,CABINET_BG.b,_alpha);
        GUI.DrawTexture(new Rect(cx,cy,cw,ch),_wh);

        // ── Neon tube border (animated flicker) ───────────────────────────
        DrawNeonTubes(cx, cy, cw, ch, t);

        // ── MARQUEE banner top ─────────────────────────────────────────────
        float marH = 70f;
        DrawMarquee(cx, cy, cw, marH, t);

        // ── Screen area (reel window) ──────────────────────────────────────
        float scrX = cx + 18f, scrY = cy + marH + 10f;
        float scrW = cw - 36f, scrH = ch * 0.44f;
        DrawScreen(scrX, scrY, scrW, scrH, t);

        // ── Info strip below screen ────────────────────────────────────────
        float infoY = scrY + scrH + 6f;
        DrawInfoStrip(cx, infoY, cw, 38f, t);

        // ── Bet & lever panel ──────────────────────────────────────────────
        float btnY = infoY + 44f;
        float btnH = ch - (btnY - cy) - 18f;
        DrawBetPanel(cx, btnY, cw, btnH, t);

        // ── Balance display ────────────────────────────────────────────────
        DrawBalance(cx, cy + marH + 14f, _inventory.credits);

        // ── X close ───────────────────────────────────────────────────────
        DrawClose(cx + cw - 38f, cy + 6f);

        GUI.color = Color.white;
    }

    void DrawNeonTubes(float x, float y, float w, float h, float t)
    {
        // Each side a different neon color, slight flicker
        Color[] sides = { NEON_RED, NEON_YELLOW, NEON_CYAN, NEON_PINK };
        float[] flickers =
        {
            0.7f + 0.3f * Mathf.Sin(t * 7.3f),
            0.8f + 0.2f * Mathf.Sin(t * 5.1f + 1f),
            0.75f+ 0.25f* Mathf.Sin(t * 9.7f + 2f),
            0.85f+ 0.15f* Mathf.Sin(t * 6.4f + 3f)
        };
        float th = 3.5f;
        // top
        GUI.color = new Color(sides[0].r,sides[0].g,sides[0].b, flickers[0]*_alpha);
        GUI.DrawTexture(new Rect(x,y,w,th),_wh);
        // bottom
        GUI.color = new Color(sides[1].r,sides[1].g,sides[1].b, flickers[1]*_alpha);
        GUI.DrawTexture(new Rect(x,y+h-th,w,th),_wh);
        // left
        GUI.color = new Color(sides[2].r,sides[2].g,sides[2].b, flickers[2]*_alpha);
        GUI.DrawTexture(new Rect(x,y,th,h),_wh);
        // right
        GUI.color = new Color(sides[3].r,sides[3].g,sides[3].b, flickers[3]*_alpha);
        GUI.DrawTexture(new Rect(x+w-th,y,th,h),_wh);

        // Corner blobs
        float bsz = 10f;
        GUI.color = new Color(NEON_YELLOW.r,NEON_YELLOW.g,NEON_YELLOW.b, _alpha);
        GUI.DrawTexture(new Rect(x-2,y-2,bsz,bsz),_wh);
        GUI.DrawTexture(new Rect(x+w-bsz+2,y-2,bsz,bsz),_wh);
        GUI.DrawTexture(new Rect(x-2,y+h-bsz+2,bsz,bsz),_wh);
        GUI.DrawTexture(new Rect(x+w-bsz+2,y+h-bsz+2,bsz,bsz),_wh);
    }

    void DrawMarquee(float x, float y, float w, float h, float t)
    {
        // Dark bg with gradient
        GUI.color = new Color(0.04f,0.01f,0.08f,_alpha);
        GUI.DrawTexture(new Rect(x,y,w,h),_wh);

        // Scrolling light dots at top
        for (int i = 0; i < 20; i++)
        {
            float dot_x = x + (i / 20f + t * 0.15f % 1f) * w;
            float blink = (Mathf.Sin(t * 8f + i * 0.8f) + 1f) * .5f;
            Color dc = Color.Lerp(NEON_RED, NEON_YELLOW, i / 20f);
            GUI.color = new Color(dc.r,dc.g,dc.b, blink * _alpha);
            GUI.DrawTexture(new Rect(dot_x - 3f, y + 4f, 6f, 6f), _wh);
        }

        // LUCKY 7 SLOTS title
        float pulse = 0.85f + 0.15f * Mathf.Sin(t * 4f);
        var ts = Sty(34, FontStyle.Bold, new Color(NEON_YELLOW.r,NEON_YELLOW.g,NEON_YELLOW.b,pulse*_alpha), TextAnchor.MiddleCenter);
        // Glow shadow
        GUI.color = new Color(NEON_RED.r,NEON_RED.g,NEON_RED.b, 0.5f*_alpha);
        GUI.Label(new Rect(x+3, y+3, w, h-10f), "LUCKY  7  SLOTS", ts);
        GUI.color = new Color(NEON_YELLOW.r,NEON_YELLOW.g,NEON_YELLOW.b, pulse*_alpha);
        GUI.Label(new Rect(x, y, w, h-10f), "LUCKY  7  SLOTS", ts);

        // Subtitle
        var sub = Sty(11, FontStyle.Bold, new Color(CHROME.r,CHROME.g,CHROME.b,0.6f*_alpha), TextAnchor.MiddleCenter);
        GUI.color = new Color(1,1,1,_alpha);
        GUI.Label(new Rect(x, y+h-18f, w, 16f), "★  INSERT COIN AND SPIN  ★", sub);
    }

    void DrawScreen(float x, float y, float w, float h, float t)
    {
        // CRT screen bg
        GUI.color = new Color(SCREEN_BG.r,SCREEN_BG.g,SCREEN_BG.b,_alpha);
        GUI.DrawTexture(new Rect(x,y,w,h),_wh);

        // Inner chrome bezel
        GUI.color = new Color(CHROME_DIM.r,CHROME_DIM.g,CHROME_DIM.b,0.7f*_alpha);
        DrawBorder(x-3,y-3,w+6,h+6,3f);

        // CRT scanlines
        GUI.color = new Color(0,0,0, 0.25f*_alpha);
        GUI.DrawTextureWithTexCoords(new Rect(x,y,w,h), _scanline, new Rect(0,0,w/2f,h/2f));

        // Status message (on screen)
        Color mc = _win ? NEON_GREEN : (_state == State.Spinning ? NEON_CYAN : new Color(CHROME.r,CHROME.g,CHROME.b));
        float mp = _state == State.Result ? 0.75f + 0.25f * Mathf.Sin(t*5f) : 1f;
        var ms = Sty(15, FontStyle.Bold, new Color(mc.r,mc.g,mc.b,mp*_alpha), TextAnchor.UpperCenter);
        GUI.color = new Color(1,1,1,_alpha);
        GUI.Label(new Rect(x+6f, y+6f, w-12f, 26f), _msg, ms);

        // ── Three reels ────────────────────────────────────────────────────
        float rw = (w - 50f) / 3f;
        float rh = h - 50f;
        float ry = y + 34f;
        float rx = x + 18f;

        for (int i = 0; i < 3; i++)
            DrawReel(rx + i*(rw+7f), ry, rw, rh, i, t);
    }

    void DrawReel(float x, float y, float w, float h, int idx, float t)
    {
        // Reel drum background
        GUI.color = new Color(REEL_BG.r,REEL_BG.g,REEL_BG.b,_alpha);
        GUI.DrawTexture(new Rect(x,y,w,h),_wh);

        // Chrome border — gold when stopped
        Color bc = _stopped[idx]
            ? new Color(NEON_YELLOW.r,NEON_YELLOW.g,NEON_YELLOW.b,_alpha)
            : new Color(CHROME_DIM.r,CHROME_DIM.g,CHROME_DIM.b,0.7f*_alpha);
        GUI.color = bc; DrawBorder(x-1.5f,y-1.5f,w+3,h+3,2f);

        // Win flash glow
        if (_stopped[idx] && _flash > 0f)
        {
            GUI.color = new Color(NEON_YELLOW.r,NEON_YELLOW.g,NEON_YELLOW.b, _flash*0.3f*_alpha);
            GUI.DrawTexture(new Rect(x-4,y-4,w+8,h+8),_wh);
        }

        // Symbols (3 visible: above, center, below)
        float symH = h / 3f;
        int cur  = (int)_spinVal[idx] % SYM.Length; if (cur < 0) cur += SYM.Length;
        int prev = (cur - 1 + SYM.Length) % SYM.Length;
        int next = (cur + 1) % SYM.Length;
        float scroll = (_spinVal[idx] % 1f) * symH;

        DrawSymLabel(x, y - scroll,          w, symH, prev, 0.18f);
        DrawSymLabel(x, y + symH - scroll,   w, symH, cur,  _stopped[idx] ? 1f : 0.65f);
        DrawSymLabel(x, y + symH*2 - scroll, w, symH, next, 0.18f);

        // Pay line marks (left + right of center)
        GUI.color = new Color(NEON_RED.r,NEON_RED.g,NEON_RED.b,0.55f*_alpha);
        GUI.DrawTexture(new Rect(x-6, y+symH-1.5f, 5f, 3f),_wh);
        GUI.DrawTexture(new Rect(x+w+1, y+symH-1.5f, 5f, 3f),_wh);
        GUI.color = new Color(NEON_RED.r,NEON_RED.g,NEON_RED.b,0.3f*_alpha);
        GUI.DrawTexture(new Rect(x, y+symH-1f, w, 2f),_wh);
        GUI.color = new Color(NEON_RED.r,NEON_RED.g,NEON_RED.b,0.15f*_alpha);
        GUI.DrawTexture(new Rect(x, y+symH*2-1f, w, 2f),_wh);
    }

    void DrawSymLabel(float x, float y, float w, float h, int si, float alpha)
    {
        // Color per symbol
        Color[] cols = { NEON_RED, NEON_YELLOW, NEON_CYAN, NEON_GREEN, NEON_PINK, new Color(0.7f,0.7f,1f), CHROME };
        var s = Sty(si == 6 ? 18 : 34, FontStyle.Bold,
            new Color(cols[si].r,cols[si].g,cols[si].b, alpha*_alpha),
            TextAnchor.MiddleCenter);
        GUI.color = new Color(1,1,1,_alpha);
        GUI.Label(new Rect(x,y,w,h), SYM[si], s);
    }

    void DrawInfoStrip(float x, float y, float w, float h, float t)
    {
        GUI.color = new Color(0.08f,0.03f,0.12f,0.9f*_alpha);
        GUI.DrawTexture(new Rect(x,y,w,h),_wh);
        GUI.color = new Color(NEON_CYAN.r,NEON_CYAN.g,NEON_CYAN.b,0.4f*_alpha);
        GUI.DrawTexture(new Rect(x,y,w,1.5f),_wh);
        GUI.DrawTexture(new Rect(x,y+h-1.5f,w,1.5f),_wh);
        string pay = "7×10  ★×5  BAR×3  ♦×2.5  Suits×2  │  2-match = bet back";
        var ps = Sty(11, FontStyle.Bold, new Color(CHROME.r,CHROME.g,CHROME.b,0.65f*_alpha), TextAnchor.MiddleCenter);
        GUI.color = new Color(1,1,1,_alpha);
        GUI.Label(new Rect(x,y,w,h), pay, ps);
    }

    void DrawBetPanel(float x, float y, float w, float h, float t)
    {
        // Two-tone panel
        GUI.color = new Color(0.07f,0.02f,0.10f,0.95f*_alpha);
        GUI.DrawTexture(new Rect(x,y,w,h),_wh);

        float mid = y + h*.5f - 22f;

        // Bet label + value
        var bl = Sty(12,FontStyle.Bold,new Color(CHROME.r,CHROME.g,CHROME.b,0.6f*_alpha),TextAnchor.MiddleLeft);
        GUI.color=new Color(1,1,1,_alpha);
        GUI.Label(new Rect(x+18f,mid-4f,60f,18f),"COINS",bl);
        var ba = Sty(28,FontStyle.Bold,new Color(NEON_YELLOW.r,NEON_YELLOW.g,NEON_YELLOW.b,_alpha),TextAnchor.MiddleLeft);
        GUI.Label(new Rect(x+18f,mid+10f,140f,36f),$"{_bet} EC",ba);

        // Bet buttons
        bool idle = _state==State.Idle||_state==State.Result;
        float bx = x+135f;
        if (ArcadeBtn(bx,       mid+4f, 48f, 38f, "MIN", NEON_RED,   false) && idle) _bet=10;
        if (ArcadeBtn(bx+54f,   mid+4f, 48f, 38f, "−10", NEON_RED,   false) && idle) _bet=Mathf.Max(10,_bet-10);
        if (ArcadeBtn(bx+108f,  mid+4f, 48f, 38f, "+10", NEON_GREEN, false) && idle) _bet+=10;
        if (ArcadeBtn(bx+162f,  mid+4f, 48f, 38f, "1/2", NEON_CYAN,  false) && idle) _bet=Mathf.Max(10,_bet/2);
        if (ArcadeBtn(bx+216f,  mid+4f, 54f, 38f, "MAX", NEON_CYAN,  false) && idle) _bet=Mathf.Max(10,_inventory.credits);

        // Big SPIN button
        bool can = idle && _inventory.credits >= _bet && _bet >= 10;
        Color sc = can ? NEON_YELLOW : CHROME_DIM;
        string sl = idle ? "▶  SPIN" : "• • •";

        // Lever visual (left of spin btn)
        float lx = x + w - 280f;
        float leverPull = Mathf.Sin(_leverT * Mathf.PI) * 22f;
        GUI.color = new Color(CHROME.r,CHROME.g,CHROME.b,0.6f*_alpha);
        GUI.DrawTexture(new Rect(lx+16f, mid-8f+leverPull, 6f, 40f), _wh);
        GUI.color = new Color(NEON_RED.r,NEON_RED.g,NEON_RED.b,_alpha);
        GUI.DrawTexture(new Rect(lx+8f, mid-10f+leverPull, 22f, 16f), _wh);
        GUI.color = new Color(0.8f,0.1f,0.1f,_alpha);
        GUI.DrawTexture(new Rect(lx+10f, mid-8f+leverPull, 18f, 12f), _wh);

        if (ArcadeBtn(x+w-240f, mid-2f, 210f, 52f, sl, sc, true) && can) StartSpin();
    }

    void DrawBalance(float x, float y, int creds)
    {
        // LED-style credit display top-left of screen
        GUI.color = new Color(0.03f,0.08f,0.03f,0.9f*_alpha);
        GUI.DrawTexture(new Rect(x+8,y,120f,28f),_wh);
        GUI.color = new Color(0,NEON_GREEN.g*0.3f,0,0.4f*_alpha);
        DrawBorder(x+8,y,120f,28f,1.5f);
        var cs = Sty(15,FontStyle.Bold,new Color(NEON_GREEN.r,NEON_GREEN.g,NEON_GREEN.b,_alpha),TextAnchor.MiddleCenter);
        GUI.color = new Color(1,1,1,_alpha);
        GUI.Label(new Rect(x+8,y,120f,28f),$"◈ {creds} EC",cs);
    }

    void DrawClose(float x, float y)
    {
        Rect r = new Rect(x,y,32f,32f);
        Vector2 mp = new Vector2(Input.mousePosition.x, Screen.height-Input.mousePosition.y);
        bool hov = r.Contains(mp);
        GUI.color = new Color(NEON_RED.r,NEON_RED.g,NEON_RED.b,(hov?0.9f:0.3f)*_alpha);
        GUI.DrawTexture(new Rect(x-1,y-1,34f,34f),_wh);
        GUI.color = new Color(0.05f,0.02f,0.08f,_alpha);
        GUI.DrawTexture(r,_wh);
        var xs = Sty(20,FontStyle.Bold,hov?NEON_RED:new Color(NEON_RED.r,NEON_RED.g,NEON_RED.b,0.6f),TextAnchor.MiddleCenter);
        GUI.color = new Color(1,1,1,_alpha); GUI.Label(r,"✕",xs);
        if (hov&&Event.current.type==EventType.MouseDown&&Event.current.button==0){Event.current.Use();CloseStation();}
    }

    bool ArcadeBtn(float x, float y, float w, float h, string txt, Color col, bool large)
    {
        Rect r = new Rect(x,y,w,h);
        Vector2 mp = new Vector2(Input.mousePosition.x, Screen.height-Input.mousePosition.y);
        bool hov = r.Contains(mp);

        // Button shadow (3D pressed look)
        GUI.color = new Color(0,0,0,0.6f*_alpha);
        GUI.DrawTexture(new Rect(x+4,y+5,w,h),_wh);
        // Outer glow
        GUI.color = new Color(col.r,col.g,col.b,(hov?0.95f:0.35f)*_alpha);
        GUI.DrawTexture(new Rect(x-2,y-2,w+4,h+4),_wh);
        // Body (darker shade)
        GUI.color = new Color(col.r*0.12f,col.g*0.12f,col.b*0.12f,0.95f*_alpha);
        GUI.DrawTexture(r,_wh);
        // Top gloss
        GUI.color = new Color(1,1,1,(hov?0.18f:0.08f)*_alpha);
        GUI.DrawTexture(new Rect(x+2,y+2,w-4,h*.35f),_wh);

        // Text
        int fs = large ? 20 : 15;
        Color tc = hov ? Color.white : new Color(col.r,col.g,col.b,0.9f);
        // glow text shadow
        var s = Sty(fs,FontStyle.Bold,new Color(col.r,col.g,col.b,0.4f*_alpha),TextAnchor.MiddleCenter);
        GUI.color = new Color(1,1,1,_alpha); GUI.Label(new Rect(x+2,y+2,w,h),txt,s);
        s.normal.textColor = new Color(tc.r,tc.g,tc.b,_alpha);
        GUI.Label(r,txt,s);

        if (hov&&Event.current.type==EventType.MouseDown&&Event.current.button==0){Event.current.Use();return true;}
        return false;
    }

    void DrawBorder(float x,float y,float w,float h,float t)
    {
        GUI.DrawTexture(new Rect(x,y,w,t),_wh);
        GUI.DrawTexture(new Rect(x,y+h-t,w,t),_wh);
        GUI.DrawTexture(new Rect(x,y,t,h),_wh);
        GUI.DrawTexture(new Rect(x+w-t,y,t,h),_wh);
    }

    GUIStyle Sty(int sz,FontStyle fs,Color col,TextAnchor a)
    { var s=new GUIStyle(); s.fontSize=sz; s.fontStyle=fs; s.normal.textColor=col; s.alignment=a; s.richText=true; return s; }
}
