using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Handles spawning one or more Mimics at random MimicSpawnPoint locations
/// at the start of the game. Respects minimum distance from the player and
/// spawn point weights. Spawned Mimics are tracked so they can be respawned
/// on player death / new round.
/// </summary>
public class MimicSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("Drag the Mimic prefab here (Assets/Prefabs/Mimic.prefab).")]
    public GameObject mimicPrefab;

    [Header("Spawn Settings")]
    [Tooltip("How many Mimics to spawn at game start.")]
    [Min(1)]
    public int mimicsToSpawn = 1;

    [Tooltip("Shuffle spawn points so the same locations are not reused every session.")]
    public bool shuffleSpawnPoints = true;

    [Tooltip("If true, spawner auto-finds all MimicSpawnPoint objects in the scene. " +
             "If false, only the points in the manual list below are used.")]
    public bool autoFindSpawnPoints = true;

    [Tooltip("Manual list of spawn points (used when autoFindSpawnPoints is false).")]
    public List<MimicSpawnPoint> manualSpawnPoints = new List<MimicSpawnPoint>();

    [Header("Player Safety")]
    [Tooltip("Global minimum distance from the Player for any spawn. " +
             "Overrides individual MimicSpawnPoint.minDistanceFromPlayer if larger.")]
    public float globalMinDistanceFromPlayer = 20f;

    [Tooltip("Tag used to locate the player GameObject.")]
    public string playerTag = "Player";

    [Header("NavMesh Validation")]
    [Tooltip("Max distance to search for a valid NavMesh position near the spawn point.")]
    public float navMeshSampleRadius = 5f;

    // ── Runtime state ────────────────────────────────────────────────────────
    private List<MimicSpawnPoint> _allSpawnPoints = new List<MimicSpawnPoint>();
    private List<GameObject>      _spawnedMimics  = new List<GameObject>();

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        CollectSpawnPoints();
        SpawnMimics();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Destroy all existing Mimics and spawn a fresh set.</summary>
    public void RespawnAll()
    {
        DespawnAll();
        CollectSpawnPoints();
        SpawnMimics();
    }

    /// <summary>Destroy all currently tracked Mimics.</summary>
    public void DespawnAll()
    {
        foreach (GameObject m in _spawnedMimics)
        {
            if (m != null) Destroy(m);
        }
        _spawnedMimics.Clear();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void CollectSpawnPoints()
    {
        _allSpawnPoints.Clear();

        if (autoFindSpawnPoints)
        {
            // Find every MimicSpawnPoint in the scene (including inactive objects)
            MimicSpawnPoint[] found = FindObjectsByType<MimicSpawnPoint>(FindObjectsInactive.Include);
            _allSpawnPoints.AddRange(found);
        }
        else
        {
            _allSpawnPoints.AddRange(manualSpawnPoints);
        }

        if (_allSpawnPoints.Count == 0)
        {
            Debug.LogWarning("[MimicSpawner] No MimicSpawnPoint objects found in scene! " +
                             "Add GameObjects with the MimicSpawnPoint component to mark spawn locations.");
        }
    }

    private void SpawnMimics()
    {
        if (mimicPrefab == null)
        {
            Debug.LogError("[MimicSpawner] mimicPrefab is not assigned! " +
                           "Drag Assets/Prefabs/Mimic.prefab into the MimicSpawner component.");
            return;
        }

        if (_allSpawnPoints.Count == 0) return;

        // Find player for distance checks
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        Vector3 playerPos = player != null ? player.transform.position : Vector3.one * float.MaxValue;

        // Build a weighted, eligible list
        List<(MimicSpawnPoint point, float weight)> eligible = BuildEligibleList(playerPos);

        if (eligible.Count == 0 && _allSpawnPoints.Count > 0)
        {
            Debug.LogWarning("[MimicSpawner] No eligible spawn points found " +
                             "(all too close to player or off NavMesh). " +
                             "Try increasing the number of spawn points or reducing globalMinDistanceFromPlayer.");
            return;
        }

        // Shuffle if requested (Fisher-Yates)
        if (shuffleSpawnPoints && eligible.Count > 0)
            ShuffleList(eligible);

        int spawned = 0;
        int attempts = 0;

        while (spawned < mimicsToSpawn && attempts < 50)
        {
            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;
            bool validPos = false;

            if (eligible.Count > 0)
            {
                MimicSpawnPoint chosenPoint = PickWeighted(eligible);
                if (NavMesh.SamplePosition(chosenPoint.transform.position, out NavMeshHit navHit, navMeshSampleRadius, NavMesh.AllAreas))
                {
                    spawnPos = navHit.position;
                    spawnRot = chosenPoint.transform.rotation;
                    validPos = true;
                }
                else
                {
                    int skipIdx = eligible.FindIndex(e => e.point == chosenPoint);
                    if (skipIdx >= 0) eligible.RemoveAt(skipIdx);
                }

                if (validPos)
                {
                    int removeIdx = eligible.FindIndex(e => e.point == chosenPoint);
                    if (removeIdx >= 0) eligible.RemoveAt(removeIdx);
                }
            }
            else
            {
                // FALLBACK: Hoàn toàn random trên bản đồ nếu không có điểm spawn nào
                Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(globalMinDistanceFromPlayer, globalMinDistanceFromPlayer + 100f);
                Vector3 tryPos = playerPos != Vector3.one * float.MaxValue ? playerPos : Vector3.zero;
                tryPos += new Vector3(randomCircle.x, 0, randomCircle.y);
                
                if (NavMesh.SamplePosition(tryPos, out NavMeshHit navHit, 20f, NavMesh.AllAreas))
                {
                    spawnPos = navHit.position;
                    spawnRot = Quaternion.Euler(0, Random.Range(0, 360), 0);
                    validPos = true;
                }
            }

            if (validPos)
            {
                GameObject mimic = Instantiate(mimicPrefab, spawnPos, spawnRot);
                mimic.name = $"Mimic_{spawned + 1}";
                _spawnedMimics.Add(mimic);

                Debug.Log($"[MimicSpawner] Spawned '{mimic.name}' at {spawnPos} (Random fallback: {eligible.Count == 0})");
                spawned++;
            }
            attempts++;
        }

        if (spawned < mimicsToSpawn)
        {
            Debug.LogWarning($"[MimicSpawner] Could only spawn {spawned}/{mimicsToSpawn} Mimics. " +
                             "Add more MimicSpawnPoint objects to the scene.");
        }
    }

    /// <summary>
    /// Filters spawn points by distance from player and returns (point, weight) tuples.
    /// </summary>
    private List<(MimicSpawnPoint, float)> BuildEligibleList(Vector3 playerPos)
    {
        var eligible = new List<(MimicSpawnPoint, float)>();

        foreach (MimicSpawnPoint sp in _allSpawnPoints)
        {
            if (sp == null) continue;

            float minDist = Mathf.Max(globalMinDistanceFromPlayer, sp.minDistanceFromPlayer);
            float distToPlayer = Vector3.Distance(sp.transform.position, playerPos);

            if (distToPlayer >= minDist)
            {
                eligible.Add((sp, sp.weight));
            }
        }

        return eligible;
    }

    /// <summary>Weighted random pick from the eligible list.</summary>
    private MimicSpawnPoint PickWeighted(List<(MimicSpawnPoint point, float weight)> list)
    {
        float totalWeight = 0f;
        foreach (var entry in list) totalWeight += entry.weight;

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var entry in list)
        {
            cumulative += entry.weight;
            if (roll <= cumulative) return entry.point;
        }

        // Fallback (floating-point edge case)
        return list[list.Count - 1].point;
    }

    /// <summary>Fisher-Yates in-place shuffle.</summary>
    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Draw a line from spawner to each spawn point so relationships are clear
        if (autoFindSpawnPoints) return; // auto-found points draw themselves

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.7f);
        foreach (var sp in manualSpawnPoints)
        {
            if (sp != null)
                Gizmos.DrawLine(transform.position, sp.transform.position);
        }
    }
#endif
}
