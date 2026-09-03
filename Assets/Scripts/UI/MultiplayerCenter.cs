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

public class MultiplayerCenter : MonoBehaviour
{
    private NetworkManager Net => Unity.Netcode.NetworkManager.Singleton ?? FindAnyObjectByType<NetworkManager>();
    
    [Header("Main Menu")]
    public GameObject mainMenuPanel;
    public Button playButton, continueButton, instructButton, exitButton;

    [Header("Lobby List")]
    public GameObject lobbyListPanel;
    public Button openCreateRoomButton, refreshLobbiesButton, backToMenuButton;
    public Transform lobbyListContainer;
    public GameObject lobbyItemPrefab;

    [Header("Create Room")]
    public GameObject createRoomPanel;
    public TMP_InputField roomNameInput;
    public Button setPublicButton, setPrivateButton, confirmCreateRoomButton, cancelCreateRoomButton;
    private bool isCreatingPrivateRoom = false;

    [Header("Join Private Room")]
    public GameObject joinPrivatePanel;
    public TMP_InputField joinCodeInput;
    public Button confirmJoinButton, cancelJoinButton;
    private Lobby _selectedPrivateLobbyToJoin;

    [Header("Room Info")]
    public GameObject roomInfoPanel;
    public TMP_InputField editRoomNameInput;
    public TextMeshProUGUI roomTypeText, roomCodeText, roomPlayersText;
    public Button startWaitingButton, copyCodeButton, cancelRoomButton;

    public TextMeshProUGUI statusText;
    private int maxPlayers = 4;
    private Lobby _selectedLobbyToJoin;

    // --- CỜ TRẠNG THÁI (TRÁNH LỖI UI RACE CONDITION) ---
    private bool _isNetworkReady = false;
    private bool _isLeaving = false; 

    private async void Start()
    {
        AutoAssignUI();
        ShowPanel(mainMenuPanel);
        SetButtonsInteractable(false); // Khóa nút cho đến khi khởi tạo xong dịch vụ mạng

        // "Chơi" = Tạo phòng mới, xóa save → bắt đầu với 0 EC
        if (playButton) playButton.onClick.AddListener(() => OnPlayNewGame());
        // "Tiếp tục" = Load save cũ, tạo phòng, vào thẳng roomInfoPanel
        if (continueButton) continueButton.onClick.AddListener(() => OnContinueGame());
        if (instructButton) instructButton.onClick.AddListener(() => UpdateStatus("Coming soon"));
        if (exitButton) exitButton.onClick.AddListener(Application.Quit);

        if (openCreateRoomButton) openCreateRoomButton.onClick.AddListener(() => { ShowPanel(createRoomPanel); SetCreateRoomMode(false); });
        if (refreshLobbiesButton) refreshLobbiesButton.onClick.AddListener(OnRefreshLobbies);
        if (backToMenuButton) backToMenuButton.onClick.AddListener(() => ShowPanel(mainMenuPanel));

        if (setPublicButton) setPublicButton.onClick.AddListener(() => SetCreateRoomMode(false));
        if (setPrivateButton) setPrivateButton.onClick.AddListener(() => SetCreateRoomMode(true));
        if (confirmCreateRoomButton) confirmCreateRoomButton.onClick.AddListener(OnConfirmCreateRoom);
        if (cancelCreateRoomButton) cancelCreateRoomButton.onClick.AddListener(() => ShowPanel(lobbyListPanel));

        if (confirmJoinButton) confirmJoinButton.onClick.AddListener(OnConfirmJoinPrivateRoom);
        if (cancelJoinButton) cancelJoinButton.onClick.AddListener(() => ShowPanel(lobbyListPanel));

        if (startWaitingButton) startWaitingButton.onClick.AddListener(OnStartWaitingClicked);
        if (copyCodeButton) copyCodeButton.onClick.AddListener(OnCopyCodeClicked);
        if (cancelRoomButton) cancelRoomButton.onClick.AddListener(OnCancelRoomClicked);
        if (editRoomNameInput) editRoomNameInput.onEndEdit.AddListener(OnEditRoomNameEnd);

        var net = Net;
        if (net != null)
        {
            net.OnClientConnectedCallback -= OnClientConnected;
            net.OnClientConnectedCallback += OnClientConnected;
            net.OnClientDisconnectCallback -= OnClientDisconnected;
            net.OnClientDisconnectCallback += OnClientDisconnected;
        }

        try
        {
            UpdateStatus("Đang khởi tạo dịch vụ...");
            await LobbyManager.Instance.InitializeAsync();
            await VivoxManager.Instance.LoginAsync();
            _isNetworkReady = true;
            SetButtonsInteractable(true);
            UpdateStatus("Sẵn sàng!");
        }
        catch (Exception e)
        {
            UpdateStatus($"Init failed: {e.Message}");
        }
    }

    private void OnDestroy()
    {
        var net = Net;
        if (net != null)
        {
            net.OnClientConnectedCallback -= OnClientConnected;
            net.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void SetButtonsInteractable(bool state)
    {
        if (playButton) playButton.interactable = state;
        if (continueButton) continueButton.interactable = state;
        if (openCreateRoomButton) openCreateRoomButton.interactable = state;
        if (refreshLobbiesButton) refreshLobbiesButton.interactable = state;
    }

    private void UpdateStatus(string msg) { if (statusText) statusText.text = msg; Debug.Log($"[MultiplayerCenter] {msg}"); }

    private void ShowPanel(GameObject p)
    {
        if (mainMenuPanel) mainMenuPanel.SetActive(false);
        if (lobbyListPanel) lobbyListPanel.SetActive(false);
        if (createRoomPanel) createRoomPanel.SetActive(false);
        if (joinPrivatePanel) joinPrivatePanel.SetActive(false);
        if (roomInfoPanel) roomInfoPanel.SetActive(false);
        if (p) p.SetActive(true);
    }

    private void SetCreateRoomMode(bool isPrivate)
    {
        isCreatingPrivateRoom = isPrivate;
        if (setPublicButton) setPublicButton.GetComponent<Image>().color = isPrivate ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
        if (setPrivateButton) setPrivateButton.GetComponent<Image>().color = isPrivate ? Color.white : new Color(0.5f, 0.5f, 0.5f);
    }

    private void OnPlayNewGame()
    {
        if (!_isNetworkReady) return;
        GlobalPlayerData.ClearData();
        Debug.Log("[MultiplayerCenter] Chơi mới — đã xóa save, chuyển vào LobbyList");
        ShowPanel(lobbyListPanel);
        OnRefreshLobbies();
    }

    private async void OnContinueGame()
    {
        if (!_isNetworkReady) return;
        GlobalPlayerData.Load();
        Debug.Log($"[MultiplayerCenter] Tiếp tục — credits = {GlobalPlayerData.credits}");
        
        string rName = string.IsNullOrEmpty(GlobalPlayerData.lastRoomName) ? 
            "Room_" + UnityEngine.Random.Range(1000, 9999) : GlobalPlayerData.lastRoomName;
            
        await CreateRoomAndShowInfo(rName, false);
    }

    private async Task CreateRoomAndShowInfo(string roomName, bool isPrivate)
    {
        try
        {
            SetButtonsInteractable(false);
            UpdateStatus("Đang tạo phòng...");

            Allocation alloc = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            string relayCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

            var lobby = await LobbyManager.Instance.CreateLobby(roomName, maxPlayers, isPrivate, relayCode);
            await VivoxManager.Instance.JoinChannelAsync(lobby.Id);

            var net = Net;
            if (net != null)
            {
                net.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(alloc, "dtls"));
                net.ConnectionApprovalCallback = ApprovalCheck;

                if (net.StartHost())
                {
                    UpdateStatus("Phòng đã tạo!");
                    
                    GlobalPlayerData.lastRoomName = lobby.Name;
                    GlobalPlayerData.Save();
                    
                    ShowRoomInfo(lobby);
                    return; // Thành công thì thoát
                }
            }
            
            // Nếu StartHost() trả về false hoặc Net null
            UpdateStatus("Không thể tạo phòng.");
            await SafeCleanupAndLeave();
            ShowPanel(mainMenuPanel);
        }
        catch (Exception e)
        {
            UpdateStatus($"Lỗi: {e.Message}");
            await SafeCleanupAndLeave();
            ShowPanel(mainMenuPanel);
        }
        finally
        {
            SetButtonsInteractable(true);
        }
    }

    private async void OnConfirmCreateRoom()
    {
        string rName = roomNameInput ? roomNameInput.text : "Room";
        if (string.IsNullOrEmpty(rName)) rName = "Room";
        bool isPrivate = isCreatingPrivateRoom;

        try
        {
            if (confirmCreateRoomButton) confirmCreateRoomButton.interactable = false;
            UpdateStatus("Đang tạo phòng...");

            Allocation alloc = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            string relayCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

            var lobby = await LobbyManager.Instance.CreateLobby(rName, maxPlayers, isPrivate, relayCode);
            await VivoxManager.Instance.JoinChannelAsync(lobby.Id);

            var net = Net;
            if (net != null)
            {
                net.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(alloc, "dtls"));
                net.ConnectionApprovalCallback = ApprovalCheck;

                if (net.StartHost())
                {
                    UpdateStatus("Phòng đã tạo!");
                    
                    // LƯU LẠI TÊN PHÒNG CHO LẦN "TIẾP TỤC" SAU NÀY
                    GlobalPlayerData.lastRoomName = rName;
                    GlobalPlayerData.Save();
                    
                    ShowRoomInfo(lobby);
                    return;
                }
            }

            UpdateStatus("Không thể tạo phòng.");
            await SafeCleanupAndLeave();
            ShowPanel(lobbyListPanel);
        }
        catch (Exception e)
        {
            UpdateStatus($"Error: {e.Message}");
            await SafeCleanupAndLeave();
            ShowPanel(lobbyListPanel);
        }
        finally
        {
            if (confirmCreateRoomButton) confirmCreateRoomButton.interactable = true;
        }
    }

    private async void OnRefreshLobbies()
    {
        try
        {
            if (refreshLobbiesButton) refreshLobbiesButton.interactable = false;
            UpdateStatus("Đang làm mới...");
            var lobbies = await LobbyManager.Instance.FindPublicLobbies();
            PopulateLobbyList(lobbies);
            UpdateStatus("Đã làm mới danh sách.");
        }
        catch (Exception e) { UpdateStatus($"Error: {e.Message}"); }
        finally { if (refreshLobbiesButton) refreshLobbiesButton.interactable = true; }
    }

    private void PopulateLobbyList(List<Lobby> lobbies)
    {
        if (lobbyListContainer == null || lobbyItemPrefab == null) return;
        foreach (Transform child in lobbyListContainer) { Destroy(child.gameObject); }
        foreach (var l in lobbies)
        {
            var item = Instantiate(lobbyItemPrefab, lobbyListContainer);
            item.SetActive(true);
            item.transform.localScale = Vector3.one;
            var txt = item.GetComponentInChildren<TMP_Text>();
            
            bool isPrivate = l.Data != null && l.Data.ContainsKey("IsPrivateMode") && l.Data["IsPrivateMode"].Value == "true";
            
            if (txt) txt.text = $"{l.Name} ({l.Players.Count}/{l.MaxPlayers})" + (isPrivate ? " [RIÊNG TƯ]" : "");
            
            var btn = item.GetComponent<Button>();
            if (btn) 
            {
                btn.onClick.AddListener(() => 
                {
                    if (isPrivate)
                    {
                        if (joinPrivatePanel != null)
                        {
                            _selectedPrivateLobbyToJoin = l;
                            if (joinCodeInput) joinCodeInput.text = "";
                            ShowPanel(joinPrivatePanel);
                        }
                        else
                        {
                            Debug.LogWarning("Missing JoinPrivatePanel UI!");
                            UpdateStatus("Chưa có giao diện JoinPrivatePanel!");
                        }
                    }
                    else
                    {
                        JoinLobby(l);
                    }
                });
            }
        }
    }

    private async void OnConfirmJoinPrivateRoom()
    {
        if (_selectedPrivateLobbyToJoin == null) return;
        
        string code = joinCodeInput != null ? joinCodeInput.text.Trim().ToUpper() : "";
        if (string.IsNullOrEmpty(code))
        {
            UpdateStatus("Vui lòng nhập mã phòng.");
            return;
        }

        try
        {
            if (confirmJoinButton) confirmJoinButton.interactable = false;
            UpdateStatus("Đang kiểm tra mã...");
            
            var joined = await LobbyManager.Instance.JoinLobbyByCode(code);
            
            if (joined.Id != _selectedPrivateLobbyToJoin.Id)
            {
                await SafeCleanupAndLeave();
                UpdateStatus("Mã phòng không hợp lệ.");
                return;
            }
            
            UpdateStatus("Đang vào phòng...");
            await VivoxManager.Instance.JoinChannelAsync(joined.Id);

            UpdateStatus("Đang lấy mã Relay...");
            string relayCode = LobbyManager.Instance.GetRelayCodeFromLobby();
            if (string.IsNullOrEmpty(relayCode))
            {
                await LobbyManager.Instance.ForceRefreshLobby();
                relayCode = LobbyManager.Instance.GetRelayCodeFromLobby();
            }
            
            var joinAlloc = await RelayService.Instance.JoinAllocationAsync(relayCode);

            var net = Net;
            if (net != null)
            {
                net.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(joinAlloc, "dtls"));

                if (net.StartClient()) { UpdateStatus("Đang kết nối..."); return; }
            }
            
            UpdateStatus("Kết nối thất bại"); 
            await SafeCleanupAndLeave();
        }
        catch (Exception e) 
        { 
            UpdateStatus("Mã sai hoặc phòng đã đầy."); 
            Debug.LogError($"Join Private Room Failed: {e.Message}");
            await SafeCleanupAndLeave();
        }
        finally 
        { 
            if (confirmJoinButton) confirmJoinButton.interactable = true; 
        }
    }

    private async void JoinLobby(Lobby lobby)
    {
        try
        {
            UpdateStatus("Đang vào phòng...");
            var joined = await LobbyManager.Instance.JoinLobbyById(lobby.Id);
            
            UpdateStatus("Đang vào kênh chat...");
            await VivoxManager.Instance.JoinChannelAsync(joined.Id);

            UpdateStatus("Đang lấy mã Relay...");
            string relayCode = LobbyManager.Instance.GetRelayCodeFromLobby();
            if (string.IsNullOrEmpty(relayCode))
            {
                UpdateStatus("Mã Relay trống, đang thử tải lại...");
                await LobbyManager.Instance.ForceRefreshLobby();
                relayCode = LobbyManager.Instance.GetRelayCodeFromLobby();
            }

            if (string.IsNullOrEmpty(relayCode))
            {
                UpdateStatus("Lỗi: Không lấy được mã Relay từ máy chủ.");
                await SafeCleanupAndLeave();
                return;
            }

            UpdateStatus("Đang kết nối Relay...");
            var joinAlloc = await RelayService.Instance.JoinAllocationAsync(relayCode);

            var net = Net;
            if (net != null)
            {
                net.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(joinAlloc, "dtls"));

                if (net.StartClient()) { UpdateStatus("Đang kết nối..."); return; }
            }
            
            UpdateStatus("Kết nối thất bại"); 
            await SafeCleanupAndLeave();
        }
        catch (Exception e) { 
            UpdateStatus($"Lỗi Public: {e.GetType().Name} - {e.Message}"); 
            Debug.LogError($"JoinLobby Public Error: {e}");
            await SafeCleanupAndLeave();
        }
    }

    private void ShowRoomInfo(Lobby lobby)
    {
        ShowPanel(roomInfoPanel);
        bool isHost = Net != null && Net.IsHost;

        if (editRoomNameInput) { editRoomNameInput.text = lobby.Name; editRoomNameInput.interactable = isHost; }
        if (roomCodeText) { roomCodeText.text = $"Mã phòng: {lobby.LobbyCode}"; roomCodeText.gameObject.SetActive(true); }
        bool isPrivateMode = lobby.Data != null && lobby.Data.ContainsKey("IsPrivateMode") && lobby.Data["IsPrivateMode"].Value == "true";
        if (roomTypeText) roomTypeText.text = isPrivateMode ? "Loại: RIÊNG TƯ" : "Loại: CÔNG KHAI";
        if (roomPlayersText) roomPlayersText.text = $"Người chơi: {lobby.Players.Count}/{lobby.MaxPlayers}";
        if (startWaitingButton) startWaitingButton.gameObject.SetActive(isHost);
    }

    private async void OnEditRoomNameEnd(string newName)
    {
        if (Net != null && Net.IsHost && LobbyManager.Instance.CurrentLobby != null && newName != LobbyManager.Instance.CurrentLobby.Name)
        {
            await LobbyManager.Instance.UpdateLobbyName(newName);
            GlobalPlayerData.lastRoomName = newName;
            GlobalPlayerData.Save();
        }
    }

    private void OnStartWaitingClicked()
    {
        if (Net && Net.IsHost)
            Net.SceneManager.LoadScene("Waiting", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    private void OnCopyCodeClicked()
    {
        var lobby = LobbyManager.Instance.CurrentLobby;
        if (lobby != null && !string.IsNullOrEmpty(lobby.LobbyCode))
        {
            GUIUtility.systemCopyBuffer = lobby.LobbyCode;
            UpdateStatus("Đã sao chép mã phòng!");
        }
    }

    // --- HÀM XỬ LÝ HỦY PHÒNG SẠCH SẼ (CHỐNG LỖI RACE CONDITION) ---
    private async void OnCancelRoomClicked()
    {
        if (cancelRoomButton) cancelRoomButton.interactable = false;
        
        var net = Net;
        bool wasHost = net != null && net.IsServer;
        
        await SafeCleanupAndLeave();

        ShowPanel(wasHost ? mainMenuPanel : lobbyListPanel);
        
        if (cancelRoomButton) cancelRoomButton.interactable = true;
    }

    // Dùng chung cho tất cả các tình huống cần dọn dẹp Network + Lobby
    private async Task SafeCleanupAndLeave()
    {
        _isLeaving = true; // Chặn OnClientDisconnected đổi UI lung tung
        var net = Net;

        if (net != null && (net.IsServer || net.IsClient))
        {
            net.Shutdown();
        }

        await LobbyManager.Instance.LeaveLobby();

        if (net != null)
        {
            // Chờ cho NetworkManager dọn dẹp hoàn toàn
            while (net.ShutdownInProgress)
            {
                await Task.Yield();
            }
        }
        
        _isLeaving = false; 
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
    {
        string sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sc != "StartGame" && sc != "Waiting") { res.Approved = false; res.Reason = "Game is running"; return; }
        if (Net.ConnectedClientsIds.Count >= maxPlayers) { res.Approved = false; res.Reason = "Full"; return; }
        res.Approved = true; res.CreatePlayerObject = true;
    }

    private void OnClientConnected(ulong id)
    {
        var net = Net;
        if (net != null && net.IsServer)
        {
            if (roomPlayersText) roomPlayersText.text = $"Người chơi: {net.ConnectedClientsIds.Count}/{maxPlayers}";
        }
        
        if (net != null && id == net.LocalClientId && !net.IsServer)
        {
            ShowRoomInfo(LobbyManager.Instance.CurrentLobby);
        }
    }

    private async void OnClientDisconnected(ulong id)
    {
        if (_isLeaving) return; // Nếu đang chủ động ngắt kết nối thì bỏ qua

        var net = Net;
        if (net != null && net.IsServer)
        {
            if (roomPlayersText) roomPlayersText.text = $"Người chơi: {net.ConnectedClientsIds.Count}/{maxPlayers}";
        }
        else if (net == null || id == net.LocalClientId || id == 0)
        {
            // Bị văng khỏi server (Host đóng phòng hoặc rớt mạng)
            await SafeCleanupAndLeave();
            ShowPanel(lobbyListPanel);
            UpdateStatus("Đã ngắt kết nối khỏi máy chủ");
        }
    }

    private void AutoAssignUI()
    {
        GameObject canvasObj = GameObject.Find("Canvas");
        Canvas canvas = canvasObj != null ? canvasObj.GetComponent<Canvas>() : FindAnyObjectByType<Canvas>();
        if (!canvas) 
        {
            Debug.LogError("[MultiplayerCenter] Cảnh báo: Không tìm thấy Canvas gốc!");
            return;
        }

        foreach (var p in canvas.GetComponentsInChildren<Transform>(true))
        {
            string n = p.name.ToLower();
            if (n.Contains("mainmenu") && n.Contains("panel")) mainMenuPanel = p.gameObject;
            if (n.Contains("lobbylist") && n.Contains("panel")) lobbyListPanel = p.gameObject;
            if (n.Contains("createroom") && n.Contains("panel")) createRoomPanel = p.gameObject;
            if (n.Contains("joinprivate") && n.Contains("panel")) joinPrivatePanel = p.gameObject;
            if (n.Contains("roominfo") && n.Contains("panel")) roomInfoPanel = p.gameObject;
            if (n.Contains("container")) lobbyListContainer = p;
        }

        foreach (var b in canvas.GetComponentsInChildren<Button>(true))
        {
            string n = b.name.ToLower();
            if (n == "playbutton") playButton = b;
            if (n == "continuebutton") continueButton = b;
            if (n == "instructbutton") instructButton = b;
            if (n == "exitbutton") exitButton = b;

            if (n == "createroombutton") openCreateRoomButton = b;
            if (n == "refreshbutton") refreshLobbiesButton = b;
            if (n == "backbutton") backToMenuButton = b;

            if (n == "setpublicbutton") setPublicButton = b;
            if (n == "setprivatebutton") setPrivateButton = b;
            if (n == "confirmcreatebutton") confirmCreateRoomButton = b;
            if (n == "cancelbutton") cancelCreateRoomButton = b;

            if (n == "confirmjoinbutton") confirmJoinButton = b;
            if (n == "canceljoinbutton") cancelJoinButton = b;

            if (n == "startwaitingbutton") startWaitingButton = b;
            if (n == "copycodebutton") copyCodeButton = b;
            if (n == "cancelroombutton") cancelRoomButton = b;
        }

        foreach (var i in canvas.GetComponentsInChildren<TMP_InputField>(true))
        {
            string n = i.name.ToLower();
            if (n == "roomnameinput") roomNameInput = i;
            if (n == "editroomnameinput") editRoomNameInput = i;
            if (n == "joincodeinput") joinCodeInput = i;
        }

        foreach (var t in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            string n = t.name.ToLower();
            if (n == "status") statusText = t;
            if (n == "roomtypetext") roomTypeText = t;
            if (n == "roomcodetext") roomCodeText = t;
            if (n == "roomplayerstext") roomPlayersText = t;
        }
    }
}
