using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class FixWaitingScene
{
    [MenuItem("Tools/Fix Waiting Scene")]
    public static void Fix()
    {
        string path = "Assets/Scenes/Waiting.unity";
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        
        if (Camera.main == null && GameObject.FindObjectOfType<Camera>() == null)
        {
            GameObject camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            Camera cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.blue;
            camGO.AddComponent<AudioListener>();
        }

        if (GameObject.Find("Canvas") == null)
        {
            GameObject canvasGO = new GameObject("Canvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(canvasGO.transform, false);
            var text = textGO.AddComponent<TMPro.TextMeshProUGUI>();
            text.text = "PHÒNG CH? (WAITING)";
            text.fontSize = 50;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.color = Color.white;
            
            RectTransform rect = textGO.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
        }

        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.OpenScene("Assets/Scenes/StartGame.unity", OpenSceneMode.Single);
        Debug.Log("Fixed Waiting Scene!");
    }
}
