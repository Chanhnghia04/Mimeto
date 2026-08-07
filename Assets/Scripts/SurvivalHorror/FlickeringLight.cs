using UnityEngine;

public class FlickeringLight : MonoBehaviour
{
    [Header("Flicker Settings")]
    public Light targetLight;
    public float flickerSpeed = 5f;
    public float baseIntensity = 1f;
    private float noiseOffset;
    private bool isBroken = false;

    private void Start()
    {
        if (targetLight == null) targetLight = GetComponent<Light>();
        noiseOffset = Random.Range(0f, 1000f);
    }

    private void Update()
    {
        if (isBroken || targetLight == null) return;
        float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, noiseOffset);
        targetLight.intensity = noise * baseIntensity;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isBroken)
        {
            bool isMonster = other.GetComponent<MutantAI>() != null || other.GetComponentInParent<MutantAI>() != null;
//                              other.GetComponent<MimicAI>() != null || other.GetComponentInParent<MimicAI>() != null;
            if (isMonster)
            {
                isBroken = true;
                if (targetLight != null) targetLight.intensity = 0f;
            }
        }
    }
}
