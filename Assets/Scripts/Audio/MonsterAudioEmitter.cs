using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

namespace Mimeto.Audio
{
    /// <summary>
    /// Handles 3D spatial audio for monsters (Mutant, Exiler).
    /// Generates footstep, growl, attack, chase breathing, and death sounds.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class MonsterAudioEmitter : NetworkBehaviour
    {
        [Header("Footsteps")]
        [SerializeField] private AudioClip[] footstepClips;
        [SerializeField] private float baseFootstepInterval = 0.5f;
        [SerializeField] private float minFootstepInterval = 0.25f;
        [SerializeField] private float maxFootstepInterval = 0.8f;
        [SerializeField] private float footstepMinDistance = 1f;
        [SerializeField] private float footstepMaxDistance = 25f;

        [Header("Idle Growls")]
        [SerializeField] private AudioClip[] idleGrowlClips;
        [SerializeField] private float minGrowlInterval = 8f;
        [SerializeField] private float maxGrowlInterval = 20f;
        [SerializeField] private float growlMinDistance = 2f;
        [SerializeField] private float growlMaxDistance = 35f;

        [Header("Attacks")]
        [SerializeField] private AudioClip[] attackClips;
        [SerializeField] private float attackMaxDistance = 30f;

        [Header("Chase Breathing")]
        [SerializeField] private AudioClip chaseBreatheClip;
        [SerializeField] private float breatheFadeSpeed = 2f;
        [SerializeField] private float chaseVolume = 0.8f;

        [Header("Death")]
        [SerializeField] private AudioClip[] deathClips;
        [SerializeField] private float deathMaxDistance = 40f;

        // Public state
        public NetworkVariable<bool> isChasing = new NetworkVariable<bool>(false);

        // Components
        private NavMeshAgent agent;

        // Audio Sources
        private AudioSource footstepSource;
        private AudioSource growlSource;
        private AudioSource attackSource;
        private AudioSource chaseSource;
        private AudioSource deathSource;

        // Timers & State
        private float nextFootstepTime;
        private float nextGrowlTime;
        private bool isDead = false;

        private void Start()
        {
            agent = GetComponent<NavMeshAgent>();

            // Initialize AudioSources as child objects
            footstepSource = CreateAudioSource("FootstepAudio", footstepMinDistance, footstepMaxDistance);
            growlSource = CreateAudioSource("GrowlAudio", growlMinDistance, growlMaxDistance);
            attackSource = CreateAudioSource("AttackAudio", 1f, attackMaxDistance);
            
            chaseSource = CreateAudioSource("ChaseAudio", 1f, 25f);
            chaseSource.loop = true;
            chaseSource.volume = 0f;
            if (chaseBreatheClip != null)
            {
                chaseSource.clip = chaseBreatheClip;
                chaseSource.Play(); // Play continuously, control volume for fade in/out
            }

            deathSource = CreateAudioSource("DeathAudio", 1f, deathMaxDistance);

            SetNextGrowlTime();
        }

        private void Update()
        {
            if (isDead) return;

            HandleFootsteps();
            HandleGrowls();
            HandleChaseBreathing();
        }

        /// <summary>
        /// Creates an AudioSource on a new child GameObject.
        /// </summary>
        private AudioSource CreateAudioSource(string name, float minDist, float maxDist)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(transform);
            child.transform.localPosition = Vector3.zero;

            AudioSource source = child.AddComponent<AudioSource>();
            source.spatialBlend = 1f; // Full 3D
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDist;
            source.maxDistance = maxDist;
            source.playOnAwake = false;

            return source;
        }

        private Vector3 lastPosition;

        private void HandleFootsteps()
        {
            if (footstepClips == null || footstepClips.Length == 0) return;

            float currentSpeed = 0f;
            if (agent != null && agent.enabled)
            {
                currentSpeed = agent.velocity.magnitude;
            }
            else
            {
                currentSpeed = (transform.position - lastPosition).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            }
            lastPosition = transform.position;

            // Check if agent is moving
            if (currentSpeed > 0.1f)
            {
                if (Time.time >= nextFootstepTime)
                {
                    PlayRandomClip(footstepSource, footstepClips, 0.85f, 1.15f);

                    // Calculate next footstep interval based on velocity
                    float interval = baseFootstepInterval / (currentSpeed * 0.3f);
                    interval = Mathf.Clamp(interval, minFootstepInterval, maxFootstepInterval);

                    nextFootstepTime = Time.time + interval;
                }
            }
            else
            {
                // Reset timer slightly if standing still so it plays immediately on move
                if (Time.time >= nextFootstepTime)
                {
                    nextFootstepTime = Time.time;
                }
            }
        }

        private void HandleGrowls()
        {
            if (idleGrowlClips == null || idleGrowlClips.Length == 0) return;

            if (Time.time >= nextGrowlTime)
            {
                PlayRandomClip(growlSource, idleGrowlClips, 0.9f, 1.1f, 0.8f, 1f);
                SetNextGrowlTime();
            }
        }

        private void HandleChaseBreathing()
        {
            if (chaseBreatheClip == null) return;

            float targetVolume = isChasing.Value ? chaseVolume : 0f;
            chaseSource.volume = Mathf.MoveTowards(chaseSource.volume, targetVolume, Time.deltaTime * breatheFadeSpeed);
        }

        private void SetNextGrowlTime()
        {
            nextGrowlTime = Time.time + Random.Range(minGrowlInterval, maxGrowlInterval);
        }

        /// <summary>
        /// Call this method from AI script when the monster attacks.
        /// </summary>
        [ClientRpc]
        public void PlayAttackSoundClientRpc()
        {
            if (isDead || attackClips == null || attackClips.Length == 0) return;
            PlayRandomClip(attackSource, attackClips, 0.9f, 1.1f);
        }

        /// <summary>
        /// Call this method from AI script when the monster dies.
        /// </summary>
        [ClientRpc]
        public void PlayDeathSoundClientRpc()
        {
            if (isDead) return;
            isDead = true;

            // Stop all other looping or currently playing sounds
            footstepSource.Stop();
            growlSource.Stop();
            attackSource.Stop();
            
            // Fade out chase instantly
            chaseSource.Stop();

            if (deathClips != null && deathClips.Length > 0)
            {
                AudioClip clip = deathClips[Random.Range(0, deathClips.Length)];
                float volume = Random.Range(0.9f, 1.1f); // Just an example volume variation
                AudioSource.PlayClipAtPoint(clip, transform.position, volume);
            }
        }

        /// <summary>
        /// Plays a random clip from the array on the given AudioSource.
        /// </summary>
        private void PlayRandomClip(AudioSource source, AudioClip[] clips, float minPitch = 1f, float maxPitch = 1f, float minVol = 1f, float maxVol = 1f)
        {
            if (clips == null || clips.Length == 0) return;

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            source.pitch = Random.Range(minPitch, maxPitch);
            source.volume = Random.Range(minVol, maxVol);
            source.PlayOneShot(clip);
        }
    }
}
