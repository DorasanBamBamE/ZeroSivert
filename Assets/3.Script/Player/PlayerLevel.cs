using System;
using UnityEngine;

// 플레이어 레벨과 경험치. PDA 통계 탭 상단 바에 대응한다.
//
// 원작 표기: "현재: 368  /  다음 레벨: 500"
// 즉 현재 레벨 안에서의 누적 경험치와 다음 레벨까지 필요한 총량을 보여준다.
//
// 총기 숙련도(WeaponMastery)와는 별개 시스템이다.
// 숙련도는 무기 종류별, 이쪽은 캐릭터 전체.
//
// 씬을 넘나들어도 유지되어야 하므로 DontDestroyOnLoad로 살려둔다.
public class PlayerLevel : MonoBehaviour
{
    private static PlayerLevel instance;

    public static PlayerLevel Instance
    {
        get { return instance; }
    }

    [SerializeField] private string hunterName = "Unknown Hunter";

    [SerializeField] private int level = 1;
    [SerializeField] private int currentExp;

    // 레벨 n → n+1에 필요한 경험치 = expBase + expStep * (n - 1)
    [SerializeField] private int expBase = 500;
    [SerializeField] private int expStep = 250;

    // 이번 사냥에서 얻은 경험치. 귀환 시 최고 기록 판정에 쓴다.
    private int raidExp;

    public event Action<int> OnLevelUp;

    public string HunterName
    {
        get { return hunterName; }
    }

    public int Level
    {
        get { return level; }
    }

    public int CurrentExp
    {
        get { return currentExp; }
    }

    // 11 - 이번 출격에서 번 경험치. 결과 화면의 "+10"이 이 값이다.
    public int RaidExp
    {
        get { return raidExp; }
    }

    public int ExpForNextLevel
    {
        get { return expBase + expStep * (level - 1); }
    }

    public float ExpRatio
    {
        get
        {
            int need = ExpForNextLevel;
            return need > 0 ? Mathf.Clamp01((float)currentExp / need) : 0f;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AddExp(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentExp += amount;
        raidExp += amount;

        if (GameStats.Instance != null)
        {
            GameStats.Instance.Add(GameStats.StatId.ExpTotal, amount);
        }

        // 한 번에 여러 레벨이 오를 수 있다.
        while (currentExp >= ExpForNextLevel)
        {
            currentExp -= ExpForNextLevel;
            level++;

            if (OnLevelUp != null)
            {
                OnLevelUp(level);
            }
        }
    }

    // 존에 진입할 때 호출한다.
    public void BeginRaid()
    {
        raidExp = 0;

        if (GameStats.Instance != null)
        {
            GameStats.Instance.Add(GameStats.StatId.RaidsTotal);
        }
    }

    // 탈출에 성공했을 때 호출한다. 사망 시에는 호출하지 않는다.
    public void EndRaidSurvived()
    {
        if (GameStats.Instance == null)
        {
            return;
        }

        GameStats.Instance.Add(GameStats.StatId.RaidsSurvived);
        GameStats.Instance.ReportMax(GameStats.StatId.ExpBestRaid, raidExp);
    }
}