using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Đặt script này vào một GameObject trống trong Scene (ví dụ đặt tên "GameManager").
/// Nó sẽ tự động tìm Cửa và Chìa khóa để dịch chuyển đi chỗ khác mỗi khi bắt đầu game.
/// </summary>
public class EscapeRandomizer : MonoBehaviour
{
    [Header("Random Settings")]
    [Tooltip("Bật/tắt việc random cửa. Nếu tắt, cửa sẽ nằm cố định ở vị trí bạn đặt trong Scene.")]
    public bool randomizeDoor = false;

    [Tooltip("Bán kính tối đa của bản đồ (m) để script tìm rìa")]
    public float mapRadius = 50f;

    [Header("Manual Door Spawns (Tùy chọn)")]
    [Tooltip("Nếu bạn muốn cửa chỉ ra ở các vị trí cố định do bạn chỉ định, kéo các Transform vào đây. Nếu để trống, game sẽ tự dò rìa bản đồ bằng AI.")]
    public Transform[] predefinedDoorEdges;

    // RNG riêng, đồng bộ bằng Seed chung
    private System.Random _rng;

    System.Collections.IEnumerator Start()
    {
        if (Unity.Netcode.NetworkManager.Singleton != null && !Unity.Netcode.NetworkManager.Singleton.IsServer) yield break;

        while (PlayerInventory.GlobalMatchSeed == 0) yield return null;
        _rng = new System.Random(PlayerInventory.GlobalMatchSeed + 1337);

        // 1. Random vị trí Cửa Thoát Hiểm
        ExtractionSystem door = Object.FindAnyObjectByType<ExtractionSystem>();
        if (door != null && randomizeDoor)
        {
            MoveDoorToEdge(door.gameObject);
        }

        // 2. Random vị trí Chìa Khóa
        ScrapItem[] items = Object.FindObjectsByType<ScrapItem>();
        foreach (var item in items)
        {
            if (item.scrapType.ToLower() == "key" || item.scrapType.ToLower() == "escape_key")
            {
                MoveItemToRandomLocation(item.gameObject);
            }
        }
    }

    void MoveDoorToEdge(GameObject door)
    {
        // Nếu bạn có chỉ định điểm thì dùng điểm của bạn
        if (predefinedDoorEdges != null && predefinedDoorEdges.Length > 0)
        {
            Transform p = predefinedDoorEdges[_rng.Next(0, predefinedDoorEdges.Length)];
            door.transform.position = p.position;
            door.transform.rotation = p.rotation;
        }
        else
        {
            // Tự động dò rìa bản đồ bằng NavMesh.Raycast từ tâm bắn ra ngoài
            Vector3 center = Vector3.zero;
            
            // Cố gắng tìm tâm NavMesh
            if (NavMesh.SamplePosition(center, out NavMeshHit centerHit, 10f, NavMesh.AllAreas))
            {
                center = centerHit.position;
            }

            // Pre-compute tất cả góc random trước khi dùng NavMesh
            float[] angles = new float[30];
            for (int i = 0; i < 30; i++)
            {
                angles[i] = (float)_rng.NextDouble() * 360f;
            }

            // Thử bắn nhiều tia để tìm rìa xa nhất
            Vector3 bestPos = center;
            Vector3 bestNormal = Vector3.forward;
            float maxDist = -1f;

            for (int i = 0; i < 30; i++)
            {
                float angle = angles[i];
                Vector3 dir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0, Mathf.Sin(angle * Mathf.Deg2Rad)).normalized;
                Vector3 target = center + dir * mapRadius;

                if (NavMesh.Raycast(center, target, out NavMeshHit hit, NavMesh.AllAreas))
                {
                    float dist = Vector3.Distance(center, hit.position);
                    if (dist > maxDist)
                    {
                        maxDist = dist;
                        bestPos = hit.position;
                        bestNormal = hit.normal;
                    }
                }
            }

            if (maxDist > 0f)
            {
                door.transform.position = bestPos;
                
                Vector3 toCenter = center - bestPos;
                toCenter.y = 0;
                if (Vector3.Dot(bestNormal, toCenter) < 0)
                {
                    bestNormal = -bestNormal;
                }
                
                if (bestNormal != Vector3.zero)
                {
                    door.transform.rotation = Quaternion.LookRotation(bestNormal);
                }
            }
            else
            {
                door.transform.position = center + Vector3.forward * 10f;
            }
        }
    }

    void MoveItemToRandomLocation(GameObject item)
    {
        // Pre-compute random values
        float rx = (float)_rng.NextDouble() * 2f - 1f;
        float ry = (float)_rng.NextDouble() * 2f - 1f;
        float rz = (float)_rng.NextDouble() * 2f - 1f;
        float rotY = (float)_rng.NextDouble() * 360f;

        Vector3 randomPos = new Vector3(rx, ry, rz).normalized * (mapRadius * 0.8f * (float)_rng.NextDouble());
        randomPos.y = 0;
        
        if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, mapRadius, NavMesh.AllAreas))
        {
            item.transform.position = hit.position + Vector3.up * 0.5f;
            item.transform.rotation = Quaternion.Euler(0, rotY, 0);
        }
    }
}
