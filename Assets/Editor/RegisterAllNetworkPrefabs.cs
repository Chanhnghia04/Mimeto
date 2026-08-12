using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System.Collections.Generic;

public class RegisterAllNetworkPrefabs
{
    [MenuItem("Tools/Register All Network Prefabs")]
    public static void RegisterAll()
    {
        List<GameObject> allNetPrefabs = new List<GameObject>();
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Packages/")) continue;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.GetComponent<NetworkObject>() != null)
            {
                allNetPrefabs.Add(prefab);
            }
        }

        Debug.Log($"Found {allNetPrefabs.Count} prefabs with NetworkObject.");

        // 1. Update NetworkManager in StartGame scene
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/StartGame.unity", OpenSceneMode.Single);
        NetworkManager nm = GameObject.FindAnyObjectByType<NetworkManager>();
        if (nm != null)
        {
            if (nm.NetworkConfig.Prefabs == null)
            {
                nm.NetworkConfig.Prefabs = new NetworkPrefabs();
            }

            foreach (var prefab in allNetPrefabs)
            {
                if (!nm.NetworkConfig.Prefabs.Contains(prefab))
                {
                    nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
                    Debug.Log($"Added {prefab.name} to Scene NetworkManager");
                }
            }
            EditorUtility.SetDirty(nm);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Saved StartGame scene.");
        }

        // 2. Update NetworkManager prefab
        string nmPrefabPath = "Assets/Prefabs/UI/NetworkManager.prefab";
        GameObject nmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(nmPrefabPath);
        if (nmPrefab != null)
        {
            NetworkManager nmComp = nmPrefab.GetComponent<NetworkManager>();
            if (nmComp != null)
            {
                if (nmComp.NetworkConfig.Prefabs == null)
                {
                    nmComp.NetworkConfig.Prefabs = new NetworkPrefabs();
                }

                bool dirty = false;
                foreach (var prefab in allNetPrefabs)
                {
                    if (!nmComp.NetworkConfig.Prefabs.Contains(prefab))
                    {
                        nmComp.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
                        dirty = true;
                    }
                }

                if (dirty)
                {
                    PrefabUtility.SavePrefabAsset(nmPrefab);
                    Debug.Log("Updated NetworkManager prefab.");
                }
            }
        }

        // 3. Update all NetworkPrefabsList scriptable objects
        string[] listGuids = AssetDatabase.FindAssets("t:NetworkPrefabsList");
        foreach (string guid in listGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(path);
            if (list != null)
            {
                bool listDirty = false;
                foreach (var prefab in allNetPrefabs)
                {
                    if (!list.Contains(prefab))
                    {
                        list.Add(new NetworkPrefab { Prefab = prefab });
                        listDirty = true;
                        Debug.Log($"Added {prefab.name} to list {list.name}");
                    }
                }
                if (listDirty)
                {
                    EditorUtility.SetDirty(list);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        Debug.Log("DONE registering all Network Prefabs!");
    }
}
