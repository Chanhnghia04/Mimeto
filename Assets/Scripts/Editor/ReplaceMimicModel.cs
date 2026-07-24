using UnityEngine;
using UnityEditor;

public class ReplaceMimicModelAuto
{
    // Lệnh này sẽ tự động chạy ngay khi Unity compile xong (không cần bạn phải bấm gì cả)
    [UnityEditor.Callbacks.DidReloadScripts]
    [MenuItem("Tools/Replace Mimic Model")]
    public static void ReplaceModel()
    {
        string prefabPath = "Assets/Prefabs/Mimic.prefab";
        string fbxPath = "Assets/AI Toolkit/Mimic/Mimic.fbx";

        GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabContents == null) return;

        bool changed = false;

        // Xóa cục Capsule
        MeshFilter mf = prefabContents.GetComponent<MeshFilter>();
        if (mf != null) { Object.DestroyImmediate(mf); changed = true; }
        
        MeshRenderer mr = prefabContents.GetComponent<MeshRenderer>();
        if (mr != null) { Object.DestroyImmediate(mr); changed = true; }

        // Kiểm tra xem đã thêm model fbx chưa
        Transform existingModel = prefabContents.transform.Find("Mimic");
        if (existingModel == null)
        {
            // Thêm model FBX vào
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxAsset != null)
            {
                GameObject fbxInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
                fbxInstance.name = "Mimic";
                fbxInstance.transform.SetParent(prefabContents.transform, false);
                
                // Khung xương (Animator)
                Animator anim = fbxInstance.GetComponent<Animator>();
                if (anim == null) fbxInstance.AddComponent<Animator>();
                
                changed = true;
            }
        }

        if (changed)
        {
            PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
            Debug.Log("TỰ ĐỘNG THAY THẾ MÔ HÌNH MIMIC THÀNH CÔNG!");
        }

        PrefabUtility.UnloadPrefabContents(prefabContents);
    }
}
