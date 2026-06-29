using UnityEngine;
using Unity.Netcode;

public class HidingSpot : MonoBehaviour, IInteractable
{
    [Header("Settings")]
    public Transform hidePosition;
    public Transform exitPosition;
    public float transitionSpeed = 5f;

    private bool _isOccupied = false;
    private GameObject _occupant;

    public bool IsOccupied => _isOccupied;

    public void Interact(GameObject player)
    {
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller == null) return;

        if (!_isOccupied)
        {
            EnterHide(player, controller);
        }
        else if (_occupant == player)
        {
            ExitHide(player, controller);
        }
    }

    private void EnterHide(GameObject player, PlayerController controller)
    {
        _isOccupied = true;
        _occupant = player;
        controller.SetHiding(true, hidePosition.position, hidePosition.rotation);
        Debug.Log("Player entered hiding spot.");
    }

    private void ExitHide(GameObject player, PlayerController controller)
    {
        _isOccupied = false;
        _occupant = null;
        controller.SetHiding(false, exitPosition.position, exitPosition.rotation);
        Debug.Log("Player exited hiding spot.");
    }
}
