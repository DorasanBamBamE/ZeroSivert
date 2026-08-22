using System.Collections.Generic;
using UnityEngine;

// 상자·시체가 무엇을 떨구는지 정의한다.
// Project 창 우클릭 → Create → ZeroSievert → Loot Table
//
// 굴리는 방식 — entries를 전부 순회하지 않는다.
// minRolls~maxRolls 번 반복하며 매번 하나를 무작위로 골라 chance로 판정한다.
// 그래서 같은 테이블이라도 열 때마다 결과가 달라진다.
[CreateAssetMenu(fileName = "Loot_", menuName = "ZeroSievert/Loot Table")]
public class LootTable : ScriptableObject
{
    [Header("항목")]
    public LootEntry[] entries;

    [Header("굴림 횟수")]
    // 한 번 열 때 시도할 항목 수.
    public int minRolls = 1;
    public int maxRolls = 3;

    [Header("빈 상자")]
    // 아무것도 안 나올 확률. 원작에서도 빈 상자가 나온다.
    [Range(0f, 1f)] public float emptyChance = 0f;

    public List<LootRoll> Roll()
    {
        List<LootRoll> result = new List<LootRoll>();

        if (entries == null || entries.Length == 0)
        {
            return result;
        }

        if (Random.value < emptyChance)
        {
            return result;
        }

        int lo = Mathf.Max(0, Mathf.Min(minRolls, maxRolls));
        int hi = Mathf.Max(lo, maxRolls);
        int rolls = Random.Range(lo, hi + 1);

        for (int i = 0; i < rolls; i++)
        {
            LootEntry entry = entries[Random.Range(0, entries.Length)];

            if (entry == null)
            {
                continue;
            }

            int count = entry.RollCount();

            if (count > 0)
            {
                result.Add(new LootRoll(entry.item, count));
            }
        }

        return result;
    }

    // 굴린 결과를 인벤토리에 바로 넣는다. 실제로 들어간 종류 수를 돌려준다.
    public int RollInto(InventoryController target)
    {
        if (target == null)
        {
            return 0;
        }

        List<LootRoll> rolled = Roll();
        int placed = 0;

        for (int i = 0; i < rolled.Count; i++)
        {
            if (target.TryAdd(rolled[i].item, rolled[i].count) > 0)
            {
                placed++;
            }
        }

        return placed;
    }

    private void OnValidate()
    {
        minRolls = Mathf.Max(0, minRolls);
        maxRolls = Mathf.Max(minRolls, maxRolls);
    }
}
