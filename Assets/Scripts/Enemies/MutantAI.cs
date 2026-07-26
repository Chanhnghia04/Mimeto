using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MutantAI : MonoBehaviour
{
    public enum MutantState { Patrol, Listen, Charge, Confused, Attack }
    
    [Header("Current State")]
    public MutantState currentState = MutantState.Patrol;

    [Header("Mutant Stats")]
    public float health = 300f;
    public float patrolSpeed = 1.5f;
    public float chargeSpeed = 8.5f;
    public float attackRange = 2.5f;
    public float attackDamage = 10f;
    public float attackRate = 1.2f;

    [Header("Senses")]
    [Tooltip("The radius where Enami can hear high BPM or sprinting")]
    public float hearRadius = 45f;
    [Tooltip("Radius of the toxic gas emitting from its body")]
    public float toxicAuraRadius = 5f;
    public float toxicDamagePerSecond = 10f;

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
    private bool isDead = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        
        agent.speed = patrolSpeed;
        currentState = MutantState.Patrol;
    }

    void Update()
    {
        if (health <= 0 || isDead) return;
        
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

    private void ApplyToxicAura()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, toxicAuraRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                PlayerSurvival survival = hit.GetComponent<PlayerSurvival>();
                if (survival != null && survival.currentHealth > 0)
                {
                    survival.TakeDamage(toxicDamagePerSecond * Time.deltaTime, "Mutant Toxic Aura");
                }
            }
        }
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

        ListenForHeartbeats();
    }

    void ListenForHeartbeats()
    {
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
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
                audioSource.PlayOneShot(chargeScreamClip);
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
            targetPlayer = null;
            SetConfused();
            return;
        }

        agent.isStopped = false;
        agent.speed = chargeSpeed;
        agent.SetDestination(targetPlayer.transform.position);

        if (dist <= attackRange)
        {
            // Bắt đầu tấn công: Khóa cứng vị trí
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
            currentState = MutantState.Attack;
        }
    }

    void SetConfused()
    {
        currentState = MutantState.Confused;
        confuseTimer = 3f;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        if (audioSource != null && confusedClip != null)
        {
            audioSource.PlayOneShot(confusedClip);
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
                animator.SetTrigger("Punch");
            }
            if (audioSource != null && attackClip != null) audioSource.PlayOneShot(attackClip);

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
        if (animator == null) return;
        
        // Cập nhật Speed để chuyển đổi Idle/Walk
        float currentSpeed = agent.velocity.magnitude;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetFloat("Speed", currentSpeed);
            animator.SetBool("IsRunning", currentState == MutantState.Charge);
            animator.SetBool("IsConfuse", currentState == MutantState.Confused);
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        health -= amount;
        
        // Bị đánh đau quá thì rống lên và nhắm vào người chơi gần nhất
        ListenForHeartbeats(); 

        if (health <= 0)
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
            audioSource.PlayOneShot(chargeScreamClip);
        }
    }

    void Die()
    {
        isDead = true;
        agent.enabled = false;
        
        if (animator != null) 
        {
            animator.SetTrigger("Die");
        }
        
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        
        this.enabled = false;
        Destroy(gameObject, 5f);
    }
}