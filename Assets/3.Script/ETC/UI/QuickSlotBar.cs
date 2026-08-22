using System;
using UnityEngine;
using UnityEngine.UI;

// 하단 중앙 퀵슬롯. 원작 s_hud_quickbar_slot(18×18) 기준.
//
// 인벤토리에서 소비 아이템 위에 마우스를 올리고 3~6을 누르면 등록한다.
// 게임 중 같은 키를 누르면 해당 아이템을 즉시 사용하며, 수량은 InventoryController.Changed를 따라간다.
//
// 하이어라키 — 이 스크립트는 QuickBar 컨테이너에 붙인다.
//   QuickBar               Horizontal Layout Group, 하단 중앙 앵커
//   ├ Slot_0               Image: s_hud_quickbar_slot (18×18)
//   │   ├ Icon             Image: 아이템 아이콘 (16×16), 비었을 때 비활성
//   │   └ KeyLabel         TMP_Text: "3"
//   ├ Slot_1 …             동일 구조
//   ⋮
//
// Layout Group의 Child Control / Force Expand는 반드시 꺼둘 것.
// 켜져 있으면 18×18 크기 지정이 무시된다.
public class QuickSlotBar : MonoBehaviour
{
    [Serializable]
    public class Slot
    {
        public RectTransform root;
        public Image frame;
        public Image icon;

        [HideInInspector] public ItemData item;
        [HideInInspector] public Sprite itemSprite;
        [HideInInspector] public int count;
    }

    [SerializeField] private Slot[] slots = new Slot[4];

    // 슬롯 순서와 1:1 대응. 원작 배치는 3, 4, 5, 6.
    [SerializeField]
    private KeyCode[] slotKeys =
    {
        KeyCode.Alpha3,
        KeyCode.Alpha4,
        KeyCode.Alpha5,
        KeyCode.Alpha6,
    };

    [Header("선택 표시")]
    // 선택된 슬롯의 프레임 스프라이트를 교체한다. 비워두면 색 틴트만 적용된다.
    [SerializeField] private Sprite frameNormal;
    [SerializeField] private Sprite frameSelected;
    [SerializeField] private Color normalTint = new Color32(255, 255, 255, 200);
    [SerializeField] private Color selectedTint = Color.white;

    // 선택된 슬롯 없이 시작하려면 -1로 둔다.
    [SerializeField] private int startIndex = -1;

    private int selectedIndex = -1;
    private InventoryController inventory;
    private static readonly ItemData[] savedBindings = new ItemData[4];

    public static QuickSlotBar Current { get; private set; }

    // 슬롯을 사용할 때 발생. 인벤토리·소비아이템 시스템에서 구독한다.
    public event Action<int> OnSlotUsed;

    public int SelectedIndex
    {
        get { return selectedIndex; }
    }

private void Awake()
    {
        Current = this;
        ResolveInventory();

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && i < savedBindings.Length)
            {
                slots[i].item = savedBindings[i];
            }

            RefreshIcon(i);
        }

        Select(startIndex);
    }

private void OnEnable()
    {
        Current = this;
        ResolveInventory();
        SubscribeInventory();
        RefreshAll();
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.Changed -= RefreshAll;
        }

        if (Current == this)
        {
            Current = null;
        }
    }


private void Update()
    {
        if (!InventoryScreen.IsOpen && !UIBlocker.Any)
        {
            ReadKeys();
        }
    }

private void ReadKeys()
    {
        int limit = Mathf.Min(slots.Length, slotKeys.Length);

        for (int i = 0; i < limit; i++)
        {
            if (!Input.GetKeyDown(slotKeys[i]))
            {
                continue;
            }

            Select(i);
            Use(i);
            return;
        }
    }

    public void Select(int index)
    {
        selectedIndex = (index >= 0 && index < slots.Length) ? index : -1;

        for (int i = 0; i < slots.Length; i++)
        {
            ApplySelection(i, i == selectedIndex);
        }
    }

    private void ApplySelection(int index, bool selected)
    {
        Slot slot = slots[index];

        if (slot == null || slot.frame == null)
        {
            return;
        }

        Sprite sprite = selected ? frameSelected : frameNormal;

        if (sprite != null)
        {
            slot.frame.sprite = sprite;
            slot.frame.SetNativeSize();
        }

        slot.frame.color = selected ? selectedTint : normalTint;
    }

    // 인벤토리에서 아이템을 배치할 때 호출한다. sprite가 null이면 빈 슬롯이 된다.
    public void SetItem(int index, Sprite sprite, int amount)
    {
        if (index < 0 || index >= slots.Length || slots[index] == null)
        {
            return;
        }

        slots[index].itemSprite = sprite;
        slots[index].count = amount;
        RefreshIcon(index);
    }

public void Assign(int index, InventoryController source, ItemData item)
    {
        if (index < 0 || index >= slots.Length || index >= savedBindings.Length || slots[index] == null || item == null || !item.IsConsumable)
        {
            return;
        }

        inventory = source != null ? source : inventory;
        SubscribeInventory();
        slots[index].item = item;
        savedBindings[index] = item;
        RefreshIcon(index);
        Select(index);
    }

public void Clear(int index)
    {
        if (index < 0 || index >= slots.Length || index >= savedBindings.Length || slots[index] == null)
        {
            return;
        }

        slots[index].item = null;
        slots[index].itemSprite = null;
        slots[index].count = 0;
        savedBindings[index] = null;
        RefreshIcon(index);
    }

private void RefreshIcon(int index)
    {
        if (index < 0 || index >= slots.Length)
        {
            return;
        }

        Slot slot = slots[index];
        if (slot == null || slot.icon == null)
        {
            return;
        }

        if (slot.item != null)
        {
            slot.itemSprite = slot.item.icon;
            slot.count = inventory != null ? inventory.CountOf(slot.item) : 0;
        }

        bool hasItem = slot.itemSprite != null && slot.count > 0;
        slot.icon.enabled = hasItem;

        if (hasItem)
        {
            slot.icon.sprite = slot.itemSprite;
            slot.icon.SetNativeSize();
        }
    }

    // 실제 사용. 지금은 이벤트만 쏘고, 소비 처리는 구독하는 쪽에서 한다.
private void Use(int index)
    {
        if (index < 0 || index >= slots.Length || slots[index] == null)
        {
            return;
        }

        Slot quick = slots[index];
        if (quick.item != null && inventory != null)
        {
            InventorySlotData target = null;
            var items = inventory.Slots;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].item == quick.item && inventory.CanUse(items[i]))
                {
                    target = items[i];
                    break;
                }
            }

            if (target != null)
            {
                inventory.Use(target);
            }

            RefreshAll();
            return;
        }

        if (quick.count <= 0)
        {
            return;
        }

        if (OnSlotUsed != null)
        {
            OnSlotUsed(index);
        }
    }

    // 소비 후 개수를 줄일 때 호출한다.
    private void ResolveInventory()
    {
        if (inventory != null)
        {
            return;
        }

        PlayerStats player = FindFirstObjectByType<PlayerStats>();
        if (player != null)
        {
            inventory = player.GetComponent<InventoryController>();
        }
    }

    private void SubscribeInventory()
    {
        if (inventory == null)
        {
            ResolveInventory();
        }

        if (inventory != null)
        {
            inventory.Changed -= RefreshAll;
            inventory.Changed += RefreshAll;
        }
    }

    public void RefreshAll()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            RefreshIcon(i);
        }
    }

    public void Consume(int index, int amount = 1)
    {
        if (index < 0 || index >= slots.Length || slots[index] == null)
        {
            return;
        }

        slots[index].count = Mathf.Max(0, slots[index].count - amount);

        if (slots[index].count == 0)
        {
            slots[index].itemSprite = null;
        }

        RefreshIcon(index);
    }
}