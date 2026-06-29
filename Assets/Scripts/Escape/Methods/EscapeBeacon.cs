using UnityEngine;
using System.Collections;

/// <summary>
/// Phương thức Beacon: Bấm [E] để xây beacon (tốn scrap) → đếm ngược 3 phút sống sót.
/// Trong thời gian đếm ngược, cần tránh Mimic cho đến khi "đội cứu hộ" đến.
///
/// SETUP:
///   1. Tạo GameObject "EscapeBeacon" trong Scene, đặt INACTIVE.
///   2. Dùng Cylinder primitive (height ~2m) cho hình dáng antenna.
///   3. Gắn script này vào. Tuỳ chọn: kéo Light, AudioSource vào.
///   4. EscapeManager sẽ tự bật nếu màn này chọn Beacon.
/// </summary>
public class EscapeBeacon : MonoBehaviour, IInteractable
{
    [Header("Chi phí xây dựng")]
    public int requiredCircuits  = 2;
    public int requiredBatteries = 1;

    [Header("Đếm ngược")]
    [Tooltip("Thời gian sống sót sau khi bật beacon (giây)")]
    public float countdownSeconds = 180f;

    [Header("Đèn Beacon")]
    public Light beaconLight;
    public Color activeColor = new Color(0.1f, 0.75f, 1f);

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip   buildClip;
    public AudioClip   rescueClip;

    // ── State ─────────────────────────────────────────────────────────────────
    private bool  _isBuilt    = false;
    private bool  _isDone     = false;
    private float _remaining;

    // OnGUI message
    private string _msg      = "";
    private Color  _msgColor = Color.white;
    private float  _msgTimer = 0f;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        _remaining = countdownSeconds;

        // Tìm đèn trong con trước khi tạo mới
        if (beaconLight == null)
        {
            beaconLight = GetComponentInChildren<Light>();
        }

        if (beaconLight == null)
        {
            GameObject lg = new GameObject("BeaconLight");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = Vector3.up * 1.2f;
            beaconLight = lg.AddComponent<Light>();
            beaconLight.type      = LightType.Point;
            beaconLight.color     = Color.grey;
            beaconLight.intensity = 0.6f;
            beaconLight.range     = 6f;
        }

        if (audioSource == null) audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
    }

    void Update()
    {
        if (_msgTimer > 0) _msgTimer -= Time.deltaTime;
        if (!_isBuilt || _isDone) return;

        // Nhấp nháy đèn xanh
        float pulse = Mathf.Sin(Time.time * (2f + _remaining < 30f ? 6f : 2f)) * 0.5f + 1f;
        if (beaconLight != null)
        {
            beaconLight.color     = activeColor;
            beaconLight.intensity = pulse * 2.5f;
        }

        // Đếm ngược
        _remaining -= Time.deltaTime;
        float progress = 1f - (_remaining / countdownSeconds);
        EscapeManager.Instance?.ReportProgress(
            $"Sống sót thêm: {FormatTime(Mathf.Max(0, _remaining))}  ←  Đội cứu hộ đang đến",
            progress);

        if (_remaining <= 0f) StartCoroutine(RescueArrived());
    }

    // ── IInteractable ─────────────────────────────────────────────────────────

    public void Interact(GameObject interactor)
    {
        if (_isBuilt)
        {
            ShowMsg("Beacon đang phát tín hiệu...", new Color(0.1f, 0.8f, 1f));
            return;
        }

        PlayerInventory inv = interactor.GetComponentInParent<PlayerInventory>()
                           ?? interactor.GetComponentInChildren<PlayerInventory>();
        if (inv == null) return;

        if (!inv.HasResources(requiredCircuits, 0, 0, 0, 0, requiredBatteries))
        {
            ShowMsg($"Cần {requiredCircuits} Circuit + {requiredBatteries} Battery!", Color.red);
            return;
        }

        inv.ConsumeResources(requiredCircuits, 0, 0, 0, 0, requiredBatteries);
        Build();
    }

    // ── Build + Countdown ─────────────────────────────────────────────────────

    void Build()
    {
        _isBuilt = true;
        if (audioSource != null && buildClip != null) audioSource.PlayOneShot(buildClip);
        ShowMsg("BEACON KÍCH HOẠT! Sống sót trong 3 phút!", new Color(0.1f, 0.85f, 1f));
        Debug.Log("<color=cyan>[EscapeBeacon] Beacon kích hoạt! Đếm ngược bắt đầu!</color>");
    }

    IEnumerator RescueArrived()
    {
        _isDone = true;

        if (beaconLight != null) beaconLight.color = Color.green;
        if (audioSource != null && rescueClip != null) audioSource.PlayOneShot(rescueClip);

        ShowMsg("ĐỘI CỨU HỘ ĐÃ ĐẾN! Đến cửa thoát ngay!", Color.green);
        Debug.Log("<color=lime>[EscapeBeacon] Đội cứu hộ đến! Escape unlocked!</color>");

        EscapeManager.Instance?.UnlockEscape();
        yield break;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static string FormatTime(float s)
        => $"{(int)(s / 60):00}:{(int)(s % 60):00}";

    void ShowMsg(string msg, Color color) { _msg = msg; _msgColor = color; _msgTimer = 4f; }

    void OnGUI()
    {
        if (_msgTimer <= 0) return;
        GUIStyle s = new GUIStyle { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        s.normal.textColor = Color.black;
        GUI.Label(new Rect(2f, Screen.height * 0.65f + 2, Screen.width, 50), _msg, s);
        s.normal.textColor = _msgColor;
        GUI.Label(new Rect(0f, Screen.height * 0.65f,     Screen.width, 50), _msg, s);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = _isBuilt ? new Color(0.1f, 0.8f, 1f, 0.6f) : new Color(0.5f, 0.5f, 0.5f, 0.4f);
        Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(0.7f, 2f, 0.7f));
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.5f,
            _isBuilt ? $"[BEACON ON]  {FormatTime(_remaining)}" : $"[BEACON OFF]  {requiredCircuits}x Circuit + {requiredBatteries}x Battery");
    }
#endif
}
