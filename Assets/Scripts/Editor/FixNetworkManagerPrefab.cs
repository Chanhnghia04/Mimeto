using UnityEngine;
using UnityEditor;
using Unity.Netcode;

public class FixNetworkManagerPrefab
{
    [InitializeOnLoadMethod]
    static void Run()
    {
        // NetworkManager lives under Prefabs/UI in this project.  The old path
        // silently made this auto-fix a no-op on every editor reload.
        const string networkManagerPath = "Assets/Prefabs/UI/NetworkManager.prefab";
        var nmObj = AssetDatabase.LoadAssetAtPath<GameObject>(networkManagerPath);
        if(nmObj != null)
        {
            var nm = nmObj.GetComponent<NetworkManager>();
            
            var mimic = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mimic.prefab");
            var mutant = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AI Toolkit/Enami/EnamiMutant.prefab");

            bool changed = false;

            if (mimic != null && !nm.NetworkConfig.Prefabs.Contains(mimic))
            {
                nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = mimic });
                changed = true;
            }

            if (mutant != null && !nm.NetworkConfig.Prefabs.Contains(mutant))
            {
                nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = mutant });
                changed = true;
            }

            // Đồng thời gài luôn file DefaultNetworkPrefabs vào nếu chưa có
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (list != null && !nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(list))
            {
                nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
                changed = true;
            }

            if(changed)
            {
                EditorUtility.SetDirty(nmObj);
                PrefabUtility.SavePrefabAsset(nmObj);
                Debug.Log($"[FixNetworkManagerPrefab] Updated {networkManagerPath} with network prefabs.");
            }
        }
    }
}
