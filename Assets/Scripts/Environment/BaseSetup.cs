using System.Collections;
using UnityEngine;

public class BaseSetup : MonoBehaviour
{
    [Tooltip("Vị trí Player sẽ xuất hiện khi vào game")]
    public Transform spawnPoint;

    private IEnumerator Start()
    {
        // Chờ NetworkManager và Local PlayerObject được spawn
        float timeout = 10f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (Unity.Netcode.NetworkManager.Singleton != null &&
                Unity.Netcode.NetworkManager.Singleton.LocalClient != null &&
                Unity.Netcode.NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Tự động tìm Player trong scene
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.LocalClient?.PlayerObject != null)
        {
            GameObject player = Unity.Netcode.NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;
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
            Debug.LogWarning("Không tìm thấy LocalClient.PlayerObject để dịch chuyển về căn cứ.");
        }
    }
}
