using UnityEngine;

// 전리품 테이블의 항목 하나.
// 별도 파일로 뺀 이유는 ItemCategory와 같다 — LootTable.cs를 통째로 갈아끼울 때
// 이 클래스까지 같이 날아가는 걸 막는다.
[System.Serializable]
public class LootEntry
{
    public ItemData item;

    [Header("개수")]
    public int minCount = 1;
    public int maxCount = 1;

    [Header("확률")]
    // 이 항목이 뽑혔을 때 실제로 나올 확률. 1이면 뽑히면 무조건 나온다.
    [Range(0f, 1f)] public float chance = 0.5f;

    // 굴림 결과. 안 나오면 0.
    public int RollCount()
    {
        if (item == null || Random.value > chance)
        {
            return 0;
        }

        int lo = Mathf.Max(1, Mathf.Min(minCount, maxCount));
        int hi = Mathf.Max(lo, maxCount);

        // Random.Range(int, int)는 상한을 포함하지 않으므로 +1.
        return Random.Range(lo, hi + 1);
    }
}

// 굴림 한 번의 결과.
public class LootRoll
{
    public ItemData item;
    public int count;

    public LootRoll(ItemData item, int count)
    {
        this.item = item;
        this.count = count;
    }
}
