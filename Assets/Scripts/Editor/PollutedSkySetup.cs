using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor.SceneManagement;

public class PollutedSkySetup : EditorWindow
{
    [MenuItem("Tools/Apply Polluted Sky Effect")]
    public static void ApplyEffect()
    {
        // 1. Áp dụng sương mù (Fog) - Màu tối đen
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.04f; // Sương mù đặc hơn
        ColorUtility.TryParseHtmlString("#0A0908", out Color fogColor); // Gần như đen xì
        RenderSettings.fogColor = fogColor;

        // 2. Chỉnh sửa ánh sáng mặt trời (Directional Light) - Gần như tắt hẳn
        Light[] lights = FindObjectsOfType<Light>();
        Light mainLight = null;
        foreach (var light in lights)
        {
            if (light.type == LightType.Directional)
            {
                mainLight = light;
                break;
            }
        }

        if (mainLight != null)
        {
            mainLight.intensity = 0.05f; // Ánh sáng cực yếu
            ColorUtility.TryParseHtmlString("#1F1B16", out Color lightColor);
            mainLight.color = lightColor;
            mainLight.shadowStrength = 0.9f;
        }

        // 3. Tùy chỉnh Skybox - Đen kịt
        Material skyboxMat = RenderSettings.skybox;
        if (skyboxMat == null || skyboxMat.shader.name != "Skybox/Procedural")
        {
            skyboxMat = new Material(Shader.Find("Skybox/Procedural"));
            string path = "Assets/GeneratedAssets/PollutedSkybox.mat";
            
            if (!AssetDatabase.IsValidFolder("Assets/GeneratedAssets"))
            {
                AssetDatabase.CreateFolder("Assets", "GeneratedAssets");
            }
            
            AssetDatabase.CreateAsset(skyboxMat, path);
            RenderSettings.skybox = skyboxMat;
        }
        
        skyboxMat.SetFloat("_AtmosphereThickness", 3.0f); // Không khí siêu đặc
        ColorUtility.TryParseHtmlString("#0A0908", out Color skyTint);
        skyboxMat.SetColor("_SkyTint", skyTint);
        ColorUtility.TryParseHtmlString("#050504", out Color groundColor);
        skyboxMat.SetColor("_GroundColor", groundColor);
        skyboxMat.SetFloat("_Exposure", 0.05f); // Bầu trời gần như không phát sáng

        // 4. Tạo hiệu ứng Post Processing (Global Volume) - Giảm sáng mạnh
        Volume volume = FindObjectOfType<Volume>();
        if (volume == null)
        {
            GameObject volumeObj = new GameObject("Polluted Sky Global Volume");
            volume = volumeObj.AddComponent<Volume>();
            volume.isGlobal = true;
        }

        if (volume.profile == null)
        {
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            
            if (!AssetDatabase.IsValidFolder("Assets/GeneratedAssets"))
            {
                AssetDatabase.CreateFolder("Assets", "GeneratedAssets");
            }
            
            AssetDatabase.CreateAsset(profile, "Assets/GeneratedAssets/PollutedSkyProfile.asset");
            volume.profile = profile;
        }

        // Thêm Color Adjustments
        if (!volume.profile.TryGet(out ColorAdjustments colorAdjustments))
        {
            colorAdjustments = volume.profile.Add<ColorAdjustments>(true);
        }
        colorAdjustments.postExposure.Override(-2.0f); // Ép tối toàn bộ hình ảnh mạnh tay
        ColorUtility.TryParseHtmlString("#524B40", out Color filterColor);
        colorAdjustments.colorFilter.Override(filterColor);
        colorAdjustments.contrast.Override(25f);

        // Thêm Vignette (Tối 4 góc)
        if (!volume.profile.TryGet(out Vignette vignette))
        {
            vignette = volume.profile.Add<Vignette>(true);
        }
        vignette.intensity.Override(0.5f);

        // Lưu thay đổi vào Scene
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        
        Debug.Log("Đã áp dụng hiệu ứng Bầu trời ô nhiễm thành công!");
    }
}
