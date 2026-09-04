using UnityEngine;
public class TestCompile : MonoBehaviour
{
    void Start()
    {
        var x = FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
    }
}
