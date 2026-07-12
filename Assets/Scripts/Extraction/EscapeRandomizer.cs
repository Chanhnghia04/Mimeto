using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Đặt script này vào một GameObject trống trong Scene (ví dụ đặt tên "GameManager").
/// Nó sẽ tự động tìm Cửa và Chìa khóa để dịch chuyển đi chỗ khác mỗi khi bắt đầu game.
/// </summary>
public class EscapeRandomizer : MonoBehaviour
{
    [Header("Random Settings")]
    [Tooltip("Bán kính tối đa của bản đồ (m) để script tìm rìa")]
    public float mapRadius = 50f;

    [Header("Manual Door Spawns (Tùy chọn)")]
    [Tooltip("Nếu bạn muốn cửa chỉ ra ở các vị trí cố định do bạn chỉ định, kéo các Transform vào đây. Nếu để trống, game sẽ tự dò rìa bản đồ bằng AI.")]
    public Transform[] predefinedDoorEdges;

    void Start()
    {
        // 1. Random vị trí Cửa Thoát Hiểm
        ExtractionSystem door = Object.FindAnyObjectByType<ExtractionSystem>();
        if (door != null)
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
            Transform p = predefinedDoorEdges[Random.Range(0, predefinedDoorEdges.Length)];
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

            // Thử bắn nhiều tia để tìm rìa xa nhất (đảm bảo là tường bao ngoài cùng chứ không phải vách ngăn nhỏ)
            Vector3 bestPos = center;
            Vector3 bestNormal = Vector3.forward;
            float maxDist = -1f;

            for (int i = 0; i < 30; i++)
            {
                float angle = Random.Range(0f, 360f);
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)).normalized;
                Vector3 target = center + dir * mapRadius;

                if (NavMesh.Raycast(center, target, out NavMeshHit hit, NavMesh.AllAreas))
                {
                    float dist = Vector3.Distance(center, hit.position);
                    // Lưu lại rìa xa nhất tìm được
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
                
                // Đảm bảo pháp tuyến hướng vào trong map
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
                // Fallback nếu không có navmesh
                door.transform.position = center + Vector3.forward * 10f;
            }
        }
    }

    void MoveItemToRandomLocation(GameObject item)
    {
        // Chìa khóa sẽ random ở vòng trong của bản đồ
        Vector3 randomPos = Random.insideUnitSphere * (mapRadius * 0.8f);
        randomPos.y = 0;
        
        if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, mapRadius, NavMesh.AllAreas))
        {
            // Đặt chìa khóa nhô lên một chút khỏi mặt đất để không bị chìm
            item.transform.position = hit.position + Vector3.up * 0.5f;
            
            // Xoay lung tung cho tự nhiên
            item.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
        }
    }
}
