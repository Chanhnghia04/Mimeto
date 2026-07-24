using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Linq;

[InitializeOnLoad]
public class FixMimicAnimatorQuick
{
    static FixMimicAnimatorQuick()
    {
        EditorApplication.delayCall += RunFix;
    }

    static void RunFix()
    {
        try
        {
            string path = "Assets/AI Toolkit/Mimic/MimicAnimator.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                Debug.LogError("[Mimic Fix] Không tìm thấy MimicAnimator.controller ở " + path);
                return;
            }

            // 1. Sửa file Controller
            bool isModified = false;
            if (controller.layers.Length == 0)
            {
                controller.AddLayer("Base Layer");
                isModified = true;
            }

            if (!controller.parameters.Any(p => p.name == "Speed"))
            {
                controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
                controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

                var runClip = AssetDatabase.LoadAllAssetsAtPath("Assets/AI Toolkit/Mimic/Vampire A Lusth@Zombie Run.fbx")
                    .OfType<AnimationClip>().FirstOrDefault(c => !c.name.Contains("preview"));
                var attackClip = AssetDatabase.LoadAllAssetsAtPath("Assets/AI Toolkit/Mimic/Vampire A Lusth@Mutant Punch.fbx")
                    .OfType<AnimationClip>().FirstOrDefault(c => !c.name.Contains("preview"));

                if (runClip != null && attackClip != null)
                {
                    var sm = controller.layers[0].stateMachine;
                    var runState = sm.AddState("Zombie Run");
                    runState.motion = runClip;
                    sm.defaultState = runState;

                    var attackState = sm.AddState("Mutant Punch");
                    attackState.motion = attackClip;

                    var t1 = runState.AddTransition(attackState);
                    t1.hasExitTime = false;
                    t1.AddCondition(AnimatorConditionMode.If, 0, "Attack");

                    var t2 = attackState.AddTransition(runState);
                    t2.hasExitTime = true;

                    EditorUtility.SetDirty(controller);
                    isModified = true;
                    Debug.Log("[Mimic Fix] Đã set up các state và parameters cho Animator!");
                }
            }

            // 2. Sửa Prefab (Gắn controller vào Mimic)
            string prefabPath = "Assets/Prefabs/Mimic.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                var animator = prefab.GetComponentInChildren<Animator>(true);
                if (animator != null && animator.runtimeAnimatorController != controller)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    var instAnimator = instance.GetComponentInChildren<Animator>(true);
                    if (instAnimator != null)
                    {
                        instAnimator.runtimeAnimatorController = controller;
                        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                        isModified = true;
                        Debug.Log("[Mimic Fix] Đã gắn MimicAnimator.controller vào Mimic Prefab!");
                    }
                    Object.DestroyImmediate(instance);
                }
            }
            
            if (isModified)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[Mimic Fix] HOÀN TẤT: Đã sửa toàn bộ lỗi animation của Mimic!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Mimic Fix] Lỗi: " + e.Message);
        }
    }
}
