using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class SetupComboPunchAnimator : Editor
{
    [MenuItem("Tools/Setup Combo Punch Animator")]
    public static void SetupAnimator()
    {
        string fbxPath = "Assets/Models/Player/Combo Punch.fbx";
        
        // 1. CHIA ĐÔI ANIMATION TRONG FBX
        ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer != null)
        {
            var takes = importer.importedTakeInfos;
            if (takes != null && takes.Length > 0)
            {
                var take = takes[0];
                float duration = take.stopTime - take.startTime;
                float fps = take.sampleRate;
                if (fps <= 0f) fps = 30f;
                int totalFrames = Mathf.RoundToInt(duration * fps);
                int midFrame = totalFrames / 2;

                ModelImporterClipAnimation c1 = new ModelImporterClipAnimation();
                c1.name = "Punch1";
                c1.firstFrame = 0;
                c1.lastFrame = midFrame + 2;
                c1.loopTime = false;

                ModelImporterClipAnimation c2 = new ModelImporterClipAnimation();
                c2.name = "Punch2";
                c2.firstFrame = midFrame - 2;
                c2.lastFrame = totalFrames;
                c2.loopTime = false;

                importer.clipAnimations = new[] { c1, c2 };
                importer.SaveAndReimport();
                Debug.Log($"Đã cắt FBX thành Punch1 và Punch2");
            }
        }

        AssetDatabase.Refresh();

        // 2. SETUP LẠI ANIMATOR
        string controllerPath = "Assets/Models/Player/PlayerAnimatorController.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

        if (controller == null) return;

        AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;

        AnimationClip clip1 = null;
        AnimationClip clip2 = null;
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        foreach (Object asset in assets)
        {
            if (asset is AnimationClip)
            {
                if (asset.name == "Punch1") clip1 = asset as AnimationClip;
                if (asset.name == "Punch2") clip2 = asset as AnimationClip;
            }
        }

        foreach (var state in rootStateMachine.states)
        {
            if (state.state.name == "Punch1" && clip1 != null)
            {
                state.state.motion = clip1;
            }
            if (state.state.name == "Punch2" && clip2 != null)
            {
                state.state.motion = clip2;
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("Gán Animation thành công! Đã sửa lỗi Console.");
    }
}
