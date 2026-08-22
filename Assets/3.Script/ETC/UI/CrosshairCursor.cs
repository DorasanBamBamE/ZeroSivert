using UnityEngine;
using UnityEngine.UI;

// 기본 마우스 커서를 숨기고 십자선을 그린다.
//
// 십자선은 마우스가 아니라 WeaponRecoil의 조준점(AimPoint)을 따라간다.
// 그래서 마우스를 휙 돌리면 십자선이 한 박자 늦게 따라붙고, 연사하면 반경 안을 떠돈다.
// 벌어짐도 반경(RadiusPixels)을 그대로 쓰므로 십자선이 곧 실제 명중 범위다.
//
// 하이어라키 구성 — 이 스크립트는 Crosshair 본체에 붙인다.
//   Crosshair          RectTransform만, 앵커·피벗 Center, 크기 0×0
//   ├ Arm_Up           crosshair_arm, Rotation Z 0
//   ├ Arm_Down         crosshair_arm, Rotation Z 180
//   ├ Arm_Left         crosshair_arm, Rotation Z 90
//   ├ Arm_Right        crosshair_arm, Rotation Z 270
//   └ Dot              crosshair_dot
// 모든 조각은 앵커·피벗 Center + Set Native Size. Canvas의 마지막 자식일 것.
public class CrosshairCursor : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private WeaponRecoil recoil;


    [SerializeField] private PDAController pda;

    [Header("십자선 조각")]
    [SerializeField] private RectTransform armUp;
    [SerializeField] private RectTransform armDown;
    [SerializeField] private RectTransform armLeft;
    [SerializeField] private RectTransform armRight;

    [Header("벌어짐")]
    // 반경 위에 더해지는 여유 간격(픽셀). 팔이 조준점에 달라붙지 않게 한다.
    [SerializeField] private float gapPadding = 4f;

    // 반경이 급변할 때 십자선이 튀지 않도록 하는 감쇠. 클수록 빠릿하다.
    [SerializeField] private float gapSmoothing = 18f;

    private RectTransform rect;
    private RectTransform parentRect;
    private Camera worldCamera;
    private Camera uiCamera;
    private Image[] parts;

    private bool systemCursorVisible = true;
    private bool partsVisible = true;
    private float gap;
    private int lastGapPixels = int.MinValue;

    // 메뉴가 열려 있으면 시스템 커서를 되살려 버튼을 누를 수 있게 한다.
    private bool IsMenuOpen
    {
        get
        {
            return pda != null && pda.IsOpen;
        }
    }
    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        parentRect = rect.parent as RectTransform;
        worldCamera = Camera.main;

        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        // Overlay 캔버스는 카메라를 null로 넘겨야 좌표 변환이 맞는다.
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = canvas.worldCamera;
        }

        if (recoil == null)
        {
            recoil = FindFirstObjectByType<WeaponRecoil>();
        }

        // 십자선이 UI 클릭을 가로채지 않도록 한다.
        parts = GetComponentsInChildren<Image>(true);

        for (int i = 0; i < parts.Length; i++)
        {
            parts[i].raycastTarget = false;
        }

        gap = gapPadding;
        ApplyGap(Mathf.RoundToInt(gap));
    }

    private void OnEnable()
    {
        SetSystemCursor(false);
    }

    private void OnDisable()
    {
        SetSystemCursor(true);
    }

    // timeScale이 0이어도 Update 계열은 계속 돌기 때문에 메뉴가 열려도 동작한다.
    private void LateUpdate()
    {
        bool useSystemCursor = IsMenuOpen;

        SetSystemCursor(useSystemCursor);
        SetPartsVisible(!useSystemCursor);

        if (useSystemCursor)
        {
            return;
        }

        Follow();
        UpdateGap();
    }

    private void Follow()
    {
        if (parentRect == null || worldCamera == null || recoil == null)
        {
            return;
        }

        // 조준점(월드) → 화면 → 캔버스 로컬 순으로 변환한다.
        Vector3 screen = worldCamera.WorldToScreenPoint(recoil.AimPoint);
        Vector2 local;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, screen, uiCamera, out local))
        {
            return;
        }

        // 조각이 모두 홀수 크기이므로 중심을 픽셀 "한가운데"에 놓아야 격자에 맞는다.
        local.x = Mathf.Floor(local.x) + 0.5f;
        local.y = Mathf.Floor(local.y) + 0.5f;

        rect.anchoredPosition = local;
    }

    private void UpdateGap()
    {
        // 게임 픽셀과 캔버스 픽셀이 1:1이므로 반경을 그대로 쓸 수 있다.
        float target = (recoil != null ? recoil.RadiusPixels : 0f) + gapPadding;

        // 프레임레이트에 흔들리지 않는 감쇠.
        float t = 1f - Mathf.Exp(-gapSmoothing * Time.unscaledDeltaTime);
        gap = Mathf.Lerp(gap, target, t);

        int pixels = Mathf.RoundToInt(gap);

        // 정수 픽셀 단위로만 움직이므로 값이 바뀔 때만 반영한다.
        if (pixels == lastGapPixels)
        {
            return;
        }

        lastGapPixels = pixels;
        ApplyGap(pixels);
    }

    private void ApplyGap(int pixels)
    {
        SetArm(armUp, new Vector2(0f, pixels));
        SetArm(armDown, new Vector2(0f, -pixels));
        SetArm(armLeft, new Vector2(-pixels, 0f));
        SetArm(armRight, new Vector2(pixels, 0f));
    }

    private static void SetArm(RectTransform arm, Vector2 position)
    {
        if (arm != null)
        {
            arm.anchoredPosition = position;
        }
    }

    private void SetPartsVisible(bool visible)
    {
        if (partsVisible == visible || parts == null)
        {
            return;
        }

        partsVisible = visible;

        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] != null)
            {
                parts[i].enabled = visible;
            }
        }
    }

    // 매 프레임 Cursor.visible을 건드리면 플랫폼에 따라 깜빡이므로 변경 시에만 호출한다.
    private void SetSystemCursor(bool visible)
    {
        if (systemCursorVisible == visible)
        {
            return;
        }

        systemCursorVisible = visible;
        Cursor.visible = visible;
    }
}