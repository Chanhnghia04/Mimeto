using UnityEngine;

/// <summary>
/// Quái vật demo offline. Nó tuần tra, phát hiện người chơi đang chạy và
/// đuổi theo nếu người chơi không cúi. Bị bắt sẽ đưa người chơi về checkpoint.
/// </summary>
public sealed class TutorialMonster : MonoBehaviour
{
    public float patrolDistance = 2.5f;
    public float patrolSpeed = 1.1f;
    public float chaseSpeed = 3.6f;
    public float detectionRadius = 7f;
    public float catchDistance = 1.25f;

    private Transform _target;
    private Vector3 _origin;
    private bool _trainingActive;
    private float _patrolTime;
    private Animator _animator;

    public bool IsAlerted { get; private set; }

    public void Configure(Transform target)
    {
        _target = target;
        _origin = transform.position;
        _animator = GetComponentInChildren<Animator>(true);
    }

    public void SetTrainingActive(bool active)
    {
        _trainingActive = active;
        IsAlerted = false;
    }

    private void Update()
    {
        if (_target == null)
            return;

        bool crouching = Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        float distance = Vector3.Distance(transform.position, _target.position);
        IsAlerted = _trainingActive && !crouching && distance <= detectionRadius;

        if (IsAlerted)
        {
            if (_animator != null)
            {
                _animator.SetFloat("Speed", 1f);
                _animator.SetBool("IsRunning", true);
                _animator.SetBool("IsConfuse", false);
            }
            Vector3 direction = _target.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                transform.position += direction.normalized * chaseSpeed * Time.deltaTime;
            }

            if (distance <= catchDistance)
            {
                var controller = _target.GetComponentInParent<TutorialSceneController>();
                if (controller != null)
                    controller.HandleMonsterCaught();
            }
        }
        else
        {
            if (_animator != null)
            {
                _animator.SetFloat("Speed", _trainingActive ? 0.35f : 0.8f);
                _animator.SetBool("IsRunning", false);
                _animator.SetBool("IsConfuse", _trainingActive && crouching);
            }
            _patrolTime += Time.deltaTime * patrolSpeed;
            Vector3 patrolPosition = _origin + new Vector3(Mathf.Sin(_patrolTime) * patrolDistance, 0f, 0f);
            transform.position = Vector3.Lerp(transform.position, patrolPosition, Time.deltaTime * 4f);
        }
    }
}
