using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 씬 전환 담당. 페이드 아웃 → 로드 → 페이드 인.
//
// 이 오브젝트는 자체 Canvas(Sort Order 높게)를 자식으로 가지며
// DontDestroyOnLoad로 남는다. 씬마다 하나씩 두면 안 된다.
//
// timeScale을 반드시 1로 되돌린다. PDA를 열어둔 채 씬을 넘기면
// 다음 씬이 멈춘 상태로 시작하는 사고가 난다.
public class SceneLoader : MonoBehaviour
{
    private static SceneLoader instance;

    public static SceneLoader Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<SceneLoader>();
            }

            return instance;
        }
    }

    [SerializeField] private CanvasGroup fade;
    [SerializeField] private float fadeTime = 0.35f;

    private bool isLoading;

    public bool IsLoading
    {
        get { return isLoading; }
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

        if (fade != null)
        {
            fade.alpha = 0f;
            fade.blocksRaycasts = false;
        }
    }

    public void Load(string sceneName)
    {
        if (isLoading)
        {
            return;
        }

        StartCoroutine(LoadRoutine(sceneName));
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        isLoading = true;

        // PDA 등으로 멈춰 있을 수 있으므로 먼저 되돌린다.
        Time.timeScale = 1f;

        if (fade != null)
        {
            fade.blocksRaycasts = true;
        }

        yield return Fade(0f, 1f);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);

        while (op != null && !op.isDone)
        {
            yield return null;
        }

        // 씬의 Start()가 한 번 돌 시간을 준다. 존 생성이 여기서 끝난다.
        yield return null;

        yield return Fade(1f, 0f);

        if (fade != null)
        {
            fade.blocksRaycasts = false;
        }

        isLoading = false;
    }

    // timeScale에 영향받지 않도록 unscaledDeltaTime을 쓴다.
    private IEnumerator Fade(float from, float to)
    {
        if (fade == null || fadeTime <= 0f)
        {
            yield break;
        }

        float t = 0f;

        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            fade.alpha = Mathf.Lerp(from, to, t / fadeTime);
            yield return null;
        }

        fade.alpha = to;
    }
}