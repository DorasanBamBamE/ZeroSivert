using System.Collections.Generic;
using UnityEngine;

// 탈출 지점 표식 - 원작의 붉은 발광 플레어.
//
// 원작에서는 존 어딘가에 연막 플레어가 타오르고, 그 연기가 멀리서도 보인다.
// 그게 "저기가 탈출구다"라는 유일한 안내다. 지도를 안 열어도 방향을 알 수 있다.
//
// ★ 스프라이트를 새로 만들지 않는다.
//   불빛과 연기는 런타임에 Texture2D로 굽는다(방사형 그라디언트 하나면 된다).
//   PPU 16 픽셀 게임이라 작은 텍스처를 Point로 확대해도 감성이 유지된다.
//
// 탈출 지점(ExtractionZone)과 같은 오브젝트에 붙이거나 자식으로 둔다.
// 렌더 순서는 지면(-10)·구조물(-8)보다 위, 플레이어(0)보다 아래인 -2를 쓴다.
public class ExtractionFlare : MonoBehaviour
{
    [Header("불꽃")]
    [SerializeField] private Color flareColor = new Color32(255, 70, 40, 255);

    // 불꽃 본체 크기(유닛). 1이면 16픽셀.
    [SerializeField] private float flareSize = 1.5f;

    // 주변을 물들이는 넓은 발광. 멀리서 보이라고 크게 잡는다.
    [SerializeField] private float glowSize = 7f;

    // 초당 깜빡임 횟수. 원작 플레어는 불규칙하게 흔들린다.
    [SerializeField] private float flickerSpeed = 11f;
    [SerializeField] private float flickerAmount = 0.22f;

    [Header("연기")]
    [SerializeField] private Color smokeColor = new Color32(190, 190, 195, 255);
    [SerializeField] private int smokeCount = 7;
    [SerializeField] private float smokeRiseSpeed = 1.3f;
    [SerializeField] private float smokeLife = 3.2f;
    [SerializeField] private float smokeStartSize = 0.7f;
    [SerializeField] private float smokeEndSize = 2.6f;
    [SerializeField] private float smokeDrift = 0.35f;

    [Header("렌더")]
    [SerializeField] private int sortingOrder = -2;

    // 연기가 위로 흐르는 방향. 바람이 있는 것처럼 살짝 기울여 둔다.
    [SerializeField] private Vector2 windDirection = new Vector2(0.25f, 1f);

    private static Sprite dotSprite;

    private SpriteRenderer flare;
    private SpriteRenderer glow;
    private readonly List<SpriteRenderer> smoke = new List<SpriteRenderer>();
    private readonly List<float> smokeAge = new List<float>();

    private float seed;

    private void Awake()
    {
        seed = Random.value * 100f;
        Build();
    }

    private void OnDestroy()
    {
        // dotSprite는 static이라 여기서 지우지 않는다.
        // 씬을 오갈 때마다 다시 구우면 낭비다.
    }

    // ───────────────── 만들기 ─────────────────

    private static Sprite GetDot()
    {
        if (dotSprite != null)
        {
            return dotSprite;
        }

        // 32x32 방사형 그라디언트. 가운데가 희고 밖으로 갈수록 투명해진다.
        const int N = 32;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] buffer = new Color[N * N];
        float half = (N - 1) * 0.5f;

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                // 1 - d^2 로 떨어뜨리면 가장자리가 부드럽다.
                float a = Mathf.Clamp01(1f - d);
                a = a * a;

                buffer[y * N + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(buffer);
        tex.Apply(false);

        dotSprite = Sprite.Create(tex, new Rect(0f, 0f, N, N), new Vector2(0.5f, 0.5f), 16f);
        dotSprite.name = "FlareDot";
        return dotSprite;
    }

    private SpriteRenderer MakePiece(string name, Color color, float size, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetDot();
        sr.color = color;
        sr.sortingOrder = order;

        // 스프라이트가 PPU 16 · 32px이므로 기본 크기는 2유닛이다.
        float s = size / 2f;
        go.transform.localScale = new Vector3(s, s, 1f);

        return sr;
    }

    private void Build()
    {
        Color glowColor = flareColor;
        glowColor.a = 0.22f;
        glow = MakePiece("Flare_Glow", glowColor, glowSize, sortingOrder);

        flare = MakePiece("Flare_Core", flareColor, flareSize, sortingOrder + 1);

        for (int i = 0; i < smokeCount; i++)
        {
            Color c = smokeColor;
            c.a = 0f;

            SpriteRenderer sr = MakePiece("Smoke_" + i, c, smokeStartSize, sortingOrder + 2);
            smoke.Add(sr);

            // 처음부터 한 줄로 뭉쳐 오르지 않도록 나이를 흩어 둔다.
            smokeAge.Add(smokeLife * (i / (float)Mathf.Max(1, smokeCount)));
        }
    }

    // ───────────────── 매 프레임 ─────────────────

    private void Update()
    {
        // 탈출 중에는 시간이 멈추지 않지만, 인벤토리·지도를 열면 timeScale이 0이 된다.
        // 그때도 플레어는 살아 있어야 자연스러워서 unscaled를 쓴다.
        float dt = Time.unscaledDeltaTime;
        float t = Time.unscaledTime;

        UpdateFlare(t);
        UpdateSmoke(dt);
    }

    private void UpdateFlare(float t)
    {
        // 서로 다른 주파수 두 개를 겹쳐 불규칙하게 만든다.
        float n = Mathf.Sin((t + seed) * flickerSpeed)
                  * 0.6f
                  + Mathf.Sin((t + seed) * flickerSpeed * 2.37f) * 0.4f;

        float k = 1f + n * flickerAmount;

        if (flare != null)
        {
            float s = (flareSize * k) / 2f;
            flare.transform.localScale = new Vector3(s, s, 1f);

            Color c = flareColor;
            c.a = Mathf.Clamp01(0.85f + n * 0.15f);
            flare.color = c;
        }

        if (glow != null)
        {
            float s = (glowSize * (1f + n * flickerAmount * 0.5f)) / 2f;
            glow.transform.localScale = new Vector3(s, s, 1f);

            Color c = flareColor;
            c.a = Mathf.Clamp01(0.22f + n * 0.06f);
            glow.color = c;
        }
    }

    private void UpdateSmoke(float dt)
    {
        Vector2 wind = windDirection.sqrMagnitude > 0.0001f
            ? windDirection.normalized
            : Vector2.up;

        for (int i = 0; i < smoke.Count; i++)
        {
            SpriteRenderer sr = smoke[i];

            if (sr == null)
            {
                continue;
            }

            float age = smokeAge[i] + dt;

            if (age >= smokeLife)
            {
                age -= smokeLife;
            }

            smokeAge[i] = age;

            float u = age / smokeLife;                 // 0 → 1

            // 위치 — 바람 방향으로 오르며 좌우로 흔들린다.
            float sway = Mathf.Sin((age + i * 1.7f) * 2.1f) * smokeDrift * u;
            Vector2 offset = wind * (smokeRiseSpeed * age)
                             + new Vector2(-wind.y, wind.x) * sway;

            sr.transform.localPosition = new Vector3(offset.x, offset.y, 0f);

            // 크기 — 오르면서 퍼진다.
            float size = Mathf.Lerp(smokeStartSize, smokeEndSize, u) / 2f;
            sr.transform.localScale = new Vector3(size, size, 1f);

            // 투명도 — 났다가 사라진다. 처음 15%는 짙어지고 나머지는 옅어진다.
            float a = (u < 0.15f)
                ? Mathf.InverseLerp(0f, 0.15f, u)
                : 1f - Mathf.InverseLerp(0.15f, 1f, u);

            Color c = smokeColor;
            c.a = Mathf.Clamp01(a) * 0.45f;
            sr.color = c;
        }
    }
}
