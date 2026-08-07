using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class ExilerAI : NetworkBehaviour
{
    public enum ExilerState { Idle, Patrol, Investigate, Alert, Chase, Attack, Dead }
    
    [Header("Current State")]
    public ExilerState currentState = ExilerState.Idle;

    [Header("Exiler Stats")]
    public float maxHealth = 150f;
    public float currentHealth;
    public float patrolSpeed = 2.0f;
    public float chaseSpeed = 5.5f;
    public float attackRange = 2.0f;
    public float attackDamage = 14f;
    public float attackCooldown = 2.0f;
    public float attackDelay = 0.5f;

    [Header("Senses (Sự thông minh)")]
    public float sightRadius = 20f;
    public float hearRadius = 30f;
    [Range(0, 360)]
    public float viewAngle = 120f;
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

    // Animation Hashes
    private readonly int hashSpeed = Animator.StringToHash("MoveSpeed");
    private readonly int hashAttack = Animator.StringToHash("Attack");
    private readonly int hashDead = Animator.StringToHash("Die");
    private readonly int hashAlert = Animator.StringToHash("Alert");
    private float pathUpdateTimer = 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        
        currentHealth = maxHealth;
        currentState = ExilerState.Patrol;
        agent.speed = patrolSpeed;

        if (obstacleMask == 0) obstacleMask = LayerMask.GetMask("Default", "Environment", "Wall");
    }

    void Update()
    {
        // Chạy trên Server. Nếu game không start server, quái sẽ đứng im!
        if (currentState == ExilerState.Dead) return;

        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning("[ExilerAI] Quái vật không nằm trên NavMesh! Hãy chắc chắn map đã Bake NavMesh và quái đứng trên phần màu xanh.");
            return;
        }

        UpdateState();
        UpdateAnimations();
    }

    private void UpdateState()
    {
        switch (currentState)
        {
            case ExilerState.Idle:
                break;

            case ExilerState.Patrol:
                PatrolLogic();
                SensePlayer();
                break;

            case ExilerState.Investigate:
                InvestigateLogic();
                SensePlayer();
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
        // Lấy tất cả vật thể xung quanh thay vì chỉ layer Player (tránh lỗi Player không nằm đúng Layer)
        Collider[] hits = Physics.OverlapSphere(transform.position, Mathf.Max(sightRadius, hearRadius));
        
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Player")) continue; // Lọc bằng Tag chắc chắn hơn

            var survival = hit.GetComponent<PlayerSurvival>();
            if (survival == null || survival.currentHealth <= 0) continue;

            Transform p = hit.transform;
            Vector3 dirToPlayer = (p.position - transform.position).normalized;
            float distanceToPlayer = Vector3.Distance(transform.position, p.position);

            bool canSee = false;
            bool canHear = false;

            // 1. Tầm nhìn
            if (distanceToPlayer <= sightRadius)
            {
                if (Vector3.Angle(transform.forward, dirToPlayer) < viewAngle / 2)
                {
                    Vector3 rayOrigin = eyes != null ? eyes.position : transform.position + Vector3.up * 1.5f;
                    Vector3 rayTarget = p.position + Vector3.up * 1f;
                    
                    // Bắn tia Linecast. Nếu chạm cái gì đó
                    if (Physics.Linecast(rayOrigin, rayTarget, out RaycastHit rayHit, obstacleMask))
                    {
                        // Nếu cái chạm phải chính là Player (Player nằm trong obstacle mask) thì tính là nhìn thấy
                        if (rayHit.transform == p || rayHit.transform.IsChildOf(p))
                        {
                            canSee = true;
                        }
                    }
                    else
                    {
                        // Không chạm gì cả (không có tường chắn)
                        canSee = true;
                    }
                }
            }

            // 2. Thính giác siêu nhạy
            bool isPlayerSprinting = false;
            PlayerController pc = hit.GetComponent<PlayerController>();
            if (pc != null && pc.isSprinting) isPlayerSprinting = true;

            if (distanceToPlayer <= hearRadius)
            {
                if (survival.currentHealth < survival.maxHealth * 0.5f) canHear = true; // Nghe tiếng thở dốc
                if (isPlayerSprinting) canHear = true; // Nghe tiếng bước chân chạy nước rút, xuyên tường
            }

            if (canSee || canHear)
            {
                targetPlayer = p;
                lastKnownPosition = targetPlayer.position;

                if (currentState == ExilerState.Patrol || currentState == ExilerState.Investigate)
                {
                    if (RandomEventManager.IsBloodMoonActive)
                    {
                        currentState = ExilerState.Alert;
                        agent.isStopped = true;
                        agent.velocity = Vector3.zero;
                        animator.SetTrigger(hashAlert);
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

        lastKnownPosition = targetPlayer.position;
        agent.isStopped = false;
        
        // Kích hoạt cuồng nộ (Frenzy) nếu quái mất nửa máu hoặc Trăng Máu
        bool isFrenzied = currentHealth <= maxHealth * 0.5f || RandomEventManager.IsBloodMoonActive;
        agent.speed = isFrenzied ? chaseSpeed * 1.3f : chaseSpeed;
        
        // Thuật toán bọc lót (Intercept) đoán hướng di chuyển của người chơi
        pathUpdateTimer -= Time.deltaTime;
        if (pathUpdateTimer <= 0f)
        {
            Vector3 targetDest = targetPlayer.position;
            PlayerController pc = targetPlayer.GetComponent<PlayerController>();
            CharacterController cc = targetPlayer.GetComponent<CharacterController>();
            
            if (pc != null && pc.isMoving && cc != null)
            {
                Vector3 playerVel = cc.velocity;
                playerVel.y = 0;
                // Đón đầu chặn đường trước 1.5 giây
                targetDest = targetPlayer.position + playerVel * 1.5f; 
            }
            
            agent.SetDestination(targetDest);
            pathUpdateTimer = 0.2f; // Chỉ cập nhật đường đi 5 lần/giây để tránh lỗi velocity = 0 (trượt)
        }
        
        float distance = Vector3.Distance(transform.position, targetPlayer.position);
        
        if (distance <= attackRange)
        {
            currentState = ExilerState.Attack;
        }
        else if (distance > sightRadius * 1.5f) 
        {
            LoseTarget();
        }
        else
        {
            // Update the check during chase
            Vector3 rayOrigin = eyes != null ? eyes.position : transform.position + Vector3.up * 1.5f;
            Vector3 rayTarget = targetPlayer.position + Vector3.up * 1f;
            if (Physics.Linecast(rayOrigin, rayTarget, out RaycastHit hit, obstacleMask))
            {
                if (hit.transform != targetPlayer && !hit.transform.IsChildOf(targetPlayer))
                {
                    LoseTarget(); // Bị khuất tường, chuyển sang Investigate
                }
            }
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

        bool isFrenzied = currentHealth <= maxHealth * 0.5f;
        float currentCooldown = isFrenzied ? attackCooldown * 0.5f : attackCooldown;

        if (Time.time >= lastAttackTime + currentCooldown)
        {
            lastAttackTime = Time.time;
            animator.SetTrigger(hashAttack);
            
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
            float currentSpeed = agent.velocity.magnitude;
            
            float animSpeed = 0f;
            if (!agent.isStopped && currentSpeed > 0.1f)
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

    public void TakeDamage(float damage)
    {
       
        
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
                animator.SetTrigger(hashAlert);
                alertTimer = 1.0f;
            }
            else
            {
                currentState = ExilerState.Chase;
                agent.isStopped = false;
                agent.speed = chaseSpeed;
            }
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        currentState = ExilerState.Dead;
        agent.isStopped = true;
        
        if (animator != null)
        {
            animator.SetTrigger(hashDead);
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
}
