using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class UITweenAnimator : MonoBehaviour
{
    public float animationDuration = 0.3f;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    
    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        StartCoroutine(AnimateIn());
    }

    private IEnumerator AnimateIn()
    {
        float timer = 0;
        
        // Start state
        rectTransform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        canvasGroup.alpha = 0f;
        
        // Bounce effect
        while (timer < animationDuration)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / animationDuration;
            
            // Ease out back calculation
            float s = 1.70158f;
            normalizedTime -= 1;
            float scale = (normalizedTime * normalizedTime * ((s + 1) * normalizedTime + s) + 1);
            
            // Apply bounds just in case
            if (scale > 1.2f) scale = 1.2f;
            if (scale < 0f) scale = 0f;

            rectTransform.localScale = new Vector3(scale, scale, scale);
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / (animationDuration * 0.5f));
            
            yield return null;
        }
        
        rectTransform.localScale = Vector3.one;
        canvasGroup.alpha = 1f;
    }
}
