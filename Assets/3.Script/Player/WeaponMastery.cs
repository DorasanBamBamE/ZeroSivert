using System;
using UnityEngine;

// 총기 숙련도. 원작 mastery 시스템에 대응한다.
//
// 무기 종류별로 경험치를 쌓아 레벨을 올리고, 레벨에 따라 반동이 줄어든다.
// 원작 아이콘이 6종(pistol/smg/shotgun/rifle/dmr/sniper)이므로 그대로 맞춘다.
// MVP에서 실제로 쓰는 무기는 3정이지만, 나머지는 0레벨로 표시된다.
//
// 레벨은 캡슐(pip) 하나로 표시된다. 마일스톤 레벨은 노란색으로 강조된다.
//
// 플레이어에 붙인다.
public class WeaponMastery : MonoBehaviour
{
    // 순서를 바꾸면 인스펙터에 저장된 값이 어긋난다. 항상 끝에 추가할 것.
    public enum WeaponClass
    {
        Pistol,
        Smg,
        Shotgun,
        Rifle,
        Dmr,
        Sniper,
    }

    // 종류 하나의 진행 상황.
    [Serializable]
    public class Progress
    {
        public int level = 1;
        public float exp;
    }

    // 캡슐 개수와 같다. 원작 화면 기준 15개.
    [SerializeField] private int maxLevel = 15;

    // 이 간격마다 보너스가 붙는 마일스톤 레벨이 된다. UI에서 노란색으로 강조된다.
    [SerializeField] private int milestoneInterval = 4;

    // 레벨 n → n+1에 필요한 경험치 = expBase * n^expCurve
    [SerializeField] private float expBase = 100f;
    [SerializeField] private float expCurve = 1.4f;

    [Header("레벨 보너스")]
    // 최대 레벨에서 사격 반동이 이 비율까지 줄어든다. 0.4면 60% 감소.
    [SerializeField] private float recoilMultiplierAtMax = 0.4f;

    // enum 순서와 1:1 대응. Awake에서 크기를 맞춘다.
    [SerializeField] private Progress[] entries;

    // 레벨업 시 알림. UI 팝업 등에 쓴다.
    public event Action<WeaponClass, int> OnLevelUp;

    public int MaxLevel
    {
        get { return maxLevel; }
    }

    private void Awake()
    {
        int count = Enum.GetValues(typeof(WeaponClass)).Length;

        if (entries == null || entries.Length != count)
        {
            Progress[] resized = new Progress[count];

            for (int i = 0; i < count; i++)
            {
                // 인스펙터에서 미리 넣어둔 값이 있으면 유지한다.
                resized[i] = (entries != null && i < entries.Length && entries[i] != null)
                    ? entries[i]
                    : new Progress();
            }

            entries = resized;
        }
    }

    public Progress Get(WeaponClass weaponClass)
    {
        int index = (int)weaponClass;

        if (entries == null || index < 0 || index >= entries.Length)
        {
            return new Progress();
        }

        return entries[index];
    }

    // 해당 레벨이 보너스를 주는 마일스톤인지. UI 색 구분에 쓴다.
    public bool IsMilestone(int level)
    {
        return milestoneInterval > 0 && level % milestoneInterval == 0;
    }

    // 현재 레벨에서 다음 레벨까지 필요한 경험치.
    public float GetExpForNextLevel(WeaponClass weaponClass)
    {
        Progress p = Get(weaponClass);

        if (p.level >= maxLevel)
        {
            return 0f;
        }

        return expBase * Mathf.Pow(p.level, expCurve);
    }

    public float GetExpRatio(WeaponClass weaponClass)
    {
        float need = GetExpForNextLevel(weaponClass);

        if (need <= 0f)
        {
            return 1f;
        }

        return Mathf.Clamp01(Get(weaponClass).exp / need);
    }

    // 레벨에 따른 사격 반동 배율. 1레벨이면 1.0, 최대 레벨이면 recoilMultiplierAtMax.
    public float GetRecoilMultiplier(WeaponClass weaponClass)
    {
        if (maxLevel <= 1)
        {
            return 1f;
        }

        float t = (Get(weaponClass).level - 1f) / (maxLevel - 1f);
        return Mathf.Lerp(1f, recoilMultiplierAtMax, Mathf.Clamp01(t));
    }

    // 적 처치 시 호출한다.
    public void AddExp(WeaponClass weaponClass, float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        Progress p = Get(weaponClass);

        if (p.level >= maxLevel)
        {
            return;
        }

        p.exp += amount;

        // 한 번에 여러 레벨이 오를 수 있으므로 반복 처리한다.
        while (p.level < maxLevel)
        {
            float need = expBase * Mathf.Pow(p.level, expCurve);

            if (p.exp < need)
            {
                break;
            }

            p.exp -= need;
            p.level++;

            if (OnLevelUp != null)
            {
                OnLevelUp(weaponClass, p.level);
            }
        }

        if (p.level >= maxLevel)
        {
            p.exp = 0f;
        }
    }
}