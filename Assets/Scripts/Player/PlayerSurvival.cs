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
            sfxSource.PlayOneShot(damageHitClip);
        }

        if (currentHealth <= 0)
{
            currentHealth = 0; // Bug Fix: clamp to 0 to prevent negative health display
            Die(reason);
        }
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
        else
        {
            Respawn();
        }
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
}
