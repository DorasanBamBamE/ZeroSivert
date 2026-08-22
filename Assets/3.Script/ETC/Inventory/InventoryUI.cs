using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 6×4 그리드를 그리고 좌표를 변환한다. PDA 안의 인벤토리 탭 패널에 붙인다.
//
// ── 씬 구성 (이대로 만들 것) ──────────────────────────
// Panel_Inventory            ← 이 스크립트
//   ├ GridRoot     (RectTransform, 빈 오브젝트)   ← gridRoot
//   ├ ItemLayer    (RectTransform, 빈 오브젝트)   ← itemLayer
//   ├ DragLayer    (RectTransform, 빈 오브젝트)   ← dragLayer  (마지막 자식이어야 맨 앞에 그려진다)
//   └ Text_Weight  (Text, 레거시 UI)              ← weightText
//
// GridRoot / ItemLayer / DragLayer 는 크기·앵커를 신경 쓸 필요 없다.
// Awake에서 왼쪽 위 기준으로 코드가 직접 맞춰준다.
//
// ItemLayer와 DragLayer에는 Layout Group을 절대 붙이지 말 것.
// Layout Group이 아이템 크기를 강제로 되돌린다.
public class InventoryUI : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private InventoryController controller;

    // 플레이어 인벤토리는 켠다. GROUND 패널은 반드시 꺼둘 것.
    // 켜두면 GROUND가 플레이어 인벤토리를 잡아버려서 좌우가 같은 내용이 된다.
    [SerializeField] private bool autoFindController = true;
    [SerializeField] private RectTransform gridRoot;
    [SerializeField] private RectTransform itemLayer;
    [SerializeField] private RectTransform dragLayer;

    // 레거시 UI Text를 쓴다 (TextMeshPro 아님).
    [SerializeField] private Text weightText;

    // 08 루팅 — 패널 머리글. 원작은 우측 패널 제목이 GROUND ↔ 컨테이너 이름으로 바뀐다.
    [SerializeField] private Text titleText;

    // 아무것도 안 열었을 때 되돌릴 기본 제목. GROUND 패널이면 "GROUND".
    [SerializeField] private string defaultTitle = "";

    [Header("프리팹")]
    [SerializeField] private InventorySlotUI cellPrefab;
    [SerializeField] private ItemView itemPrefab;

    [Header("셀 규격 (Canvas Scaler Reference PPU 16 기준)")]
    [SerializeField] private float cellSize = 16f;
    [SerializeField] private float spacing = 1f;

    private readonly List<InventorySlotUI> cells = new List<InventorySlotUI>();
    private readonly List<ItemView> views = new List<ItemView>();
    private Canvas canvas;
    private bool suppressRebuild;
    private bool built;

    public InventoryController Controller { get { return controller; } }
    public RectTransform DragLayer { get { return dragLayer; } }
    public float Step { get { return cellSize + spacing; } }
    public float CellSize { get { return cellSize; } }

    // Screen Space - Overlay면 null을 넘겨야 좌표 변환이 맞는다.
    public Camera UICamera
    {
        get
        {
            if (canvas == null)
            {
                return null;
            }

            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }
    }

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();

        // autoFindController를 반드시 함께 본다.
        // 이 조건을 빼면 GROUND 패널이 Awake에서 플레이어 인벤토리를 잡아버려
        // 좌우가 같은 내용이 된다. OnEnable에만 조건을 걸어서는 막을 수 없다.
        if (controller == null && autoFindController)
        {
            controller = FindPlayerController();
        }

        // 왼쪽 위를 원점으로 고정한다. 인스펙터에서 어떻게 설정돼 있든 여기서 덮어쓴다.
        AnchorTopLeft(gridRoot);
        AnchorTopLeft(itemLayer);

        if (dragLayer != null)
        {
            AnchorTopLeft(dragLayer);
            dragLayer.SetAsLastSibling();
        }

        BuildCells();
    }

    private void OnEnable()
    {
        // 플레이어가 나중에 스폰되는 씬이면 Awake 시점에 못 찾을 수 있다.
        if (controller == null && autoFindController)
        {
            controller = FindPlayerController();
        }

        BuildCells();

        if (controller != null)
        {
            // -=를 먼저 부르는 이유는 중복 구독을 막기 위해서다.
            // 구독하지 않은 델리게이트를 빼는 것은 안전하다.
            controller.Changed -= OnInventoryChanged;
            controller.Changed += OnInventoryChanged;
        }

        Rebuild();
    }

    private void OnDisable()
    {
        if (controller != null)
        {
            controller.Changed -= OnInventoryChanged;
        }

        // 창을 닫는 순간 드래그가 끊길 수 있다.
        // 드롭이 확정되지 않은 분할 조각을 반드시 회수하고 끝낸다.
        suppressRebuild = false;

        if (controller != null)
        {
            controller.RecoverPendingSplit();
        }

        ClearHighlight();
    }

    // 08 루팅 — GROUND 패널이 무엇을 보여줄지 갈아끼운다.
    // 상자를 열면 그 컨테이너로, 닫으면 지면으로 바뀐다.
    //
    // 격자 크기가 서로 다를 수 있으므로 셀을 통째로 다시 만든다.
    public void SetController(InventoryController next)
    {
        if (controller != null)
        {
            controller.Changed -= OnInventoryChanged;
        }

        controller = next;

        if (controller != null)
        {
            controller.Changed -= OnInventoryChanged;
            controller.Changed += OnInventoryChanged;
        }

        RebuildCells();
        Rebuild();
    }

    // 08 루팅 — 패널 제목을 갈아끼운다. 원작 우측 패널이 GROUND ↔ 상자 이름으로 바뀌는 것.
    // 빈 문자열을 넘기면 defaultTitle로 되돌린다.
    public void SetTitle(string t)
    {
        if (titleText == null)
        {
            return;
        }

        titleText.text = string.IsNullOrEmpty(t) ? defaultTitle : t;
    }

    private void RebuildCells()
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] != null)
            {
                Destroy(cells[i].gameObject);
            }
        }

        cells.Clear();
        built = false;
        BuildCells();
    }

    // 08 루팅 이후로 씬에는 InventoryController가 여러 개 존재한다.
    // 플레이어 / 지면(GroundContainer) / 열어본 상자의 Contents가 전부 이 컴포넌트를 쓴다.
    //
    // 그래서 FindFirstObjectByType<InventoryController>()를 그냥 쓰면 안 된다.
    // 순서가 보장되지 않아 왼쪽 INVENTORY 패널이 지면이나 상자를 잡아버린다.
    // PlayerStats가 붙은 오브젝트의 것만 플레이어 인벤토리다.
    private InventoryController FindPlayerController()
    {
        PlayerStats stats = FindFirstObjectByType<PlayerStats>();

        if (stats == null)
        {
            return null;
        }

        return stats.GetComponent<InventoryController>();
    }

    private static void AnchorTopLeft(RectTransform rt)
    {
        if (rt == null)
        {
            return;
        }

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
    }

    // ───────────────── 셀 ─────────────────

    private void BuildCells()
    {
        if (built || controller == null || gridRoot == null || cellPrefab == null)
        {
            return;
        }

        built = true;

        int w = controller.GridWidth;
        int h = controller.GridHeight;

        gridRoot.sizeDelta = new Vector2(
            w * cellSize + (w - 1) * spacing,
            h * cellSize + (h - 1) * spacing);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                InventorySlotUI cell = Instantiate(cellPrefab, gridRoot);
                RectTransform rt = cell.GetComponent<RectTransform>();
                AnchorTopLeft(rt);
                rt.sizeDelta = new Vector2(cellSize, cellSize);
                rt.anchoredPosition = CellToAnchored(x, y);

                cell.SetCell(x, y);
                cells.Add(cell);
            }
        }

        // ItemLayer를 GridRoot의 자식으로 뒀다면 셀들보다 뒤로 보내야 한다.
        // 셀은 런타임에 추가되므로 그냥 두면 셀이 아이템을 덮는다.
        if (itemLayer != null && itemLayer.parent == gridRoot)
        {
            itemLayer.SetAsLastSibling();
        }
    }

    public Vector2 CellToAnchored(int x, int y)
    {
        return new Vector2(x * Step, -y * Step);
    }

    public Vector2 SizeForCells(int w, int h)
    {
        return new Vector2(
            w * cellSize + (w - 1) * spacing,
            h * cellSize + (h - 1) * spacing);
    }

    // 아이템 사각형의 왼쪽 위 모서리(월드 좌표)가 어느 셀에 해당하는지.
    // gridRoot의 pivot이 (0,1)이라 로컬 좌표의 원점이 곧 (0,0) 셀의 왼쪽 위다.
    public Vector2Int WorldTopLeftToCell(Vector3 worldTopLeft)
    {
        Vector3 local = gridRoot.InverseTransformPoint(worldTopLeft);
        int cx = Mathf.RoundToInt(local.x / Step);
        int cy = Mathf.RoundToInt(-local.y / Step);
        return new Vector2Int(cx, cy);
    }

    // ───────────────── 하이라이트 ─────────────────

    public void ClearHighlight()
    {
        for (int i = 0; i < cells.Count; i++)
        {
            cells[i].SetState(InventorySlotUI.State.Normal);
        }
    }

    public void ShowHighlight(int x, int y, int w, int h, bool valid)
    {
        ClearHighlight();

        InventorySlotUI.State s = valid ? InventorySlotUI.State.Valid : InventorySlotUI.State.Invalid;

        for (int dx = 0; dx < w; dx++)
        {
            for (int dy = 0; dy < h; dy++)
            {
                InventorySlotUI cell = GetCell(x + dx, y + dy);

                if (cell != null)
                {
                    cell.SetState(s);
                }
            }
        }
    }

    private InventorySlotUI GetCell(int x, int y)
    {
        if (controller == null || x < 0 || y < 0 || x >= controller.GridWidth || y >= controller.GridHeight)
        {
            return null;
        }

        return cells[y * controller.GridWidth + x];
    }

    // ───────────────── 다시 그리기 ─────────────────

    private void OnInventoryChanged()
    {
        if (suppressRebuild)
        {
            return;
        }

        Rebuild();
    }

    // 드래그가 시작되면 다시 그리기를 잠근다.
    // 안 잠그면 드롭 도중 Changed가 터져서 드래그 중인 뷰가 파괴된다.
    public void BeginInteraction()
    {
        suppressRebuild = true;
    }

    public void EndInteraction()
    {
        suppressRebuild = false;

        // 드롭이 확정되지 않은 분할 조각이 남아 있으면 여기서 되돌린다.
        if (controller != null)
        {
            controller.RecoverPendingSplit();
        }

        ClearHighlight();
        Rebuild();
    }

    public void Rebuild()
    {
        if (controller == null || itemLayer == null || itemPrefab == null)
        {
            return;
        }

        for (int i = 0; i < views.Count; i++)
        {
            if (views[i] != null)
            {
                Destroy(views[i].gameObject);
            }
        }

        views.Clear();

        IReadOnlyList<InventorySlotData> slots = controller.Slots;

        for (int i = 0; i < slots.Count; i++)
        {
            ItemView view = Instantiate(itemPrefab, itemLayer);
            view.Bind(slots[i], this, controller);
            views.Add(view);
        }

        UpdateWeightText();
    }

    private void UpdateWeightText()
    {
        if (weightText == null || controller == null)
        {
            return;
        }

        weightText.text = controller.CurrentWeight.ToString("0.0")
                          + " / " + controller.Capacity.ToString("0.0") + " kg";

        // 허기 등급이 나빠지면 상한이 내려가서 초과 상태가 될 수 있다.
        weightText.color = controller.IsOverweight
            ? new Color(0.85f, 0.25f, 0.25f)
            : Color.white;
    }
}
