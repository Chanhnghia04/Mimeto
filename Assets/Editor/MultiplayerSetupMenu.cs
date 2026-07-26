using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using Unity.Netcode.Components;

public class MultiplayerSetupMenu : MonoBehaviour
{
    [MenuItem("Tools/Multiplayer/Add NetworkObject \u0026 NetworkTransform")]
    public static void AddMultiplayerComponents()
    {
        int count = 0;
        foreach (GameObject go in Selection.gameObjects)
        {
            Undo.RecordObject(go, "Add Network Components");
            bool added = false;
            
            if (go.GetComponent<NetworkObject>() == null)
            {
                go.AddComponent<NetworkObject>();
                added = true;
            }
            
            if (go.GetComponent<NetworkTransform>() == null)
            {
                go.AddComponent<NetworkTransform>();
                added = true;
            }

            if (added)
            {
                EditorUtility.SetDirty(go);
                count++;
            }
        }
        
        Debug.Log($"Added NetworkObject and NetworkTransform to {count} GameObjects.");
    }

    [MenuItem("Tools/Multiplayer/Add NetworkObject \u0026 NetworkTransform", true)]
    public static bool ValidateAddMultiplayerComponents()
    {
        return Selection.gameObjects.Length > 0;
    }
}
