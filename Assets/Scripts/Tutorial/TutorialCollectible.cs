using UnityEngine;

/// <summary>
/// Vật phẩm mẫu trong guided gameplay course. Người chơi phải nhìn vào nó
/// và nhấn E, giống cách nhặt item trong Map thật.
/// </summary>
public sealed class TutorialCollectible : MonoBehaviour
{
    public string itemName = "Circuit Board";
    public string itemDescription = "Vật phẩm mẫu";
    public Color accentColor = new Color(0f, 0.9f, 1f);
    public bool IsCollected { get; private set; }

    public void Collect()
    {
        if (IsCollected)
            return;

        IsCollected = true;
        gameObject.SetActive(false);
    }

    public void ResetForTesting()
    {
        IsCollected = false;
        gameObject.SetActive(true);
    }
}
