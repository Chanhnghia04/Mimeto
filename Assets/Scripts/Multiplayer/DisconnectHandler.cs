using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class DisconnectHandler : MonoBehaviour
{
    private void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnect;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnect;
        }
    }

    private void OnDisconnect(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId || clientId == 0)
        {
            if (SceneManager.GetActiveScene().name != "StartGame")
            {
                Debug.Log("[DisconnectHandler] Local client disconnected. Returning to StartGame.");
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                SceneManager.LoadScene("StartGame", LoadSceneMode.Single);
            }
        }
    }
}