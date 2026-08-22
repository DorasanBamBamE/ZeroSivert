using UnityEngine;

// 허브(벙커)에 두는 존 출발 지점.
// 플레이어가 범위 안에서 키를 누르면 존 씬으로 넘어간다.
//
// BoxCollider2D를 붙이고 Is Trigger를 켤 것. 플레이어에 Tag "Player" 필요.
[RequireComponent(typeof(Collider2D))]
public class ZoneEntryPoint : MonoBehaviour
{
    [SerializeField] private string zoneSceneName = "Forest";
    
    [SerializeField] private bool directInteraction = true;
[SerializeField] private KeyCode useKey = KeyCode.E;

    // "E — 숲으로 출발" 같은 안내. 없으면 비워둬도 된다.
    [SerializeField] private GameObject prompt;

    private PlayerStats stats;
    private bool inRange;

    private void Awake()
    {
        if (prompt != null)
        {
            prompt.SetActive(false);
        }
    }

private void Update()
    {
        if (!directInteraction || !inRange || stats == null)
        {
            return;
        }

        if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading)
        {
            return;
        }

        if (Input.GetKeyDown(useKey))
        {
            Depart();
        }
    }

public bool Depart()
    {
        if (stats == null)
        {
            stats = FindFirstObjectByType<PlayerStats>();
        }

        if (stats == null || (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading))
        {
            return false;
        }

        RunData.Instance.Save(stats);
        RunData.Instance.BeginRaid();

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.Load(zoneSceneName);
            return true;
        }

        Debug.LogWarning("ZoneEntryPoint: 씬에 SceneLoader가 없다.", this);
        return false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        stats = other.GetComponent<PlayerStats>();
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
        stats = null;

        if (prompt != null)
        {
            prompt.SetActive(false);
        }
    }
}