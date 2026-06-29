using UnityEngine;

/// <summary>
/// Shared utility methods for spawning items correctly:
///   - SnapToGround  : moves an object so its mesh bottom sits exactly on a ground point
///   - FitColliders  : resizes every BoxCollider to exactly match its mesh bounds
/// </summary>
public static class SpawnUtils
{
    /// <summary>
    /// After instantiating an object, call this to:
    ///  1. Snap the object so the BOTTOM of its combined renderer bounds
    ///     sits exactly on <paramref name="groundPoint"/>.
    ///  2. Fit all BoxColliders to their mesh bounds.
    /// </summary>
    /// <param name="obj">The spawned GameObject.</param>
    /// <param name="groundPoint">World-space point on the ground surface (from Raycast hit.point).</param>
    public static void SnapToGround(GameObject obj, Vector3 groundPoint)
    {
        // Unity needs one frame for renderers to initialise in play mode, but
        // in Editor we can use bounds immediately — this works for both.
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            // No renderer — just place at ground point
            obj.transform.position = groundPoint;
            return;
        }

        // Calculate combined world-space bounds across ALL child renderers
        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            worldBounds.Encapsulate(renderers[i].bounds);

        // The gap between the pivot (transform.position.y) and the mesh bottom
        // e.g. pivot is at centre → pivotToBottom = bounds.extents.y
        float pivotToBottom = obj.transform.position.y - worldBounds.min.y;

        // Place so mesh bottom = groundPoint.y
        obj.transform.position = new Vector3(
            groundPoint.x,
            groundPoint.y + pivotToBottom,
            groundPoint.z
        );
    }

    /// <summary>
    /// Fits every BoxCollider on this object (and its children) to match
    /// the exact bounds of the MeshFilter on the same GameObject.
    ///
    /// If a child has a MeshRenderer but no BoxCollider, one is added automatically.
    /// If a child has no mesh at all, it is skipped.
    /// </summary>
    /// <param name="obj">Root of the spawned hierarchy.</param>
    public static void FitColliders(GameObject obj)
    {
        MeshFilter[] meshFilters = obj.GetComponentsInChildren<MeshFilter>();

        if (meshFilters.Length == 0)
        {
            // Fallback: fit root BoxCollider to combined renderer bounds in local space
            FitRootColliderFromRenderers(obj);
            return;
        }

        foreach (MeshFilter mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;

            // Use LOCAL-space mesh bounds — perfect for BoxCollider
            Bounds localBounds = mf.sharedMesh.bounds;

            BoxCollider bc = mf.gameObject.GetComponent<BoxCollider>();
            if (bc == null)
                bc = mf.gameObject.AddComponent<BoxCollider>();

            bc.center = localBounds.center;
            bc.size   = localBounds.size;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Fallback for objects that have no MeshFilter (e.g. primitives).
    /// Converts world-space renderer bounds into the object's local space
    /// and applies them to a root BoxCollider.
    /// </summary>
    private static void FitRootColliderFromRenderers(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds worldBounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
            worldBounds.Encapsulate(r.bounds);

        BoxCollider bc = obj.GetComponent<BoxCollider>();
        if (bc == null)
            bc = obj.AddComponent<BoxCollider>();

        // Convert world-space bounds centre/size to local space
        bc.center = obj.transform.InverseTransformPoint(worldBounds.center);
        bc.size   = obj.transform.InverseTransformVector(worldBounds.size);

        // Make size always positive (InverseTransformVector can produce negatives with flipped scales)
        bc.size = new Vector3(
            Mathf.Abs(bc.size.x),
            Mathf.Abs(bc.size.y),
            Mathf.Abs(bc.size.z)
        );
    }
}
