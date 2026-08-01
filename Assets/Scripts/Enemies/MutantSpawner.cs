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

    private void OnDestroy()
    {
        CancelInvoke(nameof(SpawnMutants));
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

        if (completelyRandomSpawn)
        {
            SpawnRandomly(playerPos);
        }
        else
        {
            SpawnAtSpawnPoints(playerPos);
        }
    }

    /// <summary>Spawn at weighted MutantSpawnPoint locations, respecting player distance.</summary>
    private void SpawnAtSpawnPoints(Vector3 playerPos)
    {
        MutantSpawnPoint[] allPoints = FindObjectsByType<MutantSpawnPoint>();
        if (allPoints.Length == 0)
        {
            Debug.LogWarning("[MutantSpawner] No MutantSpawnPoints found! Falling back to random spawn.");
            SpawnRandomly(playerPos);
            return;
        }

        // Build weighted list of eligible points
        List<MutantSpawnPoint> eligible = new List<MutantSpawnPoint>();
        float totalWeight = 0f;

        foreach (var sp in allPoints)
        {
            float minDist = Mathf.Max(sp.minDistanceFromPlayer, globalMinDistanceFromPlayer);
            float distToPlayer = Vector3.Distance(sp.transform.position, playerPos);
            if (distToPlayer >= minDist)
            {
                eligible.Add(sp);
                totalWeight += sp.weight;
            }
        }

        if (eligible.Count == 0)
        {
            Debug.LogWarning("[MutantSpawner] All SpawnPoints too close to player! Falling back to random spawn.");
            SpawnRandomly(playerPos);
            return;
        }

        // Shuffle to avoid same order bias
        if (shuffleSpawnPoints)
        {
            for (int i = eligible.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (eligible[i], eligible[j]) = (eligible[j], eligible[i]);
            }
        }

        int spawned = 0;
        HashSet<int> usedIndices = new HashSet<int>();

        while (spawned < mutantsToSpawn && usedIndices.Count < eligible.Count)
        {
            // Weighted random selection
            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            int selectedIndex = 0;

            for (int i = 0; i < eligible.Count; i++)
            {
                if (usedIndices.Contains(i)) continue;
                cumulative += eligible[i].weight;
                if (roll <= cumulative)
                {
                    selectedIndex = i;
                    break;
                }
            }

            usedIndices.Add(selectedIndex);
            MutantSpawnPoint chosenPoint = eligible[selectedIndex];
            totalWeight -= chosenPoint.weight;

            // Validate on NavMesh
            if (NavMesh.SamplePosition(chosenPoint.transform.position, out NavMeshHit navHit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                Vector3 spawnPos = navHit.position;
                Quaternion spawnRot = chosenPoint.transform.rotation;

                GameObject mutant = Instantiate(mutantPrefab, spawnPos, spawnRot);
                mutant.name = $"Mutant_{spawned + 1}";

                NetworkObject netObj = mutant.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn(true);
                }
                else
                {
                    Debug.LogWarning("[MutantSpawner] mutantPrefab is missing a NetworkObject component!");
                }

                _spawnedMutants.Add(mutant);
                Debug.Log($"[MutantSpawner] Spawned '{mutant.name}' at SpawnPoint '{chosenPoint.name}' (weight={chosenPoint.weight})");
                spawned++;
            }
        }

        if (spawned < mutantsToSpawn)
        {
            Debug.LogWarning($"[MutantSpawner] Only spawned {spawned}/{mutantsToSpawn} via SpawnPoints. Spawning rest randomly.");
            int remaining = mutantsToSpawn - spawned;
            int oldCount = mutantsToSpawn;
            mutantsToSpawn = remaining;
            SpawnRandomly(playerPos);
            mutantsToSpawn = oldCount;
        }
    }

    /// <summary>Spawn at completely random NavMesh positions.</summary>
    private void SpawnRandomly(Vector3 playerPos)
    {
        int spawned = 0;
        int attempts = 0;

        while (spawned < mutantsToSpawn && attempts < 100)
        {
            Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(globalMinDistanceFromPlayer, globalMinDistanceFromPlayer + 150f);
            Vector3 tryPos = playerPos + new Vector3(randomCircle.x, 0, randomCircle.y);
            
            if (NavMesh.SamplePosition(tryPos, out NavMeshHit navHit, 30f, NavMesh.AllAreas))
            {
                Vector3 spawnPos = navHit.position;
                Quaternion spawnRot = Quaternion.Euler(0, Random.Range(0, 360), 0);

                GameObject mutant = Instantiate(mutantPrefab, spawnPos, spawnRot);
                mutant.name = $"Mutant_{spawned + 1}";
                
                NetworkObject netObj = mutant.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn(true);
                }
                else
                {
                    Debug.LogWarning("[MutantSpawner] mutantPrefab is missing a NetworkObject component!");
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
