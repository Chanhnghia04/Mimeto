using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class SetupMimicAnimatorAuto
{
    [UnityEditor.Callbacks.DidReloadScripts]
    [MenuItem("Tools/Setup Mimic Animator")]
    public static void SetupAnimator()
    {
        // Kiểm tra xem đã chạy chưa để tránh lặp lại mỗi lần compile
        if (EditorPrefs.GetBool("SetupMimicAnimatorDone", false)) return;

        string controllerPath = "Assets/AI Toolkit/Mimic/MimicAnimator.controller";
        
        // Find animation clips from the uploaded FBXs
        AnimationClip runClip = GetClip("Assets/AI Toolkit/Mimic/Vampire A Lusth@Zombie Run.fbx");
        AnimationClip attackClip = GetClip("Assets/AI Toolkit/Mimic/Vampire A Lusth@Mutant Punch.fbx");
        AnimationClip idleClip = GetClip("Assets/AI Toolkit/Mimic/Mimic.fbx"); 

        if (runClip == null || attackClip == null)
        {
            return; // Đợi đến khi có đủ file
        }

        // Create the Animator Controller
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        
        // Add Parameters
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        // State Machine setup
        var rootStateMachine = controller.layers[0].stateMachine;

        var idleState = rootStateMachine.AddState("Idle");
        if (idleClip != null) idleState.motion = idleClip;

        var runState = rootStateMachine.AddState("Run");
        runState.motion = runClip;

        var attackState = rootStateMachine.AddState("Attack");
        attackState.motion = attackClip;

        // Transitions
        var idleToRun = idleState.AddTransition(runState);
        idleToRun.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Greater, 0.1f, "Speed");

        var runToIdle = runState.AddTransition(idleState);
        runToIdle.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Less, 0.1f, "Speed");

        var anyToAttack = rootStateMachine.AddAnyStateTransition(attackState);
        anyToAttack.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Attack");

        var attackToIdle = attackState.AddTransition(idleState);
        attackToIdle.hasExitTime = true;
        attackToIdle.exitTime = 1f;

        // Attach controller to the Mimic prefab
        string prefabPath = "Assets/Prefabs/Mimic.prefab";
        GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
        Animator anim = prefabContents.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.runtimeAnimatorController = controller;
            PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
            Debug.Log("TỰ ĐỘNG TẠO ANIMATOR VÀ GẮN VÀO MIMIC THÀNH CÔNG!");
            EditorPrefs.SetBool("SetupMimicAnimatorDone", true);
        }
        PrefabUtility.UnloadPrefabContents(prefabContents);
    }

    static AnimationClip GetClip(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (Object asset in assets)
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                return clip;
            }
        }
        return null;
    }
}
