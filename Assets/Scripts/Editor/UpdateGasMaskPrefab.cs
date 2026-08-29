using UnityEngine;
using UnityEditor;

public class UpdateGasMaskPrefab
{
    [MenuItem("Tools/Update Gas Mask Prefab")]
    public static void UpdatePrefab()
    {
        string[] masks = { "basic_gasmask", "adv_gasmask" };
        GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Item/gas-mask-and-helmet/source/Extracted/Gasmask.obj");
        
        if (modelPrefab == null)
        {
            Debug.LogError("Could not find Gasmask.obj");
            return;
        }

        foreach (string mask in masks)
        {
            string path = $"Assets/Resources/Items/{mask}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            if (prefab == null) continue;
            
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            
            // Remove old visual components
            MeshFilter mf = instance.GetComponent<MeshFilter>();
            if (mf != null) Object.DestroyImmediate(mf);
            
            MeshRenderer mr = instance.GetComponent<MeshRenderer>();
            if (mr != null) Object.DestroyImmediate(mr);
            
            // Delete child objects if they exist to avoid duplicate meshes
            for (int i = instance.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(instance.transform.GetChild(i).gameObject);
            }
            
            // Add the new model as a child
            GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            modelInstance.transform.SetParent(instance.transform);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            
            // Fix scale since OBJs can be huge
            modelInstance.transform.localScale = new Vector3(1f, 1f, 1f);
            
            // Apply textures
            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Item/gas-mask-and-helmet/textures/Gasmask_BaseColor.png");
            Material gasMaskMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Item/gas-mask-and-helmet/GasMaskMat.mat");
            if (gasMaskMat == null)
            {
                gasMaskMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(gasMaskMat, "Assets/Models/Item/gas-mask-and-helmet/GasMaskMat.mat");
            }
            else
            {
                gasMaskMat.shader = Shader.Find("Universal Render Pipeline/Lit");
            }
            if (baseColor != null)
            {
                gasMaskMat.SetTexture("_BaseMap", baseColor);
            }
            
            Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Item/gas-mask-and-helmet/textures/Gasmask_Normal.png");
            if (normalMap != null)
            {
                gasMaskMat.EnableKeyword("_NORMALMAP");
                gasMaskMat.SetTexture("_BumpMap", normalMap);
            }

            MeshRenderer[] renderers = modelInstance.GetComponentsInChildren<MeshRenderer>();
            foreach (var r in renderers)
            {
                // Assign the material to all slots just to be safe
                Material[] newMats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < newMats.Length; i++) newMats[i] = gasMaskMat;
                r.sharedMaterials = newMats;
            }
            
            // Adjust BoxCollider
            BoxCollider col = instance.GetComponent<BoxCollider>();
            if (col != null)
            {
                col.center = Vector3.zero;
                col.size = new Vector3(0.5f, 0.5f, 0.5f);
            }
            
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            
            Debug.Log($"Updated {path} with proper Gasmask model.");
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
