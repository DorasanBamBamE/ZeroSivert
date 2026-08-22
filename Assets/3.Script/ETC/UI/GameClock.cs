using UnityEngine;

// 게임 내 시각. 씬을 넘어가도 계속 흐른다.
//
// ★ 구조 변경 (2026-08-17)
// 예전에는 이 스크립트가 씬 안의 Text를 직접 들고 갱신했다. 그러면
// DontDestroyOnLoad로 씬을 넘기는 순간 그 Text 참조가 끊긴다.
// 플레이어를 씬 너머로 넘기지 않고 스냅샷만 들고 다니는 것과 같은 이유다.
//
// 그래서 방향을 뒤집었다.
//   GameClock  — 시각만 들고 씬을 넘어 살아남는다. UI를 모른다
//   ClockLabel — 각 씬의 Text가 GameClock.Instance를 찾아가 자기를 갱신한다
//
// ★ 09 추가 (2026-08-18)
// Day를 센다. 네트워커의 일일 임무가 이 값이 바뀌는 것을 보고 갱신된다.
// 예전에는 자정에 totalMinutes를 1440 빼는 것으로 끝냈는데, 그러면
// "하루가 지났다"는 사실이 어디에도 남지 않았다. 이제 셀 때마다 Day가 오른다.
//
// PDA나 인벤토리가 열리면 Time.timeScale이 0이 되므로 시간도 멈춘다.
// unscaledDeltaTime을 쓰지 않는 건 의도된 것이다 — 원작과 같은 동작이다.
public class GameClock : MonoBehaviour
{
    private static GameClock instance;

    // 씬에 없으면 자동으로 만든다. 어느 씬에서 시작해도 시간이 흐른다.
    public static GameClock Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<GameClock>();
            }

            if (instance == null)
            {
                GameObject go = new GameObject("GameClock");
                instance = go.AddComponent<GameClock>();
            }

            return instance;
        }
    }

    // 실시간 1초당 흐르는 게임 내 분. 60이면 실시간 1초 = 게임 1시간.
    [SerializeField] private float minutesPerSecond = 1f;

    [SerializeField] private int startHour = 12;
    [SerializeField] private int startMinute = 0;

    // 시작 날짜. 1일차부터 센다.
    [SerializeField] private int startDay = 1;

    private float totalMinutes;
    private int day;

    // 다른 시스템에서 밤낮 판정에 쓸 수 있다.
    public int Hour
    {
        get { return Mathf.FloorToInt(totalMinutes / 60f) % 24; }
    }

    public int Minute
    {
        get { return Mathf.FloorToInt(totalMinutes) % 60; }
    }

    // ★ 09에서 추가. 일일 임무 갱신의 기준.
    public int Day
    {
        get { return day; }
    }

    public bool IsNight
    {
        get { return Hour < 6 || Hour >= 20; }
    }

    // "12:05" 형태. 분이 바뀔 때만 부를 것 — 매번 문자열을 새로 만든다.
    public string TimeText
    {
        get { return Hour.ToString("00") + ":" + Minute.ToString("00"); }
    }

    // "3일차 12:05" 형태. 라벨에 날짜까지 띄우고 싶으면 이걸 쓴다.
    public string DateTimeText
    {
        get { return day + "일차 " + TimeText; }
    }

    private void Awake()
    {
        // 씬에 이미 하나 있으면 중복을 버린다.
        // 이 오브젝트에 PlayerLevel·GameStats가 같이 붙어 있다면 그것들도
        // 함께 버려지므로, 세 스크립트가 한 오브젝트에 모여 있어야 한다.
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        totalMinutes = startHour * 60f + startMinute;
        day = Mathf.Max(1, startDay);
    }

    private void Update()
    {
        totalMinutes += minutesPerSecond * Time.deltaTime;

        // 하루가 지나면 되감고 날짜를 올린다.
        // while인 이유 — minutesPerSecond를 크게 올려 테스트할 때 한 프레임에
        // 이틀이 지날 수 있다. if면 그때 하루를 잃는다.
        while (totalMinutes >= 1440f)
        {
            totalMinutes -= 1440f;
            day++;
        }
    }

    // 필요하면 외부에서 시각을 직접 세팅한다.
    public void SetTime(int hour, int minute)
    {
        totalMinutes = Mathf.Repeat(hour * 60f + minute, 1440f);
    }

    // 테스트용. 인스펙터 우클릭 → 하루 넘기기.
    // 네트워커에게 가면 일일 임무가 새로 뽑혀 있어야 한다.
    [ContextMenu("하루 넘기기")]
    public void SkipDay()
    {
        day++;
        Debug.Log("[GameClock] " + day + "일차", this);
    }
}
