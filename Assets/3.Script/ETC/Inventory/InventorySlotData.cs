using UnityEngine;

// 인벤토리에 놓인 아이템 하나. 좌표와 개수를 함께 들고 있다.
// RunData.PlayerSnapshot에 그대로 담기므로 [System.Serializable]이 필요하다.
//
// 좌표는 왼쪽 위가 (0, 0)이고 x는 오른쪽, y는 아래로 증가한다.
[System.Serializable]
public class InventorySlotData
{
    public ItemData item;
    public int count = 1;

    // 아이템이 차지하는 사각형의 왼쪽 위 셀 좌표.
    public int x;
    public int y;

    // true면 90도 돌아간 상태. 판정에 쓰는 가로·세로가 뒤바뀐다.
    public bool rotated;

    // ─────────────────────────────────────────────
    // 회전을 여기서 흡수한다. 덕분에 배치 판정 코드는 회전을 몰라도 된다.
    // ─────────────────────────────────────────────

    public int Width
    {
        get
        {
            if (item == null)
            {
                return 1;
            }

            return rotated ? item.gridHeight : item.gridWidth;
        }
    }

    public int Height
    {
        get
        {
            if (item == null)
            {
                return 1;
            }

            return rotated ? item.gridWidth : item.gridHeight;
        }
    }

    public float TotalWeight
    {
        get { return item == null ? 0f : item.weight * count; }
    }

    public bool IsFull
    {
        get { return item == null || count >= item.MaxStackSafe; }
    }

    // 이 슬롯이 (cx, cy) 칸을 덮고 있는지.
    public bool Covers(int cx, int cy)
    {
        return cx >= x && cx < x + Width
            && cy >= y && cy < y + Height;
    }

    // 스냅샷에 담을 때 반드시 이걸로 복사한다.
    // 참조를 그대로 넘기면 존에서의 변경이 허브 스냅샷에 소급되는 유령 버그가 난다.
    public InventorySlotData Clone()
    {
        InventorySlotData copy = new InventorySlotData();
        copy.item = item;          // ItemData는 프로젝트 에셋이라 참조 공유가 안전하다
        copy.count = count;
        copy.x = x;
        copy.y = y;
        copy.rotated = rotated;
        return copy;
    }
}