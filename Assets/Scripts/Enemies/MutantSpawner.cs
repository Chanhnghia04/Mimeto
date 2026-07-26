using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Unity.Netcode;

/// <summary>
/// Handles spawning one or more Mutants at random MutantSpawnPoint locations
/// at the start of the game. Respects minimum distance from the player and
/// spawn point weights.
/// </summary>
public class MutantSpawner : NetworkBehaviour
{
    [Header("Prefab")]
    [Tooltip("Drag the Mutant prefab here (Assets/Prefabs/Mutant.prefab).")]
    public GameObject mutantPrefab;

    [Header("Spawn Settings")]
    [Tooltip("How many Mutants to spawn at game start.")]
    [Min(1)]
    public int mutantsToSpawn = 1;

    [Tooltip("Shuffle spawn points so the same locations are not reused every session.")]
    public bool shuffleSpawnPoints = true;

    [Tooltip("If true, spawning happens at completely random locations on the map.")]
    public bool completelyRandomSpawn = true;

    [Header("Player Safety")]
    [Tooltip("Global minimum distance from the Player for any spawn. " +
             "Overrides individual MutantSpawnPoint.minDistanceFromPlayer if larger.")]
    public float globalMinDistanceFromPlayer = 20f;

    [Tooltip("Tag used to locate the player GameObject.")]
    public string playerTag = "Player";

    [Header("NavMesh Validation")]
    [Tooltip("Max distance to search for a valid NavMesh position near the spawn point.")]
    public float navMeshSampleRadius = 5f;

    // ── Runtime state ────────────────────────────────────────────────────────
    private List<GameObject>      _spawnedMutants  = new List<GameObject>();



    // ─────────────────────────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Invoke(nameof(SpawnMutants), 2f);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Destroy all existing Mutants and spawn a fresh set. Server only.</summary>
    public void RespawnAll()
    {
        if (!IsServer) return;
        DespawnAll();
        SpawnMutants();
    }

    /// <summary>Destroy all currently tracked Mutants. Server only.</summary>
    public void DespawnAll()
    {
        if (!IsServer) return;
        foreach (GameObject m in _spawnedMutants)
        {
            if (m != null && m.GetComponent<NetworkObject>() != null)
            {
                m.GetComponent<NetworkObject>().Despawn();
            }
            else if (m != null)
            {
                Destroy(m);
            }
        }
        _spawnedMutants.Clear();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void SpawnMutants()
    {
        if (mutantPrefab == null)
        {
            Debug.LogError("[MutantSpawner] mutantPrefab is not assigned! " +
                           "Drag Assets/Prefabs/Mutant.prefab into the MutantSpawner component.");
            return;
        }

        // Find player for distance checks (if any exists early on)
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;

        int spawned = 0;
        int attempts = 0;

        while (spawned < mutantsToSpawn && attempts < 100)
        {
            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;
            bool validPos = false;

            // HOÀN TOÀN NGẪU NHIÊN: Sinh random trên bản đồ (NavMesh)
            // Lấy 1 điểm ngẫu nhiên trong bán kính lớn (ví dụ từ 20 đến 150 đơn vị)
            Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(globalMinDistanceFromPlayer, globalMinDistanceFromPlayer + 150f);
            Vector3 tryPos = playerPos + new Vector3(randomCircle.x, 0, randomCircle.y);
            
            if (NavMesh.SamplePosition(tryPos, out NavMeshHit navHit, 30f, NavMesh.AllAreas))
            {
                spawnPos = navHit.position;
                spawnRot = Quaternion.Euler(0, Random.Range(0, 360), 0);
                validPos = true;
            }

            if (validPos)
            {
                GameObject mutant = Instantiate(mutantPrefab, spawnPos, spawnRot);
                mutant.name = $"Mutant_{spawned + 1}";
                
                NetworkObject netObj = mutant.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn(true); // destroyWithScene = true
                }
                else
                {
                    Debug.LogWarning($"[MutantSpawner] mutantPrefab is missing a NetworkObject component!");
                }

                _spawnedMutants.Add(mutant);

                Debug.Log($"[MutantSpawner] Randomly spawned '{mutant.name}' at {spawnPos}");
                spawned++;
            }
            attempts++;
        }

        if (spawned < mutantsToSpawn)
        {
            Debug.LogWarning($"[MutantSpawner] Could only spawn {spawned}/{mutantsToSpawn} Mutants randomly.");
        }
    }
}
