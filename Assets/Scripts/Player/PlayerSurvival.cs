using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public enum GasMaskType { None, Basic, Advanced }

public class PlayerSurvival : NetworkBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth;
    public float toxicDamagePerSecond = 5f;

    // Bug Fix: expose a spawn point so Respawn() doesn't always teleport to (0,2,0)
    [Header("Spawn Settings")]
    public Transform spawnPoint;
    
    [Header("Oxygen Settings")]
    public float maxOxygen = 100f;
    public float currentOxygen;
    public float oxygenDepletionRate = 2f; // Oxygen drops by 2 per second
    public float oxygenRestoreRate = 10f; // Oxygen restores by 10 per second in Safe Zone
    public bool inSafeZone = false;

    [Header("Mask Settings")]
    public GasMaskType activeMaskType = GasMaskType.None;
    public float maskDurability = 0f;
    public float basicMaskProtection = 0.8f; // 80% reduction
    public float advancedMaskProtection = 0.95f; // 95% reduction
    
    [Header("UI References")]
    public Slider healthBar;
    public Slider oxygenBar;
    public GameObject deathPanel;
    public Camera deathCamera;

    // Basic lasts ~60s, Advanced lasts ~300s
    private float basicDurabilityLoss = 1.66f; // 100/60
    private float advancedDurabilityLoss = 0.33f; // 100/300

    public Transform headTransform;
    private GameObject equippedMaskInstance;

    // Fix: proper timer to avoid Debug.LogWarning spam based on FPS
    private float nextWarningTime = 0f;

    [Header("Audio")]
    public AudioSource breathingSource;
    public AudioSource sfxSource;
    public AudioClip heavyBreathingClip;
    public AudioClip damageHitClip;
    public float lowOxygenThreshold = 30f;

    void Start()
    {
        // Safe fallbacks to prevent 0 initialization from empty Inspector settings
        if (maxHealth <= 0) maxHealth = 100f;
        if (maxOxygen <= 0) maxOxygen = 100f;

        currentHealth = maxHealth;
        currentOxygen = maxOxygen;
        
        // Setup AudioSources if missing
        if (breathingSource == null) breathingSource = gameObject.AddComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        
        breathingSource.loop = true;
        breathingSource.playOnAwake = false;
        breathingSource.spatialBlend = 0f; // 2D for player breathing
        
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;

        // Move player to spawn point at the start of the game
        if (spawnPoint != null)
        {
            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation;
        }
        
        // Hide old UI sliders to use the new Sci-Fi IMGUI
        if (healthBar != null) healthBar.gameObject.SetActive(false);
        if (oxygenBar != null) oxygenBar.gameObject.SetActive(false);
        
        // Try to find the head automatically if not set
        if (headTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) headTransform = cam.transform;
            else headTransform = transform;
        }
    }

    void Update()
    {
        if (IsSpawned && !IsOwner) return; // CHỈ CẬP NHẬT MÁU/OXY CHO NHÂN VẬT CỦA MÌNH HOẶC KHI OFFLINE

        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName == "StartGame" || sceneName == "Menu")
        {
            if (breathingSource != null && breathingSource.isPlaying)
                breathingSource.Stop();
            return; // Không tính toán oxy/máu trong sảnh
        }

        if (inSafeZone)
        {
            // Restore oxygen and don't deplete mask or oxygen
            currentOxygen += oxygenRestoreRate * Time.deltaTime;
            if (currentOxygen > maxOxygen) currentOxygen = maxOxygen;
        }
        else if (activeMaskType != GasMaskType.None)
        {
            float loss = (activeMaskType == GasMaskType.Advanced) ? advancedDurabilityLoss : basicDurabilityLoss;
            maskDurability -= loss * Time.deltaTime;
            
            if (maskDurability <= 0)
            {
                maskDurability = 0;
                Debug.Log($"<color=red>Gas Mask ({activeMaskType}) broke!</color>");
                activeMaskType = GasMaskType.None;
                UnequipVisualMask();
            }
        }
        else
        {
            // Deplete oxygen only when NOT wearing a mask
            currentOxygen -= oxygenDepletionRate * Time.deltaTime;
            if (currentOxygen <= 0)
            {
                currentOxygen = 0;
                // Lose 5 health per second when oxygen is 0
                TakeDamage(5f * Time.deltaTime);
                
                // Fix: use a timer variable instead of Time.time % interval to avoid FPS-dependent spam
                if (Time.time >= nextWarningTime)
                {
                    Debug.LogWarning("DANGER: OUT OF OXYGEN! Taking suffocation damage.");
                    nextWarningTime = Time.time + 2f;
                }
            }
            else
            {
                if (Time.time >= nextWarningTime)
                {
                    Debug.LogWarning("DANGER: NO GAS MASK EQUIPPED! Oxygen depleting.");
                    nextWarningTime = Time.time + 2f;
                }
            }
        }

        // --- BLEED LOGIC ---
        if (Time.time < bleedEndTime && currentHealth > 0 && !isDead)
        {
            currentHealth -= bleedDps * Time.deltaTime;
            if (currentHealth <= 0)
            {
                currentHealth = 0;
                Die("Bled out from Mutant attack!");
            }
        }
        // -------------------

        // Update UI Bars
        if (healthBar != null) healthBar.value = currentHealth / maxHealth;
        if (oxygenBar != null) oxygenBar.value = currentOxygen / maxOxygen;

        // Handle Breathing Audio
        if (currentOxygen < lowOxygenThreshold && !isDead)
        {
            if (breathingSource != null && !breathingSource.isPlaying && heavyBreathingClip != null)
            {
                breathingSource.clip = heavyBreathingClip;
                breathingSource.Play();
            }
            
            if (breathingSource != null)
            {
                // Increase volume as oxygen gets lower
                float oxygenPercent = currentOxygen / lowOxygenThreshold;
                breathingSource.volume = Mathf.Lerp(0.8f, 0.2f, oxygenPercent);
            }
        }
        else
        {
            if (breathingSource != null && breathingSource.isPlaying)
            {
                breathingSource.Stop();
            }
        }

        UpdateEKG(); // Update EKG every frame
    }

    public void EquipMask(GasMaskType type)
    {
        activeMaskType = type;
        maskDurability = 100f;
        Debug.Log($"Equipped {type} Gas Mask. Protection: {(type == GasMaskType.Advanced ? advancedMaskProtection : basicMaskProtection)*100}%");
        EquipVisualMask();
    }

    private void EquipVisualMask()
    {
        EquipmentManager em = GetComponent<EquipmentManager>();
        if (em != null)
        {
            em.EquipItem("gasmask", EquipmentSlot.Face);
        }
    }

    private void UnequipVisualMask()
    {
        EquipmentManager em = GetComponent<EquipmentManager>();
        if (em != null)
        {
            em.UnequipSlot(EquipmentSlot.Face);
        }
    }

    public void TakeDamage(float amount, string reason = "Toxicity! Health reached 0.")
    {
        if (currentHealth <= 0) return;

        currentHealth -= amount;

        // Play Hit Sound
        if (sfxSource != null && damageHitClip != null && amount > 0.1f)
        {
            sfxSource.clip = damageHitClip;
            sfxSource.Play();
        }

        if (currentHealth <= 0)
        {
            currentHealth = 0; // Bug Fix: clamp to 0 to prevent negative health display
            Die(reason);
        }
    }

    private float bleedEndTime = 0f;
    private float bleedDps = 0f;

    public bool IsBleeding => Time.time < bleedEndTime && currentHealth > 0 && !isDead;

    public void ApplyBleed(float dps, float duration = 4f)
    {
        bleedDps = dps;
        bleedEndTime = Time.time + duration;
    }

    // Bug Fix: track isDead to prevent Time.timeScale being set multiple times from double-death
    private bool isDead = false;

    void Die(string reason = "Player Died!")
    {
        if (isDead) return; // Guard against double-death (e.g. oxygen + mimic hit same frame)
        isDead = true;

        Debug.Log($"<color=red>DEATH: {reason}</color>");
        
        // Slow motion effect
        Time.timeScale = 0.5f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        // Mất hết đồ khi chết
        PlayerInventory inv = GetComponent<PlayerInventory>();
        if (inv != null)
        {
            inv.ClearInventoryOnDeath();
        }

        // Disable movement
        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null) controller.enabled = false;

        if (deathPanel != null)
        {
            deathPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (deathCamera != null)
            {
                deathCamera.gameObject.SetActive(true);
                // Position camera slightly further and higher for dramatic view
                deathCamera.transform.position = transform.position + new Vector3(2, 4, -5);
                deathCamera.transform.LookAt(transform.position + Vector3.up);
                
                Camera pc = GetComponentInChildren<Camera>();
                if (pc != null) pc.enabled = false;
            }
        }

        // Tự động về WaitingRoom sau 4 giây thực tế
        StartCoroutine(LoadWaitingRoomOnDeath(4f));
    }

    private System.Collections.IEnumerator LoadWaitingRoomOnDeath(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("WaitingRoom");
    }

    public void Respawn()
    {
        isDead = false; // Allow dying again after respawn

        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;

        // Reset Stats
        currentHealth = maxHealth;
        currentOxygen = maxOxygen;
        
        // Re-enable movement
        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null) controller.enabled = true;

        // Bug Fix: use assigned spawnPoint if available, else fallback to (0,2,0)
        transform.position = (spawnPoint != null) ? spawnPoint.position : Vector3.up * 2f;
        
        // Remove mask on death
        activeMaskType = GasMaskType.None;
        UnequipVisualMask();

        if (deathPanel != null)
        {
            deathPanel.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (deathCamera != null)
            {
                deathCamera.gameObject.SetActive(false);
                Camera pc = GetComponentInChildren<Camera>();
                if (pc != null) pc.enabled = true;
            }
        }
    }

    // ─── NEW SCI-FI HUD IMPLEMENTATION (ULTIMATE WOW EDITION) ───────────────

    private Texture2D _hudBgTex;
    private Texture2D _hpTexGreen;
    private Texture2D _hpTexYellow;
    private Texture2D _hpTexRed;
    private Texture2D _oxyTex;
    private Texture2D _scanlineTex;
    private float _noiseOffset = 0f;

    // EKG Data
    private float[] _ekgHistory = new float[120];
    private float _ekgTimer = 0f;

    private void InitHUD()
    {
        if (_hudBgTex != null) return;
        _hudBgTex = MakeTex(new Color(0.02f, 0.05f, 0.08f, 0.85f)); 
        _hpTexGreen = MakeTex(new Color(0.1f, 1f, 0.4f, 1f));
        _hpTexYellow = MakeTex(new Color(1f, 0.9f, 0.1f, 1f));
        _hpTexRed = MakeTex(new Color(1f, 0.1f, 0.2f, 1f));
        _oxyTex = MakeTex(new Color(0f, 0.8f, 1f, 1f));
        
        _scanlineTex = new Texture2D(2, 4);
        for(int y=0; y<4; y++) 
            for(int x=0; x<2; x++) 
                _scanlineTex.SetPixel(x, y, (y % 2 == 0) ? new Color(0,0,0,0.1f) : new Color(1,1,1,0.02f));
        _scanlineTex.Apply();
    }

    Texture2D MakeTex(Color col)
    {
        Texture2D t = new Texture2D(1, 1);
        t.SetPixel(0, 0, col);
        t.Apply();
        return t;
    }

    void UpdateEKG()
    {
        // Shift history left
        for (int i = 0; i < _ekgHistory.Length - 1; i++) _ekgHistory[i] = _ekgHistory[i+1];

        // Simulate BPM based on health and running state
        float bpm = (currentHealth / maxHealth) * 50f + 40f; // 40 - 90 BPM
        if (currentOxygen < lowOxygenThreshold) bpm += 40f; // Panic heart rate
        
        float speed = bpm / 60f * 1.5f; 
        _ekgTimer += Time.deltaTime * speed;
        
        float val = 0f;
        float beatPhase = _ekgTimer % 1f;
        
        // Synthesize ECG waveform (P, Q, R, S, T waves)
        if (beatPhase < 0.1f) val = Mathf.Sin(beatPhase * 10f * Mathf.PI) * 0.2f; 
        else if (beatPhase < 0.12f) val = -0.3f; 
        else if (beatPhase < 0.16f) val = 1f; // Massive R spike
        else if (beatPhase < 0.20f) val = -0.4f; 
        else if (beatPhase < 0.35f) val = Mathf.Sin((beatPhase-0.20f) * 6.6f * Mathf.PI) * 0.25f; 
        
        // Micro noise
        val += Random.Range(-0.02f, 0.02f);
        if (isDead) val = Random.Range(-0.01f, 0.01f); // Flatline

        _ekgHistory[_ekgHistory.Length - 1] = val;
    }

    void OnGUI()
    {
        if (IsSpawned && !IsOwner) return;
        if (isDead) return;

        // Tắt HUD hiển thị trong các scene menu để không đè lên RoomInfoPanel
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName == "StartGame" || sceneName == "Menu") 
            return;

        InitHUD();
        _noiseOffset += Time.deltaTime * 15f;

        Matrix4x4 oldMatrix = GUI.matrix;
        
        float panelW = 460f;
        float panelH = 180f;
        
        // Dịch sang phải thêm 40px (từ 100f thành 140f)
        float panelX = 140f; 
        
        // Nhỏ lại thêm 20% (từ 0.8 xuống 0.6)
        float scale = 0.6f;
        
        // Dịch xuống 10px (từ 80f xuống 70f cách đáy)
        float panelY = Screen.height - (panelH * scale) - 70f;

        // Thu nhỏ UI xuống 60% tại vị trí mới
        GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), new Vector2(panelX, panelY));
        
        // Draw Main Hologram Box
        GUI.color = new Color(1f, 1f, 1f, 0.9f);
        GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), _hudBgTex);
        
        // Scanlines
        GUI.color = new Color(1f, 1f, 1f, 0.3f);
        GUI.DrawTextureWithTexCoords(new Rect(panelX, panelY, panelW, panelH), _scanlineTex, new Rect(0, _noiseOffset * 0.1f, panelW, panelH / 4f));
        GUI.color = Color.white;
        
        // Tech Corners
        DrawTechCorners(panelX, panelY, panelW, panelH, new Color(0.2f, 0.8f, 1f, 0.8f));

        // 1. HUGE HP NUMBER & BPM
        Color hpColor = GetHealthColor();
        GUIStyle hugeNum = new GUIStyle();
        hugeNum.fontSize = 54;
        hugeNum.fontStyle = FontStyle.Bold;
        hugeNum.normal.textColor = hpColor;
        hugeNum.alignment = TextAnchor.MiddleCenter;
        
        float hpBoxW = 100f;
        GUI.Label(new Rect(panelX + 15f, panelY + 25f, hpBoxW, 60f), Mathf.CeilToInt(currentHealth).ToString("000"), hugeNum);
        
        GUIStyle bpmStyle = new GUIStyle();
        bpmStyle.fontSize = 12;
        bpmStyle.fontStyle = FontStyle.Bold;
        bpmStyle.normal.textColor = new Color(0.6f, 0.8f, 1f);
        bpmStyle.alignment = TextAnchor.MiddleCenter;
        
        float currentBpm = (currentHealth / maxHealth) * 50f + 40f + (currentOxygen < lowOxygenThreshold ? 40f : 0f);
        GUI.Label(new Rect(panelX + 15f, panelY + 90f, hpBoxW, 20f), $"BPM: {currentBpm:F1}", bpmStyle);

        // 2. SEGMENTED BARS (MÁU VÀ OXY)
        float barStartX = panelX + 120f;
        float barWidth = panelW - 140f;
        
        DrawSciFiBar(barStartX, panelY + 30f, barWidth, 14f, "INTEGRITY", currentHealth, maxHealth, GetHealthTex(), currentHealth <= 25f, 30);
        DrawSciFiBar(barStartX, panelY + 75f, barWidth, 14f, "OXYGEN", currentOxygen, maxOxygen, _oxyTex, currentOxygen < lowOxygenThreshold, 30);

        // 3. REAL-TIME EKG GRAPH (ĐỒ THỊ NHỊP TIM)
        DrawEKG(panelX + 15f, panelY + 120f, 180f, 45f);

        // 4. GPS & STATUS INFO
        float infoX = panelX + 210f;
        float infoY = panelY + 125f;
        GUIStyle infoStyle = new GUIStyle();
        infoStyle.fontSize = 12;
        infoStyle.fontStyle = FontStyle.Bold;
        infoStyle.normal.textColor = new Color(0.4f, 0.8f, 1f, 0.8f);

        string gpsCoords = $"POS: X {transform.position.x:F1} Y {transform.position.y:F1} Z {transform.position.z:F1}";
        GUI.Label(new Rect(infoX, infoY, 200f, 20f), gpsCoords, infoStyle);

        if (activeMaskType != GasMaskType.None)
        {
            infoStyle.normal.textColor = new Color(0.1f, 1f, 0.5f);
            GUI.Label(new Rect(infoX, infoY + 20f, 200f, 20f), $"FILTER: {activeMaskType.ToString().ToUpper()} ({Mathf.CeilToInt(maskDurability)}%)", infoStyle);
        }
        else
        {
            float pulse = Mathf.PingPong(Time.time * 8f, 1f);
            infoStyle.normal.textColor = new Color(1f, 0.2f, 0.2f, 0.4f + 0.6f * pulse);
            GUI.Label(new Rect(infoX, infoY + 20f, 200f, 20f), "WARNING: NO FILTER DETECTED", infoStyle);
        }

        GUI.matrix = oldMatrix; 
    }

    Color GetHealthColor()
    {
        float ratio = currentHealth / maxHealth;
        if (ratio > 0.6f) return new Color(0.1f, 1f, 0.4f);
        if (ratio > 0.3f) return new Color(1f, 0.9f, 0.1f);
        return new Color(1f, 0.2f, 0.2f);
    }

    Texture2D GetHealthTex()
    {
        float ratio = currentHealth / maxHealth;
        if (ratio > 0.6f) return _hpTexGreen;
        if (ratio > 0.3f) return _hpTexYellow;
        return _hpTexRed;
    }

    void DrawEKG(float x, float y, float w, float h)
    {
        // Draw grid
        GUI.color = new Color(0.1f, 0.5f, 0.8f, 0.15f);
        for(int i = 0; i <= 5; i++) {
            GUI.DrawTexture(new Rect(x, y + (h/5)*i, w, 1f), Texture2D.whiteTexture);
        }
        for(int i = 0; i <= 10; i++) {
            GUI.DrawTexture(new Rect(x + (w/10)*i, y, 1f, h), Texture2D.whiteTexture);
        }

        GUI.color = GetHealthColor();
        float stepX = w / (_ekgHistory.Length - 1);
        
        for(int i = 0; i < _ekgHistory.Length - 1; i++)
        {
            float y1 = y + (h / 2f) - (_ekgHistory[i] * h * 0.4f);
            float y2 = y + (h / 2f) - (_ekgHistory[i+1] * h * 0.4f);
            
            float minY = Mathf.Min(y1, y2);
            float maxY = Mathf.Max(y1, y2);
            float thick = Mathf.Max(1.5f, maxY - minY);
            
            // Fading trail effect
            GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, (float)i / _ekgHistory.Length);
            GUI.DrawTexture(new Rect(x + i*stepX, minY, stepX * 1.5f, thick), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
        
        // Scanline passing over EKG
        float sweep = (Time.time * 100f) % w;
        GUI.color = new Color(1f, 1f, 1f, 0.5f);
        GUI.DrawTexture(new Rect(x + sweep, y, 2f, h), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void DrawSciFiBar(float x, float y, float width, float height, string label, float current, float max, Texture2D fillTex, bool alert, int totalSegments)
    {
        GUIStyle labelStyle = new GUIStyle();
        labelStyle.fontSize = 11;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.normal.textColor = new Color(0.5f, 0.8f, 1f);
        GUI.Label(new Rect(x, y - 16f, 150f, 20f), label, labelStyle);
        
        GUIStyle valStyle = new GUIStyle(labelStyle);
        valStyle.alignment = TextAnchor.MiddleRight;
        valStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x + width - 100f, y - 16f, 100f, 20f), $"{Mathf.CeilToInt((current / max) * 100)}%", valStyle);

        // Tech Background
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (alert)
        {
            float pulse = Mathf.PingPong(Time.time * 10f, 1f);
            GUI.color = new Color(1f, 0.3f + 0.7f * pulse, 0.3f + 0.7f * pulse);
        }

        float gap = 2f;
        float segWidth = (width - (gap * (totalSegments - 1))) / totalSegments;
        int activeSegs = Mathf.CeilToInt((current / max) * totalSegments);

        for (int i = 0; i < totalSegments; i++)
        {
            float segX = x + (i * (segWidth + gap));
            if (i < activeSegs)
            {
                GUI.DrawTexture(new Rect(segX, y, segWidth, height), fillTex);
            }
            else
            {
                GUI.color = new Color(1f, 1f, 1f, 0.1f);
                GUI.DrawTexture(new Rect(segX, y, segWidth, height), Texture2D.whiteTexture);
                if (alert) GUI.color = new Color(1f, 0.3f, 0.3f);
                else GUI.color = Color.white;
            }
        }
        
        GUI.color = Color.white;
    }

    void DrawTechCorners(float x, float y, float w, float h, Color color)
    {
        GUI.color = color;
        float len = 12f;
        float thick = 2f;
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
}
