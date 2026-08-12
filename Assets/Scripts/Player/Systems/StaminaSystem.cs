using UnityEngine;
using Unity.Netcode;
using System;

namespace Mimeto.PlayerSystems
{
    /// <summary>
    /// Hệ thống quản lý Thể lực (Stamina) độc lập.
    /// </summary>
    public class StaminaSystem : NetworkBehaviour
    {
        [Header("Stamina Settings")]
        public float maxStamina = 100f;
        public float staminaDepletionRate = 20f; // Chạy 5s là hết lực
        public float staminaRestoreRate = 15f;
        
        // Stamina thường không cần đồng bộ qua mạng (chỉ Client nội bộ dùng để chạy)
        // Nên dùng biến local để giảm băng thông, trừ khi cần hiển thị stamina cho người khác xem
        public float currentStamina { get; private set; }

        public event Action<float, float> OnStaminaChanged;
        public event Action OnExhausted;

        private void Start()
        {
            currentStamina = maxStamina;
        }

        public void DepleteStamina(float deltaTime)
        {
            if (!IsOwner) return;

            currentStamina -= staminaDepletionRate * deltaTime;
            currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
            
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);

            if (currentStamina <= 0)
            {
                OnExhausted?.Invoke();
            }
        }

        public void RestoreStamina(float deltaTime)
        {
            if (!IsOwner) return;

            if (currentStamina < maxStamina)
            {
                currentStamina += staminaRestoreRate * deltaTime;
                currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
                OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            }
        }

        public bool HasStamina()
        {
            return currentStamina > 0;
        }
    }
}
