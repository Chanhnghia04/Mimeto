using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private Vector3 originalScale;
    public float hoverScale = 1.05f;
    public float clickScale = 0.95f;
    public float transitionSpeed = 15f;
    
    private Vector3 targetScale;
    private Button btn;

    void Awake()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
        btn = GetComponent<Button>();
    }

    void Update()
    {
        if (transform.localScale != targetScale)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * transitionSpeed);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (btn != null && !btn.interactable) return;
        targetScale = originalScale * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (btn != null && !btn.interactable) return;
        targetScale = originalScale * clickScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (btn != null && !btn.interactable) return;
        targetScale = originalScale * hoverScale;
    }
    
    void OnDisable()
    {
        transform.localScale = originalScale;
        targetScale = originalScale;
    }
}