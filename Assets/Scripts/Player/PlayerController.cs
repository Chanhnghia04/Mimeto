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

    [Header("Combat")]
    public float punchRange = 2f;
    public float punchDamage = 20f;
    public float punchCooldown = 0.5f;
    public float comboResetTime = 1.5f;
    public float punchHitDelay = 0.3f; // Thời gian delay để chờ tay đấm trúng mục tiêu
    public LayerMask hitLayers;

    [Header("UV Flashlight")]
    public Light uvLight;
    
    [Header("Realistic Effects")]
    public float bobSpeed = 10f;
    public float bobAmount = 0.05f;
    public float tiltAmount = 2f;
    public float tiltSpeed = 5f;
    public float sprintFOVMultiplier = 1.2f;
    public float fovTransitionSpeed = 5f;
    public float landDipAmount = 0.1f;
    public float landDipSpeed = 10f;

    private float defaultPosY = 0;
    private float timer = 0;
    private float currentTilt = 0f;
    private float defaultFOV = 60f;
    private float targetFOV = 60f;
    private float landDipOffset = 0f;
    private bool wasGrounded = true;

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
    private CraftingUI  _craftingUI;
    private ChestUI     _chestUI;

    public override void OnNetworkSpawn()
    {
        // Tắt CharacterController trên các client không phải chủ sở hữu 
        // để ClientNetworkTransform có thể đồng bộ vị trí tự do mà không bị khóa
        if (!IsOwner)
        {
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
        }

        // Tránh tình trạng 2 player spawn đè lên nhau ở (0,0,0)
        if (IsServer)
        {
            transform.position += new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), 0, UnityEngine.Random.Range(-1.5f, 1.5f));
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
        }
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        networkAnimator = GetComponentInChildren<ClientNetworkAnimator>();
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
        originalCameraLocalPos = playerCamera != null ? playerCamera.localPosition : Vector3.zero;
        defaultPosY = originalCameraLocalPos.y;

        _inventoryUI = GetComponent<InventoryUI>();
        _craftingUI  = GetComponent<CraftingUI>();
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
        }

        UpdateVisualHeldItem();
    }

    private float lastPunchTime = 0f;
    private int punchStep = 0;

    void Update()
    {
        if (controller == null)
        {
            controller = GetComponent<CharacterController>();
            animator = GetComponentInChildren<Animator>();
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) playerCamera = cam.transform;
            _inventoryUI = GetComponent<InventoryUI>();
            _craftingUI  = GetComponent<CraftingUI>();
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

        bool canMove = !IsUIOpen() && !PlayerSurvival.IsGameOverUIOpen();
        UpdateVisualHeldItem();
        
        // Gọi Camera Effects ở cuối Update để tránh độ trễ 1 frame

        if (canMove && Input.GetKeyDown(KeyCode.F))
        {
            PlayerInventory inv = GetComponent<PlayerInventory>();
            if (inv != null && inv.hasUVFlashlight)
            {
                if (uvLight != null)
                {
                    uvLight.enabled = !uvLight.enabled;
                    UpdateVisualHeldItem();
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

            // Removed playerCamera.localRotation here, handled in HandleRealisticCameraEffects
            transform.Rotate(Vector3.up * mouseX);

            if (attackAction != null && attackAction.WasPressedThisFrame())
            {
                // Tự động ép Cooldown xuống thấp (0.2s) để bấm nhanh được
                float actualCooldown = Mathf.Min(punchCooldown, 0.2f); 
                
                if (Time.time - lastPunchTime >= actualCooldown)
                {
                    // Nếu thời gian chờ quá lâu, reset lại từ đòn 1
                    if (Time.time - lastPunchTime > comboResetTime)
                    {
                        punchStep = 0; 
                    }

                    punchStep++;
                    if (punchStep > 2) punchStep = 1; // Lặp lại đòn 1 -> 2 -> 1 -> 2

                    if (animator != null)
                    {
                        // Dùng ClientNetworkAnimator để đồng bộ Trigger qua mạng
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

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            float targetInputX = controller.isGrounded ? moveInput.x * (isCrouching ? 0.5f : (sprintAction != null && sprintAction.IsPressed() ? 1.5f : 1f)) : 0;
            float targetInputY = controller.isGrounded ? moveInput.y * (isCrouching ? 0.5f : (sprintAction != null && sprintAction.IsPressed() ? 1.5f : 1f)) : 0;
            animator.SetFloat("InputX", Mathf.Lerp(animator.GetFloat("InputX"), targetInputX, Time.deltaTime * 8f));
            animator.SetFloat("InputY", Mathf.Lerp(animator.GetFloat("InputY"), targetInputY, Time.deltaTime * 8f));
            animator.SetBool("isSneaking", isCrouching);
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
        if (canMove)
        {
            HandleRealisticCameraEffects();
        }
    }

    // Hàm này hiện tại là public để bạn có thể gọi từ Animation Event
    public void ExecutePunch()
    {
        float actualRange = punchRange;
        float actualDamage = punchDamage;
        PlayerInventory inv = GetComponent<PlayerInventory>();
        
        if (inv != null)
        {
            if (inv.hasAxe)
            {
                actualRange = punchRange * 1.2f;
                actualDamage = punchDamage * 4.0f; // Axe: 80 dmg
            }
            else if (inv.hasMachete)
            {
                actualRange = punchRange * 1.3f;
                actualDamage = punchDamage * 3.5f; // Machete: 70 dmg
            }
            else if (inv.hasCrowbar)
            {
                actualRange = punchRange * 1.5f;
                actualDamage = punchDamage * 2.5f; // Crowbar: 50 dmg
            }
            else if (inv.hasShovel)
            {
                actualRange = punchRange * 1.4f;
                actualDamage = punchDamage * 2.0f; // Shovel: 40 dmg
            }
            else if (inv.hasBat)
            {
                actualRange = punchRange * 1.1f;
                actualDamage = punchDamage * 3.0f; // Bat: 60 dmg
            }
        }

        UpdateVisualHeldItem();
        LayerMask mask = hitLayers.value == 0 ? Physics.DefaultRaycastLayers : hitLayers;
        
        // Sửa lỗi 1: Dùng SphereCast (bắn ra hình cầu to) thay vì Raycast (tia nhỏ) để cực kỳ dễ trúng mục tiêu!
        RaycastHit[] hits = Physics.SphereCastAll(playerCamera.position, 0.4f, playerCamera.forward, actualRange, mask);
        foreach (var h in hits)
        {
            // Bỏ qua chính bản thân người chơi
            if (h.collider.transform.root == transform.root) continue;

            MimicAI mimic = h.collider.GetComponentInParent<MimicAI>();
            if (mimic != null)
            {
                NetworkObject netObj = mimic.GetComponent<NetworkObject>();
                if (netObj != null && IsSpawned)
                {
                    DealDamageToEnemyServerRpc(netObj.NetworkObjectId, actualDamage, true);
                }
                else
                {
                    mimic.TakeDamage(actualDamage); // Fallback offline
                }
                break; // Chỉ gây sát thương cho 1 con mỗi lần đấm
            }
            
            MutantAI mutant = h.collider.GetComponentInParent<MutantAI>();
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
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void DealDamageToEnemyServerRpc(ulong enemyNetworkObjectId, float damage, bool isMimic, ServerRpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(enemyNetworkObjectId, out NetworkObject enemyObj))
        {
            if (isMimic)
            {
                MimicAI mimic = enemyObj.GetComponent<MimicAI>();
                if (mimic != null) mimic.TakeDamage(damage);
            }
            else
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
        }
    }

    public void UpdateVisualHeldItem()
    {
        PlayerInventory inv = GetComponent<PlayerInventory>();
        EquipmentManager em = GetComponent<EquipmentManager>();
        if (inv == null || em == null) return;
        if (inv.hasUVFlashlight && uvLight != null && uvLight.enabled) em.EquipItem("flashlight", EquipmentSlot.RightHand);
        else if (inv.hasAxe) em.EquipItem("axe", EquipmentSlot.RightHand);
        else if (inv.hasMachete) em.EquipItem("machete", EquipmentSlot.RightHand);
        else if (inv.hasBat) em.EquipItem("bat", EquipmentSlot.RightHand);
        else if (inv.hasCrowbar) em.EquipItem("crowbar", EquipmentSlot.RightHand);
        else if (inv.hasShovel) em.EquipItem("shovel", EquipmentSlot.RightHand);
        else em.UnequipSlot(EquipmentSlot.RightHand);
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
            // Find the hiding spot we are in (optional improvement: keep reference)
            HidingSpot[] spots = Object.FindObjectsByType<HidingSpot>();
            foreach (var spot in spots)
            {
                if (spot.IsOccupied)
                {
                    spot.Interact(gameObject);
                    break;
                }
            }
        }
    }

    private void HandleRealisticCameraEffects()
    {
        if (playerCamera == null) return;

        Vector2 moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        bool isMoving = Mathf.Abs(moveInput.x) > 0.1f || Mathf.Abs(moveInput.y) > 0.1f;
        bool isSprinting = sprintAction != null && sprintAction.IsPressed() && isMoving;

        // Tính base Y dựa theo độ lún của character khi crouch
        float bottomY = originalCenter.y - originalHeight / 2f;
        float baseCameraY = bottomY + (originalCameraLocalPos.y - bottomY) * (controller.height / originalHeight);

        // 1. Bobbing
        if (isMoving && controller.isGrounded)
        {
            timer += Time.deltaTime * (isSprinting ? bobSpeed * 1.5f : bobSpeed);
            float bob = Mathf.Sin(timer) * bobAmount;
            playerCamera.localPosition = new Vector3(
                originalCameraLocalPos.x,
                baseCameraY + bob - landDipOffset,
                originalCameraLocalPos.z
            );
        }
        else
        {
            // Idle breathing
            timer += Time.deltaTime * 1.5f;
            playerCamera.localPosition = new Vector3(
                originalCameraLocalPos.x,
                baseCameraY + Mathf.Sin(timer) * (bobAmount * 0.2f) - landDipOffset,
                originalCameraLocalPos.z
            );
        }

        // 2. Camera Tilt (Strafe)
        float targetTilt = -moveInput.x * tiltAmount;
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * tiltSpeed);
        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, currentTilt);

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

    private bool IsUIOpen()
    {
        // 1. Kiểm tra nhanh (fast path) bằng cờ bool hoặc tham chiếu đã có sẵn
        if (isShopMode) return true;
        
        if (_inventoryUI == null) _inventoryUI = GetComponent<InventoryUI>();
        if (_inventoryUI != null && _inventoryUI.inventoryPanel   != null && _inventoryUI.inventoryPanel.activeSelf)   return true;
        
        if (_craftingUI == null) _craftingUI = GetComponent<CraftingUI>();
        if (_craftingUI  != null && _craftingUI.craftingPanel     != null && _craftingUI.craftingPanel.activeSelf)     return true;
        
        if (_chestUI == null) _chestUI = Object.FindAnyObjectByType<ChestUI>();
        if (_chestUI     != null && _chestUI.chestPanel           != null && _chestUI.chestPanel.activeSelf)           return true;

        SettingsUI settingsUI = Object.FindAnyObjectByType<SettingsUI>();
        if (settingsUI != null && settingsUI.settingsPanel != null && settingsUI.settingsPanel.activeSelf) return true;

        if (OpenMinigameCount > 0) return true;
        
        // 2. Kiểm tra chậm (slow path): Quét toàn Scene 4 lần/giây thay vì 60 lần/giây để chống giật lag (Optimize)
        _uiCheckTimer -= Time.deltaTime;
        if (_uiCheckTimer <= 0f)
        {
            _uiCheckTimer = 0.25f; // Chờ 0.25s mới quét lại
            _cachedUiOpen = false;

            EscapeCipher cipher = Object.FindAnyObjectByType<EscapeCipher>();
            if (cipher != null && cipher.IsKeypadOpen) _cachedUiOpen = true;
            
            if (!_cachedUiOpen)
            {
                ExtractionSystem extraction = Object.FindAnyObjectByType<ExtractionSystem>();
                if (extraction != null && extraction.isAssembling) _cachedUiOpen = true;
            }
            
            if (!_cachedUiOpen)
            {
                EscapeBeacon beacon = Object.FindAnyObjectByType<EscapeBeacon>();
                if (beacon != null && beacon.isUIOpen) _cachedUiOpen = true;
            }
            
            if (!_cachedUiOpen)
            {
                EscapeReactor reactor = Object.FindAnyObjectByType<EscapeReactor>();
                if (reactor != null && reactor.isUIOpen) _cachedUiOpen = true;
            }

            if (!_cachedUiOpen)
            {
                BlackjackStation blackjack = Object.FindAnyObjectByType<BlackjackStation>();
                if (blackjack != null && blackjack.isOpen) _cachedUiOpen = true;
            }
            
            if (!_cachedUiOpen)
            {
                SlotMachineStation slot = Object.FindAnyObjectByType<SlotMachineStation>();
                if (slot != null && slot.isOpen) _cachedUiOpen = true;
            }
            
            if (!_cachedUiOpen)
            {
                DiceBetStation dice = Object.FindAnyObjectByType<DiceBetStation>();
                if (dice != null && dice.isOpen) _cachedUiOpen = true;
            }
            
            if (!_cachedUiOpen)
            {
                InfoBoard board = Object.FindAnyObjectByType<InfoBoard>();
                if (board != null && board.isOpen) _cachedUiOpen = true;
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
