using UnityEngine;
using UnityEditor;
using Unity.Netcode;
public class DumpNet
{
    [MenuItem("Tools/Dump NetConfig")]
    public static void Dump()
    {
        var go = new GameObject("Net");
        var nm = go.AddComponent<NetworkManager>();
        var so = new SerializedObject(nm);
        var config = so.FindProperty("NetworkConfig");
        var iter = config.Copy();
        var end = config.GetEndProperty();
        iter.NextVisible(true);
        while (!SerializedProperty.EqualContents(iter, end))
        {
            Debug.Log(iter.name + " (" + iter.propertyType + ")");
            iter.NextVisible(false);
        }
        GameObject.DestroyImmediate(go);
    }
}
