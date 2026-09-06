using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartGameUI : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private GameObject multiplayerPanel;

    private void Start()
    {
        // Ép hiện chuột ở màn hình Start
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        // --- ĐẢM BẢO LUÔN CÓ CAMERA CHO SCENE NÀY ---
        Camera[] cams = Resources.FindObjectsOfTypeAll<Camera>();
        bool hasActiveCamera = false;
        foreach (Camera c in cams)
        {
            // Chỉ xét những camera thuộc scene hiện tại
            if (c.gameObject.scene == this.gameObject.scene)
            {
                c.gameObject.SetActive(true); // Bật nó lên nếu nó lỡ bị tắt
                hasActiveCamera = true;
            }
        }
        
        // Nếu không có bất kỳ Camera nào trong scene, tự tạo 1 cái
        if (!hasActiveCamera)
        {
            GameObject camObj = new GameObject("Main Camera Auto");
            Camera autoCam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
            autoCam.clearFlags = CameraClearFlags.SolidColor;
            autoCam.backgroundColor = Color.black;
        }

        if (startButton != null)
            startButton.onClick.AddListener(OnStartButtonClicked);

        FindMultiplayerPanel();

        // Ẩn panel multiplayer ban đầu
        if (multiplayerPanel != null)
            multiplayerPanel.SetActive(false);
    }

    private void Update()
    {
        // Liên tục ép mở chuột và hiển thị chuột trong menu
        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
        }
        if (!Cursor.visible)
        {
            Cursor.visible = true;
        }

        // Đảm bảo EventSystem luôn tồn tại để có thể click vào UI
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }

    private void FindMultiplayerPanel()
    {
        if (multiplayerPanel != null) return;

        // Ưu tiên tìm component MultiplayerCenter trước vì nó có thể nằm trên object bị ẩn (inactive)
        var center = Resources.FindObjectsOfTypeAll<MultiplayerCenter>();
        if (center != null && center.Length > 0)
        {
            foreach (var c in center)
            {
                if (c.gameObject.scene.isLoaded) // Đảm bảo thuộc scene hiện tại
                {
                    multiplayerPanel = c.gameObject;
                    return;
                }
            }
        }

        // Nếu không có MultiplayerCenter, thử tìm con trong Canvas hiện tại
        var canvas = FindAnyObjectByType<Canvas>();
        if (canvas != null)
        {
            var mp = canvas.transform.Find("MultiplayerPanel");
            if (mp != null) 
            {
                multiplayerPanel = mp.gameObject;
                return;
            }
        }

        // Fallback cuối cùng
        var fallback = GameObject.Find("MultiplayerPanel");
        if (fallback != null) multiplayerPanel = fallback;
    }

    private void OnStartButtonClicked()
    {
        FindMultiplayerPanel();

        // Hiện panel chọn Host/Client thay vì chuyển scene ngay
        if (multiplayerPanel != null)
        {
            multiplayerPanel.SetActive(true);
            // Ẩn nút START sau khi bấm
            if (startButton != null)
                startButton.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("MultiplayerPanel is missing! Please make sure it exists in the scene and is assigned.");
        }
    }

    private void OnDestroy()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(OnStartButtonClicked);
    }
}
