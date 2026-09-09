
using UnityEngine;
using UnityEditor;

public class TestRenderers
{
    // This is a diagnostic helper, not an editor startup hook.  Running it on
    // every domain reload spammed the Console and could run before assets were
    // imported.  Use the menu item when renderer names are actually needed.
    [MenuItem("Tools/Mimeto/Diagnostics/Log Player Renderers")]
    public static void Test()
    {
        GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        // The asset may still be unavailable while Unity is importing.  Keep
        // the diagnostic command harmless in that case.
        if (go == null)
            return;

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

