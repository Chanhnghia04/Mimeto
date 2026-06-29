using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class UIJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Scaling")]
    public float hoverScale = 1.05f;
    public float downScale = 0.95f;
    public float animationSpeed = 10f;

    [Header("Glow")]
    public bool useGlow = true;
    public Color glowColor = new Color(0, 0.83f, 1f, 0.5f); // Cold Cyan

    private Vector3 _originalScale;
    private Vector3 _targetScale;
    private Image _image;
    private Color _originalColor;
    private Outline _outline;

    void Awake()
    {
        _originalScale = transform.localScale;
        _targetScale = _originalScale;
        _image = GetComponent<Image>();
        if (_image != null) _originalColor = _image.color;

        if (useGlow)
        {
            _outline = gameObject.GetComponent<Outline>();
            if (_outline == null) _outline = gameObject.AddComponent<Outline>();
            _outline.effectColor = new Color(glowColor.r, glowColor.g, glowColor.b, 0);
            _outline.effectDistance = new Vector2(2, 2);
            _outline.enabled = false;
        }
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.deltaTime * animationSpeed);
        
        if (useGlow && _outline != null)
        {
            float targetAlpha = (_targetScale.x > _originalScale.x) ? glowColor.a : 0f;
            Color c = _outline.effectColor;
            c.a = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * animationSpeed);
            _outline.effectColor = c;
            _outline.enabled = c.a > 0.01f;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _targetScale = _originalScale * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _targetScale = _originalScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _targetScale = _originalScale * downScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _targetScale = _originalScale * hoverScale;
    }
}
