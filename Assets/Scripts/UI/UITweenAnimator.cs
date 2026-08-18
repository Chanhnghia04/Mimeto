using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class UITweenAnimator : MonoBehaviour
{
    public float animationDuration = 0.3f;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector3 _originalScale = Vector3.one;
    
    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _originalScale = rectTransform.localScale;
    }

    void OnEnable()
    {
        StartCoroutine(AnimateIn());
    }

    private IEnumerator AnimateIn()
    {
        float timer = 0;
        
        // Start state
        rectTransform.localScale = new Vector3(0.1f, 0.1f, 0.1f) * _originalScale.x;
        canvasGroup.alpha = 0f;
        
        // Bounce effect
        while (timer < animationDuration)
        {
            timer += Time.unscaledDeltaTime;
            float normalizedTime = timer / animationDuration;
            
            // Ease out back calculation
            float s = 1.70158f;
            normalizedTime -= 1;
            float scaleMultiplier = (normalizedTime * normalizedTime * ((s + 1) * normalizedTime + s) + 1);
            
            // Apply bounds just in case
            if (scaleMultiplier > 1.2f) scaleMultiplier = 1.2f;
            if (scaleMultiplier < 0f) scaleMultiplier = 0f;

            rectTransform.localScale = _originalScale * scaleMultiplier;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / (animationDuration * 0.5f));
            
            yield return null;
        }
        
        rectTransform.localScale = _originalScale;
        canvasGroup.alpha = 1f;
    }
}
