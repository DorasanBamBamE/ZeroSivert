using UnityEngine;
using UnityEngine.UI;

// 상태 아이콘 하나. 등급에 따라 아이콘 색이 바뀐다.
// 심볼 스프라이트는 흰색으로 그려져 있어 색상 틴트가 그대로 적용된다.
// 출혈은 등급이 없는 On/Off 디버프이므로 Bleed 타입으로 처리한다.
public class StatusIcon : MonoBehaviour
{
    public enum IconType
    {
        Fatigue,
        Hunger,
        Thirst,
        Radiation,
        Bleed,
    }

    // 인덱스 0=녹색, 1=백색, 2=노랑, 3=주황, 4=빨강
    private static readonly Color[] TierColors =
    {
        new Color32(89, 193, 53, 255),
        new Color32(255, 255, 255, 255),
        new Color32(255, 252, 64, 255),
        new Color32(249, 163, 27, 255),
        new Color32(180, 32, 42, 255),
    };

    [SerializeField] private PlayerStats stats;
    [SerializeField] private IconType type;
    [SerializeField] private Image symbol;

    private int lastIndex = -1;

    private void Awake()
    {
        if (stats == null)
        {
            stats = FindFirstObjectByType<PlayerStats>();
        }
        if (symbol == null)
        {
            symbol = GetComponent<Image>();
        }
    }

    private void Update()
    {
        if (stats == null || symbol == null)
        {
            return;
        }

        if (type == IconType.Bleed)
        {
            symbol.enabled = stats.IsBleeding;
            SetTier(4);
            return;
        }

        SetTier((int)GetTier());
    }

    private void SetTier(int index)
    {
        if (index == lastIndex || index < 0 || index >= TierColors.Length)
        {
            return;
        }

        lastIndex = index;
        symbol.color = TierColors[index];
    }

    private PlayerStats.Tier GetTier()
    {
        switch (type)
        {
            case IconType.Fatigue:
                return stats.EnergyTier;
            case IconType.Hunger:
                return stats.HungerTier;
            case IconType.Thirst:
                return stats.ThirstTier;
            default:
                return stats.RadiationTier;
        }
    }
}