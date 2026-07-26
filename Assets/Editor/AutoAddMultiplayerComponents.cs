using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using Unity.Netcode.Components;

[InitializeOnLoad]
public class AutoAddMultiplayerComponents
{
    static AutoAddMultiplayerComponents()
    {
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
    }

    private static void OnHierarchyChanged()
    {
        // Khi người dùng kéo thả object mới vào Hierarchy, hàm này sẽ chạy.
        // Tìm các object chưa có NetworkObject nhưng cần thiết phải đồng bộ.
        
        GameObject[] allObjects = Object.FindObjectsOfType<GameObject>();
        foreach (GameObject go in allObjects)
        {
            if (go.isStatic) continue;

            string n = go.name.ToLower();
            bool isDynamicProp = n.Contains("barrel") || n.Contains("thùng") || 
                                 n.Contains("box") || n.Contains("obstacle") || 
                                 n.Contains("chướng ngại") || n.Contains("monster") || 
                                 n.Contains("quái") || n.Contains("enemy") || 
                                 n.Contains("chest") || n.Contains("hòm") ||
                                 n.Contains("door") || n.Contains("cửa");
            
            // Nếu là vật thể động hoặc có Rigidbody (cần vật lý/di chuyển)
            if (isDynamicProp || go.GetComponent<Rigidbody>() != null)
            {
                bool modified = false;
                if (go.GetComponent<NetworkObject>() == null)
                {
                    go.AddComponent<NetworkObject>();
                    modified = true;
                }
                
                if (go.GetComponent<NetworkTransform>() == null)
                {
                    go.AddComponent<NetworkTransform>();
                    modified = true;
                }

                if (modified)
                {
                    Debug.Log($"[Auto Multiplayer] Đã tự động thêm NetworkObject và NetworkTransform cho {go.name}");
                }
            }
        }
    }
}
