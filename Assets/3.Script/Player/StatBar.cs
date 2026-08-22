using UnityEngine;
using UnityEngine.UI;

// 생존 스탯 한 종류를 표시하는 바.
// HUD와 PDA 등 같은 스탯을 여러 곳에서 표시할 때 각각 하나씩 붙인다.
// Fill 이미지는 Image Type을 Filled로, Method와 Origin은 바 방향에 맞게 설정할 것.
public class StatBar : MonoBehaviour
{
    public enum StatType
    {
        Health,
        Stamina,
        Hunger,
        Thirst,
    }

    [SerializeField] private PlayerStats stats;
    [SerializeField] private StatType type;
    [SerializeField] private Image fill;
    [SerializeField] private Text valueLabel;

    private void Awake()
    {
        if (stats == null)
        {
            stats = FindFirstObjectByType<PlayerStats>();
        }
    }

    private void Update()
    {
        if (stats == null || fill == null)
        {
            return;
        }

        fill.fillAmount = Mathf.Clamp01(GetRatio());
        UpdateLabel();
    }

    private int lastShown = -1;

    // 정수가 바뀔 때만 문자열을 만든다. 매 프레임 만들면 GC가 계속 발생한다.
    private void UpdateLabel()
    {
        if (valueLabel == null)
        {
            return;
        }

        int current = Mathf.RoundToInt(GetRatio() * GetMax());

        if (current == lastShown)
        {
            return;
        }

        lastShown = current;
        valueLabel.text = current + "/" + Mathf.RoundToInt(GetMax());
    }

    // PlayerStats의 최대치. 현재는 체력·스태미나 모두 100 기준이다.
    private float GetMax()
    {
        return 100f;
    }
    private float GetRatio()
    {
        switch (type)
        {
            case StatType.Health:
                return stats.HealthRatio;
            case StatType.Stamina:
                return stats.StaminaRatio;
            case StatType.Hunger:
                return stats.HungerRatio;
            case StatType.Thirst:
                return stats.ThirstRatio;
            default:
                return 0f;
        }
    }
}