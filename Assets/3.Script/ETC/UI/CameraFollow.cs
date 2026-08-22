using UnityEngine;

// 플레이어 추적 카메라 + 사격 반동 셰이크.
//
// 원작 scr_camera_shake는 흔들림 오프셋을 얹었다 되돌리는 방식이 아니라
// obj_camera.x/y 자체를 밀어버린다(obj_camera.x += xx). 복귀는 추적 로직이 담당한다.
// 그래서 지지직 떠는 게 아니라 "밀렸다가 스르륵 돌아오는" 느낌이 난다.
//
// 이 구조는 추적이 lerp일 때만 성립한다. 즉시 추적이면 다음 프레임에 오프셋이 사라진다.
//
// Pixel Perfect Camera와 같은 오브젝트에 붙인다. 위치 스냅은 그쪽이 처리하므로
// 여기서 픽셀 반올림을 하면 안 된다(이중 스냅이 되어 오히려 떨린다).
public class CameraFollow : MonoBehaviour
{
    private const float PixelsPerUnit = 16f;

    [SerializeField] private Transform target;

    // 클수록 빠르게 따라붙는다. 낮추면 셰이크 복귀도 함께 느려진다.
    [SerializeField] private float followSpeed = 7f;

    [SerializeField] private Vector2 offset = Vector2.zero;

    [Header("맵 경계")]
    // 맵 밖의 빈 하늘이 보이지 않도록 카메라를 존 안에 가둔다.
    // 비워두면 씬에서 ZoneGenerator를 찾는다. 없으면(허브) 제한하지 않는다.
    [SerializeField] private bool clampToZone = true;
    [SerializeField] private ZoneGenerator zone;

    // 경계에서 안쪽으로 더 밀어 넣을 여유(유닛). 청크 가장자리가 비칠 때 올린다.
    [SerializeField] private float boundsPadding = 0f;

    private Camera cam;

    [Header("셰이크 상한 (게임 픽셀)")]
    // 연사 시 카메라가 무한정 밀려나지 않도록 목표 지점에서의 최대 이탈을 제한한다.
    [SerializeField] private float maxDisplacement = 12f;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        if (clampToZone && zone == null)
        {
            zone = FindFirstObjectByType<ZoneGenerator>();
        }

        if (target == null)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();

            if (player != null)
            {
                target = player.transform;
            }
        }

        if (target != null)
        {
            Vector3 start = target.position + (Vector3)offset;
            start.z = transform.position.z;
            transform.position = new Vector3(Clamp(start).x, Clamp(start).y, start.z);
        }
    }

    // 플레이어 이동이 FixedUpdate에서 일어나므로 LateUpdate에서 따라간다.
    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector2 goal = (Vector2)target.position + offset;
        Vector2 now = transform.position;

        // 프레임레이트에 흔들리지 않는 감쇠.
        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        Vector2 next = Vector2.Lerp(now, goal, t);

        next = Clamp(next);
        transform.position = new Vector3(next.x, next.y, transform.position.z);
    }

    // 화면 절반만큼 안쪽으로 가둔다. 맵이 화면보다 작으면 가운데에 고정한다.
    private Vector2 Clamp(Vector2 p)
    {
        if (!clampToZone || zone == null)
        {
            return p;
        }

        if (cam == null)
        {
            cam = GetComponent<Camera>();
        }

        if (cam == null || !cam.orthographic)
        {
            return p;
        }

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        Vector2 origin = zone.transform.position;
        Vector2 size = zone.GetMapSize();

        float minX = origin.x + halfW + boundsPadding;
        float maxX = origin.x + size.x - halfW - boundsPadding;
        float minY = origin.y + halfH + boundsPadding;
        float maxY = origin.y + size.y - halfH - boundsPadding;

        p.x = (minX > maxX) ? (origin.x + size.x * 0.5f) : Mathf.Clamp(p.x, minX, maxX);
        p.y = (minY > maxY) ? (origin.y + size.y * 0.5f) : Mathf.Clamp(p.y, minY, maxY);

        return p;
    }

    // 원작 scr_camera_shake(amount, direction) 대응.
    // amount는 게임 픽셀, angle은 도(°). 카메라를 그 반대 방향으로 밀어낸다.
    public void Shake(float amountPixels, float angleDegrees)
    {
        if (target == null || amountPixels <= 0f)
        {
            return;
        }

        float rad = angleDegrees * Mathf.Deg2Rad;
        Vector2 push = -new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * (amountPixels / PixelsPerUnit);

        Vector2 goal = (Vector2)target.position + offset;
        Vector2 moved = (Vector2)transform.position + push;

        // 목표 지점에서 너무 멀어지지 않도록 제한한다.
        Vector2 fromGoal = moved - goal;
        float limit = maxDisplacement / PixelsPerUnit;

        if (fromGoal.magnitude > limit)
        {
            moved = goal + fromGoal.normalized * limit;
        }

        transform.position = new Vector3(moved.x, moved.y, transform.position.z);
    }
}