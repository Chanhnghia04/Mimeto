using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Trạm dịch chuyển Multiplayer.
/// Tự động đếm ngược 5 giây khi tất cả người chơi đứng vào trong khu vực.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class LevelTransportStation : MonoBehaviour, IInteractable
{
    [Header("Settings")]
    public string targetScene = "Map";
    public string interactHint = "Waiting for all players...";
    
    private Camera _mainCam;
    private PlayerInventory _localPlayerInv;
    private Transform _localPlayer;
    private bool _playerNearby = false;
    private Texture2D _hintBgTex;
    private static readonly Color COL_CYAN = new Color(0.000f, 0.949f, 1.000f);

    private float _countdownTimer = 5f;
    private bool _hasTriggered = false;
    private int _playersInZone = 0;
    private int _totalPlayers = 0;

    void Update()
    {
        PlayerInventory[] allPlayers = FindObjectsByType<PlayerInventory>();
        _totalPlayers = allPlayers.Length;
        _playersInZone = 0;
        
        foreach (var p in allPlayers)
        {
            if (Vector3.Distance(p.transform.position, transform.position) <= 5f)
            {
                _playersInZone++;
            }
            if (p.IsOwner)
            {
                _localPlayerInv = p;
                _localPlayer = p.transform;
            }
        }

        if (_totalPlayers > 0 && _playersInZone == _totalPlayers)
        {
            _playerNearby = true; // Force show UI
            if (!_hasTriggered)
            {
                _countdownTimer -= Time.deltaTime;
                
                if (_countdownTimer <= 0f)
                {
                    _hasTriggered = true;
                    if (Unity.Netcode.NetworkManager.Singleton != null)
                    {
                        if (Unity.Netcode.NetworkManager.Singleton.IsServer)
                        {
                            // ★ Tạo seed mới → items/chests/enemies/escape đều random lại
                            foreach (var client in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsList)
                            {
                                var inv = client.PlayerObject?.GetComponent<PlayerInventory>();
                                if (inv != null && inv.IsServer)
                                {
                                    inv.RegenerateSeed();
                                    break; // Chỉ cần 1 lần — seed là NetworkVariable, tự sync
                                }
                            }

                            Debug.Log("[Transport] Server loading map with NEW seed...");
                            Unity.Netcode.NetworkManager.Singleton.SceneManager.LoadScene(targetScene, LoadSceneMode.Single);
                        }
                    }
                    else
                    {
                        SceneManager.LoadScene(targetScene);
                    }
                }
            }
        }
        else
        {
            _countdownTimer = 5f;
            _hasTriggered = false;
            if (_localPlayer != null)
            {
                _playerNearby = Vector3.Distance(_localPlayer.position, transform.position) <= 5f;
            }
            else
            {
                _playerNearby = false;
            }
        }

        if (_mainCam == null || !_mainCam.gameObject.activeInHierarchy) _mainCam = Camera.main;
    }

    public void Interact(GameObject interactor)
    {
        // Tắt tính năng tương tác bằng nút E nếu muốn tự động hoàn toàn
        // Hoặc giữ lại như một cách force start nếu cần thiết.
    }

    void OnGUI()
    {
        if (!_playerNearby || _mainCam == null) return;

        Vector3 worldPos = transform.position + Vector3.up * 1.5f;
        Vector3 screenPos = _mainCam.WorldToScreenPoint(worldPos);
        if (screenPos.z < 0) return;

        float guiY = Screen.height - screenPos.y;

        if (_hintBgTex == null)
        {
            _hintBgTex = new Texture2D(1, 1);
            _hintBgTex.SetPixel(0, 0, new Color(0.02f, 0.02f, 0.03f, 0.85f));
            _hintBgTex.Apply();
        }

        float hintW = 350f;
        float hintH = 32f;
        float hintX = screenPos.x - hintW * 0.5f;
        float hintY = guiY - hintH - 10f;

        GUI.DrawTexture(new Rect(hintX, hintY, hintW, hintH), _hintBgTex);
        DrawTechCorners(hintX, hintY, hintW, hintH, new Color(COL_CYAN.r, COL_CYAN.g, COL_CYAN.b, 0.8f), 8f, 2f);

        GUIStyle hintStyle = new GUIStyle();
        hintStyle.fontSize = 14;
        hintStyle.fontStyle = FontStyle.Bold;
        hintStyle.normal.textColor = COL_CYAN;
        hintStyle.alignment = TextAnchor.MiddleCenter;

        string displayText;
        if (_playersInZone < _totalPlayers)
        {
            displayText = $"Waiting for all players ({_playersInZone}/{_totalPlayers})";
            hintStyle.normal.textColor = Color.yellow;
        }
        else
        {
            displayText = $"Teleporting in {Mathf.CeilToInt(_countdownTimer)}s...";
            hintStyle.normal.textColor = Color.green;
        }

        GUI.Label(new Rect(hintX, hintY, hintW, hintH), displayText, hintStyle);
    }

    void DrawTechCorners(float x, float y, float w, float h, Color color, float len, float thick)
    {
        GUI.color = color;
        Texture2D tex = Texture2D.whiteTexture;
        GUI.DrawTexture(new Rect(x, y, len, thick), tex);
        GUI.DrawTexture(new Rect(x, y, thick, len), tex);
        GUI.DrawTexture(new Rect(x + w - len, y, len, thick), tex);
        GUI.DrawTexture(new Rect(x + w - thick, y, thick, len), tex);
        GUI.DrawTexture(new Rect(x, y + h - thick, len, thick), tex);
        GUI.DrawTexture(new Rect(x, y + h - len, thick, len), tex);
        GUI.DrawTexture(new Rect(x + w - len, y + h - thick, len, thick), tex);
        GUI.DrawTexture(new Rect(x + w - thick, y + h - len, thick, len), tex);
        GUI.color = Color.white;
    }
}
