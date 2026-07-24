using UnityEngine;
using UnityEditor;

public class AutoSetupAtmosphere : MonoBehaviour
{
    [MenuItem("Setup/Create InfoBoard and RestBench")]
    public static void CreateObjects()
    {
        var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.name = "InfoBoard";
        board.AddComponent<InfoBoard>();
        board.transform.position = new Vector3(5, 1, -5);
        board.transform.localScale = new Vector3(2, 2, 0.2f);
        board.GetComponent<Renderer>().material.color = Color.cyan;
        board.GetComponent<BoxCollider>().isTrigger = false;

        var bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bench.name = "RestBench";
        bench.AddComponent<RestBench>();
        bench.transform.position = new Vector3(-5, 0.5f, -5);
        bench.transform.localScale = new Vector3(3, 0.5f, 1);
        bench.GetComponent<Renderer>().material.color = Color.gray;
        bench.GetComponent<BoxCollider>().isTrigger = false;

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
}
