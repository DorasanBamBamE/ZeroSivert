using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// PDA 탭 아이콘 하나의 색 상태를 관리한다.
//
//   사용 불가 (미구현·잠긴 지역)   회색     ← interactable 해제
//   사용 가능, 선택 안 됨          흰색
//   선택됨                        노란색
//   마우스 올림                    노란색   ← 누르면 어떻게 되는지 미리 보여준다
//
// 탭 7개 전부에 붙인다. 미구현 탭은 interactable만 해제하면 회색으로 고정되고
// 클릭·호버에 반응하지 않는다.
//
// Button의 Color Tint를 쓰지 않는 이유는 Tint가 Image.color와 곱해져서
// 여기서 지정한 색과 충돌하기 때문이다. Button의 Transition은 None으로 둘 것.
//
// 마우스 이벤트가 오려면 씬에 EventSystem이 있어야 하고
// 이 오브젝트의 Raycast Target이 켜져 있어야 한다.
[RequireComponent(typeof(Image))]
public class PDATabButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image icon;

    // 해제하면 회색 고정. 미구현 탭에 사용한다.
    [SerializeField] private bool interactable = true;

    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color lockedColor = new Color32(90, 90, 90, 255);
    [SerializeField] private Color selectedColor = new Color32(255, 216, 74, 255);

    private bool isSelected;
    private bool isHovered;

    public bool Interactable
    {
        get { return interactable; }
    }

    private void Awake()
    {
        if (icon == null)
        {
            icon = GetComponent<Image>();
        }

        // 잠긴 탭은 클릭도 막는다.
        Button button = GetComponent<Button>();

        if (button != null)
        {
            button.interactable = interactable;
        }

        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    // PDA를 닫는 순간 마우스가 위에 있었다면 호버 상태가 남는다.
    private void OnDisable()
    {
        isHovered = false;
    }

    // PDAController가 탭 전환 시 호출한다.
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        Apply();
    }

    // 지역 해금 등으로 런타임에 사용 가능해질 때 호출한다.
    public void SetInteractable(bool value)
    {
        interactable = value;

        Button button = GetComponent<Button>();

        if (button != null)
        {
            button.interactable = value;
        }

        if (!value)
        {
            isHovered = false;
            isSelected = false;
        }

        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!interactable)
        {
            return;
        }

        isHovered = true;
        Apply();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        Apply();
    }

    private void Apply()
    {
        if (icon == null)
        {
            return;
        }

        if (!interactable)
        {
            icon.color = lockedColor;
            return;
        }

        if (isSelected || isHovered)
        {
            icon.color = selectedColor;
            return;
        }

        icon.color = availableColor;
    }
}