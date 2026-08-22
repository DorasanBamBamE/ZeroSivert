using UnityEngine;

// 지금 열려 있는 거래. 08의 LootTarget과 같은 자리에 있는 것이고,
// 하는 일이 하나 더 있다 — 아이템이 오갈 때 돈을 정산한다.
//
// ★ 거래 화면을 새로 만들지 않은 이유
//   원작의 상점은 좌측이 내 인벤토리, 우측이 상인 재고다.
//   07·08에서 만든 인벤토리 창의 구조가 정확히 그것이다.
//   그래서 GROUND 패널에 상인 재고를 물리고, 드래그가 일어날 때만
//   여기서 값을 치르게 했다. 드래그·회전·분할·무게 판정이 전부 공짜로 따라온다.
//
// 정산이 일어나는 지점은 InventoryController.TryTransferTo 한 곳뿐이다.
public static class ShopSession
{
    public static ShopController Active { get; private set; }

    public static bool IsOpen
    {
        get { return Active != null; }
    }

    // 마지막 거래 결과. UI가 읽어서 안내를 띄운다.
    public static string LastMessage { get; private set; }

    // 잔액·거래 발생 알림. ShopHUD가 구독한다.
    public static event System.Action Changed;

    private static void Notify()
    {
        if (Changed != null)
        {
            Changed();
        }
    }

    public static void Open(ShopController shop)
    {
        if (shop == null || shop.Stock == null)
        {
            return;
        }

        Active = shop;
        LastMessage = "";

        // 반드시 창을 열기 전에 대상을 지정한다. 08과 같은 순서다.
        //
        // ★ 이름을 같이 넘긴다. 안 넘기면 우측 머리글이 GROUND로 뜬다 -
        //   상인 재고를 보고 있는데 "지면"이라고 적혀 있으면 무엇을 보는지 알 수 없다.
        //   MerchantData에는 이름이 없으므로 같은 오브젝트의 NPC에서 가져온다.
        LootTarget.Set(shop.Stock, ResolveName(shop));

        InventoryScreen screen = Object.FindFirstObjectByType<InventoryScreen>();

        if (screen != null)
        {
            screen.Open();
        }

        Notify();
    }

    // 우측 패널 머리글에 쓸 상인 이름.
    private static string ResolveName(ShopController shop)
    {
        NPCInteract npc = shop.GetComponent<NPCInteract>();

        if (npc != null && npc.Data != null && !string.IsNullOrEmpty(npc.Data.npcName))
        {
            return npc.Data.npcName;
        }

        if (shop.Data != null)
        {
            return shop.Data.name;
        }

        return shop.name;
    }

    // InventoryScreen이 닫힐 때 부른다.
    public static void Close()
    {
        Active = null;
        LastMessage = "";
        Notify();
    }

    // ───────────────── 정산 ─────────────────

    // 아이템이 from에서 to로 옮겨가기 직전에 불린다.
    // false를 돌려주면 이동이 취소된다 — 아직 아무것도 안 건드린 시점이다.
    //
    // ★ 반드시 원본을 건드리기 전에 불러야 한다.
    //   돈을 못 냈는데 아이템만 옮겨가는 사고를 막는 유일한 방법이다.
    public static bool Authorize(InventoryController from, InventoryController to,
                                 ItemData item, int count)
    {
        // 거래 중이 아니면 아무 제약이 없다. 루팅·지면 버리기는 그대로 동작한다.
        if (!IsOpen || item == null || count <= 0)
        {
            return true;
        }

        MerchantData m = Active.Data;
        InventoryController stock = Active.Stock;

        if (m == null || stock == null)
        {
            return true;
        }

        // 구매 — 상인 재고에서 내 쪽으로
        if (from == stock && to != stock)
        {
            int price = m.GetBuyPrice(item, count);

            if (!Wallet.Instance.Spend(price))
            {
                LastMessage = "루블이 모자란다. (" + price + " ₽ 필요)";
                Notify();
                return false;
            }

            LastMessage = item.displayName + " x" + count + " 구입 · -" + price + " ₽";
            Notify();
            return true;
        }

        // 판매 — 내 쪽에서 상인 재고로
        if (to == stock && from != stock)
        {
            if (!m.Accepts(item))
            {
                LastMessage = "이건 안 산다.";
                Notify();
                return false;
            }

            int price = m.GetSellPrice(item, count);
            Wallet.Instance.Earn(price);

            LastMessage = item.displayName + " x" + count + " 판매 · +" + price + " ₽";
            Notify();
            return true;
        }

        // 내 인벤토리 안에서의 이동. 돈이 오가지 않는다.
        return true;
    }

    // 가격표에 찍을 값. 거래 중이 아니거나 값을 매길 수 없으면 0.
    public static int PriceFor(InventoryController owner, InventorySlotData slot)
    {
        if (!IsOpen || owner == null || slot == null || slot.item == null)
        {
            return 0;
        }

        MerchantData m = Active.Data;

        if (m == null)
        {
            return 0;
        }

        // 상인 재고에 있는 것 = 내가 낼 값
        if (owner == Active.Stock)
        {
            return m.GetBuyPrice(slot.item, slot.count);
        }

        // 내 인벤토리에 있는 것 = 내가 받을 값
        return m.GetSellPrice(slot.item, slot.count);
    }
}
