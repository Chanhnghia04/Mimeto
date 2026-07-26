using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using UnityEngine.AI;

public class PlayerSpawnHandler : NetworkBehaviour
{
    private CharacterController cc;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            
            // If we spawned directly into the Map (e.g. testing)
            if (SceneManager.GetActiveScene().name == "Map")
            {
                TeleportToSafeSpawn();
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsOwner) return;

        if (scene.name == "Map")
        {
            TeleportToSafeSpawn();
        }
        else if (scene.name == "Waiting")
        {
            var survival = GetComponent<PlayerSurvival>();
            if (survival != null)
            {
                survival.Respawn();
                survival.inSafeZone = true;
            }
        }
    }

    private void TeleportToSafeSpawn()
    {
        Vector3 tryPos = new Vector3(Random.Range(-10f, 10f), 10f, Random.Range(-10f, 10f));
        if (NavMesh.SamplePosition(tryPos, out NavMeshHit hit, 50f, NavMesh.AllAreas))
        {
            tryPos = hit.position + Vector3.up * 2f;
        }
        else 
        {
            // Fallback if NavMesh not found nearby
            tryPos = new Vector3(0, 5f, 0);
        }

        if (cc != null) cc.enabled = false;
        transform.position = tryPos;
        if (cc != null) cc.enabled = true;
        
        // Reset inSafeZone so oxygen can deplete when entering the map
        var survival = GetComponent<PlayerSurvival>();
        if (survival != null)
        {
            survival.inSafeZone = false;
        }
    }
}
