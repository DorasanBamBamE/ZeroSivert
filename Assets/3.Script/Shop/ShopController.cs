using UnityEngine;

// 상인의 재고와 거래를 담당한다. NPC 오브젝트에 NPCInteract와 나란히 붙인다.
//
// 재고는 08의 LootContainer와 완전히 같은 방식으로 만든다 —
// 자식 오브젝트에 InventoryController를 하나 만들고 무게 제한을 없앤다.
// 그래서 거래 화면이 루팅 화면과 같은 코드로 돌아간다.
public class ShopController : MonoBehaviour
{
    [SerializeField] private MerchantData data;

    private InventoryController stock;
    private int stockedDay = -9999;
    private bool stockedOnce;

    public MerchantData Data
    {
        get { return data; }
    }

    public InventoryController Stock
    {
        get { EnsureStock(); return stock; }
    }

    public bool HasMerchant
    {
        get { return data != null; }
    }

    private void EnsureStock()
    {
        if (stock != null)
        {
            return;
        }

        GameObject go = new GameObject("Stock");
        go.transform.SetParent(transform, false);

        stock = go.AddComponent<InventoryController>();

        int w = (data != null) ? data.stockWidth : 8;
        int h = (data != null) ? data.stockHeight : 8;

        // 상인 재고에는 무게 제한이 없다.
        stock.Configure(w, h, 99999f);
    }

    // 거래 화면을 열기 직전에 부른다.
    public void RefreshStock()
    {
        EnsureStock();

        if (data == null || data.stockTable == null)
        {
            return;
        }

        int today = GameClock.Instance != null ? GameClock.Instance.Day : 0;

        bool need = !stockedOnce || (data.restockDaily && today != stockedDay);

        if (!need)
        {
            return;
        }

        stockedOnce = true;
        stockedDay = today;

        // ★ 매일 비우고 새로 굴린다.
        //   안 비우면 플레이어가 판 물건이 쌓여서 재고가 무한히 늘어난다.
        stock.Clear();
        data.stockTable.RollInto(stock);
    }

    // 거래를 연다. DialogueUI의 '거래' 버튼이 부른다.
    public void OpenTrade()
    {
        if (data == null)
        {
            Debug.LogWarning("[Shop] MerchantData가 비어 있다. " + name, this);
            return;
        }

        RefreshStock();
        ShopSession.Open(this);
    }
}
