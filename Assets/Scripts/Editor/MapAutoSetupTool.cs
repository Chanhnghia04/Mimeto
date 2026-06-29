using UnityEngine;
using UnityEditor;

/// <summary>
/// Tool tự động thiết lập các cài đặt mệt mỏi cho Map.
/// Sẽ xuất hiện ở menu: Tools → Map Auto Setup
/// </summary>
public class MapAutoSetupTool : EditorWindow
{
    [MenuItem("Tools/Map Auto Setup (Làm tự động các bước)")]
    public static void ShowWindow()
    {
        GetWindow<MapAutoSetupTool>("Map Setup");
    }

    private void OnGUI()
    {
        GUILayout.Label("Tự động cấu hình Map", EditorStyles.boldLabel);

        if (GUILayout.Button("1. Bật Shadow (Bóng đổ)"))
        {
            SetShadows();
        }

        if (GUILayout.Button("2. Bật Realtime Fog (Sương mù)"))
        {
            SetFog();
        }

        if (GUILayout.Button("3. Tự động thêm LOD Group cho TẤT CẢ các nhà"))
        {
            SetupLODForHouses();
        }

        GUILayout.Space(20);
        GUILayout.Label("Các bước phải tự làm bằng tay:", EditorStyles.helpBox);
        GUILayout.Label("- Đặt đèn cột: Kéo thả Point Light vào đường phố.");
        GUILayout.Label("- Bake Lighting: Window -> Rendering -> Lighting -> Generate Lighting.");
    }

    private void SetShadows()
    {
        QualitySettings.shadowDistance = 80f;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.High;
        Debug.Log("<b>[AutoSetup]</b> Đã bật Shadow và set khoảng cách = 80.");
    }

    private void SetFog()
    {
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.12f, 0.18f, 0.06f, 1f); // Màu xanh độc
        RenderSettings.fogDensity = 0.025f;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        
        // Buộc Unity lưu lại Scene
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            
        Debug.Log("<b>[AutoSetup]</b> Đã bật sương mù (Fog).");
    }

    private void SetupLODForHouses()
    {
        // Tìm tất cả GameObject trong scene
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int count = 0;

        foreach (GameObject go in allObjects)
        {
            // Kiểm tra xem tên có chứa chữ "house" (không phân biệt hoa thường)
            if (go.name.ToLower().Contains("house"))
            {
                // Chỉ thêm LODGroup cho cục to nhất (thường không có cha)
                if (go.transform.parent == null || !go.transform.parent.name.ToLower().Contains("house"))
                {
                    // Nếu chưa có LODGroup thì mới thêm
                    if (go.GetComponent<LODGroup>() == null)
                    {
                        Undo.AddComponent<LODGroup>(go);
                        count++;
                    }
                }
            }
        }

        Debug.Log($"<b>[AutoSetup]</b> Đã tự động thêm LOD Group cho {count} ngôi nhà.");
    }
}
