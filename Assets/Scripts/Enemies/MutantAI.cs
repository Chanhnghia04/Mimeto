using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using Mimeto.Audio;

[RequireComponent(typeof(NavMeshAgent))]
public class MutantAI : NetworkBehaviour
{
    public enum MutantState { Patrol, Listen, Charge, Confused, Attack, Investigate }
    
    [Header("Current State")]
    public MutantState currentState = MutantState.Patrol;
    private NetworkVariable<int> _netState = new NetworkVariable<int>((int)MutantState.Patrol);
    private NetworkVariable<float> _netSpeed = new NetworkVariable<float>(0f);

    [Header("Mutant Stats")]
    public NetworkVariable<float> health = new NetworkVariable<float>(300f);
    public float patrolSpeed = 1.5f;
    public float chargeSpeed = 8.5f;
    public float attackRange = 2.5f;
    public float attackDamage = 10f;
    public float attackRate = 1.2f;

    [Header("Senses")]
    [Tooltip("The radius where Mutant can hear sprinting or detect nearby players")]
    public float hearRadius = 45f;
    [Tooltip("Layer chứa Player — đặt đúng layer để tối ưu hiệu năng OverlapSphere")]
    public LayerMask playerLayer = ~0;

    [Header("References")]
    public NavMeshAgent agent;
    public Animator animator;
    public AudioSource audioSource;
    public AudioClip chargeScreamClip;
    public AudioClip attackClip;
    public AudioClip confusedClip;

    private PlayerController targetPlayer;
    private Vector3 investigateTarget;
    private float lastAttackTime;
    private float patrolTimer = 0f;
    private float confuseTimer = 0f;
    private float investigateTimer = 0f;
    private float pathUpdateTimer = 0f;
    private float heartbeatCheckTimer = 0f;
    private bool isDead = false;
    private MonsterAudioEmitter _audioEmitter;
    private float stuckTimer = 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        _audioEmitter = GetComponent<MonsterAudioEmitter>();
        
        // FIX: Chống giật khi va BoxCollider
        // Thêm Rigidbody kinematic để physics không đẩy ngược NavMeshAgent
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        
        // Tắt autoBraking — tránh dao động stop/go khi gần stoppingDistance
        if (agent != null && agent.enabled)
        {
            agent.autoBraking = false;
            agent.speed = patrolSpeed;
        }
        
        currentState = MutantState.Patrol;
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
        UpdateAnimator();

        // Server-only: prevent all clients from running AI logic independently
        if (Unity.Netcode.NetworkManager.Singleton != null && !Unity.Netcode.NetworkManager.Singleton.IsServer) 
        {
            if (agent != null && agent.enabled) agent.enabled = false;
            return;
        }
        
        if (health.Value <= 0 || isDead) return;
        
        // --- SMOOTH ROTATION (Phong cách game kinh dị) ---
        if (agent.enabled && !agent.isStopped && agent.desiredVelocity.sqrMagnitude > 0.1f && currentState != MutantState.Attack)
        {
            Vector3 direction = agent.desiredVelocity.normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 6f);
            }
        }
        
        switch (currentState)
        {
            case MutantState.Patrol:
                HandlePatrol();
                break;
            case MutantState.Charge:
                HandleCharge();
                break;
            case MutantState.Confused:
                HandleConfused();
                break;
            case MutantState.Attack:
                HandleAttack();
                break;
            case MutantState.Investigate:
                HandleInvestigate();
                break;
        }

        // Update audio emitter chase state
        if (_audioEmitter != null && IsServer)
        {
            _audioEmitter.isChasing.Value = (currentState == MutantState.Charge || currentState == MutantState.Attack);
        }
    }



    void HandlePatrol()
    {
        agent.stoppingDistance = 0f;
        agent.speed = patrolSpeed;
        patrolTimer += Time.deltaTime;
        
        if (patrolTimer >= 5f || (!agent.pathPending && agent.remainingDistance < 0.5f))
        {
            patrolTimer = 0f;
            Vector2 randomDir = Random.insideUnitCircle * 15f;
            Vector3 randomPos = transform.position + new Vector3(randomDir.x, 0, randomDir.y);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPos, out hit, 15f, NavMesh.AllAreas))
            {
                if (agent.isOnNavMesh) agent.SetDestination(hit.position); // FIX: Safe check
            }
        }

        heartbeatCheckTimer -= Time.deltaTime;
        if (heartbeatCheckTimer <= 0f)
        {
            heartbeatCheckTimer = 0.5f; // Chỉ quét 2 lần 1 giây để tối ưu hiệu năng
            SenseSurroundings();
        }
    }

    public float viewRadius = 30f;
    public float viewAngle = 100f; // 50 degrees left/right

    void SenseSurroundings()
    {
        float maxRadius = Mathf.Max(viewRadius, hearRadius);
        Collider[] hits = Physics.OverlapSphere(transform.position, maxRadius, playerLayer);
        PlayerController bestTarget = null;
        float highestThreat = 0f;

        foreach (var col in hits)
        {
            PlayerController p = col.GetComponentInParent<PlayerController>();
            if (p == null || p.isHiding) continue;
            PlayerSurvival ps = p.GetComponent<PlayerSurvival>();
            if (ps == null || ps.currentHealth <= 0) continue;

            float dist = Vector3.Distance(transform.position, p.transform.position);
            float threat = 0f;
            bool detected = false;

            // 1. VISION CHECK
            if (dist <= viewRadius)
            {
                Vector3 dirToPlayer = (p.transform.position - transform.position).normalized;
                if (Vector3.Angle(transform.forward, dirToPlayer) < viewAngle / 2f)
                {
                    RaycastHit hit;
                    // Raycast từ vị trí mắt (cao khoảng 1.5m)
                    if (Physics.Raycast(transform.position + Vector3.up * 1.5f, dirToPlayer, out hit, viewRadius))
                    {
                        if (hit.transform.CompareTag("Player") || hit.transform.GetComponentInParent<PlayerController>() != null)
                        {
                            detected = true;
                            threat += 300f - dist; // Nhìn thấy là ưu tiên cao nhất, ưu tiên kẻ gần hơn
                        }
                    }
                }
            }

            // 2. HEARING CHECK
            if (dist <= hearRadius)
            {
                if (RandomEventManager.IsBloodMoonActive || p.netIsSprinting.Value || dist < 8f) 
                {
                    detected = true;
                    threat += 100f - (dist * 0.5f);
                    if (RandomEventManager.IsBloodMoonActive) threat += 100f;
                }
            }

            if (detected && threat > highestThreat)
            {
                highestThreat = threat;
                bestTarget = p;
            }
        }

        if (bestTarget != null)
        {
            targetPlayer = bestTarget;
            currentState = MutantState.Charge;
            
            if (audioSource != null && chargeScreamClip != null && !audioSource.isPlaying)
            {
                PlaySoundClientRpc(0);
            }
        }
    }

    void HandleCharge()
    {
        if (targetPlayer == null)
        {
            SetConfused();
            return;
        }

        PlayerSurvival ps = targetPlayer.GetComponent<PlayerSurvival>();
        if (ps == null || ps.currentHealth <= 0)
        {
            targetPlayer = null;
            SetConfused();
            return;
        }

        Vector3 flatPos = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 flatTarget = new Vector3(targetPlayer.transform.position.x, 0, targetPlayer.transform.position.z);
        float dist = Vector3.Distance(flatPos, flatTarget);
        
        // Nếu player ngồi yên và ở xa > 8m, Mutant mất dấu
        if (!targetPlayer.netIsMoving.Value && targetPlayer.netIsCrouching.Value)
        {
            if (dist > 8f)
            {
                // Đi tới vị trí cuối cùng nghe thấy thay vì đứng yên ngơ ngác ngay lập tức
                if (agent.isOnNavMesh) agent.SetDestination(targetPlayer.transform.position);
                targetPlayer = null;
                SetConfused();
                return;
            }
        }

        agent.isStopped = false;
        agent.speed = chargeSpeed;
        agent.stoppingDistance = attackRange * 0.8f;

        pathUpdateTimer -= Time.deltaTime;
        if (pathUpdateTimer <= 0f)
        {
            if (agent.isOnNavMesh) agent.SetDestination(targetPlayer.transform.position);
            pathUpdateTimer = 0.2f;
        }
        
        // FIX: Stuck detection — nếu bị kẹt collider quá 1.5s → mất dấu, quay patrol
        if (dist > attackRange)
        {
            bool isStuck = agent.velocity.sqrMagnitude < 0.25f && !agent.pathPending;
            bool pathBlocked = agent.pathStatus == NavMeshPathStatus.PathPartial 
                               && agent.remainingDistance < 1f;
            
            if (isStuck || pathBlocked)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer >= 1.5f)
                {
                    stuckTimer = 0f;
                    targetPlayer = null;
                    SetConfused();
                    return;
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }
        
        if (dist <= attackRange)
        {
            // Bắt đầu tấn công: Giữ lại một chút quán tính trượt lên thay vì phanh cháy đường (Khựng lại hoàn toàn)
            agent.isStopped = true;
            agent.velocity = agent.velocity * 0.25f; 
            agent.ResetPath();
            currentState = MutantState.Attack;
        }
    }

    void SetConfused()
    {
        currentState = MutantState.Confused;
        confuseTimer = 4f;
        // Quái vật không dừng hẳn mà sẽ đi chầm chậm tìm kiếm xung quanh (Cinematic feel)
        if (agent.isOnNavMesh) agent.isStopped = false;
        agent.speed = patrolSpeed * 0.4f; 

        if (audioSource != null && confusedClip != null)
        {
            PlaySoundClientRpc(2);
        }
    }

    void HandleConfused()
    {
        confuseTimer -= Time.deltaTime;
        if (confuseTimer <= 0)
        {
            agent.isStopped = false;
            currentState = MutantState.Patrol;
        }
    }

    void HandleAttack()
    {
        if (targetPlayer == null)
        {
            SetConfused();
            return;
        }

        Vector3 flatPos = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 flatTarget = new Vector3(targetPlayer.transform.position.x, 0, targetPlayer.transform.position.z);
        float dist = Vector3.Distance(flatPos, flatTarget);
        
        // Nếu người chơi chạy ra khỏi tầm đánh, quay lại trạng thái rượt đuổi
        if (dist > attackRange + 0.5f)
        {
            agent.isStopped = false;
            currentState = MutantState.Charge;
            return;
        }
        
        // Xoay mặt về phía người chơi khi tấn công
        Vector3 direction = (targetPlayer.transform.position - transform.position).normalized;
        direction.y = 0;
        if (direction.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 10f);

        if (Time.time - lastAttackTime >= attackRate)
        {
            lastAttackTime = Time.time;
            
            if (animator != null)
            {
                // Sử dụng Trigger Punch đã thiết lập trong Animator
                TriggerAnimClientRpc("Punch");
            }
            if (audioSource != null && attackClip != null) PlaySoundClientRpc(1);

            StartCoroutine(DealDamageAfterDelay(0.5f)); 
        }
    }

    System.Collections.IEnumerator DealDamageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (targetPlayer != null && !isDead)
        {
            Vector3 flatPos = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 flatTarget = new Vector3(targetPlayer.transform.position.x, 0, targetPlayer.transform.position.z);
            float dist = Vector3.Distance(flatPos, flatTarget);

            // Chỉ gây sát thương nếu Player vẫn còn trong tầm đánh (thêm độ trễ / leniency 1.5f)
            if (dist <= attackRange + 1.5f) 
            {
                PlayerSurvival survival = targetPlayer.GetComponent<PlayerSurvival>();
                if (survival != null && survival.currentHealth > 0)
                {
                    survival.TakeDamage(attackDamage, "Mutant Crushing Blow");
                    if (!survival.IsBleeding)
                    {
                        survival.ApplyBleed(2f, 4f); 
                    }
                }
            }
        }
    }

    void UpdateAnimator()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        
        float currentSpeed = 0f;
        MutantState stateToUse = currentState;

        if (IsServer)
        {
            currentSpeed = (agent != null && agent.enabled && agent.isOnNavMesh) ? agent.velocity.magnitude : 0f;
            if (Mathf.Abs(currentSpeed - _netSpeed.Value) > 0.1f)
            {
                _netSpeed.Value = currentSpeed;
            }
            if (_netState.Value != (int)currentState)
            {
                _netState.Value = (int)currentState;
            }
        }
        else
        {
            currentSpeed = _netSpeed.Value;
            stateToUse = (MutantState)_netState.Value;
        }

        animator.SetFloat("Speed", currentSpeed);
        animator.SetBool("IsRunning", stateToUse == MutantState.Charge);
        animator.SetBool("IsConfuse", stateToUse == MutantState.Confused);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestTakeDamageServerRpc(float amount)
    {
        TakeDamage(amount);
    }

    public void TakeDamage(float amount)
    {
        UnityEngine.Debug.Log($"Mutant taking {amount} damage on server. Current HP: {health.Value}");
        if (!IsServer) return; // FIX: Prevent local damage on client
        if (isDead) return;

        health.Value -= amount;
        
        // Bị đánh đau quá thì rống lên và nhắm vào người chơi gần nhất
        SenseSurroundings(); 

        // FIX: Nếu bị bắn lén từ quá xa và không thấy ai, ít nhất phải cảnh giác tìm kiếm tại chỗ
        if (targetPlayer == null && currentState != MutantState.Confused && currentState != MutantState.Investigate)
        {
            ForceInvestigate(transform.position);
        }

        if (health.Value <= 0)
        {
            Die();
        }
    }

    public void ForceTarget(PlayerController player)
    {
        if (isDead) return;
        targetPlayer = player;
        currentState = MutantState.Charge;
        agent.isStopped = false;
        if (audioSource != null && chargeScreamClip != null && !audioSource.isPlaying)
        {
            PlaySoundClientRpc(0);
        }
    }

    void Die()
    {
        UnityEngine.Debug.Log("Mutant Die() called!");
        if (!IsServer) return; // FIX: Ensure Die logic and ClientRpc are only called from Server

        isDead = true;
        agent.enabled = false;
        
        // Play death sound via audio emitter
        if (_audioEmitter != null) _audioEmitter.PlayDeathSoundClientRpc();

        if (animator != null) 
        {
            TriggerAnimClientRpc("Die");
        }
        
        StartCoroutine(DespawnAfterDelay(5f)); // FIX: NetworkObject.Despawn instead of Destroy
    }

    private System.Collections.IEnumerator DespawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (IsServer)
        {
            var netObj = GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned) netObj.Despawn(true);
        }
    }

    private float originalSpeed = -1f;
    private float originalPatrolSpeed = -1f;
    private float originalDamage = -1f;
    private float originalHearRadius = -1f;

    public void ApplyBloodMoonBuff(float speedMult, float buffMult)
    {
        if (originalSpeed < 0)
        {
            originalSpeed = chargeSpeed;
            originalDamage = attackDamage;
            originalPatrolSpeed = patrolSpeed;
            originalHearRadius = hearRadius;
        }
        chargeSpeed = originalSpeed * speedMult;
        patrolSpeed = originalPatrolSpeed * speedMult;
        attackDamage = originalDamage * buffMult;
        hearRadius = originalHearRadius * buffMult;
        
        // Cập nhật agent.speed ngay lập tức cho state hiện tại
        if (agent != null)
        {
            if (currentState == MutantState.Charge || currentState == MutantState.Attack)
                agent.speed = chargeSpeed;
            else
                agent.speed = patrolSpeed;
        }
    }

    public void RemoveBloodMoonBuff()
    {
        if (originalSpeed > 0)
        {
            chargeSpeed = originalSpeed;
            patrolSpeed = originalPatrolSpeed;
            attackDamage = originalDamage;
            hearRadius = originalHearRadius;
            if (agent != null)
            {
                if (currentState == MutantState.Charge || currentState == MutantState.Attack)
                    agent.speed = chargeSpeed;
                else
                    agent.speed = patrolSpeed;
            }
        }
    }

    [ClientRpc]
    private void PlaySoundClientRpc(int soundType)
    {
        if (audioSource == null) return;
        if (soundType == 0 && chargeScreamClip != null) audioSource.PlayOneShot(chargeScreamClip);
        else if (soundType == 1 && attackClip != null) audioSource.PlayOneShot(attackClip);
        else if (soundType == 2 && confusedClip != null) audioSource.PlayOneShot(confusedClip);
    }

    [ClientRpc]
    private void TriggerAnimClientRpc(string triggerName)
    {
        if (animator != null) animator.SetTrigger(triggerName);
        if (triggerName == "Die")
        {
            isDead = true;
            if (agent != null) agent.enabled = false;
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }
    }

    public void ForceInvestigate(Vector3 pos)
    {
        if (!IsServer || isDead) return;
        targetPlayer = null;
        investigateTarget = pos;

        currentState = MutantState.Investigate;
        investigateTimer = 8f;
        if (agent.isOnNavMesh)
        {
            agent.SetDestination(investigateTarget);
            agent.isStopped = false;
            agent.speed = chargeSpeed * 0.8f;
        }
    }

    void HandleInvestigate()
    {
        if (agent.pathPending) return;

        if (agent.hasPath && agent.remainingDistance > 1f)
        {
            agent.isStopped = false;
            agent.speed = chargeSpeed * 0.8f;
        }
        else
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            investigateTimer -= Time.deltaTime;
            
            // Xoay quanh tìm kiếm
            transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y + 60f * Time.deltaTime, 0);

            if (investigateTimer <= 0f)
            {
                SetConfused();
            }
        }
        
        heartbeatCheckTimer -= Time.deltaTime;
        if (heartbeatCheckTimer <= 0f)
        {
            heartbeatCheckTimer = 0.5f;
            SenseSurroundings();
        }
    }
}
