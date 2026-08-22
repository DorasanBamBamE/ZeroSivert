using System.Collections.Generic;
using UnityEngine;

// 인벤토리의 모든 판정과 데이터를 담당한다. UI는 전혀 모른다.
// 플레이어 오브젝트에 붙인다 (PlayerStats와 같은 GameObject).
//
// RunData가 Save/Load 때 stats.GetComponent<InventoryController>()로 찾아가므로
// 반드시 PlayerStats와 같은 오브젝트에 있어야 한다.
public class InventoryController : MonoBehaviour
{
    // 내용이 바뀔 때마다 알린다. InventoryUI가 구독해서 다시 그린다.
    public event System.Action Changed;

    [Header("그리드")]
    [SerializeField] private int gridWidth = 6;
    [SerializeField] private int gridHeight = 4;

    [Header("무게")]
    // 기본 소지 상한(kg). 여기에 PlayerStats.CarryWeightBonus가 더해진다.
    [SerializeField] private float baseCarryWeight = 25f;

    // 11 - 무게 상한을 넘겨 담을 수 있게 한다. 플레이어에게만 적용된다.
    [SerializeField] private bool allowOverweight = true;

    [Header("기능 스위치")]
    // 문제가 생기면 이 둘을 꺼라. 나머지 기능은 그대로 돈다.
    [SerializeField] private bool enableRotation = true;
    [SerializeField] private bool enableSplit = true;

    [Header("테스트용 (선택)")]
    // 인스펙터 우클릭 → "테스트 아이템 채우기"로 넣어볼 아이템들.
    [SerializeField] private ItemData[] testItems;

    // 켜면 분할 드래그의 전 과정을 콘솔에 찍는다. 문제 생겼을 때만 켤 것.
    [SerializeField] private bool debugLog = false;

    private readonly List<InventorySlotData> slots = new List<InventorySlotData>();
    private PlayerStats stats;
    private EquipmentController equipment;

    // 분할해서 떼어낸 조각. 아직 slots에 안 들어간 상태로 드래그 중이다.
    // 드롭이 확정되거나 취소될 때까지 여기 붙들어 둔다.
    // OnEndDrag가 어떤 이유로든 안 불려도 RecoverPendingSplit()이 되돌린다.
    private InventorySlotData pendingSplit;
    private InventorySlotData pendingSource;

    private void Log(string msg)
    {
        if (debugLog)
        {
            Debug.Log("[Inventory] " + msg, this);
        }
    }

    public int GridWidth { get { return gridWidth; } }
    public int GridHeight { get { return gridHeight; } }
    public bool EnableRotation { get { return enableRotation; } }
    public bool EnableSplit { get { return enableSplit; } }

    public IReadOnlyList<InventorySlotData> Slots { get { return slots; } }

    public float CurrentWeight
    {
        get
        {
            // 증분 갱신하지 않는다. 분할·합치기에서 반드시 어긋난다.
            float sum = 0f;

            for (int i = 0; i < slots.Count; i++)
            {
                sum += slots[i].TotalWeight;
            }

            return sum;
        }
    }

    public float Capacity
    {
        get
        {
            // 허기 등급 보너스(−4 ~ +2) + 장착한 배낭의 추가 용량.
            float bonus = stats != null ? stats.CarryWeightBonus : 0f;
            float pack = equipment != null ? equipment.CarryBonus : 0f;
            return Mathf.Max(0f, baseCarryWeight + bonus + pack);
        }
    }

    public float RemainingWeight
    {
        get { return Capacity - CurrentWeight; }
    }

    public bool IsOverweight
    {
        get { return CurrentWeight > Capacity; }
    }

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        equipment = GetComponent<EquipmentController>();

        // 여기서 PlayerStats 없음을 경고하지 않는다.
        // 08부터는 상자·시체·지면도 이 컨트롤러를 그릇으로 쓰는데, 그쪽은
        // PlayerStats가 없는 게 정상이다. 경고는 실제로 문제가 되는 Use()에서 낸다.
    }

    // 컨테이너용 런타임 설정. 08 루팅에서 상자·지면이 부른다.
    public void Configure(int width, int height, float capacity)
    {
        gridWidth = Mathf.Max(1, width);
        gridHeight = Mathf.Max(1, height);
        baseCarryWeight = Mathf.Max(0f, capacity);
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        if (Changed != null)
        {
            Changed();
        }
    }

    // 08 루팅 통계 — 상자·시체·지면에서 플레이어 인벤토리로 넘어온 것만 센다.
    //
    // 방향 판정은 PlayerStats 유무로 한다. 컨테이너 쪽 컨트롤러는 PlayerStats가 없고
    // 플레이어 것만 갖고 있으므로, 이 조합이 곧 "주웠다"는 뜻이다.
    // 반대 방향(버리기)이나 컨테이너끼리의 이동은 세지 않는다.
    private void ReportLooted(InventoryController target, int count)
    {
        if (count <= 0 || target == null)
        {
            return;
        }

        if (stats != null || target.stats == null)
        {
            return;
        }

        if (GameStats.Instance != null)
        {
            GameStats.Instance.Add(GameStats.StatId.ItemsLooted, count);
        }
    }

    // ───────────────── 점유 판정 ─────────────────

    // 점유 맵은 저장하지 않고 매번 새로 만든다. 24칸이라 비용은 무시해도 된다.
    private bool[,] BuildOccupancy(InventorySlotData ignore)
    {
        bool[,] map = new bool[gridWidth, gridHeight];

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlotData s = slots[i];

            if (s == ignore)
            {
                continue;
            }

            for (int dx = 0; dx < s.Width; dx++)
            {
                for (int dy = 0; dy < s.Height; dy++)
                {
                    int cx = s.x + dx;
                    int cy = s.y + dy;

                    if (cx >= 0 && cx < gridWidth && cy >= 0 && cy < gridHeight)
                    {
                        map[cx, cy] = true;
                    }
                }
            }
        }

        return map;
    }

    // (x, y)에 w×h 크기를 놓을 수 있는가.
    // ignore에는 반드시 자기 자신을 넘길 것. 안 넘기면 제자리 재배치조차 실패한다.
    public bool CanPlace(int x, int y, int w, int h, InventorySlotData ignore)
    {
        if (x < 0 || y < 0 || x + w > gridWidth || y + h > gridHeight)
        {
            return false;
        }

        bool[,] map = BuildOccupancy(ignore);

        for (int dx = 0; dx < w; dx++)
        {
            for (int dy = 0; dy < h; dy++)
            {
                if (map[x + dx, y + dy])
                {
                    return false;
                }
            }
        }

        return true;
    }

    // 드래그 중인 슬롯 기준의 편의 오버로드.
    public bool CanPlace(InventorySlotData slot, int x, int y, bool rotated)
    {
        if (slot == null || slot.item == null)
        {
            return false;
        }

        int w = rotated ? slot.item.gridHeight : slot.item.gridWidth;
        int h = rotated ? slot.item.gridWidth : slot.item.gridHeight;

        return CanPlace(x, y, w, h, slot);
    }

    // (x, y) 칸을 덮고 있는 슬롯을 찾는다. 없으면 null.
    public InventorySlotData GetSlotAt(int x, int y, InventorySlotData ignore)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlotData s = slots[i];

            if (s != ignore && s.Covers(x, y))
            {
                return s;
            }
        }

        return null;
    }

    // 빈 자리를 왼쪽 위부터 훑는다. 회전이 켜져 있으면 돌려서도 시도한다.
    private bool FindFreeSpot(ItemData item, out int fx, out int fy, out bool frot)
    {
        fx = 0;
        fy = 0;
        frot = false;

        if (item == null)
        {
            return false;
        }

        int passes = enableRotation && item.gridWidth != item.gridHeight ? 2 : 1;

        for (int p = 0; p < passes; p++)
        {
            bool rot = p == 1;
            int w = rot ? item.gridHeight : item.gridWidth;
            int h = rot ? item.gridWidth : item.gridHeight;

            for (int y = 0; y <= gridHeight - h; y++)
            {
                for (int x = 0; x <= gridWidth - w; x++)
                {
                    if (CanPlace(x, y, w, h, null))
                    {
                        fx = x;
                        fy = y;
                        frot = rot;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    // ───────────────── 넣기 ─────────────────

    // 아이템을 자동으로 넣는다. 실제로 들어간 개수를 돌려준다.
    // 08 루팅에서 이 메서드를 그대로 쓴다.
    //
    // 순서: 기존 스택에 먼저 채우고 → 남으면 빈 자리에 새 슬롯을 만든다.
    // 무게 상한을 넘기면 넘기 직전까지만 넣는다.
    public int TryAdd(ItemData item, int count = 1)
    {
        if (item == null || count <= 0)
        {
            return 0;
        }

        int added = 0;
        int left = count;

        // 1) 기존 스택 채우기
        if (item.stackable)
        {
            for (int i = 0; i < slots.Count && left > 0; i++)
            {
                InventorySlotData s = slots[i];

                if (s.item != item || s.IsFull)
                {
                    continue;
                }

                int room = item.MaxStackSafe - s.count;
                int move = Mathf.Min(room, left);
                move = LimitByWeight(item, move);

                if (move <= 0)
                {
                    break;
                }

                s.count += move;
                left -= move;
                added += move;
            }
        }

        // 2) 빈 자리에 새 슬롯
        while (left > 0)
        {
            int fx, fy;
            bool frot;

            if (!FindFreeSpot(item, out fx, out fy, out frot))
            {
                break;
            }

            int move = Mathf.Min(item.MaxStackSafe, left);
            move = LimitByWeight(item, move);

            if (move <= 0)
            {
                break;
            }

            InventorySlotData ns = new InventorySlotData();
            ns.item = item;
            ns.count = move;
            ns.x = fx;
            ns.y = fy;
            ns.rotated = frot;
            slots.Add(ns);

            left -= move;
            added += move;
        }

        if (added > 0)
        {
            NotifyChanged();
        }

        return added;
    }

    // 무게 상한에 걸리지 않는 최대 개수로 깎는다.
    private int LimitByWeight(ItemData item, int want)
    {
        if (item.weight <= 0f)
        {
            return want;
        }

        // 11 - 원작은 상한을 넘겨 담을 수 있다. 대신 PlayerStats.SpeedMultiplier가 깎인다.
        //   상자·지면 컨테이너는 그대로 막는다. stats가 있는 쪽이 플레이어다.
        if (allowOverweight && stats != null)
        {
            return want;
        }

        float room = RemainingWeight;

        if (room <= 0f)
        {
            return 0;
        }

        // 0.0001을 더하는 이유 — float 오차 때문이다.
        // 상한 7.0에 6.9를 담으면 room이 0.1이 아니라 0.09999994로 나와서,
        // 무게 0.1짜리가 딱 맞는데도 floor가 0을 뱉는다.
        int fit = Mathf.FloorToInt((room + 0.0001f) / item.weight);
        return Mathf.Clamp(want, 0, Mathf.Min(want, fit));
    }

    // 이 인벤토리가 지금 받아줄 수 있는 최대 개수. 다른 인벤토리에서 옮겨올 때 쓴다.
    public int MaxAddable(ItemData item, int want)
    {
        if (item == null || want <= 0)
        {
            return 0;
        }

        return LimitByWeight(item, want);
    }

    // 이미 만들어진 슬롯을 그대로 꽂는다. 좌표와 회전은 호출자가 정해둘 것.
    // 크로스 인벤토리 이동에서만 쓴다 — 판정은 호출자가 이미 끝냈다고 본다.
    public void AddExisting(InventorySlotData slot)
    {
        if (slot == null || slot.item == null || slots.Contains(slot))
        {
            return;
        }

        slots.Add(slot);
        NotifyChanged();
    }

    // 지정 좌표에 강제로 놓는다. 성공하면 true.
    public bool TryAddAt(ItemData item, int count, int x, int y, bool rotated)
    {
        if (item == null || count <= 0)
        {
            return false;
        }

        int w = rotated ? item.gridHeight : item.gridWidth;
        int h = rotated ? item.gridWidth : item.gridHeight;

        if (!CanPlace(x, y, w, h, null))
        {
            return false;
        }

        if (LimitByWeight(item, count) < count)
        {
            return false;
        }

        InventorySlotData ns = new InventorySlotData();
        ns.item = item;
        ns.count = count;
        ns.x = x;
        ns.y = y;
        ns.rotated = rotated;
        slots.Add(ns);

        NotifyChanged();
        return true;
    }

    // ───────────────── 드롭 처리 (UI가 부른다) ─────────────────

    // 드래그가 끝났을 때 한 번 부른다.
    // detached가 true면 slots에 아직 없는 임시 슬롯(분할해서 떼어낸 것)이다.
    //
    // 처리 순서: 스택 합치기 시도 → 안 되면 빈 자리 배치 → 둘 다 실패면 false.
    public bool TryDrop(InventorySlotData dragging, int x, int y, bool rotated, bool detached)
    {
        if (dragging == null || dragging.item == null)
        {
            return false;
        }

        if (x < 0 || y < 0 || x >= gridWidth || y >= gridHeight)
        {
            return false;
        }

        // 1) 합치기
        InventorySlotData target = GetSlotAt(x, y, dragging);

        if (target != null)
        {
            if (target.item == dragging.item && dragging.item.stackable && !target.IsFull)
            {
                int room = dragging.item.MaxStackSafe - target.count;
                int move = Mathf.Min(room, dragging.count);

                target.count += move;
                dragging.count -= move;

                if (dragging.count <= 0)
                {
                    if (!detached)
                    {
                        slots.Remove(dragging);
                    }
                    else
                    {
                        ClearPending();
                    }

                    Log("TryDrop 합치기 전량 (" + x + "," + y + ") 대상 " + target.count + "개");
                    NotifyChanged();
                    return true;
                }

                // 다 못 옮겼다. 나머지는 호출자가 되돌린다.
                Log("TryDrop 합치기 일부 " + move + "개, 남은 " + dragging.count + "개");
                NotifyChanged();
                return false;
            }

            // 다른 아이템 위다. 자리 맞바꾸기는 범위 밖이므로 거부.
            Log("TryDrop 거부 — 다른 아이템 위 (" + x + "," + y + ")");
            return false;
        }

        // 2) 빈 자리 배치
        if (!CanPlace(dragging, x, y, rotated))
        {
            Log("TryDrop 거부 — 배치 불가 (" + x + "," + y + ")");
            return false;
        }

        dragging.x = x;
        dragging.y = y;
        dragging.rotated = rotated;

        if (detached && !slots.Contains(dragging))
        {
            slots.Add(dragging);
            ClearPending();
            Log("TryDrop 분할 조각 배치 (" + x + "," + y + ") " + dragging.count + "개");
        }

        NotifyChanged();
        return true;
    }

    // ───────────────── 다른 인벤토리로 옮기기 (08 루팅) ─────────────────

    // 이 인벤토리의 슬롯을 target의 (x, y)로 옮긴다.
    // 인벤토리 ↔ 상자 ↔ 지면이 전부 이 메서드 하나로 오간다.
    //
    // ★ 순서가 생명이다.
    //   대상에 넣을 수 있는지 전부 확인한 뒤에야 원본에서 뺀다.
    //   원본을 먼저 빼면 대상에 못 넣는 순간 아이템이 증발한다.
    //   07에서 분할 조각이 사라졌던 것과 같은 종류의 사고다.
    public bool TryTransferTo(
        InventorySlotData source,
        InventoryController target,
        int x, int y, bool rotated, bool detached)
    {
        if (source == null || source.item == null || target == null || target == this)
        {
            return false;
        }

        if (!detached && !slots.Contains(source))
        {
            return false;
        }

        ItemData item = source.item;

        // 1) 대상 칸에 같은 아이템이 있으면 합친다.
        InventorySlotData onto = target.GetSlotAt(x, y, null);

        if (onto != null)
        {
            if (onto.item != item || !item.stackable || onto.IsFull)
            {
                Log("TryTransferTo 거부 — 대상 칸이 막혀 있다");
                return false;
            }

            int room = item.MaxStackSafe - onto.count;
            int move = Mathf.Min(room, source.count);
            move = target.MaxAddable(item, move);

            if (move <= 0)
            {
                Log("TryTransferTo 거부 — 대상 무게 초과");
                return false;
            }

            // ★ 10번 상점 — 원본을 건드리기 직전에 정산한다.
            //   여기보다 뒤로 옮기면 돈을 못 냈는데 아이템만 넘어간다.
            if (!ShopSession.Authorize(this, target, item, move))
            {
                Log("TryTransferTo 거부 — 거래 정산 실패");
                return false;
            }

            onto.count += move;
            source.count -= move;

            if (source.count <= 0)
            {
                if (detached)
                {
                    ClearPending();
                }
                else
                {
                    slots.Remove(source);
                }
            }

            Log("TryTransferTo 합치기 " + move + "개, 남은 " + source.count);
            ReportLooted(target, move);
            NotifyChanged();
            target.NotifyChanged();

            // 남은 게 있으면 호출자가 원위치시켜야 하므로 false.
            return source.count <= 0;
        }

        // 2) 빈 자리에 놓는다.
        int w = rotated ? item.gridHeight : item.gridWidth;
        int h = rotated ? item.gridWidth : item.gridHeight;

        if (!target.CanPlace(x, y, w, h, null))
        {
            Log("TryTransferTo 거부 — 자리 없음 (" + x + "," + y + ")");
            return false;
        }

        if (target.MaxAddable(item, source.count) < source.count)
        {
            Log("TryTransferTo 거부 — 대상 무게 초과");
            return false;
        }

        // ★ 10번 상점 — 자리·무게 확인이 전부 끝난 뒤, 원본을 건드리기 직전.
        if (!ShopSession.Authorize(this, target, item, source.count))
        {
            Log("TryTransferTo 거부 — 거래 정산 실패");
            return false;
        }

        // 여기서 처음으로 원본을 건드린다.
        if (detached)
        {
            ClearPending();
        }
        else
        {
            slots.Remove(source);
        }

        InventorySlotData moved = source.Clone();
        moved.x = x;
        moved.y = y;
        moved.rotated = rotated;
        target.AddExisting(moved);

        Log("TryTransferTo " + item.displayName + " x" + moved.count + " → (" + x + "," + y + ")");
        ReportLooted(target, moved.count);
        NotifyChanged();
        return true;
    }

    // ───────────────── 장비 슬롯으로 보내기 ─────────────────

    // 인벤토리의 슬롯을 장비 칸에 장착한다.
    // 원래 그 칸에 있던 장비는 방금 비운 자리로 되돌아온다.
    // 어느 단계에서든 실패하면 전부 원상복구하고 false를 돌려준다.
    public bool TryEquipFrom(InventorySlotData source, EquipSlot target)
    {
        if (source == null || source.item == null || !slots.Contains(source))
        {
            return false;
        }

        if (equipment == null)
        {
            equipment = GetComponent<EquipmentController>();
        }

        if (equipment == null || !EquipmentController.Accepts(target, source.item))
        {
            return false;
        }

        ItemData item = source.item;
        int x = source.x;
        int y = source.y;
        bool rot = source.rotated;

        // 자리를 먼저 비워야 교체품이 들어올 수 있다.
        slots.Remove(source);

        ItemData replaced;

        if (!equipment.TryEquip(target, item, out replaced))
        {
            slots.Add(source);
            return false;
        }

        if (replaced != null)
        {
            // 방금 비운 자리에 먼저 놓아보고, 안 되면 아무 빈 자리에.
            bool placed = TryAddAt(replaced, 1, x, y, rot) || TryAdd(replaced, 1) >= 1;

            if (!placed)
            {
                // 되돌린다. 교체품을 다시 장착하고 원래 아이템은 제자리로.
                ItemData ignored;
                equipment.TryEquip(target, replaced, out ignored);
                slots.Add(source);
                NotifyChanged();
                Log("TryEquipFrom 실패 — 교체품을 넣을 자리가 없다");
                return false;
            }
        }

        Log("TryEquipFrom " + item.displayName + " → " + target);
        NotifyChanged();
        return true;
    }

    // ───────────────── 분할 ─────────────────

    // 절반을 떼어낸 임시 슬롯을 만든다. slots에는 넣지 않는다.
    // 미리 넣으면 CanPlace가 자기 자신과 충돌해서 어디에도 못 놓는다.
    //
    // 홀수면 원본이 하나 더 갖는다. (5개 → 원본 3 / 떼어낸 것 2)
    public InventorySlotData SplitHalf(InventorySlotData source)
    {
        if (!enableSplit || source == null || source.item == null || source.count < 2)
        {
            return null;
        }

        int take = source.count / 2;

        if (take <= 0)
        {
            return null;
        }

        source.count -= take;

        InventorySlotData temp = new InventorySlotData();
        temp.item = source.item;
        temp.count = take;
        temp.x = source.x;
        temp.y = source.y;
        temp.rotated = source.rotated;

        // 드롭이 확정되거나 취소될 때까지 붙들어 둔다.
        pendingSplit = temp;
        pendingSource = source;

        Log("SplitHalf " + source.item.displayName + " → 원본 " + source.count + " / 떼어냄 " + take);

        NotifyChanged();
        return temp;
    }

    private void ClearPending()
    {
        pendingSplit = null;
        pendingSource = null;
    }

    // 분할 드롭이 실패했을 때 원본에 도로 합친다.
    public void CancelSplit(InventorySlotData temp, InventorySlotData source)
    {
        if (temp == null)
        {
            return;
        }

        ClearPending();

        if (temp.count <= 0)
        {
            return;
        }

        if (source != null && slots.Contains(source))
        {
            source.count += temp.count;
            Log("CancelSplit 원본에 복귀 → " + source.count + "개");
            NotifyChanged();
            return;
        }

        // 원본이 사라졌으면 아무 데나 넣어본다.
        int added = TryAdd(temp.item, temp.count);

        if (added < temp.count)
        {
            Debug.LogWarning("InventoryController: 분할 취소분 " + (temp.count - added) + "개를 되돌릴 자리가 없다.", this);
        }
        else
        {
            Log("CancelSplit 새 자리에 복귀 " + added + "개");
        }
    }

    // 안전망 — 드롭이 확정되지 않은 분할 조각이 남아 있으면 되돌린다.
    //
    // OnEndDrag가 어떤 이유로든(뷰가 파괴됨, 창이 닫힘, 씬 전환) 안 불려도
    // 여기서 회수하므로 분할 조각이 영영 사라지는 일이 없다.
    // InventoryUI가 드래그를 끝낼 때와 창을 닫을 때 부른다.
    public void RecoverPendingSplit()
    {
        if (pendingSplit == null)
        {
            return;
        }

        // 이미 배치된 조각이면 회수 대상이 아니다.
        if (slots.Contains(pendingSplit))
        {
            ClearPending();
            return;
        }

        InventorySlotData temp = pendingSplit;
        InventorySlotData source = pendingSource;

        Log("RecoverPendingSplit — 미처리 분할 조각 " + temp.count + "개 회수");
        CancelSplit(temp, source);
    }

    // ───────────────── 사용 · 제거 ─────────────────

    // 지금 이 아이템을 써서 효과가 있는가.
    // 체력이 가득인데 붕대가 소모되는 식의 낭비를 막는다.
    // UI에서 사용 불가 아이템을 흐리게 표시하고 싶으면 이걸 쓰면 된다.
    public bool CanUse(InventorySlotData slot)
    {
        if (slot == null || slot.item == null)
        {
            return false;
        }

        // 상자·지면 인벤토리에서는 아이템을 쓸 수 없다. 플레이어 것만 쓴다.
        if (stats == null)
        {
            return false;
        }

        if (stats.IsDead || !slot.item.IsConsumable)
        {
            return false;
        }

        // 비율이 1에 딱 맞아떨어지지 않는 float 오차를 흡수한다.
        const float full = 0.9999f;

        switch (slot.item.category)
        {
            case ItemCategory.Medical:
                // 출혈을 멈추는 아이템(붕대)은 출혈 중이면 체력이 가득이어도 쓸 수 있다.
                // 그게 원작에서의 본래 용도다.
                if (slot.item.curesBleeding && stats.IsBleeding)
                {
                    return true;
                }

                return stats.HealthRatio < full;

            case ItemCategory.Food:
                return stats.HungerRatio < full;

            case ItemCategory.Drink:
                return stats.ThirstRatio < full;

            case ItemCategory.Antirad:
                return stats.RadiationRatio > 0.0001f;

            case ItemCategory.Stimulant:
                return stats.EnergyRatio < full;
        }

        return false;
    }

    // 우클릭 사용. 성공하면 true.
    public bool Use(InventorySlotData slot)
    {
        if (!CanUse(slot))
        {
            return false;
        }

        ItemData item = slot.item;

        switch (item.category)
        {
            case ItemCategory.Medical:
                stats.Heal(item.effectAmount);

                if (item.curesBleeding)
                {
                    stats.StopBleeding();
                }

                break;

            case ItemCategory.Food:
                stats.EatFood(item.effectAmount);
                break;

            case ItemCategory.Drink:
                stats.DrinkWater(item.effectAmount);
                break;

            case ItemCategory.Antirad:
                stats.ReduceRadiation(item.effectAmount);
                break;

            case ItemCategory.Stimulant:
                stats.RestoreEnergy(item.effectAmount);
                break;

            default:
                return false;
        }

        slot.count -= 1;

        if (slot.count <= 0)
        {
            slots.Remove(slot);
        }

        NotifyChanged();
        return true;
    }

    public void Remove(InventorySlotData slot)
    {
        if (slot == null)
        {
            return;
        }

        if (slots.Remove(slot))
        {
            NotifyChanged();
        }
    }

    public void Clear()
    {
        if (slots.Count == 0)
        {
            return;
        }

        slots.Clear();
        NotifyChanged();
    }

    // 특정 아이템을 몇 개 갖고 있는지. 09 퀘스트에서 쓴다.
    public int CountOf(ItemData item)
    {
        int total = 0;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].item == item)
            {
                total += slots[i].count;
            }
        }

        return total;
    }

    // 특정 아이템을 count개 회수한다. 실제로 뺀 개수를 돌려준다.
    // 09 퀘스트 완료 시 수집품을 넘기는 데 쓴다.
    public int RemoveCount(ItemData item, int count)
    {
        if (item == null || count <= 0)
        {
            return 0;
        }

        int left = count;

        // 뒤에서부터 지운다. 앞에서부터 지우면 인덱스가 밀린다.
        for (int i = slots.Count - 1; i >= 0 && left > 0; i--)
        {
            if (slots[i].item != item)
            {
                continue;
            }

            int take = Mathf.Min(slots[i].count, left);
            slots[i].count -= take;
            left -= take;

            if (slots[i].count <= 0)
            {
                slots.RemoveAt(i);
            }
        }

        int removed = count - left;

        if (removed > 0)
        {
            NotifyChanged();
        }

        return removed;
    }

    // ───────────────── 씬 전환용 스냅샷 ─────────────────

    // PlayerStats.CaptureTo와 같은 패턴이다.
    public void CaptureTo(RunData.PlayerSnapshot s)
    {
        if (s == null)
        {
            return;
        }

        s.inventory = new List<InventorySlotData>();

        for (int i = 0; i < slots.Count; i++)
        {
            // 반드시 복사한다. 참조를 공유하면 존에서의 변경이 스냅샷에 소급된다.
            s.inventory.Add(slots[i].Clone());
        }
    }

    public void RestoreFrom(RunData.PlayerSnapshot s)
    {
        if (s == null)
        {
            return;
        }

        slots.Clear();

        if (s.inventory != null)
        {
            for (int i = 0; i < s.inventory.Count; i++)
            {
                InventorySlotData src = s.inventory[i];

                // ItemData가 없는 슬롯은 버린다 (에셋이 삭제된 경우).
                if (src == null || src.item == null)
                {
                    continue;
                }

                slots.Add(src.Clone());
            }
        }

        NotifyChanged();
    }

    // ───────────────── 테스트 ─────────────────

    // 인스펙터 컴포넌트 우클릭 메뉴에서 부를 수 있다.
    // UI를 만들기 전 1일차 오전에 판정 로직만 확인할 때 쓴다.
    [ContextMenu("테스트 아이템 채우기")]
    private void DebugFill()
    {
        if (testItems == null || testItems.Length == 0)
        {
            Debug.Log("InventoryController: testItems가 비어 있다.", this);
            return;
        }

        for (int i = 0; i < testItems.Length; i++)
        {
            ItemData it = testItems[i];

            if (it == null)
            {
                continue;
            }

            int want = it.stackable ? it.MaxStackSafe : 1;
            int got = TryAdd(it, want);
            Debug.Log("TryAdd " + it.displayName + " x" + want + " → " + got + "개 들어감", this);
        }

        DebugDump();
    }

    [ContextMenu("현재 인벤토리 출력")]
    private void DebugDump()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("── 인벤토리 " + CurrentWeight.ToString("0.0") + " / " + Capacity.ToString("0.0") + " kg");

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlotData s = slots[i];
            sb.AppendLine("  (" + s.x + "," + s.y + ") " + s.Width + "x" + s.Height
                          + (s.rotated ? " [회전]" : "") + "  " + s.item.displayName + " x" + s.count);
        }

        // 점유 맵을 눈으로 확인한다.
        bool[,] map = BuildOccupancy(null);

        for (int y = 0; y < gridHeight; y++)
        {
            string row = "  ";

            for (int x = 0; x < gridWidth; x++)
            {
                row += map[x, y] ? "■" : "□";
            }

            sb.AppendLine(row);
        }

        Debug.Log(sb.ToString(), this);
    }
}
