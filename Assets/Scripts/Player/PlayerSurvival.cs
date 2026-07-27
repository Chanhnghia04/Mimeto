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

    public NetworkVariable<bool> isGhost = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private static bool isGameOver = false;
    private static bool showGameOver = false;
    private static bool isWinResult = false;
    private static bool pendingSceneLoad = false;
    private static float serverLoadSceneTimer = -1f;

    public static bool IsGameOverUIOpen()
    {
        return showGameOver;
    }

    public override void OnNetworkSpawn()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName == "Map" || sceneName == "PollutedZone")
        {
            isGameOver = false;
            showGameOver = false;
            pendingSceneLoad = false;
        }

        isGhost.OnValueChanged += (oldVal, newVal) => { ApplyGhostVisuals(newVal); };
        if (isGhost.Value) ApplyGhostVisuals(true);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetGhostServerRpc(bool ghost)
    {
        isGhost.Value = ghost;
        if (ghost && IsServer)
        {
            CheckTeamWipe();
        }
    }

    private void ApplyGhostVisuals(bool ghost)
    {
        // Hide/Show Model
        Transform model = transform.Find("Model");
        if (model != null) model.gameObject.SetActive(!ghost);

        // Hide/Show Nametag
        var nametag = GetComponentInChildren<TMPro.TextMeshPro>();
        if (nametag != null) nametag.enabled = !ghost;

        // Disable CharacterController for everyone to avoid invisible collisions
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = !ghost;
        
        // Hide held items
        EquipmentManager em = GetComponent<EquipmentManager>();
        if (em != null)
        {
            if (em.rightHandSocket != null) em.rightHandSocket.gameObject.SetActive(!ghost);
            if (em.leftHandSocket != null) em.leftHandSocket.gameObject.SetActive(!ghost);
            if (em.faceSocket != null) em.faceSocket.gameObject.SetActive(!ghost);
        }
    }
    // Bug Fix: expose a spawn point so Respawn() doesn't always teleport to (0,2,0)
    [Header("Spawn Settings")]
    public Transform spawnPoint;
    
    [Header("Oxygen Settings")]
    public float maxOxygen = 100f;
    public float currentOxygen;
    public float oxygenDepletionRate = 0.3333f; // 3s mất 1 oxy
    public float oxygenRestoreRate = 10f; 
    public bool inSafeZone = false;

    [Header("Stamina Settings")]
    public float maxStamina = 100f;
    public float currentStamina;
    public float staminaDepletionRate = 20f; // Chạy 5s là hết lực
    public float staminaRestoreRate = 15f;

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
        currentStamina = maxStamina;
        
        // Ghi đè chỉ số bằng code để tránh việc Unity Inspector lưu lại giá trị cũ
        oxygenDepletionRate = 0.3333f; 
        
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
        if (IsServer && pendingSceneLoad)
        {
            pendingSceneLoad = false;
            Time.timeScale = 1f;
            Debug.Log("[PlayerSurvival] Loading Waiting scene via NetworkManager...");
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.SceneManager != null)
            {
                Unity.Netcode.NetworkManager.Singleton.SceneManager.LoadScene("Waiting", UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
        }

        if (IsSpawned && !IsOwner) return; // CHỈ CẬP NHẬT MÁU/OXY CHO NHÂN VẬT CỦA MÌNH HOẶC KHI OFFLINE

        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName != "Map" && sceneName != "PollutedZone")
        {
            if (breathingSource != null && breathingSource.isPlaying)
                breathingSource.Stop();
            return; // Không tính toán oxy/máu trong sảnh
        }

        // Reset inSafeZone if it got stuck from previous scenes
        if (Time.frameCount % 60 == 0 && inSafeZone)
        {
            // Optional: fallback check if not colliding with any safe zone
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
                // Trừ máu trực tiếp khi hết Oxy thay vì dùng TakeDamage để tránh spam RPC
                currentHealth -= 5f * Time.deltaTime;
                if (currentHealth <= 0)
                {
                    currentHealth = 0;
                    Die("Suffocated!");
                }
                
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

        // --- STAMINA LOGIC ---
        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null && pc.isSprinting)
        {
            currentStamina -= staminaDepletionRate * Time.deltaTime;
            if (currentStamina <= 0) 
            {
                currentStamina = 0;
                pc.isExhausted = true; // Đánh dấu là đã kiệt sức
            }
        }
        else
        {
            currentStamina += staminaRestoreRate * Time.deltaTime;
            if (currentStamina > maxStamina) currentStamina = maxStamina;
            
            // Hồi lại ít nhất 20% lực mới cho phép chạy tiếp để tránh bị giật khựng
            if (currentStamina >= maxStamina * 0.2f && pc != null)
            {
                pc.isExhausted = false;
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

        // Handle Breathing Audio (Hết oxy hoặc hết thể lực đều thở dốc)
        if ((currentOxygen < lowOxygenThreshold || currentStamina <= 5f) && !isDead)
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

        if (IsServer)
        {
            CheckTeamWipe();
        }
    }

    private void CheckTeamWipe()
    {
        if (isGameOver) return;
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName != "Map" && sceneName != "PollutedZone") return;

        PlayerSurvival[] players = FindObjectsByType<PlayerSurvival>(FindObjectsSortMode.None);
        if (players.Length == 0) return;

        bool allDead = true;
        foreach (var p in players)
        {
            if (!p.isGhost.Value)
            {
                allDead = false;
                break;
            }
        }

        if (allDead)
        {
            isGameOver = true;
            ShowGameOverClientRpc(false);
        }
    }

    [ClientRpc]
    public void ShowGameOverClientRpc(bool win)
    {
        showGameOver = true;
        isWinResult = win;
        
        if (!win && IsOwner)
        {
            GetComponent<PlayerInventory>().ClearInventoryOnDeath();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void DeclareVictoryServerRpc()
    {
        if (isGameOver) return;
        isGameOver = true;
        ShowGameOverClientRpc(true);
    }

    public void TakeDamage(float amount, string reason = "Toxicity! Health reached 0.")
    {
        if (currentHealth <= 0) return;

        // Nếu Server gọi hàm này (do AI đánh trúng)
        if (IsServer && !IsOwner)
        {
            TakeDamageClientRpc(amount, reason);
        }
        else if (IsOwner) // Nếu là Client tự mất oxy/chảy máu
        {
            ApplyDamageLocally(amount, reason);
        }
    }

    [ClientRpc]
    private void TakeDamageClientRpc(float amount, string reason)
    {
        if (IsOwner)
        {
            ApplyDamageLocally(amount, reason);
        }
    }

    private void ApplyDamageLocally(float amount, string reason)
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

        // Báo cho Server biết máu hiện tại để AI (chạy trên Server) không cắn xác chết
        UpdateHealthServerRpc(currentHealth);
    }

    [ServerRpc(RequireOwnership = false)]
    private void UpdateHealthServerRpc(float newHealth)
    {
        currentHealth = newHealth;
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
        
        // Cập nhật lên server trạng thái máu = 0 để chắc chắn
        if (IsOwner) UpdateHealthServerRpc(0f);
        
        // Spectator / Ghost mode setup
        if (IsOwner)
        {
            SetGhostServerRpc(true);
            PlayerController controller = GetComponent<PlayerController>();
            if (controller != null) controller.isGhostMode = true;

            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (deathPanel != null)
            {
                if (sceneName != "Map" && sceneName != "PollutedZone")
                {
                    deathPanel.SetActive(true);
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    // In Map, just hide the panel so they can see while flying as a ghost
                    deathPanel.SetActive(false);
                }

                if (deathCamera != null)
                {
                    deathCamera.gameObject.SetActive(false); // Make sure it's off so we can use player camera
                }
            }

            // Don't auto respawn in Map. They stay as ghosts until team wipe or extract.
            if (sceneName != "Map" && sceneName != "PollutedZone")
            {
                StartCoroutine(RespawnAfterDelay(4f));
            }
            else
            {
                // Báo cho Server kiểm tra xem cả team đã chết hết chưa
                NotifyTeamWipeCheckServerRpc();
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void NotifyTeamWipeCheckServerRpc()
    {
        CheckTeamWipe();
    }

    private System.Collections.IEnumerator RespawnAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Respawn();
    }

    public void Respawn()
    {
        isDead = false; // Allow dying again after respawn
        isGameOver = false;
        showGameOver = false;

        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;
        
        if (IsOwner)
        {
            SetGhostServerRpc(false);
            PlayerController pc = GetComponent<PlayerController>();
            if (pc != null) pc.isGhostMode = false;
        }

        // Reset Stats
        currentHealth = maxHealth;
        currentOxygen = maxOxygen;
        bleedEndTime = 0f;
        bleedDps = 0f;
        
        // Re-enable movement script
        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null) controller.enabled = true;

        // Cố gắng tìm vị trí an toàn trên NavMesh để hồi sinh
        Vector3 safePos = Vector3.up * 2f;
        if (spawnPoint != null) 
        {
            safePos = spawnPoint.position;
        }
        else
        {
            if (UnityEngine.AI.NavMesh.SamplePosition(new Vector3(0, 10f, 0), out UnityEngine.AI.NavMeshHit hit, 50f, UnityEngine.AI.NavMesh.AllAreas))
            {
                safePos = hit.position + Vector3.up * 2f;
            }
        }
        
        if (controller != null && controller.TryGetComponent(out CharacterController cc)) cc.enabled = false;
        transform.position = safePos;
        if (controller != null && controller.TryGetComponent(out CharacterController cc2)) cc2.enabled = true;
        
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
            }
        }
    }

    // ─── NEW SCI-FI HUD IMPLEMENTATION (ULTIMATE WOW EDITION) ───────────────

    private Texture2D _hudBgTex;
    private Texture2D _hpTexGreen;
    private Texture2D _hpTexYellow;
    private Texture2D _hpTexRed;
    private Texture2D _oxyTex;
    private Texture2D _stamTex;
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
        _stamTex = MakeTex(new Color(1f, 0.6f, 0f, 1f)); // Màu cam cho Stamina
        
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
        if (showGameOver && IsOwner)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            GUI.color = new Color(0, 0, 0, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 80;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            
            Color textColor = isWinResult ? new Color(0f, 1f, 0.5f) : new Color(1f, 0.2f, 0.2f);
            
            string title = isWinResult ? "MISSION SUCCESS" : "MISSION FAILED";
            string sub = isWinResult ? "All members survived and escaped successfully!\nSell your loot and prepare for the next mission." 
                                     : "";

            // Draw shadow
            titleStyle.normal.textColor = Color.black;
            GUI.Label(new Rect(4, Screen.height / 2 - 120 + 4, Screen.width, 100), title, titleStyle);
            // Draw text
            titleStyle.normal.textColor = textColor;
            GUI.Label(new Rect(0, Screen.height / 2 - 120, Screen.width, 100), title, titleStyle);
            
            GUIStyle subStyle = new GUIStyle(GUI.skin.label);
            subStyle.fontSize = 28;
            subStyle.alignment = TextAnchor.MiddleCenter;
            
            // Draw shadow
            subStyle.normal.textColor = Color.black;
            GUI.Label(new Rect(2, Screen.height / 2 + 20 + 2, Screen.width, 100), sub, subStyle);
            // Draw text
            subStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(0, Screen.height / 2 + 20, Screen.width, 100), sub, subStyle);
            
            // Draw return button for Host
            if (IsServer)
            {
                GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
                btnStyle.fontSize = 24;
                btnStyle.fontStyle = FontStyle.Bold;
                string btnText = pendingSceneLoad ? "LOADING..." : "CONTINUE";
                
                GUI.enabled = !pendingSceneLoad;
                if (GUI.Button(new Rect(Screen.width / 2 - 150, Screen.height / 2 + 120, 300, 60), btnText, btnStyle))
                {
                    Debug.Log("[PlayerSurvival] Continue button clicked!");
                    pendingSceneLoad = true;
                }
                GUI.enabled = true;
            }
            else
            {
                GUIStyle waitStyle = new GUIStyle(GUI.skin.label);
                waitStyle.fontSize = 20;
                waitStyle.fontStyle = FontStyle.Italic;
                waitStyle.alignment = TextAnchor.MiddleCenter;
                waitStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
                GUI.Label(new Rect(0, Screen.height / 2 + 120, Screen.width, 50), "Waiting for Host to continue...", waitStyle);
            }

            return;
        }

        if (IsSpawned && !IsOwner) return;
        if (isDead) return;

        // Tắt HUD hiển thị trong các scene menu để không đè lên RoomInfoPanel
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName != "Map" && sceneName != "PollutedZone") 
            return;

        InitHUD();
        _noiseOffset += Time.deltaTime * 15f;

        Matrix4x4 oldMatrix = GUI.matrix;
        
        float panelW = 460f;
        float panelH = 210f; // Tăng chiều cao để chứa thanh Stamina
        
        float panelX = 120f; // Dịch sang trái 20px (từ 140f thành 120f) theo yêu cầu
        
        // Tự động Scale phóng to theo màn hình khi kéo giãn cửa sổ
        // Đã tăng 50% kích thước so với trước (từ 0.6f lên 0.9f)
        float scale = 0.9f * (Screen.height / 1080f);
        if (scale < 0.6f) scale = 0.6f;
        
        float panelY = Screen.height - (panelH * scale) - (70f * (Screen.height / 1080f));

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

        // 2. SEGMENTED BARS (MÁU, OXY, STAMINA)
        float barStartX = panelX + 120f;
        float barWidth = panelW - 140f;
        
        DrawSciFiBar(barStartX, panelY + 20f, barWidth, 14f, "INTEGRITY", currentHealth, maxHealth, GetHealthTex(), currentHealth <= 25f, 30);
        DrawSciFiBar(barStartX, panelY + 60f, barWidth, 14f, "OXYGEN", currentOxygen, maxOxygen, _oxyTex, currentOxygen < lowOxygenThreshold, 30);
        DrawSciFiBar(barStartX, panelY + 100f, barWidth, 14f, "STAMINA", currentStamina, maxStamina, _stamTex, currentStamina <= 15f, 30);

        // 3. REAL-TIME EKG GRAPH (ĐỒ THỊ NHỊP TIM)
        DrawEKG(panelX + 15f, panelY + 145f, 180f, 45f);

        // 4. GPS & STATUS INFO
        float infoX = panelX + 210f;
        float infoY = panelY + 150f;
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
