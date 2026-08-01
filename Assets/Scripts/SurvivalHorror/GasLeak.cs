using UnityEngine;

public class GasLeak : MonoBehaviour
{
    [Header("Gas Settings")]
    public float oxygenDepletionRate = 5f;
    public float speedPenaltyRatio = 0.3f;

    private float originalSpeed = 0f;
    private bool playerInGas = false;
    private PlayerStatusEffect playerStatus;

    private void OnTriggerEnter(Collider other)
    {
        playerStatus = other.GetComponent<PlayerStatusEffect>();
        if (playerStatus == null) playerStatus = other.GetComponentInParent<PlayerStatusEffect>();

        if (playerStatus != null && !playerStatus.hasGasMask)
        {
            playerInGas = true;
            originalSpeed = playerStatus.walkSpeed;
            playerStatus.walkSpeed = originalSpeed * speedPenaltyRatio;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (playerInGas && playerStatus != null && !playerStatus.hasGasMask)
        {
            playerStatus.currentOxygen -= oxygenDepletionRate * Time.deltaTime;
            playerStatus.currentOxygen = Mathf.Clamp(playerStatus.currentOxygen, 0, 100);
        }
        else if (playerStatus != null && playerStatus.hasGasMask && playerInGas)
        {
            ResetPlayerSpeed();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerStatusEffect exitedPlayer = other.GetComponent<PlayerStatusEffect>();
        if (exitedPlayer == null) exitedPlayer = other.GetComponentInParent<PlayerStatusEffect>();

        if (exitedPlayer != null && playerInGas)
        {
            ResetPlayerSpeed();
            playerStatus = null;
        }
    }

    private void ResetPlayerSpeed()
    {
        if (playerStatus != null)
        {
            playerStatus.walkSpeed = originalSpeed;
            playerInGas = false;
        }
    }
}
