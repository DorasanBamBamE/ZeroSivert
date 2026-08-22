using System;
using UnityEngine;

// 플레이 기록 누적 카운터. 원작 PDA 통계 탭에 대응한다.
//
// 씬을 넘나들어도 값이 유지되어야 하므로 DontDestroyOnLoad로 살려둔다.
// Hub ↔ Zone 전환(05번 작업)에서 이 구조가 그대로 쓰인다.
//
// 새 항목이 필요하면 StatId에 추가하고 PDA 목록에 라벨만 등록하면 된다.
// enum 순서를 중간에 바꾸면 저장된 값이 어긋나므로 항상 끝에 추가할 것.
public class GameStats : MonoBehaviour
{
    public enum StatId
    {
        // 사냥
        RaidsTotal,
        RaidsSurvived,
        QuestsCompleted,

        // 노획
        ContainersOpened,
        ItemsLooted,

        // 처치 — 돌연변이
        MutantKillsTotal,
        KillZombie,
        KillWolf,

        // 처치 — 인간
        HumanKillsTotal,
        KillBandit,

        // 기록
        ExpTotal,
        ExpBestRaid,
        ShotsFired,
        DistanceTraveled,
    }

    private static GameStats instance;

    public static GameStats Instance
    {
        get { return instance; }
    }

    // 인스펙터에서 현재 값을 확인할 수 있게 배열로 들고 있다.
    [SerializeField] private int[] values;

    // 값이 바뀌면 알린다. UI가 매 프레임 폴링하지 않도록 하기 위한 것.
    public event Action<StatId, int> OnStatChanged;

    private void Awake()
    {
        // 씬 전환 시 중복 생성을 막는다.
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        int count = Enum.GetValues(typeof(StatId)).Length;

        if (values == null || values.Length != count)
        {
            values = new int[count];
        }
    }

    public int Get(StatId id)
    {
        int index = (int)id;

        if (values == null || index < 0 || index >= values.Length)
        {
            return 0;
        }

        return values[index];
    }

    public void Add(StatId id, int amount = 1)
    {
        int index = (int)id;

        if (values == null || index < 0 || index >= values.Length)
        {
            return;
        }

        values[index] += amount;

        if (OnStatChanged != null)
        {
            OnStatChanged(id, values[index]);
        }
    }

    // 최고 기록처럼 더하지 않고 큰 값만 남기는 항목에 쓴다.
    public void ReportMax(StatId id, int candidate)
    {
        int index = (int)id;

        if (values == null || index < 0 || index >= values.Length)
        {
            return;
        }

        if (candidate <= values[index])
        {
            return;
        }

        values[index] = candidate;

        if (OnStatChanged != null)
        {
            OnStatChanged(id, candidate);
        }
    }

    // 처치 기록은 총합과 세부 항목을 함께 올린다.
    public void ReportKill(StatId detail, bool isHuman)
    {
        Add(detail);
        Add(isHuman ? StatId.HumanKillsTotal : StatId.MutantKillsTotal);
    }

    // 테스트용. 인스펙터 컨텍스트 메뉴에서 호출할 수 있다.
    [ContextMenu("모든 기록 초기화")]
    public void ResetAll()
    {
        if (values == null)
        {
            return;
        }

        for (int i = 0; i < values.Length; i++)
        {
            values[i] = 0;
        }
    }
}