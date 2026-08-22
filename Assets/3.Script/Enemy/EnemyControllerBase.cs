using UnityEngine;

// 적 AI 공통 베이스. 감지 · 경계 · 순찰 · 수색을 담당한다.
//
// 원작 감지 구조를 따른다.
//   alert_min_distance      코앞이면 시야 무관 즉시 감지
//   alert_radius            소리 감지 — 전방향, 벽 통과
//   alert_visual_distance   시야 거리
//   alert_angle             시야각 (scr_enemy_target_inside_cone)
//   alert_player_max_value  경계 게이지 — 즉시 발각이 아니다
//   scr_enemy_alert_others  발각 시 주변 동료에게 전파
//
// 경계 상태에서의 실제 전투 행동은 TickCombat()에서 파생 클래스가 구현한다.
//   MeleeEnemyController   접근 후 근접 공격 (좀비 · 늑대)
//   RangedEnemyController  거리 유지 + 사격 (밴딧)
//
// Rigidbody2D는 Dynamic / Gravity Scale 0 / Freeze Rotation Z 체크 필요.
[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyControllerBase : MonoBehaviour
{
    protected enum State
    {
        Idle,
        Patrol,
        Alert,
        Search,
    }

    [SerializeField] protected SpriteRenderer bodyRenderer;
    [SerializeField] protected float moveSpeed = 2.2f;

    [Header("감지")]
    // 이 거리 안이면 시야와 무관하게 즉시 감지한다.
    [SerializeField] private float alertMinDistance = 1.5f;

    // 소리 감지 반경. 전방향이며 벽을 통과한다.
    [SerializeField] private float alertRadius = 4f;

    [SerializeField] private float visualDistance = 9f;
    [SerializeField] private float visualAngle = 100f;

    // 시야를 가로막는 레이어. Obstacle 레이어를 지정할 것.
    [SerializeField] protected LayerMask sightBlockers;

    [Header("경계 게이지")]
    [SerializeField] private float alertBuildSpeed = 2.5f;
    [SerializeField] private float alertDecaySpeed = 0.8f;
    [SerializeField] private float searchDuration = 4f;
    [SerializeField] private float alertOthersRadius = 6f;

    [Header("순찰")]
    [SerializeField] private bool patrolEnabled = true;
    [SerializeField] private float patrolRadius = 4f;
    [SerializeField] private float patrolSpeedMultiplier = 0.45f;
    [SerializeField] private float patrolWaitMin = 1.5f;
    [SerializeField] private float patrolWaitMax = 4f;

    protected Rigidbody2D rb;
    protected Transform target;
    protected PlayerStats targetStats;

    protected State state = State.Idle;
    protected float stateTimer;

    private float alertValue;
    private Vector2 homePosition;
    private Vector2 patrolPoint;
    private Vector2 lastKnownPosition;

    // 애니메이터에서 Run / Idle 판단에 사용한다.
    public bool IsMoving
    {
        get { return rb != null && rb.linearVelocity.sqrMagnitude > 0.01f; }
    }

    public bool IsAlerted
    {
        get { return state == State.Alert; }
    }

    public float AlertRatio
    {
        get { return Mathf.Clamp01(alertValue); }
    }

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    protected virtual void OnEnable()
    {
        alertValue = 0f;
        state = State.Idle;
        stateTimer = 0f;
        homePosition = transform.position;
        patrolPoint = homePosition;
        FindTarget();
    }

    private void FindTarget()
    {
        targetStats = FindFirstObjectByType<PlayerStats>();

        if (targetStats != null)
        {
            target = targetStats.transform;
        }
    }

    protected virtual void Update()
    {
        stateTimer += Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (target == null || targetStats == null || targetStats.IsDead)
        {
            Stop();
            return;
        }

        UpdateAlert();

        switch (state)
        {
            case State.Alert:
                TickCombat();
                break;
            case State.Search:
                TickSearch();
                break;
            default:
                TickPatrol();
                break;
        }
    }

    // ── 감지 ──────────────────────────────────────────────

    private void UpdateAlert()
    {
        if (CanDetectTarget())
        {
            alertValue += alertBuildSpeed * Time.fixedDeltaTime;
            lastKnownPosition = target.position;

            if (alertValue >= 1f)
            {
                alertValue = 1f;

                if (state != State.Alert)
                {
                    state = State.Alert;
                    stateTimer = 0f;
                    AlertOthers();
                    OnAlerted();
                }
            }

            return;
        }

        alertValue = Mathf.Max(0f, alertValue - alertDecaySpeed * Time.fixedDeltaTime);

        // 완전히 놓쳤으면 마지막 목격 지점을 뒤진다.
        if (state == State.Alert)
        {
            state = State.Search;
            stateTimer = 0f;
        }
    }

    private bool CanDetectTarget()
    {
        Vector2 toTarget = (Vector2)target.position - rb.position;
        float distance = toTarget.magnitude;

        if (distance <= alertMinDistance)
        {
            return true;
        }

        if (distance <= alertRadius)
        {
            return true;
        }

        if (distance > visualDistance)
        {
            return false;
        }

        if (!IsInsideCone(toTarget))
        {
            return false;
        }

        return HasLineOfSight(toTarget, distance);
    }

    // 원작 scr_enemy_target_inside_cone 대응.
    private bool IsInsideCone(Vector2 toTarget)
    {
        return Vector2.Angle(GetFacing(), toTarget) <= visualAngle * 0.5f;
    }

    protected bool HasLineOfSight(Vector2 direction, float distance)
    {
        if (sightBlockers.value == 0)
        {
            return true;
        }

        RaycastHit2D hit = Physics2D.Raycast(rb.position, direction.normalized, distance, sightBlockers);
        return hit.collider == null;
    }

    // 스프라이트가 좌우 반전만 하므로 바라보는 방향도 좌우로만 잡는다.
    protected Vector2 GetFacing()
    {
        if (bodyRenderer != null)
        {
            return bodyRenderer.flipX ? Vector2.left : Vector2.right;
        }

        return Vector2.right;
    }

    // 원작 scr_enemy_alert_others 대응.
    private void AlertOthers()
    {
        if (alertOthersRadius <= 0f)
        {
            return;
        }

        Collider2D[] found = Physics2D.OverlapCircleAll(rb.position, alertOthersRadius);

        for (int i = 0; i < found.Length; i++)
        {
            EnemyControllerBase other = found[i].GetComponent<EnemyControllerBase>();

            if (other != null && other != this)
            {
                other.ForceAlert(lastKnownPosition);
            }
        }
    }

    // 동료의 호출 또는 피격으로 강제 경계 상태에 들어간다.
    public void ForceAlert(Vector2 position)
    {
        lastKnownPosition = position;
        alertValue = 1f;

        if (state != State.Alert)
        {
            state = State.Alert;
            stateTimer = 0f;
            OnAlerted();
        }
    }

    // 경계 진입 시 한 번 호출된다. 포효 소리 등에 사용한다.
    protected virtual void OnAlerted()
    {
    }

    // ── 경계 상태의 전투 행동 ────────────────────────────────

    protected abstract void TickCombat();

    // ── 공통 행동 ─────────────────────────────────────────

    private void TickSearch()
    {
        if (stateTimer >= searchDuration)
        {
            state = State.Idle;
            stateTimer = 0f;
            patrolPoint = homePosition;
            Stop();
            return;
        }

        MoveToward(lastKnownPosition, moveSpeed * 0.7f, 0.3f);
    }

    // 원작 scr_enemy_choose_idle_or_move / _choose_move_pos 대응.
    private void TickPatrol()
    {
        if (!patrolEnabled)
        {
            Stop();
            return;
        }

        if (state == State.Idle)
        {
            Stop();

            if (stateTimer >= Random.Range(patrolWaitMin, patrolWaitMax))
            {
                patrolPoint = homePosition + Random.insideUnitCircle * patrolRadius;
                state = State.Patrol;
                stateTimer = 0f;
            }

            return;
        }

        bool arrived = MoveToward(patrolPoint, moveSpeed * patrolSpeedMultiplier, 0.25f);

        if (arrived || stateTimer > 6f)
        {
            state = State.Idle;
            stateTimer = 0f;
        }
    }

    // 목적지에 도착했으면 true.
    protected bool MoveToward(Vector2 destination, float speed, float tolerance)
    {
        Vector2 delta = destination - rb.position;

        if (delta.magnitude <= tolerance)
        {
            Stop();
            return true;
        }

        FaceDirection(delta.x);
        rb.linearVelocity = delta.normalized * speed;
        return false;
    }

    public void Stop()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    protected void FaceDirection(float dirX)
    {
        if (bodyRenderer == null || Mathf.Abs(dirX) < 0.01f)
        {
            return;
        }

        bodyRenderer.flipX = dirX < 0f;
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Vector3 p = transform.position;

        Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
        Gizmos.DrawWireSphere(p, alertRadius);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.7f);
        Gizmos.DrawWireSphere(p, alertMinDistance);

        Vector2 facing = Application.isPlaying ? GetFacing() : Vector2.right;
        float half = visualAngle * 0.5f;
        Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
        Gizmos.DrawRay(p, Quaternion.Euler(0f, 0f, half) * facing * visualDistance);
        Gizmos.DrawRay(p, Quaternion.Euler(0f, 0f, -half) * facing * visualDistance);

        if (patrolEnabled)
        {
            Vector3 home = Application.isPlaying ? (Vector3)homePosition : p;
            Gizmos.color = new Color(0.4f, 0.8f, 0.4f, 0.4f);
            Gizmos.DrawWireSphere(home, patrolRadius);
        }
    }


public void ConfigureGroupPatrol(Vector2 groupCenter, float radius)
    {
        homePosition = groupCenter;
        patrolPoint = groupCenter;
        patrolRadius = Mathf.Max(1f, radius);

        if (state != State.Alert && state != State.Search)
        {
            state = State.Idle;
            stateTimer = 0f;
        }
    }
}