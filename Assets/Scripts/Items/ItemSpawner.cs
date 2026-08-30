using UnityEngine;
using System.Collections.Generic;

public class ItemSpawner : MonoBehaviour
{
    public GameObject[] itemPrefabs;
    public int totalItemsToSpawn = 50;
    public Vector2 spawnAreaSize = new Vector2(100, 100);

    [Tooltip("Height above the area from which to cast the ground-detection ray.")]
    public float spawnHeight = 10f;

    public LayerMask groundLayer;

    [Tooltip("If true, the spawner waits one frame after Instantiate before snapping " +
             "to ground, so renderer bounds are fully initialised (recommended for complex prefabs).")]
    public bool deferSnap = false;

    System.Collections.IEnumerator Start()
    {
        float seedElapsed = 0f;
        while (PlayerInventory.GlobalMatchSeed == 0 && seedElapsed < 15f)
        {
            seedElapsed += Time.deltaTime;
            yield return null;
        }

        // Wait for Netcode Scene Synchronization to finish before spawning NetworkObjects
        yield return new WaitForSeconds(2f);

        if (PlayerInventory.GlobalMatchSeed == 0)
        {
            Debug.LogWarning("[ItemSpawner] Timeout chờ seed! Dùng fallback seed.");
            PlayerInventory.GlobalMatchSeed = (int)(System.DateTime.Now.Ticks % 100000000);
        }
        SpawnItems();
    }

    public void SpawnItems()
    {
        if (Unity.Netcode.NetworkManager.Singleton != null && !Unity.Netcode.NetworkManager.Singleton.IsServer) return;

        if (itemPrefabs == null || itemPrefabs.Length == 0)
        {
            Debug.LogWarning("No item prefabs assigned to ItemSpawner.");
            return;
        }

        // Dùng System.Random với seed cố định => Host & Client sinh cùng chuỗi số
        int seed = PlayerInventory.GlobalMatchSeed + (int)transform.position.sqrMagnitude;
        System.Random rng = new System.Random(seed);

        // BƯỚC 1: Tính trước TẤT CẢ random values (prefab index, X, Z, rotation)
        // Điều này đảm bảo chuỗi random KHÔNG BAO GIỜ bị lệch bởi Physics.Raycast
        int[] prefabIndices = new int[totalItemsToSpawn];
        float[] randomXs    = new float[totalItemsToSpawn];
        float[] randomZs    = new float[totalItemsToSpawn];
        float[] randomRots  = new float[totalItemsToSpawn];

        for (int i = 0; i < totalItemsToSpawn; i++)
        {
            prefabIndices[i] = rng.Next(0, itemPrefabs.Length);
            randomXs[i]      = (float)rng.NextDouble() * spawnAreaSize.x - spawnAreaSize.x / 2f;
            randomZs[i]      = (float)rng.NextDouble() * spawnAreaSize.y - spawnAreaSize.y / 2f;
            randomRots[i]    = (float)rng.NextDouble() * 360f;
        }

        // BƯỚC 2: Spawn dựa trên dữ liệu đã tính trước
        int actuallySpawned = 0;

        for (int i = 0; i < totalItemsToSpawn; i++)
        {
            GameObject prefab = itemPrefabs[prefabIndices[i]];
            if (prefab == null) continue;

            Vector3 castOrigin = transform.position + new Vector3(randomXs[i], spawnHeight, randomZs[i]);

            if (!Physics.Raycast(castOrigin, Vector3.down, out RaycastHit hit,
                                 spawnHeight * 2f, groundLayer))
                continue; // Missed the ground — skip (nhưng random đã tiêu thụ ở trên rồi, không ảnh hưởng)

            Quaternion randomYRot = Quaternion.Euler(0f, randomRots[i], 0f);
            Quaternion rot = prefab.transform.rotation * randomYRot;

            GameObject spawned = Instantiate(prefab, hit.point, rot);
            // NetworkObject listens to Transform.parent changes.  A dynamically
            // instantiated NetworkObject is not spawned yet at this point, so
            // parenting it under this (non-NetworkObject) spawner would trigger
            // SpawnStateException from Netcode.  Networked items must remain
            // root objects; only ordinary local prefabs can be grouped here.
            Unity.Netcode.NetworkObject networkObject =
                spawned.GetComponent<Unity.Netcode.NetworkObject>();
            if (networkObject == null)
                spawned.transform.SetParent(transform, true);

            SpawnUtils.FitColliders(spawned);
            SpawnUtils.SnapToGround(spawned, hit.point);
            if (networkObject != null &&
                Unity.Netcode.NetworkManager.Singleton != null &&
                Unity.Netcode.NetworkManager.Singleton.IsServer)
            {
                // Spawn only after the final position/collider setup.  Do not
                // reparent afterwards: ItemSpawner is not a NetworkObject and
                // Netcode only supports synchronized parenting between spawned
                // NetworkObjects.
                networkObject.Spawn();
            }

            actuallySpawned++;
        }

        Debug.Log($"[ItemSpawner] Spawned {actuallySpawned} / {totalItemsToSpawn} items " +
                  "(some may have missed the ground layer).");
    }
}
