using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlayerStatusEffect : MonoBehaviour
{
    [Header("Health & Oxygen")]
    [Range(0, 100)] public float currentHealth = 100f;
    [Range(0, 100)] public float currentOxygen = 100f;
    public float walkSpeed = 5f;
    public bool hasGasMask = false;

    [Header("Post Processing")]
    public Volume globalVolume;
    private ChromaticAberration chromaticAberration;
    private FilmGrain filmGrain;
    private Vignette vignette;

    [Header("Glitch (Monster Proximity)")]
    public float monsterDetectRadius = 10f;

    [Header("Vignette (Low Stats)")]
    public AudioSource heartbeatAudio;
    public float pulseSpeed = 5f;
    
    private static readonly Collider[] _monsterBuffer = new Collider[20];

    private void Start()
    {
        if (globalVolume == null)
        {
            GameObject volumeGo = GameObject.Find("Global Toxic Volume");
            if (volumeGo != null) globalVolume = volumeGo.GetComponent<Volume>();
        }

        if (globalVolume != null && globalVolume.profile != null)
        {
            if (!globalVolume.profile.TryGet(out chromaticAberration))
                chromaticAberration = globalVolume.profile.Add<ChromaticAberration>();
            if (!globalVolume.profile.TryGet(out filmGrain))
                filmGrain = globalVolume.profile.Add<FilmGrain>();
            if (!globalVolume.profile.TryGet(out vignette))
                vignette = globalVolume.profile.Add<Vignette>();
                
            chromaticAberration.active = true;
            filmGrain.active = true;
            vignette.active = true;
        }

        if (heartbeatAudio == null)
        {
            heartbeatAudio = gameObject.AddComponent<AudioSource>();
            AudioClip clip = Resources.Load<AudioClip>("SFX/Heartbeat") ?? Resources.Load<AudioClip>("Heartbeat");
            heartbeatAudio.clip = clip;
            heartbeatAudio.loop = false;
        }
    }

    private void Update()
    {
        HandleMonsterGlitch();
        HandleLowStatsVignette();
    }

    private void HandleMonsterGlitch()
    {
        if (chromaticAberration == null || filmGrain == null) return;

        int count = Physics.OverlapSphereNonAlloc(transform.position, monsterDetectRadius, _monsterBuffer);
        float closestDistance = monsterDetectRadius;
        bool monsterNearby = false;

        for (int i = 0; i < count; i++)
        {
            Collider col = _monsterBuffer[i];
//             MimicAI mimic = col.GetComponentInParent<MimicAI>();
            MutantAI mutant = col.GetComponentInParent<MutantAI>();
// bool isMimic = false;
//             bool isMimic = mimic != null && mimic.currentState != MimicAI.MimicState.HumanForm;
            bool isMutant = mutant != null;
//             if (isMimic || isMutant)
            {
                monsterNearby = true;
                float dist = Vector3.Distance(transform.position, col.transform.position);
                if (dist < closestDistance) closestDistance = dist;
            }
        }

        if (monsterNearby)
        {
            float intensity = 1f - (closestDistance / monsterDetectRadius);
            chromaticAberration.intensity.value = Mathf.Lerp(chromaticAberration.intensity.value, intensity, Time.deltaTime * 5f);
            filmGrain.intensity.value = Mathf.Lerp(filmGrain.intensity.value, intensity, Time.deltaTime * 5f);
        }
        else
        {
            chromaticAberration.intensity.value = Mathf.Lerp(chromaticAberration.intensity.value, 0f, Time.deltaTime * 2f);
            filmGrain.intensity.value = Mathf.Lerp(filmGrain.intensity.value, 0f, Time.deltaTime * 2f);
        }
    }

    private void HandleLowStatsVignette()
    {
        if (vignette == null) return;

        bool isCritical = currentOxygen < 20f || currentHealth < 30f;

        if (isCritical)
        {
            vignette.color.value = Color.red;
            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;
            float targetIntensity = Mathf.Lerp(0.3f, 0.6f, pulse);
            vignette.intensity.value = Mathf.Lerp(vignette.intensity.value, targetIntensity, Time.deltaTime * 10f);

            if (pulse > 0.95f && heartbeatAudio != null && !heartbeatAudio.isPlaying)
            {
                heartbeatAudio.Play();
            }
        }
        else
        {
            vignette.color.value = Color.black;
            vignette.intensity.value = Mathf.Lerp(vignette.intensity.value, 0f, Time.deltaTime * 2f);
        }
    }
}
