using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class SetupUIScript
{
    [MenuItem("Tools/Wow My UI - Escape Theme & Layout")]
    public static void Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        int modified = 0;

        foreach (var obj in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (obj.scene != scene) continue;

            // --- THAY ĐỔI BỐ CỤC (LAYOUT) ---
            
            // 1. Canh giữa và tạo form chuẩn cho các Panel chính
            if (obj.name.EndsWith("Panel"))
            {
                RectTransform rect = obj.GetComponent<RectTransform>();
                if (rect != null)
                {
                    // Canh giữa màn hình
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    // Đặt kích thước dạng thẻ (Card) dọc giống UI sự kiện
                    rect.sizeDelta = new Vector2(500, 700);
                    rect.anchoredPosition = Vector2.zero;
                    modified++;
                }
            }

            // 2. Gom các Nút bấm thành dạng danh sách (List)
            if (obj.name.Contains("Container"))
            {
                // Thêm VerticalLayoutGroup để các nút tự động xếp hàng dọc gọn gàng
                VerticalLayoutGroup vlg = obj.GetComponent<VerticalLayoutGroup>();
                if (vlg == null) vlg = obj.AddComponent<VerticalLayoutGroup>();
                
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlHeight = false;
                vlg.childControlWidth = false;
                vlg.childForceExpandHeight = false;
                vlg.childForceExpandWidth = false;
                vlg.spacing = 20; // Khoảng cách giữa các nút
                
                RectTransform rect = obj.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0, 0);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.offsetMin = new Vector2(20, 20);
                    rect.offsetMax = new Vector2(-20, -100); // Chừa chỗ cho Title ở trên
                }
                modified++;
            }

            // 3. Chuẩn hóa kích thước nút bấm
            Button btn = obj.GetComponent<Button>();
            if (btn != null)
            {
                RectTransform rect = obj.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.sizeDelta = new Vector2(350, 60); // Kích thước nút chuẩn
                }
            }
            
            // 4. Canh Title lên trên cùng
            if (obj.name.ToLower().Contains("title"))
            {
                RectTransform rect = obj.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0.5f, 1f);
                    rect.anchorMax = new Vector2(0.5f, 1f);
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.anchoredPosition = new Vector2(0, -30);
                }
            }
        }

        if (modified > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Wow UI] Thay đổi bố cục cho {modified} phần tử thành công! Hãy lưu Scene.");
        }
    }
}
