using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryItemDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int slotIndex;
    private InventoryUI inventoryUI;
    private Canvas canvas;
    
    private Transform iconTransform;
    private Transform amountTransform;
    
    private Vector2 originalIconPos;
    private Vector2 originalAmountPos;
    
    private Transform originalParent;
    private CanvasGroup canvasGroup;

    void Start()
    {
        // Find InventoryUI on the player if not set
        if (inventoryUI == null)
        {
            GameObject player = GameObject.Find("Player");
            if (player != null) inventoryUI = player.GetComponent<InventoryUI>();
        }
        
        canvas = GetComponentInParent<Canvas>();
        
        // Find icon and amount (children)
        iconTransform = transform.Find("Icon");
        amountTransform = transform.Find("Amount");
        
        // Add CanvasGroup to the icon for blocking raycasts
        if (iconTransform != null)
        {
            canvasGroup = iconTransform.gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = iconTransform.gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (inventoryUI == null || string.IsNullOrEmpty(inventoryUI.gridSlots[slotIndex].currentItemType) || iconTransform == null)
        {
            eventData.pointerDrag = null;
            return;
        }

        originalIconPos = ((RectTransform)iconTransform).anchoredPosition;
        if (amountTransform != null) originalAmountPos = ((RectTransform)amountTransform).anchoredPosition;
        
        originalParent = transform;
        
        // Move to canvas level
        iconTransform.SetParent(canvas.transform, true);
        if (amountTransform != null) amountTransform.SetParent(iconTransform, true); // Parent text to icon during drag
        
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.6f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas != null && iconTransform != null)
            ((RectTransform)iconTransform).anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1.0f;
        }
        
        if (iconTransform != null)
        {
            // Return to original parent
            if (amountTransform != null) amountTransform.SetParent(originalParent, true);
            iconTransform.SetParent(originalParent, true);
            
            ((RectTransform)iconTransform).anchoredPosition = originalIconPos;
            if (amountTransform != null) ((RectTransform)amountTransform).anchoredPosition = originalAmountPos;
        }

        // Check drop
        GameObject droppedOn = eventData.pointerCurrentRaycast.gameObject;
        if (droppedOn != null && inventoryUI != null)
        {
            InventoryItemDrag targetSlot = droppedOn.GetComponentInParent<InventoryItemDrag>();
            if (targetSlot != null && targetSlot != this)
            {
                inventoryUI.SwapSlots(slotIndex, targetSlot.slotIndex);
            }
        }
    }
}
