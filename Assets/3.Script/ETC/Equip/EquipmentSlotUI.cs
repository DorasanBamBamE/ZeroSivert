using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 장비 슬롯 한 칸의 시각 표현. 인벤토리 그리드와 별개로 동작한다.
//
// ── 오브젝트 구성 ────────────────────────────────
// Slot_Weapon1        Image(배경, Raycast Target 켬) + 이 스크립트
//   └ Icon            Image (Raycast Target 끔, Preserve Aspect 켬)
//
// 조작
//   인벤토리에서 아이템을 드래그해 이 위에 놓으면 장착된다
//   우클릭하면 인벤토리로 되돌아간다 (자리가 없으면 아무 일도 안 일어난다)
public class EquipmentSlotUI : MonoBehaviour, IPointerClickHandler
{
    [Header("이 칸이 받는 장비")]
    [SerializeField] private EquipSlot slot = EquipSlot.Weapon1;

    [Header("참조")]
    [SerializeField] private Image background;
    [SerializeField] private Image iconImage;

    [Header("색")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.12f);
    [SerializeField] private Color validColor = new Color(0.1f, 0.8f, 0.35f, 0.45f);
    [SerializeField] private Color invalidColor = new Color(0.85f, 0.25f, 0.25f, 0.45f);

    // 활성 무기 슬롯을 밝게 표시한다.
    [SerializeField] private Color activeColor = new Color(1f, 1f, 1f, 0.3f);

    [Header("아이콘 크기")]
    // 아이콘 사방 여백(px).
    [SerializeField] private float padding = 4f;

    // 인벤토리 격자와 같은 셀 크기. 아이콘 원본 크기를 계산하는 데 쓴다.
    [SerializeField] private float cellSize = 16f;

    // 켜면 1배·2배·3배처럼 정수 배율로만 키운다. 픽셀이 선명하지만
    // 슬롯 크기가 딱 맞지 않으면 아이콘이 작게 남아 여백이 생긴다.
    //
    // 끄면 슬롯을 비율 유지한 채로 꽉 채운다. 레이아웃을 자유롭게 잡을 수 있는 대신
    // 1.4배 같은 배율에서 픽셀 크기가 조금 들쭉날쭉해진다.
    [SerializeField] private bool integerScale = false;

    private EquipmentController equipment;
    private RectTransform rect;

    public EquipSlot Slot { get { return slot; } }

    private void Awake()
    {
        rect = GetComponent<RectTransform>();

        if (background == null)
        {
            background = GetComponent<Image>();
        }

        if (iconImage != null)
        {
            iconImage.raycastTarget = false;

            // 크기를 코드가 직접 잡으므로 preserveAspect는 끈다.
            iconImage.preserveAspect = false;

            // 아이콘은 슬롯 한가운데에 원본 비율 그대로 놓인다.
            RectTransform ir = iconImage.rectTransform;
            ir.anchorMin = new Vector2(0.5f, 0.5f);
            ir.anchorMax = new Vector2(0.5f, 0.5f);
            ir.pivot = new Vector2(0.5f, 0.5f);
            ir.anchoredPosition = Vector2.zero;
        }
    }

    private void OnEnable()
    {
        Bind();
        Refresh();
    }

    private void OnDisable()
    {
        if (equipment != null)
        {
            equipment.Changed -= Refresh;
        }
    }

    private void Bind()
    {
        // 플레이어는 씬마다 새로 만들어지므로 런타임에 찾는다.
        // 파괴된 컨트롤러는 유니티가 == null로 잡아주므로 이 조건이 재탐색도 겸한다.
        if (equipment == null)
        {
            equipment = FindFirstObjectByType<EquipmentController>();
        }

        if (equipment == null)
        {
            return;
        }

        // ★ 반드시 매번 다시 구독한다.
        //
        // 예전에는 equipment가 이미 있으면 곧바로 return했다. 그런데 OnDisable에서
        // 구독을 끊어놓기 때문에, 창을 한 번 닫았다 열면 다시 구독되지 않았다.
        // 그 상태에서 장착·해제를 하면 슬롯이 갱신되지 않아 아이콘이 사라진 것처럼
        // 보이고, 창을 닫았다 열어야(OnEnable의 Refresh) 다시 나타났다.
        //
        // -=를 먼저 부르는 이유는 중복 구독을 막기 위해서다.
        // 구독하지 않은 델리게이트를 빼는 것은 C#에서 안전하다.
        equipment.Changed -= Refresh;
        equipment.Changed += Refresh;
    }

    public void Refresh()
    {
        Bind();

        ItemData item = equipment != null ? equipment.Get(slot) : null;

        if (iconImage != null)
        {
            iconImage.sprite = item != null ? item.icon : null;
            iconImage.enabled = item != null && item.icon != null;

            if (iconImage.enabled)
            {
                ResizeIcon(item);
            }
        }

        ApplyIdleColor();
    }

    // 아이콘을 슬롯 크기에 맞춰 키운다. 가로세로 비율은 항상 유지된다.
    private void ResizeIcon(ItemData item)
    {
        if (rect == null)
        {
            rect = GetComponent<RectTransform>();
        }

        float nativeW = item.gridWidth * cellSize;
        float nativeH = item.gridHeight * cellSize;

        if (nativeW <= 0f || nativeH <= 0f)
        {
            return;
        }

        float availW = rect.rect.width - padding * 2f;
        float availH = rect.rect.height - padding * 2f;

        // 패널이 아직 레이아웃 전이면 rect가 0이다. 그때는 원본 크기로 둔다.
        if (availW <= 0f || availH <= 0f)
        {
            iconImage.rectTransform.sizeDelta = new Vector2(nativeW, nativeH);
            return;
        }

        float scale = Mathf.Min(availW / nativeW, availH / nativeH);

        if (integerScale)
        {
            // 픽셀아트를 선명하게 유지한다. 대신 슬롯이 딱 맞지 않으면 여백이 남는다.
            scale = Mathf.Max(1f, Mathf.Floor(scale));
        }
        else
        {
            // 슬롯을 꽉 채운다. 아이콘이 슬롯보다 크면 줄이기도 한다.
            scale = Mathf.Max(0.1f, scale);
        }

        iconImage.rectTransform.sizeDelta = new Vector2(nativeW * scale, nativeH * scale);
    }

    private void ApplyIdleColor()
    {
        if (background == null)
        {
            return;
        }

        bool isActiveWeapon =
            equipment != null
            && (slot == EquipSlot.Weapon1 || slot == EquipSlot.Weapon2)
            && (int)slot == equipment.ActiveWeapon;

        background.color = isActiveWeapon ? activeColor : normalColor;
    }

    // 드래그 중인 아이템이 이 칸 위에 있을 때 InventoryUI가 부른다.
    public void SetDropHighlight(bool valid)
    {
        if (background != null)
        {
            background.color = valid ? validColor : invalidColor;
        }
    }

    public void ClearHighlight()
    {
        ApplyIdleColor();
    }

    // 우클릭 → 인벤토리로 돌려보낸다.
    public void OnPointerClick(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Right)
        {
            return;
        }

        Bind();

        if (equipment == null)
        {
            return;
        }

        equipment.UnequipToInventory(slot);
    }
}