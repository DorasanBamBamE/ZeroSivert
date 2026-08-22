using System.Collections.Generic;
using UnityEngine;

// 퀘스트 수락 · 진행 · 완료와 세력 평판을 관리한다.
// RunData·GameClock과 같은 패턴 — 씬을 넘어 살아남고, 없으면 스스로 만들어진다.
//
// ★ 사망해도 퀘스트는 유지된다.
// RunData.ResetSnapshot()은 인벤토리와 스탯만 지운다. 퀘스트 진행은 여기 따로 있다.
// 다만 수집형은 아이템이 사라지므로 진행도가 자동으로 0으로 돌아간다 —
// 인벤토리를 매번 세는 방식이라 코드 없이 공짜로 얻는 동작이다.
public class QuestManager : MonoBehaviour
{
    private static QuestManager instance;

    public static QuestManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<QuestManager>();
            }

            if (instance == null)
            {
                GameObject go = new GameObject("QuestManager");
                instance = go.AddComponent<QuestManager>();
            }

            return instance;
        }
    }

    // ★ Instance는 없으면 만든다. 그래서 OnDisable·OnDestroy에서 쓰면 안 된다 —
    //   게임을 끄는 중에 새 오브젝트가 생겨서 "씬을 나갈 때 오브젝트를 만들 수
    //   없습니다" 경고가 뜬다. 구독 해제 같은 정리 코드는 이쪽을 본다.
    public static bool Exists
    {
        get { return instance != null; }
    }

    // 수락 · 완료 · 진행 변화 시 알린다. QuestListUI와 DialogueUI가 구독한다.
    public event System.Action Changed;

    private readonly List<QuestState> active = new List<QuestState>();

    // 한 번 끝내면 다시 못 받는 퀘스트 (메인 스토리 · 의사 의뢰).
    private readonly List<QuestData> completed = new List<QuestData>();

    // 일일 임무는 하루가 지나면 다시 받을 수 있다.
    private readonly List<QuestData> completedToday = new List<QuestData>();

    // 세력 평판. 인덱스는 Faction 값과 같다.
    private readonly int[] reputation = new int[3];

    // 일일 임무 갱신 추적
    private int dailyDay = -1;
    private readonly List<QuestData> dailyOffers = new List<QuestData>();

    private InventoryController inventory;

    public IReadOnlyList<QuestState> Active { get { return active; } }

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

    private void NotifyChanged()
    {
        if (Changed != null)
        {
            Changed();
        }
    }

    // 플레이어는 씬마다 새로 만들어지므로 매번 확인한다.
    private InventoryController Inventory
    {
        get
        {
            if (inventory == null)
            {
                PlayerStats stats = FindFirstObjectByType<PlayerStats>();

                if (stats != null)
                {
                    inventory = stats.GetComponent<InventoryController>();
                }
            }

            return inventory;
        }
    }

    // ───────────────── 조회 ─────────────────

    public bool IsAccepted(QuestData q)
    {
        return FindState(q) != null;
    }

    public bool IsCompleted(QuestData q)
    {
        if (q == null)
        {
            return false;
        }

        if (q.isDaily)
        {
            return completedToday.Contains(q);
        }

        return completed.Contains(q);
    }

    private QuestState FindState(QuestData q)
    {
        if (q == null)
        {
            return null;
        }

        for (int i = 0; i < active.Count; i++)
        {
            if (active[i].quest == q)
            {
                return active[i];
            }
        }

        return null;
    }

    // 현재 진행 수.
    public int GetProgress(QuestData q)
    {
        QuestState s = FindState(q);

        if (s == null || q == null)
        {
            return 0;
        }

        if (q.type == QuestType.Collect)
        {
            // 저장하지 않고 매번 센다. 죽어서 아이템을 잃으면 자동으로 줄어든다.
            if (Inventory == null || q.targetItem == null)
            {
                return 0;
            }

            return Mathf.Min(Inventory.CountOf(q.targetItem), q.targetCount);
        }

        return Mathf.Min(s.killCount, q.targetCount);
    }

    public bool CanTurnIn(QuestData q)
    {
        if (q == null || !IsAccepted(q))
        {
            return false;
        }

        return GetProgress(q) >= q.targetCount;
    }

    public int GetReputation(Faction f)
    {
        int i = (int)f;
        return (i >= 0 && i < reputation.Length) ? reputation[i] : 0;
    }

    // ───────────────── 수락 · 완료 ─────────────────

    public bool Accept(QuestData q)
    {
        if (q == null || IsAccepted(q) || IsCompleted(q))
        {
            return false;
        }

        active.Add(new QuestState(q));
        NotifyChanged();
        return true;
    }

    public bool TurnIn(QuestData q)
    {
        if (!CanTurnIn(q))
        {
            return false;
        }

        QuestState s = FindState(q);

        // ★ 수집형은 아이템을 실제로 넘긴다. 원작과 같다 — 들고만 있으면
        //   완료 표시가 뜨지만, 넘기는 순간 인벤토리에서 사라진다.
        //   보상보다 먼저 회수해야 한다. 안 그러면 보상 아이템이 들어와
        //   자리가 꽉 찬 상태에서 회수가 일어난다.
        if (q.type == QuestType.Collect && q.targetItem != null && Inventory != null)
        {
            Inventory.RemoveCount(q.targetItem, q.targetCount);
        }

        active.Remove(s);

        if (q.isDaily)
        {
            completedToday.Add(q);
        }
        else
        {
            completed.Add(q);
        }

        GrantRewards(q);
        NotifyChanged();
        return true;
    }

    private void GrantRewards(QuestData q)
    {
        // 세력 평판
        int fi = (int)q.faction;

        if (fi > 0 && fi < reputation.Length)
        {
            reputation[fi] += q.rewardReputation;
        }

        // 보상 아이템 — 08의 LootTable을 그대로 쓴다.
        if (q.rewardItems != null && Inventory != null)
        {
            q.rewardItems.RollInto(Inventory);
        }

        // 경험치
        GrantExp(q.rewardExp);

        // 루블 — 10번에서 연결됐다.
        if (q.rewardRubles > 0)
        {
            Wallet.Instance.Earn(q.rewardRubles);
        }

        Debug.Log("[Quest] 완료: " + q.title
                  + " / EXP " + q.rewardExp
                  + " / 루블 +" + q.rewardRubles
                  + " / " + q.faction + " 평판 +" + q.rewardReputation, this);
    }

    // ★ 여기가 09에서 유일하게 비어 있는 칸이다.
    //
    // 프로젝트의 PlayerLevel(또는 경험치를 들고 있는 스크립트)이 어떤 이름의
    // 메서드를 쓰는지 몰라서 일부러 타입 참조를 걸지 않았다. 없는 타입을
    // 적어두면 프로젝트 전체가 컴파일이 안 되기 때문이다.
    //
    // 채우는 법 — 아래 두 줄의 주석을 풀고 메서드 이름만 맞춘다.
    //
    //   private PlayerLevel playerLevel;
    //   ...
    //   if (playerLevel == null) playerLevel = FindFirstObjectByType<PlayerLevel>();
    //   if (playerLevel != null) playerLevel.AddExp(amount);
    //
    // 비워둔 상태로도 평판·보상 아이템·수집품 회수는 전부 정상 동작한다.
    private void GrantExp(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        // PlayerLevel은 싱글톤이고 씬에 없으면 스스로 만들어지지 않는다.
        // 없을 때는 조용히 넘어간다 — 경험치만 안 들어갈 뿐 나머지는 정상이다.
        if (PlayerLevel.Instance != null)
        {
            PlayerLevel.Instance.AddExp(amount);
        }
    }

    // ───────────────── 처치 보고 ─────────────────

    // EnemyHealth가 사망 처리에서 부른다.
    // enemyId는 적 종류 이름. QuestData.targetEnemyId와 대조한다.
    public void ReportKill(string enemyId)
    {
        bool touched = false;

        for (int i = 0; i < active.Count; i++)
        {
            QuestState s = active[i];

            if (s.quest == null || s.quest.type != QuestType.Kill)
            {
                continue;
            }

            // targetEnemyId가 비어 있으면 아무 적이나 센다.
            string want = s.quest.targetEnemyId;

            if (!string.IsNullOrEmpty(want) && want != enemyId)
            {
                continue;
            }

            if (s.killCount < s.quest.targetCount)
            {
                s.killCount++;
                touched = true;
            }
        }

        if (touched)
        {
            NotifyChanged();
        }
    }

    // ───────────────── 일일 임무 ─────────────────

    // 네트워커가 오늘 발주한 임무 목록.
    // GameClock의 날짜가 바뀌면 자동으로 새로 뽑는다.
    public IReadOnlyList<QuestData> GetDailyOffers(QuestData[] pool, int count)
    {
        int today = GameClock.Instance != null ? GameClock.Instance.Day : 0;

        if (today != dailyDay)
        {
            dailyDay = today;
            completedToday.Clear();
            RollDaily(pool, count);
            NotifyChanged();
        }
        else if (dailyOffers.Count == 0)
        {
            RollDaily(pool, count);
        }

        return dailyOffers;
    }

    private void RollDaily(QuestData[] pool, int count)
    {
        dailyOffers.Clear();

        if (pool == null || pool.Length == 0)
        {
            return;
        }

        // 중복 없이 뽑는다.
        List<QuestData> bag = new List<QuestData>();

        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i] != null)
            {
                bag.Add(pool[i]);
            }
        }

        int take = Mathf.Min(count, bag.Count);

        for (int i = 0; i < take; i++)
        {
            int pick = Random.Range(0, bag.Count);
            dailyOffers.Add(bag[pick]);
            bag.RemoveAt(pick);
        }
    }
}
