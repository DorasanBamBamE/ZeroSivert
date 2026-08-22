using System.Collections.Generic;
using UnityEngine;

// 씬을 넘어가도 유지되는 한 판(레이드)의 데이터.
// 플레이어 오브젝트 자체를 DontDestroyOnLoad로 넘기면 카메라·캔버스 참조가
// 전부 끊기므로, 수치만 스냅샷으로 들고 다니고 플레이어는 씬마다 새로 만든다.
//
// ★ 07번 변경점 ─────────────────────────────────────────
// PlayerSnapshot에 inventory를 추가하고, Save/Load에서 InventoryController를
// 직접 찾아 쓴다. 시그니처를 바꾸지 않았으므로 ZoneEntryPoint / ExtractionZone /
// PlayerPersistence는 손댈 필요가 없다.
//
// 사망 시 인벤토리 소실은 따로 구현하지 않는다. ResetSnapshot()이
// snapshot을 통째로 새로 만들기 때문에 자동으로 함께 사라진다.
// ────────────────────────────────────────────────────────
public class RunData : MonoBehaviour
{
    public enum Outcome
    {
        None,
        Extracted,   // 탈출 성공
        Died,        // 사망
    }

    // 씬을 넘길 값들.
    [System.Serializable]
    public class PlayerSnapshot
    {
        public float health = 100f;
        public float stamina = 100f;
        public float energy = 100f;
        public float hunger = 100f;
        public float thirst = 100f;
        public float radiation = 0f;
        public bool bleeding = false;
        public float bleedTimer = 0f;

        // 07번 추가. ItemData는 프로젝트 에셋이라 씬을 넘어도 살아 있으므로
        // SO 참조를 그대로 담아도 안전하다 (디스크 직렬화가 아니라 런타임 유지).
        public List<InventorySlotData> inventory = new List<InventorySlotData>();

        // 07-C 추가. 장비 4칸(무기1 · 무기2 · 방탄복 · 배낭) + 들고 있던 무기 번호.
        public List<ItemData> equipment = new List<ItemData>();
        public int activeWeapon = 0;
    }

    private static RunData instance;

    // 씬에 없으면 자동으로 만든다. 허브 씬부터 시작하지 않아도 동작한다.
    public static RunData Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<RunData>();
            }

            if (instance == null)
            {
                GameObject go = new GameObject("RunData");
                instance = go.AddComponent<RunData>();
            }

            return instance;
        }
    }

    [SerializeField] private PlayerSnapshot snapshot = new PlayerSnapshot();

    private bool hasSnapshot;
    private Outcome lastOutcome = Outcome.None;
    private int raidCount;

    public PlayerSnapshot Snapshot { get { return snapshot; } }
    public bool HasSnapshot { get { return hasSnapshot; } }
    public Outcome LastOutcome { get { return lastOutcome; } }
    public int RaidCount { get { return raidCount; } }

    private void Awake()
    {
        // 씬에 이미 하나 있으면 중복을 버린다.
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // 존으로 떠나기 직전 호출. 허브에서의 상태를 그대로 들고 간다.
    public void Save(PlayerStats stats)
    {
        if (stats == null)
        {
            return;
        }

        stats.CaptureTo(snapshot);

        // 인벤토리는 PlayerStats와 같은 오브젝트에 있다.
        // 이렇게 찾으면 호출부(ZoneEntryPoint, ExtractionZone)를 안 건드려도 된다.
        InventoryController inv = stats.GetComponent<InventoryController>();

        if (inv != null)
        {
            inv.CaptureTo(snapshot);
        }

        EquipmentController eq = stats.GetComponent<EquipmentController>();

        if (eq != null)
        {
            eq.CaptureTo(snapshot);
        }

        hasSnapshot = true;
    }

    // 씬 진입 후 플레이어에게 되돌린다.
    public void Load(PlayerStats stats)
    {
        if (stats == null || !hasSnapshot)
        {
            return;
        }

        stats.RestoreFrom(snapshot);

        InventoryController inv = stats.GetComponent<InventoryController>();

        if (inv != null)
        {
            inv.RestoreFrom(snapshot);
        }

        EquipmentController eq = stats.GetComponent<EquipmentController>();

        if (eq != null)
        {
            eq.RestoreFrom(snapshot);
        }
    }

    // 사망 시. 다음 판은 새 몸으로 시작한다.
    // snapshot을 통째로 갈아치우므로 인벤토리도 여기서 함께 사라진다.
    public void ResetSnapshot()
    {
        snapshot = new PlayerSnapshot();
        hasSnapshot = false;
    }

    public void SetOutcome(Outcome outcome)
    {
        lastOutcome = outcome;
    }

    public void BeginRaid()
    {
        raidCount++;
        lastOutcome = Outcome.None;
    }
}