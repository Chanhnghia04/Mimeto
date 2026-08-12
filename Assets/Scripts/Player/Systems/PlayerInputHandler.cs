using UnityEngine;
using UnityEngine.InputSystem;

namespace Mimeto.PlayerSystems
{
    /// <summary>
    /// Hệ thống Input độc lập.
    /// Nhiệm vụ DUY NHẤT: Bắt phím bấm và lưu thành dữ liệu (biến).
    /// Các script khác (như Movement, Combat) sẽ đọc dữ liệu từ đây.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        public Vector2 moveInput { get; private set; }
        public Vector2 lookInput { get; private set; }
        public bool jumpPressed { get; private set; }
        public bool sprintHeld { get; private set; }
        public bool crouchPressed { get; private set; }
        public bool attackPressed { get; private set; }

        private PlayerInput playerInput;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction crouchAction;
        private InputAction attackAction;

        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
            
            // Map actions
            moveAction = playerInput.actions["Move"];
            lookAction = playerInput.actions["Look"];
            jumpAction = playerInput.actions["Jump"];
            sprintAction = playerInput.actions["Sprint"];
            crouchAction = playerInput.actions["Crouch"];
            attackAction = playerInput.actions["Attack"];
        }

        private void Update()
        {
            moveInput = moveAction.ReadValue<Vector2>();
            lookInput = lookAction.ReadValue<Vector2>();
            
            // Xóa trigger phím bấm sau 1 frame bằng cách dùng WasPressedThisFrame
            jumpPressed = jumpAction.WasPressedThisFrame();
            sprintHeld = sprintAction.IsPressed();
            crouchPressed = crouchAction.WasPressedThisFrame();
            attackPressed = attackAction.WasPressedThisFrame();
        }
    }
}
