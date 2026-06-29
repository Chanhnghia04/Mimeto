using UnityEngine;
using UnityEditor;
using System.IO;

public class IconGenerator : EditorWindow
{
    public GameObject targetModel;
    public int imageSize = 256;
    public Vector3 modelRotation = new Vector3(15f, 45f, 0f);

    [MenuItem("Tools/Mimeto/3D Model To UI Icon")]
    public static void ShowWindow()
    {
        GetWindow<IconGenerator>("Tạo Icon Từ 3D Model");
    }

    void OnGUI()
    {
        GUILayout.Label("Công cụ tự động chụp 3D Model thành Icon 2D (Sprite)", EditorStyles.boldLabel);
        GUILayout.Space(10);

        targetModel = (GameObject)EditorGUILayout.ObjectField("Kéo 3D Model vào đây", targetModel, typeof(GameObject), true);
        imageSize = EditorGUILayout.IntSlider("Kích thước ảnh", imageSize, 64, 1024);
        modelRotation = EditorGUILayout.Vector3Field("Góc xoay Model", modelRotation);

        GUILayout.Space(20);

        if (GUILayout.Button("Chụp Ảnh & Lưu Thành UI Sprite", GUILayout.Height(40)))
        {
            if (targetModel == null)
            {
                EditorUtility.DisplayDialog("Lỗi", "Vui lòng kéo một 3D Model vào ô trống!", "OK");
                return;
            }

            CreateIcon();
        }
    }

    void CreateIcon()
    {
        // Tạo một phòng chụp ảo ở xa tít để không dính cảnh
        Vector3 studioPos = new Vector3(0, -5000, 0);
        GameObject instance = Instantiate(targetModel, studioPos, Quaternion.Euler(modelRotation));
        
        // Tắt hết các script không cần thiết để tránh lỗi lúc chụp
        MonoBehaviour[] scripts = instance.GetComponentsInChildren<MonoBehaviour>();
        foreach (var script in scripts) DestroyImmediate(script);

        // Canh chỉnh Camera
        GameObject camObj = new GameObject("PhotoCamera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.transform.position = studioPos + new Vector3(0, 0.5f, -2f);
        cam.transform.LookAt(instance.transform.position + Vector3.up * 0.5f);
        
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0); // Nền trong suốt (Transparent)
        cam.orthographic = true;
        cam.orthographicSize = 1f; // Chỉnh độ zoom (có thể tự chỉnh lại cho vừa)

        // Setup Render Texture
        RenderTexture rt = new RenderTexture(imageSize, imageSize, 24);
        cam.targetTexture = rt;
        Texture2D screenShot = new Texture2D(imageSize, imageSize, TextureFormat.RGBA32, false);

        // Chụp
        cam.Render();
        RenderTexture.active = rt;
        screenShot.ReadPixels(new Rect(0, 0, imageSize, imageSize), 0, 0);
        screenShot.Apply();

        // Xóa studio ảo
        cam.targetTexture = null;
        RenderTexture.active = null;
        DestroyImmediate(camObj);
        DestroyImmediate(instance);

        // Lưu file PNG
        byte[] bytes = screenShot.EncodeToPNG();
        string dirPath = Application.dataPath + "/Icons";
        if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);

        string fileName = targetModel.name.Replace("(Clone)", "") + "_Icon.png";
        string filePath = dirPath + "/" + fileName;
        File.WriteAllBytes(filePath, bytes);

        // Tự động chuyển hình vừa lưu thành Sprite UI
        AssetDatabase.Refresh();
        string assetPath = "Assets/Icons/" + fileName;
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        Debug.Log($"<color=green>Đã chụp thành công!</color> Icon được lưu tại: {assetPath}");
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }
}
