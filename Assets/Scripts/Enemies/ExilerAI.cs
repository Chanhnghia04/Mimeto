using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using System.Collections;
using Mimeto.Audio;

[RequireComponent(typeof(NavMeshAgent))]
public class ExilerAI : NetworkBehaviour
{
    public enum ExilerState { Idle, Patrol, Investigate, Alert, Chase, Attack, Dead }
    
    [Header("Current State")]
    public ExilerState currentState = ExilerState.Idle;
    private NetworkVariable<int> _netState = new NetworkVariable<int>((int)ExilerState.Idle);
    private NetworkVariable<float> _netSpeed = new NetworkVariable<float>(0f);

    [Header("Exiler Stats")]
    public float maxHealth = 150f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(150f);
    public float patrolSpeed = 2.0f;
    public float chaseSpeed = 5.5f;
    public float attackRange = 2.0f;
    public float attackDamage = 14f;
    public float attackCooldown = 2.0f;
    public float attackDelay = 0.5f;

    [Header("Senses — Blind / Sound-Only (Mù — chỉ nghe)")]
    [Tooltip("Bán kính nghe tiếng bước chân đi/chạy bình thường (mét)")]
    public float hearRadius = 50f;
    [Tooltip("Player ngồi (crouch) di chuyển có bị phát hiện không?")]
    public bool crouchDetectable = false;
    public LayerMask obstacleMask;

    [Header("References")]
    public NavMeshAgent agent;
    public Animator animator;
    public Transform eyes;

    private Transform targetPlayer;
    private Vector3 lastKnownPosition;
    private float lastAttackTime;
    private float patrolTimer = 0f;
    private float alertTimer = 0f;
    private float investigateTimer = 0f;
    private float senseTimer = 0f;

    // Animation Hashes
    private readonly int hashSpeed = Animator.StringToHash("MoveSpeed");
    private readonly int hashAttack = Animator.StringToHash("Attack");
    private readonly int hashDead = Animator.StringToHash("Die");
    private readonly int hashAlert = Animator.StringToHash("Alert");
    private float pathUpdateTimer = 0f;
    private MonsterAudioEmitter _audioEmitter;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        _audioEmitter = GetComponent<MonsterAudioEmitter>();
        
        if (IsServer) currentHealth.Value = maxHealth;
        currentState = ExilerState.Patrol;
        agent.speed = patrolSpeed;

        if (obstacleMask == 0) obstacleMask = LayerMask.GetMask("Default", "Environment", "Wall", "Player");
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (!IsServer && agent != null)
        {
            agent.enabled = false;
        }
        else if (IsServer && agent != null)
        {
            agent.updateRotation = false; // Tắt tự động xoay giật cục
        }
    }

    void Update()
    {
        UpdateAnimations();

        if (Unity.Netcode.NetworkManager.Singleton != null && !Unity.Netcode.NetworkManager.Singleton.IsServer) 
        {
            if (agent != null && agent.enabled) agent.enabled = false;
            return;
        }

        // Chạy trên Server. Nếu game không start server, quái sẽ đứng im!
        if (currentState == ExilerState.Dead) return;

        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning("[ExilerAI] Quái vật không nằm trên NavMesh! Hãy chắc chắn map đã Bake NavMesh và quái đứng trên phần màu xanh.");
            return;
        }

        // --- SMOOTH ROTATION (Phong cách game kinh dị) ---
        if (agent.enabled && !agent.isStopped && agent.desiredVelocity.sqrMagnitude > 0.1f && currentState != ExilerState.Attack && currentState != ExilerState.Alert)
        {
            Vector3 direction = agent.desiredVelocity.normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 6f);
            }
        }

        UpdateState();

        // Update audio emitter chase state
        if (_audioEmitter != null)
        {
            _audioEmitter.isChasing = (currentState == ExilerState.Chase || currentState == ExilerState.Attack);
        }
    }

    private void UpdateState()
    {
        switch (currentState)
        {
            case ExilerState.Idle:
                break;

            case ExilerState.Patrol:
                PatrolLogic();
                senseTimer -= Time.deltaTime;
                if (senseTimer <= 0f) { senseTimer = 0.5f; SensePlayer(); }
                break;

            case ExilerState.Investigate:
                InvestigateLogic();
                senseTimer -= Time.deltaTime;
                if (senseTimer <= 0f) { senseTimer = 0.5f; SensePlayer(); }
                break;

            case ExilerState.Alert:
                AlertLogic();
                break;

            case ExilerState.Chase:
                ChaseLogic();
                break;

            case ExilerState.Attack:
                AttackLogic();
                break;
        }
    }

    private void PatrolLogic()
    {
        agent.isStopped = false; // Đảm bảo quái được phép di chuyển
        agent.speed = patrolSpeed;

        patrolTimer -= Time.deltaTime;
        if (patrolTimer <= 0f || agent.remainingDistance < 0.5f)
        {
            Vector3 randomDirection = Random.insideUnitSphere * 15f;
            randomDirection += transform.position;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, 15f, 1))
            {
                agent.SetDestination(hit.position);
            }
            patrolTimer = Random.Range(5f, 10f);
        }
    }

    private float searchSweepTimer = 0f;

    private void InvestigateLogic()
    {
        if (agent.pathPending) return;

        if (agent.remainingDistance > 1f)
        {
            agent.isStopped = false;
            agent.SetDestination(lastKnownPosition);
            agent.speed = patrolSpeed * 1.5f; // Chạy nhanh tới vị trí mất dấu
            searchSweepTimer = 0f;
        }
        else
        {
            // Đứng lại và dáo dác nhìn xung quanh
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            searchSweepTimer += Time.deltaTime;
            
            // Nhìn sang trái phải liên tục
            float sweepAngle = Mathf.Sin(searchSweepTimer * 3f) * 60f;
            transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y + sweepAngle * Time.deltaTime, 0);

            investigateTimer -= Time.deltaTime;
            if (investigateTimer <= 0f)
            {
                currentState = ExilerState.Patrol;
            }
        }
    }

    private void SensePlayer()
    {
        // === EXILER BỊ MÙ — CHỈ PHÁT HIỆN BẰNG ÂM THANH ===
        Collider[] hits = Physics.OverlapSphere(transform.position, hearRadius);
        
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Player")) continue;

            var survival = hit.GetComponent<PlayerSurvival>();
            if (survival == null || survival.currentHealth <= 0) continue;

            PlayerController pc = hit.GetComponent<PlayerController>();
            if (pc == null) continue;

            float distanceToPlayer = Vector3.Distance(transform.position, hit.transform.position);
            if (distanceToPlayer > hearRadius) continue;

            // --- Tính toán mức độ tiếng ồn ---
            bool isPlayerMakingNoise = false;

            if (pc.netIsCrouching.Value)
            {
                // Ngồi di chuyển = gần như im lặng
                isPlayerMakingNoise = crouchDetectable && pc.netIsMoving.Value;
            }
            else if (pc.netIsMoving.Value || pc.netIsSprinting.Value)
            {
                // Đi bộ hoặc chạy bình thường = tạo tiếng ồn
                isPlayerMakingNoise = true;
            }

            // Thở dốc khi máu thấp vẫn bị nghe
            if (survival.currentHealth < survival.maxHealth * 0.5f)
            {
                isPlayerMakingNoise = true;
            }

            if (isPlayerMakingNoise)
            {
                targetPlayer = hit.transform;
                lastKnownPosition = targetPlayer.position;

                if (currentState == ExilerState.Patrol || currentState == ExilerState.Investigate)
                {
                    if (RandomEventManager.IsBloodMoonActive)
                    {
                        currentState = ExilerState.Alert;
                        agent.isStopped = true;
                        agent.velocity = Vector3.zero;
                        TriggerAnimClientRpc(hashAlert);
                        alertTimer = 1.5f; 
                    }
                    else
                    {
                        currentState = ExilerState.Chase;
                        agent.isStopped = false;
                        agent.speed = chaseSpeed;
                    }
                }
                break; 
            }
        }
    }

    private void AlertLogic()
    {
        alertTimer -= Time.deltaTime;

        if (targetPlayer != null)
        {
            Vector3 direction = (targetPlayer.position - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 5f);
            }
        }

        if (alertTimer <= 0f)
        {
            currentState = ExilerState.Chase;
            agent.isStopped = false;
            agent.speed = chaseSpeed;
        }
    }

    private void ChaseLogic()
    {
        if (targetPlayer == null)
        {
            LoseTarget();
            return;
        }

        var survival = targetPlayer.GetComponent<PlayerSurvival>();
        if (survival == null || survival.currentHealth <= 0)
        {
            LoseTarget();
            return;
        }

        // === MÙ: Mất mục tiêu nếu player ngồi xuống (im lặng) ===
        PlayerController pc = targetPlayer.GetComponent<PlayerController>();
        if (pc != null && pc.netIsCrouching.Value && !crouchDetectable)
        {
            // Player ngồi xuống → Exiler không còn nghe thấy → mất dấu
            LoseTarget();
            return;
        }

        lastKnownPosition = targetPlayer.position;
        agent.isStopped = false;
        
        // Kích hoạt cuồng nộ (Frenzy) nếu quái mất nửa máu hoặc Trăng Máu
        bool isFrenzied = currentHealth.Value <= maxHealth * 0.5f || RandomEventManager.IsBloodMoonActive;
        agent.speed = isFrenzied ? chaseSpeed * 1.3f : chaseSpeed;
        
        // Thuật toán bọc lót (Intercept) đoán hướng di chuyển của người chơi
        pathUpdateTimer -= Time.deltaTime;
        if (pathUpdateTimer <= 0f)
        {
            Vector3 targetDest = targetPlayer.position;
            CharacterController cc = targetPlayer.GetComponent<CharacterController>();
            
            if (pc != null && pc.netIsMoving.Value && cc != null)
            {
                float speed = pc.netIsSprinting.Value ? pc.sprintSpeed : pc.walkSpeed;
                Vector3 playerVel = targetPlayer.forward * speed;
                playerVel.y = 0;
                // Đón đầu chặn đường trước 1.5 giây
                targetDest = targetPlayer.position + playerVel * 1.5f; 
            }
            
            // Đảm bảo targetDest nằm trên NavMesh
            if (NavMesh.SamplePosition(targetDest, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                targetDest = hit.position;
            }
            
            agent.SetDestination(targetDest);
            pathUpdateTimer = 0.2f;
        }
        
        float distance = Vector3.Distance(transform.position, targetPlayer.position);
        
        if (distance <= attackRange)
        {
            currentState = ExilerState.Attack;
        }
        else if (distance > hearRadius * 1.2f) 
        {
            // Quá xa tầm nghe → mất mục tiêu
            LoseTarget();
        }
    }

    private void LoseTarget()
    {
        targetPlayer = null;
        currentState = ExilerState.Investigate;
        investigateTimer = 4.0f;
    }

    private void AttackLogic()
    {
        if (targetPlayer == null)
        {
            currentState = ExilerState.Chase;
            return;
        }

        var survival = targetPlayer.GetComponent<PlayerSurvival>();
        if (survival == null || survival.currentHealth <= 0)
        {
            LoseTarget();
            return;
        }

        float distance = Vector3.Distance(transform.position, targetPlayer.position);
        if (distance > attackRange)
        {
            currentState = ExilerState.Chase;
            return;
        }

        Vector3 direction = (targetPlayer.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 10f);
        }

        agent.velocity = Vector3.zero;
        agent.isStopped = true;

        bool isFrenzied = currentHealth.Value <= maxHealth * 0.5f;
        float currentCooldown = isFrenzied ? attackCooldown * 0.5f : attackCooldown;

        if (Time.time >= lastAttackTime + currentCooldown)
        {
            lastAttackTime = Time.time;
            TriggerAnimClientRpc(hashAttack);
            
            // Dùng Coroutine để delay sát thương cho khớp với animation
            StartCoroutine(DealDamageWithDelay(survival, attackDamage, 0.4f));
            
            agent.isStopped = false;
        }
    }

    private IEnumerator DealDamageWithDelay(PlayerSurvival survival, float damage, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (targetPlayer != null && Vector3.Distance(transform.position, targetPlayer.position) <= attackRange + 0.5f)
        {
            survival.TakeDamage(damage, "Killed by Exiler");
            Debug.Log($"[ExilerAI] Đã tấn công {targetPlayer.name}, trừ {damage} máu!");
        }
    }

    private void UpdateAnimations()
    {
        if (animator != null)
        {
            float currentSpeed = 0f;
            ExilerState stateToUse = currentState;

            if (IsServer)
            {
                currentSpeed = agent.velocity.magnitude;
                _netSpeed.Value = currentSpeed;
                _netState.Value = (int)currentState;
            }
            else
            {
                currentSpeed = _netSpeed.Value;
                stateToUse = (ExilerState)_netState.Value;
            }
            
            float animSpeed = 0f;
            bool isStopped = (stateToUse == ExilerState.Idle || stateToUse == ExilerState.Alert || stateToUse == ExilerState.Attack || stateToUse == ExilerState.Dead);

            if (!isStopped && currentSpeed > 0.1f)
            {
                if (currentSpeed <= patrolSpeed + 0.5f) 
                    animSpeed = 0.5f; 
                else 
                    animSpeed = 1f; 
            }
            
            float smoothSpeed = Mathf.Lerp(animator.GetFloat(hashSpeed), animSpeed, Time.deltaTime * 10f);
            animator.SetFloat(hashSpeed, smoothSpeed);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestTakeDamageServerRpc(float damage)
    {
        TakeDamage(damage);
    }

    public void TakeDamage(float damage)
    {
        if (!IsServer) return; // FIX: Prevent local damage on client
        if (currentState == ExilerState.Dead) return;
        
        currentHealth.Value -= damage;

        // Nếu đang không truy đuổi mà bị bắn lén, lập tức vào trạng thái Alert
        if (currentState == ExilerState.Patrol || currentState == ExilerState.Idle || currentState == ExilerState.Investigate)
        {
            // Quay mặt về hướng bị bắn bằng cách tìm người chơi gần nhất
            Collider[] hits = Physics.OverlapSphere(transform.position, hearRadius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player"))
                {
                    targetPlayer = hit.transform;
                    lastKnownPosition = targetPlayer.position;
                    break;
                }
            }

            if (RandomEventManager.IsBloodMoonActive)
            {
                currentState = ExilerState.Alert;
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                TriggerAnimClientRpc(hashAlert);
                alertTimer = 1.0f;
            }
            else
            {
                currentState = ExilerState.Chase;
                agent.isStopped = false;
                agent.speed = chaseSpeed;
            }
        }

        if (currentHealth.Value <= 0)
        {
            Die();
        }
    }

    public void ForceTarget(PlayerController player)
    {
        if (!IsServer || currentState == ExilerState.Dead) return;
        
        targetPlayer = player.transform;
        lastKnownPosition = targetPlayer.position;

        if (currentState == ExilerState.Patrol || currentState == ExilerState.Idle || currentState == ExilerState.Investigate)
        {
            if (RandomEventManager.IsBloodMoonActive)
            {
                currentState = ExilerState.Alert;
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                TriggerAnimClientRpc(hashAlert);
                alertTimer = 1.0f;
            }
            else
            {
                currentState = ExilerState.Chase;
                agent.isStopped = false;
                agent.speed = chaseSpeed;
            }
        }
    }

    private void Die()
    {
        if (!IsServer) return; // FIX: Despawn and Die logic should run on Server

        currentState = ExilerState.Dead;
        agent.isStopped = true;

        // Play death sound via audio emitter
        if (_audioEmitter != null) _audioEmitter.PlayDeathSound();
        
        if (animator != null)
        {
            TriggerAnimClientRpc(hashDead);
        }
        
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        
        Invoke(nameof(DespawnServer), 5f);
    }

    private void DespawnServer()
    {
        if (IsServer)
        {
            NetworkObject.Despawn(true);
        }
    }

    [ClientRpc]
    private void TriggerAnimClientRpc(int hashValue)
    {
        if (animator != null)
        {
            animator.SetTrigger(hashValue);
        }
    }
}
