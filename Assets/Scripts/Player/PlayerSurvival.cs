using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public enum GasMaskType { None, Basic, Advanced }

public class PlayerSurvival : NetworkBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public NetworkVariable<float> netHealth = new NetworkVariable<float>();
    public float currentHealth { get { return netHealth.Value; } set { if (IsServer) netHealth.Value = value; } }
    public float toxicDamagePerSecond = 5f;


    public NetworkVariable<bool> isGhost = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private static bool isGameOver = false;
    private static bool showGameOver = false;
    private static bool isWinResult = false;
    private static bool pendingSceneLoad = false;


    public static bool IsGameOverUIOpen()
    {
        return showGameOver;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer) { netHealth.Value = maxHealth; netOxygen.Value = maxOxygen; netStamina.Value = maxStamina; }
        
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName == "Map" || sceneName == "PollutedZone")
        {
            isGameOver = false;
            showGameOver = false;
            pendingSceneLoad = false;
            
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
            }
        }

        isGhost.OnValueChanged += (oldVal, newVal) => { ApplyGhostVisuals(newVal); };
        if (isGhost.Value) ApplyGhostVisuals(true);
        
        netEquippedMask.OnValueChanged += OnEquippedMaskChanged;
        if (netEquippedMask.Value != 0) OnEquippedMaskChanged(0, netEquippedMask.Value);
    }

    [ServerRpc]
    public void SetGhostServerRpc(bool ghost)
    {
        isGhost.Value = ghost;
        if (ghost && IsServer)
        {
            CheckTeamWipe();
        }
    }
    
    [ServerRpc]
    private void BreakMaskServerRpc(int maskType)
    {
        PlayerInventory inv = GetComponent<PlayerInventory>();
        if (inv != null)
        {
            if (maskType == 2 && inv.advancedGasMasks > 0) inv.advancedGasMasks--;
            else if (maskType == 1 && inv.basicGasMasks > 0) inv.basicGasMasks--;
        }
    }
    
    public override void OnNetworkDespawn()
    {
        netEquippedMask.OnValueChanged -= OnEquippedMaskChanged;
        
        if (IsServer && Unity.Netcode.NetworkManager.Singleton != null)
        {
            Unity.Netcode.NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
        }
        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        if (Unity.Netcode.NetworkManager.Singleton != null)
        {
            Unity.Netcode.NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
        }
        base.OnDestroy();
    }
    
    private void OnClientDisconnect(ulong clientId)
    {
        if (IsServer)
        {
            StartCoroutine(CheckTeamWipeDelayed());
        }
    }

    private System.Collections.IEnumerator CheckTeamWipeDelayed()
    {
        yield return null; // Chờ 1 frame để Netcode dọn dẹp xong GameObject của người vừa văng mạng
        CheckTeamWipe();
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
    public NetworkVariable<float> netOxygen = new NetworkVariable<float>();
    public float currentOxygen { get { return netOxygen.Value; } set { if (IsServer) netOxygen.Value = value; } }
    public float oxygenDepletionRate = 0.3333f; // 3s mất 1 oxy
    public float oxygenRestoreRate = 10f; 
    public bool inSafeZone = false;

    [Header("Stamina Settings")]
    public float maxStamina = 100f;
    public NetworkVariable<float> netStamina = new NetworkVariable<float>();
    public float currentStamina 
    { 
        get 
        { 
            var staminaSys = GetComponent<Mimeto.PlayerSystems.StaminaSystem>();
            if (staminaSys != null) return staminaSys.currentStamina;
            return netStamina.Value; 
        } 
        set 
        { 
            if (IsServer) netStamina.Value = value; 
        } 
    }
    public float staminaDepletionRate = 20f; // Chạy 5s là hết lực
    public float staminaRestoreRate = 15f;

    [Header("Mask Settings")]
    public GasMaskType activeMaskType = GasMaskType.None;
    public NetworkVariable<int> netEquippedMask = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public float maskDurability = 0f;
    public float basicMaskProtection = 0.8f; // 80% reduction
    public float advancedMaskProtection = 0.95f; // 95% reduction
    
    [Header("UI References")]
    public Slider healthBar;
    public Slider oxygenBar;
    public GameObject deathPanel;
    public Camera deathCamera;

    // Basic lasts ~60s, Advanced lasts ~300s
    private float basicDurabilityLoss = 0.555f; // 100/180
    private float advancedDurabilityLoss = 0.111f; // 100/900

    public Transform headTransform;
    private GameObject equippedMaskInstance;

    // Fix: proper timer to avoid Debug.LogWarning spam based on FPS
    private float nextWarningTime = 0f;
    private float accumulatedOxygenDamage = 0f;
    private float accumulatedBleedDamage = 0f;

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

        if (IsSpawned && !IsOwner && !IsServer) return; // CHỈ CẬP NHẬT MÁU/OXY CHO NHÂN VẬT CỦA MÌNH HOẶC KHI OFFLINE, VÀ SERVER CẬP NHẬT CHO TẤT CẢ

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
            if (IsServer)
            {
                currentOxygen += oxygenRestoreRate * Time.deltaTime;
                if (currentOxygen > maxOxygen) currentOxygen = maxOxygen;
            }
        }
        else if (activeMaskType != GasMaskType.None)
        {
            float loss = (activeMaskType == GasMaskType.Advanced) ? advancedDurabilityLoss : basicDurabilityLoss;
            maskDurability -= loss * Time.deltaTime;
            
            if (maskDurability <= 0)
            {
                maskDurability = 0;
                Debug.Log($"<color=red>Gas Mask ({activeMaskType}) broke!</color>");
                
                if (IsOwner)
                {
                    BreakMaskServerRpc(netEquippedMask.Value);
                    netEquippedMask.Value = 0; // Tự động gọi callback tháo mặt nạ
                }
            }
        }
        else
        {
            // Deplete oxygen only when NOT wearing a mask
            if (IsServer)
            {
                currentOxygen -= oxygenDepletionRate * Time.deltaTime;
                if (currentOxygen <= 0) currentOxygen = 0;
            }
            if (currentOxygen <= 0)
            {
                float tickDamage = 5f * Time.deltaTime;
                accumulatedOxygenDamage += tickDamage;
                // if (!IsServer) currentHealth -= tickDamage; // Removed client prediction
                
                if (accumulatedOxygenDamage >= 1f || currentHealth <= 0)
                {
                    if (IsServer) ApplyDamageLogic(accumulatedOxygenDamage, "Suffocated!");
                    else TakeDamageServerRpc(accumulatedOxygenDamage, "Suffocated!");
                    accumulatedOxygenDamage = 0f;
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

        // --- STAMINA LOGIC (FACADE PATTERN) ---
        PlayerController pc = GetComponent<PlayerController>();
        var staminaSys = GetComponent<Mimeto.PlayerSystems.StaminaSystem>();
        
        if (staminaSys != null)
        {
            if (pc != null && (pc.isSprinting || pc.netIsSprinting.Value)) staminaSys.DepleteStamina(Time.deltaTime);
            else staminaSys.RestoreStamina(Time.deltaTime);
            
            // Đồng bộ ngược lại biến cũ để UI hiện tại không bị vỡ (Step 3)
            if (IsServer) currentStamina = staminaSys.currentStamina; 
            
            if (pc != null) 
            {
                if (!staminaSys.HasStamina()) pc.isExhausted = true;
                else if (staminaSys.currentStamina >= staminaSys.maxStamina * 0.2f) pc.isExhausted = false;
            }
        }
        else
        {
            // Fallback: Logic cũ phòng trường hợp bạn chưa kéo component vào Prefab
            if (pc != null && (pc.isSprinting || pc.netIsSprinting.Value))
            {
                if (IsServer)
                {
                    currentStamina -= staminaDepletionRate * Time.deltaTime;
                    if (currentStamina <= 0) currentStamina = 0;
                }
                if (currentStamina <= 0) 
                {
                    pc.isExhausted = true;
                }
            }
            else
            {
                if (IsServer)
                {
                    currentStamina += staminaRestoreRate * Time.deltaTime;
                    if (currentStamina > maxStamina) currentStamina = maxStamina;
                }
                
                if (currentStamina >= maxStamina * 0.2f && pc != null)
                {
                    pc.isExhausted = false;
                }
            }
        }

        // --- BLEED LOGIC ---
        if (Time.time < bleedEndTime && currentHealth > 0 && !isDead)
        {
            float tickDamage = bleedDps * Time.deltaTime;
            accumulatedBleedDamage += tickDamage;
            // if (!IsServer) currentHealth -= tickDamage; // Removed client prediction
            
            if (accumulatedBleedDamage >= 1f || currentHealth <= 0)
            {
                if (IsServer) ApplyDamageLogic(accumulatedBleedDamage, "Bled out from Mutant attack!");
                else TakeDamageServerRpc(accumulatedBleedDamage, "Bled out from Mutant attack!");
                accumulatedBleedDamage = 0f;
            }
        }
        // -------------------

        if (IsOwner)
        {
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
    
    
            // Feed stats to Horror Audio Director for adaptive music
            if (HorrorAudioDirector.Instance != null)
            {
                HorrorAudioDirector.Instance.SetPlayerStats(currentHealth, currentOxygen, currentStamina, IsBleeding);
            }


        }
    }

    public void EquipMask(GasMaskType type)
    {
        activeMaskType = type;
        if (maskDurability <= 0f) maskDurability = 100f; // Chỉ khôi phục 100% nếu cái trước đó đã hỏng (bắt đầu dùng cái mới)
        Debug.Log($"Equipped {type} Gas Mask. Protection: {(type == GasMaskType.Advanced ? advancedMaskProtection : basicMaskProtection)*100}%");
        EquipVisualMask();
    }

    public void ToggleGasMask(string specificMask = null)
    {
        if (!IsOwner) return;

        if (netEquippedMask.Value != 0)
        {
            // Tháo ra (không cần cộng lại vào túi đồ nữa vì lúc đeo không trừ)
            netEquippedMask.Value = 0;
        }
        else
        {
            // Đeo vào
            PlayerInventory inv = GetComponent<PlayerInventory>();
            if (inv != null)
            {
                bool wantAdv = (specificMask == "adv_gasmask");
                bool wantBasic = (specificMask == "basic_gasmask");
                
                // Nếu không chỉ định cụ thể, ưu tiên loại đang có
                if (string.IsNullOrEmpty(specificMask))
                {
                    if (inv.advancedGasMasks > 0) wantAdv = true;
                    else if (inv.basicGasMasks > 0) wantBasic = true;
                }

                // Chỉ đeo khi túi đồ có sở hữu, không trừ đi (để nó luôn nằm trong túi/hotbar)
                if (wantAdv && inv.advancedGasMasks > 0)
                {
                    netEquippedMask.Value = 2; // Advanced
                }
                else if (wantBasic && inv.basicGasMasks > 0)
                {
                    netEquippedMask.Value = 1; // Basic
                }
                else
                {
                    Debug.Log("Không có mặt nạ nào phù hợp trong túi đồ!");
                }
            }
        }
    }

    private void OnEquippedMaskChanged(int previous, int current)
    {
        if (current == 0)
        {
            activeMaskType = GasMaskType.None;
            UnequipVisualMask();
        }
        else if (current == 1)
        {
            activeMaskType = GasMaskType.Basic;
            if (IsOwner && maskDurability <= 0f) maskDurability = 100f;
            EquipVisualMask();
        }
        else if (current == 2)
        {
            activeMaskType = GasMaskType.Advanced;
            if (IsOwner && maskDurability <= 0f) maskDurability = 100f;
            EquipVisualMask();
        }
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

    private void CheckTeamWipe()
    {
        if (isGameOver) return;
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName != "Map" && sceneName != "PollutedZone") return;

        PlayerSurvival[] players = FindObjectsByType<PlayerSurvival>();
        if (players.Length == 0) return;

        bool allDead = true;
        foreach (var p in players)
        {
            // Bỏ qua object rác chưa kịp xóa
            if (p == null || !p.IsSpawned || !p.gameObject.activeInHierarchy) continue;

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
            if (IsServer) StartCoroutine(AutoTransitionToWaitingRoom(10f));
        }
    }

    [ClientRpc]
    public void ShowGameOverClientRpc(bool win)
    {
        showGameOver = true;
        isWinResult = win;
        
        if (win)
        {
            Time.timeScale = 0.1f; // Slow motion lúc win cho ngầu
            if (IsOwner)
            {
                var inv = GetComponent<PlayerInventory>();
                if (inv != null) inv.hasEscaped = true;
            }
        }
        
        if (!win && IsOwner)
        {
            GetComponent<PlayerInventory>().ClearInventoryOnDeath();
        }
    }

    [ServerRpc]
    public void DeclareVictoryServerRpc()
    {
        if (isGameOver) return;
        isGameOver = true;
        ShowGameOverClientRpc(true);
        StartCoroutine(AutoTransitionToWaitingRoom(10f));
    }

    private System.Collections.IEnumerator AutoTransitionToWaitingRoom(float delayRealtime)
    {
        yield return new WaitForSecondsRealtime(delayRealtime);
        if (IsServer && !pendingSceneLoad)
        {
            pendingSceneLoad = true;
        }
    }

    public void TakeDamage(float amount, string reason = "Toxicity! Health reached 0.")
    {
        if (currentHealth <= 0) return;

        if (IsServer)
        {
            // Trừ máu trực tiếp trên Server và đồng bộ xuống mọi Client
            ApplyDamageLogic(amount, reason);
        }
        else if (IsOwner)
        {
            // Nếu Client tự mất máu (ngạt thở, té ngã), yêu cầu Server trừ máu
            TakeDamageServerRpc(amount, reason);
        }
    }

    [ServerRpc]
    private void TakeDamageServerRpc(float amount, string reason)
    {
        if (currentHealth <= 0) return;
        ApplyDamageLogic(amount, reason);
    }

    private void ApplyDamageLogic(float amount, string reason)
    {
        var healthSys = GetComponent<Mimeto.PlayerSystems.HealthSystem>();
        if (healthSys != null)
        {
            // Chuyển giao quyền lực cho HealthSystem
            healthSys.TakeDamage(amount);
            
            // Đồng bộ lại biến cũ để UI không bị vỡ (Step 3)
            currentHealth = healthSys.currentHealth.Value;
        }
        else
        {
            // Logic cũ (Fallback)
            currentHealth -= amount;
            if (currentHealth <= 0)
            {
                currentHealth = 0;
            }
        }
        
        UpdateHealthClientRpc(currentHealth, amount, reason);
        
        // Host (Server) tự gọi hàm Die hoặc phát âm thanh vì ClientRpc có thể không chạy trên Host nếu logic bọc sai
        if (IsOwner)
        {
            PlayHitSound(amount);
            if (currentHealth <= 0) Die(reason);
        }
    }

    [ClientRpc]
    public void UpdateHealthClientRpc(float newHealth, float amount, string reason)
    {
        // currentHealth setter only updates netHealth on Server, so it does nothing on Client.
        // We must use newHealth for immediate logic.
        
        if (IsOwner)
        {
            PlayHitSound(amount);
            if (newHealth <= 0)
            {
                Die(reason);
            }
        }
    }

    private void PlayHitSound(float amount)
    {
        if (sfxSource != null && damageHitClip != null && amount > 0.1f)
        {
            sfxSource.clip = damageHitClip;
            sfxSource.Play();
        }
    }

    public void Heal(float amount)
    {
        if (currentHealth <= 0 || isDead) return;
        
        // currentHealth = Mathf.Min(currentHealth + amount, maxHealth); // Removed client prediction
        
        if (IsServer)
        {
            ApplyHealLogic(amount);
        }
        else if (IsOwner)
        {
            HealServerRpc(amount);
        }
    }

    [ServerRpc]
    private void HealServerRpc(float amount)
    {
        if (currentHealth <= 0 || isDead) return;
        ApplyHealLogic(amount);
    }
    
    [ServerRpc]
    public void ApplyAntidoteServerRpc(ServerRpcParams rpcParams = default)
    {
        // Placeholder for Parasite cure logic
        // For now, it could heal or just remove status effects.
        Debug.Log($"[Survival] Player {OwnerClientId} was cured by an Antidote!");
    }

    [ServerRpc]
    public void UpdateHealthServerRpc(float newHealth)
    {
        var healthSys = GetComponent<Mimeto.PlayerSystems.HealthSystem>();
        if (healthSys != null)
        {
            healthSys.currentHealth.Value = newHealth;
        }
        currentHealth = newHealth;
        UpdateHealthClientRpc(newHealth);
    }

    private void ApplyHealLogic(float amount)
    {
        var healthSys = GetComponent<Mimeto.PlayerSystems.HealthSystem>();
        if (healthSys != null)
        {
            healthSys.Heal(amount);
            currentHealth = healthSys.currentHealth.Value;
        }
        else
        {
            currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        }
        
        UpdateHealthClientRpc(currentHealth);
    }

    [ClientRpc]
    public void UpdateHealthClientRpc(float newHealth)
    {
        if (!IsServer)
        {
            currentHealth = newHealth;
        }
    }
//
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
            
            // GetComponent<PlayerInventory>().DropAllItemsOnDeath();

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

    [ServerRpc]
    public void NotifyTeamWipeCheckServerRpc()
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

        // Ẩn thanh HUD khi đang mở Tủ Đồ, Cheat hoặc các UI khác để tránh đè lên nhau
        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null && pc.IsUIOpen())
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

        // 1. HUGE HP NUMBER
        Color hpColor = GetHealthColor();
        GUIStyle hugeNum = new GUIStyle();
        hugeNum.fontSize = 54;
        hugeNum.fontStyle = FontStyle.Bold;
        hugeNum.normal.textColor = hpColor;
        hugeNum.alignment = TextAnchor.MiddleCenter;
        
        float hpBoxW = 100f;
        GUI.Label(new Rect(panelX + 15f, panelY + 25f, hpBoxW, 60f), Mathf.CeilToInt(currentHealth).ToString("000"), hugeNum);
        
        // 2. SEGMENTED BARS (MÁU, OXY, STAMINA)
        float barStartX = panelX + 120f;
        float barWidth = panelW - 140f;
        
        DrawSciFiBar(barStartX, panelY + 20f, barWidth, 14f, "INTEGRITY", currentHealth, maxHealth, GetHealthTex(), currentHealth <= 25f, 30);
        DrawSciFiBar(barStartX, panelY + 60f, barWidth, 14f, "OXYGEN", currentOxygen, maxOxygen, _oxyTex, currentOxygen < lowOxygenThreshold, 30);
        DrawSciFiBar(barStartX, panelY + 100f, barWidth, 14f, "STAMINA", currentStamina, maxStamina, _stamTex, currentStamina <= 15f, 30);

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
 
