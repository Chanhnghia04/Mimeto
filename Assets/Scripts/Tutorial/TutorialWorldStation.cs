using UnityEngine;

/// <summary>
/// Một trạm kiến thức trong scene Tutorial. Trạm chỉ mở UI local và không
/// can thiệp vào NetworkManager hay việc chuyển scene của đội.
/// </summary>
public sealed class TutorialWorldStation : MonoBehaviour
{
    [Min(0)] public int pageIndex;
    public string stationTitle = "Tutorial Station";
    public string interactHint = "Nhấn E để xem hướng dẫn";
    public Color accentColor = new Color(0f, 0.9f, 1f);

    public void Configure(int page, string title, Color color)
    {
        pageIndex = page;
        stationTitle = title;
        accentColor = color;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = accentColor;
        Gizmos.DrawWireSphere(transform.position + Vector3.up, 1.25f);
    }
}
