using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class WaitingRoomPlayerSpawner : MonoBehaviour
{
    public Transform spawnPoint;

    private IEnumerator Start()
    {
        // Chờ NetworkManager và Local PlayerObject được spawn
        float timeout = 10f;
        float elapsed = 0f;

        while (NetworkManager.Singleton == null || 
               NetworkManager.Singleton.LocalClient == null || 
               NetworkManager.Singleton.LocalClient.PlayerObject == null)
        {
            if (elapsed > timeout)
            {
                Debug.LogWarning("WaitingRoomPlayerSpawner: Timeout waiting for Local PlayerObject!");
                yield break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        GameObject player = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;
        
        Vector3 targetPos = spawnPoint != null ? spawnPoint.position : new Vector3(0, 2f, 0);
        Quaternion targetRot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        player.transform.position = targetPos;
        player.transform.rotation = targetRot;

        // Reset velocity if it has Rigidbody (in case it fell in StartGame)
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        var cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
            player.transform.position = targetPos;
            cc.enabled = true;
        }

        Debug.Log("Teleported Player to Waiting Room Spawn Point!");
    }
}
