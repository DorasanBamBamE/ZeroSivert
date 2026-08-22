using UnityEngine;
using UnityEngine.UI;

// 루블 잔액 표시. ClockLabel과 같은 방식이다 —
// 데이터는 Wallet이 씬을 넘어 들고 있고, 각 씬의 Text가 스스로 찾아가 갱신한다.
//
// 인벤토리 창 · 대화창 · HUD 어디에 붙여도 된다.
public class WalletLabel : MonoBehaviour
{
    [SerializeField] private Text label;

    // 거래 결과 안내. 없어도 된다. 거래 중에만 켜진다.
    [SerializeField] private Text messageText;

    [SerializeField] private string format = "{0} ₽";

    private int shown = -1;

    private void Awake()
    {
        if (label == null)
        {
            label = GetComponent<Text>();
        }
    }

    private void OnEnable()
    {
        // 07 장비창에서 배운 것 — 항상 끊고 다시 잇는다.
        Wallet.Instance.Changed -= Refresh;
        Wallet.Instance.Changed += Refresh;

        ShopSession.Changed -= Refresh;
        ShopSession.Changed += Refresh;

        shown = -1;
        Refresh();
    }

    private void OnDisable()
    {
        if (Wallet.Exists)
        {
            Wallet.Instance.Changed -= Refresh;
        }

        ShopSession.Changed -= Refresh;
    }

    public void Refresh()
    {
        int now = Wallet.Instance.Rubles;

        if (label != null && now != shown)
        {
            shown = now;
            label.text = string.Format(format, now);
        }

        if (messageText != null)
        {
            bool show = ShopSession.IsOpen && !string.IsNullOrEmpty(ShopSession.LastMessage);
            messageText.gameObject.SetActive(show);

            if (show)
            {
                messageText.text = ShopSession.LastMessage;
            }
        }
    }
}
