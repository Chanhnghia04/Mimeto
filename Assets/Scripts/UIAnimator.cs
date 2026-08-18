using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(RectTransform))]
public class UIAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Panel Animation")]
    public bool animateOnEnable = true;
    public float panelDuration = 0.5f;
    public Vector2 startOffset = new Vector2(0, -50f);
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Button Hover Effects (If Button)")]
    public bool isButton = false;
    public float hoverScale = 1.1f;
    public float clickScale = 0.95f;
    public float buttonAnimDuration = 0.15f;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 originalPosition;
    private Vector3 originalScale;

    private Coroutine currentAnim;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        originalPosition = rectTransform.anchoredPosition;
        originalScale = rectTransform.localScale;
    }

    private void OnEnable()
    {
        if (animateOnEnable)
        {
            PlayPanelIn();
        }
    }

    public void PlayPanelIn()
    {
        if (currentAnim != null) StopCoroutine(currentAnim);
        
        rectTransform.anchoredPosition = originalPosition + startOffset;
        canvasGroup.alpha = 0f;
        
        currentAnim = StartCoroutine(AnimatePanel(originalPosition, 1f, panelDuration));
    }

    private IEnumerator AnimatePanel(Vector2 targetPos, float targetAlpha, float duration)
    {
        float time = 0;
        Vector2 startPos = rectTransform.anchoredPosition;
        float startAlpha = canvasGroup.alpha;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = time / duration;
            float easedT = easeCurve.Evaluate(t);

            rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, easedT);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, easedT);
            
            yield return null;
        }

        rectTransform.anchoredPosition = targetPos;
        canvasGroup.alpha = targetAlpha;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isButton) return;
        if (currentAnim != null) StopCoroutine(currentAnim);
        currentAnim = StartCoroutine(AnimateScale(originalScale * hoverScale, buttonAnimDuration));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isButton) return;
        if (currentAnim != null) StopCoroutine(currentAnim);
        currentAnim = StartCoroutine(AnimateScale(originalScale, buttonAnimDuration));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isButton) return;
        if (currentAnim != null) StopCoroutine(currentAnim);
        currentAnim = StartCoroutine(ClickSequence());
    }

    private IEnumerator ClickSequence()
    {
        yield return StartCoroutine(AnimateScale(originalScale * clickScale, buttonAnimDuration / 2f));
        yield return StartCoroutine(AnimateScale(originalScale * hoverScale, buttonAnimDuration / 2f));
    }

    private IEnumerator AnimateScale(Vector3 targetScale, float duration)
    {
        float time = 0;
        Vector3 startScale = rectTransform.localScale;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = time / duration;
            rectTransform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }
        rectTransform.localScale = targetScale;
    }
}
