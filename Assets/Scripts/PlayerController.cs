using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

// Корабль игрока: движение WASD/стрелки, стрельба на Пробел, ульта на X, жизни.
public class PlayerController : MonoBehaviour
{
    [Header("Движение")]
    public float moveSpeed = 7f;    // скорость корабля
    public float padding = 0.5f;    // отступ от краёв экрана (примерно половина размера корабля)

    [Header("Стрельба")]
    public GameObject laserPrefab;  // префаб лазера
    public Transform firePoint;     // пустой объект на носу корабля (можно оставить пустым)

    [Header("Жизни")]
    public int lives = 3;

    [Header("Ульта (клавиша X)")]
    public float ultCooldown = 15f; // перезарядка в секундах (магазин потом будет её уменьшать)

    float nextUltTime = 0f;         // когда ульта снова будет готова
    float minX, maxX, minY, maxY;   // границы движения
    float screenTop;                // верхний край экрана

    // Сколько секунд осталось до готовности ульты (пригодится для UI)
    public float UltCooldownLeft
    {
        get { return Mathf.Max(0f, nextUltTime - Time.time); }
    }

    void Start()
    {
        // Считаем границы экрана по камере
        Camera cam = Camera.main;
        Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0, 0, 0));
        Vector3 topRight = cam.ViewportToWorldPoint(new Vector3(1, 1, 0));

        minX = bottomLeft.x + padding;
        maxX = topRight.x - padding;
        minY = bottomLeft.y + padding;
        maxY = topRight.y - padding;
        screenTop = topRight.y;

        GameManager.Instance.SetLives(lives);
    }

    void Update()
    {
        // После Game Over управление отключено
        if (GameManager.Instance.isUiBlocked) return;

        Move();

        // GetKeyDown = один клик по клавише даёт один выстрел
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Shoot();
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            TryUseUltimate();
        }
    }

    void Move()
    {
        // Оси Horizontal/Vertical в Unity по умолчанию уже включают WASD и стрелки
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // normalized: чтобы по диагонали корабль не летел быстрее
        Vector3 direction = new Vector3(h, v, 0f).normalized;
        Vector3 newPosition = transform.position + direction * moveSpeed * Time.deltaTime;

        // Не выпускаем корабль за границы экрана
        newPosition.x = Mathf.Clamp(newPosition.x, minX, maxX);
        newPosition.y = Mathf.Clamp(newPosition.y, minY, maxY);

        transform.position = newPosition;
    }

    void Shoot()
    {
        Transform spawnPoint = (firePoint != null) ? firePoint : transform;
        Instantiate(laserPrefab, spawnPoint.position, Quaternion.identity);
        AudioManager.Shoot();
    }

    void TryUseUltimate()
    {
        // Ещё на перезарядке
        if (UltCooldownLeft > 0f) return;

        nextUltTime = Time.time + ultCooldown;
        AudioManager.Ult();
        Fx.Shake(0.4f, 0.4f);

        // Копируем список: астероиды удаляют сами себя из него, а менять список во время foreach нельзя
        List<Asteroid> copy = new List<Asteroid>(Asteroid.Active);
        foreach (Asteroid asteroid in copy)
        {
            if (asteroid == null) continue;

            // Только те, что уже на экране (не выше верхнего края)
            if (asteroid.transform.position.y > screenTop) continue;

            asteroid.Die(true);   // с наградой, как при обычном убийстве
        }

        // Босс ультой не убивается мгновенно: он теряет 20% от максимального HP
        if (Boss.Current != null)
        {
            Boss.Current.TakeDamage(Mathf.Max(1, Boss.Current.maxHealth / 5));
        }
    }

    // Вызывается из Asteroid.cs при столкновении
    public void TakeHit()
    {
        lives--;
        AudioManager.PlayerHit();
        Fx.Shake(0.3f, 0.35f);
        Fx.Burst(transform.position, Color.red, 25, 5f, 0.2f, 0.6f);
        GameManager.Instance.SetLives(lives);

        if (lives <= 0)
        {
            GameManager.Instance.GameOver();
            gameObject.SetActive(false);
        }
        else
        {
            // Небольшой "толчок" масштабом при получении урона
            transform.DOKill(true);
            transform.DOPunchScale(Vector3.one * 0.3f, 0.3f).SetLink(gameObject);
        }
    }
}
