using UnityEngine;
using Unity.Netcode;
using System;

namespace Mimeto.PlayerSystems
{
    /// <summary>
    /// Hệ thống quản lý Máu độc lập. 
    /// Dùng Event (Action) để báo cho các class khác (UI, Game Manager) khi máu thay đổi hoặc chết.
    /// </summary>
    public class HealthSystem : NetworkBehaviour
    {
        [Header("Health Settings")]
        public float maxHealth = 100f;
        public NetworkVariable<float> currentHealth = new NetworkVariable<float>(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Các event để UI và các class khác subscribe vào, thay vì phải tìm reference trực tiếp
        public event Action<float, float> OnHealthChanged;
        public event Action OnDeath;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                currentHealth.Value = maxHealth;
            }
            
            // Lắng nghe sự thay đổi của biến mạng và gọi Event
            currentHealth.OnValueChanged += (oldValue, newValue) => 
            {
                OnHealthChanged?.Invoke(newValue, maxHealth);
                if (newValue <= 0 && oldValue > 0)
                {
                    OnDeath?.Invoke();
                }
            };
        }

        // Chỉ Server mới được quyền trừ máu để chống hack
        public void TakeDamage(float amount)
        {
            if (!IsServer) return;
            
            currentHealth.Value = Mathf.Clamp(currentHealth.Value - amount, 0, maxHealth);
        }

        public void Heal(float amount)
        {
            if (!IsServer) return;
            currentHealth.Value = Mathf.Clamp(currentHealth.Value + amount, 0, maxHealth);
        }
    }
}
