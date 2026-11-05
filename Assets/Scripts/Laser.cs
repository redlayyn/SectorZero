using UnityEngine;

// Лазер: летит вверх, при попадании в астероид наносит урон и исчезает.
public class Laser : MonoBehaviour
{
    public float speed = 12f;   // скорость полёта вверх

    int damage;     // урон берём из GameManager (его потом прокачивает магазин)
    float topY;     // выше этой линии лазер удаляется
    bool used;      // чтобы один лазер не ударил два астероида в один кадр

    void Start()
    {
        damage = GameManager.Instance.laserDamage;

        // Верхняя граница экрана камеры плюс небольшой запас
        topY = Camera.main.ViewportToWorldPoint(new Vector3(0, 1, 0)).y + 1f;
    }

    void Update()
    {
        // Летим вверх по оси Y
        transform.Translate(Vector3.up * speed * Time.deltaTime, Space.World);

        // Вылетел за экран - удаляем
        if (transform.position.y > topY)
        {
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (used) return;

        // Бьём всё, что умеет получать урон: астероид или босс
        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target != null)
        {
            used = true;
            target.TakeDamage(damage);
            Fx.Burst(transform.position, new Color(0.4f, 0.9f, 1f), 5, 3f, 0.1f, 0.25f);
            Destroy(gameObject);
        }
    }
}
