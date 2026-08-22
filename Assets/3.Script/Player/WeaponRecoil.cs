using UnityEngine;

// 원작 플레이어 반동 모델. data.win 분석 결과 반동 원인이 3가지로 분리되어 있다.
//
//   recoil_from_shooting     사격 시 누적, 시간이 지나면 감쇠
//   recoil_from_movement     이동 자세(정지/걷기/달리기)에 따른 상시 가산
//   recoil_from_moving_mouse 마우스를 빨리 돌릴수록 가산 — "조준을 다시 잡는" 감각
//
// 세 값을 더한 것이 현재 반경이고, 조준점은 그 반경 안을 계속 떠돈다(class_npcr_recoil 구조).
// 실제 조준 지점 = 중심 + 오프셋. 총알은 랜덤 콘 없이 이 지점으로 나가므로 보이는 곳에 맞는다.
//
// 원작 도움말: "You can mitigate your weapons recoil by pulling back in the opposite direction."
// → 반동은 조준을 밀어내고 플레이어가 마우스로 되잡는 구조다.
//
// 이 스크립트는 플레이어에 붙인다. 모든 거리 단위는 게임 픽셀(1유닛 = 16픽셀).
[RequireComponent(typeof(PlayerController))]
public class WeaponRecoil : MonoBehaviour
{
    public enum Stance
    {
        Idle,
        Walk,
        Run,
    }

    [Header("반경 기본값 (게임 픽셀)")]
    // 모든 반동이 0일 때의 반경. 원작 조준점 스프라이트가 3×9라 이 정도가 적당하다.
    [SerializeField] private float radiusBase = 3f;
    [SerializeField] private float radiusMax = 26f;

    [Header("사격 반동")]
    [SerializeField] private float shootPerShot = 3.5f;
    [SerializeField] private float shootMax = 14f;
    [SerializeField] private float shootRecovery = 13f;

    [Header("이동 반동")]
    [SerializeField] private float walkRecoil = 4f;
    [SerializeField] private float runRecoil = 11f;

    // 자세가 바뀔 때 값이 튀지 않도록 하는 감쇠.
    [SerializeField] private float movementBlendSpeed = 6f;

    [Header("마우스 반동")]
    // 마우스 각속도(초당 도)에 이 값을 곱해 가산한다.
    [SerializeField] private float mousePerDegree = 0.05f;
    [SerializeField] private float mouseMax = 9f;
    [SerializeField] private float mouseRecovery = 16f;

    [Header("조준점 떠돌기 (초당 픽셀)")]
    [SerializeField] private float crossSpeed = 14f;
    [SerializeField] private float crossSpeedMax = 70f;

    // 중심이 마우스를 따라오는 속도. 낮출수록 조준이 늦게 붙는다.
    [SerializeField] private float crossDelay = 14f;

    // 경계에 갇히는 것을 막는 장치. 원작 off_center_counter / ForceMiddle.
    [SerializeField] private int offCenterCounterMax = 3;
    [SerializeField] private float offsetCenter = 5f;

    private const float PixelsPerUnit = 16f;

    private Camera cam;
    private PlayerController controller;

    private Vector2 center;
    private Vector2 offset;
    private float moveDirection;

    private float shootRecoil;
    private float movementRecoil;
    private float mouseRecoil;
    private float lastMouseAngle;
    private bool hasLastAngle;

    private int offCenterCounter;

    // 조준점의 월드 좌표. 무기와 크로스헤어가 함께 읽는다.
    public Vector2 AimPoint
    {
        get { return center + offset / PixelsPerUnit; }
    }

    // 현재 반경(게임 픽셀). 크로스헤어가 벌어지는 정도로 그대로 쓴다.
    public float RadiusPixels
    {
        get
        {
            float total = radiusBase + shootRecoil + movementRecoil + mouseRecoil;
            return Mathf.Min(total, radiusMax);
        }
    }

    public Stance CurrentStance
    {
        get
        {
            if (controller == null || !controller.IsMoving)
            {
                return Stance.Idle;
            }

            return controller.IsSprinting ? Stance.Run : Stance.Walk;
        }
    }

    private void Awake()
    {
        cam = Camera.main;
        controller = GetComponent<PlayerController>();

        center = transform.position;
        moveDirection = Random.Range(0f, 360f);
    }

    private void Update()
    {
        UpdateCenter();
        UpdateMovementRecoil();
        UpdateShootRecoil();
        MoveCrosshair();
    }

    // 원작 NewCenter — 중심이 마우스를 뒤늦게 따라간다.
    // 마우스 각속도를 함께 재서 마우스 반동에 반영한다.
    private void UpdateCenter()
    {
        if (cam == null)
        {
            return;
        }

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);

        UpdateMouseRecoil(mouseWorld);

        float t = 1f - Mathf.Exp(-crossDelay * Time.deltaTime);
        center = Vector2.Lerp(center, mouseWorld, t);
    }

    // 플레이어 기준 마우스 방향이 얼마나 빨리 도는지를 잰다.
    // 화면 이동량이 아니라 각도를 쓰는 이유는, 멀리 있는 마우스를 조금 움직여도
    // 조준 방향은 거의 안 바뀌기 때문이다.
    private void UpdateMouseRecoil(Vector2 mouseWorld)
    {
        Vector2 toMouse = mouseWorld - (Vector2)transform.position;

        mouseRecoil = Mathf.Max(0f, mouseRecoil - mouseRecovery * Time.deltaTime);

        if (toMouse.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float angle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg;

        if (!hasLastAngle)
        {
            lastMouseAngle = angle;
            hasLastAngle = true;
            return;
        }

        float delta = Mathf.Abs(Mathf.DeltaAngle(lastMouseAngle, angle));
        lastMouseAngle = angle;

        if (Time.deltaTime > 0f)
        {
            float degreesPerSecond = delta / Time.deltaTime;
            mouseRecoil += degreesPerSecond * mousePerDegree * Time.deltaTime;
            mouseRecoil = Mathf.Min(mouseRecoil, mouseMax);
        }
    }

    private void UpdateMovementRecoil()
    {
        float target;

        switch (CurrentStance)
        {
            case Stance.Run:
                target = runRecoil;
                break;
            case Stance.Walk:
                target = walkRecoil;
                break;
            default:
                target = 0f;
                break;
        }

        float t = 1f - Mathf.Exp(-movementBlendSpeed * Time.deltaTime);
        movementRecoil = Mathf.Lerp(movementRecoil, target, t);
    }

    private void UpdateShootRecoil()
    {
        shootRecoil = Mathf.Max(0f, shootRecoil - shootRecovery * Time.deltaTime);
    }

    // 원작 MoveCrosshair — 반경이 클수록 빠르게 떠돌고, 경계에 닿으면 튕긴다.
    private void MoveCrosshair()
    {
        float radius = RadiusPixels;
        float range = radiusMax - radiusBase;
        float ratio = range > 0.01f ? (radius - radiusBase) / range : 0f;
        float speed = Mathf.Lerp(crossSpeed, crossSpeedMax, Mathf.Clamp01(ratio));

        float rad = moveDirection * Mathf.Deg2Rad;
        offset += new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * speed * Time.deltaTime;

        if (offset.magnitude >= radius)
        {
            // 정반대가 아니라 15~30도 비틀어 튕겨야 같은 궤도를 반복하지 않는다.
            float turn = (Random.value < 0.5f ? -1f : 1f) * Random.Range(15f, 30f);
            moveDirection += 180f + turn;

            offCenterCounter++;

            if (offCenterCounter >= offCenterCounterMax)
            {
                offCenterCounter = 0;
                ClampInside(radius);
            }
        }

        // 중심 근처를 지나갔으면 갇힘 카운터를 푼다.
        if (offset.magnitude < offsetCenter)
        {
            offCenterCounter = 0;
        }
    }

    // 원작 ForceMiddle — 이름과 달리 경계 바로 안쪽으로 끌어당기는 갇힘 방지 장치다.
    // 반경이 급히 줄어 오프셋이 밖에 남았을 때도 이걸로 회수한다.
    private void ClampInside(float radius)
    {
        if (offset.sqrMagnitude < 0.0001f)
        {
            return;
        }

        offset = offset.normalized * Mathf.Max(0f, radius - 1f);
    }

    // 발사 시 무기가 호출한다.
    // multiplier는 총기 숙련도에서 오는 반동 감소 배율이다.
    // perShot과 max는 WeaponData에서 오고, 숙련도 배율이 이미 곱해진 값이다.
    public void AddShot(float perShot, float max)
    {
        shootRecoil = Mathf.Min(max, shootRecoil + perShot);
    }
    public void ResetRecoil()
    {
        shootRecoil = 0f;
        mouseRecoil = 0f;
        offset = Vector2.zero;
        offCenterCounter = 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, RadiusPixels / PixelsPerUnit);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(AimPoint, 0.1f);
    }
}