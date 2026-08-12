using UnityEngine;
using UnityEditor;

public class OptimizeProject : EditorWindow
{
    [MenuItem("Tools/Optimize Project Settings (No Code Change)")]
    public static void Optimize()
    {
        // 1. Tối ưu Physics
        Physics.autoSyncTransforms = false; // Tắt tự động đồng bộ transform khi không cần thiết (tăng FPS)
        Physics.reuseCollisionCallbacks = true; // Dùng lại object collision để giảm rác RAM (Garbage Collection)
        
        // 2. Tối ưu Quality
        QualitySettings.vSyncCount = 0; // Tắt VSync trong editor để tránh bị giới hạn khung hình
        Application.targetFrameRate = 120; // Đẩy mức giới hạn lên 120 FPS
        
        // Giảm bóng đổ một chút để mượt hơn
        QualitySettings.shadowCascades = 2; // Từ 4 xuống 2 giúp giảm tải CPU/GPU
        QualitySettings.shadowDistance = 75f; // Không render bóng ở khoảng cách quá xa
        
        // 3. Tối ưu Network/NGO (Tick rate)
        // Lưu ý: Không can thiệp vào logic mạng, chỉ cấu hình môi trường Unity cho mượt
        
        // 4. Garbage Collection
#if UNITY_2021_1_OR_NEWER
        if (!PlayerSettings.gcIncremental)
        {
            PlayerSettings.gcIncremental = true; // Bật dọn rác ngầm để tránh giật lag (stutter)
        }
#endif

        Debug.Log("<color=cyan>Đã tối ưu hóa Settings (Physics, Shadows, GC, FPS)! Game sẽ mượt hơn mà không ảnh hưởng logic.</color>");
    }
}
