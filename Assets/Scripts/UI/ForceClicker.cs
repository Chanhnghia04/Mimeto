using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ForceClicker : MonoBehaviour, IPointerClickHandler
{
    public GameObject targetPanel;
    public GameObject currentPanel;

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("FORCE CLICKER ACTIVATED!");
        if (currentPanel) currentPanel.SetActive(false);
        if (targetPanel) targetPanel.SetActive(true);
        
        var mc = FindObjectOfType<MultiplayerCenter>();
        if (mc != null)
        {
            var m = mc.GetType().GetMethod("OnRefreshLobbies", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (m != null) m.Invoke(mc, null);
        }
    }
}
