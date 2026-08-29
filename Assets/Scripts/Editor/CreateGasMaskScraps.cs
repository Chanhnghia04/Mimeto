using UnityEngine;
using UnityEditor;

public class CreateGasMaskScraps
{
    [MenuItem("Tools/Create Gas Mask Scraps")]
    public static void CreateScraps()
    {
        string[] masks = { "basic_gasmask", "adv_gasmask", "antidote" };
        
        foreach (string mask in masks)
        {
            GameObject instance = null;
            if (mask == "antidote")
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/Antidote.prefab");
                if (src != null) instance = (GameObject)PrefabUtility.InstantiatePrefab(src);
            }
            if (instance == null)
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            }
            
            instance.name = mask;
            
            // Add ScrapItem
            ScrapItem scrap = instance.GetComponent<ScrapItem>();
            if (scrap == null) scrap = instance.AddComponent<ScrapItem>();
            
            scrap.scrapType = mask;
            scrap.amount = 1;
            scrap.interactHint = $"Press [E] to pick up {mask}";
            
            if (instance.GetComponent<BoxCollider>() == null)
                instance.AddComponent<BoxCollider>();

            // Save to Resources
            string targetPath = $"Assets/Resources/Items/{mask}.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, targetPath);
            GameObject.DestroyImmediate(instance);
            
            Debug.Log($"Created {targetPath}");
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
