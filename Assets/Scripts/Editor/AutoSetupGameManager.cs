using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class AutoSetupGameManager
{
    static AutoSetupGameManager()
    {
        EditorApplication.delayCall += DoSetup;
    }

    [MenuItem("Tools/Mimeto/Setup GameManager")]
    public static void ManualSetup()
    {
        DoSetupInternal();
    }

    static void DoSetup()
    {
        if (EditorPrefs.GetBool("AutoSetupGameManager_Done", false)) return;
        DoSetupInternal();
        EditorPrefs.SetBool("AutoSetupGameManager_Done", true);
    }

    static void DoSetupInternal()
    {
        // 1. Tìm hoặc tạo GameManager
        GameObject gm = GameObject.Find("GameManager");
        if (gm == null)
        {
            gm = new GameObject("GameManager");
        }

        // 2. Gắn script EscapeRandomizer nếu chưa có
        if (gm.GetComponent<EscapeRandomizer>() == null)
        {
            var randomizer = gm.AddComponent<EscapeRandomizer>();
            randomizer.mapRadius = 50f; // Mặc định bán kính 50m
            
            Selection.activeGameObject = gm;
            Debug.Log("<color=green>[Thành Công] Đã tự động tạo GameManager và gắn script Randomizer cho bạn!</color>");
        }
    }
}
