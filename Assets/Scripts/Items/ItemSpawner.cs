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

    void Start()
    {
        SpawnItems();
    }

    public void SpawnItems()
    {
        if (itemPrefabs == null || itemPrefabs.Length == 0)
        {
            Debug.LogWarning("No item prefabs assigned to ItemSpawner.");
            return;
        }

        int actuallySpawned = 0;

        for (int i = 0; i < totalItemsToSpawn; i++)
        {
            // Skip null slots in the Inspector array
            GameObject prefab = itemPrefabs[Random.Range(0, itemPrefabs.Length)];
            if (prefab == null) continue;

            // Pick a random XZ position and cast from above
            float randomX = Random.Range(-spawnAreaSize.x / 2f, spawnAreaSize.x / 2f);
            float randomZ = Random.Range(-spawnAreaSize.y / 2f, spawnAreaSize.y / 2f);
            Vector3 castOrigin = transform.position + new Vector3(randomX, spawnHeight, randomZ);

            if (!Physics.Raycast(castOrigin, Vector3.down, out RaycastHit hit,
                                 spawnHeight * 2f, groundLayer))
                continue; // Missed the ground — skip this slot

            // Spawn at the hit point (we'll adjust Y precisely after bounds are ready)
            Quaternion randomYRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Quaternion rot = prefab.transform.rotation * randomYRot;

            GameObject spawned = Instantiate(prefab, hit.point, rot);
            spawned.transform.SetParent(transform);

            // Fit BoxColliders to the actual mesh, then snap bottom to ground
            SpawnUtils.FitColliders(spawned);
            SpawnUtils.SnapToGround(spawned, hit.point);

            actuallySpawned++;
        }

        Debug.Log($"[ItemSpawner] Spawned {actuallySpawned} / {totalItemsToSpawn} items " +
                  "(some may have missed the ground layer).");
    }
}
