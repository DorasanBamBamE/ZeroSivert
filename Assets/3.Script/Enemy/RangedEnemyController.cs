using UnityEngine;

// 원거리 적 (밴딧). 거리를 유지하며 사격한다.
//
// 원작 대응 스크립트
//   scr_enemy_search_shoot_position   사격 위치 확보
//   scr_enemy_search_line_of_sight    시야 확보 후 사격
//   scr_enemy_change_cover            엄폐물 간 이동
//
// 행동 규칙
//   너무 멀면 접근하고, 너무 가까우면 물러난다.
//   ★ 물러나는 중에도 사격은 계속한다. 이동 판정과 사격 판정을 분리했다.
//   사거리 · 시야 · 사선이 모두 맞아야 쏜다.
//   재장전 중에는 좌우로 움직여 회피한다.
//   시야가 막히면 옆으로 돌아 시야를 튼다.
//   사선에 아군이 있으면 사격을 보류하고 비켜선다.
//
// 아군 사격 자체를 막지는 않는다. 판정이 완벽하지 않으므로 빗나간
// 유탄이 아군에게 맞을 수 있고, 그게 원작의 느낌에 가깝다.
public class RangedEnemyController : EnemyControllerBase
{
    [Header("교전 거리")]
    [SerializeField] private float preferredRangeMax = 7f;
    [SerializeField] private float preferredRangeMin = 4f;
    [SerializeField] private float fireRange = 9f;

    [Header("무장")]
    [SerializeField] private EnemyWeapon weapon;

    // 발각 후 첫 발까지의 지연. 플레이어에게 반응할 시간을 준다.
    [SerializeField] private float aimDelay = 0.45f;

    [Header("사선 판정")]
    // 사선에 아군이 있으면 쏘지 않는다. 끄면 무조건 쏜다.
    [SerializeField] private bool holdFireOnFriendly = true;

    // 사선 검사 굵기. 총알보다 넉넉하게 잡아야 스칠 것도 걸러진다.
    [SerializeField] private float lineWidth = 0.4f;

    // 아군을 검사할 레이어. Enemy 레이어를 지정한다.
    [SerializeField] private LayerMask friendlyMask;

    [Header("회피")]
    [SerializeField] private float strafeSpeedMultiplier = 0.8f;
    [SerializeField] private float strafeFlipInterval = 1.2f;

    // 근접당했을 때 물러나는 속도 배율. 0이면 제자리에서 버티며 쏜다.
    [SerializeField] private float retreatSpeedMultiplier = 0.7f;

    private float aimTimer;
    private float strafeTimer;
    private int strafeDirection = 1;
    private Collider2D selfCollider;

    protected override void Awake()
    {
        base.Awake();

        selfCollider = GetComponent<Collider2D>();

        if (weapon == null)
        {
            weapon = GetComponentInChildren<EnemyWeapon>();
        }

        if (weapon != null)
        {
            weapon.SetOwner(selfCollider);
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        aimTimer = 0f;
        strafeTimer = 0f;
        strafeDirection = Random.value < 0.5f ? -1 : 1;
    }

    protected override void Update()
    {
        base.Update();

        strafeTimer += Time.deltaTime;

        if (strafeTimer >= strafeFlipInterval)
        {
            strafeTimer = 0f;
            strafeDirection = -strafeDirection;
        }
    }

    protected override void OnAlerted()
    {
        // 발각되자마자 쏘지 않고 반응 지연을 준다.
        aimTimer = 0f;
    }

    protected override void TickCombat()
    {
        Vector2 toTarget = (Vector2)target.position - rb.position;
        float distance = toTarget.magnitude;

        FaceDirection(toTarget.x);

        if (weapon != null)
        {
            weapon.Aim(toTarget);
        }

        // 시야가 막혔으면 쏠 수 없다. 옆으로 돌아 시야를 튼다.
        if (!HasLineOfSight(toTarget, distance))
        {
            aimTimer = 0f;
            Strafe(toTarget, moveSpeed);
            return;
        }

        // 재장전 중에는 사격을 못 하므로 회피에 전념한다.
        if (weapon != null && weapon.IsReloading)
        {
            aimTimer = 0f;
            Strafe(toTarget, moveSpeed * strafeSpeedMultiplier);
            return;
        }

        // ── 이동 결정 ────────────────────────────────────
        //
        // ★ 여기서 return하지 않는다. 아래 사격 판정까지 반드시 흘려보낸다.
        //
        // 예전에는 "너무 가깝다" 분기에서 곧바로 return해서, 플레이어가
        // preferredRangeMin 안으로 파고들면 뒷걸음질만 치고 영영 쏘지 못했다.
        // 게다가 aimTimer를 0으로 밀어서 물러난 뒤에도 조준을 처음부터 다시 했다.
        if (distance > preferredRangeMax)
        {
            // 너무 멀다. 접근하면서도 사거리 안이면 쏜다.
            rb.linearVelocity = toTarget.normalized * moveSpeed;
        }
        else if (distance < preferredRangeMin)
        {
            // 너무 가깝다. 물러나되 조준은 유지한다.
            rb.linearVelocity = -toTarget.normalized * moveSpeed * retreatSpeedMultiplier;
        }
        else
        {
            // 적정 거리. 멈춰서 정확하게 쏜다.
            Stop();
        }

        // ── 사격 결정 ────────────────────────────────────

        if (distance > fireRange)
        {
            aimTimer = 0f;
            return;
        }

        // 사선에 아군이 있으면 쏘지 않고 옆으로 비켜선다.
        if (holdFireOnFriendly && IsFriendlyInLine(toTarget, distance))
        {
            aimTimer = 0f;
            Strafe(toTarget, moveSpeed * strafeSpeedMultiplier);
            return;
        }

        aimTimer += Time.fixedDeltaTime;

        if (aimTimer < aimDelay)
        {
            return;
        }

        if (weapon != null)
        {
            weapon.TryFire(toTarget);
        }
    }

    // 총구에서 목표까지의 사선에 다른 적이 끼어 있는지 검사한다.
    // CircleCast를 쓰는 이유는 총알이 스칠 정도도 걸러내기 위해서다.
    private bool IsFriendlyInLine(Vector2 toTarget, float distance)
    {
        if (friendlyMask.value == 0)
        {
            return false;
        }

        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            rb.position, lineWidth, toTarget.normalized, distance, friendlyMask);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i].collider;

            if (hit == null || hit == selfCollider)
            {
                continue;
            }

            // 자기 자신이 아닌 적이 사선에 있으면 보류한다.
            if (hit.CompareTag("Enemy"))
            {
                return true;
            }
        }

        return false;
    }

    // 목표를 바라본 채 좌우로 이동한다.
    private void Strafe(Vector2 toTarget, float speed)
    {
        Vector2 side = new Vector2(-toTarget.y, toTarget.x).normalized;
        rb.linearVelocity = side * strafeDirection * speed;
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Vector3 p = transform.position;
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.5f);
        Gizmos.DrawWireSphere(p, fireRange);
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.5f);
        Gizmos.DrawWireSphere(p, preferredRangeMax);
        Gizmos.DrawWireSphere(p, preferredRangeMin);

        // 사선 표시
        if (Application.isPlaying && target != null)
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
            Gizmos.DrawLine(p, target.position);
        }
    }
}