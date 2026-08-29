using UnityEngine;
using UnityEditor;

public class UpdateFaceMaskPrefab
{
    [MenuItem("Tools/Update Face Mask Equipment")]
    public static void UpdateEquipment()
    {
        // 1. Create a proper prefab for the equipped Gas Mask
        GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Item/gas-mask-and-helmet/source/Extracted/Gasmask.obj");
        Material gasMaskMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Item/gas-mask-and-helmet/GasMaskMat.mat");
        
        if (modelPrefab == null || gasMaskMat == null)
        {
            Debug.LogError("Could not find Gasmask.obj or GasMaskMat.mat");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
        
        MeshRenderer[] renderers = instance.GetComponentsInChildren<MeshRenderer>();
        foreach (var r in renderers)
        {
            Material[] newMats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < newMats.Length; i++) newMats[i] = gasMaskMat;
            r.sharedMaterials = newMats;
        }
        
        string targetPath = "Assets/Prefabs/Items/GasMaskEquipment.prefab";
        GameObject newPrefab = PrefabUtility.SaveAsPrefabAsset(instance, targetPath);
        Object.DestroyImmediate(instance);
        
        // 2. Update the Player prefab to use this new prefab instead of the raw .obj
        string playerPath = "Assets/Prefabs/Player.prefab";
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
        if (playerPrefab != null)
        {
            using (var editingScope = new PrefabUtility.EditPrefabContentsScope(playerPath))
            {
                var prefabRoot = editingScope.prefabContentsRoot;
                var eqManager = prefabRoot.GetComponentInChildren<EquipmentManager>();
                if (eqManager != null)
                {
                    eqManager.gasMaskPrefab = newPrefab;
                    Debug.Log("Updated Player's EquipmentManager to use GasMaskEquipment.prefab!");
                }
            }
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
