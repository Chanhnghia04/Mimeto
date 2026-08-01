using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class AutoPlayButtonSound : MonoBehaviour
{
    [Header("Âm thanh khi bấm nút")]
    public AudioClip clickSound;
    
    private AudioSource audioSource;

    void Start()
    {
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(PlaySound);
        }

        // Tự động tạo AudioSource 2D trên nút để đảm bảo luôn nghe thấy
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // Ép thành âm thanh 2D (không bị nhỏ theo khoảng cách)
        audioSource.ignoreListenerPause = true; // Phát ngay cả khi game đang bị Pause

        if (clickSound == null) clickSound = Resources.Load<AudioClip>("SFX/UI/ui_wav/click_sound") ?? Resources.Load<AudioClip>("SFX/Buy_Coin");
    }

    void PlaySound()
    {
        if (clickSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(clickSound);
        }
    }
}
