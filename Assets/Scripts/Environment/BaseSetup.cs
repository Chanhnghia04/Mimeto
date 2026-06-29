using UnityEngine;

public class BaseSetup : MonoBehaviour
{
    [Tooltip("Vị trí Player sẽ xuất hiện khi vào game")]
    public Transform spawnPoint;

    void Awake()
    {
        // Tự động tìm Player trong scene
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            PlayerSurvival survival = player.GetComponent<PlayerSurvival>();
            if (survival != null && spawnPoint != null)
            {
                // Gán Spawn Point cho Player
                survival.spawnPoint = spawnPoint;
                
                // Dịch chuyển Player tới đây ngay lập tức
                player.transform.position = spawnPoint.position;
                player.transform.rotation = spawnPoint.rotation;
                
                Debug.Log("Đã tự động dịch chuyển Player về Căn Cứ An Toàn!");
            }
        }
        else
        {
            Debug.LogWarning("Không tìm thấy object nào có tag 'Player' trong Scene để dịch chuyển về căn cứ.");
        }
    }
}
