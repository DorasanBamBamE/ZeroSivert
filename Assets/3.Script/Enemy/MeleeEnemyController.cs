using UnityEngine;

// 근접 추적형 적. 좀비 · 늑대에 사용한다.
// 감지와 순찰은 EnemyControllerBase가 처리하고, 여기서는 접근과 공격만 담당한다.
public class MeleeEnemyController : EnemyControllerBase
{
    [Header("근접 공격")]
    [SerializeField] private float attackRange = 0.8f;
    [SerializeField] private float attackDamage = 12f;
    [SerializeField] private float attackCooldown = 1.2f;

    // 공격 모션 중에는 멈춰 선다. 클립 길이에 맞출 것.
    [SerializeField] private float attackFreezeTime = 0.3f;

    [Header("연출")]
    [SerializeField] private EnemyAnimator enemyAnimator;
    [SerializeField] private AudioSource audioSource;

    // 경계 진입과 공격 시. 원작 snd_ghoul_attack 계열.
    [SerializeField] private AudioClip alertSound;
    [SerializeField] private AudioClip attackSound;

    // 배회 중 간헐적으로 내는 신음. 여러 개를 넣으면 랜덤 재생한다.
    [SerializeField] private AudioClip[] idleSounds;
    [SerializeField] private float idleSoundMinDelay = 4f;
    [SerializeField] private float idleSoundMaxDelay = 10f;

    private float attackTimer;
    private float freezeTimer;
    private float idleSoundTimer;

    protected override void Awake()
    {
        base.Awake();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (enemyAnimator == null)
        {
            enemyAnimator = GetComponentInChildren<EnemyAnimator>();
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        attackTimer = 0f;
        freezeTimer = 0f;
        ResetIdleSoundTimer();
    }

    protected override void Update()
    {
        base.Update();

        attackTimer += Time.deltaTime;

        if (freezeTimer > 0f)
        {
            freezeTimer -= Time.deltaTime;
        }

        UpdateIdleSound();
    }

    protected override void TickCombat()
    {
        Vector2 toTarget = (Vector2)target.position - rb.position;
        float distance = toTarget.magnitude;

        FaceDirection(toTarget.x);

        // 공격 모션 중에는 움직이지 않는다.
        if (freezeTimer > 0f)
        {
            Stop();
            return;
        }

        if (distance <= attackRange)
        {
            Stop();
            TryAttack();
            return;
        }

        rb.linearVelocity = toTarget.normalized * moveSpeed;
    }

    private void TryAttack()
    {
        if (attackTimer < attackCooldown)
        {
            return;
        }

        attackTimer = 0f;
        freezeTimer = attackFreezeTime;

        targetStats.TakeDamage(attackDamage);

        if (enemyAnimator != null)
        {
            enemyAnimator.PlayAttack();
        }

        Play(attackSound != null ? attackSound : alertSound);
    }

    protected override void OnAlerted()
    {
        Play(alertSound);
    }

    // 배회 중에만 신음을 낸다. 경계 상태에서는 공격음이 대신한다.
    private void UpdateIdleSound()
    {
        if (idleSounds == null || idleSounds.Length == 0 || IsAlerted)
        {
            return;
        }

        idleSoundTimer -= Time.deltaTime;

        if (idleSoundTimer > 0f)
        {
            return;
        }

        ResetIdleSoundTimer();
        Play(idleSounds[Random.Range(0, idleSounds.Length)]);
    }

    private void ResetIdleSoundTimer()
    {
        idleSoundTimer = Random.Range(idleSoundMinDelay, idleSoundMaxDelay);
    }

    private void Play(AudioClip clip)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        // 같은 소리가 반복될 때 단조롭지 않도록 음정을 흔든다.
        audioSource.pitch = Random.Range(0.92f, 1.08f);
        audioSource.PlayOneShot(clip);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}