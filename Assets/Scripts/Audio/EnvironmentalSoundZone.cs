using System.Collections;
using UnityEngine;

namespace Mimeto.Audio
{
    [RequireComponent(typeof(Collider))]
    public class EnvironmentalSoundZone : MonoBehaviour
    {
        [Header("Ambient Loop")]
        [SerializeField] private AudioClip ambientLoop;
        [SerializeField] private float fadeTime = 1.5f;
        [SerializeField] private float maxVolume = 0.6f;

        [Header("Random One-shot Sounds")]
        [SerializeField] private AudioClip[] randomClips;
        [SerializeField] private float minInterval = 10f;
        [SerializeField] private float maxInterval = 30f;

        [Header("Reverb Zone")]
        [SerializeField] private bool addReverbZone = false;
        [SerializeField] private AudioReverbPreset reverbPreset = AudioReverbPreset.Cave;

        [Header("Danger Zone Modifier")]
        [SerializeField, Range(0f, 1f)] private float threatModifier = 0f;

        /// <summary>
        /// Gets the threat modifier for this zone so HorrorAudioDirector can read it.
        /// </summary>
        public float ZoneThreatModifier => threatModifier;

        private AudioSource ambientSource;
        private Collider zoneCollider;
        private Coroutine fadeCoroutine;
        private Coroutine randomSoundCoroutine;
        private bool isPlayerInZone = false;

        private void Awake()
        {
            zoneCollider = GetComponent<Collider>();
            zoneCollider.isTrigger = true;

            // Setup Ambient Loop AudioSource
            if (ambientLoop != null)
            {
                ambientSource = gameObject.AddComponent<AudioSource>();
                ambientSource.clip = ambientLoop;
                ambientSource.loop = true;
                ambientSource.spatialBlend = 1f; // 3D Sound
                ambientSource.volume = 0f;
                ambientSource.playOnAwake = false;
            }

            // Setup Reverb Zone
            if (addReverbZone)
            {
                AudioReverbZone reverbZone = gameObject.AddComponent<AudioReverbZone>();
                reverbZone.reverbPreset = reverbPreset;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                isPlayerInZone = true;

                // Handle Ambient Sound Fade In
                if (ambientSource != null)
                {
                    if (!ambientSource.isPlaying)
                    {
                        ambientSource.Play();
                    }

                    if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
                    fadeCoroutine = StartCoroutine(FadeAmbientVolume(maxVolume, fadeTime));
                }

                // Handle Random Sounds
                if (randomClips != null && randomClips.Length > 0)
                {
                    if (randomSoundCoroutine != null) StopCoroutine(randomSoundCoroutine);
                    randomSoundCoroutine = StartCoroutine(PlayRandomSoundsRoutine());
                }

                // Notify HorrorAudioDirector (adjust method name based on actual implementation)
                // HorrorAudioDirector.Instance?.SetThreatModifier(ZoneThreatModifier);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                isPlayerInZone = false;

                // Handle Ambient Sound Fade Out
                if (ambientSource != null)
                {
                    if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
                    fadeCoroutine = StartCoroutine(FadeAmbientVolume(0f, fadeTime, stopWhenZero: true));
                }

                // Handle Random Sounds
                if (randomSoundCoroutine != null)
                {
                    StopCoroutine(randomSoundCoroutine);
                    randomSoundCoroutine = null;
                }

                // Notify HorrorAudioDirector to reset modifier
                // HorrorAudioDirector.Instance?.SetThreatModifier(0f);
            }
        }

        private IEnumerator FadeAmbientVolume(float targetVolume, float duration, bool stopWhenZero = false)
        {
            float startVolume = ambientSource.volume;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                ambientSource.volume = Mathf.Lerp(startVolume, targetVolume, timer / duration);
                yield return null;
            }

            ambientSource.volume = targetVolume;

            if (stopWhenZero && ambientSource.volume <= 0f)
            {
                ambientSource.Stop();
            }
        }

        private IEnumerator PlayRandomSoundsRoutine()
        {
            while (isPlayerInZone)
            {
                float waitTime = Random.Range(minInterval, maxInterval);
                yield return new WaitForSeconds(waitTime);

                if (isPlayerInZone && randomClips.Length > 0)
                {
                    AudioClip clip = randomClips[Random.Range(0, randomClips.Length)];
                    Vector3 randomPoint = GetRandomPointInCollider();

                    // Play 3D one-shot using HorrorAudioDirector
                    if (HorrorAudioDirector.Instance != null)
                    {
                        HorrorAudioDirector.Instance.PlayOneShot3D(clip, randomPoint, 1f);
                    }
                }
            }
        }

        private Vector3 GetRandomPointInCollider()
        {
            Bounds bounds = zoneCollider.bounds;
            return new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z)
            );
        }
    }
}
