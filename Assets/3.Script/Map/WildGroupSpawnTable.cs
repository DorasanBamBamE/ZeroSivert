using UnityEngine;

[CreateAssetMenu(
    fileName = "WildGroupSpawnTable",
    menuName = "ZeroSivert/Spawn/Wild Group Table")]
public class WildGroupSpawnTable : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string label = "Zombie";

        [Tooltip("이 그룹에서 사용할 적 프리팹. 그룹 하나는 같은 Entry 안에서 뽑는다.")]
        public GameObject[] prefabs;

        [Min(0.01f)]
        public float weight = 1f;

        [Range(1, 10)]
        public int minMembers = 3;

        [Range(1, 10)]
        public int maxMembers = 5;

        [Tooltip("그룹 중심에서 처음 흩어져 생성되는 반경")]
        [Min(0.5f)]
        public float scatterRadius = 3.5f;

        [Tooltip("그룹 중심을 기준으로 평상시에 배회하는 반경")]
        [Min(1f)]
        public float roamRadius = 7f;

        public GameObject PickPrefab()
        {
            if (prefabs == null || prefabs.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < prefabs.Length * 2; i++)
            {
                GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];

                if (prefab != null)
                {
                    return prefab;
                }
            }

            return null;
        }

        public int PickMemberCount()
        {
            int low = Mathf.Max(1, Mathf.Min(minMembers, maxMembers));
            int high = Mathf.Max(low, Mathf.Max(minMembers, maxMembers));
            return Random.Range(low, high + 1);
        }
    }

    [SerializeField] private Entry[] entries;

    public Entry[] Entries
    {
        get { return entries; }
    }

    public Entry Pick()
    {
        if (entries == null || entries.Length == 0)
        {
            return null;
        }

        float total = 0f;

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && entries[i].weight > 0f)
            {
                total += entries[i].weight;
            }
        }

        if (total <= 0f)
        {
            return null;
        }

        float roll = Random.value * total;

        for (int i = 0; i < entries.Length; i++)
        {
            Entry entry = entries[i];

            if (entry == null || entry.weight <= 0f)
            {
                continue;
            }

            roll -= entry.weight;

            if (roll <= 0f)
            {
                return entry;
            }
        }

        return entries[entries.Length - 1];
    }
}
