using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Linq;

[InitializeOnLoad]
public class UpdateMimicWalk
{
    static UpdateMimicWalk()
    {
        EditorApplication.delayCall += RunFix;
    }

    static void RunFix()
    {
        if (EditorPrefs.GetBool("UpdateMimicWalk_Done_v1", false)) return;

        try
        {
            string path = "Assets/AI Toolkit/Mimic/MimicAnimator.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                Debug.LogWarning("Không tìm thấy MimicAnimator.controller!");
                return;
            }

            var walkClip = AssetDatabase.LoadAllAssetsAtPath("Assets/AI Toolkit/Mimic/Vampire A Lusth@Drunk Walk.fbx")
                .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
            var runClip = AssetDatabase.LoadAllAssetsAtPath("Assets/AI Toolkit/Mimic/Vampire A Lusth@Zombie Run.fbx")
                .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
            var attackClip = AssetDatabase.LoadAllAssetsAtPath("Assets/AI Toolkit/Mimic/Vampire A Lusth@Mutant Punch.fbx")
                .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
            var idleClip = AssetDatabase.LoadAllAssetsAtPath("Assets/AI Toolkit/Mimic/Mimic.fbx")
                .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));

            if (walkClip == null)
            {
                Debug.LogWarning("Không tìm thấy animation Walk (Vampire A Lusth@Drunk Walk.fbx)!");
                return;
            }

            var sm = controller.layers[0].stateMachine;

            // Xoá các state cũ
            sm.states = new ChildAnimatorState[0];
            sm.anyStateTransitions = new AnimatorStateTransition[0];

            if (!controller.parameters.Any(p => p.name == "Speed"))
                controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            if (!controller.parameters.Any(p => p.name == "Attack"))
                controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

            var idleState = sm.AddState("Idle");
            if (idleClip != null) idleState.motion = idleClip;

            var walkState = sm.AddState("Walk");
            walkState.motion = walkClip;

            var runState = sm.AddState("Run");
            if (runClip != null) runState.motion = runClip;

            var attackState = sm.AddState("Attack");
            if (attackClip != null) attackState.motion = attackClip;

            sm.defaultState = idleState;

            // Idle <-> Walk
            var i2w = idleState.AddTransition(walkState);
            i2w.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            i2w.hasExitTime = false;

            var w2i = walkState.AddTransition(idleState);
            w2i.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            w2i.hasExitTime = false;

            // Walk <-> Run
            var w2r = walkState.AddTransition(runState);
            w2r.AddCondition(AnimatorConditionMode.Greater, 4f, "Speed");
            w2r.hasExitTime = false;

            var r2w = runState.AddTransition(walkState);
            r2w.AddCondition(AnimatorConditionMode.Less, 4f, "Speed");
            r2w.hasExitTime = false;
            
            // Run -> Idle (khi dừng đột ngột)
            var r2i = runState.AddTransition(idleState);
            r2i.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            r2i.hasExitTime = false;

            // Any -> Attack
            var any2att = sm.AddAnyStateTransition(attackState);
            any2att.AddCondition(AnimatorConditionMode.If, 0, "Attack");

            var att2idle = attackState.AddTransition(idleState);
            att2idle.hasExitTime = true;
            att2idle.exitTime = 1f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log("<color=green>[Mimic Update] Đã thêm animation Đi bộ (Walk) vào MimicAnimator thành công!</color>");
            EditorPrefs.SetBool("UpdateMimicWalk_Done_v1", true);
        }
        catch (System.Exception e)
        {
            Debug.LogError("UpdateMimicWalk Error: " + e.Message);
        }
    }
}
