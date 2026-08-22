using UnityEngine;

// Ǯ������ �����Ǵ� �Ѿ�. ���� Destroy���� �ʰ� Ǯ�� ��ȯ�ȴ�.
// �����տ��� Rigidbody2D(Kinematic)�� IsTrigger �ݶ��̴��� �ʿ��ϴ�.
//
// ����(Faction)�� ���� ������ ����� �޶�����.
//   Player  �� Enemy��
//   Enemy   �� Player�� �ٸ� Enemy ��� (���ۿ��� �Ʊ� ���簡 �ִ�)
//
// �� �Ѿ��� �ٸ� ���� ������ ���� �ǵ��� �����̴�. �÷��̾ �� ���̷�
// ������ ���� ��� ����� ������ �����Ѵ�. ��� ���� �Ʊ��� �缱�� ������
// ����� �����Ѵ� (RangedEnemyController.IsFriendlyInLine).
//
// Rigidbody2D�� Collision Detection�� Continuous�� �� ��.
// ��� �Ѿ��� ���� ���� ����ϴ� ���� ���´�.
[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    public enum Faction
    {
        Player,
        Enemy,
    }

    private Rigidbody2D rb;
    private BulletPooling pool;
    private float damage;
    private float lifeTimer;
    private float lifeTime;

    private Faction faction = Faction.Player;
    private WeaponMastery sourceMastery;
    private WeaponMastery.WeaponClass sourceClass;

    // �ڱ� �ڽ��� �� �Ѿ˿� ���� �ʵ��� �߻��ڸ� ����Ѵ�.
    private Collider2D shooter;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
    }

    public void SetPool(BulletPooling owner)
    {
        pool = owner;
    }

    // ���� �� �Ѿ�����. �߻� ���� �����Ѵ�.
    // shooterCollider�� �ѱ�� �� �ݶ��̴��� �����Ѵ�(���� ����).
    public void SetFaction(Faction value, Collider2D shooterCollider = null)
    {
        faction = value;
        shooter = shooterCollider;
    }

    // �߻��� ������ ����. ���� �׿��� �� ���õ� ����ġ�� ���� ���� �����Ѵ�.
    // ���� �� �Ѿ��� mastery�� null�� �ѱ��.
    public void SetSource(WeaponMastery mastery, WeaponMastery.WeaponClass weaponClass)
    {
        sourceMastery = mastery;
        sourceClass = weaponClass;
    }

    // �߻� ������ ����, �ӵ�, ������, ���� �ð��� �����Ѵ�.
    public void Fire(Vector2 position, float angle, float speed, float damageValue, float life)
    {
        transform.position = position;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        float rad = angle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        rb.linearVelocity = dir * speed;
        damage = damageValue;
        lifeTime = life;
        lifeTimer = 0f;
    }

    private void Update()
    {
        lifeTimer += Time.deltaTime;

        if (lifeTimer >= lifeTime)
        {
            ReturnToPool();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // �ѱ��� ���뿡 ���ĵ� �ڱ� �ѿ� ���� �ʴ´�.
        if (shooter != null && other == shooter)
        {
            return;
        }

        if (other.CompareTag("Obstacle") || other.gameObject.layer == LayerMask.NameToLayer("Obstacle"))
        {
            ReturnToPool();
            return;
        }

        if (faction == Faction.Player)
        {
            HitAsPlayerBullet(other);
        }
        else
        {
            HitAsEnemyBullet(other);
        }
    }

    // �÷��̾� �Ѿ��� ���� ������.
    private void HitAsPlayerBullet(Collider2D other)
    {
        if (!other.CompareTag("Enemy"))
        {
            return;
        }

        DamageEnemy(other, sourceMastery);
        ReturnToPool();
    }

    // �� �Ѿ��� �÷��̾�� �ٸ� �� ��θ� ������.
    private void HitAsEnemyBullet(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerStats stats = other.GetComponent<PlayerStats>();

            if (stats != null)
            {
                stats.TakeDamage(damage);
            }

            ReturnToPool();
            return;
        }

        if (other.CompareTag("Enemy"))
        {
            // ����� ���� ���� �÷��̾� ������ �ƴϹǷ� ���õ��� ���� �ʴ´�.
            DamageEnemy(other, null);
            ReturnToPool();
        }
    }

    private void DamageEnemy(Collider2D other, WeaponMastery mastery)
    {
        EnemyHealth health = other.GetComponent<EnemyHealth>();

        if (health == null)
        {
            return;
        }

        health.SetLastAttacker(mastery, sourceClass);
        health.TakeDamage(damage);
    }

    private void ReturnToPool()
    {
        rb.linearVelocity = Vector2.zero;

        // ���� �߻翡�� �߸��� ������ ���� �ʵ��� �ʱ�ȭ�Ѵ�.
        faction = Faction.Player;
        sourceMastery = null;
        shooter = null;

        if (pool != null)
        {
            pool.Return(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}