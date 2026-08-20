using UnityEngine;

/// <summary>
/// Vùng an toàn mẫu để người chơi thực hành đi tới nơi hồi Oxygen.
/// </summary>
public sealed class TutorialSafeZone : MonoBehaviour
{
    public float radius = 2.2f;
    public Color accentColor = new Color(0.2f, 1f, 0.55f);

    public bool Contains(Vector3 position)
    {
        Vector3 flatDelta = position - transform.position;
        flatDelta.y = 0f;
        return flatDelta.sqrMagnitude <= radius * radius;
    }
}
