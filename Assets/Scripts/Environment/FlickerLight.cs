using UnityEngine;

public class FlickerLight : MonoBehaviour
{
    public Light targetLight;
    public float minIntensity = 0.2f;
    public float maxIntensity = 1.5f;
    public float flickerSpeed = 0.1f;
    
    [Header("Blackout Settings")]
    public float blackoutChance = 0.05f;
    public float minBlackoutTime = 0.1f;
    public float maxBlackoutTime = 0.5f;

    private float _baseIntensity;
    private float _nextFlicker;
    private bool _isBlackout = false;

    void Start()
    {
        if (targetLight == null) targetLight = GetComponent<Light>();
        if (targetLight != null) _baseIntensity = targetLight.intensity;
    }

    void Update()
    {
        if (targetLight == null) return;

        if (_isBlackout) return;

        if (Time.time > _nextFlicker)
        {
            if (Random.value < blackoutChance)
            {
                StartCoroutine(BlackoutRoutine());
            }
            else
            {
                targetLight.intensity = Random.Range(minIntensity, maxIntensity);
                _nextFlicker = Time.time + flickerSpeed;
            }
        }
    }

    private System.Collections.IEnumerator BlackoutRoutine()
    {
        _isBlackout = true;
        targetLight.intensity = 0;
        yield return new WaitForSeconds(Random.Range(minBlackoutTime, maxBlackoutTime));
        targetLight.intensity = _baseIntensity;
        _isBlackout = false;
    }
}
