using UnityEngine;
using UnityEditor;
using Unity.Netcode;

public class FixNetworkPrefabs 
{
    [InitializeOnLoadMethod]
    static void Run() 
    {
        var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
        if(list == null) return;

        var mimic = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mimic.prefab");
        var mutant = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AI Toolkit/Enami/EnamiMutant.prefab");

        bool changed = false;
        
        if (mimic != null && !list.Contains(mimic)) 
        { 
            list.Add(new NetworkPrefab { Prefab = mimic }); 
            changed = true; 
        }
        
        if (mutant != null && !list.Contains(mutant)) 
        { 
            list.Add(new NetworkPrefab { Prefab = mutant }); 
            changed = true; 
        }
        
        if (changed) 
        {
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            Debug.Log("[FixNetworkPrefabs] Successfully registered Mimic and Mutant to DefaultNetworkPrefabs.asset!");
        }
    }
}
