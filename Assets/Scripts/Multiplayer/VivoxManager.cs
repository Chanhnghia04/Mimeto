using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Vivox;

public class VivoxManager : MonoBehaviour
{
    private static VivoxManager _instance;
    public static VivoxManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("VivoxManager");
                _instance = go.AddComponent<VivoxManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private string currentChannelName;
    private string echoChannelName;
    private bool isLoggedIn = false;
    private bool isJoined = false;
    private bool isEchoTest = false;
    
    // Lưu Transform của Player Local (Client này) để cập nhật âm thanh 3D
    private Transform localPlayerTransform;
    private Transform localCameraTransform;

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

    /// <summary>
    /// Khởi tạo và Đăng nhập Vivox sau khi đã Auth
    /// </summary>
    public async Task LoginAsync()
    {
        if (isLoggedIn) return;

        try
        {
            // Vivox tự khởi tạo cùng UnityServices.InitializeAsync() rồi
            // Chỉ cần kiểm tra xem nó đã sẵn sàng chưa
            if (VivoxService.Instance == null)
            {
                Debug.LogWarning("[VivoxManager] VivoxService chưa sẵn sàng. Hãy kích hoạt Vivox trên Unity Dashboard (Edit → Project Settings → Services → Vivox).");
                return;
            }

            LoginOptions options = new LoginOptions
            {
                DisplayName = "Player_" + AuthenticationService.Instance.PlayerId.Substring(0, 6)
            };

            await VivoxService.Instance.LoginAsync(options);
            isLoggedIn = true;
            Debug.Log("[VivoxManager] Logged in to Vivox successfully.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VivoxManager] Vivox chưa được setup trên Dashboard, voice chat tạm tắt. ({e.Message})");
        }
    }

    /// <summary>
    /// Tham gia kênh âm thanh dựa trên Lobby ID (dùng chế độ Positional / 3D)
    /// </summary>
    public async Task JoinChannelAsync(string lobbyId)
    {
        if (!isLoggedIn)
        {
            Debug.LogWarning("[VivoxManager] Cannot join channel, not logged in.");
            return;
        }

        // Rời echo test nếu đang chạy
        if (isEchoTest)
        {
            await LeaveEchoTestAsync();
        }

        if (isJoined)
        {
            await LeaveChannelAsync();
        }

        try
        {
            currentChannelName = "Lobby_" + lobbyId;
            
            ChannelOptions channelOptions = new ChannelOptions
            {
                // Sử dụng kênh Positional để mô phỏng âm thanh 3D (Proximity)
                MakeActiveChannelUponJoining = true
            };

            // JoinPositionalChannelAsync yêu cầu cấu hình môi trường 3D cơ bản
            // Conversational: kênh nói chuyện bình thường. Positional: kênh 3D
            Channel3DProperties props = new Channel3DProperties(
                32, // AudibleDistance: Khoảng cách nghe thấy max (32 mét)
                1,  // ConversationalDistance: Khoảng cách nghe rõ 100% (1 mét)
                1.0f, // AudioFadeIntensity
                AudioFadeModel.InverseByDistance // Mô hình nhỏ dần theo khoảng cách
            );

            await VivoxService.Instance.JoinPositionalChannelAsync(currentChannelName, ChatCapability.AudioOnly, props, channelOptions);
            isJoined = true;
            Debug.Log($"[VivoxManager] Joined 3D Voice Channel: {currentChannelName}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[VivoxManager] Join Channel failed: {e.Message}");
        }
    }

    /// <summary>
    /// Rời kênh hiện tại
    /// </summary>
    public async Task LeaveChannelAsync()
    {
        if (!isJoined || string.IsNullOrEmpty(currentChannelName)) return;

        try
        {
            await VivoxService.Instance.LeaveChannelAsync(currentChannelName);
            isJoined = false;
            currentChannelName = null;
            Debug.Log("[VivoxManager] Left Voice Channel.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[VivoxManager] Leave Channel failed: {e.Message}");
        }
    }

    // =============================================
    // ECHO TEST — Nói vào mic → nghe lại chính mình
    // =============================================

    /// <summary>
    /// Bắt đầu Echo Test: nói vào mic sẽ nghe lại giọng mình ngay lập tức.
    /// Dùng để kiểm tra mic có hoạt động không.
    /// Gọi bằng Console hoặc UI button.
    /// </summary>
    public async Task StartEchoTestAsync()
    {
        if (!isLoggedIn)
        {
            Debug.LogWarning("[VivoxManager] Phải đăng nhập trước khi test echo.");
            return;
        }

        if (isEchoTest)
        {
            Debug.Log("[VivoxManager] Echo Test đang chạy hoặc đang kết nối.");
            return;
        }

        isEchoTest = true; // Khóa ngay lập tức để tránh gọi 2 lần cùng lúc

        try
        {
            echoChannelName = "EchoTest_" + AuthenticationService.Instance.PlayerId.Substring(0, 8);
            await VivoxService.Instance.JoinEchoChannelAsync(echoChannelName, ChatCapability.AudioOnly);
            Debug.Log("<color=cyan>[VivoxManager] 🎤 ECHO TEST BẮT ĐẦU — Hãy nói vào mic, bạn sẽ nghe lại giọng mình!</color>");
        }
        catch (Exception e)
        {
            isEchoTest = false;
            Debug.LogError($"[VivoxManager] Echo Test failed: {e.Message}");
        }
    }

    /// <summary>
    /// Dừng Echo Test
    /// </summary>
    public async Task LeaveEchoTestAsync()
    {
        if (!isEchoTest || string.IsNullOrEmpty(echoChannelName)) return;

        isEchoTest = false; // Khóa ngay lập tức

        try
        {
            string channelToLeave = echoChannelName;
            echoChannelName = null;
            await VivoxService.Instance.LeaveChannelAsync(channelToLeave);
            Debug.Log("<color=cyan>[VivoxManager] 🎤 ECHO TEST KẾT THÚC.</color>");
        }
        catch (Exception e)
        {
            isEchoTest = true;
            Debug.LogError($"[VivoxManager] Leave Echo failed: {e.Message}");
        }
    }

    /// <summary>
    /// Toggle Echo Test (bật/tắt) — tiện gọi từ UI button
    /// </summary>
    public async void ToggleEchoTest()
    {
        if (isEchoTest)
            await LeaveEchoTestAsync();
        else
            await StartEchoTestAsync();
    }

    /// <summary>
    /// Thiết lập Transform của Local Player để Vivox biết vị trí phát/thu âm thanh
    /// </summary>
    public void SetLocalPlayerTransform(Transform playerT, Transform cameraT)
    {
        localPlayerTransform = playerT;
        localCameraTransform = cameraT;
    }

    private void Update()
    {
        // Liên tục cập nhật vị trí 3D cho Vivox nếu đã join channel và có player
        if (isJoined && isLoggedIn && localPlayerTransform != null && localCameraTransform != null)
        {
            // Set3DPosition: (speakerPos, listenerPos, forward, up, channelName)
            VivoxService.Instance.Set3DPosition(
                localCameraTransform.position,  // Speaker Pos
                localCameraTransform.position,  // Listener Pos
                localCameraTransform.forward,   // Listener Forward
                localCameraTransform.up,        // Listener Up
                currentChannelName              // Channel name
            );
        }
    }

    private async void OnDestroy()
    {
        if (isEchoTest) await LeaveEchoTestAsync();
        if (isJoined) await LeaveChannelAsync();
        if (isLoggedIn && VivoxService.Instance != null)
        {
            await VivoxService.Instance.LogoutAsync();
        }
    }
}
