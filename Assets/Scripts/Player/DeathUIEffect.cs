using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DeathUIEffect : MonoBehaviour
{
    public TextMeshProUGUI deathText;
    public Image backgroundImage;
    public float initialPulseSpeed = 6f;
    public float minPulseSpeed = 1f;
    public float slowingRate = 0.5f;
    
    private float timer;
    private float currentPulseSpeed;

    void OnEnable()
    {
        timer = 0;
        currentPulseSpeed = initialPulseSpeed;
        if (deathText != null) deathText.alpha = 0;
        if (backgroundImage != null) backgroundImage.color = new Color(0, 0, 0, 0);
    }

    void Update()
    {
        // Bug Fix: Time.deltaTime is halved by the 0.5x timeScale set in PlayerSurvival.Die().
        // Use unscaledDeltaTime so the death heartbeat effect plays at full intended speed.
        timer += Time.unscaledDeltaTime;
        
        // Slow down pulse over time
        currentPulseSpeed = Mathf.Max(minPulseSpeed, initialPulseSpeed - (timer * slowingRate));
        
        // Fade in background
        if (backgroundImage != null)
        {
            float bgAlpha = Mathf.Clamp01(timer / 3f) * 0.85f;
            backgroundImage.color = new Color(0, 0, 0, bgAlpha);
        }

        // Heartbeat pulse logic (thump-thump)
        if (deathText != null)
        {
            float pulse = Mathf.Sin(timer * currentPulseSpeed);
            // Double beat simulation
            float heartBeat = Mathf.Pow(Mathf.Max(0, pulse), 4) + Mathf.Pow(Mathf.Max(0, Mathf.Sin(timer * currentPulseSpeed + 0.5f)), 8);
            
            float alpha = Mathf.Lerp(0.2f, 1f, heartBeat);
            deathText.alpha = alpha;
            
            float scale = 1f + (heartBeat * 0.15f);
            deathText.transform.localScale = Vector3.one * scale;
            
            // Subtle color shift to dark red
            deathText.color = Color.Lerp(Color.red, new Color(0.3f, 0, 0), 1f - heartBeat);
        }
    }
}
