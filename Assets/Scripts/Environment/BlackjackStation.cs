using UnityEngine;
using System.Collections.Generic;

public class BlackjackStation : MonoBehaviour, IInteractable
{
    public bool isOpen = false;
    private PlayerInventory _inventory;

    // ── Palette: Luxury Royal Casino ────────────────────────────────────────
    private static readonly Color GOLD         = new Color(1.00f, 0.82f, 0.22f);
    private static readonly Color GOLD_DARK    = new Color(0.70f, 0.52f, 0.05f);
    private static readonly Color GOLD_DIM     = new Color(1.00f, 0.82f, 0.22f, 0.25f);
    private static readonly Color EMERALD      = new Color(0.05f, 0.72f, 0.35f);
    private static readonly Color EMERALD_DARK = new Color(0.02f, 0.18f, 0.09f);
    private static readonly Color EMERALD_MID  = new Color(0.03f, 0.35f, 0.16f);
    private static readonly Color CRIMSON      = new Color(0.95f, 0.15f, 0.20f);
    private static readonly Color IVORY        = new Color(0.98f, 0.96f, 0.90f);
    private static readonly Color VELVET       = new Color(0.06f, 0.03f, 0.10f);
    private static readonly Color VELVET_PANEL = new Color(0.09f, 0.05f, 0.14f, 0.97f);
    private static readonly Color SHADOW       = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color WIN_COLOR    = new Color(0.20f, 1.00f, 0.45f);
    private static readonly Color LOSE_COLOR   = new Color(1.00f, 0.25f, 0.30f);
    private static readonly Color PUSH_COLOR   = new Color(0.80f, 0.80f, 0.40f);

    // ── Game State ───────────────────────────────────────────────────────────
    enum GameState { Idle, WaitingForServer, PlayerTurn, DealerTurn, GameOver }
    private GameState state = GameState.Idle;
    private int betAmount = 10;
    private string statusMessage = "Place your bet and press DEAL";
    private bool lastResultWin = false;

    private List<Card> deck        = new List<Card>();
    private List<Card> playerHand  = new List<Card>();
    private List<Card> dealerHand  = new List<Card>();

    // ── Animation ────────────────────────────────────────────────────────────
    private float panelAlpha   = 0f;
    private float panelScale   = 0.85f;        // zoom-in on open
    private float winFlash     = 0f;           // glow flash on win
    private float chipBounce   = 0f;
    private float statusPulse  = 0f;

    // Particles (simple)
    private struct Particle { public Vector2 pos, vel; public float life, maxLife; public Color col; public float size; }
    private List<Particle> _particles = new List<Particle>();

    // ── Textures ─────────────────────────────────────────────────────────────
    private Texture2D _white;
    private Texture2D _overlayTex;
    private Texture2D _feltTex;       // 4×4 subtle felt noise
    private Texture2D _cardFrontTex;
    private Texture2D _cardBackTex;
    private Texture2D _gradientTex;   // top-gold gradient stripe

    public class Card
    {
        public string suit, rank;
        public int value;
        public bool isHidden = false;
        public Vector2 pos = new Vector2(-600, 0);
        public float rotation = 0f;
        public bool initialized = false;
    }

    // ── Awake ────────────────────────────────────────────────────────────────
    void Awake()
    {
        _white      = Texture2D.whiteTexture;
        _overlayTex = MakeTex(1, 1, new Color(0.04f, 0.02f, 0.08f, 0.94f));
        _feltTex    = MakeFeltTexture();
        _cardFrontTex = MakeTex(1, 1, IVORY);
        _gradientTex  = MakeGradientH(new Color(1f, 0.82f, 0.1f, 0.6f), new Color(0.5f, 0.3f, 0.0f, 0f));
    }

    void OnEnable() {
        PlayerInventory.OnBlackjackStartResult += HandleStart;
        PlayerInventory.OnBlackjackHitResult += HandleHit;
        PlayerInventory.OnBlackjackStandResult += HandleStand;
    }
    void OnDisable() {
        PlayerInventory.OnBlackjackStartResult -= HandleStart;
        PlayerInventory.OnBlackjackHitResult -= HandleHit;
        PlayerInventory.OnBlackjackStandResult -= HandleStand;
        if (isOpen) { isOpen = false; PlayerController.OpenMinigameCount--; }
    }

    Card IntToCard(int c) {
        string[] suits = { "♠", "♣", "♦", "♥" };
        string[] ranks = { "A","2","3","4","5","6","7","8","9","10","J","Q","K" };
        int suitIdx = c / 13;
        int rankIdx = c % 13;
        int val = rankIdx + 1; if (val > 10) val = 10;
        return new Card { suit = suits[suitIdx], rank = ranks[rankIdx], value = val };
    }

    void HandleStart(int[] pHand, int[] dHand) {
        if (state != GameState.WaitingForServer) return;
        playerHand.Clear(); dealerHand.Clear();
        foreach(int c in pHand) playerHand.Add(IntToCard(c));
        foreach(int c in dHand) dealerHand.Add(IntToCard(c));
        dealerHand[1].isHidden = true;
        
        state = GameState.PlayerTurn;
        statusMessage = "Your turn  —  HIT or STAND?";
        
        if (GetScore(playerHand) == 21) { statusMessage = "✦ BLACKJACK! Natural 21! ✦"; EndGameCheck(); }
    }
    
    void HandleHit(int card) {
        if (state != GameState.WaitingForServer) return;
        playerHand.Add(IntToCard(card));
        int score = GetScore(playerHand);
        
        if (score > 21) {
            statusMessage = "BUST  —  Over 21";
            EndGameCheck();
        } else if (playerHand.Count >= 5) {
            statusMessage = "❇  NGŨ LINH  —  Five Card Charlie!  ❇";
            EndGameCheck();
        } else {
            state = GameState.PlayerTurn;
            statusMessage = $"Card dealt  ({score} pts)  —  HIT or STAND?";
        }
    }
    
    void HandleStand(int[] drawn) {
        if (state != GameState.WaitingForServer) return;
        foreach(int c in drawn) dealerHand.Add(IntToCard(c));
        EndGameCheck();
    }

    // ── Texture helpers ───────────────────────────────────────────────────────
    Texture2D MakeTex(int w, int h, Color c)
    {
        Texture2D t = new Texture2D(w, h);
        Color[] px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = c;
        t.SetPixels(px); t.Apply(); return t;
    }

    Texture2D MakeFeltTexture()
    {
        Texture2D t = new Texture2D(4, 4);
        t.filterMode = FilterMode.Point;
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                float n = Random.Range(0f, 0.04f);
                t.SetPixel(x, y, new Color(0.03f + n, 0.18f + n, 0.09f + n, 1f));
            }
        t.Apply(); return t;
    }

    Texture2D MakeGradientH(Color left, Color right)
    {
        int w = 64;
        Texture2D t = new Texture2D(w, 1);
        for (int x = 0; x < w; x++)
            t.SetPixel(x, 0, Color.Lerp(left, right, (float)x / (w - 1)));
        t.Apply(); return t;
    }

    // ── Interaction ───────────────────────────────────────────────────────────
    public void Interact(GameObject interactor)
    {
        if (isOpen) return;
        PlayerInventory inv = interactor.GetComponentInParent<PlayerInventory>()
                           ?? interactor.GetComponentInChildren<PlayerInventory>();
        if (inv == null || !inv.IsOwner) return;

        _inventory = inv;
        isOpen     = true;
        PlayerController.OpenMinigameCount++;
        panelAlpha = 0f;
        panelScale = 0.88f;
        winFlash   = 0f;
        _particles.Clear();
        ResetGame();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    void CloseStation()
    {
        if (isOpen) { isOpen = false; PlayerController.OpenMinigameCount--; }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    // ── Update ────────────────────────────────────────────────────────────────
    void Update()
    {
        if (!isOpen) return;

        float dt = Time.unscaledDeltaTime;
        panelAlpha  = Mathf.Lerp(panelAlpha,  1f,   dt * 9f);
        panelScale  = Mathf.Lerp(panelScale,  1f,   dt * 8f);
        winFlash    = Mathf.Max(0f, winFlash  - dt * 1.2f);
        chipBounce += dt * 3.5f;
        statusPulse += dt * 4f;

        // Update particles
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.life -= dt;
            p.pos  += p.vel * dt;
            p.vel  += new Vector2(0, 120f) * dt;   // gravity
            _particles[i] = p;
            if (p.life <= 0f) _particles.RemoveAt(i);
        }

        if (Input.GetKeyDown(KeyCode.Escape)) CloseStation();
    }

    // ── Spawn Win Particles ───────────────────────────────────────────────────
    void SpawnWinParticles(float cx, float cy)
    {
        Color[] cols = { GOLD, EMERALD, WIN_COLOR, new Color(1f,1f,0.4f), new Color(0.4f,1f,1f) };
        for (int i = 0; i < 60; i++)
        {
            float ang  = Random.Range(0f, Mathf.PI * 2f);
            float spd  = Random.Range(100f, 400f);
            _particles.Add(new Particle
            {
                pos     = new Vector2(cx, cy),
                vel     = new Vector2(Mathf.Cos(ang) * spd, Mathf.Sin(ang) * spd - 200f),
                life    = Random.Range(0.6f, 1.4f),
                maxLife = 1.4f,
                col     = cols[Random.Range(0, cols.Length)],
                size    = Random.Range(4f, 10f)
            });
        }
    }

    void ResetGame()
    {
        state         = GameState.Idle;
        lastResultWin = false;
        playerHand.Clear(); dealerHand.Clear();
        statusMessage = "Place your bet and press DEAL";
    }

    void StartGame()
    {
        if (betAmount < 10) betAmount = 10;
        if (_inventory.credits < betAmount || _inventory.credits < 10)
        { statusMessage = "⚠  INSUFFICIENT FUNDS  ( min 10 EC )"; return; }

        state = GameState.WaitingForServer;
        statusMessage = "Waiting for dealer...";
        _inventory.RequestBlackjackStartServerRpc(betAmount);
    }

    void Hit()
    {
        state = GameState.WaitingForServer;
        statusMessage = "Waiting for card...";
        _inventory.RequestBlackjackHitServerRpc();
    }

    void Stand()
    {
        state = GameState.WaitingForServer;
        statusMessage = "Dealer's turn...";
        _inventory.RequestBlackjackStandServerRpc();
    }

    void EndGameCheck()
    {
        state = GameState.GameOver;
        if (dealerHand.Count > 1) dealerHand[1].isHidden = false;

        int pScore = GetScore(playerHand);
        int dScore = GetScore(dealerHand);

        if (pScore > 21)
        { statusMessage = "BUST  —  Better luck next time"; lastResultWin = false; }
        else if (playerHand.Count >= 5 && pScore <= 21)
        {
            statusMessage = "❇  NGŨ LINH  —  Five Card Charlie! 2×  ❇";
            lastResultWin = true;
        }
        else if (dScore > 21)
        { statusMessage = "✦  DEALER BUST  —  You Win!  ✦"; lastResultWin = true; }
        else if (pScore == 21 && playerHand.Count == 2)
        { statusMessage = "✦  BLACKJACK  —  2.5× Payout!  ✦"; lastResultWin = true; }
        else if (pScore > dScore)
        { statusMessage = "✦  VICTORY  —  You Win!  ✦"; lastResultWin = true; }
        else if (pScore < dScore)
        { statusMessage = "Dealer wins  —  Try again"; lastResultWin = false; }
        else
        { statusMessage = "PUSH  —  Bet returned"; lastResultWin = false; }


        if (lastResultWin)
        {
            winFlash = 1f;
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            SpawnWinParticles(cx, cy);
        }
    }

    int GetScore(List<Card> hand)
    {
        int score = 0, aces = 0;
        foreach (var c in hand) { if (c.isHidden) continue; score += c.value; if (c.value == 1) aces++; }
        while (aces > 0 && score + 10 <= 21) { score += 10; aces--; }
        return score;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  O N G U I   —  LUXURY ROYAL CASINO
    // ═══════════════════════════════════════════════════════════════════════════
    void OnGUI()
    {
        if (!isOpen || _inventory == null) return;
        GUI.depth = -10;

        float sw = Screen.width, sh = Screen.height;
        float t  = Time.unscaledTime;

        // ── Cinematic overlay ────────────────────────────────────────────────
        GUI.color = new Color(1, 1, 1, panelAlpha);
        GUI.DrawTexture(new Rect(0, 0, sw, sh), _overlayTex);

        // Draw particles
        foreach (var p in _particles)
        {
            float a = Mathf.Clamp01(p.life / p.maxLife);
            GUI.color = new Color(p.col.r, p.col.g, p.col.b, a * panelAlpha);
            GUI.DrawTexture(new Rect(p.pos.x - p.size * 0.5f, p.pos.y - p.size * 0.5f, p.size, p.size), _white);
        }

        // ── Panel geometry (scale-in animation) ──────────────────────────────
        float panelW = Mathf.Min(sw * 0.82f, 920f);
        float panelH = Mathf.Min(sh * 0.86f, 640f);
        float rawPx  = (sw - panelW) * 0.5f;
        float rawPy  = (sh - panelH) * 0.5f;

        // Apply scale pivot from center
        float scaledW = panelW * panelScale;
        float scaledH = panelH * panelScale;
        float px = rawPx + (panelW - scaledW) * 0.5f;
        float py = rawPy + (panelH - scaledH) * 0.5f;
        panelW = scaledW; panelH = scaledH;

        // ── Win glow aura behind panel ────────────────────────────────────────
        if (winFlash > 0f)
        {
            float glow = winFlash * panelAlpha;
            float spread = 30f * winFlash;
            GUI.color = new Color(WIN_COLOR.r, WIN_COLOR.g, WIN_COLOR.b, glow * 0.35f);
            GUI.DrawTexture(new Rect(px - spread, py - spread, panelW + spread*2, panelH + spread*2), _white);
        }

        // ── Panel drop shadow ─────────────────────────────────────────────────
        GUI.color = new Color(0, 0, 0, 0.6f * panelAlpha);
        GUI.DrawTexture(new Rect(px + 12, py + 14, panelW, panelH), _white);

        // ── Outer gold frame (thick) ──────────────────────────────────────────
        float goldPulse = (Mathf.Sin(t * 2.5f) + 1f) * 0.5f;
        Color outerBorder = Color.Lerp(GOLD_DARK, GOLD, goldPulse);
        outerBorder.a = panelAlpha;
        GUI.color = outerBorder;
        float borderThick = 4f;
        GUI.DrawTexture(new Rect(px - borderThick, py - borderThick, panelW + borderThick*2, panelH + borderThick*2), _white);

        // ── Velvet panel body ─────────────────────────────────────────────────
        GUI.color = new Color(VELVET_PANEL.r, VELVET_PANEL.g, VELVET_PANEL.b, VELVET_PANEL.a * panelAlpha);
        GUI.DrawTexture(new Rect(px, py, panelW, panelH), _white);

        // ── HEADER ZONE (dark velvet strip) ───────────────────────────────────
        float headerH = 72f;
        GUI.color = new Color(0.04f, 0.02f, 0.08f, 0.98f * panelAlpha);
        GUI.DrawTexture(new Rect(px, py, panelW, headerH), _white);

        // Gold top border line
        GUI.color = new Color(GOLD.r, GOLD.g, GOLD.b, panelAlpha);
        GUI.DrawTexture(new Rect(px, py, panelW, 2f), _white);
        GUI.DrawTexture(new Rect(px, py + headerH - 2f, panelW, 2f), _white);

        // Gradient shimmer on header
        GUI.color = new Color(1, 1, 1, 0.18f * panelAlpha);
        GUI.DrawTexture(new Rect(px, py, panelW * 0.5f, headerH), _gradientTex);

        // ── Title ─────────────────────────────────────────────────────────────
        GUIStyle titleStyle = NewStyle(38, FontStyle.Bold, GOLD, TextAnchor.MiddleCenter);
        titleStyle.richText = true;
        // Shadow
        GUI.color = new Color(0, 0, 0, 0.7f * panelAlpha);
        GUI.Label(new Rect(px + 2, py + 3, panelW, headerH), "♠  ROYAL BLACKJACK  ♠", titleStyle);
        // Main text with pulse
        Color titleCol = Color.Lerp(GOLD, Color.white, goldPulse * 0.3f);
        titleCol.a = panelAlpha;
        GUI.color = titleCol;
        GUI.Label(new Rect(px, py, panelW, headerH), "♠  ROYAL BLACKJACK  ♠", titleStyle);

        // ── EC Balance chip (top-left) ────────────────────────────────────────
        DrawChipBalance(px + 20f, py + 12f, _inventory.credits);

        // ── EXIT button (top-right) ───────────────────────────────────────────
        DrawExitButton(px + panelW - 50f, py + 18f, 36f, 36f);

        // ── FELT play area ────────────────────────────────────────────────────
        float feltY = py + headerH + 4f;
        float feltH = panelH - headerH - 110f;

        // Felt texture tile
        GUI.color = new Color(1, 1, 1, panelAlpha * 0.9f);
        GUI.DrawTextureWithTexCoords(new Rect(px + 4, feltY, panelW - 8, feltH), _feltTex,
            new Rect(0, 0, (panelW - 8) / 4f, feltH / 4f));

        // Felt inner gold oval ring (decorative)
        DrawGoldOvalRing(px + panelW * 0.5f, feltY + feltH * 0.5f - 10f, panelW * 0.72f, feltH * 0.75f);

        // ── STATUS BAR ────────────────────────────────────────────────────────
        float statusY = py + headerH + 8f;
        DrawStatusBar(px, statusY, panelW);

        // ── HANDS ─────────────────────────────────────────────────────────────
        if (state != GameState.Idle)
        {
            float cardAreaY  = feltY + 30f;
            float dealerRowY = cardAreaY;
            float playerRowY = cardAreaY + 175f;

            DrawHandRow(px, dealerRowY, panelW, "DEALER", dealerHand, GetScore(dealerHand), true);
            DrawHandRow(px, playerRowY, panelW, "YOU",    playerHand,  GetScore(playerHand), false);
        }
        else
        {
            // Idle: show decorative card suits
            DrawIdleDecor(px, feltY, panelW, feltH);
        }

        // ── BOTTOM CONTROLS ───────────────────────────────────────────────────
        float ctrlY  = py + panelH - 100f;
        float ctrlH  = 96f;

        // Control zone background
        GUI.color = new Color(0.04f, 0.02f, 0.08f, 0.95f * panelAlpha);
        GUI.DrawTexture(new Rect(px, ctrlY, panelW, ctrlH), _white);
        GUI.color = new Color(GOLD.r, GOLD.g, GOLD.b, 0.5f * panelAlpha);
        GUI.DrawTexture(new Rect(px, ctrlY, panelW, 2f), _white);

        DrawControls(px, ctrlY, panelW, ctrlH);

        // ── Decorative corner ornaments ───────────────────────────────────────
        DrawGoldCornerOrnaments(px, py, panelW, panelH);

        GUI.color = Color.white;
    }

    // ── Chip Balance Display ──────────────────────────────────────────────────
    void DrawChipBalance(float x, float y, int credits)
    {
        float chipR = 22f;
        // Chip circle
        Color chipCol = new Color(GOLD.r, GOLD.g, GOLD.b, panelAlpha);
        GUI.color = new Color(0, 0, 0, 0.5f * panelAlpha);
        DrawCircle(x + chipR + 2, y + chipR + 2, chipR + 2);
        GUI.color = chipCol;
        DrawCircle(x + chipR, y + chipR, chipR);
        GUI.color = new Color(VELVET.r, VELVET.g, VELVET.b, panelAlpha);
        DrawCircle(x + chipR, y + chipR, chipR - 5f);
        GUI.color = chipCol;
        DrawCircle(x + chipR, y + chipR, chipR - 8f);

        // EC icon
        GUIStyle ecIcon = NewStyle(13, FontStyle.Bold, VELVET, TextAnchor.MiddleCenter);
        GUI.color = new Color(1,1,1, panelAlpha);
        GUI.Label(new Rect(x, y + chipR * 0.35f, chipR * 2, chipR), "EC", ecIcon);

        // Credits text
        GUIStyle credStyle = NewStyle(18, FontStyle.Bold, GOLD, TextAnchor.MiddleLeft);
        GUI.color = new Color(1,1,1, panelAlpha);
        GUI.Label(new Rect(x + chipR * 2 + 10f, y + 4f, 200f, 30f), $"{credits:N0} EC", credStyle);
    }

    // ── Exit Button ───────────────────────────────────────────────────────────
    void DrawExitButton(float x, float y, float w, float h)
    {
        Rect r = new Rect(x, y, w, h);
        Vector2 mp = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        bool hover = r.Contains(mp);

        GUI.color = new Color(CRIMSON.r, CRIMSON.g, CRIMSON.b, hover ? 0.85f * panelAlpha : 0.35f * panelAlpha);
        GUI.DrawTexture(new Rect(x - 1, y - 1, w + 2, h + 2), _white);
        GUI.color = new Color(VELVET.r * 1.5f, VELVET.g, VELVET.b, panelAlpha);
        GUI.DrawTexture(r, _white);

        GUIStyle s = NewStyle(22, FontStyle.Bold, hover ? Color.white : new Color(1f,0.4f,0.4f), TextAnchor.MiddleCenter);
        GUI.color = new Color(1,1,1, panelAlpha);
        GUI.Label(r, "✕", s);

        if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
        { PlayClickSound(); Event.current.Use(); CloseStation(); }
    }

    // ── Status Bar ────────────────────────────────────────────────────────────
    void DrawStatusBar(float px, float y, float panelW)
    {
        float t = Time.unscaledTime;

        // Determine color
        Color statusCol;
        if (statusMessage.Contains("WIN") || statusMessage.Contains("VICTORY") || statusMessage.Contains("BLACKJACK") || statusMessage.Contains("BUST  —  Dealer"))
            statusCol = WIN_COLOR;
        else if (statusMessage.Contains("BUST") || statusMessage.Contains("Better luck") || statusMessage.Contains("INSUFFICIENT"))
            statusCol = LOSE_COLOR;
        else if (statusMessage.Contains("PUSH"))
            statusCol = PUSH_COLOR;
        else
            statusCol = new Color(0.85f, 0.92f, 1.0f);

        float pulse = state == GameState.GameOver ? (Mathf.Sin(statusPulse * 1.5f) + 1f) * 0.5f * 0.3f + 0.7f : 1f;

        // Glow bg
        GUI.color = new Color(statusCol.r, statusCol.g, statusCol.b, 0.12f * panelAlpha * pulse);
        GUI.DrawTexture(new Rect(px + 100f, y + 2f, panelW - 200f, 28f), _white);

        // Text
        GUIStyle ss = NewStyle(18, FontStyle.Bold, new Color(statusCol.r, statusCol.g, statusCol.b, panelAlpha * pulse), TextAnchor.MiddleCenter);
        GUI.color = new Color(1,1,1, panelAlpha);
        // shadow
        GUI.color = new Color(0,0,0, 0.5f * panelAlpha);
        GUI.Label(new Rect(px + 1, y + 3, panelW, 30f), statusMessage, ss);
        GUI.color = new Color(statusCol.r, statusCol.g, statusCol.b, panelAlpha * pulse);
        GUI.Label(new Rect(px, y + 2, panelW, 30f), statusMessage, ss);
        GUI.color = Color.white;
    }

    // ── Hand Row (dealer or player) ────────────────────────────────────────────
    void DrawHandRow(float px, float rowY, float panelW, string label, List<Card> hand, int score, bool isDealer)
    {
        float t   = Time.unscaledTime;

        // Row label + score badge
        string scoreStr = isDealer && hand.Count > 1 && hand[1].isHidden ? "?" : score.ToString();
        Color labelCol  = isDealer ? new Color(0.9f, 0.5f, 0.5f) : new Color(0.5f, 0.9f, 1.0f);

        // Label shadow
        GUIStyle lblStyle = NewStyle(14, FontStyle.Bold, labelCol, TextAnchor.MiddleCenter);
        GUI.color = new Color(0,0,0, 0.6f * panelAlpha);
        GUI.Label(new Rect(px + 1, rowY + 1, 90f, 22f), label, lblStyle);
        GUI.color = new Color(labelCol.r, labelCol.g, labelCol.b, panelAlpha);
        GUI.Label(new Rect(px, rowY, 90f, 22f), label, lblStyle);

        // Score badge
        DrawScoreBadge(px + 90f, rowY, scoreStr, labelCol, isDealer && score > 17);

        // Cards
        float cardW   = 72f;
        float cardH   = 108f;
        float spacing = 82f;
        float startX  = px + panelW * 0.5f - (hand.Count * spacing * 0.5f) + spacing * 0.5f;

        for (int i = 0; i < hand.Count; i++)
        {
            Card c = hand[i];
            Vector2 target = new Vector2(startX + i * spacing, rowY + 18f);

            if (!c.initialized)
            {
                c.pos         = new Vector2(target.x + (isDealer ? -400f : 400f), target.y - 250f);
                c.rotation    = Random.Range(-15f, 15f);
                c.initialized = true;
            }
            float lerpSpd = 14f - i * 1.5f;
            c.pos      = Vector2.Lerp(c.pos,      target,  Time.unscaledDeltaTime * lerpSpd);
            c.rotation = Mathf.Lerp(c.rotation,   0f,      Time.unscaledDeltaTime * 10f);

            DrawPremiumCard(c.pos.x, c.pos.y, cardW, cardH, c);
        }
    }

    // ── Score Badge ───────────────────────────────────────────────────────────
    void DrawScoreBadge(float x, float y, string score, Color col, bool danger)
    {
        float bw = 40f, bh = 22f;
        Color bg = danger ? new Color(0.5f, 0.05f, 0.05f, 0.85f) : new Color(col.r*0.15f, col.g*0.15f, col.b*0.15f, 0.85f);
        GUI.color = new Color(bg.r, bg.g, bg.b, bg.a * panelAlpha);
        GUI.DrawTexture(new Rect(x, y, bw, bh), _white);
        GUI.color = new Color(col.r, col.g, col.b, panelAlpha);
        GUI.DrawTexture(new Rect(x, y, bw, 1.5f), _white);
        GUI.DrawTexture(new Rect(x, y + bh - 1.5f, bw, 1.5f), _white);

        GUIStyle bs = NewStyle(13, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        GUI.color = new Color(1,1,1, panelAlpha);
        GUI.Label(new Rect(x, y, bw, bh), score, bs);
    }

    // ── Premium Card ──────────────────────────────────────────────────────────
    void DrawPremiumCard(float x, float y, float w, float h, Card c)
    {
        bool isRed = (c.suit == "♥" || c.suit == "♦");

        if (c.isHidden)
        {
            // Card shadow
            GUI.color = new Color(0, 0, 0, 0.4f * panelAlpha);
            GUI.DrawTexture(new Rect(x + 4, y + 5, w, h), _white);

            // Card back — deep purple with gold diamond pattern
            GUI.color = new Color(0.22f, 0.04f, 0.40f, panelAlpha);
            GUI.DrawTexture(new Rect(x, y, w, h), _white);

            // Inner border
            GUI.color = new Color(GOLD.r, GOLD.g, GOLD.b, 0.6f * panelAlpha);
            DrawBorderRect(x + 4, y + 4, w - 8, h - 8, 1.5f);

            // Center diamond
            GUIStyle ds = NewStyle(32, FontStyle.Bold, new Color(GOLD.r, GOLD.g, GOLD.b, 0.7f * panelAlpha), TextAnchor.MiddleCenter);
            GUI.color = new Color(1,1,1, panelAlpha);
            GUI.Label(new Rect(x, y, w, h), "◆", ds);

            // Shimmer lines
            GUI.color = new Color(1f, 0.9f, 0.5f, 0.08f * panelAlpha);
            for (int l = 0; l < 5; l++)
                GUI.DrawTexture(new Rect(x + 4, y + 4 + l * (h - 8) / 5f, w - 8, 1f), _white);
        }
        else
        {
            // Shadow
            GUI.color = new Color(0, 0, 0, 0.4f * panelAlpha);
            GUI.DrawTexture(new Rect(x + 4, y + 5, w, h), _white);

            // Card face
            GUI.color = new Color(IVORY.r, IVORY.g, IVORY.b, panelAlpha);
            GUI.DrawTexture(new Rect(x, y, w, h), _white);

            // Subtle warm gradient at top
            GUI.color = new Color(1f, 0.97f, 0.90f, 0.35f * panelAlpha);
            GUI.DrawTexture(new Rect(x, y, w, h * 0.4f), _white);

            // Border
            Color borderCol = isRed
                ? new Color(0.7f, 0.1f, 0.1f, 0.6f * panelAlpha)
                : new Color(0.1f, 0.1f, 0.2f, 0.5f * panelAlpha);
            GUI.color = borderCol;
            DrawBorderRect(x, y, w, h, 1.5f);

            // Suit color
            Color suitCol = isRed ? CRIMSON : new Color(0.08f, 0.08f, 0.12f);

            // Top-left rank + suit
            GUIStyle rankStyle = NewStyle(17, FontStyle.Bold, new Color(suitCol.r, suitCol.g, suitCol.b, panelAlpha), TextAnchor.UpperLeft);
            GUI.color = new Color(1,1,1, panelAlpha);
            GUI.Label(new Rect(x + 5f, y + 3f, 28f, 20f), c.rank, rankStyle);

            GUIStyle suitSmall = NewStyle(13, FontStyle.Normal, new Color(suitCol.r, suitCol.g, suitCol.b, panelAlpha), TextAnchor.UpperLeft);
            GUI.Label(new Rect(x + 6f, y + 20f, 20f, 18f), c.suit, suitSmall);

            // Center big suit
            GUIStyle bigSuit = NewStyle(38, FontStyle.Bold, new Color(suitCol.r, suitCol.g, suitCol.b, 0.88f * panelAlpha), TextAnchor.MiddleCenter);
            GUI.Label(new Rect(x, y, w, h), c.suit, bigSuit);

            // Bottom-right (upside-down mirrored — just repeat)
            GUIStyle rankBR = NewStyle(14, FontStyle.Bold, new Color(suitCol.r, suitCol.g, suitCol.b, panelAlpha), TextAnchor.LowerRight);
            GUI.Label(new Rect(x, y, w - 5f, h - 4f), c.rank, rankBR);
        }

        GUI.color = new Color(1,1,1, panelAlpha);
    }

    // ── Idle Decor ────────────────────────────────────────────────────────────
    void DrawIdleDecor(float px, float feltY, float panelW, float feltH)
    {
        float t = Time.unscaledTime;
        string[] bigSuits = { "♠", "♥", "♦", "♣" };
        float[] xs = { 0.12f, 0.35f, 0.65f, 0.88f };
        Color[] sc = { new Color(0.2f,0.8f,1f), CRIMSON, GOLD, EMERALD };

        for (int i = 0; i < 4; i++)
        {
            float pulse = (Mathf.Sin(t * 1.8f + i * 1.1f) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(0.07f, 0.18f, pulse) * panelAlpha;
            GUIStyle ds = NewStyle(72, FontStyle.Bold, new Color(sc[i].r, sc[i].g, sc[i].b, alpha), TextAnchor.MiddleCenter);
            GUI.color = new Color(1,1,1, panelAlpha);
            GUI.Label(new Rect(px + panelW * xs[i] - 50f, feltY + feltH * 0.3f, 100f, 100f), bigSuits[i], ds);
        }

        // Tagline
        GUIStyle tag = NewStyle(20, FontStyle.Bold, new Color(GOLD.r, GOLD.g, GOLD.b, 0.45f * panelAlpha), TextAnchor.MiddleCenter);
        GUI.color = new Color(1,1,1, panelAlpha);
        GUI.Label(new Rect(px, feltY + feltH * 0.5f, panelW, 32f), "—  Set your bet below and press DEAL  —", tag);
    }

    // ── Controls ──────────────────────────────────────────────────────────────
    void DrawControls(float px, float ctrlY, float panelW, float ctrlH)
    {
        float cy = ctrlY + ctrlH * 0.5f - 22f;   // vertically centered

        if (state == GameState.Idle || state == GameState.GameOver)
        {
            // BET label
            GUIStyle betLbl = NewStyle(13, FontStyle.Bold, new Color(GOLD.r, GOLD.g, GOLD.b, 0.7f * panelAlpha), TextAnchor.MiddleLeft);
            GUI.color = new Color(1,1,1, panelAlpha);
            GUI.Label(new Rect(px + 24f, cy - 2f, 60f, 18f), "BET", betLbl);

            // Bet amount display
            GUIStyle betAmt = NewStyle(26, FontStyle.Bold, new Color(GOLD.r, GOLD.g, GOLD.b, panelAlpha), TextAnchor.MiddleLeft);
            GUI.Label(new Rect(px + 24f, cy + 12f, 110f, 34f), $"{betAmount} EC", betAmt);

            // Chip bounce animation
            float bounce = Mathf.Abs(Mathf.Sin(chipBounce)) * 3f;

            // Bet buttons
            float btnX = px + 120f;
            if (DrawLuxButton(btnX,        cy + bounce, 50f, 40f, "MIN",    LOSE_COLOR,  false)) betAmount = 10;
            if (DrawLuxButton(btnX + 55f,  cy,          50f, 40f, "−10",    LOSE_COLOR,  false)) betAmount = Mathf.Max(10, betAmount - 10);
            if (DrawLuxButton(btnX + 110f, cy,          50f, 40f, "+10",    WIN_COLOR,   false)) betAmount += 10;
            if (DrawLuxButton(btnX + 165f, cy,          50f, 40f, "1/2",    GOLD,        false)) betAmount = Mathf.Max(10, betAmount / 2);
            if (DrawLuxButton(btnX + 220f, cy,          54f, 40f, "MAX",    GOLD,        false)) betAmount = Mathf.Max(10, _inventory.credits);

            bool canAfford = _inventory.credits >= betAmount && betAmount >= 10 && _inventory.credits >= 10;
            if (DrawLuxButton(px + panelW - 220f, cy - 4f, 190f, 48f, "✦  DEAL  ✦", canAfford ? EMERALD : new Color(0.4f,0.4f,0.4f), true))
                if (canAfford) StartGame();

            if (state == GameState.GameOver)
            {
                GUIStyle replay = NewStyle(12, FontStyle.Bold, new Color(0.6f, 0.6f, 0.6f, 0.6f * panelAlpha), TextAnchor.MiddleLeft);
                GUI.Label(new Rect(px + 24f, ctrlY + ctrlH - 22f, 250f, 18f), "Adjust bet and deal again", replay);
            }
        }
        else if (state == GameState.PlayerTurn)
        {
            float center = px + panelW * 0.5f;
            if (DrawLuxButton(center - 160f, cy - 2f, 140f, 46f, "HIT  ✦", EMERALD, true)) Hit();

            if (DrawLuxButton(center + 20f, cy - 2f, 140f, 46f, "STAND  ■", GOLD, true)) Stand();

            // Tip
            GUIStyle tip = NewStyle(12, FontStyle.Bold, new Color(0.6f, 0.7f, 0.8f, 0.55f * panelAlpha), TextAnchor.MiddleCenter);
            GUI.color = new Color(1,1,1, panelAlpha);
            GUI.Label(new Rect(px, ctrlY + ctrlH - 24f, panelW, 18f), "HIT to draw  ·  STAND to hold  ·  ESC to quit", tip);
        }
    }

    // ── Luxury Button ────────────────────────────────────────────────────────
    bool DrawLuxButton(float x, float y, float w, float h, string text, Color accent, bool large)
    {
        Rect r   = new Rect(x, y, w, h);
        Vector2 mp = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        bool hover = r.Contains(mp);
        bool gray  = accent.maxColorComponent < 0.25f;

        // Drop shadow
        GUI.color = new Color(0, 0, 0, 0.45f * panelAlpha);
        GUI.DrawTexture(new Rect(x + 3, y + 4, w, h), _white);

        // Accent border glow
        float glowAlpha = hover ? 0.9f : 0.4f;
        GUI.color = new Color(accent.r, accent.g, accent.b, glowAlpha * panelAlpha);
        GUI.DrawTexture(new Rect(x - 1.5f, y - 1.5f, w + 3, h + 3), _white);

        // Body
        Color body = hover
            ? new Color(accent.r * 0.28f, accent.g * 0.28f, accent.b * 0.28f, 0.95f * panelAlpha)
            : new Color(0.07f, 0.04f, 0.12f, 0.95f * panelAlpha);
        GUI.color = body;
        GUI.DrawTexture(r, _white);

        // Top highlight stripe
        if (!gray)
        {
            GUI.color = new Color(1f, 1f, 1f, (hover ? 0.15f : 0.06f) * panelAlpha);
            GUI.DrawTexture(new Rect(x + 1, y + 1, w - 2, h * 0.3f), _white);
        }

        // Text
        int fs = large ? 18 : 15;
        GUIStyle s = NewStyle(fs, FontStyle.Bold,
            hover ? Color.white : (gray ? new Color(0.4f,0.4f,0.4f) : new Color(accent.r, accent.g, accent.b)),
            TextAnchor.MiddleCenter);
        GUI.color = new Color(0,0,0, 0.5f * panelAlpha);
        GUI.Label(new Rect(x + 1, y + 2, w, h), text, s);
        GUI.color = new Color(1,1,1, panelAlpha);
        GUI.Label(r, text, s);

        if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
        { PlayClickSound(); Event.current.Use(); return true; }
        return false;
    }

    private static AudioClip s_clickSound;
    private void PlayClickSound()
    {
        if (s_clickSound == null) s_clickSound = Resources.Load<AudioClip>("SFX/UI/ui_wav/click_sound") ?? Resources.Load<AudioClip>("SFX/Buy_Coin");
        if (s_clickSound != null && Camera.main != null) AudioSource.PlayClipAtPoint(s_clickSound, Camera.main.transform.position, 0.5f);
    }

    // ── Gold Oval Ring (felt decor) ───────────────────────────────────────────
    void DrawGoldOvalRing(float cx, float cy, float ow, float oh)
    {
        int segs = 40;
        float thick = 2.5f;
        float a = panelAlpha * 0.25f;
        GUI.color = new Color(GOLD.r, GOLD.g, GOLD.b, a);
        for (int i = 0; i < segs; i++)
        {
            float ang0 = (float)i / segs * Mathf.PI * 2f;
            float ang1 = (float)(i+1) / segs * Mathf.PI * 2f;
            float x0 = cx + Mathf.Cos(ang0) * ow * 0.5f;
            float y0 = cy + Mathf.Sin(ang0) * oh * 0.5f;
            GUI.DrawTexture(new Rect(x0 - thick*0.5f, y0 - thick*0.5f, thick, thick), _white);
        }
    }

    // ── Gold Corner Ornaments ────────────────────────────────────────────────
    void DrawGoldCornerOrnaments(float px, float py, float pw, float ph)
    {
        float len   = 28f;
        float thick = 3.5f;
        float pa    = panelAlpha;
        Color gc    = new Color(GOLD.r, GOLD.g, GOLD.b, pa);

        GUI.color = gc;
        // TL
        GUI.DrawTexture(new Rect(px - thick, py - thick, len + thick, thick), _white);
        GUI.DrawTexture(new Rect(px - thick, py - thick, thick, len + thick), _white);
        // TR
        GUI.DrawTexture(new Rect(px + pw - len, py - thick, len + thick, thick), _white);
        GUI.DrawTexture(new Rect(px + pw, py - thick, thick, len + thick), _white);
        // BL
        GUI.DrawTexture(new Rect(px - thick, py + ph, len + thick, thick), _white);
        GUI.DrawTexture(new Rect(px - thick, py + ph - len, thick, len + thick), _white);
        // BR
        GUI.DrawTexture(new Rect(px + pw - len, py + ph, len + thick, thick), _white);
        GUI.DrawTexture(new Rect(px + pw, py + ph - len, thick, len + thick), _white);

        // Diamond jewels at corners
        string gem = "◆";
        GUIStyle gs = NewStyle(12, FontStyle.Bold, new Color(GOLD.r, GOLD.g, GOLD.b, pa), TextAnchor.MiddleCenter);
        GUI.color = new Color(1,1,1, pa);
        GUI.Label(new Rect(px - 10, py - 10, 20, 20), gem, gs);
        GUI.Label(new Rect(px + pw - 10, py - 10, 20, 20), gem, gs);
        GUI.Label(new Rect(px - 10, py + ph - 10, 20, 20), gem, gs);
        GUI.Label(new Rect(px + pw - 10, py + ph - 10, 20, 20), gem, gs);
    }

    // ── Border Rect ──────────────────────────────────────────────────────────
    void DrawBorderRect(float x, float y, float w, float h, float t)
    {
        GUI.DrawTexture(new Rect(x, y, w, t), _white);
        GUI.DrawTexture(new Rect(x, y + h - t, w, t), _white);
        GUI.DrawTexture(new Rect(x, y, t, h), _white);
        GUI.DrawTexture(new Rect(x + w - t, y, t, h), _white);
    }

    // ── Draw Circle (approximated with small rects) ───────────────────────────
    void DrawCircle(float cx, float cy, float r)
    {
        int segs = 18;
        for (int i = 0; i < segs; i++)
        {
            float a0 = (float)i / segs * Mathf.PI * 2f;
            float a1 = (float)(i+1) / segs * Mathf.PI * 2f;
            float x0 = cx + Mathf.Cos(a0) * r;
            float y0 = cy + Mathf.Sin(a0) * r;
            float x1 = cx + Mathf.Cos(a1) * r;
            float y1 = cy + Mathf.Sin(a1) * r;
            // Fill by drawing triangles as quads towards center
            GUI.DrawTexture(new Rect(Mathf.Min(x0, x1, cx) - 1, Mathf.Min(y0, y1, cy) - 1, r * 0.35f + 2, r * 0.35f + 2), _white);
        }
        // Solid center fill
        GUI.DrawTexture(new Rect(cx - r * 0.7f, cy - r * 0.7f, r * 1.4f, r * 1.4f), _white);
    }

    // ── GUIStyle factory ─────────────────────────────────────────────────────
    GUIStyle NewStyle(int size, FontStyle fontStyle, Color color, TextAnchor anchor)
    {
        var s = new GUIStyle();
        s.fontSize   = size;
        s.fontStyle  = fontStyle;
        s.normal.textColor = color;
        s.alignment  = anchor;
        s.richText   = true;
        return s;
    }
}
