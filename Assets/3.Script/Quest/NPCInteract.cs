using UnityEngine;

// 벙커에 서 있는 NPC. 가까이 가서 E를 누르면 대화창이 열린다.
//
// 08의 LootContainer와 완전히 같은 방식이다 — 트리거 진입으로 inRange를 잡고
// Update에서 키를 본다. 같은 패턴을 쓰는 이유는 상자와 NPC가 나란히 있어도
// 동작이 예측 가능해야 하기 때문이다.
//
// ★ 상자와 NPC가 겹치면 둘 다 E에 반응한다. 벙커에는 상자를 두지 않거나,
//   둔다면 콜라이더 반경이 겹치지 않게 배치할 것.
[RequireComponent(typeof(Collider2D))]
public class NPCInteract : MonoBehaviour
{
    [Header("정체")]
    [SerializeField] private NPCData data;

    [Header("입력")]
    [SerializeField] private KeyCode talkKey = KeyCode.E;

    // "E — 대화" 안내. 없으면 비워둬도 된다.
    [SerializeField] private GameObject prompt;

    // 비워두면 런타임에 찾는다. 씬에 대화창이 하나뿐이면 비워둬도 된다.
    [SerializeField] private DialogueUI dialogue;

    // 10번 — 이 NPC가 상인이면 여기 붙어 있다. 없으면 null.
    private ShopController shop;
    private ZoneEntryPoint departure;

    private bool inRange;

    public NPCData Data
    {
        get { return data; }
    }

private void Awake()
    {
        if (prompt != null)
        {
            prompt.SetActive(false);
        }

        shop = GetComponent<ShopController>();
        departure = GetComponentInChildren<ZoneEntryPoint>(true);
    }

    private void OnDisable()
    {
        if (prompt != null)
        {
            prompt.SetActive(false);
        }

        inRange = false;
    }

    private void Update()
    {
        if (!inRange || data == null)
        {
            return;
        }

        // 다른 창이 열려 있으면 E를 먹지 않는다.
        // 대화창 자체가 timeScale을 0으로 만들므로 이 조건이 재입력도 막아준다.
        if (UIBlocker.Any || InventoryScreen.IsOpen || Time.timeScale == 0f)
        {
            return;
        }

        if (Input.GetKeyDown(talkKey))
        {
            Talk();
        }
    }

public void Talk()
    {
        if (dialogue == null)
        {
            dialogue = FindFirstObjectByType<DialogueUI>(FindObjectsInactive.Include);
        }

        if (dialogue == null)
        {
            Debug.LogWarning("[NPC] 씬에 DialogueUI가 없다. " + name, this);
            return;
        }

        dialogue.Open(data, shop, departure);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        inRange = true;

        if (prompt != null)
        {
            prompt.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        inRange = false;

        if (prompt != null)
        {
            prompt.SetActive(false);
        }

        // 대화 중에 멀어지는 일은 없다 — 대화창이 열리면 시간이 멈추고
        // 플레이어가 움직이지 못한다. 그래도 방어적으로 닫아둔다.
        if (dialogue != null && dialogue.Current == data)
        {
            dialogue.Close();
        }
    }
}
