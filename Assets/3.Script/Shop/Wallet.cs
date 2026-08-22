using UnityEngine;

// 루블. 씬을 넘어 살아남고, 없으면 스스로 만들어진다.
// RunData · GameClock · QuestManager와 같은 패턴이다.
//
// ★ 죽어도 돈은 잃지 않는다.
// RunData.ResetSnapshot()은 인벤토리와 스탯만 지운다. 돈은 여기 따로 있다.
// 원작도 루블은 벙커에 보관되는 개념이라 출격 중 사망으로 사라지지 않는다.
// 잃게 만들고 싶으면 RunEndHandler에서 Wallet.Instance.Spend(...)를 부르면 된다.
public class Wallet : MonoBehaviour
{
    private static Wallet instance;

    public static Wallet Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<Wallet>();
            }

            if (instance == null)
            {
                GameObject go = new GameObject("Wallet");
                instance = go.AddComponent<Wallet>();
            }

            return instance;
        }
    }

    // 끄는 중에 새로 만들지 않기 위한 것. 정리 코드는 이쪽을 본다.
    public static bool Exists
    {
        get { return instance != null; }
    }

    // 잔액이 바뀌면 알린다. WalletLabel이 구독한다.
    public event System.Action Changed;

    [SerializeField] private int startRubles = 1000;

    private int rubles;
    private bool started;

    public int Rubles
    {
        get { return rubles; }
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

        if (!started)
        {
            started = true;
            rubles = Mathf.Max(0, startRubles);
        }
    }

    private void Notify()
    {
        if (Changed != null)
        {
            Changed();
        }
    }

    public bool CanAfford(int amount)
    {
        return amount <= 0 || rubles >= amount;
    }

    // 성공하면 true. 모자라면 아무것도 바꾸지 않고 false.
    public bool Spend(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (rubles < amount)
        {
            return false;
        }

        rubles -= amount;
        Notify();
        return true;
    }

    public void Earn(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        rubles += amount;
        Notify();
    }

    [ContextMenu("루블 10000 지급")]
    private void DebugGive()
    {
        Earn(10000);
        Debug.Log("[Wallet] " + rubles + " ₽", this);
    }
}
