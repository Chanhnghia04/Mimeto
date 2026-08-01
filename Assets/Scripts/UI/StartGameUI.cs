using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartGameUI : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private GameObject multiplayerPanel;

    private void Start()
    {
        if (startButton != null)
            startButton.onClick.AddListener(OnStartButtonClicked);

        FindMultiplayerPanel();

        // Ẩn panel multiplayer ban đầu
        if (multiplayerPanel != null)
            multiplayerPanel.SetActive(false);
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
        var canvas = FindFirstObjectByType<Canvas>();
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
