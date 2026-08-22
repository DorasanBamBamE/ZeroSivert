using System.Collections;
using UnityEngine;

// 적 체력. 총알이 피격 시 호출한다.
// 사망하면 이동과 충돌을 끄고 사망 애니메이션 재생 후 비활성화한다.
public class EnemyHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 60f;
    [SerializeField] private EnemyAnimator enemyAnimator;
    [SerializeField] private float corpseDelay = 3f;

    // 08 루팅 ? 시체에 LootContainer가 붙어 있으면 시신을 치우지 않는다.
    // 원작에서 시체는 존을 나갈 때까지 남아 있고 언제든 뒤질 수 있다.
    // corpseDelay로 꺼버리면 3초 뒤에 사라져서 루팅 자체가 불가능하다.
    [SerializeField] private bool keepCorpseForLoot = true;

    [Header("처치 보상")]
    [SerializeField] private GameStats.StatId killStatId = GameStats.StatId.KillZombie;
    [SerializeField] private bool isHuman;
    [SerializeField] private int expReward = 40;
    [SerializeField] private float masteryExp = 25f;

    [Header("사운드")]
    [SerializeField] private AudioSource audioSource;

    // 피격음. 여러 개를 넣으면 랜덤 재생한다. 원작 snd_bullet_flesh1~4.
    [SerializeField] private AudioClip[] hurtSounds;

    // 사망음. 원작 snd_ghoul_death / snd_wolf_death.
    [SerializeField] private AudioClip deathSound;

    private float currentHealth;
    private bool isDead;

    private WeaponMastery lastMastery;
    private WeaponMastery.WeaponClass lastClass;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void OnEnable()
    {
        currentHealth = maxHealth;
        isDead = false;

        // 풀링 재사용 시 이전 공격자 정보가 남지 않게 한다.
        lastMastery = null;
    }

    // 총알이 맞기 직전에 호출한다. 마지막으로 때린 무기를 기록한다.
    public void SetLastAttacker(WeaponMastery mastery, WeaponMastery.WeaponClass weaponClass)
    {
        lastMastery = mastery;
        lastClass = weaponClass;
    }

    public void TakeDamage(float amount)
    {
        if (isDead)
        {
            return;
        }

        currentHealth -= amount;

        PlayRandom(hurtSounds);

        // 맞으면 시야와 무관하게 즉시 경계에 들어간다.
        EnemyControllerBase ctrl = GetComponent<EnemyControllerBase>();

        if (ctrl != null)
        {
            PlayerStats player = FindFirstObjectByType<PlayerStats>();

            if (player != null)
            {
                ctrl.ForceAlert(player.transform.position);
            }
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;

        Play(deathSound);

        // 플레이 기록과 캐릭터 경험치.
        if (GameStats.Instance != null)
        {
            GameStats.Instance.ReportKill(killStatId, isHuman);
        }

        // 09 - 처치형 퀘스트 카운트
        // 11 - 들고 있던 총을 시체에 남긴다.
        WeaponDrop drop = GetComponent<WeaponDrop>();

        if (drop != null)
        {
            drop.DropInto(GetComponentInChildren<LootContainer>(true));
        }

        EnemyIdentity id = GetComponent<EnemyIdentity>();

        if (id != null)
        {
            id.ReportDeath();
        }

        if (PlayerLevel.Instance != null)
        {
            PlayerLevel.Instance.AddExp(expReward);
        }

        // 마지막으로 때린 무기 종류에 숙련도 경험치를 준다.
        if (lastMastery != null)
        {
            lastMastery.AddExp(lastClass, masteryExp);
        }

        EnemyControllerBase controller = GetComponent<EnemyControllerBase>();
        if (controller != null)
        {
            controller.Stop();
            controller.enabled = false;
        }

        // 사격형 적은 죽은 뒤 총을 쏘면 안 된다.
        EnemyWeapon weapon = GetComponentInChildren<EnemyWeapon>();
        if (weapon != null)
        {
            weapon.enabled = false;
        }

        // 08 루팅 ? 총알에 맞는 콜라이더만 끈다.
        // 전부 끄면 시체에 붙은 루팅 트리거까지 죽어서 E 감지가 안 된다.
        Collider2D[] bodyColliders = GetComponents<Collider2D>();

        for (int i = 0; i < bodyColliders.Length; i++)
        {
            if (!bodyColliders[i].isTrigger)
            {
                bodyColliders[i].enabled = false;
            }
        }

        if (enemyAnimator != null)
        {
            enemyAnimator.SetDead();
        }

        // 08 루팅 ? 시체 컨테이너를 켠다. 자식에 달아둔 경우도 찾도록 InChildren을 쓴다.
        LootContainer loot = GetComponentInChildren<LootContainer>(true);

        if (loot != null)
        {
            loot.gameObject.SetActive(true);
            loot.enabled = true;
        }

        // 뒤질 수 있는 시체는 치우지 않는다. 컨테이너가 없으면 기존대로 사라진다.
        if (loot == null || !keepCorpseForLoot)
        {
            StartCoroutine(DisableAfterDelay());
        }
    }

    // 추후 루팅 오브젝트 생성 시점으로 사용한다.
    private IEnumerator DisableAfterDelay()
    {
        yield return new WaitForSeconds(corpseDelay);
        gameObject.SetActive(false);
    }

    private void PlayRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
        {
            return;
        }

        Play(clips[Random.Range(0, clips.Length)]);
    }

    private void Play(AudioClip clip)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        // 같은 소리가 반복될 때 단조롭지 않도록 음정을 흔든다.
        audioSource.pitch = Random.Range(0.92f, 1.08f);
        audioSource.PlayOneShot(clip);
    }
}