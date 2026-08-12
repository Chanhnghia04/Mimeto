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
    private Texture2D _headerTex;
    private Texture2D _btnTex;
    private Texture2D _btnHoverTex;
    private Texture2D _btnDisabledTex;
    private Texture2D _scanlineTex;
    private Texture2D _hintBgTex;
    private Texture2D _itemBgTex;
    private Texture2D _itemBgAltTex;
    private Texture2D _priceBgTex;
    private Texture2D _creditsBgTex;
    private Texture2D _separatorTex;
    private float _noiseOffset = 0f;

    // Colors
    private static readonly Color COL_BG       = new Color(0.010f, 0.012f, 0.020f, 0.96f);
    private static readonly Color COL_PANEL    = new Color(0.025f, 0.035f, 0.055f, 0.96f);
    private static readonly Color COL_HEADER   = new Color(0.015f, 0.060f, 0.090f, 0.98f);
    private static readonly Color COL_CYAN     = new Color(0.000f, 0.878f, 1.000f);
    private static readonly Color COL_GREEN    = new Color(0.180f, 1.000f, 0.180f);
    private static readonly Color COL_AMBER    = new Color(1.000f, 0.780f, 0.100f);
    private static readonly Color COL_GOLD     = new Color(1.000f, 0.843f, 0.000f);
    private static readonly Color COL_RED      = new Color(1.000f, 0.250f, 0.250f);
    private static readonly Color COL_DIM      = new Color(0.350f, 0.420f, 0.500f);
    private static readonly Color COL_EVENT    = new Color(1.000f, 0.350f, 0.750f);
    private static readonly Color COL_WHITE_DIM = new Color(0.700f, 0.780f, 0.850f);

    // Icon mapping
    private static readonly string[] ITEM_ICONS = {
        "♥", "♥♥", "☣", "⛑", "⛑⛑", "⚡", "⚗", "⊞", "◎"
    };

    // ── Unity Callbacks ──────────────────────────────────────────────────────

    void Awake()
    {
        ShopData.RollMarketEvent();
    }

    void OnEnable() { PlayerInventory.OnShopBuyResult += HandleShopBuyResult; }
    void OnDisable() { PlayerInventory.OnShopBuyResult -= HandleShopBuyResult; }

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
        _headerTex      = MakeTex(COL_HEADER);
        _btnTex         = MakeTex(new Color(0.00f, 0.55f, 0.75f, 0.85f));
        _btnHoverTex    = MakeTex(new Color(0.00f, 0.75f, 1.00f, 0.95f));
        _btnDisabledTex = MakeTex(new Color(0.08f, 0.08f, 0.10f, 0.70f));
        _itemBgTex      = MakeTex(new Color(0.03f, 0.05f, 0.08f, 0.85f));
        _itemBgAltTex   = MakeTex(new Color(0.04f, 0.06f, 0.10f, 0.85f));
        _priceBgTex     = MakeTex(new Color(0.10f, 0.07f, 0.00f, 0.60f));
        _creditsBgTex   = MakeTex(new Color(0.08f, 0.06f, 0.00f, 0.80f));
        _separatorTex   = MakeTex(new Color(COL_CYAN.r, COL_CYAN.g, COL_CYAN.b, 0.20f));

        _scanlineTex = new Texture2D(2, 4);
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 2; x++)
                _scanlineTex.SetPixel(x, y, y % 2 == 0 ? new Color(0,0,0,0) : new Color(0,0,0,0.25f));
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
        _noiseOffset += Time.unscaledDeltaTime * 8f;

        // Full-screen dim overlay
        GUI.depth = -10;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _bgTex);

        // Scanlines (subtler)
        GUI.color = new Color(1f, 1f, 1f, 0.06f);
        GUI.DrawTextureWithTexCoords(
            new Rect(0, 0, Screen.width, Screen.height),
            _scanlineTex,
            new Rect(0, _noiseOffset * 0.03f, Screen.width, Screen.height / 4f));
        GUI.color = Color.white;

        // ── Main panel ──
        float panelW = Mathf.Min(Screen.width * 0.52f, 620f);
        float panelH = Mathf.Min(Screen.height * 0.82f, 720f);
        float px = (Screen.width - panelW) * 0.5f;
        float py = (Screen.height - panelH) * 0.5f;

        // Panel shadow
        GUI.color = new Color(0, 0, 0, 0.4f);
        GUI.DrawTexture(new Rect(px + 4f, py + 4f, panelW, panelH), _panelTex);
        GUI.color = Color.white;

        // Panel body
        GUI.DrawTexture(new Rect(px, py, panelW, panelH), _panelTex);

        // Outer glow border
        DrawGlowBorder(px, py, panelW, panelH, COL_CYAN, 2f);

        // ── HEADER SECTION ──
        float headerH = 110f;
        GUI.DrawTexture(new Rect(px, py, panelW, headerH), _headerTex);

        // Header bottom line
        GUI.color = new Color(COL_CYAN.r, COL_CYAN.g, COL_CYAN.b, 0.5f);
        GUI.DrawTexture(new Rect(px, py + headerH, panelW, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Title
        float flicker = 0.88f + Mathf.PingPong(Time.unscaledTime * 2.5f, 0.12f);
        GUIStyle titleStyle = new GUIStyle();
        titleStyle.fontSize = Mathf.RoundToInt(Mathf.Min(panelW * 0.045f, 26f));
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = COL_CYAN;
        titleStyle.alignment = TextAnchor.MiddleCenter;

        GUI.color = new Color(1f, 1f, 1f, flicker);
        GUI.Label(new Rect(px, py + 8f, panelW, 36f), "━━  SUPPLY STATION  ━━", titleStyle);
        GUI.color = Color.white;

        // Market Event Banner
        GUIStyle eventStyle = new GUIStyle();
        eventStyle.fontSize = Mathf.RoundToInt(Mathf.Min(panelW * 0.022f, 13f));
        eventStyle.fontStyle = FontStyle.Bold;
        eventStyle.alignment = TextAnchor.MiddleCenter;

        if (ShopData.CurrentEvent != MarketEvent.Normal)
        {
            // Pulsing event text
            float pulse = 0.7f + Mathf.PingPong(Time.unscaledTime * 2f, 0.3f);
            eventStyle.normal.textColor = new Color(COL_EVENT.r, COL_EVENT.g, COL_EVENT.b, pulse);
            GUI.Label(new Rect(px, py + 42f, panelW, 20f), "★ " + ShopData.EventDescription + " ★", eventStyle);
        }
        else
        {
            eventStyle.normal.textColor = COL_DIM;
            GUI.Label(new Rect(px, py + 42f, panelW, 20f), ShopData.EventDescription, eventStyle);
        }

        // ── CREDITS DISPLAY (big, unmissable) ──
        float credBoxW = panelW * 0.55f;
        float credBoxH = 38f;
        float credBoxX = px + (panelW - credBoxW) * 0.5f;
        float credBoxY = py + 64f;

        GUI.DrawTexture(new Rect(credBoxX, credBoxY, credBoxW, credBoxH), _creditsBgTex);
        DrawGlowBorder(credBoxX, credBoxY, credBoxW, credBoxH, new Color(COL_GOLD.r, COL_GOLD.g, COL_GOLD.b, 0.6f), 1f);

        // Credit icon + amount
        GUIStyle credLabelStyle = new GUIStyle();
        credLabelStyle.fontSize = 12;
        credLabelStyle.normal.textColor = COL_WHITE_DIM;
        credLabelStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(credBoxX, credBoxY, credBoxW, 14f), "YOUR BALANCE", credLabelStyle);

        GUIStyle credAmountStyle = new GUIStyle();
        credAmountStyle.fontSize = Mathf.RoundToInt(Mathf.Min(panelW * 0.040f, 22f));
        credAmountStyle.fontStyle = FontStyle.Bold;
        credAmountStyle.normal.textColor = COL_GOLD;
        credAmountStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(credBoxX, credBoxY + 12f, credBoxW, 28f), $"◈  {_inventory.credits}  EC  ◈", credAmountStyle);

        // ── Close button (top-right) ──
        float closeBtnSize = 32f;
        Rect closeRect = new Rect(px + panelW - closeBtnSize - 6f, py + 6f, closeBtnSize, closeBtnSize);
        bool closeHover = closeRect.Contains(Event.current.mousePosition);

        GUIStyle closeStyle = new GUIStyle();
        closeStyle.fontSize = 20;
        closeStyle.fontStyle = FontStyle.Bold;
        closeStyle.normal.textColor = closeHover ? Color.white : COL_RED;
        closeStyle.alignment = TextAnchor.MiddleCenter;
        if (GUI.Button(closeRect, "✖", closeStyle))
            Toggle(false);

        // ── ITEMS SCROLL AREA ──
        float scrollY = py + headerH + 8f;
        float scrollH = panelH - headerH - 65f;
        float itemH = 80f;
        float itemSpacing = 4f;
        float totalContentH = ShopData.BuyableItems.Count * (itemH + itemSpacing) + 10f;

        float contentW = panelW - 24f;
        Rect scrollViewRect = new Rect(px + 8f, scrollY, panelW - 16f, scrollH);
        Rect scrollContentRect = new Rect(0, 0, contentW - 16f, totalContentH);

        _scrollPos = GUI.BeginScrollView(scrollViewRect, _scrollPos, scrollContentRect);

        float itemY = 4f;
        for (int idx = 0; idx < ShopData.BuyableItems.Count; idx++)
        {
            ShopItemData item = ShopData.BuyableItems[idx];
            string icon = idx < ITEM_ICONS.Length ? ITEM_ICONS[idx] : "●";
            DrawBuyItem(4f, itemY, scrollContentRect.width - 8f, itemH, item, icon, idx % 2 == 1);
            itemY += itemH + itemSpacing;
        }

        GUI.EndScrollView();

        // ── Status bar ──
        if (!string.IsNullOrEmpty(_statusMsg))
        {
            Color sc = _statusIsError ? COL_RED : COL_GREEN;
            float alpha = Mathf.Clamp01(_statusTimer);

            // Status background
            float statusH = 36f;
            float statusY = py + panelH - statusH - 8f;
            GUI.color = new Color(sc.r, sc.g, sc.b, 0.18f * alpha);
            GUI.DrawTexture(new Rect(px + 16f, statusY, panelW - 32f, statusH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Status border
            GUI.color = new Color(sc.r, sc.g, sc.b, 0.5f * alpha);
            GUI.DrawTexture(new Rect(px + 16f, statusY, panelW - 32f, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(px + 16f, statusY + statusH, panelW - 32f, 1f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle sStyle = new GUIStyle();
            sStyle.fontSize = 14;
            sStyle.fontStyle = FontStyle.Bold;
            sStyle.normal.textColor = new Color(sc.r, sc.g, sc.b, alpha);
            sStyle.alignment = TextAnchor.MiddleCenter;

            string prefix = _statusIsError ? "✖  " : "✔  ";
            GUI.Label(new Rect(px, statusY, panelW, statusH), prefix + _statusMsg, sStyle);
        }

        // Tech corners on main panel
        DrawTechCorners(px, py, panelW, panelH, COL_CYAN, 18f, 2f);
    }

    // ── Draw Buy Item Row ────────────────────────────────────────────────────

    void DrawBuyItem(float x, float y, float w, float h, ShopItemData item, string icon, bool alt)
    {
        bool canAfford = _inventory.credits >= item.currentPrice;
        Rect itemRect = new Rect(x, y, w, h);
        bool isHover = itemRect.Contains(Event.current.mousePosition);

        // ── Item background ──
        Texture2D bg = canAfford ? (alt ? _itemBgAltTex : _itemBgTex) : _btnDisabledTex;
        GUI.DrawTexture(itemRect, bg);

        // Hover highlight
        if (isHover && canAfford)
        {
            GUI.color = new Color(COL_CYAN.r, COL_CYAN.g, COL_CYAN.b, 0.06f);
            GUI.DrawTexture(itemRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // Left accent bar
        Color accentColor = GetCategoryColor(item.category);
        GUI.color = canAfford ? accentColor : COL_DIM;
        GUI.DrawTexture(new Rect(x, y, 3f, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // ── Icon circle ──
        float iconSize = 40f;
        float iconX = x + 14f;
        float iconY = y + (h - iconSize) * 0.5f;

        // Icon background circle (simulated with box)
        GUI.color = new Color(accentColor.r, accentColor.g, accentColor.b, canAfford ? 0.15f : 0.06f);
        GUI.DrawTexture(new Rect(iconX, iconY, iconSize, iconSize), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle iconStyle = new GUIStyle();
        iconStyle.fontSize = 18;
        iconStyle.fontStyle = FontStyle.Bold;
        iconStyle.normal.textColor = canAfford ? accentColor : COL_DIM;
        iconStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(iconX, iconY, iconSize, iconSize), icon, iconStyle);

        // ── Text area ──
        float textX = iconX + iconSize + 12f;
        float textW = w - iconSize - 170f;

        // Category tag
        GUIStyle catStyle = new GUIStyle();
        catStyle.fontSize = 9;
        catStyle.fontStyle = FontStyle.Bold;
        catStyle.normal.textColor = canAfford ? accentColor : COL_DIM;
        GUI.Label(new Rect(textX, y + 8f, 120f, 14f), $"[ {item.category.ToString().ToUpper()} ]", catStyle);

        // Item name
        GUIStyle nameStyle = new GUIStyle();
        nameStyle.fontSize = 15;
        nameStyle.fontStyle = FontStyle.Bold;
        nameStyle.normal.textColor = canAfford ? Color.white : COL_DIM;
        GUI.Label(new Rect(textX, y + 22f, textW, 24f), item.displayName, nameStyle);

        // Description
        GUIStyle descStyle = new GUIStyle();
        descStyle.fontSize = 11;
        descStyle.normal.textColor = canAfford ? COL_WHITE_DIM : COL_DIM;
        descStyle.wordWrap = true;
        GUI.Label(new Rect(textX, y + 46f, textW, 28f), item.description, descStyle);

        // ── PRICE TAG (right side, very visible) ──
        float priceAreaW = 130f;
        float priceAreaX = x + w - priceAreaW - 8f;

        // Price background box
        float priceBoxH = 32f;
        float priceBoxY = y + 8f;
        GUI.DrawTexture(new Rect(priceAreaX, priceBoxY, priceAreaW, priceBoxH), _priceBgTex);

        // Price border
        Color priceColor = canAfford ? COL_GOLD : COL_RED;
        GUI.color = new Color(priceColor.r, priceColor.g, priceColor.b, 0.4f);
        GUI.DrawTexture(new Rect(priceAreaX, priceBoxY, priceAreaW, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(priceAreaX, priceBoxY + priceBoxH, priceAreaW, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(priceAreaX, priceBoxY, 1f, priceBoxH), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(priceAreaX + priceAreaW, priceBoxY, 1f, priceBoxH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Price label
        GUIStyle priceLabelStyle = new GUIStyle();
        priceLabelStyle.fontSize = 9;
        priceLabelStyle.normal.textColor = COL_WHITE_DIM;
        priceLabelStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(priceAreaX, priceBoxY, priceAreaW, 12f), "PRICE", priceLabelStyle);

        // Price value (BIG)
        GUIStyle priceStyle = new GUIStyle();
        priceStyle.fontSize = 16;
        priceStyle.fontStyle = FontStyle.Bold;
        priceStyle.normal.textColor = priceColor;
        priceStyle.alignment = TextAnchor.MiddleCenter;

        string priceText;
        if (item.basePrice != item.currentPrice)
        {
            // Show strikethrough old price + new price
            priceText = $"{item.currentPrice} EC";

            // Draw old price with line through
            GUIStyle oldPriceStyle = new GUIStyle();
            oldPriceStyle.fontSize = 10;
            oldPriceStyle.normal.textColor = COL_DIM;
            oldPriceStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(priceAreaX, priceBoxY + 10f, priceAreaW * 0.4f, 18f), $"({item.basePrice})", oldPriceStyle);
            GUI.Label(new Rect(priceAreaX + priceAreaW * 0.35f, priceBoxY + 10f, priceAreaW * 0.65f, 20f), priceText, priceStyle);
        }
        else
        {
            priceText = $"{item.currentPrice} EC";
            GUI.Label(new Rect(priceAreaX, priceBoxY + 11f, priceAreaW, 20f), priceText, priceStyle);
        }

        // ── BUY BUTTON ──
        float btnW = priceAreaW;
        float btnH = 28f;
        float btnX = priceAreaX;
        float btnY = y + h - btnH - 8f;

        string btnLabel = canAfford ? "◈  BUY" : "✖  NO EC";
        if (DrawButton(btnX, btnY, btnW, btnH, btnLabel, canAfford ? Color.white : COL_DIM, canAfford))
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
        if (_inventory.credits < item.currentPrice)
        {
            ShowStatus("Không đủ Energy Cells!", true);
            PlaySound(errorSound);
            return;
        }

        ShowStatus("Processing purchase...", false);
        _inventory.RequestBuyItemServerRpc(item.id, item.currentPrice);
    }

    void HandleShopBuyResult(string itemId)
    {
        int index = ShopData.BuyableItems.FindIndex(x => x.id == itemId);
        if (index == -1) return;

        ShopItemData item = ShopData.BuyableItems[index];
        switch (item.id)
        {
            case "antidote":
                _inventory.antidotes++;
                ShowStatus("Antidote đã thêm vào túi đồ!", false);
                break;

            case "health_pack":
                _inventory.healthPacks++;
                ShowStatus("Health Pack đã thêm vào túi đồ!", false);
                break;

            case "full_health_kit":
                // Giả sử kit to cho 2 cục máu nhỏ, hoặc 1 biến riêng. Ở đây cho 2 health pack.
                _inventory.healthPacks += 2;
                ShowStatus("2x Health Pack đã thêm vào túi đồ!", false);
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
                _inventory.oxygenTanks++;
                ShowStatus("Oxygen Tank đã thêm vào túi đồ!", false);
                break;

            default:
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
            _hintBgTex.SetPixel(0, 0, new Color(0.02f, 0.02f, 0.03f, 0.88f));
            _hintBgTex.Apply();
        }

        float hintW = 280f;
        float hintH = 36f;
        float hintX = (Screen.width - hintW) * 0.5f;
        float hintY = (Screen.height * 0.5f) + 45f;

        GUI.DrawTexture(new Rect(hintX, hintY, hintW, hintH), _hintBgTex);
        DrawGlowBorder(hintX, hintY, hintW, hintH, new Color(COL_CYAN.r, COL_CYAN.g, COL_CYAN.b, 0.7f), 1f);

        GUIStyle hintStyle = new GUIStyle();
        hintStyle.fontSize = 15;
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
        {
            Color borderCol = isHover ? COL_CYAN : new Color(COL_CYAN.r, COL_CYAN.g, COL_CYAN.b, 0.3f);
            GUI.color = borderCol;
            GUI.DrawTexture(new Rect(x, y, w, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y + h, w, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y, 1f, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x + w, y, 1f, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

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

    void DrawGlowBorder(float x, float y, float w, float h, Color color, float thick = 2f)
    {
        GUI.color = color;
        Texture2D tex = Texture2D.whiteTexture;
        GUI.DrawTexture(new Rect(x, y, w, thick), tex);             // top
        GUI.DrawTexture(new Rect(x, y + h - thick, w, thick), tex); // bottom
        GUI.DrawTexture(new Rect(x, y, thick, h), tex);             // left
        GUI.DrawTexture(new Rect(x + w - thick, y, thick, h), tex); // right
        GUI.color = Color.white;
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
