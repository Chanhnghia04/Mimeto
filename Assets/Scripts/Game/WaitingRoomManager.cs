using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Quản lý phòng chờ (Waiting scene).
/// Hiển thị lobby code, danh sách player, nút Start (host only).
/// Khi host bấm Start → lock lobby + load Map → client không join được nữa.
/// </summary>
public class WaitingRoomManager : NetworkBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text lobbyNameText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private Button startButton;
    [SerializeField] private Button leaveButton;
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private GameObject playerItemPrefab;

    private NetworkList<ulong> connectedPlayers;

    

    public override void OnNetworkSpawn()
    {
        // Hiển thị thông tin lobby
        if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobby != null)
        {
            var lobby = LobbyManager.Instance.CurrentLobby;
            if (roomCodeText != null)
                roomCodeText.text = $"Mã phòng: {lobby.LobbyCode}";
            if (lobbyNameText != null)
                lobbyNameText.text = lobby.Name;
        }

        // Chỉ Host thấy nút Start
        if (startButton != null)
            startButton.gameObject.SetActive(IsHost);

        // Gán events
        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);

        // Theo dõi player list
        connectedPlayers.OnListChanged += OnPlayerListChanged;

        if (IsServer)
        {
            // Thêm players hiện có
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (!connectedPlayers.Contains(client.ClientId))
                    connectedPlayers.Add(client.ClientId);
            }

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        UpdateUI();
    }

    public override void OnNetworkDespawn()
    {
        connectedPlayers.OnListChanged -= OnPlayerListChanged;

        if (IsServer)
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (IsServer && !connectedPlayers.Contains(clientId))
        {
            connectedPlayers.Add(clientId);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (IsServer && connectedPlayers.Contains(clientId))
        {
            connectedPlayers.Remove(clientId);
        }
    }

    private void OnPlayerListChanged(NetworkListEvent<ulong> changeEvent)
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        int max = LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobby != null
            ? LobbyManager.Instance.CurrentLobby.MaxPlayers
            : 4;

        if (playerCountText != null)
        {
            playerCountText.text = $"Người chơi: {connectedPlayers.Count}/{max}";
        }

        // Cập nhật danh sách player
        if (playerListContainer != null)
        {
            foreach (Transform child in playerListContainer)
            {
                Destroy(child.gameObject);
            }

            for (int i = 0; i < connectedPlayers.Count; i++)
            {
                if (playerItemPrefab != null)
                {
                    var item = Instantiate(playerItemPrefab, playerListContainer);
                    var text = item.GetComponentInChildren<TMP_Text>();
                    if (text != null)
                    {
                        bool isMe = connectedPlayers[i] == NetworkManager.Singleton.LocalClientId;
                        bool isHost = connectedPlayers[i] == 0; // Host luôn là clientId 0
                        string label = $"Player {i + 1}";
                        if (isHost) label += " (Host)";
                        if (isMe) label += " ★";
                        text.text = label;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Host bấm Start → lock lobby → load Map
    /// </summary>
    private async void OnStartClicked()
    {
        if (!IsHost) return;

        // 1. Lock lobby — không ai join được nữa
        if (LobbyManager.Instance != null)
        {
            await LobbyManager.Instance.SetGameStarted();
        }

        // 2. Load Map scene (tất cả client sẽ tự động load theo)
        NetworkManager.Singleton.SceneManager.LoadScene("Map", LoadSceneMode.Single);
    }

    /// <summary>
    /// Rời phòng
    /// </summary>
    private async void OnLeaveClicked()
    {
        // Rời lobby
        if (LobbyManager.Instance != null)
        {
            await LobbyManager.Instance.LeaveLobby();
        }

        // Shutdown network
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene("Menu");
    }
}
