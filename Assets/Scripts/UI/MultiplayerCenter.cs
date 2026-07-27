using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Lobbies.Models;

/// <summary>
/// UI Controller cho StartGame scene.
/// Host: tạo lobby (Public/Private) → Relay → hiện RoomInfo → Waiting scene
/// Client: tìm lobby public HOẶC nhập lobby code (private) → join Relay → Waiting scene
/// Chặn join khi Host đã vào Map.
/// </summary>
public class MultiplayerCenter : MonoBehaviour
{
    [Header("Main Buttons")]
    public Button hostButton;
    public Button clientButton;
    public Button backButton;

    [Header("Host Panel")]
    public GameObject hostPanel;
    public TMP_InputField lobbyNameInput;
    public Button publicButton;
    public Button privateButton;
    public TextMeshProUGUI hostStatusText;

    [Header("Client Panel")]
    public GameObject clientPanel;
    public TMP_InputField joinCodeInput;
    public Button joinByCodeButton;
    public Button refreshLobbiesButton;
    public Transform lobbyListContainer;
    public GameObject lobbyItemPrefab; // Prefab với Button + TMP_Text
    public TextMeshProUGUI clientStatusText;

    [Header("Room Info Panel")]
    public GameObject roomInfoPanel;
    public TextMeshProUGUI roomNameText;
    public TextMeshProUGUI roomCodeText;
    public TextMeshProUGUI roomTypeText;
    public TextMeshProUGUI roomPlayersText;
    public Button startWaitingButton;
    public Button copyCodeButton;
    public Button cancelRoomButton;

    [Header("Settings")]
    public int maxPlayers = 4;
    public string defaultLobbyName = "Mimeto Room";

    [Header("Status")]
    public TextMeshProUGUI statusText;

    private async void Start()
    {
        AutoAssignUI();

        // Ẩn panels
        if (hostPanel != null) hostPanel.SetActive(false);
        if (clientPanel != null) clientPanel.SetActive(false);
        if (roomInfoPanel != null) roomInfoPanel.SetActive(false);

        // Gán events
        if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
        if (clientButton != null) clientButton.onClick.AddListener(OnClientClicked);
        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);

        if (publicButton != null) publicButton.onClick.AddListener(() => CreateLobby(false));
        if (privateButton != null) privateButton.onClick.AddListener(() => CreateLobby(true));

        if (joinByCodeButton != null) joinByCodeButton.onClick.AddListener(OnJoinByCodeClicked);
        if (refreshLobbiesButton != null) refreshLobbiesButton.onClick.AddListener(OnRefreshLobbies);

        if (startWaitingButton != null) startWaitingButton.onClick.AddListener(OnStartWaitingClicked);
        if (copyCodeButton != null) copyCodeButton.onClick.AddListener(OnCopyCodeClicked);
        if (cancelRoomButton != null) cancelRoomButton.onClick.AddListener(OnCancelRoomClicked);

        // Callbacks
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        // Khởi tạo Services
        try
        {
            UpdateStatus("Đang khởi tạo...");
            await LobbyManager.Instance.InitializeAsync();
            UpdateStatus("Sẵn sàng! Chọn Host hoặc Client.");
        }
        catch (Exception e)
        {
            UpdateStatus("Lỗi khởi tạo: " + e.Message);
            Debug.LogError(e);
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    #region === BUTTON HANDLERS ===

    private void OnHostClicked()
    {
        if (hostPanel != null)
        {
            hostPanel.SetActive(true);
            if (clientPanel != null) clientPanel.SetActive(false);
            if (roomInfoPanel != null) roomInfoPanel.SetActive(false);
            UpdateHostStatus("Chọn chế độ lobby");
        }
        else
        {
            // Fallback: Nếu không có panel nâng cao, tự động tạo phòng Public luôn
            CreateLobby(false);
        }
    }

    private void OnClientClicked()
    {
        if (clientPanel != null)
        {
            if (hostPanel != null) hostPanel.SetActive(false);
            if (roomInfoPanel != null) roomInfoPanel.SetActive(false);
            clientPanel.SetActive(true);
            UpdateClientStatus("Tìm phòng hoặc nhập mã");
            OnRefreshLobbies();
        }
        else
        {
            // Fallback: Nếu không có panel nâng cao, tự động tìm và vào phòng Public đầu tiên
            if (joinCodeInput != null && !string.IsNullOrEmpty(joinCodeInput.text))
                OnJoinByCodeClicked();
            else
                JoinFirstPublicLobby();
        }
    }

    private async void JoinFirstPublicLobby()
    {
        UpdateStatus("Đang tìm phòng Public...");
        try
        {
            var lobbies = await LobbyManager.Instance.FindPublicLobbies();
            if (lobbies != null && lobbies.Count > 0)
            {
                UpdateStatus($"Đã thấy phòng: {lobbies[0].Name}. Đang join...");
                JoinPublicLobby(lobbies[0].Id);
            }
            else
            {
                UpdateStatus("Không tìm thấy phòng Public nào.");
            }
        }
        catch (Exception e)
        {
            UpdateStatus("Lỗi tìm phòng: " + e.Message);
        }
    }

    private void OnBackClicked()
    {
        // Cleanup
        if (NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient))
        {
            NetworkManager.Singleton.Shutdown();
        }

        _ = LobbyManager.Instance.LeaveLobby();
        UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
    }

    #endregion

    #region === HOST: TẠO LOBBY ===

    private async void CreateLobby(bool isPrivate)
    {
        try
        {
            string lobbyName = lobbyNameInput != null && !string.IsNullOrEmpty(lobbyNameInput.text)
                ? lobbyNameInput.text.Trim()
                : defaultLobbyName;

            string mode = isPrivate ? "Private" : "Public";
            UpdateHostStatus($"Đang tạo phòng {mode}...");
            SetHostInteractable(false);

            // 1. Tạo Lobby
            var lobby = await LobbyManager.Instance.CreateLobby(lobbyName, maxPlayers, isPrivate);

            // 2. Tạo Relay
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // 3. Lưu relay code vào lobby
            await LobbyManager.Instance.UpdateRelayCode(relayJoinCode);

            // 4. Setup transport
            RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            // 5. Connection approval
            NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;

            // 6. Start Host
            if (NetworkManager.Singleton.StartHost())
            {
                UpdateStatus($"Host started! (1/{maxPlayers})");

                // Hiện Room Info Panel thay vì chuyển scene ngay
                ShowRoomInfo(lobby, isPrivate);
            }
            else
            {
                UpdateHostStatus("Lỗi khởi tạo Host!");
                SetHostInteractable(true);
                await LobbyManager.Instance.LeaveLobby();
            }
        }
        catch (Exception e)
        {
            UpdateHostStatus($"Lỗi: {e.Message}");
            SetHostInteractable(true);
            Debug.LogError($"[MultiplayerCenter] CreateLobby error: {e}");
        }
    }

    /// <summary>
    /// Hiện panel thông tin phòng sau khi tạo lobby thành công
    /// </summary>
    private void ShowRoomInfo(Lobby lobby, bool isPrivate)
    {
        // Ẩn host panel, hiện room info panel
        if (hostPanel != null) hostPanel.SetActive(false);
        if (clientPanel != null) clientPanel.SetActive(false);

        if (roomInfoPanel != null)
        {
            roomInfoPanel.SetActive(true);

            if (roomNameText != null)
                roomNameText.text = $"Tên phòng: {lobby.Name}";

            if (roomCodeText != null)
            {
                roomCodeText.text = $"Mã phòng: {lobby.LobbyCode}";
                roomCodeText.gameObject.SetActive(true); // Luôn hiện mã phòng
            }

            if (roomTypeText != null)
                roomTypeText.text = isPrivate
                    ? "Loại: PRIVATE (Cần mã để vào)"
                    : "Loại: PUBLIC (Ai cũng vào được)";

            if (roomPlayersText != null)
                roomPlayersText.text = $"Người chơi: {lobby.Players.Count}/{lobby.MaxPlayers}";

            // Hiện/ẩn nút copy code
            if (copyCodeButton != null)
                copyCodeButton.gameObject.SetActive(true);

            bool isHost = NetworkManager.Singleton.IsHost;
            if (startWaitingButton != null)
                startWaitingButton.gameObject.SetActive(isHost);

            if (cancelRoomButton != null)
            {
                cancelRoomButton.gameObject.SetActive(true);
                var tmp = cancelRoomButton.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) tmp.text = isHost ? "Hủy Phòng" : "Thoát Phòng";
            }
        }
        else
        {
            // Fallback: không có panel → chuyển scene luôn (nếu là host)
            if (NetworkManager.Singleton.IsHost)
            {
                NetworkManager.Singleton.SceneManager.LoadScene("Waiting", UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
        }
    }

    /// <summary>
    /// Cập nhật số người chơi trên Room Info Panel
    /// </summary>
    private void UpdateRoomInfoPlayers()
    {
        if (roomInfoPanel != null && roomInfoPanel.activeSelf && roomPlayersText != null)
        {
            var lobby = LobbyManager.Instance.CurrentLobby;
            if (lobby != null)
            {
                roomPlayersText.text = $"Người chơi: {NetworkManager.Singleton.ConnectedClientsIds.Count}/{maxPlayers}";
            }
        }
    }

    /// <summary>
    /// Bấm "Bắt đầu" → chuyển sang Waiting scene
    /// </summary>
    private void OnStartWaitingClicked()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("Waiting", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    /// <summary>
    /// Copy mã phòng vào clipboard
    /// </summary>
    private void OnCopyCodeClicked()
    {
        var lobby = LobbyManager.Instance.CurrentLobby;
        if (lobby != null && !string.IsNullOrEmpty(lobby.LobbyCode))
        {
            GUIUtility.systemCopyBuffer = lobby.LobbyCode;
            UpdateStatus("Đã copy mã phòng!");
            Debug.Log($"[MultiplayerCenter] Copied lobby code: {lobby.LobbyCode}");
        }
    }

    /// <summary>
    /// Hủy phòng từ Room Info Panel
    /// </summary>
    private async void OnCancelRoomClicked()
    {
        bool wasHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        await LobbyManager.Instance.LeaveLobby();

        if (roomInfoPanel != null) roomInfoPanel.SetActive(false);
        
        if (wasHost)
        {
            if (hostPanel != null) hostPanel.SetActive(true);
            SetHostInteractable(true);
            UpdateHostStatus("Phòng đã hủy. Chọn lại chế độ.");
        }
        else
        {
            if (clientPanel != null) clientPanel.SetActive(true);
            SetClientInteractable(true);
            UpdateClientStatus("Đã thoát phòng.");
        }
        
        UpdateStatus("Sẵn sàng!");
    }

    #endregion

    #region === CLIENT: TÌM & JOIN LOBBY ===

    /// <summary>
    /// Refresh danh sách lobby public
    /// </summary>
    private async void OnRefreshLobbies()
    {
        UpdateClientStatus("Đang tìm phòng...");

        var lobbies = await LobbyManager.Instance.FindPublicLobbies();
        PopulateLobbyList(lobbies);

        UpdateClientStatus(lobbies.Count > 0
            ? $"Tìm thấy {lobbies.Count} phòng"
            : "Không tìm thấy phòng nào");
    }

    /// <summary>
    /// Hiển thị danh sách lobby
    /// </summary>
    private void PopulateLobbyList(List<Lobby> lobbies)
    {
        if (lobbyListContainer == null) return;

        // Clear cũ
        foreach (Transform child in lobbyListContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (var lobby in lobbies)
        {
            if (lobbyItemPrefab == null) continue;

            var item = Instantiate(lobbyItemPrefab, lobbyListContainer);
            var text = item.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.text = $"{lobby.Name} ({lobby.Players.Count}/{lobby.MaxPlayers})";
            }

            var button = item.GetComponent<Button>();
            if (button != null)
            {
                string lobbyId = lobby.Id;
                button.onClick.AddListener(() => JoinPublicLobby(lobbyId));
            }
        }
    }

    /// <summary>
    /// Join lobby public bằng ID
    /// </summary>
    private async void JoinPublicLobby(string lobbyId)
    {
        try
        {
            UpdateClientStatus("Đang vào phòng...");
            SetClientInteractable(false);

            var lobby = await LobbyManager.Instance.JoinLobbyById(lobbyId);
            await ConnectToRelay();
        }
        catch (Exception e)
        {
            UpdateClientStatus($"Lỗi: {e.Message}");
            SetClientInteractable(true);
            Debug.LogError($"[MultiplayerCenter] JoinPublicLobby error: {e}");
        }
    }

    /// <summary>
    /// Join lobby private bằng code
    /// </summary>
    private async void OnJoinByCodeClicked()
    {
        string code = joinCodeInput != null ? joinCodeInput.text : "";
        // TextMeshPro đôi khi dính ký tự zero-width \u200B, hoặc lúc copy bị dính ký tự xuống dòng
        code = code.Replace("\u200B", "").Replace("\r", "").Replace("\n", "").Trim().ToUpper();

        if (string.IsNullOrEmpty(code))
        {
            UpdateClientStatus("Vui lòng nhập mã phòng!");
            return;
        }

        try
        {
            UpdateClientStatus("Đang vào phòng...");
            SetClientInteractable(false);

            var lobby = await LobbyManager.Instance.JoinLobbyByCode(code);
            await ConnectToRelay();
        }
        catch (Exception e)
        {
            UpdateClientStatus($"Sai mã hoặc phòng không tồn tại!");
            SetClientInteractable(true);
            Debug.LogError($"[MultiplayerCenter] JoinByCode error: {e}");
        }
    }

    /// <summary>
    /// Sau khi join lobby, lấy relay code và kết nối Netcode
    /// </summary>
    private async Task ConnectToRelay()
    {
        string relayCode = LobbyManager.Instance.GetRelayCodeFromLobby();
        int attempts = 0;
        
        // Loop retry in case Host is still generating/uploading the Relay code
        while (string.IsNullOrEmpty(relayCode) && attempts < 10)
        {
            UpdateClientStatus("Đang đồng bộ mạng...");
            await Task.Delay(1000); // Đợi 1 giây
            await LobbyManager.Instance.ForceRefreshLobby(); // Ép cập nhật lobby ngay lập tức
            relayCode = LobbyManager.Instance.GetRelayCodeFromLobby();
            attempts++;
        }

        if (string.IsNullOrEmpty(relayCode))
        {
            UpdateClientStatus("Phòng chưa sẵn sàng, thử lại...");
            SetClientInteractable(true);
            await LobbyManager.Instance.LeaveLobby();
            return;
        }

        // Join Relay
        JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayCode);
        RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");
        
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(relayServerData);

        if (NetworkManager.Singleton.StartClient())
        {
            UpdateClientStatus("Đang kết nối...");
        }
        else
        {
            UpdateClientStatus("Lỗi kết nối!");
            SetClientInteractable(true);
            await LobbyManager.Instance.LeaveLobby();
        }
    }

    #endregion

    #region === CONNECTION APPROVAL ===

    /// <summary>
    /// Kiểm duyệt kết nối — chặn khi đã vào Map
    /// </summary>
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // Chỉ cho join khi ở Waiting hoặc StartGame
        if (currentScene != "Waiting" && currentScene != "StartGame")
        {
            response.Approved = false;
            response.Reason = "Game đã bắt đầu, không thể vào phòng!";
            Debug.Log($"[MultiplayerCenter] Từ chối: Host đang ở {currentScene}");
            return;
        }

        // Kiểm tra phòng đầy
        if (NetworkManager.Singleton.ConnectedClientsIds.Count >= maxPlayers)
        {
            response.Approved = false;
            response.Reason = "Phòng đã đầy!";
            return;
        }

        response.Approved = true;
        response.CreatePlayerObject = true;
    }

    #endregion

    #region === CALLBACKS ===

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            UpdateStatus($"Người chơi: {NetworkManager.Singleton.ConnectedClientsIds.Count}/{maxPlayers}");
            UpdateRoomInfoPlayers();
        }
        
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            UpdateStatus("Đã kết nối!");
            if (!NetworkManager.Singleton.IsServer && LobbyManager.Instance.CurrentLobby != null)
            {
                ShowRoomInfo(LobbyManager.Instance.CurrentLobby, LobbyManager.Instance.CurrentLobby.IsPrivate);
            }
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            UpdateStatus($"Người chơi: {NetworkManager.Singleton.ConnectedClientsIds.Count}/{maxPlayers}");
            UpdateRoomInfoPlayers();
        }
        else if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            string reason = NetworkManager.Singleton.DisconnectReason;
            if (string.IsNullOrEmpty(reason)) reason = "Mất kết nối";
            UpdateClientStatus($"Ngắt kết nối: {reason}");
            SetClientInteractable(true);

            if (roomInfoPanel != null && roomInfoPanel.activeSelf)
            {
                roomInfoPanel.SetActive(false);
                if (clientPanel != null) clientPanel.SetActive(true);
            }
        }
    }

    #endregion

    #region === HELPERS ===

    private void UpdateStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
        Debug.Log($"[MultiplayerCenter] {msg}");
    }

    private void UpdateHostStatus(string msg)
    {
        if (hostStatusText != null) hostStatusText.text = msg;
    }

    private void UpdateClientStatus(string msg)
    {
        if (clientStatusText != null) clientStatusText.text = msg;
    }

    private void SetHostInteractable(bool interactable)
    {
        if (publicButton != null) publicButton.interactable = interactable;
        if (privateButton != null) privateButton.interactable = interactable;
        if (lobbyNameInput != null) lobbyNameInput.interactable = interactable;
    }

    private void SetClientInteractable(bool interactable)
    {
        if (joinByCodeButton != null) joinByCodeButton.interactable = interactable;
        if (joinCodeInput != null) joinCodeInput.interactable = interactable;
        if (refreshLobbiesButton != null) refreshLobbiesButton.interactable = interactable;
    }

    #endregion

    #region === AUTO SETUP ===

    private void AutoAssignUI()
    {
        Button[] btns = GetComponentsInChildren<Button>(true);
        foreach (var b in btns)
        {
            string n = b.name.ToLower();
            if (hostButton == null && (n.Contains("host") && !n.Contains("public") && !n.Contains("private"))) hostButton = b;
            if (clientButton == null && (n.Contains("client") || n.Contains("join") && !n.Contains("code"))) clientButton = b;
            if (backButton == null && n.Contains("back")) backButton = b;
            if (publicButton == null && n.Contains("public")) publicButton = b;
            if (privateButton == null && n.Contains("private")) privateButton = b;
            if (joinByCodeButton == null && n.Contains("join")) joinByCodeButton = b;
            if (refreshLobbiesButton == null && n.Contains("refresh")) refreshLobbiesButton = b;
            if (startWaitingButton == null && n.Contains("startwaiting")) startWaitingButton = b;
            if (copyCodeButton == null && n.Contains("copycode")) copyCodeButton = b;
            if (cancelRoomButton == null && n.Contains("cancelroom")) cancelRoomButton = b;
        }

        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        foreach (var t in transforms)
        {
            string n = t.name.ToLower();
            if (hostPanel == null && n.Contains("host") && n.Contains("panel")) hostPanel = t.gameObject;
            if (clientPanel == null && n.Contains("client") && n.Contains("panel")) clientPanel = t.gameObject;
            if (roomInfoPanel == null && n.Contains("roominfo") && n.Contains("panel")) roomInfoPanel = t.gameObject;
            if (lobbyListContainer == null && n.Contains("container")) lobbyListContainer = t;
        }

        TMPro.TMP_InputField[] inputs = GetComponentsInChildren<TMPro.TMP_InputField>(true);
        foreach (var i in inputs)
        {
            string n = i.name.ToLower();
            if (lobbyNameInput == null && n.Contains("name")) lobbyNameInput = i;
            if (joinCodeInput == null && n.Contains("code")) joinCodeInput = i;
        }

        TMPro.TextMeshProUGUI[] texts = GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
        foreach (var txt in texts)
        {
            string n = txt.name.ToLower();
            if (statusText == null && n == "status") statusText = txt;
            if (hostStatusText == null && n.Contains("host") && n.Contains("status")) hostStatusText = txt;
            if (clientStatusText == null && n.Contains("client") && n.Contains("status")) clientStatusText = txt;
            if (roomNameText == null && n.Contains("roomname")) roomNameText = txt;
            if (roomCodeText == null && n.Contains("roomcode")) roomCodeText = txt;
            if (roomTypeText == null && n.Contains("roomtype")) roomTypeText = txt;
            if (roomPlayersText == null && n.Contains("roomplayers")) roomPlayersText = txt;
        }
    }

    #endregion
}
