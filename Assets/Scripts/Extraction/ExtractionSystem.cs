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

    public bool isAssembling = false;
    private float assemblyProgress = 0f;

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
                DoEscape(inventory);
            }
            else if (EscapeManager.Instance != null && EscapeManager.Instance.CurrentMethod == EscapeMethodType.Assembly)
            {
                // Mở UI lắp ráp
                isAssembling = true;
                assemblyProgress = 0f;
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

    private void DoEscape(PlayerInventory inventory)
    {
        isActivated = true;
        if (inventory != null)
            finalRareLoot = inventory.rareLootCount;
        
        // Tùy chọn: Tạm dừng game hoặc vô hiệu hóa điều khiển ở đây
        Time.timeScale = 0.1f; // Slow motion lúc win cho ngầu
        
        Debug.Log($"<color=green>[CHIẾN THẮNG]</color> Cửa thoát hiểm mở! Bạn đã trốn thoát thành công!");

        StartCoroutine(LoadWaitingRoomAfterDelay(3f));
    }

    private System.Collections.IEnumerator LoadWaitingRoomAfterDelay(float delayRealtime)
    {
        yield return new WaitForSecondsRealtime(delayRealtime);
        Time.timeScale = 1f; // Reset timescale trước khi load scene mới
        UnityEngine.SceneManagement.SceneManager.LoadScene("WaitingRoom");
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
        else if (isAssembling)
        {
            DrawAssemblyUI();
        }
        else if (errorDisplayTimer > 0)
        {
            DrawTextWithShadow(errorMessage, 
                40, Color.red, TextAnchor.MiddleCenter);
        }
    }

    void DrawAssemblyUI()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        float width = 600;
        float height = 400;
        Rect windowRect = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);

        // Vẽ màn hình nền kiểu sci-fi
        GUI.color = new Color(0.05f, 0.1f, 0.15f, 0.95f);
        GUI.DrawTexture(windowRect, Texture2D.whiteTexture);
        
        // Viền
        GUI.color = new Color(0f, 0.8f, 1f, 1f);
        GUI.DrawTexture(new Rect(windowRect.x, windowRect.y, windowRect.width, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(windowRect.x, windowRect.y + windowRect.height - 2, windowRect.width, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(windowRect.x, windowRect.y, 2, windowRect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(windowRect.x + windowRect.width - 2, windowRect.y, 2, windowRect.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 30;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.UpperCenter;
        titleStyle.normal.textColor = new Color(0f, 0.8f, 1f);

        GUI.Label(new Rect(windowRect.x, windowRect.y + 20, windowRect.width, 40), "LẮP RÁP CỬA THOÁT HIỂM", titleStyle);

        GUIStyle descStyle = new GUIStyle(GUI.skin.label);
        descStyle.fontSize = 18;
        descStyle.alignment = TextAnchor.UpperCenter;
        descStyle.normal.textColor = Color.white;
        bool isReady = EscapeManager.Instance != null && EscapeManager.Instance.IsReadyToAssemble;
        
        string desc = isReady 
            ? "Đã thu thập đủ: Bánh răng, Bình nhiên liệu, Bo mạch.\nNhấn nút dưới đây để lắp ráp vào hệ thống cửa." 
            : "Bạn chưa thu thập đủ bộ phận!\nHãy tìm thêm trên bản đồ (Bánh răng, Bình nhiên liệu, Bo mạch).";

        GUI.Label(new Rect(windowRect.x + 50, windowRect.y + 80, windowRect.width - 100, 60), desc, descStyle);

        // Nút Lắp ráp
        GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
        btnStyle.fontSize = 24;
        btnStyle.fontStyle = FontStyle.Bold;

        GUI.enabled = isReady;

        if (GUI.Button(new Rect(windowRect.x + 150, windowRect.y + 160, 300, 60), "LẮP RÁP BỘ PHẬN", btnStyle))
        {
            assemblyProgress += 33.4f;
            if (assemblyProgress >= 100f)
            {
                assemblyProgress = 100f;
                isAssembling = false;
                
                // Mở khóa và thoát
                EscapeManager.Instance.UnlockEscape();
                
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    PlayerInventory inv = player.GetComponent<PlayerInventory>();
                    DoEscape(inv);
                }
            }
        }

        // Thanh tiến trình
        float barWidth = 400;
        float barHeight = 30;
        Rect barRect = new Rect(windowRect.x + 100, windowRect.y + 250, barWidth, barHeight);
        
        GUI.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        GUI.DrawTexture(barRect, Texture2D.whiteTexture);
        
        GUI.color = new Color(0f, 1f, 0.4f, 1f);
        GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * (assemblyProgress / 100f), barRect.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        
        GUIStyle progressStyle = new GUIStyle(GUI.skin.label);
        progressStyle.fontSize = 20;
        progressStyle.fontStyle = FontStyle.Bold;
        progressStyle.alignment = TextAnchor.MiddleCenter;
        progressStyle.normal.textColor = Color.white;
        
        GUI.Label(barRect, $"{Mathf.Min(100, Mathf.FloorToInt(assemblyProgress))}%", progressStyle);

        GUI.enabled = true; // Trả lại GUI.enabled cho nút đóng

        // Nút Đóng
        if (GUI.Button(new Rect(windowRect.x + 250, windowRect.y + 320, 100, 40), "ĐÓNG", GUI.skin.button))
        {
            isAssembling = false;
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