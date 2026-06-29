using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Linq;

public class AnimationSetup
{
    public static void SetupPlayerAnimator()
    {
        string folder = "Assets/Models/Player/";
        string controllerPath = "Assets/Models/Player/PlayerAnimatorController.controller";

        // 1. Create Controller
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        
        // 2. Add Parameters
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsCrouching", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

        var rootLayer = controller.layers[0];
        var stateMachine = rootLayer.stateMachine;

        // 3. Load Clips
        AnimationClip idleClip = LoadClip(folder + "Ch42_nonPBR@Standard Idle.fbx");
        AnimationClip walkClip = LoadClip(folder + "Ch42_nonPBR@Walking.fbx");
        AnimationClip runClip = LoadClip(folder + "Ch42_nonPBR@Fast Run.fbx");
        AnimationClip sneakClip = LoadClip(folder + "Ch42_nonPBR@Sneaking Forward.fbx");
        AnimationClip airClip = LoadClip(folder + "Ch42_nonPBR@Male Locomotion Pose.fbx"); // Fallback for air

        // 4. Create Locomotion Blend Tree
        BlendTree blendTree;
        AnimatorState locomotionState = controller.CreateBlendTreeInController("Locomotion", out blendTree);
        blendTree.blendType = BlendTreeType.Simple1D;
        blendTree.blendParameter = "Speed";
        
        if (idleClip != null) blendTree.AddChild(idleClip, 0f);
        if (walkClip != null) blendTree.AddChild(walkClip, 5f);
        if (runClip != null) blendTree.AddChild(runClip, 8f);

        // 5. Create Sneak State
        AnimatorState sneakState = stateMachine.AddState("Sneak", new Vector3(300, 0, 0));
        sneakState.motion = sneakClip;

        // 6. Create Air State
        AnimatorState airState = stateMachine.AddState("InAir", new Vector3(300, 200, 0));
        airState.motion = airClip;

        // 7. Transitions
        
        // Locomotion <-> Sneak
        var walkToSneak = locomotionState.AddTransition(sneakState);
        walkToSneak.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
        walkToSneak.duration = 0.2f;

        var sneakToWalk = sneakState.AddTransition(locomotionState);
        sneakToWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
        sneakToWalk.duration = 0.2f;

        // Grounded -> InAir
        var walkToAir = locomotionState.AddTransition(airState);
        walkToAir.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
        walkToAir.duration = 0.1f;

        var sneakToAir = sneakState.AddTransition(airState);
        sneakToAir.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
        sneakToAir.duration = 0.1f;

        // InAir -> Grounded
        var airToWalk = airState.AddTransition(locomotionState);
        airToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        airToWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
        airToWalk.duration = 0.1f;

        var airToSneak = airState.AddTransition(sneakState);
        airToSneak.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        airToSneak.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
        airToSneak.duration = 0.1f;

        // AnyState -> Air (via Jump Trigger)
        var jumpToAir = stateMachine.AddAnyStateTransition(airState);
        jumpToAir.AddCondition(AnimatorConditionMode.If, 0, "Jump");
        jumpToAir.duration = 0.05f;

        // 8. Assign to Player
        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            GameObject model = player.transform.Find("Model")?.gameObject;
            if (model != null)
            {
                Animator anim = model.GetComponent<Animator>();
                if (anim == null) anim = model.AddComponent<Animator>();
                anim.runtimeAnimatorController = controller;
                anim.avatar = AssetDatabase.LoadAllAssetsAtPath("Assets/Models/modelplayer.fbx").OfType<Avatar>().FirstOrDefault();
            }
        }
        
        AssetDatabase.SaveAssets();
        Debug.Log("Refined Animator Controller created and assigned!");
    }

    private static AnimationClip LoadClip(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.Contains("__preview__"));
    }
}
