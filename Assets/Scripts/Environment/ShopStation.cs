using UnityEngine;

/// <summary>
/// Buy Station – đặt trong WaitingRoom scene.
/// Khi người chơi tương tác (E), mở giao diện MUA VẬT PHẨM (OnGUI sci-fi).
/// Tự khắc có UI riêng, không cần tạo thêm GameObject nào.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class ShopStation : MonoBehaviour, IInteractable
{
    [Header("Interaction Settings")]
    public string interactHint = "Press [E] to Buy Items";
    public float hintRange = 4f;

    [Header("Audio (Optional)")]
    public AudioClip buySound;
    public AudioClip sellSound;
    public AudioClip errorSound;
    private void Start()
    {
        if (buySound == null) buySound = Resources.Load<AudioClip>("SFX/Buy_Coin_Sound") ?? Resources.Load<AudioClip>("SFX/Buy_Coin");
        if (sellSound == null) sellSound = Resources.Load<AudioClip>("SFX/Sell_Cash_Sound") ?? Resources.Load<AudioClip>("SFX/Sell_Cash");
        if (errorSound == null) errorSound = Resources.Load<AudioClip>("SFX/UI/ui_wav/negative_sound");
    }

    // ── State ────────────────────────────────────────────────────────────────
    [HideInInspector] public bool isOpen = false;
    private PlayerInventory _inventory;
    private PlayerSurvival _survival;
    private AudioSource _audioSource;

    // Hint
    private Transform _localPlayer;
    private bool _playerNearby = false;
    private Camera _mainCam;

    // Status message
    private string _statusMsg = "";
    private float _statusTimer = 0f;
    private bool _statusIsError = false;

    // Scroll
    private Vector2 _scrollPos = Vector2.zero;

    // Textures (lazy init)
    private Texture2D _bgTex;
    private Texture2D _panelTex;
    private Texture2D _btnTex;
    private Texture2D _btnHoverTex;
    private Texture2D _btnDisabledTex;
    private Texture2D _scanlineTex;
    private Texture2D _hintBgTex;
    private float _noiseOffset = 0f;

    // Colors
    private static readonly Color COL_BG       = new Color(0.020f, 0.020f, 0.027f, 0.95f);
    private static readonly Color COL_PANEL    = new Color(0.040f, 0.050f, 0.070f, 0.90f);
    private static readonly Color COL_CYAN     = new Color(0.000f, 0.949f, 1.000f);
    private static readonly Color COL_GREEN    = new Color(0.224f, 1.000f, 0.078f);
    private static readonly Color COL_AMBER    = new Color(1.000f, 0.702f, 0.000f);
    private static readonly Color COL_RED      = new Color(1.000f, 0.200f, 0.200f);
    private static readonly Color COL_DIM      = new Color(0.400f, 0.500f, 0.600f);
    private static readonly Color COL_EVENT    = new Color(1.000f, 0.400f, 0.800f);

    // ── Unity Callbacks ──────────────────────────────────────────────────────

    void Awake()
    {
        ShopData.RollMarketEvent();
    }

    void Update()
    {
        // Tìm local player
        if (_localPlayer == null || _inventory == null)
        {
            PlayerInventory[] inventories = FindObjectsByType<PlayerInventory>();
            foreach (var inv in inventories)
            {
                if (inv.IsOwner)
                {
                    _localPlayer = inv.transform;
                    _inventory = inv;
                    _survival = inv.GetComponent<PlayerSurvival>();
                    break;
                }
            }
            // Offline mode fallback
            if (_localPlayer == null && inventories.Length > 0)
            {
                _localPlayer = inventories[0].transform;
                _inventory = inventories[0];
                _survival = inventories[0].GetComponent<PlayerSurvival>();
            }
        }

        // Hint range
        if (_localPlayer != null)
        {
            float dist = Vector3.Distance(_localPlayer.position, transform.position);
            _playerNearby = dist <= hintRange;
        }
        else _playerNearby = false;

        if (_mainCam == null || !_mainCam.gameObject.activeInHierarchy) _mainCam = Camera.main;
        if (_audioSource == null) _audioSource = GetComponent<AudioSource>();

        // ESC để đóng
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            Toggle(false);

        // Status timer
        if (_statusTimer > 0f)
        {
            _statusTimer -= Time.unscaledDeltaTime;
            if (_statusTimer <= 0f) _statusMsg = "";
        }
    }

    // ── IInteractable ────────────────────────────────────────────────────────

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
            _survival = inv.GetComponent<PlayerSurvival>();
            
            // Báo cho PlayerController biết đang ở chế độ Shop
            PlayerController pc = inv.GetComponent<PlayerController>();
            if (pc != null) pc.isShopMode = true;
        }

        Toggle(true);
    }

    public void Toggle(bool open)
    {
        isOpen = open;
        _scrollPos = Vector2.zero;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;

        if (_localPlayer != null)
        {
            PlayerController pc = _localPlayer.GetComponent<PlayerController>();
            if (pc != null) pc.isShopMode = open;
        }
    }

    // ── TEXTURE INIT ─────────────────────────────────────────────────────────

    void InitTextures()
    {
        if (_bgTex != null) return;
        _bgTex          = MakeTex(COL_BG);
        _panelTex       = MakeTex(COL_PANEL);
        _btnTex         = MakeTex(new Color(0.06f, 0.12f, 0.18f, 0.95f));
        _btnHoverTex    = MakeTex(new Color(0.00f, 0.30f, 0.40f, 0.95f));
        _btnDisabledTex = MakeTex(new Color(0.04f, 0.04f, 0.06f, 0.80f));

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

    // ── ON GUI ────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        // ── Floating hint khi đứng gần ──
        if (!isOpen && _playerNearby && _mainCam != null)
        {
            DrawFloatingHint();
        }

        // ── Shop UI ──
        if (!isOpen || _inventory == null) return;

        InitTextures();
        _noiseOffset += Time.unscaledDeltaTime * 12f;

        // Full-screen dim overlay
        GUI.depth = -10;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _bgTex);

        // Scanlines
        GUI.color = new Color(1f, 1f, 1f, 0.12f);
        GUI.DrawTextureWithTexCoords(
            new Rect(0, 0, Screen.width, Screen.height),
            _scanlineTex,
            new Rect(0, _noiseOffset * 0.05f, Screen.width, Screen.height / 4f));
        GUI.color = Color.white;

        // ── Main panel ──
        float panelW = Mathf.Min(Screen.width * 0.55f, 550f);
        float panelH = Mathf.Min(Screen.height * 0.80f, 650f);
        float px = (Screen.width - panelW) * 0.5f;
        float py = (Screen.height - panelH) * 0.5f;

        GUI.DrawTexture(new Rect(px, py, panelW, panelH), _panelTex);
        DrawTechCorners(px, py, panelW, panelH, COL_CYAN);

        // ── Header ──
        float flicker = 0.85f + Mathf.PingPong(Time.unscaledTime * 3f, 0.15f);
        GUIStyle titleStyle = new GUIStyle();
        titleStyle.fontSize = 22;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = COL_CYAN;
        titleStyle.alignment = TextAnchor.MiddleCenter;

        GUI.color = new Color(1f, 1f, 1f, flicker);
        GUI.Label(new Rect(px, py + 10f, panelW, 35f), "▼  BUY SUPPLIES  ▼", titleStyle);
        GUI.color = Color.white;

        // Market Event Banner
        GUIStyle eventStyle = new GUIStyle();
        eventStyle.fontSize = 12;
        eventStyle.fontStyle = FontStyle.Bold;
        eventStyle.normal.textColor = ShopData.CurrentEvent == MarketEvent.Normal ? COL_DIM : COL_EVENT;
        eventStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(px, py + 40f, panelW, 20f), ShopData.EventDescription, eventStyle);

        // Decorative line
        GUI.color = new Color(COL_CYAN.r, COL_CYAN.g, COL_CYAN.b, 0.4f);
        GUI.DrawTexture(new Rect(px + 20f, py + 62f, panelW - 40f, 1f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // ── Energy Cells display ──
        GUIStyle credStyle = new GUIStyle();
        credStyle.fontSize = 18;
        credStyle.fontStyle = FontStyle.Bold;
        credStyle.normal.textColor = COL_AMBER;
        credStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(px, py + 65f, panelW, 30f), $"◈  EC: {_inventory.credits}  ◈", credStyle);

        // ── Close button ──
        GUIStyle closeStyle = new GUIStyle();
        closeStyle.fontSize = 18;
        closeStyle.fontStyle = FontStyle.Bold;
        closeStyle.normal.textColor = COL_RED;
        closeStyle.alignment = TextAnchor.MiddleCenter;
        Rect closeRect = new Rect(px + panelW - 35f, py + 5f, 30f, 30f);
        if (closeRect.Contains(Event.current.mousePosition))
            closeStyle.normal.textColor = Color.white;
        if (GUI.Button(closeRect, "✖", closeStyle))
            Toggle(false);

        // ── Scroll area for items ──
        float scrollY = py + 105f;
        float scrollH = panelH - 160f;
        float itemH = 70f;
        float totalContentH = ShopData.BuyableItems.Count * (itemH + 5f) + 10f;

        Rect scrollViewRect = new Rect(px + 10f, scrollY, panelW - 20f, scrollH);
        Rect scrollContentRect = new Rect(0, 0, scrollViewRect.width - 20f, totalContentH);

        _scrollPos = GUI.BeginScrollView(scrollViewRect, _scrollPos, scrollContentRect);

        float itemY = 5f;
        foreach (ShopItemData item in ShopData.BuyableItems)
        {
            DrawBuyItem(5f, itemY, scrollContentRect.width - 10f, itemH, item);
            itemY += itemH + 5f;
        }

        GUI.EndScrollView();

        // ── Status bar ──
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

    // ── Draw Buy Item Row ────────────────────────────────────────────────────

    void DrawBuyItem(float x, float y, float w, float h, ShopItemData item)
    {
        bool canAfford = _inventory.credits >= item.currentPrice;

        // Background
        GUI.DrawTexture(new Rect(x, y, w, h), canAfford ? _btnTex : _btnDisabledTex);

        // Category tag
        GUIStyle catStyle = new GUIStyle();
        catStyle.fontSize = 9;
        catStyle.fontStyle = FontStyle.Bold;
        catStyle.normal.textColor = GetCategoryColor(item.category);
        GUI.Label(new Rect(x + 12f, y + 5f, 100f, 14f), $"[{item.category.ToString().ToUpper()}]", catStyle);

        // Item name
        GUIStyle nameStyle = new GUIStyle();
        nameStyle.fontSize = 14;
        nameStyle.fontStyle = FontStyle.Bold;
        nameStyle.normal.textColor = canAfford ? Color.white : COL_DIM;
        GUI.Label(new Rect(x + 12f, y + 20f, w - 130f, 22f), item.displayName, nameStyle);

        // Description
        GUIStyle descStyle = new GUIStyle();
        descStyle.fontSize = 10;
        descStyle.normal.textColor = canAfford ? new Color(0.6f, 0.7f, 0.8f) : COL_DIM;
        GUI.Label(new Rect(x + 12f, y + 42f, w - 130f, 18f), item.description, descStyle);

        // Price
        GUIStyle priceStyle = new GUIStyle();
        priceStyle.fontSize = 14;
        priceStyle.fontStyle = FontStyle.Bold;
        priceStyle.normal.textColor = canAfford ? COL_AMBER : COL_RED;
        priceStyle.alignment = TextAnchor.MiddleRight;
        string priceText = item.basePrice != item.currentPrice 
                            ? $"({item.basePrice}) {item.currentPrice} EC" 
                            : $"{item.currentPrice} EC";
        GUI.Label(new Rect(x + w - 195f, y + 12f, 185f, 22f), priceText, priceStyle);

        // Buy button
        float btnW = 95f;
        float btnH = 32f;
        float btnX = x + w - btnW - 10f;
        float btnY = y + (h - btnH) * 0.5f;

        if (DrawButton(btnX, btnY, btnW, btnH, canAfford ? "BUY" : "NO ENERGY", canAfford ? COL_CYAN : COL_DIM, canAfford))
        {
            TryBuyItem(item);
        }
    }

    Color GetCategoryColor(ShopItemCategory cat)
    {
        switch (cat)
        {
            case ShopItemCategory.Consumable: return COL_GREEN;
            case ShopItemCategory.Equipment:  return COL_CYAN;
            case ShopItemCategory.Utility:    return COL_AMBER;
            default: return Color.white;
        }
    }

    // ── Purchase Logic ────────────────────────────────────────────────────────

    void TryBuyItem(ShopItemData item)
    {
        if (!_inventory.SpendCredits(item.currentPrice))
        {
            ShowStatus("Không đủ Energy Cells!", true);
            PlaySound(errorSound);
            return;
        }

        switch (item.id)
        {
            case "health_pack":
                if (_survival != null)
                    _survival.Heal(50f);
                else
                    _inventory.healthPacks++;
                ShowStatus("Health Pack đã dùng! (+50 HP)", false);
                break;

            case "full_health_kit":
                if (_survival != null)
                    _survival.Heal(_survival.maxHealth);
                else
                    _inventory.healthPacks++;
                ShowStatus("Full Health Kit đã dùng! (MAX HP)", false);
                break;

            case "basic_gas_mask":
                _inventory.basicGasMasks++;
                ShowStatus("Basic Gas Mask đã thêm vào túi đồ!", false);
                break;

            case "advanced_gas_mask":
                _inventory.advancedGasMasks++;
                ShowStatus("Advanced Gas Mask đã thêm vào túi đồ!", false);
                break;

            case "battery_pack":
                _inventory.scrapBatteries += 3;
                ShowStatus("Battery Pack đã thêm! (+3 Batteries)", false);
                break;

            case "chemical_canister":
                _inventory.chemicals += 2;
                ShowStatus("Chemical Canister đã thêm! (+2 Chemicals)", false);
                break;

            case "circuit_board":
                _inventory.circuits += 2;
                ShowStatus("Circuit Board đã thêm! (+2 Circuits)", false);
                break;

            case "oxygen_tank":
                if (_survival != null)
                    _survival.currentOxygen = _survival.maxOxygen;
                else
                {
                    _inventory.oxygenTanks++;
                    ShowStatus("Inventory is full!", true);
                    PlaySound(errorSound);
                    _inventory.AddCredits(item.currentPrice); 
                    return;
                }
                ShowStatus("Oxygen Tank đã dùng! (MAX O₂)", false);
                break;

            default:
                _inventory.AddCredits(item.currentPrice);
                ShowStatus($"Item không hợp lệ: {item.id}", true);
                return;
        }

        PlaySound(buySound);
    }

    // ── Floating Hint ────────────────────────────────────────────────────────

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
        DrawTechCorners(hintX, hintY, hintW, hintH, new Color(COL_CYAN.r, COL_CYAN.g, COL_CYAN.b, 0.8f), 8f, 2f);

        GUIStyle hintStyle = new GUIStyle();
        hintStyle.fontSize = 14;
        hintStyle.fontStyle = FontStyle.Bold;
        hintStyle.normal.textColor = COL_CYAN;
        hintStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(hintX, hintY, hintW, hintH), interactHint, hintStyle);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void ShowStatus(string msg, bool isError)
    {
        _statusMsg = msg;
        _statusIsError = isError;
        _statusTimer = 3f;
    }

    bool DrawButton(float x, float y, float w, float h, string label, Color labelColor, bool enabled)
    {
        Rect btnRect = new Rect(x, y, w, h);
        bool isHover = enabled && btnRect.Contains(Event.current.mousePosition);

        GUI.DrawTexture(btnRect, enabled ? (isHover ? _btnHoverTex : _btnTex) : _btnDisabledTex);

        if (enabled)
            DrawTechCorners(x, y, w, h, new Color(labelColor.r, labelColor.g, labelColor.b, isHover ? 0.9f : 0.4f), 6f, 1f);

        GUIStyle btnStyle = new GUIStyle();
        btnStyle.fontSize = 12;
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

    void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
            _audioSource.PlayOneShot(clip);
    }
}
