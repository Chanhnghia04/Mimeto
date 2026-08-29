using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

/// <summary>
/// Tự động thiết lập Post Processing (URP Volume) tạo không khí
/// Toxic City: màu xanh độc + vignette + chromatic aberration.
///
/// SETUP:
///   1. Tạo GameObject "MapVisualSetup" trong Scene.
///   2. Gắn script này vào.
///   3. Chạy game → auto áp dụng.
///   Hoặc dùng ContextMenu "Apply Visual Preset" ngay trong Editor.
/// </summary>
public class MapVisualSetup : MonoBehaviour
{
    [Header("Post Process (URP Volume)")]
    [Tooltip("Volume URP trong scene (nếu trống sẽ tự tìm/tạo)")]
    public Volume postProcessVolume;

    [Header("Fog Settings")]
    public bool  enableFog       = true;
    public Color fogColor        = Color.black; // Tối đen
    public float fogDensity      = 0.2f; // Sương mù cực đặc cho Linear/Exponential
    public FogMode fogMode       = FogMode.Linear;

    // Linear Fog parameters (since we use Linear for 5m limit)
    public float fogStartDistance = 0f;
    public float fogEndDistance   = 6f;

    [Header("Ambient Light")]
    public bool  overrideAmbient = true;
    public Color skyAmbient      = Color.black; // Đen hoàn toàn
    public Color groundAmbient   = Color.black; // Đen hoàn toàn

    [Header("Directional Light (Mặt trời)")]
    public Light sunLight;
    public Color sunColor        = new Color(0.1f, 0.1f, 0.15f); // Ánh trăng cực nhạt
    public float sunIntensity    = 0.005f; // Gần như bằng 0
    [Range(0f, 180f)]
    public float sunAngle        = 35f;   // góc thấp = chiều tà

    [Header("Skybox")]
    [Tooltip("Để trống sẽ tự tìm ToxicCity_Skybox trong Assets/Skyboxes")]
    public Material skyboxMaterial;

    void Start()
    {
        // Force override Inspector values to ensure it's pitch black
        enableFog = true;
        fogColor = Color.black;
        fogDensity = 0.5f; 
        fogMode = FogMode.Linear;
        fogStartDistance = 0f;
        fogEndDistance = 3f; // Quay lại 3m sương mù đặc
        
        overrideAmbient = true;
        skyAmbient = Color.black;
        groundAmbient = Color.black;
        
        sunColor = new Color(0.02f, 0.02f, 0.03f); // Cực kỳ tối
        sunIntensity = 0.001f;

        ApplyVisualPreset();
    }

    [ContextMenu("Apply Visual Preset")]
    public void ApplyVisualPreset()
    {
        ApplyFog();
        ApplyAmbientLight();
        ApplySunLight();
        ApplySkybox();
        ApplyPostProcess();

        Debug.Log("[MapVisualSetup] ✓ Visual preset đã được áp dụng.");
    }

    // ── Fog ──────────────────────────────────────────────────────────────────

    void ApplyFog()
    {
        RenderSettings.fog        = enableFog;
        RenderSettings.fogColor   = fogColor;
        RenderSettings.fogMode    = fogMode;
        RenderSettings.fogDensity = fogDensity;
        if (fogMode == FogMode.Linear)
        {
            RenderSettings.fogStartDistance = fogStartDistance;
            RenderSettings.fogEndDistance   = fogEndDistance;
        }
    }

    // ── Ambient Light ────────────────────────────────────────────────────────

    void ApplyAmbientLight()
    {
        if (!overrideAmbient) return;

        RenderSettings.ambientMode        = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor    = skyAmbient;
        RenderSettings.ambientEquatorColor = Color.Lerp(skyAmbient, groundAmbient, 0.5f);
        RenderSettings.ambientGroundColor = groundAmbient;
    }

    // ── Mặt Trời ─────────────────────────────────────────────────────────────

    void ApplySunLight()
    {
        if (sunLight == null)
            sunLight = FindAnyObjectByType<Light>();
        if (sunLight == null || sunLight.type != LightType.Directional) return;

        sunLight.color     = sunColor;
        sunLight.intensity = sunIntensity;
        sunLight.transform.rotation = Quaternion.Euler(sunAngle, 45f, 0f);
    }

    // ── Skybox ───────────────────────────────────────────────────────────────

    void ApplySkybox()
    {
        if (skyboxMaterial == null)
        {
#if UNITY_EDITOR
            skyboxMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Skyboxes/ToxicCity_Skybox.mat");
#endif
        }

        if (skyboxMaterial != null)
            RenderSettings.skybox = skyboxMaterial;
    }

    // ── Post Processing (URP Volume) ─────────────────────────────────────────

    void ApplyPostProcess()
    {
        if (postProcessVolume == null)
            postProcessVolume = FindAnyObjectByType<Volume>();

        if (postProcessVolume == null)
        {
            // Tạo Global Volume mới
            GameObject volGO    = new GameObject("GlobalPostProcess");
            postProcessVolume   = volGO.AddComponent<Volume>();
            postProcessVolume.isGlobal  = true;
            postProcessVolume.priority  = 10f;
            postProcessVolume.profile   = ScriptableObject.CreateInstance<VolumeProfile>();
        }

        var profile = postProcessVolume.profile;
        if (profile == null) return;

        // ── Color Adjustments: shift màu xanh/vàng độc hại ──────────────────
        if (!profile.TryGet<ColorAdjustments>(out var colorAdj))
            colorAdj = profile.Add<ColorAdjustments>(true);

        colorAdj.active                 = true;
        colorAdj.postExposure.value     = -4.0f;   // Tối kịt cực mạnh, gần như đen thui
        colorAdj.contrast.value         = 20f;     // tương phản cao
        colorAdj.colorFilter.value      = new Color(0.3f, 0.3f, 0.3f); // Xám đen thay vì xanh
        colorAdj.saturation.value       = -15f;    // bớt bão hòa màu

        // ── Vignette: viền tối ───────────────────────────────────────────────
        if (!profile.TryGet<Vignette>(out var vignette))
            vignette = profile.Add<Vignette>(true);

        vignette.active           = true;
        vignette.intensity.value  = 0.38f;
        vignette.smoothness.value = 0.4f;
        vignette.color.value      = new Color(0.05f, 0.08f, 0.02f); // viền xanh độc

        // ── Chromatic Aberration: méo màu nhẹ ────────────────────────────────
        if (!profile.TryGet<ChromaticAberration>(out var chroma))
            chroma = profile.Add<ChromaticAberration>(true);

        chroma.active           = true;
        chroma.intensity.value  = 0.15f;

        // ── Film Grain: hạt phim tạo cảm giác cũ kỹ ────────────────────────
        if (!profile.TryGet<FilmGrain>(out var grain))
            grain = profile.Add<FilmGrain>(true);

        grain.active            = true;
        grain.intensity.value   = 0.2f;
        grain.response.value    = 0.6f;
        grain.type.value        = FilmGrainLookup.Thin1;
    }
}
