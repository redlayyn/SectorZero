using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

// Астероид: летит вниз, имеет HP, получает урон от лазера.
// Бывает трёх размеров: маленький (0), средний (1), большой (2).
public class Asteroid : MonoBehaviour, IDamageable
{
    // Список всех живых астероидов. Нужен для ульты и дронов.
    public static readonly List<Asteroid> Active = new List<Asteroid>();

    // Множители размеров относительно СРЕДНЕГО астероида (индекс = размер 0, 1, 2)
    static readonly float[] TierHealth = { 0.5f, 1f, 2f };      // HP
    static readonly float[] TierSpeed  = { 1.3f, 1f, 0.7f };    // скорость падения
    static readonly float[] TierScale  = { 0.7f, 1f, 1.4f };    // размер на экране
    static readonly float[] TierReward = { 0.5f, 1f, 2.5f };    // награда

    [Header("Базовые параметры (средний астероид, 1-я волна)")]
    public int baseHealth = 3;
    public float baseSpeed = 2f;
    public int reward = 5;

    [Header("Ссылки (перетащи в Inspector)")]
    public SpriteRenderer spriteRenderer;   // спрайт астероида
    public TMP_Text hpText;                 // цифра HP над астероидом (можно оставить пустым)
    public GameObject explosionPrefab;      // свой эффект взрыва (можно оставить пустым: частицы создаются кодом)

    int health;
    float speed;
    int currentReward;
    int tier = 1;
    Vector3 baseScale;
    float bottomY;      // ниже этой линии астероид удаляется
    bool isDead;        // защита от двойной смерти в один кадр

    void OnEnable()  { Active.Add(this); }
    void OnDisable() { Active.Remove(this); }

    void Awake()
    {
        // Значения по умолчанию, если Setup() не вызывали
        health = baseHealth;
        speed = baseSpeed;
        currentReward = reward;
        baseScale = transform.localScale;
    }

    void Start()
    {
        // Нижняя граница экрана камеры минус запас
        bottomY = Camera.main.ViewportToWorldPoint(new Vector3(0, 0, 0)).y - 1f;
        UpdateHpText();
    }

    // Вызывается из GameManager сразу после создания астероида.
    // Умножает базовые параметры на коэффициенты волны и размера (tier: 0, 1 или 2).
    public void Setup(float healthMultiplier, float speedMultiplier, int tier = 1)
    {
        this.tier = Mathf.Clamp(tier, 0, 2);

        health = Mathf.Max(1, Mathf.RoundToInt(baseHealth * healthMultiplier * TierHealth[this.tier]));
        speed = baseSpeed * speedMultiplier * TierSpeed[this.tier];
        currentReward = Mathf.Max(1, Mathf.RoundToInt(reward * TierReward[this.tier]));
        transform.localScale = baseScale * TierScale[this.tier];

        UpdateHpText();
    }

    void Update()
    {
        // Летим вниз. Space.World - вниз по миру, даже если астероид повёрнут.
        transform.Translate(Vector3.down * speed * Time.deltaTime, Space.World);

        // Улетел за нижний край экрана - просто удаляем (без награды)
        if (transform.position.y < bottomY)
        {
            Destroy(gameObject);
        }
    }

    // Получить урон (вызывается из Laser.cs и Drone.cs)
    public void TakeDamage(int damage)
    {
        if (isDead) return;

        health -= damage;
        UpdateHpText();

        // --- Эффекты DOTween ---
        // Подёргивание масштабом. DOShakePosition здесь не подходит:
        // астероид двигается через Translate, и тряска позиции конфликтовала бы с движением.
        transform.DOKill(true);   // завершаем прошлое подёргивание, чтобы масштаб не "уплывал"
        transform.DOShakeScale(0.15f, 0.2f, 20).SetLink(gameObject);

        // Красная вспышка: белый -> красный -> обратно
        spriteRenderer.DOKill(true);
        spriteRenderer.DOColor(Color.red, 0.07f).SetLoops(2, LoopType.Yoyo).SetLink(gameObject);

        if (health <= 0)
        {
            Die(true);
        }
        else
        {
            AudioManager.Hit();
        }
    }

    // giveReward = true: игрок получает кредиты. false: астероид врезался в корабль.
    public void Die(bool giveReward)
    {
        if (isDead) return;
        isDead = true;
        AudioManager.Explosion();

        // Частицы: чем больше астероид, тем их больше
        Fx.Burst(transform.position, new Color(1f, 0.6f, 0.2f), 10 + tier * 8, 4f, 0.15f * TierScale[tier], 0.6f);

        // Большой астероид взрывается с лёгкой тряской камеры
        if (tier == 2)
        {
            Fx.Shake(0.15f, 0.2f);
        }

        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        }

        if (giveReward)
        {
            GameManager.Instance.OnAsteroidKilled(currentReward);
        }

        Destroy(gameObject);
    }

    // Столкновение с кораблём игрока
    void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;

        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                player.TakeHit();
            }
            Die(false);
        }
    }

    void UpdateHpText()
    {
        if (hpText != null)
        {
            hpText.text = Mathf.Max(0, health).ToString();
        }
    }
}
