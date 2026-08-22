using UnityEngine;

// 플레이어 애니메이션 상태 전환.
// Animator의 상태 이름은 아래 해시와 정확히 일치해야 한다.
// Animator 컨트롤러에는 트랜지션을 만들지 않고 코드에서 직접 재생한다.
public class PlayerAnimator : MonoBehaviour
{
    private static readonly int IdleState = Animator.StringToHash("PlayerIdle");
    private static readonly int WalkState = Animator.StringToHash("PlayerWalk");
    private static readonly int RunState = Animator.StringToHash("PlayerRun");
    private static readonly int DeadState = Animator.StringToHash("PlayerDead");

    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController controller;

    private int currentState;
    private bool isDead;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (controller == null)
        {
            controller = GetComponentInParent<PlayerController>();
        }
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        Play(GetTargetState());
    }

    private int GetTargetState()
    {
        if (controller == null || !controller.IsMoving)
        {
            return IdleState;
        }

        if (controller.IsSprinting)
        {
            return RunState;
        }

        return WalkState;
    }

    // 같은 상태를 반복 호출하면 애니메이션이 처음부터 다시 재생되므로 변경 시에만 호출한다.
    private void Play(int state)
    {
        if (currentState == state)
        {
            return;
        }

        currentState = state;
        animator.Play(state, 0, 0f);
    }

    // 체력 시스템에서 사망 시 호출한다.
    public void SetDead()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        Play(DeadState);
    }
}