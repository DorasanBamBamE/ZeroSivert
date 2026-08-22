using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 존 지도. M으로 열고 닫는다.
//
// ★ 지도 데이터를 새로 만들지 않았다.
//   ZoneGenerator가 이미 문자 격자로 맵을 만들고 있다(GetSymbol / GridWidth /
//   CellSize / WorldToCell). 그 격자를 한 칸 = 한 픽셀짜리 Texture2D로 굽고
//   RawImage에 얹으면 그게 곧 지도다. 픽셀 아트 게임이라 Point 필터로 확대해도
//   원본 감성이 그대로 유지된다.
//
// 허브에는 ZoneGenerator가 없다. 그때는 "지도 없음"만 띄운다 —
// 원작도 벙커 안에서는 존 지도를 보지 않는다.
//
// ★ 계층 주의 — InventoryScreen · DialogueUI와 같다.
//   이 스크립트는 항상 켜져 있는 부모에 붙이고, 켜고 끌 패널만 root에 넣는다.
public class MapUI : MonoBehaviour
{
    [Header("루트")]
    [SerializeField] private GameObject root;

    [Header("입력")]
    [SerializeField] private KeyCode toggleKey = KeyCode.M;

    [Header("지도")]
    // 구운 텍스처가 올라갈 곳. 이 RectTransform이 지도의 좌표계가 된다.
    [SerializeField] private RawImage mapImage;

    [SerializeField] private MapPalette palette;

    // 미리 그려둔 지도 그림. 넣으면 격자를 굽지 않고 이것을 그대로 쓴다.
    // 원작처럼 손으로 그린 존 지도를 쓸 때. 비우면 예전대로 굽는다.
    [SerializeField] private Texture2D overrideTexture;

    // 격자 한 칸을 몇 픽셀로 구울지. 1이면 한 칸 = 한 픽셀.
    // 2 이상으로 올리면 칸 안에 구조물 점을 찍을 수 있지만 지금은 1로 충분하다.
    [Range(1, 4)]
    [SerializeField] private int pixelsPerCell = 1;

    [Header("마커")]
    // 마커 하나의 프리팹. Image 하나짜리면 된다.
    [SerializeField] private Image markerPrefab;

    // 마커가 쌓일 부모. 비우면 mapImage 아래에 붙는다.
    [SerializeField] private RectTransform markerRoot;

    [SerializeField] private float defaultMarkerSize = 6f;

    [Header("플레이어")]
    // 플레이어 표시. 방향까지 보여주려고 삼각형 스프라이트를 권한다.
    [SerializeField] private Image playerMarker;
    [SerializeField] private float playerMarkerSize = 7f;

    // 플레이어 화살표를 바라보는 방향으로 돌릴지.
    [SerializeField] private bool rotatePlayerMarker = true;

    [Header("없을 때")]
    // ZoneGenerator가 없는 씬(허브)에서 켜는 안내.
    [SerializeField] private GameObject noMapLabel;

    [Header("커서")]
    [SerializeField] private GameObject crosshair;

    [Header("동작")]
    [SerializeField] private bool pauseWhileOpen = true;

    public static bool IsOpen { get; private set; }

    private ZoneGenerator zone;
    private Transform player;
    private Texture2D texture;
    private readonly List<Image> spawned = new List<Image>();
    private float savedTimeScale = 1f;
    private bool cursorWasVisible;

    private void Awake()
    {
        if (root == null)
        {
            Debug.LogError("[MapUI] root가 비어 있다. 켜고 끌 패널을 넣을 것.", this);
        }
        else
        {
            root.SetActive(false);
        }

        if (markerRoot == null && mapImage != null)
        {
            markerRoot = mapImage.rectTransform;
        }
    }

    private void OnDisable()
    {
        if (IsOpen)
        {
            RestoreWorld();
        }
    }

    private void OnDestroy()
    {
        if (texture != null)
        {
            Destroy(texture);
            texture = null;
        }
    }

    private void Update()
    {
        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
        {
            // 다른 창이 열려 있으면 M을 먹지 않는다.
            if (!IsOpen && (InventoryScreen.IsOpen || DialogueUI.IsOpen || QuestListUI.IsOpen || UIBlocker.PdaOpen))
            {
                return;
            }

            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        // 열려 있는 동안 플레이어 점만 계속 따라간다.
        // timeScale이 0이어도 Update는 돈다 — 07에서 배운 것.
        if (IsOpen)
        {
            UpdatePlayerMarker();
        }
    }

    // ───────────────── 열고 닫기 ─────────────────

    public void Open()
    {
        if (root == null || IsOpen)
        {
            return;
        }

        root.SetActive(true);
        IsOpen = true;
        UIBlocker.MapOpen = true;

        if (pauseWhileOpen)
        {
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        // 조준선 먼저, 커서 나중.
        if (crosshair != null)
        {
            crosshair.SetActive(false);
        }

        cursorWasVisible = Cursor.visible;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        Build();
    }

    public void Close()
    {
        if (root != null)
        {
            root.SetActive(false);
        }

        ClearMarkers();

        if (IsOpen)
        {
            RestoreWorld();
        }
    }

    private void RestoreWorld()
    {
        IsOpen = false;
        UIBlocker.MapOpen = false;

        if (pauseWhileOpen)
        {
            Time.timeScale = (savedTimeScale <= 0f) ? 1f : savedTimeScale;
        }

        if (crosshair != null)
        {
            crosshair.SetActive(true);
        }

        Cursor.visible = cursorWasVisible;
    }

    // ───────────────── 굽기 ─────────────────

    private void Build()
    {
        if (zone == null)
        {
            zone = FindFirstObjectByType<ZoneGenerator>();
        }

        bool has = (zone != null);

        if (noMapLabel != null)
        {
            noMapLabel.SetActive(!has);
        }

        if (mapImage != null)
        {
            mapImage.gameObject.SetActive(has);
        }

        if (playerMarker != null)
        {
            playerMarker.gameObject.SetActive(has);
        }

        ClearMarkers();

        if (!has)
        {
            return;
        }

        Bake();
        BuildMarkers();
        UpdatePlayerMarker();
    }

    // 격자를 텍스처로 굽는다. 열 때마다 다시 굽는 이유는
    // 존이 매번 새로 생성되기 때문이다 — 캐시하면 지난 판의 지도가 남는다.
    private void Bake()
    {
        // 미리 그려둔 지도가 있으면 굽지 않는다.
        if (overrideTexture != null)
        {
            if (mapImage != null)
            {
                mapImage.texture = overrideTexture;
            }

            return;
        }

        int gw = zone.GridWidth;
        int gh = zone.GridHeight;

        if (gw <= 0 || gh <= 0)
        {
            return;
        }

        int tw = gw * pixelsPerCell;
        int th = gh * pixelsPerCell;

        if (texture == null || texture.width != tw || texture.height != th)
        {
            if (texture != null)
            {
                Destroy(texture);
            }

            texture = new Texture2D(tw, th, TextureFormat.RGBA32, false);

            // 픽셀 게임이므로 확대해도 뭉개지면 안 된다.
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
        }

        Color[] buffer = new Color[tw * th];

        for (int y = 0; y < gh; y++)
        {
            for (int x = 0; x < gw; x++)
            {
                char c = zone.GetSymbol(x, y);
                Color col;

                if (c == '\0' || c == ' ' || c == '_')
                {
                    col = (palette != null) ? palette.voidColor : new Color(0f, 0f, 0f, 0f);
                }
                else if (palette != null)
                {
                    palette.TryGet(c, out col);
                }
                else
                {
                    col = Color.gray;
                }

                // Texture2D는 y = 0이 아래다. ZoneGenerator의 격자도 y = 0이 아래다.
                // 둘이 같아서 뒤집을 필요가 없다.
                for (int py = 0; py < pixelsPerCell; py++)
                {
                    for (int px = 0; px < pixelsPerCell; px++)
                    {
                        int ix = x * pixelsPerCell + px;
                        int iy = y * pixelsPerCell + py;
                        buffer[iy * tw + ix] = col;
                    }
                }
            }
        }

        texture.SetPixels(buffer);
        texture.Apply(false);

        if (mapImage != null)
        {
            mapImage.texture = texture;
        }
    }

    // ───────────────── 마커 ─────────────────

    private void BuildMarkers()
    {
        if (markerPrefab == null || markerRoot == null)
        {
            return;
        }

        IReadOnlyList<MapMarker> list = MapMarker.All;

        for (int i = 0; i < list.Count; i++)
        {
            MapMarker m = list[i];

            if (m == null)
            {
                continue;
            }

            Image img = Instantiate(markerPrefab, markerRoot);
            spawned.Add(img);

            if (m.Icon != null)
            {
                img.sprite = m.Icon;
            }

            img.color = m.Color;

            float s = (m.Size > 0f) ? m.Size : defaultMarkerSize;
            RectTransform rt = img.rectTransform;
            rt.sizeDelta = new Vector2(s, s);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = WorldToMap(m.transform.position);
        }
    }

    private void ClearMarkers()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null)
            {
                Destroy(spawned[i].gameObject);
            }
        }

        spawned.Clear();
    }

    private void UpdatePlayerMarker()
    {
        if (playerMarker == null || zone == null)
        {
            return;
        }

        if (player == null)
        {
            PlayerStats stats = FindFirstObjectByType<PlayerStats>();

            if (stats != null)
            {
                player = stats.transform;
            }
        }

        if (player == null)
        {
            playerMarker.gameObject.SetActive(false);
            return;
        }

        playerMarker.gameObject.SetActive(true);

        RectTransform rt = playerMarker.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(playerMarkerSize, playerMarkerSize);
        rt.anchoredPosition = WorldToMap(player.position);

        if (rotatePlayerMarker)
        {
            // 플레이어 루트가 도는 게 아니라면 각도는 0이 나온다. 그래도 무해하다.
            rt.localEulerAngles = new Vector3(0f, 0f, player.eulerAngles.z);
        }
    }

    // 월드 좌표 → 지도 RectTransform 안의 좌표(좌하단 원점).
    private Vector2 WorldToMap(Vector3 world)
    {
        if (zone == null || mapImage == null)
        {
            return Vector2.zero;
        }

        Vector2 size = zone.GetMapSize();

        if (size.x <= 0f || size.y <= 0f)
        {
            return Vector2.zero;
        }

        Vector2 local = (Vector2)world - (Vector2)zone.transform.position;

        float u = Mathf.Clamp01(local.x / size.x);
        float v = Mathf.Clamp01(local.y / size.y);

        Rect r = mapImage.rectTransform.rect;
        return new Vector2(u * r.width, v * r.height);
    }
}
