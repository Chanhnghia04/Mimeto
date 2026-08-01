using UnityEngine;
using System.Collections;

public class TrainMovement : MonoBehaviour
{
    [SerializeField] private float startX = -100f;
    [SerializeField] private float endX = 100f;
    [SerializeField] private float speed = 10f;
    [SerializeField] private float intervalSeconds = 120f;

    private void Start()
    {
        // Rigidbody must be on the root to receive trigger events from children
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;

        // Prevent scale distortion issues: Clean up any trigger collider on the root from previous builds
        Collider rootCol = GetComponent<Collider>();
        if (rootCol != null && rootCol.isTrigger)
        {
            Destroy(rootCol);
        }

        // Create a precise child trigger GameObject that is unaffected by parent scale
        Transform existingTrigger = transform.Find("TrainDamageTrigger");
        if (existingTrigger == null)
        {
            GameObject triggerGo = new GameObject("TrainDamageTrigger");
            triggerGo.transform.SetParent(this.transform, false);
            triggerGo.transform.localPosition = Vector3.zero;
            triggerGo.transform.localRotation = Quaternion.identity;
            triggerGo.transform.localScale = Vector3.one;

            BoxCollider box = triggerGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            
            // Set size of the trigger to be narrow and accurate to standard train width (e.g., 2.2m wide, 3m high, 9m long)
            // We divide by parent's lossyScale to ensure it remains exactly this size in World Space!
            Vector3 parentScale = transform.lossyScale;
            float targetWidth = 2.2f;  // standard train width is around 2.2m
            float targetHeight = 3.0f; // height
            float targetLength = 9.0f; // length

            box.size = new Vector3(
                targetLength / (parentScale.x != 0 ? parentScale.x : 1f),
                targetHeight / (parentScale.y != 0 ? parentScale.y : 1f),
                targetWidth / (parentScale.z != 0 ? parentScale.z : 1f)
            );
        }

        StartCoroutine(TrainRoutine());
    }

    private IEnumerator TrainRoutine()
    {
        while (true)
        {
            float startTime = Time.time;

            // Reset to start position
            Vector3 pos = transform.localPosition;
            pos.x = startX;
            transform.localPosition = pos;

            Debug.Log("Train starting movement...");

            // Move to end
            while (transform.localPosition.x < endX)
            {
                transform.localPosition += Vector3.right * speed * Time.deltaTime;
                CheckCollision(); // Check collision every frame
                yield return null;
            }

            // Reset to start position (out of view if startX is off-screen)
            pos.x = startX;
            transform.localPosition = pos;

            float elapsed = Time.time - startTime;
            float waitTime = Mathf.Max(0, intervalSeconds - elapsed);

            Debug.Log("Train finished movement. Next run in " + waitTime + " seconds.");
            yield return new WaitForSeconds(waitTime);
        }
    }

    private void CheckCollision()
    {
        // Ignore collisions during the first 1.0 second of loading
        if (Time.timeSinceLevelLoad < 1.0f) return;

        Transform trigger = transform.Find("TrainDamageTrigger");
        if (trigger == null) return;
        BoxCollider box = trigger.GetComponent<BoxCollider>();
        if (box == null) return;

        Collider[] hits = Physics.OverlapBox(box.bounds.center, box.bounds.extents, box.transform.rotation);
        foreach (Collider other in hits)
        {
            if (other.isTrigger) continue;

            PlayerSurvival survival = other.GetComponent<PlayerSurvival>();
            if (survival == null) survival = other.GetComponentInParent<PlayerSurvival>();

            if (survival != null)
            {
                Debug.LogWarning("<color=red>TRAIN COLLISION: Player was run over by the train!</color>");
                survival.TakeDamage(1000f, "Hit by a train!"); // Instakill the player
            }

            MimicAI mimic = other.GetComponent<MimicAI>();
            if (mimic == null) mimic = other.GetComponentInParent<MimicAI>();

            if (mimic != null)
            {
                Debug.LogWarning("<color=red>TRAIN COLLISION: Mimic was run over by the train!</color>");
                mimic.TakeDamage(1000f); // Kill the Mimic too
            }
        }
    }
}