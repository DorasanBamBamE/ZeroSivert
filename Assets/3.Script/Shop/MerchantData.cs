using UnityEngine;

// 상인 한 명의 거래 조건. Project 창 우클릭 → Create → ZeroSievert → Merchant
//
// 가격은 아이템마다 따로 적지 않는다.
//   ItemData.basePrice  × 상인 배율 × 평판 할인
// 이렇게 계산한다. 아이템이 늘어도 표를 관리할 필요가 없다.
//
// 원작 구성
//   바텐더 — 잡화·무기·탄약. 무기 마진이 크다
//   의사   — 의료품만. 대신 치료 서비스가 있다
//   네트워커 — 거래 안 함 (merchant를 비워두면 거래 버튼이 안 뜬다)
[CreateAssetMenu(fileName = "Merchant_", menuName = "ZeroSievert/Merchant")]
public class MerchantData : ScriptableObject
{
    [Header("가격 배율")]
    // 플레이어가 살 때 붙는 배율. 1.4면 기준가의 140%를 낸다.
    [Range(0.5f, 3f)]
    public float sellMultiplier = 1.4f;

    // 플레이어가 팔 때 받는 배율. 0.5면 기준가의 절반만 받는다.
    // ★ 반드시 sellMultiplier보다 낮아야 한다. 아니면 무한 차익거래가 된다.
    [Range(0.05f, 1f)]
    public float buyMultiplier = 0.5f;

    [Header("세력 평판 할인")]
    // 이 세력의 평판이 오르면 사는 값이 싸진다. Neutral이면 할인 없음.
    public Faction faction = Faction.Neutral;

    // 평판 100당 몇 % 할인인가.
    [Range(0f, 20f)]
    public float discountPer100Rep = 2f;

    // 아무리 평판이 높아도 이 이상은 안 깎인다.
    [Range(0f, 0.5f)]
    public float maxDiscount = 0.25f;

    [Header("매입 범위")]
    // 이 상인이 사주는 분류. 비워두면 전부 사준다.
    public ItemCategory[] buysCategories;

    // 이 상인이 절대 안 사는 분류. 위보다 우선한다.
    public ItemCategory[] refusesCategories;

    [Header("재고")]
    [Range(2, 10)] public int stockWidth = 8;
    [Range(2, 10)] public int stockHeight = 8;

    // 재고를 채우는 테이블. 08의 LootTable을 그대로 쓴다.
    public LootTable stockTable;

    // 하루가 지나면 재고를 새로 굴린다. 끄면 처음 한 번만 채운다.
    public bool restockDaily = true;

    [Header("서비스 (의사 전용, 선택)")]
    // 0이면 그 버튼이 안 뜬다.
    public int healPrice = 300;        // 체력 전부 회복
    public int bleedPrice = 150;       // 출혈 정지
    public int radPrice = 400;         // 방사능 제거

    // ───────────────── 가격 계산 ─────────────────

    // 평판에 따른 할인율 (0 ~ maxDiscount).
    public float GetDiscount()
    {
        if (faction == Faction.Neutral)
        {
            return 0f;
        }

        int rep = QuestManager.Exists ? QuestManager.Instance.GetReputation(faction) : 0;
        float d = (rep / 100f) * (discountPer100Rep / 100f);
        return Mathf.Clamp(d, 0f, maxDiscount);
    }

    // 플레이어가 지불하는 값. 올림 — 상인이 손해 보지 않는다.
    public int GetBuyPrice(ItemData item, int count)
    {
        if (item == null || count <= 0)
        {
            return 0;
        }

        float unit = item.basePrice * sellMultiplier * (1f - GetDiscount());
        return Mathf.Max(1, Mathf.CeilToInt(unit)) * count;
    }

    // 플레이어가 받는 값. 내림 — 같은 이유다.
    public int GetSellPrice(ItemData item, int count)
    {
        if (item == null || count <= 0 || !Accepts(item))
        {
            return 0;
        }

        float unit = item.basePrice * buyMultiplier;
        return Mathf.Max(1, Mathf.FloorToInt(unit)) * count;
    }

    public bool Accepts(ItemData item)
    {
        if (item == null)
        {
            return false;
        }

        // 값이 0인 물건은 아무도 안 산다. 퀘스트 전용 아이템을 팔아버리는 사고를 막는다.
        if (item.basePrice <= 0)
        {
            return false;
        }

        if (refusesCategories != null)
        {
            for (int i = 0; i < refusesCategories.Length; i++)
            {
                if (refusesCategories[i] == item.category)
                {
                    return false;
                }
            }
        }

        if (buysCategories == null || buysCategories.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < buysCategories.Length; i++)
        {
            if (buysCategories[i] == item.category)
            {
                return true;
            }
        }

        return false;
    }

    private void OnValidate()
    {
        // 무한 차익거래 방지. 사는 값이 파는 값보다 싸면 돈이 무한히 생긴다.
        if (buyMultiplier >= sellMultiplier)
        {
            buyMultiplier = sellMultiplier * 0.5f;
            Debug.LogWarning("[MerchantData] " + name +
                             " — 매입 배율이 판매 배율 이상이라 절반으로 낮췄다. " +
                             "그대로 두면 사서 되파는 것만으로 돈이 무한히 생긴다.", this);
        }

        healPrice = Mathf.Max(0, healPrice);
        bleedPrice = Mathf.Max(0, bleedPrice);
        radPrice = Mathf.Max(0, radPrice);
    }
}
