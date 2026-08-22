using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 아이템 하나의 아이콘. 드래그·회전·분할·우클릭 사용을 담당한다.
//
// ── 프리팹 구성 ────────────────────────────────
// ItemView                RectTransform + Image(배경, raycastTarget 켜기) + CanvasGroup + 이 스크립트
//   ├ Icon                Image  ← iconImage   (anchor·pivot 모두 중앙 0.5, 0.5)
//   └ Text_Count          Text (레거시 UI) ← countText
//
// Icon의 anchor와 pivot을 반드시 (0.5, 0.5)로 둘 것.
// RectTransform을 90도 돌려도 sizeDelta는 바뀌지 않기 때문에,
// 루트는 크기만 바꾸고 자식 아이콘만 회전시킨다. 중앙 피벗이라야 정확히 들어맞는다.
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class ItemView : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;

    // 레거시 UI Text를 쓴다 (TextMeshPro 아님).
    [SerializeField] private Text countText;

    // 10번 — 거래 중에만 켜지는 가격표. 없어도 된다.
    [SerializeField] private Text priceText;

    private RectTransform rect;
    private CanvasGroup group;

    private InventorySlotData slot;
    private InventoryUI ui;
    private InventoryController controller;

    // 드래그 상태
    private bool dragging;
    private bool detached;              // 분할해서 떼어낸 임시 슬롯인가
    private InventorySlotData splitSource;
    private Transform originalParent;
    private Vector2 grabOffset;
    private bool pendingRotated;
    private bool quickSlotHovered;

    // 지금 포인터가 올라가 있는 장비 슬롯. 없으면 null.
    private EquipmentSlotUI hoverEquip;

    // 지금 포인터가 올라가 있는 그리드. 자기 그리드일 수도, 반대편(GROUND)일 수도 있다.
    private InventoryUI hoverUI;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        group = GetComponent<CanvasGroup>();
    }

    // 안전망 — 드래그 도중 이 뷰가 꺼지거나 파괴되면 OnEndDrag가 안 불린다.
    // 그 상태로 두면 아직 slots에 안 들어간 분할 조각이 사라지므로 여기서 회수한다.
    private void OnDisable()
    {
        if (!dragging)
        {
            return;
        }

        dragging = false;

        if (hoverEquip != null)
        {
            hoverEquip.ClearHighlight();
            hoverEquip = null;
        }

        if (hoverUI != null)
        {
            hoverUI.ClearHighlight();
            hoverUI = null;
        }

        if (controller != null)
        {
            controller.RecoverPendingSplit();
        }
    }

    public void Bind(InventorySlotData s, InventoryUI owner, InventoryController ctrl)
    {
        slot = s;
        ui = owner;
        controller = ctrl;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);

        Refresh();
    }

    private void Refresh()
    {
        if (slot == null || slot.item == null || ui == null)
        {
            return;
        }

        rect.sizeDelta = ui.SizeForCells(slot.Width, slot.Height);
        rect.anchoredPosition = ui.CellToAnchored(slot.x, slot.y);

        ApplyIcon(slot.rotated);

        if (countText != null)
        {
            bool show = slot.count > 1;
            countText.gameObject.SetActive(show);

            if (show)
            {
                countText.text = slot.count.ToString();
            }
        }

        RefreshPrice();
    }

    // 거래 중이 아니면 스스로 꺼진다. 평소 인벤토리에는 아무 영향이 없다.
    private void RefreshPrice()
    {
        if (priceText == null)
        {
            return;
        }

        int price = ShopSession.PriceFor(controller, slot);
        bool show = price > 0;

        priceText.gameObject.SetActive(show);

        if (show)
        {
            priceText.text = price.ToString();
        }
    }

    // 아이콘은 원본 크기를 유지한 채 자식만 돌린다.
    private void ApplyIcon(bool rotated)
    {
        if (iconImage == null || slot == null || slot.item == null)
        {
            return;
        }

        iconImage.sprite = slot.item.icon;
        iconImage.enabled = slot.item.icon != null;

        RectTransform ir = iconImage.rectTransform;
        ir.anchorMin = new Vector2(0.5f, 0.5f);
        ir.anchorMax = new Vector2(0.5f, 0.5f);
        ir.pivot = new Vector2(0.5f, 0.5f);
        ir.anchoredPosition = Vector2.zero;

        // 아이콘은 원본 픽셀 크기 그대로 둔다 (칸당 16px, 간격은 빼고).
        // SizeForCells를 쓰면 간격만큼 늘어나서 1:1 배율이 깨지고 픽셀아트가 뭉갠다.
        // 예) 4x2 소총 아이콘 64x32 → SizeForCells는 67x33이 되어 4.7% 늘어난다.
        ir.sizeDelta = new Vector2(
            slot.item.gridWidth * ui.CellSize,
            slot.item.gridHeight * ui.CellSize);

        ir.localEulerAngles = new Vector3(0f, 0f, rotated ? 90f : 0f);
    }

    // ───────────────── 드래그 ─────────────────

    public void OnBeginDrag(PointerEventData e)
    {
        if (slot == null || slot.item == null || controller == null || ui == null)
        {
            return;
        }

        if (e.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        detached = false;
        splitSource = null;

        // ★ 순서 주의 — 반드시 SplitHalf보다 먼저 잠근다.
        //
        // SplitHalf는 Changed 이벤트를 쏜다. 잠그기 전에 쏘면 InventoryUI가
        // 곧바로 Rebuild를 돌려서 드래그를 막 시작한 이 뷰까지 Destroy해버린다.
        // 그러면 OnEndDrag가 영영 안 불리고, slots에 아직 안 들어간
        // 분할 조각이 통째로 사라진다.
        ui.BeginInteraction();

        // CTRL을 누른 채 시작하면 절반을 떼어낸다. 드래그 중에 눌러도 무시한다.
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        if (ctrl && controller.EnableSplit && slot.count >= 2)
        {
            InventorySlotData temp = controller.SplitHalf(slot);

            if (temp != null)
            {
                splitSource = slot;
                slot = temp;
                detached = true;
            }
        }

        dragging = true;
        pendingRotated = slot.rotated;

        originalParent = transform.parent;

        // 최상단으로 올린다.
        if (ui.DragLayer != null)
        {
            transform.SetParent(ui.DragLayer, true);
        }

        transform.SetAsLastSibling();

        // 이걸 안 끄면 자기 자신이 레이캐스트를 먹어서 드롭 판정이 흔들린다.
        group.blocksRaycasts = false;
        group.alpha = 0.85f;

        Refresh();
        CenterOnPointer();
        UpdateHighlight();
    }

    public void OnDrag(PointerEventData e)
    {
        if (!dragging)
        {
            return;
        }

        MoveTo(e);

        // 장비 슬롯 위에 있으면 그리드 대신 그쪽을 하이라이트한다.
        EquipmentSlotUI eq = FindEquipSlot(e);

        if (eq != hoverEquip)
        {
            if (hoverEquip != null)
            {
                hoverEquip.ClearHighlight();
            }

            hoverEquip = eq;
        }

        if (hoverEquip != null)
        {
            ui.ClearHighlight();

            if (hoverUI != null)
            {
                hoverUI.ClearHighlight();
                hoverUI = null;
            }

            bool can = !detached
                       && slot != null
                       && EquipmentController.Accepts(hoverEquip.Slot, slot.item);

            hoverEquip.SetDropHighlight(can);
            return;
        }

        // 어느 그리드 위인가. 자기 그리드일 수도, 반대편 GROUND일 수도 있다.
        InventoryUI overUI = FindInventoryUI(e);

        if (overUI != hoverUI)
        {
            if (hoverUI != null)
            {
                hoverUI.ClearHighlight();
            }

            hoverUI = overUI;
        }

        UpdateHighlight();
    }

    // 드래그 중인 이 뷰는 blocksRaycasts가 꺼져 있으므로,
    // 포인터 아래에서 잡히는 건 그 밑에 깔린 UI다.
    private EquipmentSlotUI FindEquipSlot(PointerEventData e)
    {
        if (e == null || e.pointerCurrentRaycast.gameObject == null)
        {
            return null;
        }

        return e.pointerCurrentRaycast.gameObject.GetComponentInParent<EquipmentSlotUI>();
    }

    // 같은 방식으로 그리드를 찾는다.
    // InventoryUI가 Panel_Left / Panel_Right에 하나씩 붙어 있어야 이게 갈린다.
    // 둘 다 Root에 있으면 어느 쪽 위에 있든 같은 UI가 잡혀서 크로스 이동이 안 된다.
    private InventoryUI FindInventoryUI(PointerEventData e)
    {
        if (e == null || e.pointerCurrentRaycast.gameObject == null)
        {
            return null;
        }

        return e.pointerCurrentRaycast.gameObject.GetComponentInParent<InventoryUI>();
    }

    private void Update()
    {
        HandleQuickSlotAssignment();

        if (!dragging || controller == null || !controller.EnableRotation)
        {
            return;
        }

        if (slot == null || slot.item == null)
        {
            return;
        }

        if (slot.item.gridWidth == slot.item.gridHeight)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            pendingRotated = !pendingRotated;

            int w = pendingRotated ? slot.item.gridHeight : slot.item.gridWidth;
            int h = pendingRotated ? slot.item.gridWidth : slot.item.gridHeight;

            rect.sizeDelta = ui.SizeForCells(w, h);
            ApplyIcon(pendingRotated);
            CenterOnPointer();
            UpdateHighlight();
        }
    }

    private void HandleQuickSlotAssignment()
    {
        if (!InventoryScreen.IsOpen || dragging || !quickSlotHovered || slot == null || slot.item == null || !slot.item.IsConsumable)
        {
            return;
        }

        int index = -1;
        if (Input.GetKeyDown(KeyCode.Alpha3)) index = 0;
        else if (Input.GetKeyDown(KeyCode.Alpha4)) index = 1;
        else if (Input.GetKeyDown(KeyCode.Alpha5)) index = 2;
        else if (Input.GetKeyDown(KeyCode.Alpha6)) index = 3;

        if (index < 0)
        {
            return;
        }

        QuickSlotBar bar = QuickSlotBar.Current;
        if (bar == null)
        {
            bar = FindFirstObjectByType<QuickSlotBar>(FindObjectsInactive.Include);
        }

        if (bar != null)
        {
            bar.Assign(index, controller, slot.item);
        }
    }


    // 포인터가 아이템 한가운데를 잡고 있도록 오프셋을 다시 잡는다.
    private void CenterOnPointer()
    {
        grabOffset = new Vector2(-rect.sizeDelta.x * 0.5f, rect.sizeDelta.y * 0.5f);

        Vector2 p;
        RectTransform layer = ui.DragLayer != null ? ui.DragLayer : (RectTransform)transform.parent;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                layer, Input.mousePosition, ui.UICamera, out p))
        {
            rect.localPosition = p + grabOffset;
        }
    }

    private void MoveTo(PointerEventData e)
    {
        RectTransform layer = ui.DragLayer != null ? ui.DragLayer : (RectTransform)transform.parent;

        Vector2 p;

        // localPosition은 부모의 pivot 기준이고 이 변환도 pivot 기준이라 서로 맞는다.
        // anchoredPosition을 쓰면 부모 pivot이 (0,1)이 아닐 때 어긋난다.
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                layer, e.position, e.pressEventCamera, out p))
        {
            rect.localPosition = p + grabOffset;
        }
    }

    private Vector2Int CurrentCell()
    {
        // rect의 pivot이 (0,1)이라 rect.position이 곧 왼쪽 위 모서리의 월드 좌표다.
        return ui.WorldTopLeftToCell(rect.position);
    }

    private void UpdateHighlight()
    {
        if (slot == null || slot.item == null)
        {
            return;
        }

        // 어느 그리드도 안 짚고 있으면 아무것도 칠하지 않는다.
        if (hoverUI == null)
        {
            ui.ClearHighlight();
            return;
        }

        InventoryUI targetUI = hoverUI;

        // 반대편 그리드로 넘어갔으면 자기 그리드 하이라이트를 지운다.
        if (targetUI != ui)
        {
            ui.ClearHighlight();
        }

        InventoryController targetCtrl = targetUI.Controller;

        if (targetCtrl == null)
        {
            return;
        }

        Vector2Int c = targetUI.WorldTopLeftToCell(rect.position);

        int w = pendingRotated ? slot.item.gridHeight : slot.item.gridWidth;
        int h = pendingRotated ? slot.item.gridWidth : slot.item.gridHeight;

        // 자기 그리드 안에서 옮길 때만 자기 자신을 무시한다.
        // 반대편 그리드에서는 이 슬롯이 애초에 없으므로 무시할 대상도 없다.
        bool sameGrid = targetUI == ui;
        InventorySlotData ignore = (sameGrid && !detached) ? slot : null;

        bool ok = targetCtrl.CanPlace(c.x, c.y, w, h, ignore);

        // 스택 합치기가 가능한 자리도 초록으로 보여준다.
        if (!ok)
        {
            InventorySlotData onto = targetCtrl.GetSlotAt(c.x, c.y, ignore);

            if (onto != null && onto.item == slot.item && slot.item.stackable && !onto.IsFull)
            {
                ok = true;
            }
        }

        targetUI.ShowHighlight(c.x, c.y, w, h, ok);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!dragging)
        {
            return;
        }

        dragging = false;
        group.blocksRaycasts = true;
        group.alpha = 1f;

        EquipmentSlotUI eq = hoverEquip;
        InventoryUI targetUI = hoverUI;

        if (hoverEquip != null)
        {
            hoverEquip.ClearHighlight();
            hoverEquip = null;
        }

        if (hoverUI != null)
        {
            hoverUI.ClearHighlight();
            hoverUI = null;
        }

        bool ok = false;

        // 1) 장비 슬롯에 떨어뜨렸나
        if (eq != null && !detached)
        {
            ok = controller.TryEquipFrom(slot, eq.Slot);
        }

        // 2) 그리드에 떨어뜨렸나 — 자기 그리드일 수도, 반대편 GROUND일 수도 있다
        if (!ok && targetUI != null && targetUI.Controller != null)
        {
            Vector2Int c = targetUI.WorldTopLeftToCell(rect.position);

            if (targetUI == ui)
            {
                ok = controller.TryDrop(slot, c.x, c.y, pendingRotated, detached);
            }
            else
            {
                ok = controller.TryTransferTo(
                    slot, targetUI.Controller, c.x, c.y, pendingRotated, detached);
            }
        }

        // 3) 어디에도 못 놓았다. 떼어낸 조각이면 원본에 되돌린다.
        if (!ok && detached)
        {
            controller.CancelSplit(slot, splitSource);
        }

        // 원래 부모로 되돌려 놓는다. 어차피 바로 아래에서 전부 다시 그린다.
        if (originalParent != null)
        {
            transform.SetParent(originalParent, false);
        }

        detached = false;
        splitSource = null;

        // 여기서 이 오브젝트는 파괴된다. 이 줄 뒤에 아무것도 두지 말 것.
        ui.EndInteraction();
    }

    // ───────────────── 우클릭 사용 ─────────────────

    public void OnPointerClick(PointerEventData e)
    {
        if (dragging || slot == null || slot.item == null || controller == null)
        {
            return;
        }

        if (e.button != PointerEventData.InputButton.Right)
        {
            return;
        }

        if (!slot.item.IsConsumable)
        {
            return;
        }

        controller.Use(slot);
    }


    public void OnPointerEnter(PointerEventData eventData)
    {
        quickSlotHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        quickSlotHovered = false;
    }
}
