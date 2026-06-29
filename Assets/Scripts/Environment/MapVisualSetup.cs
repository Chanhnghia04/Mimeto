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
    public Color fogColor        = new Color(0.12f, 0.18f, 0.06f, 1f); // xanh độc
    public float fogDensity      = 0.025f;
    public FogMode fogMode       = FogMode.ExponentialSquared;

    [Header("Ambient Light")]
    public bool  overrideAmbient = true;
    public Color skyAmbient      = new Color(0.08f, 0.14f, 0.04f);   // xanh tối
    public Color groundAmbient   = new Color(0.05f, 0.04f, 0.02f);   // nâu đất

    [Header("Directional Light (Mặt trời)")]
    public Light sunLight;
    public Color sunColor        = new Color(0.7f, 0.8f, 0.5f);      // vàng nhạt ô nhiễm
    public float sunIntensity    = 0.5f;
    [Range(0f, 180f)]
    public float sunAngle        = 35f;   // góc thấp = chiều tà

    [Header("Skybox")]
    [Tooltip("Để trống sẽ tự tìm ToxicCity_Skybox trong Assets/Skyboxes")]
    public Material skyboxMaterial;

    void Start()
    {
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
        colorAdj.postExposure.value     = -0.3f;   // tối hơn 1 chút
        colorAdj.contrast.value         = 20f;     // tương phản cao
        colorAdj.colorFilter.value      = new Color(0.85f, 0.95f, 0.7f); // filter xanh
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
