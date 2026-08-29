
using UnityEngine;
using UnityEditor;

public class TestRenderers
{
    [InitializeOnLoadMethod]
    public static void Test()
    {
        GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        Transform model = go.transform.Find("Model");
        if (model != null) {
            Renderer[] rs = model.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rs)
            {
                Debug.Log("[RendererFinder] " + r.gameObject.name);
            }
        }
    }
}

