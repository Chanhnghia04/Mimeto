using UnityEngine;

/// <summary>
/// Attached to a scrap item prefab.  Automatically places a correctly-sized
/// BoxCollider that matches the world-space visual bounds of the item so the
/// player can reliably pick it up with the Interaction raycast.
/// </summary>
public class ScrapItem : MonoBehaviour, IInteractable
{
    public string scrapType;
    public int amount = 1;
    public GameObject rootObject; // Destroyed on pick-up

    [Header("Interaction Hint")]
    public string interactHint = "Press [E] to pick up";

    [Header("Visual Effects")]
    [Tooltip("Bật hiệu ứng sáng màu xung quanh item")]
    public bool enableGlowEffect = true;
    public Color glowColor = new Color(0.2f, 0.8f, 1f, 1f); // Màu xanh lơ (cyan) mặc định
    public float glowIntensity = 2f;
    public float glowRange = 1.5f;

    // ── Runtime rotation + collider auto-fix ─────────────────────────────────

    void Awake()
    {
        FixRotation();
        RebuildCollider();
    }

    void Start()
    {
        if (enableGlowEffect)
        {
            // Tạo một GameObject con để chứa hiệu ứng đèn (màu xung quanh)
            GameObject glowObj = new GameObject("ItemGlow");
            glowObj.transform.SetParent(this.transform, false);
            // Đẩy nhẹ lên trên một chút để đèn toả đều
            glowObj.transform.localPosition = new Vector3(0f, 0.2f, 0f);

            Light glowLight = glowObj.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = glowColor;
            glowLight.intensity = glowIntensity;
            glowLight.range = glowRange;
            glowLight.renderMode = LightRenderMode.Auto; // Đã đổi thành Auto để tối ưu hiệu năng
        }
    }

    /// <summary>
    /// Forces the item to stand upright regardless of what rotation is baked
    /// into the prefab (e.g. -180° X from FBX axis-conversion issues).
    /// Keeps the Y rotation so random spawn orientations are preserved.
    /// </summary>
    void FixRotation()
    {
        // Read only the Y angle we want to keep
        float yAngle = transform.eulerAngles.y;

        // Apply identity rotation with only Y preserved
        transform.rotation = Quaternion.Euler(0f, yAngle, 0f);
    }

    /// <summary>
    /// Calculates the combined world-space bounds of all Renderers in this
    /// hierarchy, then writes a SINGLE BoxCollider on this root GameObject
    /// that covers exactly those bounds.
    ///
    /// This corrects FBX-imported prefabs where the mesh child has a large
    /// scale override (e.g. 22x), causing the stored collider size to be wrong.
    /// </summary>
    public void RebuildCollider()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        // ── 1. Temporarily activate all renderers so bounds are valid ─────────
        bool[] wasEnabled = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            wasEnabled[i]     = renderers[i].enabled;
            renderers[i].enabled = true;
        }

        // ── 2. Calculate combined WORLD-SPACE bounds ──────────────────────────
        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            worldBounds.Encapsulate(renderers[i].bounds);

        // ── 3. Restore renderer states ────────────────────────────────────────
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = wasEnabled[i];

        // ── 4. Remove any existing child-level BoxColliders (wrong position) ──
        BoxCollider[] allBC = GetComponentsInChildren<BoxCollider>(true);
        foreach (BoxCollider bc in allBC)
            if (bc.gameObject != gameObject)
                Destroy(bc);

        // ── 5. Write ONE BoxCollider on root in root-LOCAL space ──────────────
        BoxCollider rootBC = GetComponent<BoxCollider>();
        if (rootBC == null) rootBC = gameObject.AddComponent<BoxCollider>();

        // Convert world bounds to local space
        Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);
        Vector3 localSize   = transform.InverseTransformVector(worldBounds.size);
        localSize = new Vector3(
            Mathf.Abs(localSize.x),
            Mathf.Abs(localSize.y),
            Mathf.Abs(localSize.z));

        // Add 10 % padding so the player doesn't have to aim pixel-perfectly
        localSize *= 1.1f;

        rootBC.center    = localCenter;
        rootBC.size      = localSize;
        rootBC.isTrigger = false;
    }

    // ── IInteractable ─────────────────────────────────────────────────────────

    public void Interact(GameObject interactor)
    {
        // Search up the interactor's hierarchy in case InteractionSystem is on
        // a child (e.g. the Camera object) rather than the player root.
        PlayerInventory inventory = interactor.GetComponentInParent<PlayerInventory>();
        if (inventory == null)
            inventory = interactor.GetComponentInChildren<PlayerInventory>();

        if (inventory == null)
        {
            Debug.LogWarning($"[ScrapItem] Cannot pick up '{name}': " +
                             $"no PlayerInventory found on '{interactor.name}' or its hierarchy.");
            return;
        }

        if (!inventory.CanAddScrap(scrapType, amount))
        {
            Debug.Log($"[ScrapItem] Cannot pick up '{name}': Inventory full.");
            return;
        }

        inventory.RequestPickupItemServerRpc(transform.position, scrapType, amount);
    }

    // ── Editor gizmo: show the calculated collider in Scene view ─────────────
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc == null) return;

        Gizmos.color = new Color(0f, 1f, 0.3f, 0.35f);
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(
            transform.TransformPoint(bc.center),
            transform.rotation,
            transform.lossyScale);
        Gizmos.DrawCube(Vector3.zero, bc.size);
        Gizmos.matrix = old;

        // Draw label with scrap type
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.3f,
            $"[{scrapType} x{amount}]");
    }
#endif
}