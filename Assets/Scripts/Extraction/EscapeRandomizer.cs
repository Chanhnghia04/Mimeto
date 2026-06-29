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
            // Tự động dùng thuật toán tìm rìa bản đồ (NavMesh Edge)
            // Lấy một hướng ngẫu nhiên chĩa ra ngoài bản đồ
            Vector3 randomDir = Random.onUnitSphere;
            randomDir.y = 0;
            randomDir = randomDir.normalized * mapRadius;

            // Dóng xuống mặt đất (NavMesh)
            if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, mapRadius, NavMesh.AllAreas))
            {
                // Tìm vách ngăn / rìa (Edge) gần nhất của NavMesh
                if (NavMesh.FindClosestEdge(hit.position, out NavMeshHit edgeHit, NavMesh.AllAreas))
                {
                    door.transform.position = edgeHit.position;
                    
                    // Quay mặt cửa hướng vào giữa bản đồ (0,0,0) để dễ nhìn
                    Vector3 lookDir = -edgeHit.position;
                    lookDir.y = 0;
                    if (lookDir != Vector3.zero) 
                    {
                        door.transform.rotation = Quaternion.LookRotation(lookDir);
                    }
                }
                else
                {
                    door.transform.position = hit.position;
                }
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
