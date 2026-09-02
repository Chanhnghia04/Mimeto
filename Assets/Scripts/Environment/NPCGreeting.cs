using UnityEngine;

public class NPCGreeting : MonoBehaviour
{
    private Animator animator;
    public float interactionDistance = 4f;
    private bool wasNear = false;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        bool isNear = false;
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, interactionDistance);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player") || hitCollider.name.ToLower().Contains("player"))
            {
                var netObj = hitCollider.GetComponentInParent<Unity.Netcode.NetworkObject>();
                if (netObj != null && netObj.IsOwner)
                {
                    isNear = true;
                    break;
                }
            }
        }
        
        if (animator != null)
        {
            // Greet once when player just enters the radius
            if (isNear && !wasNear)
            {
                animator.SetTrigger("doGreet");
            }
            
            // Talk when player is near and presses E
            if (isNear && Input.GetKeyDown(KeyCode.E))
            {
                animator.SetTrigger("doTalk");
            }
        }
        
        wasNear = isNear;
    }
}
