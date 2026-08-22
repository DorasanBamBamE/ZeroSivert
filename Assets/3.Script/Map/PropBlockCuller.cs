using UnityEngine;

// 나무·풀 블록 컬링.
//
// PropScatter가 프롭을 20,000개 가까이 뿌리면서 생긴 부하를 줄인다.
// 프롭 하나하나를 켜고 끄면 그 판정 자체가 더 비싸므로,
// PropScatter가 미리 20유닛짜리 블록 부모로 묶어 두고 여기서는 블록만 켜고 끈다.
//
// 화면 밖 스프라이트는 어차피 그려지지 않지만, 렌더러가 살아 있는 한
// 컬링·정렬 비용은 매 프레임 든다. 20,000개면 그 비용이 무시할 수 없다.
// GameObject를 통째로 끄면 렌더러도 콜라이더도 목록에서 빠진다.
//
// ★ 여유(margin)를 넉넉히 둔다.
//   나무에는 콜라이더가 있다. 화면 딱 맞게 자르면 화면 밖에서 걸어오던 적이
//   나무를 통과해 버린다. 화면보다 한참 넓게 잡아야 그 티가 안 난다.
public class PropBlockCuller : MonoBehaviour
{
    [Tooltip("화면 밖으로 이만큼 더 켜 둔다(유닛).")]
    [SerializeField] private float margin = 18f;

    [Tooltip("몇 초마다 다시 판정할지. 0이면 매 프레임.")]
    [SerializeField] private float interval = 0.15f;

    [SerializeField] private Camera cam;

    private Transform[] blocks;
    private Vector2[] centers;
    private float halfExtent;
    private bool[] active;
    private float timer;
    private bool ready;

    // PropScatter가 흩뿌린 뒤 불러 준다.
    public void Init(Transform[] blockList, Vector2[] blockCenters, float blockSize, float viewMargin)
    {
        margin = viewMargin;
        blocks = blockList;
        centers = blockCenters;
        halfExtent = blockSize * 0.5f;

        active = new bool[blocks.Length];

        for (int i = 0; i < active.Length; i++)
        {
            active[i] = blocks[i] != null && blocks[i].gameObject.activeSelf;
        }

        ready = blocks.Length > 0;
        timer = 0f;
    }

    public int BlockCount
    {
        get { return blocks == null ? 0 : blocks.Length; }
    }

    public int ActiveCount
    {
        get
        {
            if (active == null)
            {
                return 0;
            }

            int n = 0;

            for (int i = 0; i < active.Length; i++)
            {
                if (active[i]) n++;
            }

            return n;
        }
    }

    private void LateUpdate()
    {
        if (!ready)
        {
            return;
        }

        timer -= Time.deltaTime;

        if (timer > 0f)
        {
            return;
        }

        timer = interval;

        if (cam == null)
        {
            cam = Camera.main;
        }

        if (cam == null || !cam.orthographic)
        {
            return;
        }

        float halfH = cam.orthographicSize + margin + halfExtent;
        float halfW = cam.orthographicSize * cam.aspect + margin + halfExtent;

        Vector2 c = cam.transform.position;

        for (int i = 0; i < blocks.Length; i++)
        {
            if (blocks[i] == null)
            {
                continue;
            }

            Vector2 d = centers[i] - c;
            bool on = Mathf.Abs(d.x) <= halfW && Mathf.Abs(d.y) <= halfH;

            if (on != active[i])
            {
                active[i] = on;
                blocks[i].gameObject.SetActive(on);
            }
        }
    }
}
