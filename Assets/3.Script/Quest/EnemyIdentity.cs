using UnityEngine;

// 적의 종류 이름. 처치형 퀘스트가 이 이름으로 센다.
//
// EnemyHealth에 필드를 직접 늘리지 않고 별도 컴포넌트로 뺀 이유
//   1. EnemyHealth는 이미 손댈 곳이 많은 파일이다. 한 줄만 넣고 끝내는 편이 안전하다
//   2. 프리팹 종류마다 이름만 다르므로, 컴포넌트 하나 붙이고 문자열만 바꾸면 된다
//   3. 나중에 도감·처치 통계 같은 게 붙어도 여기만 확장하면 된다
//
// 붙이는 법 — 적 프리팹마다 붙이고 enemyId를 채운다.
//   Bandit_Prefab   → "Bandit"
//   Ghoul_Prefab    → "Ghoul"
//
// ★ QuestData.targetEnemyId와 철자가 정확히 같아야 한다. 대소문자도 구분한다.
public class EnemyIdentity : MonoBehaviour
{
    [SerializeField] private string enemyId = "Bandit";

    // 표시용 이름. 비우면 enemyId를 쓴다.
    [SerializeField] private string displayName;

    public string EnemyId
    {
        get { return enemyId; }
    }

    public string DisplayName
    {
        get { return string.IsNullOrEmpty(displayName) ? enemyId : displayName; }
    }

    // 이미 죽은 적이 두 번 세지는 것을 막는다.
    // 사망 연출 중에 추가 피격이 들어와도 Die가 두 번 불릴 수 있다.
    private bool reported;

    // EnemyHealth의 사망 처리에서 딱 한 번 부른다.
    public void ReportDeath()
    {
        if (reported)
        {
            return;
        }

        reported = true;
        QuestManager.Instance.ReportKill(enemyId);
    }
}
