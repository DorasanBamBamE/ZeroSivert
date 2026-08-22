using UnityEngine;

// 적 애니메이션 상태 전환. 좀비 · 늑대 · 밴딧 공통으로 사용한다.
//
// Animator 상태 이름은 아래와 정확히 일치해야 한다.
//   EnemyIdle / EnemyRun / EnemyDead / EnemyAttack
//
// 적 종류가 달라도 이름은 동일하게 두고 클립만 각자의 스프라이트로 채운다.
// 그래야 컨트롤러만 갈아끼우면 되고 스크립트를 손댈 필요가 없다.
//
// EnemyAttack 상태가 없는 적(공격 클립 미제작)은 그냥 두면 된다.
// Animator.Play는 없는 상태를 조용히 무시하고, 타이머가 끝나면 원래대로 돌아온다.
//
// Animator 컨트롤러에는 트랜지션을 만들지 않고 코드에서 직접 재생한다.
public class EnemyAnimator : MonoBehaviour
{
    private static readonly int IdleState = Animator.StringToHash("EnemyIdle");
    private static readonly int RunState = Animator.StringToHash("EnemyRun");
    private static readonly int DeadState = Animator.StringToHash("EnemyDead");
    private static readonly int AttackState = Animator.StringToHash("EnemyAttack");

    [SerializeField] private Animator animator;
    [SerializeField] private EnemyControllerBase controller;

    // 공격 애니메이션 재생 시간. 실제 클립 길이에 맞출 것.
    // 클립보다 길게 잡으면 공격 후 멈춰 있는 것처럼 보인다.
    [SerializeField] private float attackDuration = 0.4f;

    private int currentState;
    private bool isDead;
    private float attackTimer;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (controller == null)
        {
            controller = GetComponentInParent<EnemyControllerBase>();
        }
    }

    private void OnEnable()
    {
        isDead = false;
        currentState = 0;
        attackTimer = 0f;
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        // 공격 중에는 이동 상태로 덮어쓰지 않는다.
        if (attackTimer > 0f)
        {
            attackTimer -= Time.deltaTime;
            return;
        }

        bool moving = controller != null && controller.IsMoving;
        Play(moving ? RunState : IdleState);
    }

    // 같은 상태를 반복 호출하면 처음부터 다시 재생되므로 변경 시에만 호출한다.
    private void Play(int state)
    {
        if (currentState == state || animator == null)
        {
            return;
        }

        currentState = state;
        animator.Play(state, 0, 0f);
    }

    // 근접 공격 시 컨트롤러가 호출한다.
    public void PlayAttack()
    {
        if (isDead)
        {
            return;
        }

        attackTimer = attackDuration;

        // 연속 공격에서도 매번 처음부터 재생되도록 상태를 초기화한다.
        currentState = 0;
        Play(AttackState);
    }

    // 체력 시스템에서 사망 시 호출한다.
    public void SetDead()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        attackTimer = 0f;
        Play(DeadState);
    }
}