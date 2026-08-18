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
        PlayerController pc = GetComponentInParent<PlayerController>();
        if (pc != null && (!pc.IsOwner || pc.isGhostMode)) return;

        // Bug Fix: block interaction while any UI panel is open or already picking up
        if (_interactAction != null && _interactAction.WasPressedThisFrame() && !IsUIOpen() && !isPickingUp)
        {
            Interact();
        }
    }

    private void RefreshUIReferences()
    {
        if (_inventoryUI == null) _inventoryUI = FindAnyObjectByType<InventoryUI>();
        if (_craftingUI == null)  _craftingUI  = FindAnyObjectByType<CraftingUI>();
        if (_chestUI == null)     _chestUI     = FindAnyObjectByType<ChestUI>();
        if (_shopStation == null) _shopStation = FindAnyObjectByType<ShopStation>();
        if (_sellStation == null) _sellStation = FindAnyObjectByType<ScrapSellStation>();
        if (_blackjackStation == null) _blackjackStation = FindAnyObjectByType<BlackjackStation>();
    }

    private bool IsUIOpen()
    {
        RefreshUIReferences();
        
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

            // --- ANTIDOTE LOGIC ---
            var pc = GetComponentInParent<PlayerController>();
            if (pc != null && pc.netEquippedWeapon.Value == 7) // 7 = Antidote
            {
                var targetSurvival = hit.collider.GetComponentInParent<PlayerSurvival>();
                if (targetSurvival != null && targetSurvival.gameObject != pc.gameObject)
                {
                    var myInv = GetComponentInParent<PlayerInventory>();
                    if (myInv != null && myInv.antidotes > 0)
                    {
                        myInv.antidotes--;
                        pc.netEquippedWeapon.Value = 0; // Unequip after use
                        targetSurvival.ApplyAntidoteServerRpc();
                        Debug.Log("[Interaction] Used antidote on " + targetSurvival.gameObject.name);
                        return;
                    }
                }
            }
            // ----------------------

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
        bool isPickupAnim = interactable is ScrapItem;
        if (isPickupAnim && animator != null)
            animator.SetTrigger("Lifting");

        StartCoroutine(DelayedPickup(interactable, isPickupAnim));
    }

    private System.Collections.IEnumerator DelayedPickup(IInteractable interactable, bool isPickupAnim = true)
    {
        isPickingUp = true;

        if (!isPickupAnim)
        {
            yield return null;
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
                Debug.LogError($"[Interaction] Error: {e.Message}");
            }
            isPickingUp = false;
            yield break;
        }

        // ── 1. Kích hoạt hiệu ứng góc nhìn thứ nhất (FPS) ──
        PlayerController pc = GetComponentInParent<PlayerController>();
        if (pc != null) pc.TriggerPickupDip();

        // ── 2. Xử lý logic nhặt đồ ngay lập tức để Server ghi nhận ──
        GameObject originalObj = (interactable as MonoBehaviour)?.gameObject;
        
        // Tạo clone giả để bay vào màn hình trước khi đồ thật biến mất
        if (originalObj != null && playerCamera != null)
        {
            StartCoroutine(FlyItemToCamera(originalObj));
        }

        yield return null;

        try
        {
            if (interactable != null && (interactable as MonoBehaviour) != null)
            {
                interactable.Interact(gameObject);
                pc?.ForceUIRefresh();
            }
        }
        catch (System.Exception)
        {
        }
        
        // Đợi 0.3s cho animation giả bay vào túi kết thúc rồi mới cho phép nhặt món khác
        yield return new WaitForSeconds(0.3f);
        isPickingUp = false;
    }

    private System.Collections.IEnumerator FlyItemToCamera(GameObject originalObj)
    {
        // Clone object
        GameObject fakeObj = new GameObject("FakePickupItem");
        fakeObj.transform.position = originalObj.transform.position;
        fakeObj.transform.rotation = originalObj.transform.rotation;
        fakeObj.transform.localScale = originalObj.transform.localScale;

        // Copy Mesh
        MeshFilter[] mfs = originalObj.GetComponentsInChildren<MeshFilter>();
        MeshRenderer[] mrs = originalObj.GetComponentsInChildren<MeshRenderer>();

        for (int i = 0; i < mfs.Length; i++)
        {
            GameObject child = new GameObject("Mesh");
            child.transform.SetParent(fakeObj.transform);
            child.transform.position = mfs[i].transform.position;
            child.transform.rotation = mfs[i].transform.rotation;
            child.transform.localScale = mfs[i].transform.localScale;

            MeshFilter mf = child.AddComponent<MeshFilter>();
            mf.sharedMesh = mfs[i].sharedMesh;
            MeshRenderer mr = child.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mrs[i].sharedMaterials;
        }

        float duration = 0.25f;
        float elapsed = 0f;
        Vector3 startPos = fakeObj.transform.position;
        Vector3 startScale = fakeObj.transform.localScale;

        while (elapsed < duration)
        {
            if (playerCamera == null) break;
            
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Ease out
            float easeT = 1f - Mathf.Pow(1f - t, 3f);

            // Mục tiêu bay đến: Trước mặt camera một chút và hơi xích xuống dưới
            Vector3 targetPos = playerCamera.transform.position + playerCamera.transform.forward * 0.4f - playerCamera.transform.up * 0.25f;

            fakeObj.transform.position = Vector3.Lerp(startPos, targetPos, easeT);
            fakeObj.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, easeT);

            yield return null;
        }

        Destroy(fakeObj);
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