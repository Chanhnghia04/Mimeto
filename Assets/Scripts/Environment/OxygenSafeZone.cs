using UnityEngine;

[RequireComponent(typeof(Collider))]
public class OxygenSafeZone : MonoBehaviour
{
    [Tooltip("If true, automatically sets the collider to be a trigger on Start.")]
    public bool autoSetTrigger = true;

    private void Start()
    {
        if (autoSetTrigger)
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerSurvival survival = other.GetComponent<PlayerSurvival>();
            if (survival == null) survival = other.GetComponentInParent<PlayerSurvival>();
            
            if (survival != null)
            {
                survival.inSafeZone = true;
                Debug.Log("Player entered Oxygen Safe Zone. Oxygen is restoring.");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerSurvival survival = other.GetComponent<PlayerSurvival>();
            if (survival == null) survival = other.GetComponentInParent<PlayerSurvival>();

            if (survival != null)
            {
                survival.inSafeZone = false;
                Debug.Log("Player left Oxygen Safe Zone. Oxygen will deplete.");
            }
        }
    }
}
