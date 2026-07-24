using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor.SceneManagement;

public class MultiplayerSetupTool : Editor
{
    [MenuItem("Tools/Auto Setup Multiplayer")]
    public static void SetupMultiplayer()
    {
        // 1. Tìm Prefab Player trong thư mục Prefabs
        string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Prefabs" });
        GameObject playerPrefab = null;
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("Player") && path.EndsWith(".prefab"))
            {
                playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                break;
            }
        }

        if (playerPrefab == null)
        {
            Debug.LogError("Không tìm thấy file Player.prefab trong thư mục Assets/Prefabs.");
            return;
        }

        // 2. Gắn NetworkObject vào Player Prefab
        if (playerPrefab.GetComponent<NetworkObject>() == null)
        {
            playerPrefab.AddComponent<NetworkObject>();
            EditorUtility.SetDirty(playerPrefab);
            Debug.Log("Đã tự động thêm NetworkObject vào Player Prefab.");
        }

        // 3. Tạo NetworkManager trong Scene
        NetworkManager networkManager = Object.FindAnyObjectByType<NetworkManager>();
        if (networkManager == null)
        {
            GameObject nmObj = new GameObject("NetworkManager");
            networkManager = nmObj.AddComponent<NetworkManager>();
            
            UnityTransport transport = nmObj.AddComponent<UnityTransport>();
            networkManager.NetworkConfig = new NetworkConfig();
            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.PlayerPrefab = playerPrefab;
            
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Đã tự động tạo NetworkManager trong Scene và gắn UnityTransport.");
        }
        else
        {
            Debug.Log("NetworkManager đã có sẵn trong Scene. Tiến hành cập nhật Player Prefab...");
            if (networkManager.NetworkConfig.PlayerPrefab == null)
            {
                networkManager.NetworkConfig.PlayerPrefab = playerPrefab;
                EditorUtility.SetDirty(networkManager);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("HOÀN TẤT THIẾT LẬP MULTIPLAYER!");
    }
}
