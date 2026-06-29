using UnityEngine;

/// <summary>
/// Tạo các hạt bụi/khói độc/tro tàn bay lơ lửng trong không khí.
/// Đặt vào Scene là tự động tạo hiệu ứng ambient particle khắp map.
///
/// SETUP: Tạo GameObject "AmbientParticles" → gắn script này.
/// Không cần Prefab hay Particle System riêng — tự tạo bằng code.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class AmbientParticleEffect : MonoBehaviour
{
    public enum ParticleMode { ToxicDust, AshFall, FogDrift }

    [Header("Chế độ hạt")]
    public ParticleMode mode = ParticleMode.ToxicDust;

    [Header("Vùng phủ")]
    [Tooltip("Kích thước vùng xuất hiện hạt (bao phủ map)")]
    public Vector3 emissionArea = new Vector3(100f, 20f, 100f);

    [Header("Màu sắc")]
    public Color startColor = new Color(0.6f, 0.8f, 0.3f, 0.15f); // xanh độc

    void Awake()
    {
        ConfigureParticleSystem();
    }

    void ConfigureParticleSystem()
    {
        ParticleSystem ps = GetComponent<ParticleSystem>();

        // ── Main module ──────────────────────────────────────────────────────
        var main            = ps.main;
        main.loop           = true;
        main.playOnAwake    = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        switch (mode)
        {
            case ParticleMode.ToxicDust:
                main.startLifetime    = new ParticleSystem.MinMaxCurve(4f, 10f);
                main.startSpeed       = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
                main.startSize        = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
                main.startColor       = new ParticleSystem.MinMaxGradient(
                    new Color(0.5f, 0.9f, 0.2f, 0.08f),
                    new Color(0.8f, 1.0f, 0.3f, 0.18f));
                main.maxParticles     = 800;
                break;

            case ParticleMode.AshFall:
                main.startLifetime    = new ParticleSystem.MinMaxCurve(6f, 14f);
                main.startSpeed       = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
                main.startSize        = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
                main.startColor       = new ParticleSystem.MinMaxGradient(
                    new Color(0.4f, 0.4f, 0.4f, 0.12f),
                    new Color(0.7f, 0.7f, 0.7f, 0.22f));
                main.gravityModifier  = new ParticleSystem.MinMaxCurve(0.05f);
                main.maxParticles     = 600;
                break;

            case ParticleMode.FogDrift:
                main.startLifetime    = new ParticleSystem.MinMaxCurve(8f, 20f);
                main.startSpeed       = new ParticleSystem.MinMaxCurve(0.02f, 0.1f);
                main.startSize        = new ParticleSystem.MinMaxCurve(0.5f, 2.0f);
                main.startColor       = new ParticleSystem.MinMaxGradient(
                    new Color(0.3f, 0.5f, 0.2f, 0.04f),
                    new Color(0.5f, 0.7f, 0.3f, 0.09f));
                main.maxParticles     = 200;
                break;
        }

        // ── Emission ─────────────────────────────────────────────────────────
        var emission    = ps.emission;
        emission.rateOverTime = mode == ParticleMode.FogDrift ? 3f : 30f;

        // ── Shape: hộp phủ toàn bộ vùng ────────────────────────────────────
        var shape       = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale     = emissionArea;

        // ── Velocity over lifetime: trôi nhẹ theo gió ───────────────────────
        var vel         = ps.velocityOverLifetime;
        vel.enabled     = true;
        vel.space       = ParticleSystemSimulationSpace.World;
        vel.x           = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
        vel.y           = mode == ParticleMode.AshFall
                          ? new ParticleSystem.MinMaxCurve(-0.3f, -0.05f) // rơi xuống
                          : new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        vel.z           = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);

        // ── Size over lifetime: fade in/out mềm ─────────────────────────────
        var sizeOL      = ps.sizeOverLifetime;
        sizeOL.enabled  = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.2f, 1f),
            new Keyframe(0.8f, 1f),
            new Keyframe(1f, 0f));
        sizeOL.size     = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // ── Renderer: dùng Billboard sprite đơn giản ────────────────────────
        var renderer    = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode       = ParticleSystemRenderMode.Billboard;
        renderer.sortingFudge     = -10f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows   = false;

        ps.Play();
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 0.2f, 0.2f);
        Gizmos.DrawWireCube(transform.position, emissionArea);
    }
#endif
}
