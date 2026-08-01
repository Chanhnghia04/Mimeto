using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public class MimicAI : MonoBehaviour
{
    public enum MimicState { Stalking, Chasing, HumanForm, Revealed }
    public MimicState currentState = MimicState.Stalking;

    [Header("Health Settings")]
    public float health = 100f;
    public bool isDead = false;

    [Header("Monster Form Settings")]
    public float monsterWalkSpeed = 3f;
    public float monsterSpeed = 8f;
    public float monsterAcceleration = 12f;
    public float stalkingRadius = 20f;
    public float detectionRadius = 15f;
    public float attackRange = 1.5f;
    public float attackDamage = 20f;
    public float attackRate = 1.0f;

    [Header("Human Form Settings")]
    public float humanSpeed = 3f;
    public float humanAcceleration = 5f;
    public float redFlashlightInterval = 2f;
    public Color redLightColor = Color.red;

    [Header("References")]
    public NavMeshAgent agent;
    public GameObject monsterModel;
    public GameObject humanModelContainer;
    public Light flashlight;
    public TMPro.TextMeshPro nametag;
    public Animator animator;
    public AudioSource audioSource;
    public AudioClip attackClip;

    [Header("Creepy Audio")]
    public AudioSource ambientAudioSource;
    public AudioClip creepyApproachClip;
    private bool hasPlayedCreepySound = false;

    [Header("Detection Settings")]
    public float fieldOfView = 110f;
    public LayerMask obstacleMask;
    public float loseTargetRadius = 25f;

    private GameObject targetPlayer;
private Color originalLightColor;
    private bool isRedLightActive = false;
    private GameObject currentHumanModel;
    private float lastScanTime;
    private float scanInterval = 1f;
    private float lastAttackTime;
    private float lastFlashTime;

    private Vector3 lastKnownPosition;
    private float loseSightTimer = 0f;
    private float erraticMoveTimer = 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (flashlight != null) originalLightColor = flashlight.color;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (ambientAudioSource == null) {
            ambientAudioSource = gameObject.AddComponent<AudioSource>();
            ambientAudioSource.spatialBlend = 1f;
            ambientAudioSource.maxDistance = 15f;
            ambientAudioSource.rolloffMode = AudioRolloffMode.Linear;
        }
        
        // Sửa lỗi 2: Ép cứng sát thương bằng 20 (ghi đè Inspector cũ)
        attackDamage = 20f;

        // Ensure monster form at start
        SetState(MimicState.Stalking);
    }

    void Update()
    {
        // Server-only: prevent all clients from running AI logic independently
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer) return;
        if (agent == null || !agent.isOnNavMesh) return;

        // NEW: Check if target player is dead. If so, forget them.
        if (targetPlayer != null)
        {
            PlayerSurvival survival = targetPlayer.GetComponent<PlayerSurvival>();
            if (survival != null && survival.currentHealth <= 0)
            {
                // Sửa lỗi 3: Player đã chết thì quên mục tiêu đi, ngừng tấn công ngay lập tức!
                targetPlayer = null;
                
                // Ngừng âm thanh và reset animation triệt để
                if (audioSource != null) audioSource.Stop();
                if (animator != null) 
                {
                    animator.ResetTrigger("Attack");
                    animator.Rebind();
                }

                // Nếu đang rượt đuổi thì chuyển sang dạng người (ăn cắp nhân dạng) hoặc rình rập
                if (currentState == MimicState.Chasing || currentState == MimicState.Revealed)
                {
                    SetState(MimicState.Stalking);
                }
            }
        }

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetFloat("Speed", agent.velocity.magnitude);
        }

        switch (currentState)
        {
            case MimicState.Stalking:
                HandleStalking();
                break;
            case MimicState.Chasing:
                HandleChasing();
                break;
            case MimicState.HumanForm:
                HandleHumanForm();
                break;
            case MimicState.Revealed:
                HandleRevealed();
                break;
        }
    }

    private float wanderTimer = 0f;
    private float wanderInterval = 5f;
    public float wanderRadius = 20f;

    private bool CanSeePlayer(GameObject player)
    {
        if (player == null) return false;

        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null && pc.isHiding) return false;

        float distance = Vector3.Distance(transform.position, player.transform.position);
        if (distance > detectionRadius) return false;

        Vector3 mimicEye = transform.position + Vector3.up * 1.5f;
        
        // Quét 3 điểm thay vì 1 để thực tế hơn: Đầu, Bụng, và Chân (chống nấp ló mông)
        Vector3[] checkPoints = new Vector3[] {
            player.transform.position + Vector3.up * 1.5f, // Đầu
            player.transform.position + Vector3.up * 0.8f, // Bụng
            player.transform.position + Vector3.up * 0.2f  // Chân
        };

        foreach (Vector3 point in checkPoints)
        {
            Vector3 directionToPlayer = (point - mimicEye).normalized;
            float distanceToPoint = Vector3.Distance(mimicEye, point);
            float angleBetween = Vector3.Angle(transform.forward, directionToPlayer);

            if (angleBetween < fieldOfView / 2f)
            {
                RaycastHit hit;
                if (Physics.Raycast(mimicEye, directionToPlayer, out hit, distanceToPoint, obstacleMask))
                {
                    if (hit.collider.gameObject == player || hit.collider.transform.IsChildOf(player.transform) || hit.collider.CompareTag("Player"))
                    {
                        return true;
                    }
                }
                else
                {
                    // Không có chướng ngại vật nào chắn tầm nhìn
                    return true;
                }
            }
        }
        
        // Always "see" player if extremely close (within 3m)
        if (distance < 3f && pc != null && !pc.isHiding) return true;

        return false;
    }

    private bool CanHearPlayer(GameObject player)
    {
        if (player == null) return false;

        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc == null || pc.isHiding) return false;

        if (!pc.isMoving) return false; // Không đi không phát tiếng
        if (pc.isCrouching) return false; // Ngồi đi cũng không phát tiếng

        float distance = Vector3.Distance(transform.position, player.transform.position);
        float hearingRadius = pc.isSprinting ? 20f : 10f; // Chạy thì 20m, đi bộ thì 10m

        return distance <= hearingRadius;
    }

    void HandleStalking()
    {
        // 1. Quét tìm player
        if (Time.time - lastScanTime > scanInterval)
        {
            targetPlayer = FindLonePlayer();
            lastScanTime = Time.time;
        }
        
        // 2. Nếu thấy player trong tầm phát hiện -> Chuyển sang đuổi (Chasing)
        if (targetPlayer != null && CanSeePlayer(targetPlayer))
        {
            Debug.Log("Mimic detected player! Chasing...");
            lastKnownPosition = targetPlayer.transform.position;
            loseSightTimer = 0f;
            SetState(MimicState.Chasing);
            return;
        }

        // 2.5. Nếu NGHE thấy tiếng bước chân -> Đi tới đó để kiểm tra
        if (targetPlayer != null && CanHearPlayer(targetPlayer))
        {
            Investigate(targetPlayer.transform.position);
        }

        // 3. Nếu chưa thấy player hoặc ở xa -> Đi tuần ngẫu nhiên (Wandering)
        if (isInvestigating)
        {
            // Nếu đã tới nơi tìm hiểu, chuyển lại thành đi dạo
            if (!agent.pathPending && agent.remainingDistance <= 2.0f)
            {
                isInvestigating = false;
                wanderTimer = wanderInterval; // Ép chọn đường đi dạo ngay
            }
        }
        else
        {
            wanderTimer += Time.deltaTime;
            if (wanderTimer >= wanderInterval || (!agent.pathPending && agent.remainingDistance <= 1.0f))
            {
                Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
                randomDirection += transform.position;
                
                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }
                wanderTimer = 0f;
                wanderInterval = Random.Range(3f, 7f); 
            }
        }
    }

    void HandleChasing()
    {
        if (targetPlayer == null || !targetPlayer.activeInHierarchy)
        {
            SetState(MimicState.Stalking);
            return;
        }

        PlayerController pc = targetPlayer.GetComponent<PlayerController>();
        if (pc != null && pc.isHiding)
        {
            // If player hides while being chased, mimic might go to last known position
            // For simplicity here, lose target
            targetPlayer = null;
            SetState(MimicState.Stalking);
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, targetPlayer.transform.position);

        if (distanceToTarget < 15f && !hasPlayedCreepySound && ambientAudioSource != null && creepyApproachClip != null)
        {
            hasPlayedCreepySound = true;
            ambientAudioSource.clip = creepyApproachClip;
            ambientAudioSource.Play();
        }

        if (distanceToTarget > loseTargetRadius)
        {
            targetPlayer = null;
            SetState(MimicState.Stalking);
            return;
        }

        if (CanSeePlayer(targetPlayer))
        {
            lastKnownPosition = targetPlayer.transform.position;
            loseSightTimer = 0f;
        }
        else
        {
            loseSightTimer += Time.deltaTime;
        }

        if (agent.isOnNavMesh) 
        {
            agent.SetDestination(lastKnownPosition);
        }

        // Mất dấu nếu sau 6s không thấy lại, HOẶC đã chạy đến nơi thấy cuối cùng mà vẫn không thấy
        if (loseSightTimer > 6f || (loseSightTimer > 0.5f && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f))
        {
            Debug.Log("Mimic lost track of player behind obstacle!");
            targetPlayer = null;
            SetState(MimicState.Stalking);
            return;
        }

        // Sửa lỗi không cắn: Tính khoảng cách bỏ qua trục Y và bù trừ bán kính của 2 nhân vật
        Vector3 flatPos = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 flatTarget = new Vector3(targetPlayer.transform.position.x, 0, targetPlayer.transform.position.z);
        float dist = Vector3.Distance(flatPos, flatTarget);

        // Nếu đã áp sát (cộng thêm 0.5f để bù cho bán kính Capsule Collider của Player và Mimic)
        // Hoặc NavMeshAgent đã báo hiệu tới nơi
        if (dist <= attackRange + 0.5f || (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f))
        {
            // Hướng mặt về phía Player
            Vector3 dir = (targetPlayer.transform.position - transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero) 
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 5f);
            }

            if (Time.time - lastAttackTime > attackRate)
            {
                AttackPlayer(targetPlayer);
                lastAttackTime = Time.time;
            }
        }
    }

    void HandleHumanForm()
    {
        GameObject group = FindClosestGroup();
        
        erraticMoveTimer -= Time.deltaTime;
        if (group != null && erraticMoveTimer <= 0f)
        {
            // Cập nhật đường đi mỗi 1-2.5 giây để tạo sự lóng ngóng, thỉnh thoảng đi dạt sang bên (mô phỏng bấm A, D)
            erraticMoveTimer = Random.Range(1.0f, 2.5f);
            Vector3 randomOffset = Random.insideUnitSphere * 4f;
            randomOffset.y = 0f;
            
            if (Random.value > 0.85f) 
            {
                // 15% tỷ lệ giả vờ đứng lại nhìn ngó ngơ ngác
                agent.isStopped = true;
            } 
            else 
            {
                agent.isStopped = false;
                agent.SetDestination(group.transform.position + randomOffset);
            }
        }

        // Bug 1 fix: use a timer variable instead of Time.time % interval
        // which fires dozens of coroutines per frame during its match window.
        if (flashlight != null && !isRedLightActive && Time.time - lastFlashTime >= redFlashlightInterval)
        {
            lastFlashTime = Time.time;
            StartCoroutine(FlashRed());
        }
    }

    void HandleRevealed()
    {
        if (targetPlayer != null && targetPlayer.activeInHierarchy)
        {
            PlayerController pc = targetPlayer.GetComponent<PlayerController>();
            float distanceToTarget = Vector3.Distance(transform.position, targetPlayer.transform.position);

            if ((pc != null && pc.isHiding) || distanceToTarget > loseTargetRadius)
            {
                targetPlayer = null;
                SetState(MimicState.Stalking);
                return;
            }

            if (CanSeePlayer(targetPlayer))
            {
                lastKnownPosition = targetPlayer.transform.position;
                loseSightTimer = 0f;
            }
            else
            {
                loseSightTimer += Time.deltaTime;
            }

            if (agent.isOnNavMesh) 
            {
                agent.SetDestination(lastKnownPosition);
            }

            if (loseSightTimer > 6f || (loseSightTimer > 0.5f && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f))
            {
                Debug.Log("Mimic (Revealed) lost track of player behind obstacle!");
                targetPlayer = null;
                SetState(MimicState.Stalking);
                return;
            }
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        health -= amount;
        Debug.Log($"Mimic took {amount} damage. Health: {health}");

        // React to hit
        if (currentState != MimicState.Chasing)
        {
            SetState(MimicState.Chasing);
        }

        if (health <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        isDead = true;
        Debug.Log("<color=red>Mimic Died!</color>");
        
        // Proper NGO network despawn instead of SetActive(false) which causes desync
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkObject netObj = GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }

    void SetState(MimicState newState)
    {
        currentState = newState;
        Debug.Log("Mimic state changed to: " + newState);
        if (newState == MimicState.Stalking || newState == MimicState.HumanForm)
        {
            hasPlayedCreepySound = false;
        }
        switch (newState)
        {
            case MimicState.Stalking:
                agent.speed = monsterWalkSpeed;
                agent.acceleration = monsterAcceleration;
                agent.stoppingDistance = 1f;
                if (monsterModel != null) monsterModel.SetActive(true);
                if (humanModelContainer != null) humanModelContainer.SetActive(false);
                break;
            // Bug 2 fix: Chasing state now correctly sets agent speed on transition
            case MimicState.Chasing:
                agent.speed = monsterSpeed;
                agent.acceleration = monsterAcceleration;
                agent.stoppingDistance = attackRange; // Tự động hãm phanh ở tầm đánh để không ủi Player
                if (monsterModel != null) monsterModel.SetActive(true);
                if (humanModelContainer != null) humanModelContainer.SetActive(false);
                break;
            case MimicState.HumanForm:
                agent.speed = humanSpeed;
                agent.acceleration = humanAcceleration;
                agent.stoppingDistance = 1f;
                if (monsterModel != null) monsterModel.SetActive(false);
                if (humanModelContainer != null) humanModelContainer.SetActive(true);
                break;
            // Bug 2 fix: Revealed state sets speed once here, not every frame in HandleRevealed
            case MimicState.Revealed:
                agent.speed = monsterSpeed * 1.5f;
                agent.acceleration = monsterAcceleration * 1.5f;
                agent.stoppingDistance = attackRange;
                if (monsterModel != null) monsterModel.SetActive(true);
                if (humanModelContainer != null) humanModelContainer.SetActive(false);
                break;
        }
    }

    void AttackPlayer(GameObject player)
    {
        PlayerSurvival survival = player.GetComponent<PlayerSurvival>();
        if (survival == null) return;

        // Double-check: don't attack if the player is already dead
        if (survival.currentHealth <= 0) return;

        if (animator != null) animator.SetTrigger("Attack");

        if (audioSource != null && attackClip != null)
        {
            audioSource.clip = attackClip;
            audioSource.Play();
        }

        StartCoroutine(DelayedDamage(survival, player));
    }

    IEnumerator DelayedDamage(PlayerSurvival survival, GameObject player)
    {
        yield return new WaitForSeconds(0.4f); // Chờ animation vung tay chạm tới player

        if (survival != null && survival.currentHealth > 0 && !isDead)
        {
            // Kiểm tra lại khoảng cách một lần nữa, cho phép player né nếu chạy kịp ra ngoài
            Vector3 flatPos = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 flatTarget = new Vector3(player.transform.position.x, 0, player.transform.position.z);
            float dist = Vector3.Distance(flatPos, flatTarget);

            if (dist <= attackRange + 1.0f) // Cho phép sai số khi tay đang vươn tới
            {
                Debug.Log("Mimic attacked " + player.name + " for " + attackDamage + " damage.");
                survival.TakeDamage(attackDamage);

                // If the player died from this hit, handle the identity theft and state change
                if (survival.currentHealth <= 0)
                {
                    Debug.Log("Mimic killed " + player.name + " and stole identity!");
                    CopyIdentity(player);
                    SetState(MimicState.HumanForm);
                }
            }
        }
    }

    void CopyIdentity(GameObject player)
    {
        if (humanModelContainer == null) return;
        foreach (Transform child in humanModelContainer.transform) Destroy(child.gameObject);

        Transform playerModel = player.transform.Find("Model");
        if (playerModel != null)
        {
            currentHumanModel = Instantiate(playerModel.gameObject, humanModelContainer.transform);
            currentHumanModel.transform.localPosition = Vector3.zero;
            currentHumanModel.transform.localRotation = Quaternion.identity;
        }

        if (nametag != null) nametag.text = player.name;
    }

    IEnumerator FlashRed()
    {
        if (isRedLightActive) yield break;
        isRedLightActive = true;
        flashlight.color = redLightColor;
        yield return new WaitForSeconds(0.1f);
        flashlight.color = originalLightColor;
        isRedLightActive = false;
    }

    GameObject FindLonePlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        if (players.Length == 0) return null;
        
        GameObject closest = null;
        float minDist = Mathf.Infinity;
        foreach (GameObject p in players)
        {
            // Bug 4 fix: removed dead-code self-skip (Mimic is never tagged "Player").
            // Also skip players who are already dead so the Mimic seeks a living target.
            PlayerSurvival ps = p.GetComponent<PlayerSurvival>();
            if (ps != null && ps.currentHealth <= 0) continue;

            float dist = Vector3.Distance(transform.position, p.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = p;
            }
        }
        return closest;
    }

    GameObject FindClosestGroup()
    {
        return FindLonePlayer();
    }

    private bool isInvestigating = false;

    public void Investigate(Vector3 targetPos)
    {
        if (isDead) return;
        
        if (currentState != MimicState.Chasing && currentState != MimicState.Revealed)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(targetPos, out hit, 10f, NavMesh.AllAreas))
            {
                SetState(MimicState.Stalking);
                agent.speed = monsterSpeed * 0.85f; 
                agent.SetDestination(hit.position);
                isInvestigating = true;
            }
        }
    }

    private float originalSpeed = -1f;
    private float originalDamage = -1f;

    public void ApplyBloodMoonBuff(float speedMult, float damageMult)
    {
        if (originalSpeed < 0)
        {
            originalSpeed = monsterSpeed;
            originalDamage = attackDamage;
        }
        monsterSpeed = originalSpeed * speedMult;
        attackDamage = originalDamage * damageMult;
        
        if (agent != null) agent.speed = monsterSpeed;
    }

    public void RemoveBloodMoonBuff()
    {
        if (originalSpeed > 0)
        {
            monsterSpeed = originalSpeed;
            attackDamage = originalDamage;
            if (agent != null) agent.speed = monsterSpeed;
        }
    }
}
