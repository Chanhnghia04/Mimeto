using UnityEngine;

/// <summary>
/// Handles player interaction (pressing E).
///
/// Primary:  Physics.Raycast from camera centre.
/// Fallback: Physics.OverlapSphere so the player can still pick up items
///           even if the raycast misses (e.g. large-scale FBX colliders
///           whose visual doesn't perfectly match the BoxCollider centre).
/// </summary>
public class InteractionSystem : MonoBehaviour
{
    public float interactionRange = 3f;
    [Header("Animation Settings")]
    [Tooltip("Thời gian chờ (giây) kể từ lúc bắt đầu animation đến lúc tay chạm vào đồ")]
    public float pickupDelay = 0.5f; 
    private bool isPickingUp = false;

    private Camera playerCamera;
    private Animator animator;

    // Fix: cache UI references in Start() instead of calling FindAnyObjectByType every frame
    private InventoryUI _inventoryUI;
    private CraftingUI  _craftingUI;
    private ChestUI          _chestUI;
    private ShopStation      _shopStation;
    private ScrapSellStation _sellStation;
    private BlackjackStation _blackjackStation;

    private UnityEngine.InputSystem.InputAction _interactAction;
    
    void Start()
    {
        playerCamera = GetComponentInChildren<Camera>();
        
        // Auto-find fallback if no camera found in children
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera != null)
            {
                Debug.LogWarning($"[InteractionSystem] No Camera child found on {gameObject.name}. Fallback to Camera.main: {playerCamera.name}");
            }
        }

        animator     = GetComponentInChildren<Animator>();

        // Cache Input Actions via PlayerInput
        var playerInput = GetComponentInParent<UnityEngine.InputSystem.PlayerInput>();
        if (playerInput != null)
        {
            _interactAction = playerInput.actions.FindAction("Interact");
        }
        else
        {
            _interactAction = UnityEngine.InputSystem.InputSystem.actions.FindAction("Interact");
        }

        // Cache UI references once at startup
        _inventoryUI = FindAnyObjectByType<InventoryUI>();
        _craftingUI  = FindAnyObjectByType<CraftingUI>();
        _chestUI     = FindAnyObjectByType<ChestUI>();
        _shopStation = FindAnyObjectByType<ShopStation>();
        _sellStation = FindAnyObjectByType<ScrapSellStation>();
        _blackjackStation = FindAnyObjectByType<BlackjackStation>();

        // Bug Fix: disable gracefully if no Camera is found
        if (playerCamera == null)
        {
            Debug.LogError("InteractionSystem: No Camera child found on " + gameObject.name + " AND Camera.main is null. Interaction disabled.");
            enabled = false;
        }
    }

    void Update()
    {
        // Bug Fix: block interaction while any UI panel is open or already picking up
        if (_interactAction != null && _interactAction.WasPressedThisFrame() && !IsUIOpen() && !isPickingUp)
        {
            Interact();
        }
    }

    private bool IsUIOpen()
    {
        if (_inventoryUI != null && _inventoryUI.inventoryPanel != null && _inventoryUI.inventoryPanel.activeSelf) return true;
        if (_craftingUI  != null && _craftingUI.craftingPanel  != null && _craftingUI.craftingPanel.activeSelf)  return true;
        if (_chestUI     != null && _chestUI.chestPanel        != null && _chestUI.chestPanel.activeSelf)        return true;
        if (_shopStation != null && _shopStation.isOpen)                                                         return true;
        if (_sellStation != null && _sellStation.isOpen)                                                         return true;
        if (_blackjackStation != null && _blackjackStation.isOpen)                                               return true;
        return false;
    }

    // ── Interaction logic ─────────────────────────────────────────────────────

    void Interact()
    {
        // ── PRIMARY: Raycast from camera centre ───────────────────────────────
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        // Draw debug ray in Scene view (visible when Gizmos are on)
        Debug.DrawRay(ray.origin, ray.direction * interactionRange, Color.cyan, 1f);

        if (Physics.Raycast(ray, out RaycastHit hit, interactionRange))
        {
            Debug.Log($"[Interaction] Raycast hit: '{hit.collider.name}'");

            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                TriggerInteract(interactable);
                return; // Success — no need for fallback
            }

            Debug.Log($"[Interaction] '{hit.collider.name}' has no IInteractable.");
        }
        else
        {
            Debug.Log($"[Interaction] Raycast missed. Trying OverlapSphere fallback...");
        }

        // ── FALLBACK: OverlapSphere around the player ─────────────────────────
        // Handles cases where the FBX model visual doesn't align with its collider.
        TryOverlapPickup();
    }

    /// <summary>
    /// Finds the CLOSEST IInteractable within interactionRange using OverlapSphere,
    /// ignoring the player's own colliders.
    /// </summary>
    private void TryOverlapPickup()
    {
        Collider[] nearby = Physics.OverlapSphere(
            playerCamera.transform.position,
            interactionRange);

        IInteractable closest    = null;
        float         closestDot = -1f; // Use dot product to prefer items the camera faces

        foreach (Collider col in nearby)
        {
            // Skip player's own colliders
            if (col.transform.IsChildOf(transform) || col.transform == transform) continue;

            IInteractable interactable = col.GetComponentInParent<IInteractable>();
            if (interactable == null) continue;

            // Prefer the interactable most directly in front of the camera
            Vector3 toItem = (col.bounds.center - playerCamera.transform.position).normalized;
            float   dot    = Vector3.Dot(playerCamera.transform.forward, toItem);

            if (dot > closestDot)
            {
                closestDot = dot;
                closest    = interactable;
            }
        }

        if (closest != null)
        {
            Debug.Log($"[Interaction] OverlapSphere found interactable: '{(closest as MonoBehaviour)?.name}'");
            TriggerInteract(closest);
        }
        else
        {
            Debug.Log($"[Interaction] Nothing interactable within {interactionRange}m. " +
                      "Check that items have BoxCollider on root and are in range.");
        }
    }

    private void TriggerInteract(IInteractable interactable)
    {
        // Chỉ phát animation Lifting khi nhặt ScrapItem thật sự.
        // Workbench / Chest / Extraction mở UI → KHÔNG cần animation nhặt,
        // tránh Animator bị kẹt trong state "Lifting" sau khi đóng UI.
        bool isPickupAnim = interactable is ScrapItem;

        if (isPickupAnim && animator != null)
            animator.SetTrigger("Lifting");

        StartCoroutine(DelayedPickup(interactable, isPickupAnim));
    }

    private System.Collections.IEnumerator DelayedPickup(IInteractable interactable, bool isPickupAnim = true)
    {
        isPickingUp = true;

        // Nếu không phải nhặt item (Workbench/Chest/Extraction) → gọi ngay,
        // không đợi animation để tránh Animator bị kẹt.
        if (!isPickupAnim)
        {
            yield return null; // 1 frame để đảm bảo UI trước đó đã ổn định
            try
            {
                if (interactable != null && (interactable as MonoBehaviour) != null)
                {
                    interactable.Interact(gameObject);
                    GetComponentInParent<PlayerController>()?.ForceUIRefresh();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Interaction] Error during Interact: {e.Message}\n{e.StackTrace}");
            }
            isPickingUp = false;
            yield break;
        }

        // ── Pickup thật: đợi animation Lifting ───────────────────────────────
        // Đợi 2 frames để Animator kịp nhận lệnh SetTrigger và chuyển State
        yield return null;
        yield return null;

        float animLength = pickupDelay; // Dùng pickupDelay làm mức an toàn dự phòng

        if (animator != null)
        {
            // Lấy độ dài của animation hiện tại (hoặc animation đang chuẩn bị chuyển sang)
            AnimatorStateInfo stateInfo = animator.IsInTransition(0) 
                ? animator.GetNextAnimatorStateInfo(0) 
                : animator.GetCurrentAnimatorStateInfo(0);
                
            if (stateInfo.length > 0)
            {
                animLength = stateInfo.length;
            }
        }

        // Tự động tính toán: Thời điểm tay chạm đất thường là 50% tiến trình của animation
        float timeToTouchGround = animLength * 0.45f;
        float timeToStandUp = animLength * 0.55f;

        // Đợi đến lúc tay chạm vật
        yield return new WaitForSeconds(timeToTouchGround);
        
        // Thực hiện nhặt (vật phẩm biến mất)
        try
        {
            if (interactable != null && (interactable as MonoBehaviour) != null)
            {
                interactable.Interact(gameObject);
                GetComponentInParent<PlayerController>()?.ForceUIRefresh();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Interaction] Error during Interact: {e.Message}\n{e.StackTrace}");
        }
        
        // Đợi nhân vật đứng thẳng lên hoàn toàn thì mới cho nhặt tiếp
        yield return new WaitForSeconds(timeToStandUp); 
        isPickingUp = false;
    }


    // ── Scene view gizmo ──────────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        if (playerCamera == null) return;

        // Raycast direction
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(playerCamera.transform.position,
                       playerCamera.transform.forward * interactionRange);

        // OverlapSphere range
        Gizmos.color = new Color(0f, 1f, 1f, 0.1f);
        Gizmos.DrawSphere(playerCamera.transform.position, interactionRange);
    }
}

public interface IInteractable
{
    void Interact(GameObject interactor);
}