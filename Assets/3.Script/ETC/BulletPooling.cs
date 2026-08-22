using System.Collections.Generic;
using UnityEngine;

// 총알 오브젝트 풀. 부족하면 자동으로 늘어난다.
// 총알은 씬 최상위 컨테이너에 보관한다. 플레이어 자식으로 두면 이동에 끌려간다.
public class BulletPooling : MonoBehaviour
{
    [SerializeField] private Bullet bulletPrefab;
    [SerializeField] private int initialSize = 30;

    private Queue<Bullet> pool = new Queue<Bullet>();
    private Transform container;

    private void Awake()
    {
        container = new GameObject("BulletPool").transform;

        for (int i = 0; i < initialSize; i++)
        {
            pool.Enqueue(Create());
        }
    }

    private Bullet Create()
    {
        Bullet bullet = Instantiate(bulletPrefab, container);
        bullet.SetPool(this);
        bullet.gameObject.SetActive(false);
        return bullet;
    }

    public Bullet Get()
    {
        Bullet bullet = pool.Count > 0 ? pool.Dequeue() : Create();
        bullet.gameObject.SetActive(true);
        return bullet;
    }

    public void Return(Bullet bullet)
    {
        bullet.gameObject.SetActive(false);
        pool.Enqueue(bullet);
    }
}