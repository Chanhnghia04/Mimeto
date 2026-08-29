using UnityEngine;
using UnityEditor;

public class CreateFlashlightScrap
{
    [MenuItem("Tools/Create Flashlight Scrap")]
    public static void CreateScrap()
    {
        GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        instance.name = "Flashlight";
        instance.transform.localScale = new Vector3(0.1f, 0.3f, 0.1f);
        
        // Add ScrapItem
        ScrapItem scrap = instance.AddComponent<ScrapItem>();
        scrap.scrapType = "flashlight";
        scrap.amount = 1;
        scrap.interactHint = "Press [E] to pick up Flashlight";
        
        // Save to Resources
        string targetPath = "Assets/Resources/Items/Flashlight.prefab";
        PrefabUtility.SaveAsPrefabAsset(instance, targetPath);
        GameObject.DestroyImmediate(instance);
        
        Debug.Log($"Created {targetPath}");
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
