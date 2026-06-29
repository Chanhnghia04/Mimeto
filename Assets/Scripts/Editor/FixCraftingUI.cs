using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class FixCraftingUI : Editor
{
    [MenuItem("Tools/Khôi phục lại nút Bàn Chế Tạo")]
    public static void FixButtons()
    {
        CraftingUI craftingUI = Object.FindAnyObjectByType<CraftingUI>();
        if (craftingUI == null)
        {
            EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy CraftingUI trong Scene!", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(craftingUI.gameObject, "Fix Buttons");

        Button[] buttons = new Button[] {
            craftingUI.craftBasicButton,
            craftingUI.craftAdvancedButton,
            craftingUI.craftUVButton,
            craftingUI.craftCrowbarButton,
            craftingUI.craftShovelButton,
            craftingUI.craftMacheteButton,
            craftingUI.craftAxeButton,
            craftingUI.craftBatButton
        };

        // Dọn dẹp cục Scroll View thừa nếu bạn chưa xoá
        Transform scrollView = craftingUI.craftingPanel.transform.Find("Scroll View");

        // Xếp lại vị trí các nút cho ngay ngắn
        float startY = -30f;
        float spacing = 50f;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
            {
                // Đưa nút về lại panel chính
                buttons[i].transform.SetParent(craftingUI.craftingPanel.transform, false);
                
                // Reset lại tọa độ
                RectTransform rt = buttons[i].GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.anchoredPosition = new Vector2(0, startY - (i * spacing));
                    rt.localScale = Vector3.one;
                }
            }
        }

        // Xóa tận gốc Scroll View
        if (scrollView != null)
        {
            Undo.DestroyObjectImmediate(scrollView.gameObject);
        }

        EditorUtility.DisplayDialog("Xong!", "Đã lôi tất cả các nút về lại giữa màn hình và xếp hàng dọc ngay ngắn. Bạn kiểm tra thử nhé!", "Quá tuyệt");
    }
}
