using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Unity.Netcode;

public class FixProjectIssues : EditorWindow
{
    [MenuItem("Tools/Fix UI and Mutant Issues")]
    public static void FixIssues()
    {
        string originalScene = EditorSceneManager.GetActiveScene().path;

        try
        {
            // 1. Fix Graphics Settings (Mutant Shader)
            FixMutantShader();

            // 2. Fix Network Manager Prefabs just in case
            FixNetworkManager();

            // 3. Fix Canvases
            FixAllCanvases();

            Debug.Log("<color=lime>Đã sửa xong toàn bộ lỗi UI và Mutant!</color>");
        }
        finally
        {
            // Trả lại Scene ban đầu cho user
            if (!string.IsNullOrEmpty(originalScene))
            {
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
            }
        }
    }

    static void FixMutantShader()
    {
        var mutant = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AI Toolkit/Enami/EnamiMutant.prefab");
        if (mutant != null)
        {
            Renderer[] renderers = mutant.GetComponentsInChildren<Renderer>(true);
            HashSet<Shader> shaders = new HashSet<Shader>();
            foreach (var r in renderers)
            {
                if (r.sharedMaterial != null && r.sharedMaterial.shader != null)
                {
                    shaders.Add(r.sharedMaterial.shader);
                }
            }

            var graphicsSettingsObj = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            if (graphicsSettingsObj != null)
            {
                SerializedObject serializedObject = new SerializedObject(graphicsSettingsObj);
                SerializedProperty arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");

                bool changed = false;
                foreach (var shader in shaders)
                {
                    bool hasShader = false;
                    for (int i = 0; i < arrayProp.arraySize; ++i)
                    {
                        if (arrayProp.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                        {
                            hasShader = true;
                            break;
                        }
                    }

                    if (!hasShader)
                    {
                        arrayProp.InsertArrayElementAtIndex(arrayProp.arraySize);
                        arrayProp.GetArrayElementAtIndex(arrayProp.arraySize - 1).objectReferenceValue = shader;
                        changed = true;
                    }
                }

                if (changed)
                {
                    serializedObject.ApplyModifiedProperties();
                    Debug.Log("[AutoFix] Đã thêm Shader của Mutant vào Always Included Shaders.");
                }
            }
        }
    }

    static void FixNetworkManager()
    {
        string[] nmScenes = { "Assets/Scenes/StartGame.unity", "Assets/Scenes/Waiting.unity", "Assets/Scenes/Map.unity" };
        foreach (var path in nmScenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var nm = Object.FindAnyObjectByType<NetworkManager>();
            if (nm == null) continue;

            var mutant = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AI Toolkit/Enami/EnamiMutant.prefab");
            var mimic = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mimic.prefab");
            bool changed = false;

            if (mutant != null && !nm.NetworkConfig.Prefabs.Contains(mutant))
            {
                nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = mutant });
                changed = true;
            }
            if (mimic != null && !nm.NetworkConfig.Prefabs.Contains(mimic))
            {
                nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = mimic });
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(nm);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[AutoFix] Đã đăng ký Mutant vào NetworkManager trong scene {scene.name}.");
            }
        }
    }

    static void FixAllCanvases()
    {
        string[] scenes = { "Assets/Scenes/StartGame.unity", "Assets/Scenes/Waiting.unity", "Assets/Scenes/Map.unity" };
        foreach (var path in scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool changed = false;
            CanvasScaler[] scalers = Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var scaler in scalers)
            {
                if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920, 1080);
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = 0.5f;
                    EditorUtility.SetDirty(scaler);
                    changed = true;
                }
            }
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[AutoFix] Đã sửa UI Canvas Scaler trong scene {scene.name}.");
            }
        }
    }
}
