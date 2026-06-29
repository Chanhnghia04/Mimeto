using UnityEngine;

public class ExtractionSystem : MonoBehaviour, IInteractable
{
    [Header("Escape Settings")]
    public bool requireKey = true;
    public bool isActivated = false;
    
    // Lưu số đồ hiếm để hiển thị lên màn hình win
    private int finalRareLoot = 0;
    
    // Lưu thông báo lỗi nếu bấm khi chưa có chìa khóa
    private string errorMessage = "";
    private float errorDisplayTimer = 0f;

    public void Interact(GameObject interactor)
    {
        if (isActivated) return;

        PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
        if (inventory != null)
        {
            // Kiểm tra trạng thái mở khóa từ hệ thống nhiệm vụ mới (EscapeManager)
            bool canEscape = !requireKey || 
                             (EscapeManager.Instance != null && EscapeManager.Instance.IsEscapeUnlocked);

            if (canEscape)
            {
                // Cho phép tẩu thoát
                isActivated = true;
                finalRareLoot = inventory.rareLootCount;
                
                // Tùy chọn: Tạm dừng game hoặc vô hiệu hóa điều khiển ở đây
                Time.timeScale = 0.1f; // Slow motion lúc win cho ngầu
                
                Debug.Log($"<color=green>[CHIẾN THẮNG]</color> Cửa thoát hiểm mở! Bạn đã trốn thoát thành công!");
            }
            else
            {
                // Báo lỗi lên màn hình
                string methodIns = EscapeManager.Instance != null 
                    ? EscapeManager.Instance.GetMethodInstruction() 
                    : "Hoàn thành nhiệm vụ thoát hiểm trước!";
                
                errorMessage = $"Cửa bị khóa!\n{methodIns}";
                errorDisplayTimer = 3f; // Hiện thông báo trong 3 giây
                Debug.Log("<color=red>Cửa bị khóa!</color> Chưa hoàn thành điều kiện tẩu thoát.");
            }
        }
    }

    void Update()
    {
        // Trừ lùi thời gian hiển thị thông báo lỗi
        if (errorDisplayTimer > 0)
        {
            errorDisplayTimer -= Time.deltaTime;
        }
    }

    // OnGUI vẽ trực tiếp chữ lên màn hình mà không cần tốn công setup Canvas
    void OnGUI()
    {
        if (isActivated)
        {
            DrawTextWithShadow($"BẠN ĐÃ TẨU THOÁT THÀNH CÔNG!\n\nSố Đồ Hiếm Thu Được: {finalRareLoot}", 
                60, Color.green, TextAnchor.MiddleCenter);
        }
        else if (errorDisplayTimer > 0)
        {
            DrawTextWithShadow(errorMessage, 
                40, Color.red, TextAnchor.MiddleCenter);
        }
    }

    // Hàm vẽ chữ có viền đen bóng mờ để dễ đọc ở mọi góc tối/sáng
    void DrawTextWithShadow(string text, int fontSize, Color textColor, TextAnchor alignment)
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = fontSize;
        style.fontStyle = FontStyle.Bold;
        style.alignment = alignment;
        
        Rect rect = new Rect(0, 0, Screen.width, Screen.height);

        // Vẽ bóng đen
        style.normal.textColor = Color.black;
        GUI.Label(new Rect(3, 3, Screen.width, Screen.height), text, style);

        // Vẽ chữ chính
        style.normal.textColor = textColor;
        GUI.Label(rect, text, style);
    }
}