using UnityEngine;
using UnityEngine.UI;

// PDA 상태 탭의 한 행. 참조 배치 기준:
//
//   [아이콘 16×16]  [수치/상태명]  ………  [효과]
//    로컬x 143       로컬x 160            로컬x 255
//
// 예) [번개] 86/100 (원기를 회복함)     +30%
//
// 등급별 색은 StatusIcon과 같은 팔레트를 쓴다.
// 아이콘 오브젝트에 StatusIcon.cs를 따로 붙이면 색 틴트는 그쪽이 처리하므로
// 여기서는 텍스트만 담당해도 된다.
public class PDAStatusRow : MonoBehaviour
{
    public enum RowType
    {
        Energy,
        Hunger,
        Thirst,
        Radiation,
    }

    // StatusIcon과 동일한 5단계 팔레트. 인덱스 0=녹색 … 4=빨강.
    private static readonly Color[] TierColors =
    {
        new Color32(89, 193, 53, 255),
        new Color32(255, 255, 255, 255),
        new Color32(255, 252, 64, 255),
        new Color32(249, 163, 27, 255),
        new Color32(180, 32, 42, 255),
    };

    [SerializeField] private PlayerStats stats;
    [SerializeField] private RowType type;

    [SerializeField] private Image icon;
    [SerializeField] private Text valueLabel;
    [SerializeField] private Text effectLabel;

    // 아이콘도 같은 색으로 물들일지. StatusIcon.cs를 따로 붙였다면 꺼둔다.
    [SerializeField] private bool tintIcon = true;

    private int lastValue = -1;
    private int lastTier = -1;

    private void Awake()
    {
        if (stats == null)
        {
            stats = FindFirstObjectByType<PlayerStats>();
        }
    }

    private void Update()
    {
        if (stats == null)
        {
            return;
        }

        int value = GetValue();
        int tier = (int)GetTier();

        if (value == lastValue && tier == lastTier)
        {
            return;
        }

        lastValue = value;
        lastTier = tier;

        Refresh(value, tier);
    }

    private void Refresh(int value, int tier)
    {
        Color color = TierColors[Mathf.Clamp(tier, 0, TierColors.Length - 1)];

        if (valueLabel != null)
        {
            valueLabel.text = BuildValueText(value);
        }

        if (effectLabel != null)
        {
            effectLabel.text = GetEffectText();
        }

        if (tintIcon && icon != null)
        {
            icon.color = color;
        }
    }
    // 방사능만 "0 (피폭되지 않음)" 형태로 분모를 표시하지 않는다.
    private string BuildValueText(int value)
    {
        string state = GetStateName();

        if (type == RowType.Radiation)
        {
            return value + " (" + state + ")";
        }

        return value + "/100 (" + state + ")";
    }

    private int GetValue()
    {
        switch (type)
        {
            case RowType.Energy:
                return Mathf.RoundToInt(stats.EnergyRatio * 100f);
            case RowType.Hunger:
                return Mathf.RoundToInt(stats.HungerRatio * 100f);
            case RowType.Thirst:
                return Mathf.RoundToInt(stats.ThirstRatio * 100f);
            default:
                return Mathf.RoundToInt(stats.RadiationRatio * 100f);
        }
    }

    private PlayerStats.Tier GetTier()
    {
        switch (type)
        {
            case RowType.Energy:
                return stats.EnergyTier;
            case RowType.Hunger:
                return stats.HungerTier;
            case RowType.Thirst:
                return stats.ThirstTier;
            default:
                return stats.RadiationTier;
        }
    }

    private string GetStateName()
    {
        PlayerStats.Tier tier = GetTier();

        switch (type)
        {
            case RowType.Energy:
                switch (tier)
                {
                    case PlayerStats.Tier.Green: return "원기를 회복함";
                    case PlayerStats.Tier.White: return "정상";
                    case PlayerStats.Tier.Yellow: return "피곤함";
                    case PlayerStats.Tier.Orange: return "매우 피곤함";
                    default: return "탈진";
                }

            case RowType.Hunger:
                switch (tier)
                {
                    case PlayerStats.Tier.Green: return "포화";
                    case PlayerStats.Tier.White: return "정상";
                    case PlayerStats.Tier.Yellow: return "허기짐";
                    case PlayerStats.Tier.Orange: return "배고픔";
                    default: return "굶주림";
                }

            case RowType.Thirst:
                switch (tier)
                {
                    case PlayerStats.Tier.Green: return "수분 공급 충분";
                    case PlayerStats.Tier.White: return "정상";
                    case PlayerStats.Tier.Yellow: return "목마름";
                    case PlayerStats.Tier.Orange: return "갈증";
                    default: return "탈수";
                }

            default:
                switch (tier)
                {
                    case PlayerStats.Tier.Green: return "피폭되지 않음";
                    case PlayerStats.Tier.White: return "경미한 피폭";
                    case PlayerStats.Tier.Yellow: return "피폭";
                    case PlayerStats.Tier.Orange: return "심한 피폭";
                    default: return "치명적 피폭";
                }
        }
    }

    // PlayerStats의 실제 배율과 문구를 맞춰둔다. 값을 고치면 여기도 같이 고칠 것.
    private string GetEffectText()
    {
        PlayerStats.Tier tier = GetTier();

        switch (type)
        {
            case RowType.Energy:
                switch (tier)
                {
                    case PlayerStats.Tier.Green: return "+30% 스태미나 회복";
                    case PlayerStats.Tier.White: return "";
                    case PlayerStats.Tier.Yellow: return "-20% 스태미나 회복";
                    case PlayerStats.Tier.Orange: return "-40% 스태미나 회복";
                    default: return "-60% 스태미나 회복";
                }

            case RowType.Hunger:
                switch (tier)
                {
                    case PlayerStats.Tier.Green: return "+2kg";
                    case PlayerStats.Tier.White: return "";
                    case PlayerStats.Tier.Yellow: return "-1kg";
                    case PlayerStats.Tier.Orange: return "-2kg";
                    default: return "-4kg, 이동속도 -15%";
                }

            case RowType.Thirst:
                switch (tier)
                {
                    case PlayerStats.Tier.Green: return "+15% 노획 속도";
                    case PlayerStats.Tier.White: return "";
                    case PlayerStats.Tier.Yellow: return "-10% 노획 속도";
                    case PlayerStats.Tier.Orange: return "-20% 노획 속도";
                    default: return "-30% 노획 속도";
                }

            default:
                switch (tier)
                {
                    case PlayerStats.Tier.Green: return "";
                    case PlayerStats.Tier.White: return "";
                    case PlayerStats.Tier.Yellow: return "";
                    case PlayerStats.Tier.Orange: return "지속 피해";
                    default: return "지속 피해, 사망 위험";
                }
        }
    }
}