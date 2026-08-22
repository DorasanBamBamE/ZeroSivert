using UnityEngine;

// 탑다운 플레이어 이동 및 조준 처리.
// 본체는 회전하지 않고 GunPivot만 커서 방향으로 360도 회전한다.
// Rigidbody2D는 Dynamic / Gravity Scale 0 / Freeze Rotation Z 체크 필요.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float sprintSpeed = 5.5f;
    [SerializeField] private Transform gunPivot;
    [SerializeField] private SpriteRenderer bodyRenderer;

    private Rigidbody2D rb;
    private Camera cam;
    private SpriteRenderer gunRenderer;
    private PlayerStats stats;
    private Vector2 moveInput;
    private bool isSprinting;
    private float aimAngle;

    // 스태미나 시스템에서 달리기 여부를 읽어갈 때 사용한다.
    public bool IsSprinting
    {
        get { return isSprinting && moveInput.sqrMagnitude > 0.01f; }
    }

    // 이동 입력이 있는지 여부.
    public bool IsMoving
    {
        get { return moveInput.sqrMagnitude > 0.01f; }
    }

    // 사격 시 총알 진행 방향으로 사용한다.
    public float AimAngle
    {
        get { return aimAngle; }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        cam = Camera.main;
        stats = GetComponent<PlayerStats>();

        if (gunPivot == null)
        {
            gunPivot = transform.Find("GunPivot");
        }

        if (gunPivot != null)
        {
            gunRenderer = gunPivot.GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void Update()
    {
        // 07번 추가 — 인벤토리(Tab)나 PDA(J)가 열려 있으면 이동·조준을 멈춘다.
        //
        // Time.timeScale이 0이면 FixedUpdate는 멈추지만 Update와 Input은 계속 돈다.
        // 이 가드가 없으면 창을 열어놓고 마우스를 움직일 때마다
        // 창 뒤에서 총과 몸통이 계속 회전한다.
        //
        // timeScale까지 같이 보는 이유 — PDAController 참조를 새로 끌어오지 않아도
        // 시간이 멈추는 모든 창(PDA, 씬 전환 페이드 등)이 한 번에 처리된다.
        if (UIBlocker.Any || Time.timeScale == 0f)
        {
            // IsMoving / IsSprinting을 읽는 PlayerAnimator와 PlayerStats 때문에
            // 입력을 비워둔다. 안 그러면 창을 연 채로 걷는 애니메이션이 남는다.
            moveInput = Vector2.zero;
            isSprinting = false;
            return;
        }

        ReadInput();
        Aim();
    }

    private void FixedUpdate()
    {
        bool canSprint = stats == null || stats.CanSprint;
        float speed = (isSprinting && canSprint) ? sprintSpeed : moveSpeed;

        if (stats != null)
        {
            speed *= stats.SpeedMultiplier;
        }

        rb.linearVelocity = moveInput * speed;
    }

    private void ReadInput()
    {
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");

        // 대각선 이동이 빨라지지 않도록 정규화한다.
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }

        isSprinting = Input.GetKey(KeyCode.LeftShift);
    }

    private void Aim()
    {
        if (cam == null || gunPivot == null)
        {
            return;
        }

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 dir = (Vector2)mouseWorld - (Vector2)gunPivot.position;

        if (dir.sqrMagnitude < 0.0001f)
        {
            return;
        }

        aimAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        gunPivot.rotation = Quaternion.Euler(0f, 0f, aimAngle);

        bool facingLeft = (aimAngle > 90f || aimAngle < -90f);

        // 왼쪽을 조준하면 총 스프라이트가 뒤집히므로 Y축으로 반전시킨다.
        Vector3 scale = gunPivot.localScale;
        scale.y = facingLeft ? -1f : 1f;
        gunPivot.localScale = scale;

        // 몸통 스프라이트도 같은 기준으로 좌우 반전한다.
        if (bodyRenderer != null)
        {
            bodyRenderer.flipX = facingLeft;
        }

        UpdateGunSorting();
    }

    // 위쪽을 조준할 때는 총이 몸 뒤로 가려지게 한다.
    private void UpdateGunSorting()
    {
        if (gunRenderer == null || bodyRenderer == null)
        {
            return;
        }

        bool aimingUp = aimAngle > 0f && aimAngle < 180f;
        gunRenderer.sortingOrder = aimingUp ? bodyRenderer.sortingOrder - 1 : bodyRenderer.sortingOrder + 1;
    }
}