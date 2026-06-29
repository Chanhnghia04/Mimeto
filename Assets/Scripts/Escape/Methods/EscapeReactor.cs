using UnityEngine;
using System.Collections;

/// <summary>
/// Phương thức Reactor: Tắt lò phản ứng bằng scrap.
/// Sau khi tắt: Oxygen không cạn nữa → Cửa thoát mở.
///
/// SETUP:
///   1. Tạo GameObject "EscapeReactor" trong Scene (dùng Cylinder/Cube lớn).
///   2. Thêm Collider để Raycast bắt được.
///   3. Gắn script này vào. Tuỳ chọn: kéo Light, AudioSource vào.
///   4. Đặt ở vị trí trung tâm bản đồ (nơi nguy hiểm nhất).
///   5. EscapeManager sẽ tự bật nếu màn này chọn Reactor.
/// </summary>
public class EscapeReactor : MonoBehaviour, IInteractable
{
    [Header("Chi phí tắt lò")]
    public int requiredChemicals = 3;
    public int requiredCircuits  = 2;

    [Header("Đèn Lò")]
    public Light reactorLight;
    public Color dangerColor   = new Color(1f,   0.12f, 0.05f);  // đỏ nguy hiểm
    public Color shutdownColor = new Color(0.1f, 0.65f, 0.2f);   // xanh lá an toàn

    [Header("Hiệu ứng tắt lò")]
    [Tooltip("Thời gian animation tắt lò (giây)")]
    public float shutdownDuration = 3f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip   shutdownClip;

    // ── State ─────────────────────────────────────────────────────────────────
    private bool  _isShutdown = false;

    // OnGUI message
    private string _msg = ""; private Color _msgColor; private float _msgTimer;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        // Tìm đèn trong con trước khi tạo mới
        if (reactorLight == null)
        {
            reactorLight = GetComponentInChildren<Light>();
        }

        if (reactorLight == null)
        {
            GameObject lg = new GameObject("ReactorLight");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = Vector3.up * 1.2f;
            reactorLight = lg.AddComponent<Light>();
            reactorLight.type      = LightType.Point;
            reactorLight.range     = 10f;
            reactorLight.intensity = 3f;
        }
        reactorLight.color = dangerColor;

        if (audioSource == null) audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        EscapeManager.Instance?.ReportProgress(
            $"Tắt lò phản ứng (cần {requiredChemicals} Chemical + {requiredCircuits} Circuit)", 0f);
    }

    void Update()
    {
        if (_msgTimer > 0) _msgTimer -= Time.deltaTime;
        if (_isShutdown) return;

        // Nhấp nháy đèn đỏ nguy hiểm
        float pulse = Mathf.Sin(Time.time * 2.8f) * 0.5f + 1f;
        if (reactorLight != null) reactorLight.intensity = pulse * 3.2f;
    }

    // ── IInteractable ─────────────────────────────────────────────────────────

    public void Interact(GameObject interactor)
    {
        if (_isShutdown)
        {
            ShowMsg("Lò đã được tắt.", Color.green);
            return;
        }

        PlayerInventory inv = interactor.GetComponentInParent<PlayerInventory>()
                           ?? interactor.GetComponentInChildren<PlayerInventory>();
        if (inv == null) return;

        if (!inv.HasResources(requiredCircuits, 0, requiredChemicals))
        {
            ShowMsg($"Cần {requiredCircuits} Circuit + {requiredChemicals} Chemical!", Color.red);
            return;
        }

        // Tiêu thụ nguyên liệu
        inv.ConsumeResources(requiredCircuits, 0, requiredChemicals);
        StartCoroutine(ShutdownSequence());
    }

    // ── Shutdown Animation ────────────────────────────────────────────────────

    IEnumerator ShutdownSequence()
    {
        _isShutdown = true;
        ShowMsg("Đang tắt lò phản ứng...", Color.yellow);
        EscapeManager.Instance?.ReportProgress("Đang tắt lò phản ứng...", 0.5f);

        if (audioSource != null && shutdownClip != null)
            audioSource.PlayOneShot(shutdownClip);

        // Fade đèn đỏ → xanh lá
        float elapsed = 0f;
        Color startCol = dangerColor;
        while (elapsed < shutdownDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shutdownDuration;
            if (reactorLight != null)
            {
                reactorLight.color     = Color.Lerp(startCol, shutdownColor, t);
                reactorLight.intensity = Mathf.Lerp(3.2f, 0.8f, t);
            }
            EscapeManager.Instance?.ReportProgress(
                $"Đang tắt lò... {(int)(t * 100)}%", Mathf.Lerp(0.5f, 0.95f, t));
            yield return null;
        }

        // Dừng Oxygen cạn (reuse safe zone flag)
        PlayerSurvival survival = Object.FindAnyObjectByType<PlayerSurvival>();
        if (survival != null)
        {
            survival.inSafeZone = true;
            Debug.Log("[EscapeReactor] Lò tắt → Oxygen không còn cạn nữa!");
        }

        if (reactorLight != null)
        {
            reactorLight.color     = shutdownColor;
            reactorLight.intensity = 0.8f;
        }

        ShowMsg("LÒ ĐÃ TẮT! Không khí sạch! Đến cửa thoát!", Color.green);
        Debug.Log("<color=lime>[EscapeReactor] Tắt lò hoàn tất! Escape unlocked!</color>");

        EscapeManager.Instance?.UnlockEscape();
    }

    // ── OnGUI message ─────────────────────────────────────────────────────────

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
        Gizmos.color = _isShutdown ? Color.green : new Color(1f, 0.1f, 0.05f, 0.6f);
        Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(1.6f, 2.2f, 1.6f));
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.8f,
            _isShutdown
                ? "[Lò: ĐÃ TẮT]"
                : $"[Lò Phản Ứng]\n{requiredChemicals}x Chemical + {requiredCircuits}x Circuit");
    }
#endif
}
