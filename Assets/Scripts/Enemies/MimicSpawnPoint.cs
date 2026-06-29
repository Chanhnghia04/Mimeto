using UnityEngine;

/// <summary>
/// Attach this component to any GameObject to mark it as a valid Mimic spawn location.
/// Spawn points are automatically discovered by MimicSpawner at runtime.
/// Visualised as a red sphere in the Scene view for easy placement.
/// </summary>
public class MimicSpawnPoint : MonoBehaviour
{
    [Tooltip("Weight of this spawn point. Higher weight = more likely to be chosen.")]
    [Range(0.1f, 5f)]
    public float weight = 1f;

    [Tooltip("Minimum distance from the Player required for this point to be eligible at spawn time.")]
    public float minDistanceFromPlayer = 20f;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Outer sphere — spawn zone indicator
        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.25f);
        Gizmos.DrawSphere(transform.position, 1.5f);

        // Wireframe border
        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 1.5f);

        // Forward arrow showing which way the Mimic will face
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, transform.forward * 2f);

        // Label
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 2.2f,
            $"[Mimic Spawn]\nWeight: {weight}"
        );
    }
#endif
}
