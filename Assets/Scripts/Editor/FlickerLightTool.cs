using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Tool gắn FlickerLight vào tất cả đèn Point/Spot trong scene chỉ 1 click.
/// Menu: Tools → Flicker → Add Flicker To All Lights
/// </summary>
public class FlickerLightTool
{
    [MenuItem("Tools/Flicker/Add Flicker To All Point+Spot Lights")]
    static void AddFlickerToAll()
    {
        Light[] allLights = Object.FindObjectsByType<Light>();
        int count = 0;

        foreach (Light light in allLights)
        {
            // Chỉ gắn cho Point Light và Spot Light (không phải Directional)
            if (light.type != LightType.Point && light.type != LightType.Spot)
                continue;

            // Bỏ qua nếu đã có FlickerLight rồi
            if (light.GetComponent<FlickerLight>() != null)
                continue;

            Undo.AddComponent<FlickerLight>(light.gameObject);
            count++;
        }

        if (count == 0)
            Debug.Log("[FlickerTool] Không tìm thấy Point/Spot Light nào để gắn. Đảm bảo scene đang mở.");
        else
            Debug.Log($"[FlickerTool] ✓ Đã gắn FlickerLight cho {count} đèn.");
    }

    [MenuItem("Tools/Flicker/Remove Flicker From All Lights")]
    static void RemoveFlickerFromAll()
    {
        FlickerLight[] all = Object.FindObjectsByType<FlickerLight>();
        foreach (var f in all)
            Undo.DestroyObjectImmediate(f);

        Debug.Log($"[FlickerTool] Đã gỡ FlickerLight khỏi {all.Length} đèn.");
    }
}
#endif
