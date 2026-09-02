using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Audio;
using Unity.Netcode;

public class SettingsUI : MonoBehaviour
{
    [Header("Main Settings Panel")]
    public GameObject settingsPanel;
    public Button closeButton;

    [Header("Tabs Buttons")]
    public Button tabRoomButton;
    public Button tabAudioButton;
    public Button tabGraphicsButton;
    public Button tabControlsButton;

    [Header("Tab Panels")]
    public GameObject roomPanel;
    public GameObject audioPanel;
    public GameObject graphicsPanel;
    public GameObject controlsPanel;


    [Header("--- ROOM INFO ---")]
    public TextMeshProUGUI roomNameText;
    public TextMeshProUGUI playerCountText;
    public TextMeshProUGUI pingText;
    public Button leaveRoomButton;

    [Header("--- AUDIO ---")]
    public AudioMixer mainAudioMixer;
    public Slider masterVolumeSlider;
    public Slider sfxVolumeSlider;
    public Slider musicVolumeSlider;

    [Header("--- GRAPHICS ---")]
    public TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown qualityDropdown;
    public Toggle fullscreenToggle;

    private Resolution[] resolutions;

    public static SettingsUI Instance;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        // Giữ Settings tồn tại xuyên suốt các màn chơi
        DontDestroyOnLoad(gameObject);
        if (settingsPanel != null && settingsPanel.transform.root != transform)
        {
            DontDestroyOnLoad(settingsPanel.transform.root.gameObject);
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    private void Start()
    {
        // 1. Setup Tab Navigation
        if (tabRoomButton != null) tabRoomButton.onClick.AddListener(() => ShowTab(roomPanel));
        if (tabAudioButton != null) tabAudioButton.onClick.AddListener(() => ShowTab(audioPanel));
        if (tabGraphicsButton != null) tabGraphicsButton.onClick.AddListener(() => ShowTab(graphicsPanel));

        // Only add controls tab if references are assigned (auto-created by PauseMenuUI)
        if (tabControlsButton != null && controlsPanel != null)
        {
            tabControlsButton.onClick.AddListener(() => ShowTab(controlsPanel));
        }

        if (closeButton != null) closeButton.onClick.AddListener(CloseSettings);
        if (leaveRoomButton != null) leaveRoomButton.onClick.AddListener(LeaveRoom);

        // 2. Setup Graphics Settings
        InitializeGraphicsSettings();

        // 3. Setup Audio Settings
        InitializeAudioSettings();
    }

    private void Update()
    {
        // Cập nhật thông tin phòng liên tục nếu tab Room đang mở
        if (settingsPanel.activeSelf && roomPanel.activeSelf)
        {
            UpdateRoomInfo();
        }
    }

    public void OpenSettings()
    {
        settingsPanel.SetActive(true);
        ShowTab(roomPanel);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "StartGame")
        {
            if (PauseMenuUI.Instance != null && !PauseMenuUI.Instance.IsOpen)
            {
                PauseMenuUI.Instance.OpenMenu();
            }
        }
    }

    public bool IsOpen => settingsPanel != null && settingsPanel.activeSelf;

    private void ShowTab(GameObject targetTab)
    {
        roomPanel.SetActive(false);
        audioPanel.SetActive(false);
        graphicsPanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(false);

        targetTab.SetActive(true);
    }

    // ================== ROOM INFO ==================
    private void UpdateRoomInfo()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            string hostType = NetworkManager.Singleton.IsServer ? "Host" : "Client";
            roomNameText.text = $"Room: [Network Active] - You are {hostType}";
            
            if (NetworkManager.Singleton.IsServer)
            {
                playerCountText.text = $"Players: {NetworkManager.Singleton.ConnectedClientsIds.Count} / 4";
            }
            else
            {
                playerCountText.text = "Players: Connected";
            }
            
            // Unity NGO doesn't have built-in ping without Unity Transport modifications, so we use a placeholder or check RTT
            pingText.text = "Ping: <color=green>Good</color>"; 
        }
        else
        {
            roomNameText.text = "Room: Offline";
            playerCountText.text = "Players: 1 / 1";
            pingText.text = "Ping: 0ms";
        }
    }

    private void LeaveRoom()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene("StartGame");
    }

    // ================== AUDIO ==================
    private void InitializeAudioSettings()
    {
        // Lấy giá trị cũ từ PlayerPrefs
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = PlayerPrefs.GetFloat("MasterVol", 1f);
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        }
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = PlayerPrefs.GetFloat("SFXVol", 1f);
            sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);
        }
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = PlayerPrefs.GetFloat("MusicVol", 1f);
            musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        }
    }

    public void SetMasterVolume(float sliderValue)
    {
        // Chuyển từ linear (0.0001 -> 1) sang Decibel (-80 -> 0)
        float db = Mathf.Log10(Mathf.Max(sliderValue, 0.0001f)) * 20;
        if (mainAudioMixer != null) mainAudioMixer.SetFloat("MasterVol", db);
        PlayerPrefs.SetFloat("MasterVol", sliderValue);
        AudioListener.volume = sliderValue; // Fallback nếu không dùng AudioMixer
    }

    public void SetSFXVolume(float sliderValue)
    {
        float db = Mathf.Log10(Mathf.Max(sliderValue, 0.0001f)) * 20;
        if (mainAudioMixer != null) mainAudioMixer.SetFloat("SFXVol", db);
        PlayerPrefs.SetFloat("SFXVol", sliderValue);
    }

    public void SetMusicVolume(float sliderValue)
    {
        float db = Mathf.Log10(Mathf.Max(sliderValue, 0.0001f)) * 20;
        if (mainAudioMixer != null) mainAudioMixer.SetFloat("MusicVol", db);
        PlayerPrefs.SetFloat("MusicVol", sliderValue);
    }

    // ================== GRAPHICS ==================
    private void InitializeGraphicsSettings()
    {
        // Cài đặt Dropdown độ phân giải
        if (resolutionDropdown != null)
        {
            resolutions = Screen.resolutions;
            resolutionDropdown.ClearOptions();

            List<string> options = new List<string>();
            int currentResIndex = 0;

            for (int i = 0; i < resolutions.Length; i++)
            {
                string option = resolutions[i].width + " x " + resolutions[i].height + " @ " + resolutions[i].refreshRateRatio.value + "hz";
                options.Add(option);

                if (resolutions[i].width == Screen.currentResolution.width &&
                    resolutions[i].height == Screen.currentResolution.height)
                {
                    currentResIndex = i;
                }
            }

            resolutionDropdown.AddOptions(options);
            resolutionDropdown.value = PlayerPrefs.GetInt("ResIndex", currentResIndex);
            resolutionDropdown.RefreshShownValue();
            resolutionDropdown.onValueChanged.AddListener(SetResolution);
        }

        // Cài đặt Dropdown Quality
        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
            qualityDropdown.value = QualitySettings.GetQualityLevel();
            qualityDropdown.RefreshShownValue();
            qualityDropdown.onValueChanged.AddListener(SetQuality);
        }

        // Cài đặt Fullscreen
        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = Screen.fullScreen;
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }
    }

    public void SetResolution(int resIndex)
    {
        Resolution res = resolutions[resIndex];
        Screen.SetResolution(res.width, res.height, Screen.fullScreen);
        PlayerPrefs.SetInt("ResIndex", resIndex);
    }

    public void SetQuality(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }
}
