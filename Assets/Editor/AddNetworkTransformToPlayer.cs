using UnityEditor;
using UnityEngine;
using Mimeto.Player;

public class AddNetworkTransformToPlayer
{
    [MenuItem("Tools/Add ClientNetworkTransform")]
    public static void AddComponent()
    {
        string path = "Assets/Prefabs/Player.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null)
        {
            if (prefab.GetComponent<ClientNetworkTransform>() == null)
            {
                prefab.AddComponent<ClientNetworkTransform>();
                EditorUtility.SetDirty(prefab);
                PrefabUtility.SavePrefabAsset(prefab);
                Debug.Log("Added ClientNetworkTransform to Player prefab successfully!");
            }
            else
            {
                Debug.Log("ClientNetworkTransform already exists.");
            }
        }
        else
        {
            Debug.LogError("Player prefab not found!");
        }
    }
}
