using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class AutoFixNetworkManager
{
    [MenuItem("Tools/Auto Fix NetworkManager")]
    public static void Fix()
    {
        string prefabPath = "Assets/Prefabs/Player.prefab";
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (playerPrefab == null)
        {
            Debug.LogError("Không tìm th?y Player.prefab t?i " + prefabPath);
            return;
        }

        if (playerPrefab.GetComponent<NetworkObject>() == null)
        {
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            inst.AddComponent<NetworkObject>();
            PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
            GameObject.DestroyImmediate(inst);
            playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Debug.Log("Ðã t? d?ng thêm NetworkObject vào Player Prefab");
        }

        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/StartGame.unity", OpenSceneMode.Single);
        NetworkManager nm = GameObject.FindObjectOfType<NetworkManager>();
        
        if (nm != null)
        {
            nm.NetworkConfig.PlayerPrefab = playerPrefab;
            
            bool found = false;
            foreach (var pref in nm.NetworkConfig.Prefabs.Prefabs)
            {
                if (pref.Prefab == playerPrefab)
                {
                    found = true;
                    break;
                }
            }
            
            if (!found)
            {
                nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = playerPrefab });
                Debug.Log("Ðã thêm Player Prefab vào danh sách Network Prefabs");
            }
            
            EditorUtility.SetDirty(nm);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Ðã gán thành công Player Prefab và LUU SCENE StartGame!");
        }
    }
}
