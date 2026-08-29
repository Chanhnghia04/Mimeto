using UnityEngine;
using UnityEditor;

public class CreateToolScraps
{
    [MenuItem("Tools/Create Tool Scraps")]
    public static void CreateScraps()
    {
        string[] tools = { "Axe", "Machete", "Bat", "Crowbar", "Shovel", "Flashlight" };
        
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Items"))
            AssetDatabase.CreateFolder("Assets/Resources", "Items");

        foreach (string tool in tools)
        {
            string sourcePath = $"Assets/Models/Generated/{tool}.prefab";
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            
            if (sourcePrefab == null)
            {
                Debug.LogWarning($"Source prefab {tool} not found at {sourcePath}");
                continue;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
            
            // Add ScrapItem
            ScrapItem scrap = instance.GetComponent<ScrapItem>();
            if (scrap == null) scrap = instance.AddComponent<ScrapItem>();
            
            scrap.scrapType = tool.ToLower();
            scrap.amount = 1;
            scrap.interactHint = $"Press [E] to pick up {tool}";
            
            // BoxCollider is added by ScrapItem.RebuildCollider automatically, 
            // but we can add one just in case
            if (instance.GetComponent<BoxCollider>() == null)
                instance.AddComponent<BoxCollider>();

            // Save to Resources
            string targetPath = $"Assets/Resources/Items/{tool}.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, targetPath);
            GameObject.DestroyImmediate(instance);
            
            Debug.Log($"Created {targetPath}");
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
