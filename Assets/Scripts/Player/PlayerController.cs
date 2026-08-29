using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float crouchSpeed = 2.5f;

    [Header("Look")]
    public float mouseSensitivity = 2f;
    private Transform playerCamera;

    [Header("Jumping & Gravity")]
    public float jumpHeight = 1.5f;
    public float gravity = -15f;
    public float fallMultiplier = 1.5f;

    [Header("Crouching")]
    public float crouchHeight = 1f;
    public float crouchTransitionSpeed = 10f;

    [Header("UI Interaction")]
    public float interactRange = 3f;
    public LayerMask interactableLayer;

    // Biến đồng bộ góc nhìn ngước lên/xuống (pitch) qua mạng
    public NetworkVariable<float> netXRotation = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    // Đồng bộ vũ khí đang cầm trên tay
    public NetworkVariable<int> netEquippedWeapon = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    [Header("Combat")]
    public float punchRange = 2f;
    public float punchDamage = 20f;
    public float punchCooldown = 0.5f;
    public float comboResetTime = 1.5f;
    public float punchHitDelay = 0.3f; // Thời gian delay để chờ tay đấm trúng mục tiêu
    public LayerMask hitLayers;

    [Header("UV Flashlight")]
    public Light uvLight;
    public NetworkVariable<bool> netFlashlightEnabled = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    // Đồng bộ trạng thái di chuyển cho AI Server đọc
    public NetworkVariable<bool> netIsMoving = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<bool> netIsSprinting = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<bool> netIsCrouching = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    [Header("Audio")]
    public AudioSource footstepSource;
    public AudioClip pickupClip;
    public float pickupVolume = 1f;

    [Header("Realistic Effects")]
    public float bobSpeed = 10f;
    public float bobAmount = 0.05f;
    public float tiltAmount = 2f;
    public float tiltSpeed = 5f;
    private float _bobTimer = 0f;
    public float sprintFOVMultiplier = 1.2f;
    public float fovTransitionSpeed = 5f;
    public float landDipAmount = 0.1f;
    public float landDipSpeed = 10f;

    private float defaultPosY = 0;
    private float timer = 0;
    private float currentTilt = 0f;
    private float yRotation = 0f; // Absolute world yaw for the head/camera
    private float bodyYaw = 0f;   // Current body yaw
    private float defaultFOV = 75f;
    private float targetFOV = 75f;
    private float landDipOffset = 0f;
    private bool wasGrounded = true;

    // --- Pickup Animation ---
    private float pickupDipRotation = 0f;
    private float pickupDipOffset = 0f;

    [Header("Hiding")]
    public bool isHiding = false;
    private Vector3 _hideTargetPos;
    [HideInInspector] public bool isShopMode = false;
    [HideInInspector] public bool isGhostMode = false;
    private Quaternion _hideTargetRot;

    private CharacterController controller;
    private Animator animator;
    private ClientNetworkAnimator networkAnimator;
    public Vector3 velocity;
    
    public bool isCrouching { get; private set; }
    public bool isSprinting { get; private set; }
    public bool isMoving { get; private set; }
    [HideInInspector] public bool isExhausted = false;

    private float xRotation = 0f;
    
    private float originalHeight;
    private Vector3 originalCenter;
    private Vector3 originalCameraLocalPos;

    private UnityEngine.InputSystem.PlayerInput playerInput;
    private UnityEngine.InputSystem.InputAction moveAction;
    private UnityEngine.InputSystem.InputAction lookAction;
    private UnityEngine.InputSystem.InputAction jumpAction;
    private UnityEngine.InputSystem.InputAction sprintAction;
    private UnityEngine.InputSystem.InputAction crouchAction;
    private UnityEngine.InputSystem.InputAction attackAction;

    private InventoryUI _inventoryUI;
    private ChestUI     _chestUI;

    public override void OnNetworkSpawn()
    {
        netEquippedWeapon.OnValueChanged += OnEquippedWeaponChanged;
        OnEquippedWeaponChanged(0, netEquippedWeapon.Value);
        
        netFlashlightEnabled.OnValueChanged += OnFlashlightChanged;
        OnFlashlightChanged(false, netFlashlightEnabled.Value);

        // Fix Flashlight not looking up/down:
        // Make the light a child of the camera so it pitches with the camera's view
        if (uvLight != null && playerCamera != null && uvLight.transform.parent != playerCamera)
        {
            uvLight.transform.SetParent(playerCamera, true);
            
            // Tùy chọn: reset local position/rotation để đèn chiếu chính xác từ giữa màn hình,
            // hoặc giữ nguyên vị trí cũ (ở ngực/vai) nhưng chỉ reset rotation để chiếu thẳng.
            uvLight.transform.localRotation = Quaternion.identity;
        }

        // Tắt CharacterController trên các client không phải chủ sở hữu 
        // để ClientNetworkTransform có thể đồng bộ vị trí tự do mà không bị khóa
        if (!IsOwner)
        {
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
        }

        // Tránh tình trạng 2 player spawn đè lên nhau ở (0,0,0)
        if (IsOwner)
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Map" || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Waiting")
            {
                transform.position += new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), 0, UnityEngine.Random.Range(-1.5f, 1.5f));
            }
        }

        if (!IsOwner)
        {
            // Tắt toàn bộ Component Camera và AudioListener của các người chơi khác,
            // KHÔNG tắt gameObject để không làm ẩn đầu nhân vật hoặc đèn pin gắn trên đó.
            Camera[] cams = GetComponentsInChildren<Camera>(true);
            foreach (Camera c in cams)
            {
                c.enabled = false;
            }
            
            AudioListener[] listeners = GetComponentsInChildren<AudioListener>(true);
            foreach (AudioListener l in listeners)
            {
                l.enabled = false;
            }
        }
        else
        {
            // Chỉ tắt Main Camera của Scene và bật Camera của Player nếu đang ở trong Map hoặc Waiting
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (currentScene == "Map" || currentScene == "Waiting" || currentScene == "PollutedZone")
            {
                if (Camera.main != null && Camera.main.transform.root != transform)
                {
                    Camera.main.gameObject.SetActive(false);
                }
            }
            
            // Đảm bảo Camera của Player được tag là MainCamera để các UI (như Shop, Sell) hoạt động đúng
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                cam.gameObject.tag = "MainCamera";
            }
            
            // -- FULL BODY FIRST PERSON CAMERA SETUP --
            // 1. Ẩn toàn bộ mesh đầu/mặt khỏi camera local (vẫn đổ bóng, người khác vẫn thấy)
            Transform modelChild = transform.Find("Model");
            Animator setupAnim = GetComponentInChildren<Animator>();
            Transform headBone = (setupAnim != null && setupAnim.isHuman) 
                ? setupAnim.GetBoneTransform(HumanBodyBones.Head) : null;

            if (modelChild != null)
            {
                Renderer[] renderers = modelChild.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in renderers)
                {
                    string rName = r.gameObject.name.ToLower();
                    
                    // Kiểm tra tên mesh có liên quan đến đầu/mặt không
                    bool isHeadPart = rName.Contains("hair") || rName.Contains("eyelash") ||
                                     rName.Contains("head") || rName.Contains("face") ||
                                     rName.Contains("eye")  || rName.Contains("teeth") ||
                                     rName.Contains("tongue")|| rName.Contains("brow") ||
                                     rName.Contains("beard") || rName.Contains("lips") ||
                                     rName.Contains("mouth") || rName.Contains("nose");
                    
                    // Nếu renderer là con của xương Head → cũng ẩn
                    if (!isHeadPart && headBone != null && r.transform.IsChildOf(headBone))
                    {
                        isHeadPart = true;
                    }
                    
                    if (isHeadPart)
                    {
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                    }
                    else
                    {
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    }
                }
            }
            
            // 2. Tùy chỉnh Camera thay vì thu nhỏ đầu
            if (playerCamera != null)
            {
                Camera pCam = playerCamera.GetComponent<Camera>();
                // Chỉnh nearClipPlane (0.18f) để gọt sạch mặt/răng/mắt
                if (pCam != null) pCam.nearClipPlane = 0.25f;
            }
            
            // Cập nhật vị trí cho Vivox 3D Audio
            if (VivoxManager.Instance != null)
            {
                VivoxManager.Instance.SetLocalPlayerTransform(transform, cam != null ? cam.transform : transform);
            }
        }
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        networkAnimator = GetComponentInChildren<ClientNetworkAnimator>();
        if (networkAnimator != null) animator = networkAnimator.Animator;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;

        playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (playerInput != null)
        {
            moveAction = playerInput.actions.FindAction("Move");
            lookAction = playerInput.actions.FindAction("Look");
            jumpAction = playerInput.actions.FindAction("Jump");
            sprintAction = playerInput.actions.FindAction("Sprint");
            crouchAction = playerInput.actions.FindAction("Crouch");
            attackAction = playerInput.actions.FindAction("Attack");
        }

        Camera cam = GetComponentInChildren<Camera>();
        if (cam != null)
        {
            playerCamera = cam.transform;
            defaultFOV = cam.fieldOfView;
            targetFOV = defaultFOV;
        }
            
        Cursor.lockState = CursorLockMode.Locked;
        
        originalHeight = controller.height;
        originalCenter = controller.center;
        
        // Đẩy camera tới trước một chút xíu (8cm) để thoát hẳn ra khỏi vùng mắt/mũi
        Vector3 camBasePos = playerCamera != null ? playerCamera.localPosition : Vector3.zero;
        originalCameraLocalPos = camBasePos + new Vector3(0, 0, 0.08f);

        yRotation = transform.eulerAngles.y;
        bodyYaw = transform.eulerAngles.y;

        Cursor.lockState = CursorLockMode.Locked;
        
        defaultPosY = originalCameraLocalPos.y;

        _inventoryUI = GetComponent<InventoryUI>();
        _chestUI     = Object.FindAnyObjectByType<ChestUI>();

        // Chuyển UI từ Camera sang Overlay giống như Scene StartGame
        UnityEngine.Canvas[] canvasses = FindObjectsByType<UnityEngine.Canvas>();
        foreach (var canvas in canvasses)
        {
            // Chỉ chỉnh những Canvas đang dính vào Camera (VD: túi đồ, máu me)
            if (canvas.renderMode == UnityEngine.RenderMode.ScreenSpaceCamera || canvas.renderMode == UnityEngine.RenderMode.ScreenSpaceOverlay)
            {
                canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
                UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                if (scaler != null)
                {
                    scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920, 1080);
                    scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = 0f; // Match Width giống hệt StartGame
                }
            }

            // --- Thêm HotbarSystem ---
            if (GetComponent<HotbarSystem>() == null)
            {
                gameObject.AddComponent<HotbarSystem>();
            }
        }
    }

    public void EquipWeaponFromHotbar(string itemId)
    {
        if (!IsOwner) return;
        int newWeapon = 0;
        if (itemId == "flashlight") newWeapon = 1;
        else if (itemId == "axe") newWeapon = 2;
        else if (itemId == "machete") newWeapon = 3;
        else if (itemId == "bat") newWeapon = 4;
        else if (itemId == "crowbar") newWeapon = 5;
        else if (itemId == "shovel") newWeapon = 6;
        else if (itemId == "antidote") newWeapon = 7;
        else if (itemId == "basic_gasmask" || itemId == "adv_gasmask")
        {
            PlayerSurvival survival = GetComponent<PlayerSurvival>();
            if (survival != null) survival.ToggleGasMask(itemId);
            return;
        }
        
        if (netEquippedWeapon.Value == newWeapon) newWeapon = 0; // Bấm lần 2 để cất
        netEquippedWeapon.Value = newWeapon;
    }

    public void UnequipIfEquipped(string itemId)
    {
        if (!IsOwner) return;

        if (itemId == "basic_gasmask" || itemId == "adv_gasmask")
        {
            var survival = GetComponent<PlayerSurvival>();
            if (survival != null && survival.netEquippedMask.Value != 0)
            {
                survival.netEquippedMask.Value = 0;
            }
            return;
        }

        int checkWeapon = 0;
        if (itemId == "flashlight") checkWeapon = 1;
        else if (itemId == "axe") checkWeapon = 2;
        else if (itemId == "machete") checkWeapon = 3;
        else if (itemId == "bat") checkWeapon = 4;
        else if (itemId == "crowbar") checkWeapon = 5;
        else if (itemId == "shovel") checkWeapon = 6;
        else if (itemId == "antidote") checkWeapon = 7;
        
        if (netEquippedWeapon.Value == checkWeapon && checkWeapon != 0)
        {
            netEquippedWeapon.Value = 0;
        }
    }


    private float lastPunchTime = 0f;
    private int punchStep = 0;

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        netEquippedWeapon.OnValueChanged -= OnEquippedWeaponChanged;
        netFlashlightEnabled.OnValueChanged -= OnFlashlightChanged;
    }

    private void OnFlashlightChanged(bool previous, bool current)
    {
        if (uvLight != null)
        {
            uvLight.enabled = current;
            if (IsOwner) // Chỉ đổi sương mù cho người chơi hiện tại
            {
                RenderSettings.fogEndDistance = current ? 8.5f : 3.5f;
            }
        }
    }

    private void OnEquippedWeaponChanged(int previous, int current)
    {
        EquipmentManager em = GetComponent<EquipmentManager>();
        if (em != null)
        {
            if (current == 0) em.UnequipSlot(EquipmentSlot.RightHand);
            else if (current == 1) em.EquipItem("flashlight", EquipmentSlot.RightHand);
            else if (current == 2) em.EquipItem("axe", EquipmentSlot.RightHand);
            else if (current == 3) em.EquipItem("machete", EquipmentSlot.RightHand);
            else if (current == 4) em.EquipItem("bat", EquipmentSlot.RightHand);
            else if (current == 5) em.EquipItem("crowbar", EquipmentSlot.RightHand);
            else if (current == 6) em.EquipItem("shovel", EquipmentSlot.RightHand);
            else if (current == 7) em.EquipItem("antidote", EquipmentSlot.RightHand);
        }

        bool isMelee = (current >= 2 && current <= 6);
        
        if (animator == null) 
        {
            if (networkAnimator != null) animator = networkAnimator.Animator;
            else animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            // SetBool chạy trên mọi client vì NetworkVariable thay đổi trên mọi client
            animator.SetBool("hasWeapon", isMelee);
            
            // SetTrigger CHỈ gọi từ Owner, NetworkAnimator sẽ tự động đồng bộ sang các client khác
            if (IsOwner)
            {
                if (current > 0 && previous == 0)
                {
                    if (networkAnimator != null) networkAnimator.SetTrigger("EquipWeapon");
                    else animator.SetTrigger("EquipWeapon");
                }
                else if (current == 0 && previous > 0)
                {
                    if (networkAnimator != null) networkAnimator.SetTrigger("UnequipWeapon");
                    else animator.SetTrigger("UnequipWeapon");
                }
            }
        }
    }


    void Update()
    {
        if (controller == null)
        {
            controller = GetComponent<CharacterController>();
            networkAnimator = GetComponentInChildren<ClientNetworkAnimator>();
            if (networkAnimator != null) animator = networkAnimator.Animator;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) playerCamera = cam.transform;
            _inventoryUI = GetComponent<InventoryUI>();
            _chestUI     = Object.FindAnyObjectByType<ChestUI>();
        }

        // CHỈ CHO PHÉP ĐIỀU KHIỂN NẾU ĐÂY LÀ NHÂN VẬT CỦA MÌNH HOẶC CHƯA KẾT NỐI MẠNG (TEST OFFLINE)
        if (IsSpawned && !IsOwner)
        {
            // Cập nhật góc nhìn ngước lên/xuống cho những người chơi khác thấy
            if (playerCamera != null)
            {
                playerCamera.localRotation = Quaternion.Euler(netXRotation.Value, 0f, 0f);
            }
            return;
        }

        // TẮT TOÀN BỘ HOẠT ĐỘNG CỦA PLAYER NẾU KHÔNG Ở TRONG MAP SCENE VÀ WAITING SCENE
        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeScene != "Map" && activeScene != "Waiting" && activeScene != "PollutedZone")
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (playerCamera != null && playerCamera.gameObject.activeSelf)
            {
                playerCamera.gameObject.SetActive(false); // Tắt camera của Player để không chèn lên UI
            }
            return;
        }
        else
        {
            // Đảm bảo camera được bật lại khi vào Map hoặc Waiting
            if (playerCamera != null && !playerCamera.gameObject.activeSelf)
            {
                playerCamera.gameObject.SetActive(true);
            }
            
            // Tắt Main Camera của Scene (nếu có) để không chiếm quyền hiển thị của Player Camera
            if (Camera.main != null && Camera.main.transform.root != transform)
            {
                Camera.main.gameObject.SetActive(false);
            }
        }

        if (isHiding)
        {
            HandleHidingState();
            return;
        }
        
        if (isGhostMode)
        {
            HandleGhostMovement();
            return;
        }

        // Landing Dip logic
        if (!wasGrounded && controller.isGrounded)
        {
            landDipOffset = landDipAmount;
        }
        wasGrounded = controller.isGrounded;
        landDipOffset = Mathf.Lerp(landDipOffset, 0, Time.deltaTime * landDipSpeed);

        bool uiOpen = IsUIOpen();
        bool canMove = !uiOpen && !PlayerSurvival.IsGameOverUIOpen();
        
        // -----------------------------
        
        // Gọi Camera Effects ở cuối Update để tránh độ trễ 1 frame

        if (canMove && Input.GetKeyDown(KeyCode.F))
        {
            PlayerInventory inv = GetComponent<PlayerInventory>();
            if (inv != null && (inv.hasUVFlashlight || inv.hasFlashlight))
            {
                bool inHotbar = false;
                if (HotbarSystem.Instance != null)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        string item = HotbarSystem.Instance.hotbarItems[i];
                        if (item == "flashlight" || item == "uvflashlight")
                        {
                            inHotbar = true;
                            break;
                        }
                    }
                }

                if (inHotbar)
                {
                    if (IsOwner) netFlashlightEnabled.Value = !netFlashlightEnabled.Value;
                }
                else
                {
                    // Có đèn pin trong người nhưng chưa để vào hotbar
                    Debug.Log("Bạn phải đặt Đèn Pin vào Hotbar (ô 1,2,3) để sử dụng!");
                }
            }
        }


        // Chỉ lock cursor khi không có UI nào đang mở
        if (!IsUIOpen() && !PlayerSurvival.IsGameOverUIOpen())
        {
            string sName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sName == "Map" || sName == "PollutedZone" || sName == "Waiting")
            {
                if (Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible   = false;
                }
            }
        }

        if (canMove && lookAction != null)
        {
            Vector2 lookValue = lookAction.ReadValue<Vector2>();
            float mouseX = lookValue.x * mouseSensitivity * 0.1f;
            float mouseY = lookValue.y * mouseSensitivity * 0.1f;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);
            if (IsSpawned) netXRotation.Value = xRotation;

            yRotation += mouseX;

            // Xoay cơ thể đuổi theo đầu nếu đang di chuyển, hoặc đầu ngoái quá 70 độ
            float angleDiff = Mathf.DeltaAngle(bodyYaw, yRotation);
            Vector2 rawMove = (canMove && moveAction != null) ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            bool isMoving = Mathf.Abs(rawMove.x) > 0.1f || Mathf.Abs(rawMove.y) > 0.1f;
            
            if (isMoving)
            {
                // Khi chạy, cơ thể xoay về hướng nhìn
                bodyYaw = Mathf.LerpAngle(bodyYaw, yRotation, Time.deltaTime * 10f);
            }
            else
            {
                // Khi đứng im, chỉ xoay cơ thể nếu cổ đã ngoái quá góc giới hạn
                if (angleDiff > 75f) bodyYaw = yRotation - 75f;
                else if (angleDiff < -75f) bodyYaw = yRotation + 75f;
            }

            transform.rotation = Quaternion.Euler(0, bodyYaw, 0);

            if (attackAction != null && attackAction.WasPressedThisFrame())
            {
                // Cooldown phải lớn hơn delay đấm trúng để không bị hủy Invoke
                float actualCooldown = Mathf.Max(punchCooldown, punchHitDelay + 0.05f); 
                
                if (Time.time - lastPunchTime >= actualCooldown)
                {
                    // === Kiểm tra có vũ khí cận chiến đang cầm ===
                    int eq = netEquippedWeapon.Value;
                    bool armed = (eq >= 2 && eq <= 6); // 2:Axe, 3:Machete, 4:Bat, 5:Crowbar, 6:Shovel

                    if (armed)
                    {
                        // Có vũ khí → dùng animation chém ngang (Standing Melee Attack)
                        if (animator != null)
                        {
                            if (networkAnimator != null) networkAnimator.SetTrigger("WeaponAttack");
                            else animator.SetTrigger("WeaponAttack");
                        }
                    }
                    else
                    {
                        // Tay không → combo Punch1/Punch2
                        if (Time.time - lastPunchTime > comboResetTime)
                        {
                            punchStep = 0; 
                        }

                        punchStep++;
                        if (punchStep > 2) punchStep = 1;

                        if (animator != null)
                        {
                            if (networkAnimator != null)
                            {
                                if (punchStep == 1) networkAnimator.SetTrigger("Punch1");
                                else if (punchStep == 2) networkAnimator.SetTrigger("Punch2");
                            }
                            else
                            {
                                if (punchStep == 1) animator.SetTrigger("Punch1");
                                else if (punchStep == 2) animator.SetTrigger("Punch2");
                            }
                        }
                    }

                    // Hủy các lệnh đấm cũ (nếu có bấm quá nhanh) và hẹn giờ đấm mới
                    CancelInvoke("ExecutePunch");
                    Invoke("ExecutePunch", punchHitDelay);
                    
                    lastPunchTime = Time.time;
                }
            }
        }

        isCrouching = canMove && (crouchAction != null && crouchAction.IsPressed());
        float targetHeight = isCrouching ? crouchHeight : originalHeight;
        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
        controller.center = new Vector3(originalCenter.x, originalCenter.y - (originalHeight - controller.height) / 2f, originalCenter.z);
        

        PlayerSurvival survival = GetComponent<PlayerSurvival>();
        bool hasStamina = survival != null && survival.currentStamina > 0 && !isExhausted;
        isSprinting = canMove && sprintAction != null && sprintAction.IsPressed() && hasStamina;
        float currentSpeed = isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed);
        Vector2 moveInput = (canMove && moveAction != null) ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        if (move.magnitude > 1f) move.Normalize();
        isMoving = move.magnitude > 0.1f;
        
        // Sync movement states to server for AI
        if (IsOwner)
        {
            netIsMoving.Value = isMoving;
            netIsSprinting.Value = isSprinting;
            netIsCrouching.Value = isCrouching;
        }

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            float targetInputX = controller.isGrounded ? moveInput.x * (isCrouching ? 0.5f : (sprintAction != null && sprintAction.IsPressed() ? 1.5f : 1f)) : 0;
            float targetInputY = controller.isGrounded ? moveInput.y * (isCrouching ? 0.5f : (sprintAction != null && sprintAction.IsPressed() ? 1.5f : 1f)) : 0;
            animator.SetFloat("InputX", Mathf.Lerp(animator.GetFloat("InputX"), targetInputX, Time.deltaTime * 8f));
            animator.SetFloat("InputY", Mathf.Lerp(animator.GetFloat("InputY"), targetInputY, Time.deltaTime * 8f));
            animator.SetBool("isSneaking", isCrouching);
            animator.SetBool("isGrounded", controller.isGrounded);
        }

        Vector3 finalMove = move * currentSpeed;
        if (controller.isGrounded && velocity.y < 0) velocity.y = -2f;
        if (canMove && jumpAction != null && jumpAction.WasPressedThisFrame() && controller.isGrounded && !isCrouching)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (networkAnimator != null) networkAnimator.SetTrigger("Jump");
            else if (animator != null) animator.SetTrigger("Jump");
        }
        velocity.y += (velocity.y < 0 ? gravity * fallMultiplier : gravity) * Time.deltaTime;
        finalMove.y = velocity.y;
        controller.Move(finalMove * Time.deltaTime);
        
        // Realistic Camera Effects (gọi sau khi đã Move và tính toán Crouch)
    }

    void LateUpdate()
    {
        if (IsOwner && IsSpawned)
        {
            // Procedural Spine & Neck Bending (Cho phép thân trên vặn theo chuột)
            if (animator != null && animator.isHuman)
            {
                Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                
                float headLocalYaw = Mathf.DeltaAngle(bodyYaw, yRotation);
                
                // Vặn xương sống và cổ theo trục ngang (nhìn trái phải)
                if (spine != null) spine.rotation = Quaternion.AngleAxis(headLocalYaw * 0.4f, transform.up) * spine.rotation;
                if (neck != null) neck.rotation = Quaternion.AngleAxis(headLocalYaw * 0.6f, transform.up) * neck.rotation;

                // Gập xương sống và cổ theo trục dọc (nhìn lên xuống)
                if (spine != null) spine.rotation = Quaternion.AngleAxis(xRotation * 0.4f, transform.right) * spine.rotation;
                if (neck != null) neck.rotation = Quaternion.AngleAxis(xRotation * 0.6f, transform.right) * neck.rotation;
            }

            HandleRealisticCameraEffects();
        }
    }

    public void ExecutePunch()
    {
        if (isGhostMode) return;
        
        float actualRange = punchRange;
        float actualDamage = punchDamage;
        
        int equipped = netEquippedWeapon.Value;
        if (equipped == 2) // Axe
        {
            actualRange = punchRange * 1.2f;
            actualDamage = punchDamage * 4.0f; // 80 dmg
        }
        else if (equipped == 3) // Machete
        {
            actualRange = punchRange * 1.3f;
            actualDamage = punchDamage * 3.5f; // 70 dmg
        }
        else if (equipped == 5) // Crowbar
        {
            actualRange = punchRange * 1.5f;
            actualDamage = punchDamage * 2.5f; // 50 dmg
        }
        else if (equipped == 6) // Shovel
        {
            actualRange = punchRange * 1.4f;
            actualDamage = punchDamage * 2.0f; // 40 dmg
        }
        else if (equipped == 4) // Bat
        {
            actualRange = punchRange * 1.1f;
            actualDamage = punchDamage * 3.0f; // 60 dmg
        }
        LayerMask mask = hitLayers.value == 0 ? Physics.DefaultRaycastLayers : hitLayers;
        
        // Sửa lỗi 1: Dùng SphereCast (bắn ra hình cầu to) thay vì Raycast (tia nhỏ) để cực kỳ dễ trúng mục tiêu!
        RaycastHit[] hits = Physics.SphereCastAll(playerCamera.position, 0.4f, playerCamera.forward, actualRange, mask);
        foreach (var h in hits)
        {
            // Bỏ qua chính bản thân người chơi
            if (h.collider.transform.root == transform.root) continue;
            
            MutantAI mutant = h.collider.GetComponentInParent<MutantAI>();
            ExilerAI exiler = h.collider.GetComponentInParent<ExilerAI>();

            if (mutant != null)
            {
                NetworkObject netObj = mutant.GetComponent<NetworkObject>();
                if (netObj != null && IsSpawned)
                {
                    DealDamageToEnemyServerRpc(netObj.NetworkObjectId, actualDamage, false);
                }
                else
                {
                    mutant.TakeDamage(actualDamage);
                    mutant.ForceTarget(this); // Fallback offline
                }
                break;
            }
            else if (exiler != null)
            {
                NetworkObject netObj = exiler.GetComponent<NetworkObject>();
                if (netObj != null && IsSpawned)
                {
                    DealDamageToEnemyServerRpc(netObj.NetworkObjectId, actualDamage, true);
                }
                else
                {
                    exiler.TakeDamage(actualDamage);
                    exiler.ForceTarget(this); // Fallback offline
                }
                break;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void DealDamageToEnemyServerRpc(ulong enemyNetworkObjectId, float damage, bool isMimic, ServerRpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(enemyNetworkObjectId, out NetworkObject enemyObj))
        {
            if (!isMimic)
            {
                MutantAI mutant = enemyObj.GetComponent<MutantAI>();
                if (mutant != null)
                {
                    mutant.TakeDamage(damage);
                    // Find the player who dealt damage to force target
                    if (NetworkManager.Singleton.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out var client))
                    {
                        PlayerController attacker = client.PlayerObject.GetComponent<PlayerController>();
                        if (attacker != null) mutant.ForceTarget(attacker);
                    }
                }
            }
            else
            {
                ExilerAI exiler = enemyObj.GetComponent<ExilerAI>();
                if (exiler != null)
                {
                    exiler.TakeDamage(damage);
                    if (NetworkManager.Singleton.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out var client))
                    {
                        PlayerController attacker = client.PlayerObject.GetComponent<PlayerController>();
                        if (attacker != null) exiler.ForceTarget(attacker);
                    }
                }
            }
        }
    }

    public void UpdateVisualHeldItem()
    {
        // Auto-equip logic removed. Weapons are now equipped manually via 1, 2, 3 hotkeys.
    }

    public void SetHiding(bool hiding, Vector3 targetPos, Quaternion targetRot)
    {
        isHiding = hiding;
        _hideTargetPos = targetPos;
        _hideTargetRot = targetRot;
        
        if (controller != null) controller.enabled = !hiding;
        
        if (hiding)
        {
            // Reset velocity and animation when hiding
            velocity = Vector3.zero;
            if (animator != null)
            {
                animator.SetFloat("InputX", 0);
                animator.SetFloat("InputY", 0);
            }
        }
    }

    private void HandleHidingState()
    {
        transform.position = Vector3.Lerp(transform.position, _hideTargetPos, Time.deltaTime * 10f);
        transform.rotation = Quaternion.Slerp(transform.rotation, _hideTargetRot, Time.deltaTime * 10f);

        // Exit hiding with Jump or Crouch
        if ((jumpAction != null && jumpAction.WasPressedThisFrame()) || (crouchAction != null && crouchAction.WasPressedThisFrame()))
        {
            HidingSpot[] spots = Object.FindObjectsByType<HidingSpot>();
            foreach (var spot in spots)
            {
                // Fix: Chỉ tương tác thoát ra nếu chính xác TÔI là người đang chiếm dụng tủ này
                if (spot.IsOccupied && spot.Occupant == gameObject)
                {
                    spot.Interact(gameObject);
                    break;
                }
            }
        }
    }

    public void TriggerPickupDip()
    {
        pickupDipRotation = 15f; // Gật xuống 15 độ
        pickupDipOffset = 0.25f; // Lún xuống 0.25 units
    }

    private void HandleRealisticCameraEffects()
    {
        if (playerCamera == null) return;

        Vector2 moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        bool isMoving = Mathf.Abs(moveInput.x) > 0.1f || Mathf.Abs(moveInput.y) > 0.1f;
        bool isSprinting = sprintAction != null && sprintAction.IsPressed() && isMoving;

        // 2. Pickup Dip Recovery
        pickupDipRotation = Mathf.Lerp(pickupDipRotation, 0f, Time.deltaTime * 8f);
        pickupDipOffset = Mathf.Lerp(pickupDipOffset, 0f, Time.deltaTime * 8f);

        if (isMoving && controller != null && controller.isGrounded)
        {
            _bobTimer += Time.deltaTime * (isSprinting ? bobSpeed * 1.5f : bobSpeed);
        }
        else
        {
            _bobTimer = 0f;
        }
        float bobOffset = Mathf.Sin(_bobTimer) * bobAmount;

        // 3. True First Person: Track head position
        bool headTracked = false;
        if (animator != null && animator.isHuman)
        {
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
            {
                // Đẩy camera ra phía trước mặt thêm một chút (từ 0.32 lên 0.40) để vượt hẳn qua chóp mũi
                Vector3 targetPos = head.position + head.up * 0.05f + head.forward * 0.40f;
                // Cộng thêm độ rung lắc (bobOffset) vào trục Y của camera
                targetPos.y += bobOffset;
                
                playerCamera.position = targetPos - transform.up * (landDipOffset + pickupDipOffset);
                headTracked = true;
            }
        }
        
        if (!headTracked)
        {
            playerCamera.localPosition = new Vector3(
                originalCameraLocalPos.x,
                originalCameraLocalPos.y - landDipOffset - pickupDipOffset + bobOffset,
                originalCameraLocalPos.z
            );
        }

        // 4. Camera Tilt (Strafe) + Pickup Dip Rotation + Independent Head Yaw
        float targetTilt = -moveInput.x * tiltAmount;
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * tiltSpeed);
        
        float headLocalYaw = Mathf.DeltaAngle(bodyYaw, yRotation);
        playerCamera.localRotation = Quaternion.Euler(xRotation + pickupDipRotation, headLocalYaw, currentTilt);

        // 3. Sprint FOV
        Camera cam = playerCamera.GetComponent<Camera>();
        if (cam != null)
        {
            targetFOV = isSprinting ? defaultFOV * sprintFOVMultiplier : defaultFOV;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * fovTransitionSpeed);
        }
    }

    
    private float _uiCheckTimer = 0f;
    private bool _cachedUiOpen = false;
    private EscapeCipher _cipher;
    private ExtractionSystem _extraction;
    private EscapeBeacon _beacon;
    private EscapeReactor _reactor;
    private BlackjackStation _blackjack;
    private SlotMachineStation _slot;
    private DiceBetStation _dice;
    private InfoBoard _board;
    private SettingsUI _settingsUI;

    public bool IsUIOpen()
    {
        if (isShopMode) return true;
        
        if (_inventoryUI == null) _inventoryUI = GetComponent<InventoryUI>();
        if (_inventoryUI != null && _inventoryUI.inventoryPanel != null && _inventoryUI.inventoryPanel.activeSelf) return true;
        
        if (_chestUI == null) _chestUI = Object.FindAnyObjectByType<ChestUI>();
        if (_chestUI != null && _chestUI.chestPanel != null && _chestUI.chestPanel.activeSelf) return true;

        if (_settingsUI == null) _settingsUI = Object.FindAnyObjectByType<SettingsUI>();
        if (_settingsUI != null && _settingsUI.settingsPanel != null && _settingsUI.settingsPanel.activeSelf) return true;

        if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsOpen) return true;
        if (OpenMinigameCount > 0) return true;
        
        _uiCheckTimer -= Time.unscaledDeltaTime;
        if (_uiCheckTimer <= 0f)
        {
            _uiCheckTimer = 0.25f;
            _cachedUiOpen = false;

            if (_cipher == null) _cipher = Object.FindAnyObjectByType<EscapeCipher>();
            if (_cipher != null && _cipher.IsKeypadOpen) _cachedUiOpen = true;
            
            if (!_cachedUiOpen) {
                if (_extraction == null) _extraction = Object.FindAnyObjectByType<ExtractionSystem>();
                if (_extraction != null && _extraction.isAssembling) _cachedUiOpen = true;
            }
            if (!_cachedUiOpen) {
                if (_beacon == null) _beacon = Object.FindAnyObjectByType<EscapeBeacon>();
                if (_beacon != null && _beacon.isUIOpen) _cachedUiOpen = true;
            }
            if (!_cachedUiOpen) {
                if (_reactor == null) _reactor = Object.FindAnyObjectByType<EscapeReactor>();
                if (_reactor != null && _reactor.isUIOpen) _cachedUiOpen = true;
            }
            if (!_cachedUiOpen) {
                if (_blackjack == null) _blackjack = Object.FindAnyObjectByType<BlackjackStation>();
                if (_blackjack != null && _blackjack.isOpen) _cachedUiOpen = true;
            }
            if (!_cachedUiOpen) {
                if (_slot == null) _slot = Object.FindAnyObjectByType<SlotMachineStation>();
                if (_slot != null && _slot.isOpen) _cachedUiOpen = true;
            }
            if (!_cachedUiOpen) {
                if (_dice == null) _dice = Object.FindAnyObjectByType<DiceBetStation>();
                if (_dice != null && _dice.isOpen) _cachedUiOpen = true;
            }
            if (!_cachedUiOpen) {
                if (_board == null) _board = Object.FindAnyObjectByType<InfoBoard>();
                if (_board != null && _board.isOpen) _cachedUiOpen = true;
            }
        }
        return _cachedUiOpen;
    }

    public static int OpenMinigameCount = 0;

    public void ForceUIRefresh()
    {
        _uiCheckTimer = 0f;
    }

    private Transform spectateTarget = null;
    private int spectateIndex = -1;

    private void HandleGhostMovement()
    {
        // Simple fly movement ignoring gravity and collisions
        bool canMove = !IsUIOpen() && !PlayerSurvival.IsGameOverUIOpen();
        if (!canMove) return;

        // Cho phép chuyển mục tiêu spectate bằng phím chuột trái (hoặc phím đánh)
        if (attackAction != null && attackAction.WasPressedThisFrame())
        {
            CycleSpectateTarget();
        }

        Vector2 moveInput = (moveAction != null) ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        float upDown = 0;
        if (jumpAction != null && jumpAction.IsPressed()) upDown = 1;
        if (crouchAction != null && crouchAction.IsPressed()) upDown = -1;
        
        // Nếu người chơi bấm phím di chuyển, sẽ thoát khỏi chế độ spectate và bay tự do
        if (moveInput.sqrMagnitude > 0.01f || upDown != 0)
        {
            spectateTarget = null;
        }

        if (spectateTarget != null)
        {
            // Đi theo mục tiêu spectate
            PlayerController targetController = spectateTarget.GetComponent<PlayerController>();
            if (targetController != null)
            {
                transform.position = spectateTarget.position + Vector3.up * 1.6f;
                transform.rotation = spectateTarget.rotation;
                xRotation = targetController.netXRotation.Value;
                playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            }
            return;
        }

        float moveSpeed = sprintSpeed * 1.5f;
        Vector3 move = playerCamera.right * moveInput.x + playerCamera.forward * moveInput.y;
        move += Vector3.up * upDown;
        
        transform.position += move * moveSpeed * Time.deltaTime;
        
        // Look around
        if (lookAction != null)
        {
            Vector2 lookValue = lookAction.ReadValue<Vector2>();
            float mouseX = lookValue.x * mouseSensitivity * 0.1f;
            float mouseY = lookValue.y * mouseSensitivity * 0.1f;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            if (IsSpawned) netXRotation.Value = xRotation;

            transform.Rotate(Vector3.up * mouseX);
        }
        
        // Lock cursor
        if (!IsUIOpen() && !PlayerSurvival.IsGameOverUIOpen())
        {
            string sName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sName == "Map" || sName == "PollutedZone" || sName == "Waiting")
            {
                if (Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible   = false;
                }
            }
        }
    }

    private void CycleSpectateTarget()
    {
        PlayerSurvival[] allPlayers = Object.FindObjectsByType<PlayerSurvival>();
        System.Collections.Generic.List<PlayerSurvival> alivePlayers = new System.Collections.Generic.List<PlayerSurvival>();
        
        foreach (var p in allPlayers)
        {
            // Chỉ thêm những người còn sống và không phải bản thân
            if (p != GetComponent<PlayerSurvival>() && p.currentHealth > 0 && !p.isGhost.Value)
            {
                alivePlayers.Add(p);
            }
        }

        if (alivePlayers.Count == 0)
        {
            spectateTarget = null;
            return;
        }

        spectateIndex++;
        if (spectateIndex >= alivePlayers.Count)
        {
            spectateIndex = 0;
        }

        spectateTarget = alivePlayers[spectateIndex].transform;
        
        // Cập nhật góc nhìn ngay lập tức
        PlayerController targetController = spectateTarget.GetComponent<PlayerController>();
        if (targetController != null)
        {
            transform.position = spectateTarget.position + Vector3.up * 1.6f;
            transform.rotation = spectateTarget.rotation;
            xRotation = targetController.netXRotation.Value;
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }
    }
}
