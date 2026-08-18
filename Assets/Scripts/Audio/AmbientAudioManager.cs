using UnityEngine;
using System.Collections.Generic;

public class AmbientAudioManager : MonoBehaviour
{
    [Header("BGM")]
    public AudioSource bgmSource;
    public AudioClip backgroundMusic;
    public float bgmVolume = 0.5f;

    [Header("Random Stingers")]
    public AudioSource stingerSource;
    public AudioClip[] stingerClips; // Monster howls, thunder, etc.
    public float minStingerDelay = 20f;
    public float maxStingerDelay = 60f;

    private float _nextStingerTime;

    void Start()
    {
        if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
        if (stingerSource == null) stingerSource = gameObject.AddComponent<AudioSource>();

        bgmSource.loop = true;
        bgmSource.clip = backgroundMusic;
        bgmSource.volume = bgmVolume;
        bgmSource.spatialBlend = 0f; // 2D BGM
        bgmSource.Play();

        stingerSource.spatialBlend = 0.5f; // Partially spatialized
        
        ScheduleNextStinger();
    }

    void Update()
    {
        if (Time.time >= _nextStingerTime)
        {
            PlayRandomStinger();
            ScheduleNextStinger();
        }
    }

    private void PlayRandomStinger()
    {
        if (stingerClips != null && stingerClips.Length > 0)
        {
            AudioClip clip = stingerClips[Random.Range(0, stingerClips.Length)];
            stingerSource.pitch = Random.Range(0.8f, 1.2f);
            stingerSource.PlayOneShot(clip, Random.Range(0.3f, 0.7f));
        }
    }

    private void ScheduleNextStinger()
    {
        _nextStingerTime = Time.time + Random.Range(minStingerDelay, maxStingerDelay);
    }
}
