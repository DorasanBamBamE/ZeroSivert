using System.Collections;
using UnityEngine;

// 존에서의 사망 처리. 씬에 하나 둔다.
//
// PlayerStats의 Died 이벤트를 받아 사망 연출을 보여준 뒤 허브로 돌려보낸다.
// 사망 시에는 스냅샷을 버려서 다음 판을 새 몸으로 시작한다.
//
// ★ 11 - 결과 화면(RunEndScreen)을 띄운다.
//   원작처럼 "교전 중 사망"이 붉게 뜨고, 경험치 게이지와 사인이 보이고,
//   건너뛰기를 누르면 벙커로 돌아간다. 안 누르면 returnDelay 뒤 자동으로 넘어간다.
//
//   deathScreen(옛 필드)은 그대로 둔다. RunEndScreen을 안 붙였을 때의
//   최소 안내로 계속 동작한다 - 둘 다 비어 있어도 흐름은 막히지 않는다.
public class RunEndHandler : MonoBehaviour
{
    [SerializeField] private PlayerStats stats;
    [SerializeField] private string hubSceneName = "Hub";

    // 사망 애니메이션을 보여줄 시간. 결과 화면은 이 뒤에 뜬다.
    [SerializeField] private float deathDelay = 2.5f;

    [Header("UI (선택)")]
    // "사망" 문구 등. 사망 즉시 켜진다. RunEndScreen이 있으면 없어도 된다.
    [SerializeField] private GameObject deathScreen;

    // 비우면 씬에서 찾는다.
    [SerializeField] private RunEndScreen endScreen;

    // 결과 화면을 안 넘기고 가만히 둬도 이 시간 뒤에는 벙커로 돌아간다.
    [SerializeField] private float returnDelay = 20f;

    private bool handled;
    private bool returning;

    private void Awake()
    {
        if (stats == null)
        {
            stats = FindFirstObjectByType<PlayerStats>();
        }

        if (endScreen == null)
        {
            endScreen = FindFirstObjectByType<RunEndScreen>(FindObjectsInactive.Include);
        }

        if (deathScreen != null)
        {
            deathScreen.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (stats != null)
        {
            stats.Died += OnPlayerDied;
        }
    }

    private void OnDisable()
    {
        if (stats != null)
        {
            stats.Died -= OnPlayerDied;
        }
    }

    private void OnPlayerDied()
    {
        if (handled)
        {
            return;
        }

        handled = true;
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        if (deathScreen != null)
        {
            deathScreen.SetActive(true);
        }

        yield return new WaitForSeconds(deathDelay);

        if (endScreen != null)
        {
            endScreen.ShowDeath(ReturnToHub);

            // 결과 화면은 timeScale을 0으로 잡으므로 unscaled로 센다.
            float t = 0f;

            while (!returning && t < returnDelay)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // 시간이 다 됐는데 아직 안 넘어갔으면 여기서 넘긴다.
            if (!returning)
            {
                Time.timeScale = 1f;
                ReturnToHub();
            }

            yield break;
        }

        ReturnToHub();
    }

    private void ReturnToHub()
    {
        if (returning)
        {
            return;
        }

        returning = true;

        // 사망하면 들고 있던 상태를 버린다. 인벤토리 손실도 07번에서 여기에 붙는다.
        RunData.Instance.ResetSnapshot();
        RunData.Instance.SetOutcome(RunData.Outcome.Died);

        // 다음 판이 지난 판의 사인을 물려받지 않게 한다.
        KillReport.Clear();

        // 결과 화면이 멈춰 둔 시간을 반드시 풀고 나간다.
        Time.timeScale = 1f;
        UIBlocker.Clear();

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.Load(hubSceneName);
        }
    }
}
