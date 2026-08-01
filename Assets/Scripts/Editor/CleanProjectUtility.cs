#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CleanProjectUtility : EditorWindow
{
    [MenuItem("Tools/Mimeto/Clean Up Missing Scripts & Fix Colliders", priority = 100)]
    public static void CleanUp()
    {
        int missingScriptsRemoved = 0;
        int collidersFixed = 0;

        // 1. Clean Prefabs
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(prefab);
                if (count > 0)
                {
                    missingScriptsRemoved += count;
                    EditorUtility.SetDirty(prefab);
                }
            }
        }

        // 2. Clean Opened Scenes
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            GameObject[] rootObjects = scene.GetRootGameObjects();
            bool sceneDirty = false;

            foreach (GameObject root in rootObjects)
            {
                // Remove missing scripts
                Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allChildren)
                {
                    int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                    if (count > 0)
                    {
                        missingScriptsRemoved += count;
                        sceneDirty = true;
                    }
                    
                    // Fix BoxCollider with negative scale
                    if (t.name.Contains("BlackJack") || t.name.Contains("Master_model.glb"))
                    {
                        BoxCollider box = t.GetComponent<BoxCollider>();
                        if (box != null)
                        {
                            Vector3 lossyScale = t.lossyScale;
                            if (lossyScale.x < 0 || lossyScale.y < 0 || lossyScale.z < 0)
                            {
                                // Fix it by putting the BoxCollider on a new child with positive scale
                                Vector3 size = box.size;
                                Vector3 center = box.center;
                                Object.DestroyImmediate(box);
                                
                                GameObject colliderObj = new GameObject("BoxColliderFix");
                                colliderObj.transform.SetParent(t, false);
                                colliderObj.transform.localScale = new Vector3(
                                    Mathf.Abs(1f / t.localScale.x),
                                    Mathf.Abs(1f / t.localScale.y),
                                    Mathf.Abs(1f / t.localScale.z)
                                );
                                
                                BoxCollider newBox = colliderObj.AddComponent<BoxCollider>();
                                newBox.size = new Vector3(Mathf.Abs(size.x * lossyScale.x), Mathf.Abs(size.y * lossyScale.y), Mathf.Abs(size.z * lossyScale.z));
                                newBox.center = new Vector3(center.x * lossyScale.x, center.y * lossyScale.y, center.z * lossyScale.z);
                                
                                collidersFixed++;
                                sceneDirty = true;
                                Debug.Log($"[CleanProjectUtility] Fixed negative scale BoxCollider on {t.name} (Path: {GetPath(t)})");
                            }
                        }
                    }
                }
            }

            if (sceneDirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        
        AssetDatabase.SaveAssets();
        string msg = $"Cleaned up {missingScriptsRemoved} missing scripts.\nFixed {collidersFixed} colliders.\nAll scenes saved.";
        Debug.Log($"[CleanProjectUtility] {msg}");
        EditorUtility.DisplayDialog("Clean Project Complete", msg, "OK");
    }

    private static string GetPath(Transform current)
    {
        if (current.parent == null)
            return "/" + current.name;
        return GetPath(current.parent) + "/" + current.name;
    }
}
#endif
