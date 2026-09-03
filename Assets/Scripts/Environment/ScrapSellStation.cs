using UnityEngine;

/// <summary>
/// Sell Station – đặt trong WaitingRoom scene.
/// Khi tương tác, mở giao diện BÁN PHẾ LIỆU (OnGUI sci-fi).
/// Cho phép bán từng vật phẩm hoặc bán tất cả.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class ScrapSellStation : MonoBehaviour, IInteractable
{
    [Header("Interaction Settings")]
    public string interactHint = "Press [E] to Sell Scrap";
    public float hintRange = 4f;

    [Header("Audio (Optional)")]
    public AudioClip sellSound;
    public AudioClip errorSound;

    private void Start()
    {
        if (sellSound == null) sellSound = Resources.Load<AudioClip>("SFX/Sell_Cash_Sound") ?? Resources.Load<AudioClip>("SFX/Sell_Cash");
        if (errorSound == null) errorSound = Resources.Load<AudioClip>("SFX/UI/ui_wav/negative_sound");
    }

    [HideInInspector] public bool isOpen = false;
    private PlayerInventory _inventory;
    private AudioSource _audioSource;

    // Hint
    private Transform _localPlayer;
    private bool _playerNearby = false;
    private Camera _mainCam;

    // Status
    private string _statusMsg = "";
    private float _statusTimer = 0f;
    private bool _statusIsError = false;

    // Textures
    private Texture2D _bgTex;
    private Texture2D _panelTex;
    private Texture2D _btnTex;
    private Texture2D _btnHoverTex;
    private Texture2D _btnDisabledTex;
    private Texture2D _scanlineTex;
    private Texture2D _hintBgTex;
    private float _noiseOffset = 0f;

    // Colors
    private static readonly Color COL_BG       = new Color(0f, 0f, 0f, 0f);
    private static readonly Color COL_PANEL    = new Color(0.180f, 0.200f, 0.240f, 0.95f);
    private static readonly Color COL_CYAN     = new Color(0.000f, 0.949f, 1.000f);
    private static readonly Color COL_GREEN    = new Color(0.224f, 1.000f, 0.078f);
    private static readonly Color COL_AMBER    = new Color(1.000f, 0.702f, 0.000f);
    private static readonly Color COL_RED      = new Color(1.000f, 0.200f, 0.200f);
    private static readonly Color COL_DIM      = new Color(0.400f, 0.500f, 0.600f);
    private static readonly Color COL_EVENT    = new Color(1.000f, 0.400f, 0.800f);

    void Update()
    {
        if (_localPlayer == null || _inventory == null)
        {
            PlayerInventory[] inventories = FindObjectsByType<PlayerInventory>();
            foreach (var inv in inventories)
            {
                if (inv.IsOwner)
                {
                    _localPlayer = inv.transform;
                    _inventory = inv;
                    break;
                }
            }
            if (_localPlayer == null && inventories.Length > 0)
            {
                _localPlayer = inventories[0].transform;
                _inventory = inventories[0];
            }
        }

        if (_localPlayer != null)
        {
            float dist = Vector3.Distance(_localPlayer.position, transform.position);
            _playerNearby = dist <= hintRange;
        }
        else _playerNearby = false;

        if (_mainCam == null || !_mainCam.gameObject.activeInHierarchy) _mainCam = Camera.main;
        if (_audioSource == null) _audioSource = GetComponent<AudioSource>();

        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            Toggle(false);

        if (_statusTimer > 0f)
        {
            _statusTimer -= Time.unscaledDeltaTime;
            if (_statusTimer <= 0f) _statusMsg = "";
        }
    }

    public void Interact(GameObject interactor)
    {
        PlayerInventory inv = interactor.GetComponentInParent<PlayerInventory>();
        if (inv == null) inv = interactor.GetComponentInChildren<PlayerInventory>();

        // Fallback: Nếu không tìm thấy qua interactor, tìm PlayerInventory của local player
        if (inv == null)
        {
            PlayerInventory[] inventories = FindObjectsByType<PlayerInventory>();
            foreach (var i in inventories)
            {
                if (i.IsOwner)
                {
                    inv = i;
                    break;
                }
            }
            if (inv == null && inventories.Length > 0) inv = inventories[0];
        }

        if (inv != null)
        {
            _localPlayer = inv.transform;
            _inventory = inv;
            
            // Báo cho PlayerController biết đang ở chế độ Shop
            PlayerController pc = inv.GetComponent<PlayerController>();
            if (pc != null) pc.isShopMode = true;
        }
        Toggle(true);
    }

    public void Toggle(bool open)
    {
        isOpen = open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;

        if (_localPlayer != null)
        {
            PlayerController pc = _localPlayer.GetComponent<PlayerController>();
            if (pc != null) pc.isShopMode = open;
        }
    }

    void InitTextures()
    {
        if (_bgTex != null) return;
        _bgTex          = MakeTex(COL_BG);
        _panelTex       = MakeTex(COL_PANEL);
        _btnTex         = MakeTex(new Color(0.12f, 0.18f, 0.25f, 0.95f));
        _btnHoverTex    = MakeTex(new Color(0.00f, 0.40f, 0.50f, 0.95f));
        _btnDisabledTex = MakeTex(new Color(0.04f, 0.04f, 0.05f, 0.50f));

        _scanlineTex = new Texture2D(2, 4);
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 2; x++)
                _scanlineTex.SetPixel(x, y, y % 2 == 0 ? new Color(0,0,0,0) : new Color(0,0,0,0.4f));
        _scanlineTex.filterMode = FilterMode.Point;
        _scanlineTex.Apply();
    }

    Texture2D MakeTex(Color c)
    {
        Texture2D t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    void OnGUI()
    {
        if (!isOpen && _playerNearby && _mainCam != null) DrawFloatingHint();
        if (!isOpen || _inventory == null) return;

        InitTextures();
        _noiseOffset += Time.unscaledDeltaTime * 12f;

        GUI.depth = -10;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _bgTex);

        GUI.color = new Color(1f, 1f, 1f, 0.12f);
        GUI.DrawTextureWithTexCoords(
            new Rect(0, 0, Screen.width, Screen.height),
            _scanlineTex,
            new Rect(0, _noiseOffset * 0.05f, Screen.width, Screen.height / 4f));
        GUI.color = Color.white;

        float panelW = Mathf.Min(Screen.width * 0.52f, 620f);
        float panelH = Mathf.Min(Screen.height * 0.82f, 720f);
        float px = (Screen.width - panelW) * 0.5f;
        float py = (Screen.height - panelH) * 0.5f;

        GUI.DrawTexture(new Rect(px, py, panelW, panelH), _panelTex);
        DrawTechCorners(px, py, panelW, panelH, COL_GREEN);

        // Header
        float flicker = 0.85f + Mathf.PingPong(Time.unscaledTime * 3f, 0.15f);
        GUIStyle titleStyle = new GUIStyle();
        titleStyle.fontSize = Mathf.RoundToInt(Mathf.Min(panelW * 0.045f, 26f));
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = COL_GREEN;
        titleStyle.alignment = TextAnchor.MiddleCenter;

        GUI.color = new Color(1f, 1f, 1f, flicker);
        GUI.Label(new Rect(px, py + 10f, panelW, 35f), "---  SELL SCRAP  ---", titleStyle);
        GUI.color = Color.white;

        // Market Event Banner
        GUIStyle eventStyle = new GUIStyle();
        eventStyle.fontSize = Mathf.RoundToInt(Mathf.Min(panelW * 0.022f, 13f));
        eventStyle.fontStyle = FontStyle.Bold;
        eventStyle.normal.textColor = ShopData.CurrentEvent == MarketEvent.Normal ? COL_DIM : COL_EVENT;
        eventStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(px, py + 40f, panelW, 20f), ShopData.EventDescription, eventStyle);

        GUI.color = new Color(COL_GREEN.r, COL_GREEN.g, COL_GREEN.b, 0.4f);
        GUI.DrawTexture(new Rect(px + 20f, py + 62f, panelW - 40f, 1f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        
        // Energy Cells
        GUIStyle credStyle = new GUIStyle();
        credStyle.fontSize = Mathf.RoundToInt(Mathf.Min(panelW * 0.040f, 22f));
        credStyle.fontStyle = FontStyle.Bold;
        credStyle.normal.textColor = COL_AMBER;
        credStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(px, py + 70f, panelW, 30f), $"-^  EC: {_inventory.credits}  -^", credStyle);

        // Close btn
        GUIStyle closeStyle = new GUIStyle();
        closeStyle.fontSize = 20;
        closeStyle.fontStyle = FontStyle.Bold;
        closeStyle.normal.textColor = COL_RED;
        closeStyle.alignment = TextAnchor.MiddleCenter;
        Rect closeRect = new Rect(px + panelW - 35f, py + 5f, 30f, 30f);
        if (closeRect.Contains(Event.current.mousePosition))
            closeStyle.normal.textColor = Color.white;
        if (GUI.Button(closeRect, "✖", closeStyle))
            Toggle(false);

        // Column headers
        float rowY = py + 115f;
        GUIStyle hStyle = new GUIStyle();
        hStyle.fontSize = 11;
        hStyle.fontStyle = FontStyle.Bold;
        hStyle.normal.textColor = COL_DIM;

        GUI.Label(new Rect(px + 20f, rowY, 130f, 20f), "ITEM", hStyle);
        hStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(px + panelW - 290f, rowY, 50f, 20f), "QTY", hStyle);
        GUI.Label(new Rect(px + panelW - 230f, rowY, 60f, 20f), "UNIT EC", hStyle);
        GUI.Label(new Rect(px + panelW - 160f, rowY, 70f, 20f), "TOTAL", hStyle);

        GUI.color = new Color(COL_GREEN.r, COL_GREEN.g, COL_GREEN.b, 0.2f);
        GUI.DrawTexture(new Rect(px + 20f, rowY + 22f, panelW - 40f, 1f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        rowY += 32f;

        // Rows (using ref to allow modifying inventory directly)
        DrawScrapRow(px, ref rowY, panelW, "Circuit",        ref _inventory.circuits,       ShopData.CircuitSellPrice);
        DrawScrapRow(px, ref rowY, panelW, "Metal Pipe",     ref _inventory.metalPipes,     ShopData.MetalPipeSellPrice);
        DrawScrapRow(px, ref rowY, panelW, "Iron Plate",     ref _inventory.ironPlates,     ShopData.IronPlateSellPrice);
        DrawScrapRow(px, ref rowY, panelW, "Chemical",       ref _inventory.chemicals,      ShopData.ChemicalSellPrice);
        DrawScrapRow(px, ref rowY, panelW, "Plastic Pipe",   ref _inventory.plasticPipes,   ShopData.PlasticPipeSellPrice);
        DrawScrapRow(px, ref rowY, panelW, "Battery",        ref _inventory.scrapBatteries, ShopData.BatterySellPrice);

        // Total
        int totalVal = _inventory.circuits * ShopData.CircuitSellPrice
                     + _inventory.metalPipes * ShopData.MetalPipeSellPrice
                     + _inventory.ironPlates * ShopData.IronPlateSellPrice
                     + _inventory.chemicals * ShopData.ChemicalSellPrice
                     + _inventory.plasticPipes * ShopData.PlasticPipeSellPrice
                     + _inventory.scrapBatteries * ShopData.BatterySellPrice;

        rowY += 15f;
        GUI.color = new Color(COL_GREEN.r, COL_GREEN.g, COL_GREEN.b, 0.3f);
        GUI.DrawTexture(new Rect(px + 20f, rowY, panelW - 40f, 1f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        rowY += 15f;

        GUIStyle totalStyle = new GUIStyle();
        totalStyle.fontSize = Mathf.RoundToInt(Mathf.Min(panelW * 0.040f, 22f));
        totalStyle.fontStyle = FontStyle.Bold;
        totalStyle.normal.textColor = COL_AMBER;
        totalStyle.alignment = TextAnchor.MiddleRight;
        GUI.Label(new Rect(px + 20f, rowY, panelW - 45f, 25f), $"TOTAL VALUE: {totalVal} EC", totalStyle);
        
        rowY += 40f;

        // Sell button
        if (totalVal > 0)
        {
            if (DrawButton(px + (panelW - 220f) * 0.5f, rowY, 220f, 40f, $"◆  SELL ALL  ({totalVal} EC)", COL_GREEN, true))
            {
                _inventory.SellAllScrap();
                ShowStatus($"Sold all scrap, received {totalVal} EC!", false);
                PlaySound(sellSound);
            }
        }
        else
        {
            DrawButton(px + (panelW - 220f) * 0.5f, rowY, 220f, 40f, "NO SCRAP TO SELL", COL_DIM, false);
        }

        // Status
        if (!string.IsNullOrEmpty(_statusMsg))
        {
            Color sc = _statusIsError ? COL_RED : COL_GREEN;
            float alpha = Mathf.Clamp01(_statusTimer);

            GUI.color = new Color(sc.r, sc.g, sc.b, 0.15f * alpha);
            GUI.DrawTexture(new Rect(px + 20f, py + panelH - 45f, panelW - 40f, 35f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle sStyle = new GUIStyle();
            sStyle.fontSize = 13;
            sStyle.fontStyle = FontStyle.Bold;
            sStyle.normal.textColor = new Color(sc.r, sc.g, sc.b, alpha);
            sStyle.alignment = TextAnchor.MiddleCenter;

            string prefix = _statusIsError ? "✖  " : "✔  ";
            GUI.Label(new Rect(px, py + panelH - 45f, panelW, 35f), prefix + _statusMsg, sStyle);
        }
    }

    void DrawScrapRow(float px, ref float rowY, float panelW, string name, ref int qty, int price)
    {
        float rowH = 30f;
        Color tCol = qty > 0 ? Color.white : COL_DIM;

        GUIStyle ns = new GUIStyle();
        ns.fontSize = 15;
        ns.normal.textColor = tCol;
        
        GUIStyle vs = new GUIStyle(ns);
        vs.alignment = TextAnchor.MiddleCenter;

        GUIStyle ts = new GUIStyle(ns);
        ts.alignment = TextAnchor.MiddleCenter;
        ts.normal.textColor = qty > 0 ? COL_AMBER : COL_DIM;

        GUI.Label(new Rect(px + 20f, rowY + 5f, 130f, 20f), name, ns);
        GUI.Label(new Rect(px + panelW - 290f, rowY + 5f, 50f, 20f), qty.ToString(), vs);
        GUI.Label(new Rect(px + panelW - 230f, rowY + 5f, 60f, 20f), $"{price} EC", vs);
        GUI.Label(new Rect(px + panelW - 160f, rowY + 5f, 70f, 20f), $"{qty * price} EC", ts);

        // Nút SELL 1
        float btnW = 60f;
        float btnH = 24f;
        float btnX = px + panelW - 80f;
        float btnY = rowY + 3f;

        if (qty > 0)
        {
            if (DrawButton(btnX, btnY, btnW, btnH, "SELL 1", COL_GREEN, true))
            {
                int c = name == "Circuit" ? 1 : 0;
                int mp = name == "Metal Pipe" ? 1 : 0;
                int ip = name == "Iron Plate" ? 1 : 0;
                int ch = name == "Chemical" ? 1 : 0;
                int pl = name == "Plastic Pipe" ? 1 : 0;
                int bat = name == "Battery" ? 1 : 0;

                qty--; // Giảm số lượng cục bộ như pattern của SellAll
                if (_inventory.IsSpawned)
                {
                    _inventory.RequestSellScrapServerRpc(c, mp, ip, ch, pl, bat);
                }
                else
                {
                    _inventory.AddCredits(price);
                }

                ShowStatus($"Sold 1 {name}, received {price} EC!", false);
                PlaySound(sellSound);
            }
        }
        else
        {
            DrawButton(btnX, btnY, btnW, btnH, "---", COL_DIM, false);
        }

        rowY += rowH;
    }

    void DrawFloatingHint()
    {
        // Hiển thị ở chính giữa màn hình (dưới hồng tâm một chút) thay vì trôi nổi trên đầu NPC
        if (_hintBgTex == null)
        {
            _hintBgTex = new Texture2D(1, 1);
            _hintBgTex.SetPixel(0, 0, new Color(0.02f, 0.02f, 0.03f, 0.85f));
            _hintBgTex.Apply();
        }

        float hintW = 260f;
        float hintH = 32f;
        float hintX = (Screen.width - hintW) * 0.5f;
        float hintY = (Screen.height * 0.5f) + 40f; // Dưới tâm màn hình 40px

        GUI.DrawTexture(new Rect(hintX, hintY, hintW, hintH), _hintBgTex);
        DrawTechCorners(hintX, hintY, hintW, hintH, new Color(COL_GREEN.r, COL_GREEN.g, COL_GREEN.b, 0.8f), 8f, 2f);

        GUIStyle hintStyle = new GUIStyle();
        hintStyle.fontSize = 15;
        hintStyle.fontStyle = FontStyle.Bold;
        hintStyle.normal.textColor = COL_GREEN;
        hintStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(hintX, hintY, hintW, hintH), interactHint, hintStyle);
    }

    bool DrawButton(float x, float y, float w, float h, string label, Color labelColor, bool enabled)
    {
        Rect btnRect = new Rect(x, y, w, h);
        bool isHover = enabled && btnRect.Contains(Event.current.mousePosition);

        GUI.DrawTexture(btnRect, enabled ? (isHover ? _btnHoverTex : _btnTex) : _btnDisabledTex);

        if (enabled)
            DrawTechCorners(x, y, w, h, new Color(labelColor.r, labelColor.g, labelColor.b, isHover ? 0.9f : 0.4f), 6f, 1f);

        GUIStyle btnStyle = new GUIStyle();
        btnStyle.fontSize = 13;
        btnStyle.fontStyle = FontStyle.Bold;
        btnStyle.normal.textColor = enabled ? (isHover ? Color.white : labelColor) : COL_DIM;
        btnStyle.alignment = TextAnchor.MiddleCenter;

        GUI.color = new Color(0, 0, 0, 0);
        bool clicked = GUI.Button(btnRect, "", GUIStyle.none);
        GUI.color = Color.white;

        GUI.Label(btnRect, label, btnStyle);
        return clicked && enabled;
    }

    void DrawTechCorners(float x, float y, float w, float h, Color color, float len = 12f, float thick = 2f)
    {
        GUI.color = color;
        Texture2D tex = Texture2D.whiteTexture;
        GUI.DrawTexture(new Rect(x, y, len, thick), tex);
        GUI.DrawTexture(new Rect(x, y, thick, len), tex);
        GUI.DrawTexture(new Rect(x + w - len, y, len, thick), tex);
        GUI.DrawTexture(new Rect(x + w - thick, y, thick, len), tex);
        GUI.DrawTexture(new Rect(x, y + h - thick, len, thick), tex);
        GUI.DrawTexture(new Rect(x, y + h - len, thick, len), tex);
        GUI.DrawTexture(new Rect(x + w - len, y + h - thick, len, thick), tex);
        GUI.DrawTexture(new Rect(x + w - thick, y + h - len, thick, len), tex);
        GUI.color = Color.white;
    }

    void ShowStatus(string msg, bool isError)
    {
        _statusMsg = msg;
        _statusIsError = isError;
        _statusTimer = 3f;
    }

    void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
            _audioSource.PlayOneShot(clip);
    }
}
