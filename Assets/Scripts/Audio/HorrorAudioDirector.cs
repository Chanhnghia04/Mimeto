using UnityEngine;
using System.Collections.Generic;

public class HorrorAudioDirector : MonoBehaviour
{
    public static HorrorAudioDirector Instance { get; private set; }

    [Header("BGM Layers")]
    [SerializeField] private AudioClip calmClip;
    [SerializeField] private AudioClip tensionClip;
    [SerializeField] private AudioClip chaseClip;
    [SerializeField] private float layerTransitionSpeed = 1f;

    [Header("Heartbeat System")]
    [SerializeField] private AudioClip heartbeatClip;
    [SerializeField] private float heartbeatFadeSpeed = 2f;

    [Header("Breathing System")]
    [SerializeField] private AudioClip heavyBreathingClip;
    [SerializeField] private AudioClip lowHpBreathingClip;
    [SerializeField] private float breathFadeSpeed = 2f;

    [Header("Jump Scare")]
    [SerializeField] private AudioClip[] jumpScareClips;
    [SerializeField] private float jumpScareCooldown = 15f;
    [SerializeField] private LayerMask monsterLayer;

    // AudioSources
    private AudioSource calmSource;
    private AudioSource tensionSource;
    private AudioSource chaseSource;
    private AudioSource heartbeatSource;
    private AudioSource breathingSource;

    // Environmental Audio Pool
    private List<AudioSource> sfxPool = new List<AudioSource>();
    private const int PoolSize = 5;

    // State
    public float ThreatLevel { get; private set; }
    private float hp = 100f, oxy = 100f, stamina = 100f;
    private bool isBeingChased = false;

    private float jumpScareTimer = 0f;
    private bool wasMonsterNearby = false;

    // Non-alloc overlap sphere
    private Collider[] monsterColliders = new Collider[5];

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitializeAudioSources();
        InitializeSFXPool();
    }

    private void InitializeAudioSources()
    {
        calmSource = CreateAudioSource("Calm BGM", calmClip, true);
        tensionSource = CreateAudioSource("Tension BGM", tensionClip, true);
        chaseSource = CreateAudioSource("Chase BGM", chaseClip, true);

        heartbeatSource = CreateAudioSource("Heartbeat", heartbeatClip, true);
        breathingSource = CreateAudioSource("Breathing", null, true);

        calmSource.volume = 1f;
        tensionSource.volume = 0f;
        chaseSource.volume = 0f;
        heartbeatSource.volume = 0f;
        breathingSource.volume = 0f;

        calmSource.Play();
        if (tensionClip != null) tensionSource.Play();
        if (chaseClip != null) chaseSource.Play();
    }

    private AudioSource CreateAudioSource(string sourceName, AudioClip clip, bool loop)
    {
        GameObject go = new GameObject(sourceName);
        go.transform.SetParent(transform);
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = loop;
        source.spatialBlend = 0f; // 2D, always clear
        source.playOnAwake = false;
        return source;
    }

    private void InitializeSFXPool()
    {
        for (int i = 0; i < PoolSize; i++)
        {
            GameObject go = new GameObject($"SFX Source {i}");
            go.transform.SetParent(transform);
            AudioSource source = go.AddComponent<AudioSource>();
            source.spatialBlend = 1f; // 3D for environmental sfx
            source.playOnAwake = false;
            sfxPool.Add(source);
        }
    }

    /// <summary>
    /// Update stats from PlayerSurvival every frame.
    /// </summary>
    public void SetPlayerStats(float playerHp, float playerOxy, float playerStamina, bool chased)
    {
        this.hp = playerHp;
        this.oxy = playerOxy;
        this.stamina = playerStamina;
        this.isBeingChased = chased;
    }

    private void Update()
    {
        UpdateMonsterDetectionAndThreat();
        UpdateBGMLayers();
        UpdateHeartbeat();
        UpdateBreathing();

        if (jumpScareTimer > 0)
        {
            jumpScareTimer -= Time.deltaTime;
        }
    }

    private void UpdateMonsterDetectionAndThreat()
    {
        // Find nearest monster
        int monsterCount = Physics.OverlapSphereNonAlloc(transform.position, 20f, monsterColliders, monsterLayer);
        float nearestMonsterDist = float.MaxValue;
        bool isMonsterNearby = false;

        for (int i = 0; i < monsterCount; i++)
        {
            float dist = Vector3.Distance(transform.position, monsterColliders[i].transform.position);
            if (dist < nearestMonsterDist)
            {
                nearestMonsterDist = dist;
            }
        }

        if (monsterCount > 0 && nearestMonsterDist < 8f)
        {
            isMonsterNearby = true;
            CheckJumpScare();
        }

        wasMonsterNearby = isMonsterNearby;

        // Calculate Threat Level
        float calculatedThreat = 0f;

        if (isBeingChased)
        {
            calculatedThreat = 1f;
        }
        else if (monsterCount > 0)
        {
            // Threat inversely proportional to distance (max distance 20m)
            calculatedThreat = Mathf.Clamp01(1f - (nearestMonsterDist / 20f));
        }

        // Add threat if stats are extremely low
        if (hp <= 30f || oxy <= 20f)
        {
            calculatedThreat = Mathf.Max(calculatedThreat, 0.4f);
        }

        // Smooth transition
        ThreatLevel = Mathf.MoveTowards(ThreatLevel, calculatedThreat, Time.deltaTime * layerTransitionSpeed);
    }

    private void UpdateBGMLayers()
    {
        // Calm: 1 - threatLevel
        float targetCalm = 1f - ThreatLevel;
        calmSource.volume = Mathf.Lerp(calmSource.volume, targetCalm, Time.deltaTime * layerTransitionSpeed);

        // Tension: volume = threatLevel when < 0.6
        float targetTension = ThreatLevel < 0.6f ? ThreatLevel : Mathf.Max(0f, 1f - (ThreatLevel - 0.6f) * 2.5f);
        tensionSource.volume = Mathf.Lerp(tensionSource.volume, targetTension, Time.deltaTime * layerTransitionSpeed);

        // Chase: volume = threatLevel when > 0.6
        float targetChase = ThreatLevel > 0.6f ? ThreatLevel : 0f;
        chaseSource.volume = Mathf.Lerp(chaseSource.volume, targetChase, Time.deltaTime * layerTransitionSpeed);
    }

    private void UpdateHeartbeat()
    {
        // Plays when HP < 30, Oxy < 20, or monster is extremely close (< 5m which translates to high threat)
        bool shouldPlayHeartbeat = (hp < 30f || oxy < 20f || ThreatLevel > 0.75f);

        if (shouldPlayHeartbeat)
        {
            if (!heartbeatSource.isPlaying && heartbeatClip != null)
            {
                heartbeatSource.Play();
            }

            float targetVolume = 1f;
            heartbeatSource.volume = Mathf.Lerp(heartbeatSource.volume, targetVolume, Time.deltaTime * heartbeatFadeSpeed);

            // Pitch increases as HP decreases below 30
            float pitchLerp = Mathf.Clamp01(1f - (hp / 30f));
            heartbeatSource.pitch = Mathf.Lerp(0.8f, 1.5f, pitchLerp);
        }
        else
        {
            heartbeatSource.volume = Mathf.Lerp(heartbeatSource.volume, 0f, Time.deltaTime * heartbeatFadeSpeed);
            if (heartbeatSource.volume <= 0.01f && heartbeatSource.isPlaying)
            {
                heartbeatSource.Stop();
            }
        }
    }

    private void UpdateBreathing()
    {
        bool needsLowHpBreathing = (hp < 20f);
        bool needsHeavyBreathing = (stamina < 30f);

        AudioClip targetClip = null;
        if (needsLowHpBreathing)
        {
            targetClip = lowHpBreathingClip;
        }
        else if (needsHeavyBreathing)
        {
            targetClip = heavyBreathingClip;
        }

        if (targetClip != null)
        {
            if (breathingSource.clip != targetClip || !breathingSource.isPlaying)
            {
                breathingSource.clip = targetClip;
                breathingSource.Play();
            }

            breathingSource.volume = Mathf.Lerp(breathingSource.volume, 1f, Time.deltaTime * breathFadeSpeed);
        }
        else
        {
            breathingSource.volume = Mathf.Lerp(breathingSource.volume, 0f, Time.deltaTime * breathFadeSpeed);
            if (breathingSource.volume <= 0.01f && breathingSource.isPlaying)
            {
                breathingSource.Stop();
            }
        }
    }

    private void CheckJumpScare()
    {
        if (!wasMonsterNearby && jumpScareTimer <= 0f)
        {
            PlayJumpScare();
        }
    }

    private void PlayJumpScare()
    {
        if (jumpScareClips != null && jumpScareClips.Length > 0)
        {
            AudioClip clip = jumpScareClips[Random.Range(0, jumpScareClips.Length)];
            // Play globally as 2D one shot
            PlayOneShot2D(clip, 1f);
            jumpScareTimer = jumpScareCooldown;
        }
    }

    // --- Public API ---

    /// <summary>
    /// Play a 3D environmental sound effect.
    /// </summary>
    public void PlayOneShot3D(AudioClip clip, Vector3 position, float volume)
    {
        if (clip == null) return;

        AudioSource source = GetAvailableSFXSource();
        if (source != null)
        {
            source.transform.position = position;
            source.spatialBlend = 1f; // Ensure 3D
            source.volume = volume;
            source.pitch = 1f;
            source.PlayOneShot(clip);
        }
    }

    /// <summary>
    /// Play a 2D sound effect (e.g. jump scares, UI).
    /// </summary>
    private void PlayOneShot2D(AudioClip clip, float volume)
    {
        if (clip == null) return;

        AudioSource source = GetAvailableSFXSource();
        if (source != null)
        {
            source.transform.position = transform.position;
            source.spatialBlend = 0f; // Ensure 2D
            source.volume = volume;
            source.pitch = 1f;
            source.PlayOneShot(clip);
        }
    }

    private AudioSource GetAvailableSFXSource()
    {
        foreach (var source in sfxPool)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }

        // If all are playing, reuse the first one
        return sfxPool[0];
    }
}
