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
    public float minTimeToEvent = 30f;    // Để 30s test theo yêu cầu
    public float maxTimeToEvent = 40f;    // Max 40s
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
        if (warningTextUI == null) yield break;

        // Chọn màu theo loại event
        Color eventColor;
        switch (ev)
        {
            case GameEvent.BloodMoon:
                eventColor = new Color(1f, 0.15f, 0.15f); // Đỏ máu
                break;
            case GameEvent.ToxicFog:
                eventColor = new Color(0.3f, 1f, 0.3f);   // Xanh lục độc
                break;
            case GameEvent.Thunderstorm:
                eventColor = new Color(0.4f, 0.6f, 1f);   // Xanh lơ sấm sét
                break;
            default:
                eventColor = new Color(1f, 0.8f, 0.2f);   // Vàng cảnh báo
                break;
        }

        string hexColor = ColorUtility.ToHtmlStringRGB(eventColor);
        warningTextUI.text = $"<color=#{hexColor}><size=150%>⚠ {eventName} ⚠</size></color>\n{eventDesc}";
        
        warningTextUI.gameObject.SetActive(true); // Đảm bảo text được bật lại

        Transform root = warningTextUI.transform.parent;
        if (root == null || root.GetComponent<UnityEngine.Canvas>() != null) root = warningTextUI.transform; // If parent is canvas, fallback to text
        
        root.gameObject.SetActive(true);
        CanvasGroup cg = root.GetComponent<CanvasGroup>();
        if (cg == null) cg = root.gameObject.AddComponent<CanvasGroup>();

        // ── Fade In (1s) ──
        float fadeInTime = 1f;
        for (float t = 0; t < fadeInTime; t += Time.deltaTime)
        {
            cg.alpha = t / fadeInTime;
            yield return null;
        }
        cg.alpha = 1f;

        // ── Hold với hiệu ứng Glitch flicker (7s) ──
        float holdTime = 7f;
        float elapsed = 0f;
        while (elapsed < holdTime)
        {
            // Ngẫu nhiên 5% cơ hội flicker mỗi frame — tạo cảm giác nhiễu sóng
            if (Random.value < 0.05f)
            {
                cg.alpha = Random.Range(0.3f, 0.7f);
                yield return new WaitForSeconds(0.05f);
                cg.alpha = 1f;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // ── Fade Out (1.5s) ──
        float fadeOutTime = 1.5f;
        for (float t = 0; t < fadeOutTime; t += Time.deltaTime)
        {
            cg.alpha = 1f - (t / fadeOutTime);
            yield return null;
        }
        cg.alpha = 0f;
        root.gameObject.SetActive(false);
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

    private IEnumerator BloodMoonRoutine()
    {
        if (!_backedUp) BackupRenderSettings();

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
//             mutant.ApplyBloodMoonBuff(monsterSpeedMultiplier, monsterDetectionMultiplier);
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
//             mutant.RemoveBloodMoonBuff();
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
//                 if (survival != null) survival.TakeDamage(5f);
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
//                     survival.TakeDamage(9999, "Parasite burst from your chest!"); // Lăn ra chết lập tức
                
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
