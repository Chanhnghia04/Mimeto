using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

[RequireComponent(typeof(NavMeshAgent))]
public class MutantAI : NetworkBehaviour
{
    public enum MutantState { Patrol, Listen, Charge, Confused, Attack }
    
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
    [Tooltip("The radius where Enami can hear high BPM or sprinting")]
    public float hearRadius = 45f;


    [Header("References")]
    public NavMeshAgent agent;
    public Animator animator;
    public AudioSource audioSource;
    public AudioClip chargeScreamClip;
    public AudioClip attackClip;
    public AudioClip confusedClip;

    private PlayerController targetPlayer;
    private float lastAttackTime;
    private float patrolTimer = 0f;
    private float confuseTimer = 0f;
    private float heartbeatCheckTimer = 0f;
    private bool isDead = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        
        agent.speed = patrolSpeed;
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
        }

        UpdateAnimator();
    }



    void HandlePatrol()
    {
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
                agent.SetDestination(hit.position);
            }
        }

        heartbeatCheckTimer -= Time.deltaTime;
        if (heartbeatCheckTimer <= 0f)
        {
            heartbeatCheckTimer = 0.5f; // Chỉ quét 2 lần 1 giây để tối ưu hiệu năng
            ListenForHeartbeats();
        }
    }

    void ListenForHeartbeats()
    {
        PlayerController[] players = FindObjectsByType<PlayerController>();
        PlayerController bestTarget = null;
        float highestThreat = 0f;

        foreach (var p in players)
        {
            if (p.isHiding) continue;
            
            PlayerSurvival ps = p.GetComponent<PlayerSurvival>();
            if (ps == null || ps.currentHealth <= 0) continue;

            float dist = Vector3.Distance(transform.position, p.transform.position);
            if (dist > hearRadius) continue;

            float bpm = (ps.currentHealth / ps.maxHealth) * 50f + 40f;
            if (ps.currentOxygen < ps.lowOxygenThreshold) bpm += 40f;
            if (p.isSprinting) bpm += 30f;
            if (!p.isMoving && p.isCrouching) bpm -= 20f;

            // 3. Bạn đứng quá gần (dist < 8f) dù nhịp tim thấp
            if (bpm > 95f || p.isSprinting || dist < 8f) 
            {
                float threat = bpm - (dist * 0.5f); 
                if (threat > highestThreat)
                {
                    highestThreat = threat;
                    bestTarget = p;
                }
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
        
        float bpm = (ps.currentHealth / ps.maxHealth) * 50f + 40f;
        if (ps.currentOxygen < ps.lowOxygenThreshold) bpm += 40f;
        if (!targetPlayer.isMoving && targetPlayer.isCrouching) bpm -= 20f;

        if (bpm < 85f && !targetPlayer.isMoving && targetPlayer.isCrouching)
        {
            // Đi tới vị trí cuối cùng nghe thấy tiếng tim thay vì đứng yên ngơ ngác ngay lập tức
            if (agent.isOnNavMesh) agent.SetDestination(targetPlayer.transform.position);
            targetPlayer = null;
            SetConfused();
            return;
        }

        agent.isStopped = false;
        agent.speed = chargeSpeed;
        agent.SetDestination(targetPlayer.transform.position);

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
            currentSpeed = agent.velocity.magnitude;
            _netSpeed.Value = currentSpeed;
            _netState.Value = (int)currentState;
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
        if (!IsServer) return; // FIX: Prevent local damage on client
        if (isDead) return;

        health.Value -= amount;
        
        // Bị đánh đau quá thì rống lên và nhắm vào người chơi gần nhất
        ListenForHeartbeats(); 

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
        if (audioSource != null && chargeScreamClip != null && !audioSource.isPlaying)
        {
            PlaySoundClientRpc(0);
        }
    }

    void Die()
    {
        if (!IsServer) return; // FIX: Ensure Die logic and ClientRpc are only called from Server

        isDead = true;
        agent.enabled = false;
        
        if (animator != null) 
        {
            TriggerAnimClientRpc("Die");
        }
        
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        
        this.enabled = false;
        StartCoroutine(DespawnAfterDelay(5f)); // FIX: NetworkObject.Despawn instead of Destroy
    }

    private System.Collections.IEnumerator DespawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (IsServer)
        {
            GetComponent<NetworkObject>().Despawn(true);
        }
    }

    private float originalSpeed = -1f;
    private float originalDamage = -1f;
    private float originalPatrolSpeed = -1f;

    public void ApplyBloodMoonBuff(float speedMult, float damageMult)
    {
        if (originalSpeed < 0)
        {
            originalSpeed = chargeSpeed;
            originalDamage = attackDamage;
            originalPatrolSpeed = patrolSpeed;
        }
        chargeSpeed = originalSpeed * speedMult;
        patrolSpeed = originalPatrolSpeed * speedMult;
        attackDamage = originalDamage * damageMult;
        
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
    }
}
