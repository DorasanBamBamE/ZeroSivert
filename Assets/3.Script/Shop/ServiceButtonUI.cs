using UnityEngine;
using UnityEngine.UI;

// 의사의 서비스 버튼 하나. 대화창 안에 세 개를 둔다.
//
// 필요 없을 때는 스스로 꺼진다.
//   상인이 의사가 아니면          → 꺼짐 (가격이 0이면 그 서비스를 안 판다)
//   이미 멀쩡하면                 → 꺼짐 (체력 만땅인데 치료를 팔지 않는다)
//   돈이 모자라면                 → 회색 (누를 수는 없지만 값은 보인다)
//
// 07의 InventoryController.CanUse()와 같은 방침이다 —
// 낭비가 될 행동은 아예 못 하게 막는다.
public class ServiceButtonUI : MonoBehaviour
{
    [SerializeField] private ServiceType type = ServiceType.Heal;

    [SerializeField] private Button button;
    [SerializeField] private Text label;

    // 켜고 끌 대상. 비우면 자기 자신.
    [SerializeField] private GameObject root;

    [Header("표시")]
    [SerializeField] private string healName = "치료";
    [SerializeField] private string bleedName = "지혈";
    [SerializeField] private string radName = "제염";

    private MerchantData merchant;
    private PlayerStats stats;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (root == null)
        {
            root = gameObject;
        }

        if (button != null)
        {
            button.onClick.AddListener(OnClick);
        }
    }

    // DialogueUI가 NPC를 열 때 부른다. merchant가 null이면 버튼이 꺼진다.
    public void Bind(MerchantData m)
    {
        merchant = m;
        Refresh();
    }

    public void Refresh()
    {
        if (root == null)
        {
            return;
        }

        int price = PriceOf();

        if (merchant == null || price <= 0 || !NeedsIt())
        {
            root.SetActive(false);
            return;
        }

        root.SetActive(true);

        if (label != null)
        {
            label.text = NameOf() + "  " + price + " ₽";
        }

        if (button != null)
        {
            button.interactable = Wallet.Instance.CanAfford(price);
        }
    }

    private PlayerStats Stats
    {
        get
        {
            if (stats == null)
            {
                stats = Object.FindFirstObjectByType<PlayerStats>();
            }

            return stats;
        }
    }

    private int PriceOf()
    {
        if (merchant == null)
        {
            return 0;
        }

        switch (type)
        {
            case ServiceType.Heal: return merchant.healPrice;
            case ServiceType.Bleed: return merchant.bleedPrice;
            case ServiceType.Radiation: return merchant.radPrice;
        }

        return 0;
    }

    private string NameOf()
    {
        switch (type)
        {
            case ServiceType.Heal: return healName;
            case ServiceType.Bleed: return bleedName;
            case ServiceType.Radiation: return radName;
        }

        return "";
    }

    // 지금 이 서비스가 의미가 있는가.
    private bool NeedsIt()
    {
        PlayerStats s = Stats;

        if (s == null || s.IsDead)
        {
            return false;
        }

        switch (type)
        {
            case ServiceType.Heal:
                // 07에서 쓴 것과 같은 기준. 부동소수 때문에 1f로 비교하지 않는다.
                return s.HealthRatio < 0.9999f;

            case ServiceType.Bleed:
                return s.IsBleeding;

            case ServiceType.Radiation:
                return s.RadiationRatio > 0.0001f;
        }

        return false;
    }

    private void OnClick()
    {
        PlayerStats s = Stats;
        int price = PriceOf();

        if (s == null || price <= 0 || !NeedsIt())
        {
            return;
        }

        // ★ 값을 먼저 치른다. 실패하면 아무 일도 일어나지 않는다.
        if (!Wallet.Instance.Spend(price))
        {
            return;
        }

        switch (type)
        {
            case ServiceType.Heal:
                s.Heal(99999f);
                break;

            case ServiceType.Bleed:
                s.StopBleeding();
                break;

            case ServiceType.Radiation:
                s.ReduceRadiation(100f);
                break;
        }

        Refresh();
    }
}
