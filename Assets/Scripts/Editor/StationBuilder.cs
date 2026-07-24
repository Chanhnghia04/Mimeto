using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class StationBuilder
{
    [MenuItem("Mimeto/Build Stations")]
    public static void BuildStations()
    {
        // Find existing stations and delete them to rebuild
        var existingSell = GameObject.Find("SellStation");
        if (existingSell != null) GameObject.DestroyImmediate(existingSell);
        
        var existingShop = GameObject.Find("ShopStation");
        if (existingShop != null) GameObject.DestroyImmediate(existingShop);

        // Build Sell Station
        GameObject sellStation = new GameObject("SellStation");
        sellStation.transform.position = new Vector3(-2, 0, 3);
        
        // Base
        GameObject sellBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        sellBase.transform.parent = sellStation.transform;
        sellBase.transform.localPosition = new Vector3(0, 0.5f, 0);
        sellBase.transform.localScale = new Vector3(1.5f, 0.5f, 1.5f);
        sellBase.GetComponent<Renderer>().sharedMaterial = GetMaterial(Color.gray, 0.1f);
        
        // Core
        GameObject sellCore = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sellCore.transform.parent = sellStation.transform;
        sellCore.transform.localPosition = new Vector3(0, 1.25f, 0);
        sellCore.transform.localScale = new Vector3(1f, 1f, 1f);
        sellCore.GetComponent<Renderer>().sharedMaterial = GetMaterial(new Color(0.2f, 0.2f, 0.3f), 0.5f);

        // Hologram Light
        GameObject sellLight = new GameObject("Light");
        sellLight.transform.parent = sellStation.transform;
        sellLight.transform.localPosition = new Vector3(0, 2f, 0);
        Light sl = sellLight.AddComponent<Light>();
        sl.type = LightType.Point;
        sl.color = Color.green;
        sl.range = 3f;
        sl.intensity = 2f;

        // Add Logic
        BoxCollider sellCol = sellStation.AddComponent<BoxCollider>();
        sellCol.size = new Vector3(2, 3, 2);
        sellCol.center = new Vector3(0, 1.5f, 0);
        sellCol.isTrigger = false;
        sellStation.AddComponent<ScrapSellStation>();
        
        // (Floating Text removed per user request)

        // Build Shop Station
        GameObject shopStation = new GameObject("ShopStation");
        shopStation.transform.position = new Vector3(2, 0, 3);
        
        // Base
        GameObject shopBase = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shopBase.transform.parent = shopStation.transform;
        shopBase.transform.localPosition = new Vector3(0, 0.5f, 0);
        shopBase.transform.localScale = new Vector3(2f, 1f, 1f);
        shopBase.GetComponent<Renderer>().sharedMaterial = GetMaterial(Color.gray, 0.1f);
        
        // Screen
        GameObject shopScreen = GameObject.CreatePrimitive(PrimitiveType.Quad);
        shopScreen.transform.parent = shopStation.transform;
        shopScreen.transform.localPosition = new Vector3(0, 1.5f, -0.51f);
        shopScreen.transform.localScale = new Vector3(1.8f, 1f, 1f);
        shopScreen.GetComponent<Renderer>().sharedMaterial = GetMaterial(Color.black, 1f);

        // Light
        GameObject shopLight = new GameObject("Light");
        shopLight.transform.parent = shopStation.transform;
        shopLight.transform.localPosition = new Vector3(0, 1.5f, -0.6f);
        Light shL = shopLight.AddComponent<Light>();
        shL.type = LightType.Point;
        shL.color = Color.cyan;
        shL.range = 3f;
        shL.intensity = 1.5f;

        // Add Logic
        BoxCollider shopCol = shopStation.AddComponent<BoxCollider>();
        shopCol.size = new Vector3(2, 3, 2);
        shopCol.center = new Vector3(0, 1.5f, 0);
        shopCol.isTrigger = false;
        var shopScript = shopStation.AddComponent<ShopStation>();
        
        // (Floating Text removed per user request)

        // Trigger UI Setup removed since UI was deleted

        Debug.Log("Sci-Fi Stations Built!");
    }

    private static Material GetMaterial(Color color, float metallic)
    {
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Glossiness", 0.8f);
        return mat;
    }
}
