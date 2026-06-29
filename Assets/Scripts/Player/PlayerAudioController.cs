using UnityEngine;
using System.Collections.Generic;

public class PlayerAudioController : MonoBehaviour
{
    [Header("Footsteps")]
    public AudioSource footstepSource;
    public float baseStepInterval = 0.5f;
    public float sprintStepMultiplier = 0.6f;
    
    [Header("Clips")]
    public AudioClip[] dirtSteps;
    public AudioClip[] woodSteps;
    public AudioClip[] metalSteps;

    [Header("Surface Detection")]
    public LayerMask groundLayer;
    public float rayDistance = 1.5f;

    private CharacterController _controller;
    private PlayerController _playerController;
    private float _stepTimer;

    void Start()
    {
        _controller = GetComponent<CharacterController>();
        _playerController = GetComponent<PlayerController>();
        if (footstepSource == null) footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.playOnAwake = false;
        footstepSource.spatialBlend = 1f;
    }

    void Update()
    {
        if (_controller == null || !_controller.isGrounded) return;

        Vector2 moveInput = new Vector2(_controller.velocity.x, _controller.velocity.z);
        if (moveInput.sqrMagnitude > 0.25f) // Using sqrMagnitude for efficiency (0.5 * 0.5 = 0.25)
        {
            bool isSprinting = false;
            if (_playerController != null)
            {
                isSprinting = _controller.velocity.magnitude > (_playerController.walkSpeed + 1f);
            }

            float currentInterval = isSprinting ? baseStepInterval * sprintStepMultiplier : baseStepInterval;
            _stepTimer += Time.deltaTime;

            if (_stepTimer >= currentInterval)
            {
                PlayFootstep();
                _stepTimer = 0;
            }
        }
        else
        {
            _stepTimer = baseStepInterval;
            if (footstepSource != null && footstepSource.isPlaying)
            {
                footstepSource.Stop();
            }
        }
    }

    private void PlayFootstep()
    {
        AudioClip clip = GetSurfaceClip();
        if (clip != null)
        {
            footstepSource.pitch = Random.Range(0.9f, 1.1f);
            footstepSource.volume = Random.Range(0.4f, 0.6f);
            footstepSource.PlayOneShot(clip);
        }
    }

    private AudioClip GetSurfaceClip()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out hit, rayDistance, groundLayer))
        {
            string tag = hit.collider.tag;
            switch (tag.ToLower())
            {
                case "wood":
                    return woodSteps[Random.Range(0, woodSteps.Length)];
                case "metal":
                    return metalSteps[Random.Range(0, metalSteps.Length)];
                default:
                    return dirtSteps[Random.Range(0, dirtSteps.Length)];
            }
        }
        return dirtSteps.Length > 0 ? dirtSteps[Random.Range(0, dirtSteps.Length)] : null;
    }
}
