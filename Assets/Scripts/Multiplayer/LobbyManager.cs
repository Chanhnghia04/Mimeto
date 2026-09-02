using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

/// <summary>
/// Quản lý Unity Lobby Service - tạo/tìm/join lobby, heartbeat, cleanup.
/// Singleton, DontDestroyOnLoad.
/// </summary>
public class LobbyManager : MonoBehaviour
{
    private static LobbyManager _instance;
    public static LobbyManager Instance 
    { 
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("LobbyManager");
                _instance = go.AddComponent<LobbyManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    public Lobby CurrentLobby { get; private set; }
    public bool IsLobbyHost { get; private set; }

    // Lobby data keys
    public const string KEY_RELAY_CODE = "RelayJoinCode";
    public const string KEY_GAME_STARTED = "GameStarted";

    private float heartbeatTimer;
    private float lobbyPollTimer;
    private const float HEARTBEAT_INTERVAL = 15f;
    private const float LOBBY_POLL_INTERVAL = 2f;

    public event Action<Lobby> OnLobbyUpdated;
    public event Action OnKickedFromLobby;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        HandleHeartbeat();
        HandleLobbyPoll();
    }

    /// <summary>
    /// Khởi tạo Unity Services + đăng nhập ẩn danh
    /// </summary>
    public async Task InitializeAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            InitializationOptions options = new InitializationOptions();
            
#if UNITY_EDITOR
            string profileId = "Editor_" + System.Guid.NewGuid().ToString().Substring(0, 8);
#else
            string profileId = "Player_" + Mathf.Abs(SystemInfo.deviceUniqueIdentifier.GetHashCode()).ToString();
#endif
            options.SetProfile(profileId);

            await UnityServices.InitializeAsync(options);
        }
        
        // Đảm bảo session cũ không bị kẹt
        AuthenticationService.Instance.ClearSessionToken();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"[LobbyManager] Signed in. Player ID: {AuthenticationService.Instance.PlayerId}");
        }
    }

    /// <summary>
    /// Tạo lobby mới (Public hoặc Private)
    /// </summary>
    public async Task<Lobby> CreateLobby(string lobbyName, int maxPlayers, bool isPrivate, string relayJoinCode)
    {
        try
        {
            var options = new CreateLobbyOptions
            {
                // LUÔN LUÔN set IsPrivate = false để nó hiện lên danh sách QueryLobbies.
                // Trạng thái Private thực sự sẽ được lưu trong Data để UI xử lý hiển thị.
                IsPrivate = false,
                Player = GetPlayer(),
                Data = new Dictionary<string, DataObject>
                {
                    { KEY_RELAY_CODE, new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) },
                    { KEY_GAME_STARTED, new DataObject(DataObject.VisibilityOptions.Public, "false", DataObject.IndexOptions.S1) },
                    { "IsPrivateMode", new DataObject(DataObject.VisibilityOptions.Public, isPrivate ? "true" : "false", DataObject.IndexOptions.S2) }
                }
            };

            CurrentLobby = await Lobbies.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            IsLobbyHost = true;

            Debug.Log($"[LobbyManager] Lobby created: {CurrentLobby.Name} | ID: {CurrentLobby.Id} | IsPrivate: {isPrivate}");
            return CurrentLobby;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[LobbyManager] Create lobby failed: {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// Tìm lobby public đang mở (chưa bắt đầu game)
    /// </summary>
    public async Task<List<Lobby>> FindPublicLobbies()
    {
        try
        {
            var options = new QueryLobbiesOptions
            {
                Count = 20,
                Filters = new List<QueryFilter>
                {
                    // Chỉ lấy lobby có slot trống
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT),
                    // Chỉ lấy lobby chưa bắt đầu game
                    new QueryFilter(QueryFilter.FieldOptions.S1, "false", QueryFilter.OpOptions.EQ)
                },
                Order = new List<QueryOrder>
                {
                    new QueryOrder(false, QueryOrder.FieldOptions.Created)
                }
            };

            var result = await Lobbies.Instance.QueryLobbiesAsync(options);
            Debug.Log($"[LobbyManager] Found {result.Results.Count} public lobbies");
            return result.Results;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[LobbyManager] Query lobbies failed: {e.Message}");
            return new List<Lobby>();
        }
    }

    /// <summary>
    /// Join lobby bằng lobby code (Private)
    /// </summary>
    public async Task<Lobby> JoinLobbyByCode(string lobbyCode)
    {
        try
        {
            var options = new JoinLobbyByCodeOptions
            {
                Player = GetPlayer()
            };

            CurrentLobby = await Lobbies.Instance.JoinLobbyByCodeAsync(lobbyCode, options);
            IsLobbyHost = false;

            Debug.Log($"[LobbyManager] Joined lobby by code: {CurrentLobby.Name}");
            return CurrentLobby;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[LobbyManager] Join by code failed: {e.Message}");
            throw;
        }
    }

    public async Task UpdateLobbyName(string newName)
    {
        if (CurrentLobby == null) return;
        try
        {
            UpdateLobbyOptions options = new UpdateLobbyOptions { Name = newName };
            CurrentLobby = await Lobbies.Instance.UpdateLobbyAsync(CurrentLobby.Id, options);
            OnLobbyUpdated?.Invoke(CurrentLobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[LobbyManager] Failed to update lobby name: {e}");
        }
    }

    /// <summary>
    /// Join lobby bằng lobby ID (Public)
    /// </summary>
    public async Task<Lobby> JoinLobbyById(string lobbyId)
    {
        try
        {
            var options = new JoinLobbyByIdOptions
            {
                Player = GetPlayer()
            };

            CurrentLobby = await Lobbies.Instance.JoinLobbyByIdAsync(lobbyId, options);
            IsLobbyHost = false;

            Debug.Log($"[LobbyManager] Joined lobby by ID: {CurrentLobby.Name}");
            return CurrentLobby;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[LobbyManager] Join by ID failed: {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// Cập nhật Relay Join Code vào lobby data (Host gọi sau khi tạo Relay)
    /// </summary>
    public async Task UpdateRelayCode(string relayJoinCode)
    {
        if (CurrentLobby == null || !IsLobbyHost) return;

        try
        {
            CurrentLobby = await Lobbies.Instance.UpdateLobbyAsync(CurrentLobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { KEY_RELAY_CODE, new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
                }
            });
            Debug.Log($"[LobbyManager] Relay code updated: {relayJoinCode}");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[LobbyManager] Update relay code failed: {e.Message}");
        }
    }

    /// <summary>
    /// Đánh dấu game đã bắt đầu — lobby sẽ không hiển thị cho player mới
    /// </summary>
    public async Task SetGameStarted()
    {
        if (CurrentLobby == null || !IsLobbyHost) return;

        try
        {
            CurrentLobby = await Lobbies.Instance.UpdateLobbyAsync(CurrentLobby.Id, new UpdateLobbyOptions
            {
                IsLocked = true,
                Data = new Dictionary<string, DataObject>
                {
                    { KEY_GAME_STARTED, new DataObject(DataObject.VisibilityOptions.Public, "true", DataObject.IndexOptions.S1) }
                }
            });
            Debug.Log("[LobbyManager] Game started — lobby locked");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[LobbyManager] Set game started failed: {e.Message}");
        }
    }

    /// <summary>
    /// Lấy relay join code từ lobby data
    /// </summary>
    public string GetRelayCodeFromLobby()
    {
        if (CurrentLobby?.Data != null &&
            CurrentLobby.Data.TryGetValue(KEY_RELAY_CODE, out var data))
        {
            return data.Value;
        }
        return null;
    }

    /// <summary>
    /// Rời lobby
    /// </summary>
    public async Task LeaveLobby()
    {
        if (CurrentLobby == null) return;

        try
        {
            string playerId = AuthenticationService.Instance.PlayerId;

            if (IsLobbyHost)
            {
                await Lobbies.Instance.DeleteLobbyAsync(CurrentLobby.Id);
                Debug.Log("[LobbyManager] Lobby deleted (host left)");
            }
            else
            {
                await Lobbies.Instance.RemovePlayerAsync(CurrentLobby.Id, playerId);
                Debug.Log("[LobbyManager] Left lobby");
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[LobbyManager] Leave lobby failed: {e.Message}");
        }
        finally
        {
            CurrentLobby = null;
            IsLobbyHost = false;
        }
    }

    /// <summary>
    /// Heartbeat để giữ lobby sống (chỉ host)
    /// </summary>
    private async void HandleHeartbeat()
    {
        if (CurrentLobby == null || !IsLobbyHost) return;

        heartbeatTimer -= Time.deltaTime;
        if (heartbeatTimer <= 0f)
        {
            heartbeatTimer = HEARTBEAT_INTERVAL;
            try
            {
                await Lobbies.Instance.SendHeartbeatPingAsync(CurrentLobby.Id);
            }
            catch (LobbyServiceException e)
            {
                Debug.LogWarning($"[LobbyManager] Heartbeat failed: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Poll lobby để cập nhật danh sách player (client)
    /// </summary>
    private async void HandleLobbyPoll()
    {
        if (CurrentLobby == null) return;

        lobbyPollTimer -= Time.deltaTime;
        if (lobbyPollTimer <= 0f)
        {
            lobbyPollTimer = LOBBY_POLL_INTERVAL;
            try
            {
                CurrentLobby = await Lobbies.Instance.GetLobbyAsync(CurrentLobby.Id);
                OnLobbyUpdated?.Invoke(CurrentLobby);

                // Kiểm tra bị kick
                if (!IsPlayerInLobby())
                {
                    CurrentLobby = null;
                    IsLobbyHost = false;
                    OnKickedFromLobby?.Invoke();
                }
            }
            catch (LobbyServiceException e)
            {
                // Lobby có thể đã bị xóa
                if (e.Reason == LobbyExceptionReason.LobbyNotFound)
                {
                    CurrentLobby = null;
                    IsLobbyHost = false;
                    OnKickedFromLobby?.Invoke();
                }
            }
        }
    }

    private bool IsPlayerInLobby()
    {
        if (CurrentLobby == null) return false;
        string playerId = AuthenticationService.Instance.PlayerId;
        foreach (var player in CurrentLobby.Players)
        {
            if (player.Id == playerId) return true;
        }
        return false;
    }

    /// <summary>
    /// Chủ động cập nhật thông tin lobby ngay lập tức thay vì đợi timer
    /// </summary>
    public async Task ForceRefreshLobby()
    {
        if (CurrentLobby == null) return;
        try
        {
            CurrentLobby = await Lobbies.Instance.GetLobbyAsync(CurrentLobby.Id);
            OnLobbyUpdated?.Invoke(CurrentLobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"[LobbyManager] Force refresh failed: {e.Message}");
        }
    }

    private Player GetPlayer()
    {
        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, 
                    $"Player_{AuthenticationService.Instance.PlayerId[..6]}") }
            }
        };
    }

    private async void OnApplicationQuit()
    {
        await LeaveLobby();
    }
}
