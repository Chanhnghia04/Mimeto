using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering; // <-- Đã thêm UnityEngine.Rendering cho AmbientMode
using TMPro; // Nếu dùng TextMeshPro cho UI

public class RandomEventManager : NetworkBehaviour
{
    public static RandomEventManager Instance;

    /// <summary>
    /// Flag tĩnh để quái vật spawn SAU event vẫn bị buff.
    /// MutantAI / MimicAI kiểm tra flag này trong Start().
    /// </summary>
    public static bool IsBloodMoonActive = false;

    public enum GameEvent
    {
        None,
        ToxicFog,
        BloodMoon,
        Infection,
        Thunderstorm
    }

    [Header("Current Event State")]
    public GameEvent currentEvent = GameEvent.None;
    public ulong infectedClientId = 9999;

    [Header("Event Timing (Seconds)")]
    public float minTimeToEvent = 180f;    // Tối thiểu 3 phút
    public float maxTimeToEvent = 300f;    // Tối đa 5 phút
    private float eventTimer;
    private bool eventTriggered = false;

    [Header("Event Duration (Seconds)")]
    [Tooltip("Sương mù kéo dài bao lâu")]
    public float fogEventDuration = 75f;
    [Tooltip("Trăng máu kéo dài bao lâu")]
    public float bloodMoonDuration = 90f;
    [Tooltip("Thời gian chuyển cảnh (fade in)")]
    public float transitionInDuration = 5f;
    [Tooltip("Thời gian fade out (chậm hơn fade in cho cinematic)")]
    public float transitionOutDuration = 10f;

    [Header("Toxic Fog Settings")]
    public float fogTargetDensity = 0.15f;
    public Color fogTargetColor = new Color(0.15f, 0.3f, 0.15f); // Xanh lục độc hại

    [Header("Blood Moon Settings")]
    public Color bloodMoonAmbient = new Color(0.3f, 0.05f, 0.05f);
    public Color bloodMoonFogColor = new Color(0.4f, 0f, 0f);
    public float bloodMoonFogDensity = 0.05f;
    public Color bloodMoonSunColor = new Color(1f, 0.2f, 0.2f);
    [Tooltip("Hệ số nhân tốc độ quái vật khi Trăng Máu")]
    public float monsterSpeedMultiplier = 1.5f;
    [Tooltip("Hệ số nhân tầm nhìn/nghe quái vật khi Trăng Máu")]
    public float monsterDetectionMultiplier = 2f;

    [Header("Thunderstorm Settings")]
    public AudioClip thunderSound;
    public float thunderstormDuration = 60f;

    [Header("Event Resources")]
    public GameObject parasiteBossPrefab; // Kéo thả prefab quái vật (Mutant/Mimic) vào đây
    public AudioClip coughSound; // Kéo thả file âm thanh ho/gầm gừ vào đây
    public TextMeshProUGUI warningTextUI; // Kéo thả Text UI cảnh báo vào đây

    // ── Backup giá trị gốc để khôi phục sau event ───────────────────────────
    private bool _backedUp = false;
    private bool originalFogEnabled;
    private float originalFogDensity;
    private Color originalFogColor;
    private FogMode originalFogMode;
    private AmbientMode originalAmbientMode;
    private Color originalAmbientSkyColor;
    private Color originalAmbientEquatorColor;
    private Color originalAmbientGroundColor;
    private Color originalAmbientLight;
    private Color originalSunColor;
    private float originalSunIntensity;
    private Light cachedSunLight;

    // Coroutine handle để dọn dẹp khi cần
    private Coroutine activeEventCoroutine;
    private Coroutine activeUICoroutine;
    private Coroutine activeInfectionTimerCoroutine;
    private Coroutine activeInfectionSymptomCoroutine;
    private AudioSource _thunderAudioSource;

    [Header("Sci-Fi WOW HUD")]
    private bool _showWarningUI = false;
    private string _uiEventName = "";
    private string _uiEventDesc = "";
    private Color _uiEventColor = Color.red;
    private float _uiTimer = 0f;
    private float _uiDuration = 8.5f;
    private float _uiTypewriterProgress = 0f;
    private float _uiNoiseOffset = 0f;
    private float _uiStripeOffset = 0f;

    private bool _guiStylesReady = false;
    private GUIStyle _uiTitleStyle;
    private GUIStyle _uiSubtitleStyle;
    private Texture2D _uiAccentTex;
    private Texture2D _uiScanlineTex;
    private Texture2D _uiStripeTex;
    private Texture2D _uiGridTex;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Random.InitState((int)System.DateTime.Now.Ticks);
            // Bắt đầu đếm ngược ngay khi map load xong
            eventTimer = Random.Range(minTimeToEvent, maxTimeToEvent);
            eventTriggered = false;
            IsBloodMoonActive = false;
        }
        
        // Ẩn Text UI lúc đầu
        if (warningTextUI != null)
        {
            warningTextUI.gameObject.SetActive(false);
        }

        // Lưu lại giá trị render gốc (sau khi MapVisualSetup đã áp dụng xong)
        Invoke(nameof(BackupRenderSettings), 0.5f);
    }

    public override void OnNetworkDespawn()
    {
        // Dọn dẹp khi rời game
        if (activeEventCoroutine != null) StopCoroutine(activeEventCoroutine);
        if (activeUICoroutine != null) StopCoroutine(activeUICoroutine);
        if (activeInfectionTimerCoroutine != null) StopCoroutine(activeInfectionTimerCoroutine);
        if (activeInfectionSymptomCoroutine != null) StopCoroutine(activeInfectionSymptomCoroutine);
        IsBloodMoonActive = false;

        if (_backedUp) RestoreRenderSettings();
    }

    private void BackupRenderSettings()
    {
        originalFogEnabled = RenderSettings.fog;
        originalFogDensity = RenderSettings.fogDensity;
        originalFogColor = RenderSettings.fogColor;
        originalFogMode = RenderSettings.fogMode;
        originalAmbientMode = RenderSettings.ambientMode;
        originalAmbientSkyColor = RenderSettings.ambientSkyColor;
        originalAmbientEquatorColor = RenderSettings.ambientEquatorColor;
        originalAmbientGroundColor = RenderSettings.ambientGroundColor;
        originalAmbientLight = RenderSettings.ambientLight;

        // Tìm và cache Directional Light (mặt trời)
        Light[] lights = FindObjectsByType<Light>();
        foreach (Light l in lights)
        {
            if (l.type == LightType.Directional)
            {
                cachedSunLight = l;
                originalSunColor = l.color;
                originalSunIntensity = l.intensity;
                break;
            }
        }

        _backedUp = true;
        Debug.Log("[RandomEventManager] Render settings backed up successfully.");
    }

    private void RestoreRenderSettings()
    {
        if (!_backedUp) return;

        RenderSettings.fog = originalFogEnabled;
        RenderSettings.fogDensity = originalFogDensity;
        RenderSettings.fogColor = originalFogColor;
        RenderSettings.fogMode = originalFogMode;
        RenderSettings.ambientMode = originalAmbientMode;
        RenderSettings.ambientSkyColor = originalAmbientSkyColor;
        RenderSettings.ambientEquatorColor = originalAmbientEquatorColor;
        RenderSettings.ambientGroundColor = originalAmbientGroundColor;
        RenderSettings.ambientLight = originalAmbientLight;

        if (cachedSunLight != null)
        {
            cachedSunLight.color = originalSunColor;
            cachedSunLight.intensity = originalSunIntensity;
        }
    }

    private void Update()
    {
        // Cập nhật vị trí mặt trăng máu (client & server) để nó luôn ở xa trên trời
        if (_visualBloodMoon != null && _visualBloodMoon.activeSelf && Camera.main != null)
        {
            _visualBloodMoon.transform.position = Camera.main.transform.position + new Vector3(3000f, 1500f, 3000f);
        }

        if (!IsServer || eventTriggered) return;

        eventTimer -= Time.deltaTime;
        if (eventTimer <= 0)
        {
            TriggerRandomEvent();
        }
    }

    private void TriggerRandomEvent()
    {
        eventTriggered = true;
        
        // Chọn ngẫu nhiên sự kiện (từ 1 đến 4)
        int eventIndex = Random.Range(1, 5);
        currentEvent = (GameEvent)eventIndex;

        string eventName = "";
        string eventDesc = "";

        switch (currentEvent)
        {
            case GameEvent.ToxicFog:
                eventName = "TOXIC FOG";
                eventDesc = "WARNING: A toxic spore cloud is approaching. Visibility severely reduced!";
                break;
            
            case GameEvent.BloodMoon:
                eventName = "BLOOD MOON";
                eventDesc = "WARNING: The Blood Moon is rising. They are starving!";
                break;
            
            case GameEvent.Infection:
                eventName = "AI PARASITE";
                eventDesc = "WARNING: The parasite has breached. One of you is infected!";
                
                // Thuật toán lây nhiễm ngẫu nhiên (Chỉ Host biết)
                var clients = NetworkManager.Singleton.ConnectedClientsIds;
                if (clients.Count > 0)
                {
                    int randomIndex = Random.Range(0, clients.Count);
                    infectedClientId = clients[randomIndex];
                    Debug.Log($"[Server] Đã lây nhiễm ngẫu nhiên cho Client: {infectedClientId}");
                    
                    // Bắt đầu bộ đếm giờ chết và triệu chứng
                    activeInfectionTimerCoroutine = StartCoroutine(InfectionTimerRoutine());
                    activeInfectionSymptomCoroutine = StartCoroutine(InfectionSymptomRoutine());
                }
                break;
                
            case GameEvent.Thunderstorm:
                eventName = "THUNDERSTORM";
                eventDesc = "WARNING: Severe thunderstorm approaching. Lights will fail.";
                break;
        }

        // Gọi lệnh xuống tất cả các máy tính để hiển thị UI và áp dụng hiệu ứng
        TriggerEventClientRpc(currentEvent, eventName, eventDesc);
    }

    [ClientRpc]
    private void TriggerEventClientRpc(GameEvent ev, string eventName, string eventDesc)
    {
        currentEvent = ev;
        Debug.LogWarning($"[EVENT] {eventName}: {eventDesc}");
        
        // Hiển thị UI giữa màn hình (có hiệu ứng fade + glitch)
        if (activeUICoroutine != null) StopCoroutine(activeUICoroutine);
        activeUICoroutine = StartCoroutine(ShowWarningUIRoutine(eventName, eventDesc, ev));

        // Áp dụng HIỆU ỨNG THỊ GIÁC / QUÁI VẬT
        switch (ev)
        {
            case GameEvent.ToxicFog:
                if (activeEventCoroutine != null) StopCoroutine(activeEventCoroutine);
                activeEventCoroutine = StartCoroutine(ToxicFogRoutine());
                break;

            case GameEvent.BloodMoon:
                if (activeEventCoroutine != null) StopCoroutine(activeEventCoroutine);
                activeEventCoroutine = StartCoroutine(BloodMoonRoutine());
                break;
                
            case GameEvent.Thunderstorm:
                if (activeEventCoroutine != null) StopCoroutine(activeEventCoroutine);
                activeEventCoroutine = StartCoroutine(ThunderstormRoutine());
                break;
        }
    }

    // ==========================================
    // UI CẢNH BÁO (Fade In → Glitch → Fade Out)
    // ==========================================

    private IEnumerator ShowWarningUIRoutine(string eventName, string eventDesc, GameEvent ev)
    {
        if (warningTextUI != null) {
            Transform root = warningTextUI.transform.parent;
            if (root == null || root.GetComponent<UnityEngine.Canvas>() != null) root = warningTextUI.transform;
            root.gameObject.SetActive(false);
        }

        Color eventColor;
        switch (ev)
        {
            case GameEvent.BloodMoon:
                eventColor = new Color(1f, 0.15f, 0.15f); 
                break;
            case GameEvent.ToxicFog:
                eventColor = new Color(0.3f, 1f, 0.3f);   
                break;
            case GameEvent.Thunderstorm:
                eventColor = new Color(0.4f, 0.6f, 1f);   
                break;
            case GameEvent.Infection:
                eventColor = new Color(0.8f, 0.2f, 1f);   
                break;
            default:
                eventColor = new Color(1f, 0.8f, 0.2f);   
                break;
        }

        _uiEventName = eventName;
        _uiEventDesc = eventDesc;
        _uiEventColor = eventColor;
        _uiDuration = 8.5f;
        _uiTimer = _uiDuration;
        _uiTypewriterProgress = 0f;
        _showWarningUI = true;

        while (_uiTimer > 0)
        {
            _uiTimer -= Time.deltaTime;
            _uiNoiseOffset += Time.deltaTime * 20f;
            _uiTypewriterProgress += Time.deltaTime * 60f;
            _uiStripeOffset += Time.deltaTime * 1.5f;
            yield return null;
        }

        _showWarningUI = false;
    }

    private void OnGUI()
    {
        if (!_showWarningUI) return;
        EnsureGUIStyles();

        float vWidth = 1920f;
        float vHeight = 1080f;
        Vector3 scale = new Vector3(Screen.width / vWidth, Screen.height / vHeight, 1f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, scale);
        
        float t = 1f - (_uiTimer / _uiDuration); 
        float alpha = 1f;

        if (t < 0.1f) alpha = Mathf.Lerp(0f, 1f, t / 0.1f);
        else if (t > 0.9f) alpha = Mathf.Lerp(1f, 0f, (t - 0.9f) / 0.1f); 
        
        float animT = 1f;
        if (t < 0.15f) animT = t / 0.15f;
        else if (t > 0.85f) animT = 1f - ((t - 0.85f) / 0.15f);
        float smoothAnim = 1f - Mathf.Pow(1f - animT, 3f); 
        
        float pulse = Mathf.Abs(Mathf.Sin(Time.time * 6f));
        
        // ── 1. SCREEN VIGNETTE / DIMMING ──
        GUI.color = new Color(0.05f, 0f, 0f, alpha * 0.45f);
        GUI.DrawTexture(new Rect(0, 0, vWidth, vHeight), _uiAccentTex);
        
        // Cinematic borders
        GUI.color = new Color(0, 0, 0, alpha * 0.95f);
        float barH = 120f * smoothAnim;
        GUI.DrawTexture(new Rect(0, 0, vWidth, barH), _uiAccentTex);
        GUI.DrawTexture(new Rect(0, vHeight - barH, vWidth, barH), _uiAccentTex);

        // Heavy Glitch Offset
        float glitchX = 0f;
        float glitchY = 0f;
        if (t > 0.1f && t < 0.9f && Random.value > 0.9f) 
        {
            alpha *= Random.Range(0.2f, 0.9f);
            glitchX = Random.Range(-30f, 30f);
            glitchY = Random.Range(-15f, 15f);
        }

        float bw = vWidth;
        float maxBh = 180f;
        float bh = maxBh * smoothAnim;
        if (bh < 2f) bh = 2f; 
        
        float by = vHeight * 0.15f + (maxBh - bh) / 2f + glitchY; 

        // Banner Base
        GUI.color = new Color(_uiEventColor.r * 0.15f, _uiEventColor.g * 0.15f, _uiEventColor.b * 0.15f, alpha * 0.95f);
        GUI.DrawTexture(new Rect(glitchX, by, bw, bh), _uiAccentTex);
        
        GUI.color = new Color(_uiEventColor.r, _uiEventColor.g, _uiEventColor.b, alpha * 0.15f);
        GUI.DrawTextureWithTexCoords(new Rect(glitchX, by, bw, bh), _uiGridTex, new Rect(_uiNoiseOffset * 0.1f, 0, bw / 32f, bh / 32f));
        
        GUI.color = new Color(1f, 1f, 1f, alpha * 0.15f);
        GUI.DrawTextureWithTexCoords(new Rect(glitchX, by, bw, bh), _uiScanlineTex, new Rect(0, _uiNoiseOffset * 0.1f, bw, bh / 2f));
        
        if (bh > 24f) {
            // Hazard Stripes
            GUI.color = new Color(_uiEventColor.r, _uiEventColor.g, _uiEventColor.b, alpha * (0.6f + pulse * 0.4f));
            GUI.DrawTextureWithTexCoords(new Rect(glitchX, by, bw, 24f), _uiStripeTex, new Rect(_uiStripeOffset, 0, bw / 50f, 1));
            GUI.DrawTextureWithTexCoords(new Rect(glitchX, by + bh - 24f, bw, 24f), _uiStripeTex, new Rect(-_uiStripeOffset, 0, bw / 50f, 1));

            // Inner Bright Lines
            GUI.color = new Color(1f, 1f, 1f, alpha * 0.8f);
            GUI.DrawTexture(new Rect(glitchX, by + 24f, bw, 2f), _uiAccentTex);
            GUI.DrawTexture(new Rect(glitchX, by + bh - 26f, bw, 2f), _uiAccentTex);

            float boxW = 1680f;
            float boxX = (vWidth - boxW) / 2f + glitchX;
            DrawTechCorners(boxX, by + 12f, boxW, bh - 24f, _uiEventColor, alpha);

            // Warning Icons & Data Barcodes
            GUIStyle warningStyle = new GUIStyle { fontSize = 70, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            warningStyle.normal.textColor = new Color(_uiEventColor.r, _uiEventColor.g, _uiEventColor.b, alpha * (0.5f + pulse * 0.5f));
            GUI.Label(new Rect(boxX + 40f, by + (bh - 80f) / 2f, 80f, 80f), "⚠", warningStyle);
            GUI.Label(new Rect(boxX + boxW - 120f, by + (bh - 80f) / 2f, 80f, 80f), "⚠", warningStyle);
            
            GUIStyle smallTechStyle = new GUIStyle { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            smallTechStyle.normal.textColor = new Color(_uiEventColor.r, _uiEventColor.g, _uiEventColor.b, alpha * 0.7f);
            GUI.Label(new Rect(boxX + 160f, by + 35f, 300f, 30f), $"SYS_OVERRIDE // {Time.frameCount}", smallTechStyle);
            smallTechStyle.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(boxX + boxW - 460f, by + bh - 65f, 300f, 30f), $"HAZARD_LVL_MAX", smallTechStyle);

            Vector2 textOffset = (pulse > 0.8f && Random.value > 0.7f) ? new Vector2(Random.Range(-12f, 12f), Random.Range(-6f, 6f)) : Vector2.zero;
            
            // Text Scrambling Effect
            string displayTitle = _uiEventName.ToUpper();
            if (t < 0.2f && Random.value > (t / 0.2f)) {
                char[] chars = new char[displayTitle.Length];
                string charset = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*";
                for(int i=0; i<chars.Length; i++) chars[i] = (displayTitle[i] == ' ') ? ' ' : charset[Random.Range(0, charset.Length)];
                displayTitle = new string(chars);
            }

            // Extreme Chromatic Aberration
            if (pulse > 0.5f && Random.value > 0.4f) {
                _uiTitleStyle.normal.textColor = new Color(1f, 0f, 0f, alpha * 0.9f);
                GUI.Label(new Rect(boxX + textOffset.x - 10f, by + (bh - 100f) / 2f + textOffset.y, boxW, 100f), displayTitle, _uiTitleStyle);
                _uiTitleStyle.normal.textColor = new Color(0f, 1f, 1f, alpha * 0.9f);
                GUI.Label(new Rect(boxX + textOffset.x + 10f, by + (bh - 100f) / 2f + textOffset.y, boxW, 100f), displayTitle, _uiTitleStyle);
            }
            
            _uiTitleStyle.normal.textColor = new Color(1f, 1f, 1f, alpha); // Solid white core
            GUI.Label(new Rect(boxX + textOffset.x, by + (bh - 100f) / 2f + textOffset.y, boxW, 100f), displayTitle, _uiTitleStyle);
            
            // Fast data stream particles
            GUI.color = new Color(_uiEventColor.r, _uiEventColor.g, _uiEventColor.b, alpha * 0.8f);
            for(int i=0; i<25; i++) {
                float px = Mathf.Repeat(Time.time * 600f * (1f + i*0.1f) + i*150f, vWidth);
                float py = by + Mathf.PingPong(Time.time * 100f + i*99f, bh - 6f) + 3f;
                GUI.DrawTexture(new Rect(px, py, 20f + (i%5)*15f, 3f), _uiAccentTex);
            }
        }

        GUI.matrix = Matrix4x4.identity;
    }

    private void DrawTechCorners(float x, float y, float w, float h, Color color, float alpha)
    {
        GUI.color = new Color(color.r, color.g, color.b, alpha * 0.8f);
        float len = 50f;
        float thick = 6f;
        GUI.DrawTexture(new Rect(x, y, len, thick), _uiAccentTex);
        GUI.DrawTexture(new Rect(x, y, thick, len), _uiAccentTex);
        GUI.DrawTexture(new Rect(x + w - len, y, len, thick), _uiAccentTex);
        GUI.DrawTexture(new Rect(x + w - thick, y, thick, len), _uiAccentTex);
        GUI.DrawTexture(new Rect(x, y + h - thick, len, thick), _uiAccentTex);
        GUI.DrawTexture(new Rect(x, y + h - len, thick, len), _uiAccentTex);
        GUI.DrawTexture(new Rect(x + w - len, y + h - thick, len, thick), _uiAccentTex);
        GUI.DrawTexture(new Rect(x + w - thick, y + h - len, thick, len), _uiAccentTex);
    }

    private void EnsureGUIStyles()
    {
        if (_guiStylesReady) return;
        _guiStylesReady = true;

        _uiAccentTex = new Texture2D(1, 1);
        _uiAccentTex.SetPixel(0, 0, Color.white);
        _uiAccentTex.Apply();

        _uiScanlineTex = new Texture2D(2, 4);
        for(int y=0; y<4; y++) for(int x=0; x<2; x++) _uiScanlineTex.SetPixel(x, y, y % 2 == 0 ? new Color(0,0,0,0) : new Color(0,0,0,0.6f));
        _uiScanlineTex.filterMode = FilterMode.Point;
        _uiScanlineTex.Apply();

        _uiStripeTex = new Texture2D(64, 64);
        for (int y = 0; y < 64; y++) {
            for (int x = 0; x < 64; x++) {
                bool isStripe = ((x + y) % 32) < 16;
                _uiStripeTex.SetPixel(x, y, isStripe ? Color.white : new Color(1,1,1,0));
            }
        }
        _uiStripeTex.wrapMode = TextureWrapMode.Repeat;
        _uiStripeTex.Apply();

        _uiGridTex = new Texture2D(32, 32);
        for(int y=0; y<32; y++) {
            for(int x=0; x<32; x++) {
                bool isBorder = (x == 0 || y == 0);
                _uiGridTex.SetPixel(x, y, isBorder ? new Color(1,1,1,0.5f) : new Color(0,0,0,0));
            }
        }
        _uiGridTex.wrapMode = TextureWrapMode.Repeat;
        _uiGridTex.Apply();

        _uiTitleStyle = new GUIStyle { fontSize = 68, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        _uiSubtitleStyle = new GUIStyle { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
    }

    // ==========================================
    // SỰ KIỆN 1: SƯƠNG MÙ ĐỘC HẠI
    // ==========================================
    // Fog chuyển DẦN DẦN bằng Lerp, không đột ngột.
    // Fade in (ease-in) → Hold → Fade out (chậm hơn)

    private IEnumerator ToxicFogRoutine()
    {
        if (!_backedUp) BackupRenderSettings();

        // ── Phase 1: Fade In — Sương mù tràn tới từ từ ──────────────────────
        RenderSettings.fog = true;
        float startDensity = RenderSettings.fogDensity;
        Color startColor = RenderSettings.fogColor;

        for (float t = 0; t < transitionInDuration; t += Time.deltaTime)
        {
            float progress = t / transitionInDuration;
            // Ease-in (bắt đầu chậm, kết thúc nhanh) — sương mù ùa tới nhanh dần
            float eased = progress * progress;

            RenderSettings.fogDensity = Mathf.Lerp(startDensity, fogTargetDensity, eased);
            RenderSettings.fogColor = Color.Lerp(startColor, fogTargetColor, eased);
            yield return null;
        }
        // Đảm bảo đạt giá trị cuối cùng chính xác
        RenderSettings.fogDensity = fogTargetDensity;
        RenderSettings.fogColor = fogTargetColor;

        Debug.Log("[ToxicFog] ☁️ Sương mù đã phủ kín! Tầm nhìn < 5m!");

        // ── Phase 2: Hold — Giữ nguyên sương mù dày đặc ────────────────────
        yield return new WaitForSeconds(fogEventDuration);

        // ── Phase 3: Fade Out — Sương mù tan dần ────────────────────────────
        Debug.Log("[ToxicFog] Sương mù đang tan dần...");

        float currentDensity = RenderSettings.fogDensity;
        Color currentColor = RenderSettings.fogColor;

        for (float t = 0; t < transitionOutDuration; t += Time.deltaTime)
        {
            float progress = t / transitionOutDuration;
            float eased = Mathf.SmoothStep(0f, 1f, progress); // Mượt 2 đầu

            RenderSettings.fogDensity = Mathf.Lerp(currentDensity, originalFogDensity, eased);
            RenderSettings.fogColor = Color.Lerp(currentColor, originalFogColor, eased);
            yield return null;
        }

        RestoreRenderSettings();
        currentEvent = GameEvent.None;
        Debug.Log("[ToxicFog] ✅ Sự kiện kết thúc. Render settings đã khôi phục.");
        // Giữ nguyên eventTriggered = true để không gọi sự kiện khác nữa.
    }

    // ==========================================
    // SỰ KIỆN 2: TRĂNG MÁU
    // ==========================================
    // Ánh sáng chuyển đỏ DẦN DẦN bằng SmoothStep.
    // Buff quái vật: speed ×1.5, detection ×2.
    // Quái spawn SAU event cũng bị buff nhờ IsBloodMoonActive.

    private GameObject _visualBloodMoon;

    private IEnumerator BloodMoonRoutine()
    {
        if (!_backedUp) BackupRenderSettings();

        // Spawn or show Blood Moon Sphere
        if (_visualBloodMoon == null)
        {
            _visualBloodMoon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _visualBloodMoon.name = "VisualBloodMoon";
            Destroy(_visualBloodMoon.GetComponent<Collider>());
            MeshRenderer mr = _visualBloodMoon.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = new Color(1f, 0.05f, 0.05f); // Deep red
            mr.material = mat;
            _visualBloodMoon.transform.localScale = new Vector3(800f, 800f, 800f);
            
            // Try to place it far away in the sky
            Vector3 camPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            _visualBloodMoon.transform.position = camPos + new Vector3(3000f, 1500f, 3000f);
        }
        _visualBloodMoon.SetActive(true);

        // ── Phase 1: Visual Fade In — Bầu trời chuyển đỏ ────────────────────
        RenderSettings.fog = true;
        float startFogDensity = RenderSettings.fogDensity;
        Color startFogColor = RenderSettings.fogColor;
        Color startAmbientSky = RenderSettings.ambientSkyColor;
        Color startAmbientEquator = RenderSettings.ambientEquatorColor;
        Color startAmbientGround = RenderSettings.ambientGroundColor;
        Color startSunColor = cachedSunLight != null ? cachedSunLight.color : Color.white;

        for (float t = 0; t < transitionInDuration; t += Time.deltaTime)
        {
            float progress = t / transitionInDuration;
            float eased = Mathf.SmoothStep(0f, 1f, progress);

            RenderSettings.fogDensity = Mathf.Lerp(startFogDensity, bloodMoonFogDensity, eased);
            RenderSettings.fogColor = Color.Lerp(startFogColor, bloodMoonFogColor, eased);
            RenderSettings.ambientSkyColor = Color.Lerp(startAmbientSky, bloodMoonAmbient, eased);
            RenderSettings.ambientEquatorColor = Color.Lerp(startAmbientEquator, bloodMoonAmbient * 0.7f, eased);
            RenderSettings.ambientGroundColor = Color.Lerp(startAmbientGround, bloodMoonAmbient * 0.5f, eased);

            if (cachedSunLight != null)
            {
                cachedSunLight.color = Color.Lerp(startSunColor, bloodMoonSunColor, eased);
            }

            yield return null;
        }

        // Đặt giá trị cuối cùng chính xác
        RenderSettings.fogDensity = bloodMoonFogDensity;
        RenderSettings.fogColor = bloodMoonFogColor;
        RenderSettings.ambientSkyColor = bloodMoonAmbient;
        if (cachedSunLight != null) cachedSunLight.color = bloodMoonSunColor;

        // ── Phase 2: Buff ALL quái vật (Server only) ─────────────────────────
        if (IsServer)
        {
            IsBloodMoonActive = true;
            BuffAllMonsters();
        }

        Debug.Log("[BloodMoon] 🩸 Trăng Máu đã lên! Quái vật điên cuồng!");

        // ── Phase 3: Hold — Giữ nguyên trạng thái điên cuồng ────────────────
        yield return new WaitForSeconds(bloodMoonDuration);

        // ── Phase 4: Debuff quái vật (Server only) ───────────────────────────
        if (IsServer)
        {
            IsBloodMoonActive = false;
            DebuffAllMonsters();
        }

        // ── Phase 5: Visual Fade Out — Bầu trời trở lại bình thường ─────────
        Debug.Log("[BloodMoon] Trăng Máu đang lặn...");
        
        if (_visualBloodMoon != null)
        {
            _visualBloodMoon.SetActive(false);
        }

        float curFogDensity = RenderSettings.fogDensity;
        Color curFogColor = RenderSettings.fogColor;
        Color curAmbientSky = RenderSettings.ambientSkyColor;
        Color curAmbientEquator = RenderSettings.ambientEquatorColor;
        Color curAmbientGround = RenderSettings.ambientGroundColor;
        Color curSunColor = cachedSunLight != null ? cachedSunLight.color : Color.white;

        for (float t = 0; t < transitionOutDuration; t += Time.deltaTime)
        {
            float progress = t / transitionOutDuration;
            float eased = Mathf.SmoothStep(0f, 1f, progress);

            RenderSettings.fogDensity = Mathf.Lerp(curFogDensity, originalFogDensity, eased);
            RenderSettings.fogColor = Color.Lerp(curFogColor, originalFogColor, eased);
            RenderSettings.ambientSkyColor = Color.Lerp(curAmbientSky, originalAmbientSkyColor, eased);
            RenderSettings.ambientEquatorColor = Color.Lerp(curAmbientEquator, originalAmbientEquatorColor, eased);
            RenderSettings.ambientGroundColor = Color.Lerp(curAmbientGround, originalAmbientGroundColor, eased);

            if (cachedSunLight != null)
            {
                cachedSunLight.color = Color.Lerp(curSunColor, originalSunColor, eased);
            }

            yield return null;
        }

        RestoreRenderSettings();
        currentEvent = GameEvent.None;
        Debug.Log("[BloodMoon] ✅ Sự kiện kết thúc. Render settings đã khôi phục.");
        // Giữ nguyên eventTriggered = true để không gọi sự kiện khác nữa.
    }

    // ==========================================
    // SỰ KIỆN 3: SẤM CHỚP (Thunderstorm)
    // ==========================================

    private IEnumerator ThunderstormRoutine()
    {
        if (!_backedUp) BackupRenderSettings();
        Debug.Log("[Thunderstorm] ⛈️ Sấm chớp bắt đầu!");
        
        float elapsed = 0f;
        while (elapsed < thunderstormDuration)
        {
            if (currentEvent != GameEvent.Thunderstorm) break;
            
            // Chờ ngẫu nhiên trước lần sét tiếp theo
            float waitTime = Random.Range(5f, 15f);
            yield return new WaitForSeconds(waitTime);
            elapsed += waitTime;
            
            // Sấm chớp nhấp nháy
            int flashes = Random.Range(2, 4);
            for (int i = 0; i < flashes; i++)
            {
                if (cachedSunLight != null) cachedSunLight.intensity = originalSunIntensity * Random.Range(3f, 6f);
                yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
                if (cachedSunLight != null) cachedSunLight.intensity = originalSunIntensity * 0.1f;
                yield return new WaitForSeconds(Random.Range(0.05f, 0.2f));
            }
            
            // Khôi phục ánh sáng mặt trời
            if (cachedSunLight != null) cachedSunLight.intensity = originalSunIntensity;
            
            // Âm thanh sấm sét trễ một chút
            yield return new WaitForSeconds(Random.Range(0.2f, 1.5f));
            if (thunderSound != null)
            {
                if (_thunderAudioSource == null)
                {
                    _thunderAudioSource = GetComponent<AudioSource>();
                    if (_thunderAudioSource == null) _thunderAudioSource = gameObject.AddComponent<AudioSource>();
                }
                _thunderAudioSource.PlayOneShot(thunderSound);
            }
        }
        
        if (cachedSunLight != null) cachedSunLight.intensity = originalSunIntensity;
        RestoreRenderSettings();
        currentEvent = GameEvent.None;
        Debug.Log("[Thunderstorm] ✅ Sự kiện sấm chớp kết thúc.");
    }

    // ==========================================
    // BUFF / DEBUFF QUÁI VẬT (Blood Moon)
    // ==========================================

    private void BuffAllMonsters()
    {
        MutantAI[] mutants = FindObjectsByType<MutantAI>();
        foreach (var mutant in mutants)
        {
            mutant.ApplyBloodMoonBuff(monsterSpeedMultiplier, monsterDetectionMultiplier);
        }

UnityEngine.Component[] mimics = new UnityEngine.Component[0];
//         MimicAI[] mimics = FindObjectsByType<MimicAI>();
        foreach (var mimic in mimics)
        {
//             mimic.ApplyBloodMoonBuff(monsterSpeedMultiplier, monsterDetectionMultiplier);
        }

        Debug.Log($"[BloodMoon] Buffed {mutants.Length} Mutants + {mimics.Length} Mimics " +
                  $"(speed ×{monsterSpeedMultiplier}, detection ×{monsterDetectionMultiplier})");
    }

    private void DebuffAllMonsters()
    {
        MutantAI[] mutants = FindObjectsByType<MutantAI>();
        foreach (var mutant in mutants)
        {
            mutant.RemoveBloodMoonBuff();
        }

UnityEngine.Component[] mimics = new UnityEngine.Component[0];
//         MimicAI[] mimics = FindObjectsByType<MimicAI>();
        foreach (var mimic in mimics)
        {
//             mimic.RemoveBloodMoonBuff();
        }

        Debug.Log("[BloodMoon] Tất cả quái vật đã trở lại bình thường.");
    }

    private void HideWarningUI()
    {
        if (warningTextUI != null) {
            Transform root = warningTextUI.transform.parent;
            if (root == null || root.GetComponent<UnityEngine.Canvas>() != null) root = warningTextUI.transform;
            root.gameObject.SetActive(false);
        }
    }

    // ==========================================
    // LOGIC INFECTION (Chỉ chạy trên Server)
    // ==========================================
    
    [ServerRpc(RequireOwnership = false)]
    public void CureInfectionServerRpc(ulong clientId)
    {
        if (currentEvent == GameEvent.Infection && infectedClientId == clientId)
        {
            currentEvent = GameEvent.None;
            infectedClientId = 9999;
            Debug.Log($"[Server] Cured player {clientId} from infection!");
        }
    }
    
    private IEnumerator InfectionTimerRoutine()
    {
        // 3 phút đếm ngược cho đến khi vỡ bụng
        yield return new WaitForSeconds(180f);
        
        if (currentEvent == GameEvent.Infection && infectedClientId != 9999)
        {
            // Hết giờ mà chưa có thuốc giải!
            KillAndSpawnBossClientRpc(infectedClientId);
        }

        currentEvent = GameEvent.None;
        infectedClientId = 9999;
        // Giữ nguyên eventTriggered = true để không gọi sự kiện khác nữa.
    }

    private IEnumerator InfectionSymptomRoutine()
    {
        while (currentEvent == GameEvent.Infection && infectedClientId != 9999)
        {
            // Đợi ngẫu nhiên 20 - 30 giây
            yield return new WaitForSeconds(Random.Range(20f, 30f));
            
            if (currentEvent != GameEvent.Infection || infectedClientId == 9999) yield break;
            
            // Tìm tất cả client TRỪ người bị nhiễm
            List<ulong> targetIds = new List<ulong>();
            foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (id != infectedClientId)
                {
                    targetIds.Add(id); // Chỉ những người khỏe mạnh mới được nghe tiếng ho
                }
            }

            if (targetIds.Count > 0)
            {
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = targetIds.ToArray() }
                };
                
                // Gửi lệnh phát âm thanh tới những người khỏe mạnh
                PlaySymptomSoundClientRpc(infectedClientId, clientRpcParams);
            }
            
            // Gửi thông báo riêng cho người bị nhiễm để họ biết
            ClientRpcParams infectedParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { infectedClientId } }
            };
            NotifyInfectedPlayerClientRpc(infectedParams);
        }
    }

    [ClientRpc]
    private void NotifyInfectedPlayerClientRpc(ClientRpcParams rpcParams = default)
    {
        if (activeUICoroutine != null) StopCoroutine(activeUICoroutine);
        activeUICoroutine = StartCoroutine(ShowWarningUIRoutine("TRIỆU CHỨNG", "Có thứ gì đó đang ngoe nguậy trong bụng bạn...", GameEvent.Infection));
        
        // Trừ đi một lượng máu nhỏ để player giật mình
        PlayerController[] players = FindObjectsByType<PlayerController>();
        foreach (var p in players)
        {
            if (p.IsOwner && p.OwnerClientId == NetworkManager.Singleton.LocalClientId)
            {
                var survival = p.GetComponent<PlayerSurvival>();
                if (survival != null) survival.TakeDamage(5f);
            }
        }
    }

    [ClientRpc]
    private void PlaySymptomSoundClientRpc(ulong targetId, ClientRpcParams clientRpcParams = default)
    {
        // Hàm này CHỈ người KHÔNG BỊ NHIỄM mới nhận được! (Người bị nhiễm đang bị "điếc" tiếng của chính mình)
        
        PlayerController[] players = FindObjectsByType<PlayerController>();
        foreach (var p in players)
        {
            if (p.OwnerClientId == targetId)
            {
                // Phát ra tiếng động từ vị trí của người bị bệnh
                AudioSource source = p.GetComponent<AudioSource>();
                if (source != null && coughSound != null)
                {
                    source.pitch = Random.Range(0.7f, 0.9f); // Méo tiếng một chút cho rùng rợn
                    source.PlayOneShot(coughSound);
                }
                else
                {
                    Debug.Log($"[Triệu chứng] Lẽ ra bạn sẽ nghe thấy tiếng rên rỉ từ {p.gameObject.name} nhưng chưa gán AudioClip!");
                }
            }
        }
    }

    [ClientRpc]
    private void KillAndSpawnBossClientRpc(ulong targetId)
    {
        PlayerController[] players = FindObjectsByType<PlayerController>();
        foreach (var p in players)
        {
            if (p.OwnerClientId == targetId)
            {
                Debug.Log($"[Infection] Người chơi {targetId} đã bị Ký Sinh Trùng xé toạc!");
                
                // Gọi hàm chết của người chơi
                var survival = p.GetComponent<PlayerSurvival>();
                if (survival != null && p.IsOwner) 
                    survival.TakeDamage(9999, "Parasite burst from your chest!"); // Lăn ra chết lập tức
                
                // Sinh ra con Boss khổng lồ từ xác chết
                if (IsServer && parasiteBossPrefab != null)
                {
                    GameObject boss = Instantiate(parasiteBossPrefab, p.transform.position, Quaternion.identity);
                    boss.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f); // Cho con boss to hơn bình thường
                    boss.GetComponent<NetworkObject>().Spawn();
                }
            }
        }
    }
}
