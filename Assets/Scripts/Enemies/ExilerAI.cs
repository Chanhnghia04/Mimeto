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

    [Header("Senses â€” Blind / Sound-Only (MÃ¹ â€” chá»‰ nghe)")]
    [Tooltip("BÃ¡n kÃ­nh nghe tiáº¿ng bÆ°á»›c chÃ¢n Ä‘i/cháº¡y bÃ¬nh thÆ°á»ng (mÃ©t)")]
    public float hearRadius = 50f;
    [Tooltip("Player ngá»“i (crouch) di chuyá»ƒn cÃ³ bá»‹ phÃ¡t hiá»‡n khÃ´ng?")]
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
            agent.updateRotation = false; // Táº¯t tá»± Ä‘á»™ng xoay giáº­t cá»¥c
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

        // Cháº¡y trÃªn Server. Náº¿u game khÃ´ng start server, quÃ¡i sáº½ Ä‘á»©ng im!
        if (currentState == ExilerState.Dead) return;

        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning("[ExilerAI] QuÃ¡i váº­t khÃ´ng náº±m trÃªn NavMesh! HÃ£y cháº¯c cháº¯n map Ä‘Ã£ Bake NavMesh vÃ  quÃ¡i Ä‘á»©ng trÃªn pháº§n mÃ u xanh.");
            return;
        }

        // --- SMOOTH ROTATION (Phong cÃ¡ch game kinh dá»‹) ---
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
        if (_audioEmitter != null && IsServer)
        {
            _audioEmitter.isChasing.Value = (currentState == ExilerState.Chase || currentState == ExilerState.Attack);
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
        agent.isStopped = false; // Äáº£m báº£o quÃ¡i Ä‘Æ°á»£c phÃ©p di chuyá»ƒn
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

        if (agent.hasPath && agent.remainingDistance > 1f)
        {
            agent.isStopped = false;
            agent.speed = patrolSpeed * 1.5f; // Cháº¡y nhanh tá»›i vá»‹ trÃ­ máº¥t dáº¥u
            searchSweepTimer = 0f;
        }
        else
        {
            // Äi loanh quanh xung quanh vá»‹ trÃ­ máº¥t dáº¥u (bÃ¡n kÃ­nh 4 mÃ©t)
            searchSweepTimer -= Time.deltaTime;
            if (searchSweepTimer <= 0f)
            {
                Vector3 randomWander = Random.insideUnitSphere * 4f;
                randomWander.y = 0;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(lastKnownPosition + randomWander, out hit, 4f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                    agent.isStopped = false;
                    agent.speed = patrolSpeed;
                }
                searchSweepTimer = Random.Range(1.5f, 3f);
            }

            investigateTimer -= Time.deltaTime;
            if (investigateTimer <= 0f)
            {
                currentState = ExilerState.Patrol;
            }
        }
    }

    private void SensePlayer()
    {
        // === EXILER Bá»Š MÃ™ â€” CHá»ˆ PHÃ T HIá»†N Báº°NG Ã‚M THANH ===
        Collider[] hits = Physics.OverlapSphere(transform.position, hearRadius);
        
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Player")) continue;

            var survival = hit.GetComponent<PlayerSurvival>();
            if (survival == null || survival.currentHealth <= 0) continue;

            PlayerController pc = hit.GetComponent<PlayerController>();
            if (pc == null) continue;
            if (pc.isHiding) continue;

            float distanceToPlayer = Vector3.Distance(transform.position, hit.transform.position);
            if (distanceToPlayer > hearRadius) continue;

            // --- TÃ­nh toÃ¡n má»©c Ä‘á»™ tiáº¿ng á»“n ---
            bool isPlayerMakingNoise = false;

            if (RandomEventManager.IsBloodMoonActive)
            {
                // TrÄƒng mÃ¡u: XuyÃªn tÆ°á»ng, tháº¥y háº¿t má»i thá»© dÃ¹ ngá»“i im hay trá»‘n
                isPlayerMakingNoise = true;
            }
            else if (pc.netIsCrouching.Value)
            {
                // Ngá»“i di chuyá»ƒn = gáº§n nhÆ° im láº·ng
                isPlayerMakingNoise = crouchDetectable && pc.netIsMoving.Value;
            }
            else if (pc.netIsMoving.Value || pc.netIsSprinting.Value)
            {
                // Äi bá»™ hoáº·c cháº¡y bÃ¬nh thÆ°á»ng = táº¡o tiáº¿ng á»“n
                isPlayerMakingNoise = true;
            }

            // Tim Ä‘áº­p nhanh, mÃ¡u tháº¥p, hoáº·c quÃ¡ gáº§n (< 4m) Ä‘á»u bá»‹ Exiler phÃ¡t hiá»‡n
            if (survival.currentHealth < survival.maxHealth * 0.5f || survival.netCurrentBPM.Value > 90f || distanceToPlayer < 4f)
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
                        investigateTimer = 12f; // Khá»Ÿi táº¡o bá»™ nhá»› 12s
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
            investigateTimer = 12f; // Khá»Ÿi táº¡o bá»™ nhá»› 12s
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

        // === MÃ™: Kiá»ƒm tra tiáº¿ng á»“n ===
        PlayerController pc = targetPlayer.GetComponent<PlayerController>();
        bool isMakingNoise = false;
        
        if (RandomEventManager.IsBloodMoonActive)
            isMakingNoise = true;
        else if (pc != null)
        {
            if (pc.netIsCrouching.Value)
                isMakingNoise = crouchDetectable && pc.netIsMoving.Value;
            else if (pc.netIsMoving.Value || pc.netIsSprinting.Value)
                isMakingNoise = true;
        }
        
        float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
        if (survival.currentHealth < survival.maxHealth * 0.5f || survival.netCurrentBPM.Value > 90f || distanceToPlayer < 4f)
            isMakingNoise = true;
            
        if (!isMakingNoise)
        {
            investigateTimer -= Time.deltaTime; // DÃ¹ng investigateTimer lÃ m bá»™ nhá»› Ä‘uá»•i theo (chase memory)
            if (investigateTimer <= 0f)
            {
                // Máº¥t dáº¥u hoÃ n toÃ n sau 12s im láº·ng -> chuyá»ƒn sang tÃ¬m kiáº¿m xung quanh (Investigate)
                LoseTarget();
                return;
            }
        }
        else
        {
            investigateTimer = 12f; // Äáº·t bá»™ nhá»› 12s má»—i khi nghe tháº¥y tiáº¿ng!
        }

        lastKnownPosition = targetPlayer.position;
        agent.isStopped = false;
        
        // KÃ­ch hoáº¡t cuá»“ng ná»™ (Frenzy) náº¿u quÃ¡i máº¥t ná»­a mÃ¡u hoáº·c TrÄƒng MÃ¡u
        bool isFrenzied = currentHealth.Value <= maxHealth * 0.5f || RandomEventManager.IsBloodMoonActive;
        agent.speed = isFrenzied ? chaseSpeed * 1.3f : chaseSpeed;
        
        pathUpdateTimer -= Time.deltaTime;
        if (pathUpdateTimer <= 0f)
        {
            agent.SetDestination(targetPlayer.position);
            pathUpdateTimer = 0.2f;
        }
        
        Vector3 flatPosChase = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 flatTargetChase = new Vector3(targetPlayer.position.x, 0, targetPlayer.position.z);
        float distance = Vector3.Distance(flatPosChase, flatTargetChase);
        
        if (distance <= attackRange)
        {
            currentState = ExilerState.Attack;
        }
        else if (distance > hearRadius * 1.2f) 
        {
            // QuÃ¡ xa táº§m nghe â†’ máº¥t má»¥c tiÃªu
            LoseTarget();
        }
    }

    private void LoseTarget()
    {
        if (targetPlayer != null)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(targetPlayer.position, out hit, 10f, NavMesh.AllAreas))
                lastKnownPosition = hit.position;
            else
                lastKnownPosition = targetPlayer.position;
        }
        
        targetPlayer = null;
        currentState = ExilerState.Investigate;
        investigateTimer = 12.0f; // Exiler sáº½ tiáº¿p tá»¥c tÃ¬m kiáº¿m vÃ  lÃ¹ng sá»¥c táº¡i vá»‹ trÃ­ cuá»‘i cÃ¹ng trong 12s
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

        Vector3 flatPosAttack = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 flatTargetAttack = new Vector3(targetPlayer.position.x, 0, targetPlayer.position.z);
        float distance = Vector3.Distance(flatPosAttack, flatTargetAttack);
        if (distance > attackRange + 0.5f)
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
            TriggerAttackSound();
            
            // DÃ¹ng Coroutine Ä‘á»ƒ delay sÃ¡t thÆ°Æ¡ng cho khá»›p vá»›i animation
            StartCoroutine(DealDamageWithDelay(attackDamage, 0.4f));
            
            agent.isStopped = false;
        }
    }

    private IEnumerator DealDamageWithDelay(float damage, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (targetPlayer != null)
        {
            Vector3 flatPos = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 flatTarget = new Vector3(targetPlayer.position.x, 0, targetPlayer.position.z);
            if (Vector3.Distance(flatPos, flatTarget) <= attackRange + 0.5f)
            {
                PlayerSurvival survival = targetPlayer.GetComponent<PlayerSurvival>();
                if (survival != null && survival.currentHealth > 0)
                {
                    survival.TakeDamage(damage, "Killed by Exiler");
                    Debug.Log($"[ExilerAI] ÄaÌƒ tÃ¢Ìn cÃ´ng {targetPlayer.name}, trÃ´Ì€u {damage} maÌu!");
                }
            }
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
                currentSpeed = (agent != null && agent.enabled && agent.isOnNavMesh) ? agent.velocity.magnitude : 0f;
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
        UnityEngine.Debug.Log($"Exiler taking {damage} damage on server. Current HP: {currentHealth.Value}");
        if (!IsServer) return; // FIX: Prevent local damage on client
        if (currentState == ExilerState.Dead) return;
        
        currentHealth.Value -= damage;

        // Náº¿u Ä‘ang khÃ´ng truy Ä‘uá»•i mÃ  bá»‹ báº¯n lÃ©n, láº­p tá»©c vÃ o tráº¡ng thÃ¡i Alert
        if (currentState == ExilerState.Patrol || currentState == ExilerState.Idle || currentState == ExilerState.Investigate)
        {
            // Quay máº·t vá» hÆ°á»›ng bá»‹ báº¯n báº±ng cÃ¡ch tÃ¬m ngÆ°á»i chÆ¡i gáº§n nháº¥t
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

            if (targetPlayer == null)
            {
                currentState = ExilerState.Alert;
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                TriggerAnimClientRpc(hashAlert);
                alertTimer = 3.0f;
            }
            else if (RandomEventManager.IsBloodMoonActive)
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
        UnityEngine.Debug.Log("Exiler Die() called!");
        if (!IsServer) return; // FIX: Despawn and Die logic should run on Server

        currentState = ExilerState.Dead;
        agent.isStopped = true;

        // Play death sound via audio emitter
        TriggerDeathSound();
        
        if (animator != null)
        {
            TriggerAnimClientRpc(hashDead);
        }
        
        Invoke(nameof(DespawnServer), 5f);
    }

    private void DespawnServer()
    {
        if (IsServer)
        {
            NetworkObject.Despawn(true);
        }
    }

    private void TriggerAttackSound()
    {
        if (_audioEmitter != null) _audioEmitter.PlayAttackSoundClientRpc();
    }

    private void TriggerDeathSound()
    {
        if (_audioEmitter != null) _audioEmitter.PlayDeathSoundClientRpc();
    }

    [ClientRpc]
    private void TriggerAnimClientRpc(int hashValue)
    {
        if (animator != null)
        {
            animator.SetTrigger(hashValue);
        }
        if (hashValue == hashDead)
        {
            currentState = ExilerState.Dead;
            if (agent != null) agent.enabled = false;
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }
    }

    public void ForceInvestigate(Vector3 pos)
    {
        if (!IsServer || currentState == ExilerState.Dead) return;
        
        targetPlayer = null;
        lastKnownPosition = pos;

        currentState = ExilerState.Investigate;
        investigateTimer = 8f;
        
        if (agent.isOnNavMesh)
        {
            agent.SetDestination(lastKnownPosition);
            agent.isStopped = false;
            agent.speed = chaseSpeed * 0.8f;
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
            originalSpeed = chaseSpeed;
            originalPatrolSpeed = patrolSpeed;
            originalDamage = attackDamage;
            originalHearRadius = hearRadius;
        }
        chaseSpeed = originalSpeed * speedMult;
        patrolSpeed = originalPatrolSpeed * speedMult;
        attackDamage = originalDamage * buffMult;
        hearRadius = originalHearRadius * buffMult;

        if (agent != null)
        {
            agent.speed = (currentState == ExilerState.Chase || currentState == ExilerState.Attack) ? chaseSpeed : patrolSpeed;
        }
    }

    public void RemoveBloodMoonBuff()
    {
        if (originalSpeed > 0)
        {
            chaseSpeed = originalSpeed;
            patrolSpeed = originalPatrolSpeed;
            attackDamage = originalDamage;
            hearRadius = originalHearRadius;

            if (agent != null)
            {
                agent.speed = (currentState == ExilerState.Chase || currentState == ExilerState.Attack) ? chaseSpeed : patrolSpeed;
            }
        }
    }
}
