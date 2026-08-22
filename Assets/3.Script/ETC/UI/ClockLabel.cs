using UnityEngine;
using UnityEngine.UI;

// 시각을 표시하는 Text에 붙인다. 씬마다 하나씩.
//
// GameClock은 씬을 넘어 살아남지만 UI는 씬마다 새로 만들어진다.
// 그래서 UI 쪽이 시계를 찾아가는 방향으로 참조를 잡는다.
// GameClock이 씬 어디에도 없으면 스스로 만들어지므로 여기서 null 걱정은 없다.
//
// 붙이는 곳 — Hub와 Forest 양쪽의 PDA 상단 `Time` 오브젝트.
[RequireComponent(typeof(Text))]
public class ClockLabel : MonoBehaviour
{
    private Text label;

    // 분이 바뀔 때만 문자열을 만든다. 매 프레임 만들면 GC가 계속 발생한다.
    private int lastMinute = -1;

    private void Awake()
    {
        label = GetComponent<Text>();
    }

    private void OnEnable()
    {
        // 창을 다시 열었을 때 곧바로 최신 시각이 뜨도록 강제로 한 번 갱신한다.
        lastMinute = -1;
    }

    private void Update()
    {
        GameClock clock = GameClock.Instance;

        if (clock == null || label == null)
        {
            return;
        }

        int minute = clock.Minute;

        if (minute == lastMinute)
        {
            return;
        }

        lastMinute = minute;
        label.text = clock.TimeText;
    }
}