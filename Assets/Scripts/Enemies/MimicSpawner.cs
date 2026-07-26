using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Unity.Netcode;

/// <summary>
/// Handles spawning one or more Mimics randomly on the map (NavMesh)
/// at the start of the game. Respects minimum distance from the player.
/// </summary>
public class MimicSpawner : NetworkBehaviour
{
    [Header("Prefab")]
    [Tooltip("Drag the Mimic prefab here (Assets/Prefabs/Mimic.prefab).")]
    public GameObject mimicPrefab;

    [Header("Spawn Settings")]
    [Tooltip("How many Mimics to spawn at game start.")]
    [Min(1)]
    public int mimicsToSpawn = 1;

    [Tooltip("If true, spawning happens at completely random locations on the map.")]
    public bool completelyRandomSpawn = true;

    [Header("Player Safety")]
    [Tooltip("Global minimum distance from the Player for any spawn.")]
    public float globalMinDistanceFromPlayer = 20f;

    [Tooltip("Tag used to locate the player GameObject.")]
    public string playerTag = "Player";

    // ── Runtime state ────────────────────────────────────────────────────────
    private List<GameObject> _spawnedMimics = new List<GameObject>();



    // ─────────────────────────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Invoke(nameof(SpawnMimics), 2f);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Destroy all existing Mimics and spawn a fresh set. Server only.</summary>
    public void RespawnAll()
    {
        if (!IsServer) return;
        DespawnAll();
        SpawnMimics();
    }

    /// <summary>Destroy all currently tracked Mimics. Server only.</summary>
    public void DespawnAll()
    {
        if (!IsServer) return;
        foreach (GameObject m in _spawnedMimics)
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
        _spawnedMimics.Clear();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void SpawnMimics()
    {
        if (mimicPrefab == null)
        {
            Debug.LogError("[MimicSpawner] mimicPrefab is not assigned! " +
                           "Drag Assets/Prefabs/Mimic.prefab into the MimicSpawner component.");
            return;
        }

        // Find player for distance checks (if any exists early on)
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;

        int spawned = 0;
        int attempts = 0;

        while (spawned < mimicsToSpawn && attempts < 100)
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
                GameObject mimic = Instantiate(mimicPrefab, spawnPos, spawnRot);
                mimic.name = $"Mimic_{spawned + 1}";
                
                NetworkObject netObj = mimic.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn(true); // destroyWithScene = true
                }
                else
                {
                    Debug.LogWarning($"[MimicSpawner] mimicPrefab is missing a NetworkObject component!");
                }

                _spawnedMimics.Add(mimic);

                Debug.Log($"[MimicSpawner] Randomly spawned '{mimic.name}' at {spawnPos}");
                spawned++;
            }
            attempts++;
        }

        if (spawned < mimicsToSpawn)
        {
            Debug.LogWarning($"[MimicSpawner] Could only spawn {spawned}/{mimicsToSpawn} Mimics randomly.");
        }
    }
}
