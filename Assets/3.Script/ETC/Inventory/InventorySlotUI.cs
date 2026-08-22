using UnityEngine;
using UnityEngine.UI;

// 그리드 셀 한 칸의 시각 표현. 배경과 하이라이트만 담당한다.
// 아이템 아이콘은 여기 들어오지 않는다 — 다중 칸 아이템은 셀 위에 따로 뜬다.
//
// 프리팹 구성: RectTransform + Image (이 스크립트) 하나면 끝.
[RequireComponent(typeof(Image))]
public class InventorySlotUI : MonoBehaviour
{
    public enum State
    {
        Normal,
        Valid,     // 여기에 놓을 수 있다
        Invalid,   // 놓을 수 없다
    }

    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.06f);
    [SerializeField] private Color validColor = new Color(0.1f, 0.8f, 0.35f, 0.45f);
    [SerializeField] private Color invalidColor = new Color(0.85f, 0.25f, 0.25f, 0.45f);

    private Image image;
    private State state = State.Normal;

    public int CellX { get; private set; }
    public int CellY { get; private set; }

    private void Awake()
    {
        image = GetComponent<Image>();

        // 하이라이트는 색만 바꾼다. 레이캐스트는 받지 않는다 —
        // 켜두면 드래그 중 셀이 포인터를 먹어서 판정이 흔들린다.
        image.raycastTarget = false;
        Apply();
    }

    public void SetCell(int x, int y)
    {
        CellX = x;
        CellY = y;
        name = "Cell_" + x + "_" + y;
    }

    public void SetState(State s)
    {
        if (state == s)
        {
            return;
        }

        state = s;
        Apply();
    }

    private void Apply()
    {
        if (image == null)
        {
            return;
        }

        switch (state)
        {
            case State.Valid:
                image.color = validColor;
                break;

            case State.Invalid:
                image.color = invalidColor;
                break;

            default:
                image.color = normalColor;
                break;
        }
    }
}