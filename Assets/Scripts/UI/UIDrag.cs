using UnityEngine;
using UnityEngine.EventSystems;

public class UIDrag : MonoBehaviour, IDragHandler, IBeginDragHandler
{
    private RectTransform rectTransform;
    private Canvas canvas;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        
        // Tăng kích cỡ lên 250%
        rectTransform.localScale = new Vector3(2.5f, 2.5f, 2.5f);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Khóa cứng
        // rectTransform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Khóa cứng
        // if (canvas == null) canvas = GetComponentInParent<Canvas>();
        // if (canvas != null)
        //     rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }
}
